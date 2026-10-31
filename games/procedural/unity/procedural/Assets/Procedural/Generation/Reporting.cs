using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Procedural.Generation
{
    internal sealed class ReportData
    {
        internal MoistureMetrics Moisture;
        internal VegetationResult Vegetation;
        internal readonly List<Invariant> Invariants = new List<Invariant>();
        internal readonly List<string> Warnings = new List<string>();
        internal readonly List<double> BasinTheta = new List<double>();
        internal int BasinThetaSkipped;
        internal readonly List<MainStemFit> MainStems = new List<MainStemFit>();
        internal double MainStemHack = double.NaN, PreCarveMainStemHack = double.NaN;
        internal double LowlandShare, FloodplainShare, CoastSinuosity;
        internal double Land, ShapeLand, Theta = double.NaN, AllChannelTheta = double.NaN, MedianTheta = double.NaN, Hack = double.NaN;
        internal float ConcavityMinimumArea;
        internal int AcrossMouthSamples;
        internal double AcrossMouthAreaRatio, AcrossMouthLengthRatio;
        internal double SlopeP50, SlopeP90, RockShare, SandShare, FacetShare, Anisotropy, BeachShare;
    }

    internal sealed class Invariant
    {
        internal string Name, Detail;
        internal bool Passed;
    }

    internal struct Fit
    {
        internal double X, Y, XX, XY, MinX, MaxX, MinY, MaxY;
        internal int N;
        internal void Add(double x, double y)
        {
            x = Math.Log(x);
            y = Math.Log(y);
            if (N == 0)
            {
                MinX = MaxX = x;
                MinY = MaxY = y;
            }
            else
            {
                MinX = Math.Min(MinX, x);
                MaxX = Math.Max(MaxX, x);
                MinY = Math.Min(MinY, y);
                MaxY = Math.Max(MaxY, y);
            }

            X += x;
            Y += y;
            XX += x * x;
            XY += x * y;
            N++;
        }

        internal double Slope()
        {
            double den = N * XX - X * X;
            return N >= 3 && den > 1e-10 ? (N * XY - X * Y) / den : double.NaN;
        }
    }

    // Only JSON primitives are needed. Names and strings are fully escaped; nonfinite fits become null.
    internal sealed class TinyJson
    {
        private readonly StringBuilder b = new StringBuilder(4096);
        private readonly List<bool> first = new List<bool>();
        private bool named;
        private void Before()
        {
            if (named)
            {
                named = false;
                return;
            }

            int i = first.Count - 1;
            if (i >= 0)
            {
                if (!first[i])
                    b.Append(',');
                first[i] = false;
            }
        }

        internal void Name(string name)
        {
            Before();
            Quote(name);
            b.Append(':');
            named = true;
        }

        internal void Obj()
        {
            Before();
            b.Append('{');
            first.Add(true);
        }

        internal void EndObj()
        {
            b.Append('}');
            first.RemoveAt(first.Count - 1);
        }

        internal void Arr()
        {
            Before();
            b.Append('[');
            first.Add(true);
        }

        internal void EndArr()
        {
            b.Append(']');
            first.RemoveAt(first.Count - 1);
        }

        internal void String(string value)
        {
            Before();
            Quote(value);
        }

        internal void Number(double value)
        {
            Before();
            b.Append(double.IsNaN(value) || double.IsInfinity(value) ? "null" : value.ToString("R", CultureInfo.InvariantCulture));
        }

        internal void Bool(bool value)
        {
            Before();
            b.Append(value ? "true" : "false");
        }

        internal void P(string key, double value)
        {
            Name(key);
            Number(value);
        }

        internal void P(string key, string value)
        {
            Name(key);
            String(value);
        }

        internal void P(string key, bool value)
        {
            Name(key);
            Bool(value);
        }

        private void Quote(string value)
        {
            b.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        b.Append("\\\"");
                        break;
                    case '\\':
                        b.Append("\\\\");
                        break;
                    case '\n':
                        b.Append("\\n");
                        break;
                    case '\r':
                        b.Append("\\r");
                        break;
                    case '\t':
                        b.Append("\\t");
                        break;
                    default:
                        if (c < 32)
                            b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            b.Append(c);
                        break;
                }
            }

            b.Append('"');
        }

        public override string ToString()
        {
            return b.ToString();
        }
    }

    internal static class Reporting
    {
        private static void Add(ReportData report, string name, bool passed, string detail)
        {
            report.Invariants.Add(new Invariant { Name = name, Passed = passed, Detail = detail });
        }

        // Extraction-time state shared by the Build passes. One small object keeps
        // every invariant pass on the same arrays without widening a public type.
        private sealed class ReportBuild
        {
            internal readonly IslandResult Result;
            internal readonly TerrainState State;
            internal readonly WaterState Water;
            internal readonly CancellationToken Cancel;
            internal readonly ReportData Report = new ReportData();
            internal readonly Drainage D;
            internal readonly Drainage Channels;
            internal readonly Grid G;
            internal readonly int N;
            internal readonly int Land;
            internal readonly int[] Position;
            internal readonly int[] Basin;
            internal Fit Hack = new Fit();
            internal bool DrainageOk = true, Acyclic = true, Finite = true;
            internal double MouthsQ;
            internal int Mouths;
            internal float Peak;
            internal int ActualLand;

            internal ReportBuild(IslandResult result, TerrainState s, WaterState water, CancellationToken cancel)
            {
                Result = result;
                State = s;
                Water = water;
                Cancel = cancel;
                D = water.Routing;
                Channels = water.ChannelRouting;
                G = s.Height;
                N = G.Data.Length;
                Land = D.Stack.Length;
                Position = new int[N];
                Basin = new int[N];
                water.Report = Report;
            }
        }

        private sealed class RiverScan
        {
            internal bool Monotone = true, Beds = true, Sea = true, Junction = true;
            internal int DryPoints, SpillingBanks;
            internal string FirstDry = "";
        }

        internal static void Build(IslandResult result, TerrainState s, WaterState water, string[] names, double[] times, bool[] loaded, CancellationToken cancel)
        {
            var b = new ReportBuild(result, s, water, cancel);
            PrepareTraversal(b);
            MeasureDrainage(b);
            MeasureConcavity(b);
            MeasureRelief(b);
            CheckFlow(b);
            CheckRivers(b);
            CheckLakes(b);
            CheckSurfaces(b);
            CheckMoisture(b);
            Finish(b);
        }

        private static void PrepareTraversal(ReportBuild b)
        {
            for (int i = 0; i < b.N; i++)
            {
                MathCore.Check(b.Cancel, i);
                b.Position[i] = -1;
            }

            for (int k = 0; k < b.Land; k++)
            {
                MathCore.Check(b.Cancel, k);
                b.Position[b.D.Stack[k]] = k;
            }
        }

        private static void MeasureDrainage(ReportBuild b)
        {
            for (int k = 0; k < b.Land; k++)
            {
                MathCore.Check(b.Cancel, k);
                int i = b.D.Stack[k], rec = b.D.Receiver[i];
                if (rec < 0 || rec >= b.N)
                {
                    b.DrainageOk = false;
                    b.Acyclic = false;
                    continue;
                }

                if (b.D.Ocean[rec])
                {
                    b.Mouths++;
                    b.MouthsQ += b.D.Discharge[i];
                    if (b.D.Discharge[i] >= b.Land * b.G.CellSize * b.G.CellSize * 0.005f)
                        b.Hack.Add(b.D.Discharge[i], b.D.Length[i]);
                }
                else
                {
                    b.Acyclic &= b.Position[rec] >= 0 && b.Position[rec] < k;
                    b.DrainageOk &= b.Position[rec] >= 0 && b.Position[rec] < k;
                }
            }
        }

        private static void MeasureConcavity(ReportBuild b)
        {
            FitBasins(b, out List<Fit> fits, out List<bool> significant, out Fit pooled, out Fit allChannels);
            RecordConcavity(b, fits, significant, pooled, allChannels);
        }

        private static void FitBasins(ReportBuild b, out List<Fit> fits, out List<bool> significant, out Fit pooled, out Fit allChannels)
        {
            // Fit the terrain, channel graph and catchments at extraction. Hydraulic
            // bed cuts/freeboard are engineered slopes, not geomorphic concavity.
            // Final routing may move an edge to a neighbouring cell or basin; mixing
            // that graph's areas with the original channel mask makes spurious fits.
            var localFits = new List<Fit>();
            var localSignificant = new List<bool>();
            Fit localPooled = new Fit(), localAllChannels = new Fit();
            // Threshold hillslopes obey a slope limit, not a fluvial power law. Use
            // a fixed scale-independent catchment floor to fit the fluvial domain.
            b.Report.ConcavityMinimumArea = b.Channels.Stack.Length * b.G.CellSize * b.G.CellSize * 0.005f;
            for (int k = 0; k < b.Channels.Stack.Length; k++)
            {
                MathCore.Check(b.Cancel, k);
                int i = b.Channels.Stack[k], rec = b.Channels.Receiver[i];
                if (b.Channels.Ocean[rec])
                {
                    b.Basin[i] = localFits.Count;
                    localFits.Add(new Fit());
                    localSignificant.Add(b.Channels.Discharge[i] >= b.Channels.Stack.Length * b.G.CellSize * b.G.CellSize * 0.005f);
                }
                else
                    b.Basin[i] = b.Basin[rec];
                if (b.Water.Channel[i] && b.Water.LakeId[i] < 0 && b.Water.LakeId[rec] < 0)
                {
                    float slope = (b.Water.ChannelHeight[i] - Math.Max(0, b.Water.ChannelHeight[rec])) / b.Channels.Distance[i];
                    if (slope > 1e-6f)
                    {
                        localAllChannels.Add(b.Channels.Discharge[i], slope);
                        if (b.Channels.Discharge[i] >= b.Report.ConcavityMinimumArea)
                        {
                            localPooled.Add(b.Channels.Discharge[i], slope);
                            Fit f = localFits[b.Basin[i]];
                            f.Add(b.Channels.Discharge[i], slope);
                            localFits[b.Basin[i]] = f;
                        }
                    }
                }
            }

            fits = localFits;
            significant = localSignificant;
            pooled = localPooled;
            allChannels = localAllChannels;
        }

        private static void RecordConcavity(ReportBuild b, List<Fit> fits, List<bool> significant, Fit pooled, Fit allChannels)
        {
            for (int j = 0; j < fits.Count; j++)
            {
                Fit fit = fits[j];
                double theta = -fit.Slope();
                if (significant[j] && fit.N >= 20 && fit.MaxX - fit.MinX >= Math.Log(10) && !double.IsNaN(theta))
                    b.Report.BasinTheta.Add(theta);
                else
                    b.Report.BasinThetaSkipped++;
            }

            b.Report.BasinTheta.Sort();
            int count = b.Report.BasinTheta.Count;
            if (count > 0)
                b.Report.MedianTheta = count % 2 == 1 ? b.Report.BasinTheta[count / 2] : (b.Report.BasinTheta[count / 2 - 1] + b.Report.BasinTheta[count / 2]) * 0.5;
            b.Report.Theta = -pooled.Slope();
            b.Report.AllChannelTheta = -allChannels.Slope();
            b.Report.Hack = b.Hack.Slope();
            b.Report.AcrossMouthSamples = b.Hack.N;
            b.Report.AcrossMouthAreaRatio = Math.Exp(b.Hack.MaxX - b.Hack.MinX);
            b.Report.AcrossMouthLengthRatio = Math.Exp(b.Hack.MaxY - b.Hack.MinY);
        }

        private static void MeasureRelief(ReportBuild b)
        {
            for (int i = 0; i < b.N; i++)
            {
                MathCore.Check(b.Cancel, i);
                float h = b.G.Data[i];
                b.Finite &= !float.IsNaN(h) && !float.IsInfinity(h) && !float.IsNaN(b.D.Discharge[i]) && !float.IsInfinity(b.D.Discharge[i]);
                if (h > 0)
                    b.ActualLand++;
                b.Peak = Math.Max(b.Peak, h);
            }

            b.Report.Land = b.ActualLand / (double)b.N;
            ReliefMetrics.Measure(b.Result, b.Water, b.Report, b.Cancel);
            b.Report.Moisture = MoistureMetrics.Measure(b.Result, b.Water, b.Report, b.Cancel);
            MainStemMetrics.Measure(b.D, b.G, b.Report, b.Cancel);
            var preCarve = new ReportData();
            MainStemMetrics.Measure(b.Channels, b.G, preCarve, b.Cancel);
            b.Report.PreCarveMainStemHack = preCarve.MainStemHack;
            int simLand = 0;
            for (int i = 0; i < b.State.Mask.Data.Length; i++)
            {
                MathCore.Check(b.Cancel, i);
                if (b.State.Mask.Data[i] > 0)
                    simLand++;
            }

            b.Report.ShapeLand = simLand / (double)b.State.Mask.Data.Length;
        }

        private static void CheckFlow(ReportBuild b)
        {
            Add(b.Report, "finite samples", b.Finite, "Height and discharge contain only finite values.");
            Add(b.Report, "drainage reaches sea", b.DrainageOk, b.Land + " land cells in the sea-rooted donor traversal.");
            Add(b.Report, "no cycles", b.Acyclic, "Each land receiver precedes its donor in the stack.");
            Add(b.Report, "unexpected pits bounded", b.Water.Pits <= Math.Max(32, b.Land / 100), b.Water.Pits + " filled samples; limit is max(32, 1% of land samples).");
            double rainfall = b.Land * (double)b.G.CellSize * b.G.CellSize;
            Add(b.Report, "discharge conservation at mouths", Math.Abs(b.MouthsQ - rainfall) <= Math.Max(0.01, rainfall * 0.00002), "Mouth sum=" + Num(b.MouthsQ) + ", rain=" + Num(rainfall));
        }

        private static void CheckRivers(ReportBuild b)
        {
            RiverScan scan = ScanRivers(b);
            Add(b.Report, "river surface never rises downstream", scan.Monotone, "All smoothed polyline point pairs, tolerance 0.1 mm.");
            Add(b.Report, "bed below surface", scan.Beds, "All river points have positive depth.");
            Add(b.Report, "river corridor remains carved", scan.DryPoints == 0, scan.DryPoints + " polyline points above water on the final bilinear heightmap (5 cm tolerance)." + scan.FirstDry);
            Add(b.Report, "banks contain river surface", scan.SpillingBanks == 0, scan.SpillingBanks + " low raster bank samples; sea, lakes and intersecting channel corridors are open water.");
            Add(b.Report, "mouths at sea level", scan.Sea, b.Water.Mouths + " river mouths, tolerance 1 mm.");
            Add(b.Report, "confluence surfaces equal", scan.Junction, "Reach endpoints share downstream or lake water levels, tolerance 1 mm.");
        }

        private static RiverScan ScanRivers(ReportBuild b)
        {
            var scan = new RiverScan();
            for (int j = 0; j < b.Result.Rivers.Count; j++)
            {
                MathCore.Check(b.Cancel, j);
                RiverSegment river = b.Result.Rivers[j];
                int last = river.X.Length - 1;
                for (int k = 0; k <= last; k++)
                {
                    MathCore.Check(b.Cancel, k);
                    scan.Beds &= river.Bed[k] < river.Surface[k];
                    if (k > 0)
                        scan.Monotone &= river.Surface[k] <= river.Surface[k - 1] + 0.0001f;
                    int previous = Math.Max(0, k - 1), next = Math.Min(last, k + 1);
                    float tx = river.X[next] - river.X[previous], tz = river.Z[next] - river.Z[previous];
                    float tangentLength = (float)Math.Sqrt(tx * tx + tz * tz);
                    if (tangentLength > 0 && k > 0 && k < last)
                    {
                        float offset = river.Width[k] * 0.5f + b.G.CellSize;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            int bx = (int)Math.Round((river.X[k] - side * tz / tangentLength * offset) / b.G.CellSize);
                            int bz = (int)Math.Round((river.Z[k] + side * tx / tangentLength * offset) / b.G.CellSize);
                            if (bx < 0 || bz < 0 || bx >= b.G.Width || bz >= b.G.Height)
                                continue;
                            int bank = bz * b.G.Width + bx;
                            if (!b.State.Ocean[bank] && b.Water.LakeId[bank] < 0 && !b.Water.Corridor[bank] && b.G.Data[bank] < river.Surface[k] - 0.05f)
                                scan.SpillingBanks++;
                        }
                    }

                    float ground = MathCore.Sample(b.G, river.X[k] / b.G.CellSize, river.Z[k] / b.G.CellSize);
                    if (ground > river.Surface[k] + 0.05f)
                    {
                        scan.DryPoints++;
                        if (scan.FirstDry.Length == 0)
                        {
                            scan.FirstDry = " First: reach=" + j + ", point=" + k + "/" + last;
                            scan.FirstDry += ", fromLake=" + river.FromLake + ", toLake=" + river.ToLake;
                            scan.FirstDry += ", x=" + Num(river.X[k]) + ", z=" + Num(river.Z[k]);
                            scan.FirstDry += ", ground=" + Num(ground) + ", water=" + Num(river.Surface[k]);
                        }
                    }
                }

                if (river.ToSea)
                    scan.Sea &= Math.Abs(river.Surface[last]) < 0.001;
                if (river.Downstream >= 0)
                    scan.Junction &= river.Downstream < b.Result.Rivers.Count && Math.Abs(river.Surface[last] - b.Result.Rivers[river.Downstream].Surface[0]) < 0.001;
                else if (!river.ToSea && river.ToLake < 0)
                    scan.Junction = false;
                if (river.ToLake >= 0)
                    scan.Junction &= Math.Abs(river.Surface[last] - b.Result.Lakes[river.ToLake].Level) < 0.001;
                if (river.FromLake >= 0)
                    scan.Junction &= Math.Abs(river.Surface[0] - b.Result.Lakes[river.FromLake].Level) < 0.001;
            }

            return scan;
        }

        private static void CheckLakes(ReportBuild b)
        {
            foreach (Lake lake in b.Result.Lakes)
                lake.InflowCatchmentArea = 0;
            for (int k = 0; k < b.D.Stack.Length; k++)
            {
                MathCore.Check(b.Cancel, k);
                int i = b.D.Stack[k];
                int rec = b.D.Receiver[i];
                if (b.Water.LakeId[i] < 0 && b.Water.LakeId[rec] >= 0)
                    b.Result.Lakes[b.Water.LakeId[rec]].InflowCatchmentArea += b.D.Discharge[i];
            }

            foreach (Lake lake in b.Result.Lakes)
                if (lake.InflowChannelLength < b.Result.WorldSize * 0.1f || lake.InflowCatchmentArea < b.Result.Recipe.channelHeadArea)
                    b.Report.Warnings.Add("Lake " + lake.Id + " has insufficient inflowing channel length or catchment.");
            bool levels = true, outlets = true;
            var exits = new int[b.Result.Lakes.Count];
            for (int i = 0; i < b.N; i++)
            {
                MathCore.Check(b.Cancel, i);
                int id = b.Water.LakeId[i];
                if (id < 0)
                    continue;
                levels &= Math.Abs(b.D.Fill[i] - b.Result.Lakes[id].Level) < 0.001f;
                int rec = b.D.Receiver[i];
                if (rec < 0 || b.Water.LakeId[rec] != id)
                    exits[id]++;
            }

            for (int j = 0; j < b.Result.Lakes.Count; j++)
            {
                Lake lake = b.Result.Lakes[j];
                levels &= Math.Abs(b.G[lake.OutletX, lake.OutletZ] - lake.Level) < 0.001;
                lake.OutletCount = exits[j];
                outlets &= exits[j] == 1;
            }

            Add(b.Report, "lake level equals spill level after carving", levels, "All final flood levels and protected outlet terrain agree within 1 mm.");
            Add(b.Report, "exactly one lake outlet", outlets, "Receiver edges leaving each labelled basin.");
        }

        private static void CheckSurfaces(ReportBuild b)
        {
            bool weights = true;
            for (int i = 0; i < b.N; i++)
            {
                MathCore.Check(b.Cancel, i);
                float sum = 0;
                for (int j = 0; j < SurfaceLayer.Count; j++)
                {
                    float v = b.Result.Surface[j].Data[i];
                    weights &= v >= 0 && v <= 1;
                    sum += v;
                }

                weights &= Math.Abs(sum - 1) < 0.00001f;
            }

            Add(b.Report, "surface weights sum to 1", weights, "Nonnegative normalized weights at every height sample, tolerance 1e-5.");
        }

        private static void CheckMoisture(ReportBuild b)
        {
            bool moistureValid = b.Result.Wetness.Width == b.G.Width && b.Result.Wetness.Height == b.G.Height && b.Result.Wetness.CellSize == b.G.CellSize && b.Result.Moisture.Width == b.G.Width && b.Result.Moisture.Height == b.G.Height && b.Result.Moisture.CellSize == b.G.CellSize;
            for (int i = 0; i < b.N; i++)
            {
                MathCore.Check(b.Cancel, i);
                float wetness = b.Result.Wetness.Data[i], moisture = b.Result.Moisture.Data[i];
                moistureValid &= wetness >= 0 && wetness <= 1 && moisture >= 0 && moisture <= 1;
                if (b.D.Ocean[i] || b.Water.Moisture.Water[i])
                    moistureValid &= wetness == 1 && moisture == 1;
            }

            Add(b.Report, "wetness and moisture contract", moistureValid, "Height dimensions/cell size; finite values in 0..1; sea and open water are 1.");
            Add(b.Report, "MFD catchment conservation", Math.Abs(b.Water.Moisture.RainArea - b.Water.Moisture.OutletArea) <= Math.Max(1e-7, b.Water.Moisture.RainArea * 1e-10), "MFD outlet area=" + Num(b.Water.Moisture.OutletArea) + ", rain=" + Num(b.Water.Moisture.RainArea));
        }

        private static void Finish(ReportBuild b)
        {
            b.Result.Passed = true;
            for (int j = 0; j < b.Report.Invariants.Count; j++)
                b.Result.Passed &= b.Report.Invariants[j].Passed;
            if (b.Result.Lakes.Count != b.Result.Recipe.lakes)
                b.Report.Warnings.Add("Lake sites/valid single-outlet basins achieved " + b.Result.Lakes.Count + " of " + b.Result.Recipe.lakes + " requested.");
            if (Math.Abs(b.Water.Density - b.Result.Recipe.riverDensity) > Math.Max(0.001, b.Result.Recipe.riverDensity * 0.1))
                b.Report.Warnings.Add("River density is outside the 10% target; grid length quantization and mandatory lake outflows can limit calibration.");
            if (double.IsNaN(b.Report.Theta) || b.Report.Theta < 0.35 || b.Report.Theta > 0.7)
                b.Report.Warnings.Add("Pooled concavity is unavailable or outside 0.35–0.7.");
            if (double.IsNaN(b.Report.MedianTheta) || b.Report.MedianTheta < 0.35 || b.Report.MedianTheta > 0.7)
                b.Report.Warnings.Add("Median basin concavity is unavailable or outside 0.35–0.7.");
            if (double.IsNaN(b.Report.MainStemHack) || b.Report.MainStemHack < 0.5 || b.Report.MainStemHack > 0.65)
                b.Report.Warnings.Add("Median main-stem Hack exponent is unavailable or outside 0.5–0.65.");
            if (b.Water.MaxOrder < (b.Result.Recipe.preset == Preset.Small ? 3 : 4))
                b.Report.Warnings.Add("Maximum Strahler order is below the preset target.");
            if (Math.Abs(b.Peak - b.Result.Recipe.peakHeight) > 0.05f * b.Result.Recipe.peakHeight)
                b.Report.Warnings.Add("Achieved peak height is outside the requested +/-5% target; slope/coast constraints can limit relief.");
            if (Math.Abs(b.Report.Land - b.Result.Recipe.landFraction) > 0.01)
                b.Report.Warnings.Add("Final above-sea land fraction differs by more than 1 percentage point after coastal carving.");
        }

        private static string Num(double d)
        {
            return d.ToString("G8", CultureInfo.InvariantCulture);
        }

        internal static string Serialize(IslandResult result, TerrainState s, WaterState water, string[] names, double[] times, bool[] loaded)
        {
            ReportData report = water.Report;
            IslandRecipe r = result.Recipe;
            var j = new TinyJson();
            j.Obj();
            WriteRecipe(j, r);
            j.P("mode", result.Mode.ToString());
            j.P("resolution", result.Height.Width);
            j.P("passed", result.Passed);
            WriteStages(j, names, times, loaded);
            WriteTargets(j, result, report, water);
            WriteMetrics(j, result, report, water, s);
            WriteLakes(j, result);
            if (report.Vegetation != null)
                VegetationStage.Serialize(j, report.Vegetation);
            WriteInvariants(j, report);
            WriteWarnings(j, report);
            j.EndObj();
            return j.ToString();
        }

        private static void WriteRecipe(TinyJson j, IslandRecipe r)
        {
            j.Name("recipe");
            j.Obj();
            j.P("seed", r.seed);
            j.P("preset", r.preset.ToString());
            j.P("relief", r.relief.ToString());
            j.P("landFraction", r.landFraction);
            j.P("coastRoughness", r.coastRoughness);
            j.P("elongation", r.elongation);
            j.P("orientationDegrees", r.orientationDegrees);
            j.P("massifs", r.massifs);
            j.P("ruggedness", r.ruggedness);
            j.P("riverDensity", r.riverDensity);
            j.P("lakes", r.lakes);
            j.P("beachShare", r.beachShare);
            j.P("beachWidth", r.beachWidth);
            j.P("channelHeadArea", r.channelHeadArea);
            j.P("particleErosion", r.particleErosion);
            j.P("erosionDropletsPerSquareMetre", r.erosionDropletsPerSquareMetre);
            j.P("implicitTimeStep", r.implicitTimeStep);
            j.P("forestCover", r.forestCover);
            j.P("microRelief", r.microRelief);
            j.P("vegetation", r.vegetation);
            j.P("peakHeight", r.peakHeight);
            j.P("seaDepth", r.seaDepth);
            j.P("maxRiverWidth", r.maxRiverWidth);
            j.P("maxRiverDepth", r.maxRiverDepth);
            j.P("worldSize", r.worldSize);
            j.P("simResolution", r.simResolution);
            j.P("heightmapResolution", r.heightmapResolution);
            j.EndObj();
        }

        private static void WriteStages(TinyJson j, string[] names, double[] times, bool[] loaded)
        {
            j.Name("stages");
            j.Arr();
            for (int k = 0; k < names.Length; k++)
            {
                j.Obj();
                j.P("name", names[k]);
                j.P("ms", times[k]);
                j.P("loadedFromCheckpoint", loaded[k]);
                j.EndObj();
            }

            j.EndArr();
        }

        private static void WriteTargets(TinyJson j, IslandResult result, ReportData report, WaterState water)
        {
            IslandRecipe r = result.Recipe;
            j.Name("targets");
            j.Obj();
            j.P("landFractionRequested", r.landFraction);
            j.P("landFractionAchieved", report.Land);
            j.P("simLandFractionAchieved", report.ShapeLand);
            j.P("lakesRequested", r.lakes);
            j.P("lakesAchieved", result.Lakes.Count);
            j.P("riverDensityRequested", r.riverDensity);
            j.P("riverDensityAchieved", water.Density);
            j.P("peakHeightRequested", r.peakHeight);
            j.P("peakHeightAchieved", result.PeakHeight);
            j.P("peakHeightWithinTolerance", Math.Abs(result.PeakHeight - r.peakHeight) <= 0.05f * r.peakHeight);
            j.EndObj();
        }

        private static void WriteMetrics(TinyJson j, IslandResult result, ReportData report, WaterState water, TerrainState s)
        {
            j.Name("metrics");
            j.Obj();
            WriteShapeMetrics(j, report, water);
            WriteConcavityMetrics(j, report);
            WriteHackMetrics(j, result, report);
            WriteNetworkMetrics(j, result, report, water, s);
            j.EndObj();
        }

        private static void WriteShapeMetrics(TinyJson j, ReportData report, WaterState water)
        {
            report.Moisture.Serialize(j, water.Moisture);
            j.P("landSlopeP50Degrees", report.SlopeP50);
            j.P("landSlopeP90Degrees", report.SlopeP90);
            j.P("rockShareOfLand", report.RockShare);
            j.P("sandShareOfLand", report.SandShare);
            j.P("facetShare", report.FacetShare);
            j.P("d8Anisotropy", report.Anisotropy);
            j.P("beachShareOfCoastline", report.BeachShare);
            j.P("lowlandShareOfLand", report.LowlandShare);
            j.P("floodplainShareOfLand", report.FloodplainShare);
            j.P("coastSinuosity", report.CoastSinuosity);
            j.P("coastLengthEstimator", "Four-direction Crofton perimeter / equal-area-circle perimeter");
        }

        private static void WriteConcavityMetrics(TinyJson j, ReportData report)
        {
            j.P("concavityMeasurement", "Selected channel terrain/catchments before carving, catchment >= 0.5% of land area");
            j.P("concavityThetaPooled", report.Theta);
            j.P("concavityThetaAllSelectedChannels", report.AllChannelTheta);
            j.P("concavityMinimumCatchmentArea", report.ConcavityMinimumArea);
            j.P("concavityThetaBasinMedian", report.MedianTheta);
            j.Name("concavityThetaPerBasin");
            j.Arr();
            for (int k = 0; k < report.BasinTheta.Count; k++)
                j.Number(report.BasinTheta[k]);
            j.EndArr();
            j.P("concavityThetaBasinsSkipped", report.BasinThetaSkipped);
            j.P("concavityThetaMinimumSamples", 20);
            j.P("concavityThetaMinimumAreaDecades", 1);
        }

        private static void WriteHackMetrics(TinyJson j, IslandResult result, ReportData report)
        {
            j.P("hackExponentAcrossMouths", report.Hack);
            j.P("hackAcrossMouthSamples", report.AcrossMouthSamples);
            j.P("hackAcrossMouthAreaMaxMinRatio", report.AcrossMouthAreaRatio);
            j.P("hackAcrossMouthLengthMaxMinRatio", report.AcrossMouthLengthRatio);
            j.P("hackExponentMainStemMedian", report.MainStemHack);
            j.P("hackExponentMainStemBeforeCarving", report.PreCarveMainStemHack);
            j.P("hackMainStemMeasurement", "Final routing; mouths >=5% of land; largest-area donor; longest upstream length versus area; area >= max(16 cells, 0.1% basin); >=20 samples and one area decade");
            j.Name("hackMainStemPerBasin");
            j.Arr();
            foreach (MainStemFit fit in report.MainStems)
            {
                j.Obj();
                j.P("mouthX", fit.Mouth % result.Height.Width);
                j.P("mouthZ", fit.Mouth / result.Height.Width);
                j.P("area", fit.Area);
                j.P("samples", fit.Samples);
                j.P("areaDecades", fit.AreaDecades);
                j.P("exponent", fit.Exponent);
                j.EndObj();
            }

            j.EndArr();
        }

        private static void WriteNetworkMetrics(TinyJson j, IslandResult result, ReportData report, WaterState water, TerrainState s)
        {
            j.P("maxStrahlerOrder", water.MaxOrder);
            j.P("numberOfMouths", water.Mouths);
            j.P("pitsFilledAfterCarving", water.Pits);
            j.P("lakeCandidates", water.LakeCandidates);
            j.P("damAttempts", water.DamAttempts);
            j.P("damFullFloodChecks", water.DamFloodChecks);
            j.P("damFullFloodBudget", water.DamFloodBudget);
            j.P("damRejectedWidth", water.DamNarrow);
            j.P("damRejectedOpen", water.DamOpen);
            j.P("damRejectedSmall", water.DamSmall);
            j.P("damRejectedLarge", water.DamLarge);
            j.P("damRejectedOverlap", water.DamOverlap);
            j.P("damRejectedInflow", water.DamInflow);
            j.P("landFraction", report.Land);
            j.P("peakHeight", result.PeakHeight);
            j.P("reliefIterations", s.Iterations);
            j.P("reliefStableIterations", s.StableIterations);
            j.P("receiversChangedFraction", s.ChangedAll);
            j.P("receiverFlipsWithNonDescendingOldEdge", s.FlipsFromUphill);
            j.P("receiverFlipsWithinFivePercentSlopeTie", s.FlipsFromNearTies);
            j.P("channelReceiversChangedFraction", s.Changed);
            j.P("channelNetworkDisplacedFraction", s.NetworkChanged);
            j.P("reliefRelativeMeanHeightChange", s.RelativeHeightChange);
            j.P("reliefFixedPointResidual", s.FixedPointResidual);
            j.P("reliefMaximumUpdateAfterWarmStartFractionOfPeak", 0);
            j.P("reliefUsesUpdateBound", false);
            j.P("reliefHillslopeNetworkFixed", true);
            j.P("reliefConvergedOnChannels", s.StableIterations >= 3);
            j.P("reliefConvergenceMeasurement", "Pure stream-power growth from seeded noise; no height damping, hillslope branch, slope cap or update bound during growth. Stop after three consecutive updates with <1% receiver switches on land and channels A>=Ac and mean absolute raw height change / mean raw height <0.1%; max 100 Preview / 250 Bake. Channel flips use the union of old/new channel cells. Fixed-network hillslope/peak shaping and final smoothing are separate; these metrics do not claim free-routing equilibrium of the shaped output.");
        }

        private static void WriteLakes(TinyJson j, IslandResult result)
        {
            j.Name("lakes");
            j.Arr();
            foreach (Lake lake in result.Lakes)
            {
                j.Obj();
                j.P("id", lake.Id);
                j.P("area", lake.Area);
                j.P("level", lake.Level);
                j.P("inflowCatchmentArea", lake.InflowCatchmentArea);
                j.P("inflowChannelLength", lake.InflowChannelLength);
                j.P("coastDistance", lake.CoastDistance);
                j.P("outletCount", lake.OutletCount);
                j.EndObj();
            }

            j.EndArr();
        }

        private static void WriteInvariants(TinyJson j, ReportData report)
        {
            j.Name("invariants");
            j.Arr();
            for (int k = 0; k < report.Invariants.Count; k++)
            {
                Invariant v = report.Invariants[k];
                j.Obj();
                j.P("name", v.Name);
                j.P("passed", v.Passed);
                j.P("detail", v.Detail);
                j.EndObj();
            }

            j.EndArr();
        }

        private static void WriteWarnings(TinyJson j, ReportData report)
        {
            j.Name("warnings");
            j.Arr();
            for (int k = 0; k < report.Warnings.Count; k++)
                j.String(report.Warnings[k]);
            j.EndArr();
        }

    }
}

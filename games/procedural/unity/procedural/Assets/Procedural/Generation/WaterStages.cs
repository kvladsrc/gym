using System;
using System.Collections.Generic;
using System.Threading;

namespace Procedural.Generation
{
    internal sealed class WaterState
    {
        internal Drainage Routing, ChannelRouting;
        internal ReportData Report;
        internal MoistureState Moisture;
        internal List<Lake> Lakes = new List<Lake>();
        internal List<RiverSegment> Rivers = new List<RiverSegment>();
        internal int[] LakeId;
        internal bool[] Channel, Corridor, Floodplain;
        internal float[] BankTarget, CarveBase, ChannelDistance, CarveScore;
        internal float[] RimFloor;
        internal float[] Surface, Depth, Width, ChannelHeight;
        internal float Density;
        internal int Pits, MaxOrder, Mouths;
        internal int DamFloodChecks, DamFloodBudget;
        internal int LakeCandidates, DamAttempts, DamNarrow, DamOpen, DamSmall, DamLarge, DamOverlap, DamInflow;
    }

    internal static partial class WaterStages
    {
        /// <summary>Reference state shared by the river-extraction steps.</summary>
        private sealed class RiverContext
        {
            internal readonly IslandRecipe Recipe;
            internal readonly TerrainState State;
            internal readonly WaterState Water;
            internal readonly Grid Grid;
            internal readonly Drainage Drainage;
            internal readonly CancellationToken Cancel;
            internal readonly int CellCount, Width;
            internal readonly float Cell;

            internal RiverContext(IslandRecipe recipe, TerrainState state, WaterState water, CancellationToken cancel)
            {
                Recipe = recipe;
                State = state;
                Water = water;
                Grid = state.Height;
                Drainage = water.Routing;
                Cancel = cancel;
                CellCount = state.Height.Data.Length;
                Width = state.Height.Width;
                Cell = state.Height.CellSize;
            }
        }

        private static float ChannelLength(Drainage d, int[] lakes, bool[] mandatory, bool[] eligible, float threshold, int width, float cell, CancellationToken cancel)
        {
            double length = 0;
            for (int k = 0; k < d.Stack.Length; k++)
            {
                MathCore.Check(cancel, k);
                int i = d.Stack[k];
                if ((!eligible[i] || d.Discharge[i] < threshold) && !mandatory[i])
                    continue;
                int rec = d.Receiver[i];
                if (lakes[i] >= 0)
                {
                    if (lakes[rec] < 0)
                        length += d.Distance[i];
                    continue;
                }

                length += d.Distance[i];
                int donors = 0, donor = -1;
                for (int j = d.DonorHead[i]; j >= 0; j = d.DonorNext[j])
                    if ((eligible[j] && d.Discharge[j] >= threshold) || mandatory[j])
                    {
                        donors++;
                        donor = j;
                    }

                if (donors != 1)
                    continue;
                float vx = i % width - donor % width, vz = i / width - donor / width;
                float wx = rec % width - i % width, wz = rec / width - i / width;
                // Exact length removed at an interior vertex by two Chaikin passes.
                double smooth = Norm(3 * vx + wx, 3 * vz + wz) / 16 + Norm(vx + wx, vz + wz) / 8 + Norm(vx + 3 * wx, vz + 3 * wz) / 16;
                length -= cell * (0.375 * (Norm(vx, vz) + Norm(wx, wz)) - smooth);
            }

            return (float)length;
        }

        private static double Norm(float x, float z)
        {
            return Math.Sqrt(x * x + z * z);
        }

        internal static void Rivers(IslandRecipe r, TerrainState s, WaterState water, CancellationToken cancel)
        {
            Grid g = s.Height;
            water.ChannelRouting = water.Routing;
            water.ChannelHeight = (float[])g.Data.Clone();
            var ctx = new RiverContext(r, s, water, cancel);
            ResolveEligibleBasins(ctx, out float area, out float target, out bool[] eligible);
            bool[] mandatory = MarkMandatoryReaches(ctx, target);
            float threshold = FindChannelThreshold(ctx, area, target, eligible, mandatory);
            float maxQ = AllocateChannelFields(ctx, target, threshold, eligible, mandatory);
            int[] donorCount = AssignChannelProfiles(ctx, maxQ);
            ExtractRiverReaches(ctx, donorCount, out int[] starts, out List<int> ends);
            LinkRiverReaches(ctx, starts, ends);
            MeasureRiverDensity(ctx, area);
            SmoothAndCarveRivers(ctx);
        }

        private static void ResolveEligibleBasins(RiverContext ctx, out float area, out float target, out bool[] eligible)
        {
            Drainage d = ctx.Drainage;
            float cell = ctx.Cell;
            area = d.Stack.Length * cell * cell;
            target = ctx.Recipe.riverDensity * area / 1000;
            // Resolve basin membership receiver-first. Reserve the length budget for
            // coherent drainage trees, rather than hundreds of short coastal fans.
            var basinAreas = new List<float>();
            for (int k = 0; k < d.Stack.Length; k++)
            {
                MathCore.Check(ctx.Cancel, k);
                int i = d.Stack[k];
                if (ctx.State.Ocean[d.Receiver[i]])
                    basinAreas.Add(d.Discharge[i]);
            }

            basinAreas.Sort((a, b) => b.CompareTo(a));
            // At this island scale the default budget supports a few mature rivers.
            // Keep up to two large basins so extra length grows nested tributaries.
            float minimumBasin = basinAreas.Count == 0 ? area : Math.Max(Math.Min(area * 0.05f, basinAreas[0]), basinAreas[Math.Min(1, basinAreas.Count - 1)]);
            eligible = new bool[ctx.CellCount];
            for (int k = 0; k < d.Stack.Length; k++)
            {
                MathCore.Check(ctx.Cancel, k);
                int i = d.Stack[k], rec = d.Receiver[i];
                eligible[i] = ctx.State.Ocean[rec] ? d.Discharge[i] >= minimumBasin : eligible[rec];
            }
        }

        private static bool[] MarkMandatoryReaches(RiverContext ctx, float target)
        {
            IslandRecipe r = ctx.Recipe;
            Drainage d = ctx.Drainage;
            WaterState water = ctx.Water;
            TerrainState s = ctx.State;
            int w = ctx.Width;
            var mandatory = new bool[ctx.CellCount];
            if (target > 0)
                for (int j = 0; j < water.Lakes.Count; j++)
                {
                    Lake lake = water.Lakes[j];
                    int i = lake.OutletZ * w + lake.OutletX, steps = 0;
                    while (!s.Ocean[i])
                    {
                        MathCore.Check(ctx.Cancel, steps++);
                        mandatory[i] = true;
                        i = d.Receiver[i];
                    }

                    // Choose a real entering river, rather than following donors
                    // through a lake's flat flood-parent tree. The latter can lead
                    // to a short side inlet even when a large upstream river exists.
                    int inlet = -1;
                    foreach (int candidate in d.Stack)
                    {
                        int rec = d.Receiver[candidate];
                        if (water.LakeId[candidate] < 0 && water.LakeId[rec] == lake.Id && (inlet < 0 || d.Discharge[candidate] > d.Discharge[inlet]))
                            inlet = candidate;
                    }

                    i = inlet;
                    float upstream = 0;
                    while (i >= 0 && upstream < r.worldSize * 0.14f)
                    {
                        MathCore.Check(ctx.Cancel, steps++);
                        mandatory[i] = true;
                        upstream += d.Distance[i];
                        int donor = -1;
                        for (int donorCell = d.DonorHead[i]; donorCell >= 0; donorCell = d.DonorNext[donorCell])
                            if (donor < 0 || d.Discharge[donorCell] > d.Discharge[donor])
                                donor = donorCell;
                        i = donor;
                    }
                }

            return mandatory;
        }

        private static float FindChannelThreshold(RiverContext ctx, float area, float target, bool[] eligible, bool[] mandatory)
        {
            float low = ctx.Cell * ctx.Cell, high = area + ctx.Cell * ctx.Cell, threshold = high, error = float.MaxValue;
            if (target > 0)
                for (int iter = 0; iter < 32; iter++)
                {
                    ctx.Cancel.ThrowIfCancellationRequested();
                    float mid = (low + high) * 0.5f, len = ChannelLength(ctx.Drainage, ctx.Water.LakeId, mandatory, eligible, mid, ctx.Width, ctx.Cell, ctx.Cancel), err = Math.Abs(len - target);
                    if (err < error)
                    {
                        error = err;
                        threshold = mid;
                    }

                    if (len > target)
                        low = mid;
                    else
                        high = mid;
                }

            return threshold;
        }

        private static float AllocateChannelFields(RiverContext ctx, float target, float threshold, bool[] eligible, bool[] mandatory)
        {
            WaterState water = ctx.Water;
            Drainage d = ctx.Drainage;
            int n = ctx.CellCount;
            water.Channel = new bool[n];
            water.Corridor = new bool[n];
            water.Floodplain = new bool[n];
            water.BankTarget = new float[n];
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(ctx.Cancel, i);
                water.BankTarget[i] = float.MinValue;
            }

            water.Surface = new float[n];
            water.Depth = new float[n];
            water.Width = new float[n];
            float maxQ = 0;
            for (int k = 0; k < d.Stack.Length; k++)
            {
                MathCore.Check(ctx.Cancel, k);
                int i = d.Stack[k];
                water.Channel[i] = ((eligible[i] && d.Discharge[i] >= threshold) || mandatory[i]) && target > 0;
                if (water.Channel[i])
                    maxQ = Math.Max(maxQ, d.Discharge[i]);
            }

            return maxQ;
        }

        private static int[] AssignChannelProfiles(RiverContext ctx, float maxQ)
        {
            IslandRecipe r = ctx.Recipe;
            TerrainState s = ctx.State;
            WaterState water = ctx.Water;
            Grid g = ctx.Grid;
            Drainage d = ctx.Drainage;
            int n = ctx.CellCount, w = ctx.Width;
            float cell = ctx.Cell;
            var upstreamLength = new float[n];
            float[] coastDistance = TerrainStages.DistanceTo(s.Ocean, w, w, ctx.Cancel);
            for (int k = d.Stack.Length - 1; k >= 0; k--)
            {
                MathCore.Check(ctx.Cancel, k);
                int i = d.Stack[k];
                int rec = d.Receiver[i];
                if (water.Channel[i] && water.LakeId[i] < 0)
                {
                    upstreamLength[rec] = Math.Max(upstreamLength[rec], upstreamLength[i] + d.Distance[i]);
                    if (water.LakeId[rec] >= 0)
                    {
                        Lake lake = water.Lakes[water.LakeId[rec]];
                        lake.InflowChannelLength = Math.Max(lake.InflowChannelLength, upstreamLength[i] + d.Distance[i]);
                    }
                }
            }

            foreach (Lake lake in water.Lakes)
                lake.CoastDistance = coastDistance[lake.OutletZ * w + lake.OutletX] * cell;
            var donorCount = new int[n];
            for (int k = 0; k < d.Stack.Length; k++)
            {
                MathCore.Check(ctx.Cancel, k);
                int i = d.Stack[k], rec = d.Receiver[i];
                if (water.Channel[i] && !s.Ocean[rec] && water.Channel[rec])
                    donorCount[rec]++;
                float q = maxQ > 0 ? MathCore.Clamp(d.Discharge[i] / maxQ, 0, 1) : 0;
                float rawWidth = Math.Max(2 * cell, r.maxRiverWidth * (float)Math.Sqrt(q));
                float downstreamWidth = water.Width[rec] > 0 ? water.Width[rec] : rawWidth;
                float widthBlend = (float)Math.Exp(-d.Distance[i] / Math.Max(3 * cell, r.maxRiverWidth * 1.5f));
                water.Width[i] = rawWidth * (1 - widthBlend) + downstreamWidth * widthBlend;
                water.Depth[i] = r.maxRiverDepth * (float)Math.Pow(q, 0.4);
                if (water.LakeId[i] >= 0)
                    water.Surface[i] = water.Lakes[water.LakeId[i]].Level;
                else if (s.Ocean[rec])
                    water.Surface[i] = 0;
                else
                    water.Surface[i] = Math.Max(water.Surface[rec], Math.Max(g.Data[i] - water.Depth[i] * 0.5f, water.RimFloor[i] + water.Depth[i]));
            }

            return donorCount;
        }

        private static void ExtractRiverReaches(RiverContext ctx, int[] donorCount, out int[] starts, out List<int> ends)
        {
            IslandRecipe r = ctx.Recipe;
            TerrainState s = ctx.State;
            WaterState water = ctx.Water;
            Grid g = ctx.Grid;
            Drainage d = ctx.Drainage;
            starts = new int[ctx.CellCount];
            for (int i = 0; i < starts.Length; i++)
            {
                MathCore.Check(ctx.Cancel, i);
                starts[i] = -1;
            }

            int[] order = Hydrology.Strahler(d, water.Channel, ctx.Cancel);
            ends = new List<int>();
            for (int k = d.Stack.Length - 1; k >= 0; k--)
            {
                MathCore.Check(ctx.Cancel, k);
                int start = d.Stack[k];
                if (!water.Channel[start] || water.LakeId[start] >= 0)
                    continue;
                bool fromLake = HasLakeDonor(d, water, start);
                if (donorCount[start] == 1 && !fromLake)
                    continue;
                var cells = new List<int>();
                int from = -1;
                if (fromLake && donorCount[start] > 1)
                    AddLakeOutletReaches(ctx, order, start, ends);
                else if (fromLake)
                {
                    for (int j = d.DonorHead[start]; j >= 0; j = d.DonorNext[j])
                        if (water.LakeId[j] >= 0)
                        {
                            from = water.LakeId[j];
                            cells.Add(j);
                            break;
                        }
                }

                int c = WalkToReachEnd(ctx, start, cells, donorCount);
                int id = water.Rivers.Count;
                starts[start] = id;
                ends.Add(c);
                var segment = new RiverSegment
                {
                    Id = id,
                    Order = order[start],
                    FromLake = from,
                    ToLake = water.LakeId[c],
                    ToSea = s.Ocean[c]
                };
                BuildPolyline(r, segment, cells, g, water, d, ctx.Cancel);
                water.Rivers.Add(segment);
                water.MaxOrder = Math.Max(water.MaxOrder, segment.Order);
                if (segment.ToSea)
                    water.Mouths++;
            }
        }

        private static bool HasLakeDonor(Drainage d, WaterState water, int cell)
        {
            for (int j = d.DonorHead[cell]; j >= 0; j = d.DonorNext[j])
                if (water.LakeId[j] >= 0)
                    return true;
            return false;
        }

        private static void AddLakeOutletReaches(RiverContext ctx, int[] order, int start, List<int> ends)
        {
            WaterState water = ctx.Water;
            Drainage d = ctx.Drainage;
            // A tributary can join immediately below a lake's step. Keep the
            // lake-to-junction reach separate so confluence endpoints coincide.
            for (int j = d.DonorHead[start]; j >= 0; j = d.DonorNext[j])
            {
                if (water.LakeId[j] < 0)
                    continue;
                var outletReach = new RiverSegment
                {
                    Id = water.Rivers.Count,
                    Order = Math.Max(1, order[j]),
                    FromLake = water.LakeId[j]
                };
                BuildPolyline(ctx.Recipe, outletReach, new List<int> { j, start }, ctx.Grid, water, d, ctx.Cancel);
                water.Rivers.Add(outletReach);
                ends.Add(start);
            }
        }

        private static int WalkToReachEnd(RiverContext ctx, int start, List<int> cells, int[] donorCount)
        {
            WaterState water = ctx.Water;
            TerrainState s = ctx.State;
            Drainage d = ctx.Drainage;
            int c = start;
            cells.Add(c);
            while (true)
            {
                MathCore.Check(ctx.Cancel, cells.Count);
                int rec = d.Receiver[c];
                cells.Add(rec);
                if (s.Ocean[rec] || water.LakeId[rec] >= 0 || donorCount[rec] != 1)
                {
                    c = rec;
                    break;
                }

                bool lakeIn = false;
                for (int j = d.DonorHead[rec]; j >= 0; j = d.DonorNext[j])
                    if (water.LakeId[j] >= 0)
                        lakeIn = true;
                if (lakeIn)
                {
                    c = rec;
                    break;
                }

                c = rec;
            }

            return c;
        }

        private static void LinkRiverReaches(RiverContext ctx, int[] starts, List<int> ends)
        {
            WaterState water = ctx.Water;
            for (int j = 0; j < water.Rivers.Count; j++)
                if (!water.Rivers[j].ToSea && water.Rivers[j].ToLake < 0)
                    water.Rivers[j].Downstream = starts[ends[j]];
        }

        private static void MeasureRiverDensity(RiverContext ctx, float area)
        {
            WaterState water = ctx.Water;
            double smoothedLength = 0;
            for (int j = 0; j < water.Rivers.Count; j++)
            {
                RiverSegment segment = water.Rivers[j];
                for (int k = 1; k < segment.X.Length; k++)
                {
                    MathCore.Check(ctx.Cancel, k);
                    smoothedLength += Norm(segment.X[k] - segment.X[k - 1], segment.Z[k] - segment.Z[k - 1]);
                }
            }

            water.Density = area > 0 ? (float)(smoothedLength * 1000 / area) : 0;
        }

        private static void SmoothAndCarveRivers(RiverContext ctx)
        {
            WaterState water = ctx.Water;
            Grid g = ctx.Grid;
            TerrainState s = ctx.State;
            int n = ctx.CellCount;
            // The same continuous curves define water ribbons, valley floors and beds.
            // At junctions every incoming endpoint lies at the downstream centreline,
            // inside its wetted width; no separate raster-cell trench remains.
            foreach (RiverSegment river in water.Rivers)
                SmoothProfile(river, ctx.Cancel);
            CarveRiverCurves(ctx);
            // Low banks beside protected lake rims need a small containment berm.
            // Apply after all reaches so a tributary is never dammed by another bank.
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(ctx.Cancel, i);
                if (!water.Corridor[i] && !s.Ocean[i] && water.LakeId[i] < 0)
                    g.Data[i] = Math.Max(g.Data[i], water.BankTarget[i]);
            }

            ReconcileProfiles(g, water, ctx.Cancel);
            ContainBanks(g, water, s.Ocean, ctx.Cancel);
        }

        private static void CarveRiverCurves(RiverContext ctx)
        {
            WaterState water = ctx.Water;
            Grid g = ctx.Grid;
            water.CarveBase = (float[])g.Data.Clone();
            water.ChannelDistance = new float[ctx.CellCount];
            water.CarveScore = new float[ctx.CellCount];
            Array.Fill(water.CarveScore, float.PositiveInfinity);
            Array.Fill(water.ChannelDistance, float.PositiveInfinity);
            foreach (RiverSegment river in water.Rivers)
                for (int k = 0; k < river.X.Length - 1; k++)
                    CarveCurve(ctx.Recipe, river, k, g, ctx.State.Ocean, water, ctx.Cancel);
        }

        private static void SmoothProfile(RiverSegment river, CancellationToken cancel)
        {
            // A 10 m triangular longitudinal filter removes receiver-cell steps.
            // Arc length makes the support independent of ribbon resampling.
            float[] original = (float[])river.Surface.Clone();
            int last = original.Length - 1;
            var arc = new double[original.Length];
            for (int k = 1; k <= last; k++)
                arc[k] = arc[k - 1] + Norm(river.X[k] - river.X[k - 1], river.Z[k] - river.Z[k - 1]);
            int first = 0, end = 0;
            for (int k = 1; k < last; k++)
            {
                MathCore.Check(cancel, k);
                while (arc[k] - arc[first] > 10)
                    first++;
                while (end < last && arc[end + 1] - arc[k] < 10)
                    end++;
                double sum = 0, weights = 0;
                for (int j = first; j <= end; j++)
                {
                    double weight = Math.Max(0, 10 - Math.Abs(arc[j] - arc[k]));
                    sum += original[j] * weight;
                    weights += weight;
                }

                river.Surface[k] = MathCore.Clamp((float)(sum / weights), original[last], original[0]);
            }

            // Keep exact lake/junction endpoints and monotonicity after filtering.
            for (int k = last - 1; k > 0; k--)
            {
                river.Surface[k] = Math.Max(river.Surface[k], river.Surface[k + 1]);
                river.Bed[k] = river.Surface[k] - river.Depth[k];
            }
        }

        internal static void CarveCurve(IslandRecipe recipe, RiverSegment river, int k, Grid g, bool[] ocean, WaterState water, CancellationToken cancel)
        {
            int w = g.Width;
            float cell = g.CellSize;
            float ax = river.X[k] / cell, az = river.Z[k] / cell;
            float bx = river.X[k + 1] / cell, bz = river.Z[k + 1] / cell;
            float dx = bx - ax, dz = bz - az, length2 = dx * dx + dz * dz;
            if (length2 < 1e-12f)
                return;
            float maxWidth = Math.Max(river.Width[k], river.Width[k + 1]);
            float grade = (river.Surface[k] - river.Surface[k + 1]) / (cell * (float)Math.Sqrt(length2));
            // Steep rapids remain gorges; alluvial treads develop on lower-gradient reaches.
            float discharge = Math.Max(river.Discharge[k], river.Discharge[k + 1]);
            float headBlend = MathCore.Smooth(recipe.channelHeadArea * 0.5f, recipe.channelHeadArea, discharge);
            float gradeBlend = 1 - MathCore.Smooth(0.08f, 0.16f, grade);
            float alluvialBlend = headBlend * gradeBlend;
            bool alluvial = alluvialBlend > 0;
            // Leopold–Maddock width scaling: both channel and alluvial floor ~ Q^0.5.
            float reach = alluvial ? maxWidth * 6.5f / cell : maxWidth * 1.2f / cell + 5;
            int minX = Math.Max(0, (int)Math.Floor(Math.Min(ax, bx) - reach)), maxX = Math.Min(w - 1, (int)Math.Ceiling(Math.Max(ax, bx) + reach));
            int minZ = Math.Max(0, (int)Math.Floor(Math.Min(az, bz) - reach)), maxZ = Math.Min(g.Height - 1, (int)Math.Ceiling(Math.Max(az, bz) + reach));
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                {
                    int i = z * w + x;
                    MathCore.Check(cancel, i);
                    if (water.LakeId[i] >= 0 || ocean[i])
                        continue;
                    float t = MathCore.Clamp(((x - ax) * dx + (z - az) * dz) / length2, 0, 1);
                    float px = ax + t * dx, pz = az + t * dz;
                    float distance = cell * (float)Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
                    float width = river.Width[k] * (1 - t) + river.Width[k + 1] * t;
                    float radius = width * 0.5f;
                    float bank = radius + Math.Max(4 * cell, width * 0.65f);
                    float surface = river.Surface[k] * (1 - t) + river.Surface[k + 1] * t;
                    float depth = river.Depth[k] * (1 - t) + river.Depth[k + 1] * t;
                    if (water.ChannelDistance != null)
                        water.ChannelDistance[i] = Math.Min(water.ChannelDistance[i], Math.Max(0, distance - radius));
                    float original = water.CarveBase == null ? g.Data[i] : water.CarveBase[i];
                    if (alluvial && distance < width * 6.5f && water.RimFloor[i] == float.MinValue)
                    {
                        float blend = alluvialBlend * (1 - MathCore.Smooth(width * 3.5f, width * 6.5f, distance));
                        float floor = surface + 0.35f + distance * 0.002f;
                        if (floor < g.Data[i])
                        {
                            g.Data[i] = Math.Min(g.Data[i], original + (floor - original) * blend);
                            if (blend > 0.95f && distance > bank)
                                water.Floodplain[i] = true;
                        }
                    }

                    if (distance > bank)
                        continue;
                    // Select one cross section from the nearest width-normalized
                    // curve point. Min-cutting every nearby span lets distant,
                    // lower downstream samples scallop the banks on a steep reach.
                    float score = distance / width;
                    if (water.CarveScore != null)
                    {
                        if (score >= water.CarveScore[i])
                            continue;
                        water.CarveScore[i] = score;
                        water.BankTarget[i] = float.MinValue;
                    }

                    if (distance <= radius)
                        water.Corridor[i] = true;
                    else
                        water.BankTarget[i] = Math.Max(water.BankTarget[i], surface + 0.15f);
                    float profile = surface - depth + (depth + 0.15f) * Math.Min(1, distance * distance / (radius * radius));
                    float blendBank = 1 - MathCore.Smooth(radius, bank, distance);
                    float cut = Math.Max(profile, water.RimFloor[i]);
                    if (cut < g.Data[i])
                        g.Data[i] = Math.Min(g.Data[i], original + (cut - original) * blendBank);
                }
        }

        private static void BuildPolyline(IslandRecipe recipe, RiverSegment segment, List<int> cells, Grid g, WaterState water, Drainage d, CancellationToken cancel)
        {
            int count = cells.Count;
            var x = new float[count];
            var z = new float[count];
            var location = new float[count];
            for (int j = 0; j < count; j++)
            {
                x[j] = cells[j] % g.Width;
                z[j] = cells[j] / g.Width;
                location[j] = j;
            }

            DisplaceMeander(recipe, cells, g, water, d, x, z, count, cancel);
            SubdivideCurve(x, z, location, cancel, out float[] smoothX, out float[] smoothZ, out float[] smoothLocation);
            DecimateCurve(smoothX, smoothZ, smoothLocation, out float[] keptX, out float[] keptZ, out float[] keptLocation);
            SampleSegment(segment, cells, g, water, d, keptX, keptZ, keptLocation, count, cancel);
        }

        private static void DisplaceMeander(IslandRecipe recipe, List<int> cells, Grid g, WaterState water, Drainage d, float[] x, float[] z, int count, CancellationToken cancel)
        {
            // Remove D8 staircases over a physical window before subdivision.
            int window = Math.Max(2, (int)Math.Ceiling(8 / g.CellSize));
            var baseX = (float[])x.Clone();
            var baseZ = (float[])z.Clone();
            var arc = new float[count];
            for (int j = 1; j < count; j++)
                arc[j] = arc[j - 1] + d.Distance[cells[j - 1]];
            ulong seed = MathCore.Stage(recipe.seed, "Meander");
            float phase = MathCore.Noise(baseX[0], baseZ[0], seed) * 6.2831853f;
            for (int j = 1; j < count - 1; j++)
            {
                float sumX = 0, sumZ = 0, sumWeight = 0;
                for (int q = Math.Max(0, j - window); q <= Math.Min(count - 1, j + window); q++)
                {
                    float weight = window + 1 - Math.Abs(q - j);
                    sumX += baseX[q] * weight;
                    sumZ += baseZ[q] * weight;
                    sumWeight += weight;
                }

                float fade = MathCore.Smooth(0, 16, Math.Min(arc[j], arc[count - 1] - arc[j]));
                x[j] += (sumX / sumWeight - x[j]) * fade;
                z[j] += (sumZ / sumWeight - z[j]) * fade;
                float tx = baseX[Math.Min(count - 1, j + window)] - baseX[Math.Max(0, j - window)];
                float tz = baseZ[Math.Min(count - 1, j + window)] - baseZ[Math.Max(0, j - window)];
                float norm = (float)Norm(tx, tz);
                if (norm > 0)
                {
                    float wavelength = Math.Max(35, water.Width[cells[j]] * 10);
                    float wiggle = (float)Math.Sin(arc[j] * 6.2831853f / wavelength + phase);
                    wiggle += 0.35f * (2 * MathCore.Noise(arc[j] / wavelength, phase, seed) - 1);
                    float offset = water.Width[cells[j]] * 0.35f / g.CellSize * fade * wiggle;
                    x[j] -= tz / norm * offset;
                    z[j] += tx / norm * offset;
                }
            }
        }

        private static void SubdivideCurve(float[] x, float[] z, float[] location, CancellationToken cancel, out float[] resultX, out float[] resultZ, out float[] resultLocation)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                var nx = new float[x.Length * 2];
                var nz = new float[nx.Length];
                var nl = new float[nx.Length];
                int last = nx.Length - 1;
                nx[0] = x[0];
                nz[0] = z[0];
                nl[0] = location[0];
                nx[last] = x[x.Length - 1];
                nz[last] = z[z.Length - 1];
                nl[last] = location[location.Length - 1];
                for (int j = 0; j < x.Length - 1; j++)
                {
                    MathCore.Check(cancel, j);
                    for (int k = 0; k < 2; k++)
                    {
                        float t = k == 0 ? 0.25f : 0.75f;
                        int p = j * 2 + k + 1;
                        nx[p] = x[j] * (1 - t) + x[j + 1] * t;
                        nz[p] = z[j] * (1 - t) + z[j + 1] * t;
                        nl[p] = location[j] * (1 - t) + location[j + 1] * t;
                    }
                }

                x = nx;
                z = nz;
                location = nl;
            }

            resultX = x;
            resultZ = z;
            resultLocation = location;
        }

        private static void DecimateCurve(float[] x, float[] z, float[] location, out float[] resultX, out float[] resultZ, out float[] resultLocation)
        {
            var keptX = new List<float>
            {
                x[0]
            };
            var keptZ = new List<float>
            {
                z[0]
            };
            var keptLocation = new List<float>
            {
                location[0]
            };
            for (int j = 1; j < x.Length - 1; j++)
            {
                int last = keptX.Count - 1;
                if (Norm(x[j] - keptX[last], z[j] - keptZ[last]) < 0.8)
                    continue;
                keptX.Add(x[j]);
                keptZ.Add(z[j]);
                keptLocation.Add(location[j]);
            }

            keptX.Add(x[x.Length - 1]);
            keptZ.Add(z[z.Length - 1]);
            keptLocation.Add(location[location.Length - 1]);
            resultX = keptX.ToArray();
            resultZ = keptZ.ToArray();
            resultLocation = keptLocation.ToArray();
        }

        private static void SampleSegment(RiverSegment segment, List<int> cells, Grid g, WaterState water, Drainage d, float[] x, float[] z, float[] location, int count, CancellationToken cancel)
        {
            int size = x.Length;
            segment.X = new float[size];
            segment.Z = new float[size];
            segment.Surface = new float[size];
            segment.Bed = new float[size];
            segment.Width = new float[size];
            segment.Depth = new float[size];
            segment.Discharge = new float[size];
            segment.Rapid = new bool[size];
            bool source = segment.FromLake < 0;
            for (int donor = d.DonorHead[cells[0]]; donor >= 0; donor = d.DonorNext[donor])
                if (water.Channel[donor])
                    source = false;
            float sourceArc = 0;
            for (int j = 0; j < size; j++)
            {
                MathCore.Check(cancel, j);
                int k = Math.Min(count - 2, (int)location[j]);
                float t = location[j] - k;
                int a = cells[k], b = cells[k + 1];
                segment.X[j] = x[j] * g.CellSize;
                segment.Z[j] = z[j] * g.CellSize;
                segment.Surface[j] = water.Surface[a] * (1 - t) + water.Surface[b] * t;
                float bd = water.Depth[b] > 0 ? water.Depth[b] : water.Depth[a], bw = water.Width[b] > 0 ? water.Width[b] : water.Width[a];
                segment.Depth[j] = water.Depth[a] * (1 - t) + bd * t;
                segment.Width[j] = water.Width[a] * (1 - t) + bw * t;
                if (j > 0)
                    sourceArc += (float)Norm(segment.X[j] - segment.X[j - 1], segment.Z[j] - segment.Z[j - 1]);
                if (source)
                {
                    float taper = MathCore.Smooth(0, Math.Max(12, segment.Width[j] * 3), sourceArc);
                    segment.Width[j] *= 0.18f + 0.82f * taper;
                    segment.Depth[j] *= 0.25f + 0.75f * taper;
                }

                segment.Bed[j] = segment.Surface[j] - segment.Depth[j];
                segment.Discharge[j] = d.Discharge[a] * (1 - t) + d.Discharge[b] * t;
                if (j > 0)
                {
                    float ddx = segment.X[j] - segment.X[j - 1], ddz = segment.Z[j] - segment.Z[j - 1];
                    segment.Rapid[j - 1] = segment.Surface[j - 1] - segment.Surface[j] > 0.3f * (float)Math.Sqrt(ddx * ddx + ddz * ddz);
                }
            }
        }

        internal static void FinalHydrology(TerrainState s, WaterState water, CancellationToken cancel)
        {
            int n = s.Height.Data.Length;
            var filled = new bool[n];
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                if (water.LakeId[i] < 0 && s.Height.Data[i] <= 0)
                    s.Ocean[i] = true;
            }

            for (int pass = 0; pass < 5; pass++)
            {
                Drainage d = Hydrology.Route(s.Height, s.Ocean, cancel);
                for (int i = 0; i < n; i++)
                {
                    MathCore.Check(cancel, i);
                    if (!s.Ocean[i] && water.LakeId[i] < 0 && d.Fill[i] - s.Height.Data[i] > 0.00001f)
                    {
                        s.Height.Data[i] = d.Fill[i];
                        if (!filled[i])
                        {
                            filled[i] = true;
                            water.Pits++;
                        }
                    }
                }

                // Physical filling does not change fill levels, so this graph/Q remain valid.
                water.Routing = d;
                ReconcileProfiles(s.Height, water, cancel);
                if (pass == 4 || !ContainBanks(s.Height, water, s.Ocean, cancel))
                    break;
            }
        }

        private static bool ContainBanks(Grid height, WaterState water, bool[] ocean, CancellationToken cancel)
        {
            bool changed = false;
            for (int j = 0; j < water.Rivers.Count; j++)
            {
                RiverSegment river = water.Rivers[j];
                int last = river.X.Length - 1;
                for (int k = 1; k < last; k++)
                {
                    MathCore.Check(cancel, k);
                    float tx = river.X[k + 1] - river.X[k - 1], tz = river.Z[k + 1] - river.Z[k - 1];
                    float length = (float)Math.Sqrt(tx * tx + tz * tz);
                    if (length == 0)
                        continue;
                    float offset = river.Width[k] * 0.5f + height.CellSize;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int x = (int)Math.Round((river.X[k] - side * tz / length * offset) / height.CellSize);
                        int z = (int)Math.Round((river.Z[k] + side * tx / length * offset) / height.CellSize);
                        if (x < 0 || z < 0 || x >= height.Width || z >= height.Height)
                            continue;
                        int i = z * height.Width + x;
                        if (ocean[i] || water.LakeId[i] >= 0 || water.Corridor[i] || height.Data[i] >= river.Surface[k] + 0.05f)
                            continue;
                        // A sub-cell source shares bilinear support with its banks;
                        // spreading a raise there can lift the sampled wet centre.
                        if (river.Width[k] < 2 * height.CellSize)
                        {
                            height.Data[i] = river.Surface[k] + 0.15f;
                            changed = true;
                            continue;
                        }

                        // Spread a containment correction into a smooth berm.
                        // Raising only the rounded probe cell left isolated teeth
                        // along an otherwise continuous analytic bank.
                        for (int dz = -2; dz <= 2; dz++)
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                int xx = x + dx, zz = z + dz;
                                if (xx < 0 || zz < 0 || xx >= height.Width || zz >= height.Height)
                                    continue;
                                int neighbor = zz * height.Width + xx;
                                if (ocean[neighbor] || water.LakeId[neighbor] >= 0 || water.Corridor[neighbor])
                                    continue;
                                float berm = river.Surface[k] + 0.15f - 0.1f * (dx * dx + dz * dz);
                                height.Data[neighbor] = Math.Max(height.Data[neighbor], berm);
                            }

                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static void ReconcileProfiles(Grid height, WaterState water, CancellationToken cancel)
        {
            // Extraction creates downstream reaches after upstream ones. Sample the
            // final raster to retain a lake outlet step across a diagonal, protected rim.
            for (int j = water.Rivers.Count - 1; j >= 0; j--)
            {
                RiverSegment river = water.Rivers[j];
                int last = river.X.Length - 1;
                if (river.Downstream >= 0)
                    river.Surface[last] = water.Rivers[river.Downstream].Surface[0];
                for (int k = last; k >= 0; k--)
                {
                    MathCore.Check(cancel, k);
                    if (k < last)
                    {
                        float ground = MathCore.Sample(height, river.X[k] / height.CellSize, river.Z[k] / height.CellSize);
                        river.Surface[k] = Math.Max(river.Surface[k + 1], Math.Max(river.Surface[k], ground + 0.005f));
                        if (river.FromLake >= 0)
                            river.Surface[k] = Math.Min(river.Surface[k], water.Lakes[river.FromLake].Level);
                    }

                    river.Bed[k] = river.Surface[k] - river.Depth[k];
                    if (k < last)
                    {
                        double distance = Norm(river.X[k + 1] - river.X[k], river.Z[k + 1] - river.Z[k]);
                        river.Rapid[k] = river.Surface[k] - river.Surface[k + 1] > distance * 0.3;
                    }
                }
            }
        }

        internal static Grid[] Materials(IslandRecipe r, TerrainState s, WaterState water, CancellationToken cancel)
        {
            Grid g = s.Height;
            int n = g.Data.Length;
            var result = new Grid[SurfaceLayer.Count];
            for (int j = 0; j < result.Length; j++)
                result[j] = new Grid(g.Width, g.Height, g.CellSize);
            var lakeMask = new bool[n];
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                lakeMask[i] = water.LakeId[i] >= 0;
            }

            float[] shore = TerrainStages.DistanceTo(lakeMask, g.Width, g.Height, cancel);
            float[] coast = TerrainStages.DistanceTo(s.Ocean, g.Width, g.Height, cancel);
            float[] channel = water.ChannelDistance ?? TerrainStages.DistanceTo(water.Corridor, g.Width, g.Height, cancel);
            float[] smoothHeight = TerrainStages.Blur(g.Data, g.Width, g.Height, Math.Max(1, (int)Math.Ceiling(4 / g.CellSize)), 3, cancel);
            var macro = new Grid(g.Width, g.Height, g.CellSize);
            Array.Copy(smoothHeight, macro.Data, n);
            ulong materialSeed = MathCore.Stage(r.seed, "Materials");
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                float slope = MathCore.Slope(g, i), h = g.Data[i];
                float sandSlope = 1 - MathCore.Smooth(0.12f, 0.23f, slope);
                float sandHeight = 1 - MathCore.Smooth(1, 7, h);
                float sandCoast = 1 - MathCore.Smooth(r.beachWidth * 0.85f, r.beachWidth * 1.1f, coast[i] * g.CellSize);
                float sand = s.Beach[i] * sandSlope * sandHeight * sandCoast;
                float macroSlope = MathCore.Slope(macro, i);
                float variation = 0.08f * (2 * MathCore.Noise(i % g.Width * g.CellSize / 45, i / g.Width * g.CellSize / 45, materialSeed) - 1);
                float rock = MathCore.Smooth(0.46f, 0.90f, macroSlope + variation);
                float gravel = Math.Max(1 - MathCore.Smooth(0, 5, shore[i] * g.CellSize), 1 - MathCore.Smooth(0, 4, channel[i] * g.CellSize));
                if (lakeMask[i])
                    gravel = 1;
                float grass = Math.Max(0.02f, (1 - sand) * (1 - rock) * (1 - gravel)), sum = sand + rock + gravel + grass;
                result[SurfaceLayer.Sand].Data[i] = sand / sum;
                result[SurfaceLayer.Rock].Data[i] = rock / sum;
                result[SurfaceLayer.Gravel].Data[i] = gravel / sum;
                result[SurfaceLayer.Grass].Data[i] = grass / sum;
            }

            return result;
        }
    }
}

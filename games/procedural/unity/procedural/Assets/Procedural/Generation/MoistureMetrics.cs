using System;
using System.Threading;

namespace Procedural.Generation
{
    internal sealed class MoistureMetrics
    {
        internal static readonly string[] ClassNames =
        {
            "waterAdjacent",
            "valleyFloor",
            "hillslope",
            "ridge"
        };
        internal double P10, P50, P90, Anisotropy;
        internal readonly double[] Means = new double[4];
        internal readonly int[] Counts = new int[4];
        internal bool Ordered;
        internal static MoistureMetrics Measure(IslandResult result, WaterState water, ReportData report, CancellationToken cancel)
        {
            var metrics = new MoistureMetrics();
            Grid g = result.Height;
            MoistureState state = water.Moisture;
            var samples = new float[g.Data.Length];
            float[] mean = TerrainStages.Blur(g.Data, g.Width, g.Height, Math.Max(1, (int)Math.Ceiling(20 / g.CellSize)), 3, cancel);
            int count = 0;
            double orientation = 0, weight = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                MathCore.Check(cancel, i);
                if (water.Routing.Ocean[i] || state.Water[i])
                    continue;
                float value = result.Moisture.Data[i];
                samples[count++] = value;
                float tpi = g.Data[i] - mean[i];
                int landform = state.WaterDistance[i] <= 15 ? 0 : water.Floodplain[i] || (tpi < -0.75f && MathCore.Slope(g, i) <= 0.176327f) ? 1 : tpi > 0.75f ? 3 : 2;
                metrics.Means[landform] += value;
                metrics.Counts[landform]++;
                int x = i % g.Width, z = i / g.Width;
                if (x == 0 || z == 0 || x == g.Width - 1 || z == g.Height - 1)
                    continue;
                // Exclude gradients crossing open water/sea. Measure the raw TWI,
                // before percentile clipping or assigning water the value 1.
                int left = i - 1, right = i + 1, up = i - g.Width, down = i + g.Width;
                if (state.Water[left] || state.Water[right] || state.Water[up] || state.Water[down] || water.Routing.Ocean[left] || water.Routing.Ocean[right] || water.Routing.Ocean[up] || water.Routing.Ocean[down])
                    continue;
                double dx = (state.Twi[right] - state.Twi[left]) / (2 * g.CellSize);
                double dz = (state.Twi[down] - state.Twi[up]) / (2 * g.CellSize);
                double slope = Math.Sqrt(dx * dx + dz * dz);
                orientation += slope * Math.Cos(8 * Math.Atan2(dz, dx));
                weight += slope;
            }

            Array.Sort(samples, 0, count);
            metrics.P10 = Percentile(samples, count, 0.1);
            metrics.P50 = Percentile(samples, count, 0.5);
            metrics.P90 = Percentile(samples, count, 0.9);
            metrics.Anisotropy = weight > 0 ? Math.Abs(orientation / weight) : 0;
            metrics.Ordered = true;
            for (int k = 0; k < 4; k++)
            {
                metrics.Means[k] = metrics.Counts[k] > 0 ? metrics.Means[k] / metrics.Counts[k] : double.NaN;
                if (metrics.Counts[k] == 0)
                    metrics.Ordered = false;
                if (k > 0 && !(metrics.Means[k - 1] > metrics.Means[k]))
                    metrics.Ordered = false;
            }

            if (!metrics.Ordered)
                report.Warnings.Add("Moisture class ordering fails or a class is empty: expect water-adjacent > valley floor > hillslope > ridge.");
            if (metrics.Anisotropy > 0.1)
                report.Warnings.Add("TWI-gradient slope-weighted grid anisotropy exceeds 0.1.");
            return metrics;
        }

        private static double Percentile(float[] sorted, int count, double percentile)
        {
            return count > 0 ? sorted[(int)((count - 1) * percentile)] : double.NaN;
        }

        internal void Serialize(TinyJson j, MoistureState state)
        {
            j.P("moistureLandDefinition", "Terrestrial samples: exclude sea, lake cells and analytic river water; equally weighted cells");
            j.P("moistureP10", P10);
            j.P("moistureP50", P50);
            j.P("moistureP90", P90);
            j.P("moistureClassDefinition", "Disjoint priority: water distance <=15m; else floodplain mask or (TPI<-0.75m and slope<=10deg); else TPI>0.75m ridge; else hillslope. TPI=height minus three box passes of radius20m; independent of moisture/TWI");
            j.Name("moistureClassMeans");
            j.Obj();
            for (int k = 0; k < 4; k++)
                j.P(ClassNames[k], Means[k]);
            j.EndObj();
            j.Name("moistureClassCounts");
            j.Obj();
            for (int k = 0; k < 4; k++)
                j.P(ClassNames[k], Counts[k]);
            j.EndObj();
            j.P("moistureClassesOrdered", Ordered);
            j.P("twiGridAnisotropy", Anisotropy);
            j.P("twiAnisotropyDefinition", "abs(sum(|grad raw TWI| * cos(8*aspect))/sum(|grad raw TWI|)); central differences; terrestrial five-point stencils; warning >0.1");
            j.P("twiLandP05", state.TwiP05);
            j.P("twiLandP95", state.TwiP95);
            j.P("mfdRainAreaM2", state.RainArea);
            j.P("mfdOutletAreaM2", state.OutletArea);
            j.P("mfdExponent", WetnessStages.FlowExponent);
            j.P("moistureTwiWeight", WetnessStages.TwiWeight);
            j.P("moistureWaterWeight", WetnessStages.WaterWeight);
            j.P("moistureElevationWeight", WetnessStages.HeightWeight);
            j.P("moistureWaterLengthM", WetnessStages.WaterLength);
            j.P("moistureNoiseLengthM", WetnessStages.NoiseLength);
            j.P("moistureNoiseAmplitude", WetnessStages.NoiseAmplitude);
            j.P("moistureWaterDistanceDefinition", "Exact Euclidean distance to union of linearly tapered river capsules and lake sample squares; empty water gives infinity; sea is not a proximity source");
        }
    }
}

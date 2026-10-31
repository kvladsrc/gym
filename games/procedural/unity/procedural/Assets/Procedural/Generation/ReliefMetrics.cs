using System;
using System.Threading;

namespace Procedural.Generation
{
    internal static class ReliefMetrics
    {
        internal static void Measure(IslandResult result, WaterState water, ReportData report, CancellationToken cancel)
        {
            Grid g = result.Height;
            var slopes = new float[g.Data.Length];
            int count = 0;
            int facets = 0;
            int coast = 0;
            int beach = 0;
            int lowlands = 0, floodplains = 0, diagonalCoast = 0;
            double rock = 0;
            double sand = 0;
            double orientation = 0;
            double weight = 0;
            for (int z = 1; z < g.Height - 1; z++)
            {
                for (int x = 1; x < g.Width - 1; x++)
                {
                    int i = z * g.Width + x;
                    MathCore.Check(cancel, i);
                    if (g.Data[i] <= 0)
                        continue;
                    float dx = (g[x + 1, z] - g[x - 1, z]) / (2 * g.CellSize);
                    float dz = (g[x, z + 1] - g[x, z - 1]) / (2 * g.CellSize);
                    double slope = Math.Sqrt(dx * dx + dz * dz);
                    float degrees = (float)(Math.Atan(slope) * 180 / Math.PI);
                    slopes[count++] = degrees;
                    if (degrees < 5 && g.Data[i] < result.PeakHeight * 0.25f)
                        lowlands++;
                    if (water.Floodplain[i] && degrees < 5)
                        floodplains++;
                    diagonalCoast += (g[x - 1, z - 1] <= 0 ? 1 : 0) + (g[x + 1, z - 1] <= 0 ? 1 : 0) + (g[x - 1, z + 1] <= 0 ? 1 : 0) + (g[x + 1, z + 1] <= 0 ? 1 : 0);
                    if (Math.Abs(degrees - 38) <= 1)
                        facets++;
                    rock += result.Surface[SurfaceLayer.Rock].Data[i];
                    sand += result.Surface[SurfaceLayer.Sand].Data[i];
                    orientation += slope * Math.Cos(8 * Math.Atan2(dz, dx));
                    weight += slope;
                    int edges = (g[x - 1, z] <= 0 ? 1 : 0) + (g[x + 1, z] <= 0 ? 1 : 0) + (g[x, z - 1] <= 0 ? 1 : 0) + (g[x, z + 1] <= 0 ? 1 : 0);
                    coast += edges;
                    if (degrees <= 12 && result.Surface[SurfaceLayer.Sand].Data[i] >= 0.5f)
                        beach += edges;
                }
            }

            Array.Sort(slopes, 0, count);
            report.SlopeP50 = slopes[(int)((count - 1) * 0.5)];
            report.SlopeP90 = slopes[(int)((count - 1) * 0.9)];
            report.RockShare = rock / count;
            report.SandShare = sand / count;
            report.FacetShare = facets / (double)count;
            report.Anisotropy = weight > 0 ? Math.Abs(orientation / weight) : 0;
            report.BeachShare = coast > 0 ? beach / (double)coast : 0;
            report.LowlandShare = lowlands / (double)count;
            report.FloodplainShare = floodplains / (double)count;
            double perimeter = Math.PI / 8 * (coast + diagonalCoast / Math.Sqrt(2)) * g.CellSize;
            report.CoastSinuosity = perimeter / (2 * Math.Sqrt(Math.PI * count * g.CellSize * g.CellSize));
            if (report.LowlandShare < 0.15 || report.LowlandShare > 0.35)
                report.Warnings.Add("Lowland share is outside 15–35% (slope <5 degrees, height <25% of peak).");
            if (report.CoastSinuosity < 1.4)
                report.Warnings.Add("Coast sinuosity is below 1.4.");
            if (report.SlopeP50 < 8 || report.SlopeP50 > 20)
                report.Warnings.Add("Land slope p50 is outside 8–20 degrees.");
            if (report.SlopeP90 > 35)
                report.Warnings.Add("Land slope p90 exceeds 35 degrees.");
            if (report.RockShare < 0.03 || report.RockShare > 0.2)
                report.Warnings.Add("Rock share of land is outside 3–20% (mean material weight).");
            if (Math.Abs(report.BeachShare - result.Recipe.beachShare) > 0.1)
                report.Warnings.Add("Beach share of coastline differs from the recipe by more than 10 percentage points.");
            if (report.FacetShare > 0.05)
                report.Warnings.Add("More than 5% of land is within one degree of the critical slope.");
            if (report.Anisotropy > 0.1)
                report.Warnings.Add("Slope-weighted D8 anisotropy exceeds 0.1.");
        }
    }
}

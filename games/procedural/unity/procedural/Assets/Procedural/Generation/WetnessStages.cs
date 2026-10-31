using System;
using System.Threading;

namespace Procedural.Generation
{
    internal sealed class MoistureState
    {
        internal Grid Wetness, Moisture;
        internal float[] WaterDistance, Twi;
        internal bool[] Water;
        internal double TwiP05, TwiP95, OutletArea, RainArea;
    }

    internal static class WetnessStages
    {
        internal const double FlowExponent = 1.1;
        internal const float TwiWeight = 0.50f, WaterWeight = 0.35f, HeightWeight = 0.15f;
        internal const float WaterLength = 40, NoiseLength = 96, NoiseAmplitude = 0.025f;
        // Kahn traversal of ALL descending edges. The D8 donor stack alone is not
        // a topological ordering of MFD edges across neighbouring D8 basins.
        // Flat/depression interiors use their no-epsilon, sea-rooted receiver.
        internal static double[] Catchment(Grid g, Drainage d, CancellationToken cancel)
        {
            int n = g.Data.Length, width = g.Width;
            var area = new double[n];
            var incoming = new int[n];
            var weights = new double[n];
            var queue = new int[n];
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                if (d.Ocean[i])
                    continue;
                area[i] = (double)g.CellSize * g.CellSize;
                int x = i % width, z = i / width;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, zz = z + dz;
                        if (xx < 0 || xx >= width || zz < 0 || zz >= g.Height)
                            continue;
                        int j = zz * width + xx;
                        double drop = (double)d.Fill[i] - d.Fill[j];
                        if (drop <= 0)
                            continue;
                        weights[i] += Weight(drop, dx, dz, g.CellSize);
                        incoming[j]++;
                    }

                if (!(weights[i] > 0))
                    incoming[d.Receiver[i]]++;
            }

            int tail = 0, visited = 0;
            for (int i = 0; i < n; i++)
                if (!d.Ocean[i] && incoming[i] == 0)
                    queue[tail++] = i;
            for (int head = 0; head < tail; head++)
            {
                MathCore.Check(cancel, head);
                int i = queue[head], x = i % width, z = i / width;
                visited++;
                if (!(weights[i] > 0))
                {
                    int j = d.Receiver[i];
                    area[j] += area[i];
                    if (--incoming[j] == 0 && !d.Ocean[j])
                        queue[tail++] = j;
                    continue;
                }

                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, zz = z + dz;
                        if (xx < 0 || xx >= width || zz < 0 || zz >= g.Height)
                            continue;
                        int j = zz * width + xx;
                        double drop = (double)d.Fill[i] - d.Fill[j];
                        if (drop <= 0)
                            continue;
                        area[j] += area[i] * Weight(drop, dx, dz, g.CellSize) / weights[i];
                        if (--incoming[j] == 0 && !d.Ocean[j])
                            queue[tail++] = j;
                    }
            }

            if (visited != d.Stack.Length)
                throw new InvalidOperationException("MFD traversal has a cycle or disconnected land.");
            return area;
        }

        private static double Weight(double drop, int dx, int dz, float cell)
        {
            return Math.Pow(drop / (cell * (dx != 0 && dz != 0 ? Math.Sqrt(2) : 1)), FlowExponent);
        }

        internal static float[] Twi(Grid height, double[] area, CancellationToken cancel)
        {
            var twi = new float[area.Length];
            double floor = Math.Tan(0.5 * Math.PI / 180);
            for (int i = 0; i < twi.Length; i++)
            {
                MathCore.Check(cancel, i);
                twi[i] = (float)Math.Log(Math.Max(area[i], (double)height.CellSize * height.CellSize) / height.CellSize / Math.Max(floor, MathCore.Slope(height, i)));
            }

            return twi;
        }

        internal static MoistureState Build(IslandRecipe recipe, TerrainState terrain, WaterState water, CancellationToken cancel)
        {
            Grid g = terrain.Height;
            int n = g.Data.Length;
            var state = new MoistureState
            {
                Wetness = new Grid(g.Width, g.Height, g.CellSize),
                Moisture = new Grid(g.Width, g.Height, g.CellSize),
                WaterDistance = WaterProximity.Distances(g, water.Rivers, water.LakeId, cancel),
                Water = new bool[n]
            };
            // Re-route the actual final heights: profile reconciliation/containment
            // may have adjusted them after the last discharge routing pass.
            Drainage routing = Hydrology.Route(g, terrain.Ocean, cancel);
            double[] area = Catchment(g, routing, cancel);
            float[] twi = Twi(g, area, cancel);
            state.Twi = twi;
            var land = new float[n];
            int count = 0;
            float peak = 0;
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                state.Water[i] = water.LakeId[i] >= 0 || state.WaterDistance[i] <= 0;
                if (!terrain.Ocean[i] && !state.Water[i])
                    land[count++] = twi[i];
                peak = Math.Max(peak, g.Data[i]);
                if (terrain.Ocean[i])
                    state.OutletArea += area[i];
                else
                    state.RainArea += (double)g.CellSize * g.CellSize;
            }

            Array.Sort(land, 0, count);
            state.TwiP05 = count > 0 ? land[(int)((count - 1) * 0.05)] : 0;
            state.TwiP95 = count > 0 ? land[(int)((count - 1) * 0.95)] : 0;
            double span = state.TwiP95 - state.TwiP05;
            ulong seed = MathCore.Stage(recipe.seed, "Moisture");
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                bool wet = terrain.Ocean[i] || state.Water[i];
                // Constant TWI has no percentile contrast; assign the midpoint.
                float normalized = span > 1e-6 ? MathCore.Clamp((float)((twi[i] - state.TwiP05) / span), 0, 1) : 0.5f;
                state.Wetness.Data[i] = wet ? 1 : normalized;
                float baseMoisture = MathCore.Clamp(TwiWeight * normalized + WaterWeight * (float)Math.Exp(-state.WaterDistance[i] / WaterLength) + HeightWeight * (1 - MathCore.Clamp(g.Data[i] / Math.Max(peak, 0.001f), 0, 1)), 0, 1);
                float noise = NoiseAmplitude * (2 * MathCore.Noise(i % g.Width * g.CellSize / NoiseLength, i / g.Width * g.CellSize / NoiseLength, seed) - 1);
                state.Moisture.Data[i] = wet ? 1 : MathCore.Clamp(baseMoisture + noise, 0, 1);
            }

            return state;
        }
    }
}

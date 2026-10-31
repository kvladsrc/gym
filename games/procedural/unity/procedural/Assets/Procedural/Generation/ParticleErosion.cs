using System;
using System.Collections.Generic;
using System.Threading;

namespace Procedural.Generation
{
    // Independent metre-space droplet trial. See tools/core/README.md for references.
    internal static class ParticleErosion
    {
        private readonly struct BrushPoint
        {
            internal readonly int X;
            internal readonly int Z;
            internal readonly float Weight;
            internal BrushPoint(int x, int z, float weight)
            {
                X = x;
                Z = z;
                Weight = weight;
            }
        }

        internal static void Run(IslandRecipe recipe, TerrainState state, CancellationToken cancel)
        {
            Grid grid = state.Height;
            float cell = grid.CellSize;
            int count = (int)Math.Min(2000000, recipe.worldSize * recipe.worldSize * recipe.erosionDropletsPerSquareMetre);
            int radius = Math.Max(2, (int)Math.Ceiling(3 / cell));
            var brush = new List<BrushPoint>();
            float total = 0;
            for (int z = -radius; z <= radius; z++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    float weight = Math.Max(0, 1 - (float)Math.Sqrt(x * x + z * z) / radius);
                    if (weight <= 0)
                        continue;
                    brush.Add(new BrushPoint(x, z, weight));
                    total += weight;
                }
            }

            ulong rng = MathCore.Stage(recipe.seed, "ParticleErosion");
            for (int drop = 0; drop < count; drop++)
            {
                MathCore.Check(cancel, drop);
                float x = 2 + MathCore.Random(ref rng) * (grid.Width - 5);
                float z = 2 + MathCore.Random(ref rng) * (grid.Height - 5);
                float vx = 0;
                float vz = 0;
                float speed = 1;
                float water = 1;
                float sediment = 0;
                for (int step = 0; step < 80; step++)
                {
                    int ix = (int)x;
                    int iz = (int)z;
                    int i = iz * grid.Width + ix;
                    if (state.Ocean[i])
                        break;
                    float before = MathCore.Sample(grid, x, z);
                    float dx = (MathCore.Sample(grid, x + 0.5f, z) - MathCore.Sample(grid, x - 0.5f, z)) / cell;
                    float dz = (MathCore.Sample(grid, x, z + 0.5f) - MathCore.Sample(grid, x, z - 0.5f)) / cell;
                    vx = vx * 0.08f - dx * 0.92f;
                    vz = vz * 0.08f - dz * 0.92f;
                    float length = (float)Math.Sqrt(vx * vx + vz * vz);
                    if (length < 1e-8f)
                        break;
                    vx /= length;
                    vz /= length;
                    float nextX = x + vx;
                    float nextZ = z + vz;
                    if (nextX < 1 || nextZ < 1 || nextX >= grid.Width - 2 || nextZ >= grid.Height - 2)
                        break;
                    float delta = MathCore.Sample(grid, nextX, nextZ) - before;
                    float capacity = Math.Max(0.001f, -delta * speed * water * 3);
                    if (delta > 0 || sediment > capacity)
                    {
                        float deposit = delta > 0 ? Math.Min(sediment, delta) : (sediment - capacity) * 0.25f;
                        Deposit(grid, state.Ocean, x, z, deposit);
                        sediment -= deposit;
                    }
                    else
                    {
                        float erode = Math.Min(-delta, (capacity - sediment) * 0.2f);
                        foreach (BrushPoint point in brush)
                        {
                            int xx = ix + point.X;
                            int zz = iz + point.Z;
                            if (xx < 1 || zz < 1 || xx >= grid.Width - 1 || zz >= grid.Height - 1)
                                continue;
                            int j = zz * grid.Width + xx;
                            if (state.Ocean[j])
                                continue;
                            float amount = Math.Min(Math.Max(0, grid.Data[j] - 0.02f), erode * point.Weight / total);
                            grid.Data[j] -= amount;
                            sediment += amount;
                        }
                    }

                    // A negative drop accelerates water; the reference implementation's sign is not reused.
                    speed = (float)Math.Sqrt(Math.Max(0, speed * speed - 9.81f * delta));
                    water *= 0.97f;
                    x = nextX;
                    z = nextZ;
                }

                // Settle remaining sediment on land. Sediment exiting to sea leaves this model.
                Deposit(grid, state.Ocean, x, z, sediment);
            }
        }

        private static void Deposit(Grid grid, bool[] ocean, float x, float z, float amount)
        {
            int ix = (int)x;
            int iz = (int)z;
            float u = x - ix;
            float v = z - iz;
            for (int dz = 0; dz <= 1; dz++)
            {
                for (int dx = 0; dx <= 1; dx++)
                {
                    int i = (iz + dz) * grid.Width + ix + dx;
                    if (!ocean[i])
                        grid.Data[i] += amount * (dx == 0 ? 1 - u : u) * (dz == 0 ? 1 - v : v);
                }
            }
        }
    }
}

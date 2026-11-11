using System;
using System.Collections.Generic;

namespace Realms.Core
{
    public static partial class WorldGen
    {
        /// <summary>
        /// Rivers along the hexagon edges. Every corner where three land cells meet drains to a
        /// neighbouring corner, found by flooding the corners up from the water, lowest first: so
        /// each one has a way down to the sea or a lake, round any hollow. Springs are corners in
        /// the hills and mountains, far enough from the water and from each other, the wetter and
        /// the higher the likelier; each river follows the drainage until it reaches water or a
        /// river already traced, which it then joins. Uses its own random numbers, so the rest of
        /// the world does not change with the rivers.
        /// </summary>
        static void Rivers(World world, WorldSettings settings, float[] moisture)
        {
            Cell[] cells = world.Cells;
            var corners = new CornerGraph(world);
            int n = corners.Count;

            // Height of a corner: the mean of its cells; wet where it touches water or the rim.
            var height = new float[n];
            var wet = new bool[n];
            for (int c = 0; c < n; c++)
            {
                List<int> around = corners.Cells[c];
                float sum = 0;
                foreach (int i in around)
                {
                    sum += cells[i].Elevation;
                    wet[c] |= cells[i].IsWater;
                }
                height[c] = sum / around.Count;
                wet[c] |= around.Count < 3;
            }

            var down = new int[n];
            var steps = new int[n];
            var reached = new bool[n];
            var filled = new float[n];
            var heap = new MinHeap();
            for (int c = 0; c < n; c++)
            {
                down[c] = -1;
                if (wet[c])
                {
                    reached[c] = true;
                    filled[c] = height[c];
                    heap.Push(height[c], c);
                }
            }
            while (heap.Count > 0)
            {
                heap.Pop(out float _, out int c);
                foreach (int next in corners.Links[c])
                {
                    if (reached[next])
                        continue;
                    reached[next] = true;
                    filled[next] = Math.Max(height[next], filled[c] + 1e-4f);
                    down[next] = c;
                    steps[next] = steps[c] + 1;
                    heap.Push(filled[next], next);
                }
            }

            // Springs: corners among hills or mountains, at least six edges from the water.
            var rng = new Rng(world.Seed * 7919 + 13);
            var springs = new List<(float score, int corner)>();
            for (int c = 0; c < n; c++)
            {
                if (wet[c] || steps[c] < 6)
                    continue;
                int high = 0;
                float wetness = 0;
                foreach (int i in corners.Cells[c])
                {
                    if (cells[i].Biome == Biome.Hills || cells[i].Biome == Biome.Mountains)
                        high++;
                    wetness += moisture[i] / 3f;
                }
                if (high == 0)
                    continue;
                springs.Add((wetness + 0.25f * high + rng.Range(0f, 0.8f), c));
            }
            springs.Sort((a, b) => b.score.CompareTo(a.score));
            var picked = new List<int>();
            foreach (var (_, c) in springs)
            {
                if (picked.Count == settings.Rivers)
                    break;
                bool apart = true;
                foreach (int p in picked)
                {
                    float dx = corners.X[p] - corners.X[c], dz = corners.Z[p] - corners.Z[c];
                    apart &= dx * dx + dz * dz >= settings.RiverSpacing * settings.RiverSpacing;
                }
                if (apart)
                    picked.Add(c);
            }
            // The longest first, so the shorter ones join them as tributaries.
            picked.Sort((a, b) => steps[b].CompareTo(steps[a]));

            var owner = new int[n];
            for (int c = 0; c < n; c++)
                owner[c] = -1;
            var courses = new List<List<int>>();
            foreach (int spring in picked)
            {
                if (owner[spring] >= 0)
                    continue;
                var course = new List<int> { spring };
                int joins = -1;
                for (int c = spring; down[c] >= 0;)
                {
                    c = down[c];
                    course.Add(c);
                    if (owner[c] >= 0)
                    {
                        joins = owner[c];
                        break;
                    }
                }
                // A brook of a few edges is not worth a river.
                if (course.Count < 5)
                    continue;
                int index = courses.Count;
                for (int k = 0; k < course.Count; k++)
                {
                    if (owner[course[k]] < 0)
                        owner[course[k]] = index;
                }
                courses.Add(course);
                var river = new River { Joins = joins };
                if (joins < 0)
                {
                    // The river runs into the lowest water cell at its last corner.
                    int last = course[course.Count - 1];
                    foreach (int i in corners.Cells[last])
                    {
                        if (cells[i].IsWater && (river.Mouth < 0 || cells[i].Elevation < cells[river.Mouth].Elevation))
                            river.Mouth = i;
                    }
                }
                world.Rivers.Add(river);
            }

            // How much water passes each corner: the river edges above it.
            var flow = new int[n];
            foreach (List<int> course in courses)
            {
                for (int k = 1; k < course.Count; k++)
                {
                    for (int c = course[k]; c >= 0; c = down[c])
                        flow[c]++;
                }
            }
            for (int r = 0; r < courses.Count; r++)
            {
                List<int> course = courses[r];
                foreach (int c in course)
                    world.Rivers[r].Points.Add(new RiverPoint(corners.X[c], corners.Z[c], Math.Max(1, flow[c])));
                for (int k = 0; k + 1 < course.Count; k++)
                {
                    foreach ((int cell, int dir) in corners.EdgeCells(course[k], course[k + 1]))
                        cells[cell].Rivers |= (byte)(1 << dir);
                }
            }
        }

        /// <summary>
        /// The corners of the map's hexagons, each counted once, with the cells around each, the
        /// corners one edge away, and for each edge the cells it borders.
        /// </summary>
        sealed class CornerGraph
        {
            public readonly List<float> X = new List<float>(), Z = new List<float>();
            public readonly List<List<int>> Cells = new List<List<int>>();
            public readonly List<List<int>> Links = new List<List<int>>();
            readonly Dictionary<long, List<(int cell, int dir)>> edges = new Dictionary<long, List<(int, int)>>();

            public int Count => X.Count;

            public CornerGraph(World world)
            {
                var ids = new Dictionary<long, int>();
                var ring = new int[6];
                foreach (Cell cell in world.Cells)
                {
                    float cx = world.CenterX(cell.Index), cz = world.CenterZ(cell.Index);
                    for (int k = 0; k < 6; k++)
                    {
                        float x = cx + HexMetrics.CornerX[k], z = cz + HexMetrics.CornerZ[k];
                        // Corners lie on a lattice: x in halves of the inner diameter, z in halves.
                        long key = (long)MathF.Round(x / HexMetrics.InnerRadius) * 1_000_003L + (long)MathF.Round(z * 2f);
                        if (!ids.TryGetValue(key, out int id))
                        {
                            id = X.Count;
                            ids[key] = id;
                            X.Add(x);
                            Z.Add(z);
                            Cells.Add(new List<int>(3));
                            Links.Add(new List<int>(3));
                        }
                        Cells[id].Add(cell.Index);
                        ring[k] = id;
                    }
                    // Edge d runs from corner d to corner d + 1.
                    for (int d = 0; d < 6; d++)
                    {
                        int a = ring[d], b = ring[(d + 1) % 6];
                        long key = Key(a, b);
                        if (!edges.TryGetValue(key, out var list))
                        {
                            list = new List<(int, int)>(2);
                            edges[key] = list;
                            Links[a].Add(b);
                            Links[b].Add(a);
                        }
                        list.Add((cell.Index, d));
                    }
                }
            }

            static long Key(int a, int b) => a < b ? (long)a << 32 | (uint)b : (long)b << 32 | (uint)a;

            /// <summary>The cells (one or two) on the edge between two corners, with the edge's direction in each.</summary>
            public List<(int cell, int dir)> EdgeCells(int a, int b) => edges[Key(a, b)];
        }
    }
}

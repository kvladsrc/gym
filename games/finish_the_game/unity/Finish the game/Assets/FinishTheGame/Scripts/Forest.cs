using System;
using System.Collections.Generic;
using UnityEngine;

namespace FinishTheGame
{
    /// <summary>Trees planted by a field, not by hand: Poisson-disc sampling
    /// (dart throwing in a random order), each tree keeping its neighbours at
    /// least the mean of their spacings away, so they are never in heaps and
    /// never leave holes; the spacing follows how wooded the land is
    /// (<see cref="Woods"/>: slow noise, so the woods come in great masses
    /// with soft edges, open woodland round them and meadows between, a lone
    /// tree here and there).</summary>
    public static class Forest
    {
        /// <summary>A tree's place, the spacing it keeps, and how wooded its land is (0–1).</summary>
        public struct Tree
        {
            public Vector2 at;
            public float spacing, wood;
        }

        /// <summary>How wooded the land is at x, z (0: meadow, 1: deep wood):
        /// a few octaves of noise over <paramref name="scale"/> metres, its
        /// edges sharpened between <paramref name="edge"/>.x and .y, and shifted by <paramref name="bias"/>.</summary>
        public static float Woods(float x, float z, float scale, Vector2 edge, float bias = 0f)
        {
            float n = 0, total = 0, amplitude = 1, frequency = 1 / scale;
            for (int octave = 0; octave < 3; octave++)
            {
                n += amplitude * Mathf.PerlinNoise(x * frequency + 311.7f * octave + 1000f, z * frequency + 127.1f * octave + 1000f);
                total += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.1f;
            }
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge.x, edge.y, n / total + bias));
        }

        /// <summary>Plants within the cells <paramref name="cells"/> (their
        /// corners, each <paramref name="cell"/> metres a side): about
        /// <paramref name="tries"/> darts a cell, each kept if its spacing
        /// (<paramref name="spacing"/>: its spacing and wood there, or a
        /// spacing ≤ 0 for no tree) leaves room from the trees already there
        /// and from what stands (<paramref name="taken"/>: place, radius).</summary>
        public static List<Tree> Plant(IList<Vector2> cells, float cell, int tries, Func<Vector2, (float spacing, float wood)> spacing,
            IEnumerable<(Vector2 at, float radius)> taken, System.Random random)
        {
            const float Bucket = 8f;
            var grid = new Dictionary<(int, int), List<int>>();
            var trees = new List<Tree>();
            var obstacles = new List<(Vector2 at, float radius)>(taken);
            var blocked = new Dictionary<(int, int), List<int>>();
            (int, int) Key(Vector2 p) => (Mathf.FloorToInt(p.x / Bucket), Mathf.FloorToInt(p.y / Bucket));
            float widest = 0, widestTaken = 0;
            for (int i = 0; i < obstacles.Count; i++)
            {
                var key = Key(obstacles[i].at);
                if (!blocked.TryGetValue(key, out var list)) blocked[key] = list = new List<int>();
                list.Add(i);
                widestTaken = Mathf.Max(widestTaken, obstacles[i].radius);
            }
            // The darts, all thrown in a random order (cell by cell would comb the woods).
            var darts = new List<Vector2>(cells.Count * tries);
            foreach (var corner in cells)
                for (int k = 0; k < tries; k++)
                    darts.Add(corner + new Vector2((float)random.NextDouble() * cell, (float)random.NextDouble() * cell));
            for (int i = darts.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (darts[i], darts[j]) = (darts[j], darts[i]);
            }
            foreach (var p in darts)
            {
                var (own, wood) = spacing(p);
                if (own <= 0) continue;
                if (!Room(p, own)) continue;
                var key = Key(p);
                if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                list.Add(trees.Count);
                trees.Add(new Tree { at = p, spacing = own, wood = wood });
                widest = Mathf.Max(widest, own);
            }
            return trees;

            bool Room(Vector2 p, float own)
            {
                var (cx, cz) = Key(p);
                int reach = Mathf.CeilToInt((own + widest) / 2f / Bucket);
                for (int dx = -reach; dx <= reach; dx++)
                    for (int dz = -reach; dz <= reach; dz++)
                        if (grid.TryGetValue((cx + dx, cz + dz), out var near))
                            foreach (int i in near)
                            {
                                float need = (own + trees[i].spacing) / 2f;
                                if ((trees[i].at - p).sqrMagnitude < need * need) return false;
                            }
                reach = Mathf.CeilToInt(widestTaken / Bucket);
                for (int dx = -reach; dx <= reach; dx++)
                    for (int dz = -reach; dz <= reach; dz++)
                        if (blocked.TryGetValue((cx + dx, cz + dz), out var near))
                            foreach (int i in near)
                            {
                                float need = obstacles[i].radius;
                                if ((obstacles[i].at - p).sqrMagnitude < need * need) return false;
                            }
                return true;
            }
        }

        /// <summary>The corners of the cells (<paramref name="cell"/> metres a
        /// side, over the square ±<paramref name="half"/>) within
        /// <paramref name="reach"/> of any of the routes, and each cell's
        /// distance to the nearest route (by the routes' points every few metres).</summary>
        public static (List<Vector2> cells, Func<Vector2, float> away) Corridor(IList<StoryPath> routes, float half, float cell, float reach)
        {
            var samples = new List<Vector2>();
            foreach (var r in routes)
                for (float s = 0; s <= r.Length; s += cell / 2f)
                {
                    var p = r.Position(s);
                    samples.Add(new Vector2(p.x, p.z));
                }
            int n = Mathf.CeilToInt(2 * half / cell);
            var distance = new float[n, n];
            var cells = new List<Vector2>();
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    var middle = new Vector2(-half + (i + 0.5f) * cell, -half + (j + 0.5f) * cell);
                    float best = float.MaxValue;
                    foreach (var q in samples) best = Mathf.Min(best, (q - middle).sqrMagnitude);
                    distance[i, j] = Mathf.Sqrt(best);
                    if (distance[i, j] < reach) cells.Add(middle - Vector2.one * (cell / 2f));
                }
            float Away(Vector2 p)
            {
                int i = Mathf.Clamp(Mathf.FloorToInt((p.x + half) / cell), 0, n - 1);
                int j = Mathf.Clamp(Mathf.FloorToInt((p.y + half) / cell), 0, n - 1);
                return distance[i, j];
            }
            return (cells, Away);
        }
    }
}

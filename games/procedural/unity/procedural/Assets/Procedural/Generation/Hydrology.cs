using System;
using System.Threading;

namespace Procedural.Generation
{
    /// <summary>Physical fill levels and an acyclic D8 routing graph. Stack contains land, receivers first.</summary>
    public sealed class Drainage
    {
        public float[] Fill, Distance, Discharge, Length;
        public int[] Parent, Receiver, Stack, DonorHead, DonorNext;
        public bool[] Ocean;
    }

    /// <summary>No-epsilon FIFO Priority-Flood, flat routing, donor traversal and catchment accumulation.</summary>
    public static class Hydrology
    {
        /// <summary>Route rainfall on land; sea samples are fixed at zero for routing and receive no rain.</summary>
        public static Drainage Route(Grid height, bool[] ocean, CancellationToken cancel)
        {
            return Route(height, ocean, cancel, null, 0);
        }

        internal static Drainage Route(Grid height, bool[] ocean, CancellationToken cancel, int[] preferredReceivers, float switchFraction, bool descendingSurface = false)
        {
            cancel.ThrowIfCancellationRequested();
            if (height == null)
                throw new ArgumentNullException(nameof(height));
            int n = height.Data.Length, w = height.Width, h = height.Height;
            if (ocean == null || ocean.Length != n)
                throw new ArgumentException("Ocean mask size differs from grid.");
            var d = new Drainage
            {
                Fill = (float[])height.Data.Clone(),
                Ocean = (bool[])ocean.Clone(),
                Parent = new int[n],
                Receiver = new int[n],
                Distance = new float[n],
                Discharge = new float[n],
                Length = new float[n],
                DonorHead = new int[n],
                DonorNext = new int[n]
            };
            var seen = descendingSurface ? null : (bool[])ocean.Clone();
            var heap = descendingSurface ? null : new MinHeap(n);
            int land = 0;
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                d.Parent[i] = d.Receiver[i] = d.DonorHead[i] = d.DonorNext[i] = -1;
                if (!ocean[i])
                {
                    land++;
                    continue;
                }

                d.Fill[i] = 0;
                if (descendingSurface)
                    continue;
                int x = i % w, z = i / w;
                bool coast = false;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, zz = z + dz;
                        if (Math.Abs(dx) + Math.Abs(dz) == 1 && xx >= 0 && xx < w && zz >= 0 && zz < h && !ocean[zz * w + xx])
                            coast = true;
                    }

                if (coast && !descendingSurface)
                    heap.Push(i, 0);
            }

            int steps = 0;
            while (heap != null && heap.Count > 0)
            {
                MathCore.Check(cancel, steps++);
                int i = heap.Pop(), x = i % w, z = i / w;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, zz = z + dz;
                        if (xx < 0 || xx >= w || zz < 0 || zz >= h)
                            continue;
                        int j = zz * w + xx;
                        if (seen[j])
                            continue;
                        seen[j] = true;
                        d.Fill[j] = Math.Max(d.Fill[j], d.Fill[i]);
                        d.Parent[j] = i;
                        heap.Push(j, d.Fill[j]);
                    }
            }

            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                if (ocean[i])
                    continue;
                if (!descendingSurface && !seen[i])
                    throw new ArgumentException("Every land component needs access to a sea cell.");
                int x = i % w, z = i / w, r = -1;
                double best = 0;
                float distance = 1;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, zz = z + dz;
                        if (xx < 0 || xx >= w || zz < 0 || zz >= h || (dx == 0 && dz == 0))
                            continue;
                        int j = zz * w + xx;
                        float len = EdgeLength(i, j, w);
                        double slope = ((double)d.Fill[i] - d.Fill[j]) / len;
                        if (slope > best + Math.Max(1e-10, Math.Abs(best) * 1e-7))
                        {
                            best = slope;
                            r = j;
                            distance = len;
                        }
                    }

                // Relief-only hysteresis: preserve a strictly descending old edge
                // unless a competitor is materially steeper. Strict descent keeps DAG safety.
                if (preferredReceivers != null && preferredReceivers[i] >= 0)
                {
                    int old = preferredReceivers[i];
                    float len = EdgeLength(i, old, w);
                    double oldSlope = ((double)d.Fill[i] - d.Fill[old]) / len;
                    if (oldSlope > 0 && best <= oldSlope * (1 + switchFraction))
                    {
                        r = old;
                        distance = len;
                    }
                }

                if (r < 0)
                {
                    // The relief fast path requires a descent everywhere. Never
                    // guess a flat parent or change flood levels if that fails.
                    if (descendingSurface)
                        return Route(height, ocean, cancel, preferredReceivers, switchFraction);
                    r = d.Parent[i];
                    distance = EdgeLength(i, r, w);
                }

                d.Receiver[i] = r;
                d.Distance[i] = distance * height.CellSize;
            }

            // Prepend in reverse row order so donors are traversed in ascending order.
            for (int i = n - 1; i >= 0; i--)
            {
                MathCore.Check(cancel, i);
                if (!ocean[i])
                {
                    int r = d.Receiver[i];
                    d.DonorNext[i] = d.DonorHead[r];
                    d.DonorHead[r] = i;
                }
            }

            d.Stack = new int[land];
            int tail = 0;
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                if (!ocean[i] && ocean[d.Receiver[i]])
                    d.Stack[tail++] = i;
            }

            for (int k = 0; k < tail; k++)
            {
                MathCore.Check(cancel, k);
                for (int j = d.DonorHead[d.Stack[k]]; j >= 0; j = d.DonorNext[j])
                    d.Stack[tail++] = j;
            }

            if (tail != land)
                throw new InvalidOperationException("Routing cycle or disconnected land.");
            float area = height.CellSize * height.CellSize;
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                d.Discharge[i] = ocean[i] ? 0 : area;
                d.Length[i] = height.CellSize;
            }

            for (int k = land - 1; k >= 0; k--)
            {
                MathCore.Check(cancel, k);
                int i = d.Stack[k], r = d.Receiver[i];
                d.Discharge[r] += d.Discharge[i];
                d.Length[r] = Math.Max(d.Length[r], d.Length[i] + d.Distance[i]);
            }

            return d;
        }

        private static float EdgeLength(int i, int j, int width)
        {
            int dx = j % width - i % width, dz = j / width - i / width;
            return dx != 0 && dz != 0 ? 1.41421356237f : 1;
        }

        /// <summary>Strahler order on a downstream-closed channel mask.</summary>
        public static int[] Strahler(Drainage d, bool[] channel, CancellationToken cancel)
        {
            int[] order = new int[channel.Length], equal = new int[channel.Length];
            for (int k = d.Stack.Length - 1; k >= 0; k--)
            {
                MathCore.Check(cancel, k);
                int i = d.Stack[k];
                if (!channel[i])
                    continue;
                order[i] = order[i] == 0 ? 1 : order[i] + (equal[i] >= 2 ? 1 : 0);
                int r = d.Receiver[i];
                if (r < 0 || !channel[r])
                    continue;
                if (order[i] > order[r])
                {
                    order[r] = order[i];
                    equal[r] = 1;
                }
                else if (order[i] == order[r])
                    equal[r]++;
            }

            return order;
        }
    }
}

using System;
using System.Threading;

namespace Procedural.Generation
{
    internal static class MathCore
    {
        internal static float Clamp(float x, float a, float b)
        {
            return Math.Max(a, Math.Min(b, x));
        }

        internal static float Smooth(float a, float b, float x)
        {
            x = Clamp((x - a) / (b - a), 0, 1);
            return x * x * (3 - 2 * x);
        }

        internal static void Check(CancellationToken c, int i)
        {
            if ((i & 4095) == 0)
                c.ThrowIfCancellationRequested();
        }

        internal static ulong Mix(ulong x)
        {
            unchecked
            {
                x += 0x9e3779b97f4a7c15UL;
                x = (x ^ (x >> 30)) * 0xbf58476d1ce4e5b9UL;
                x = (x ^ (x >> 27)) * 0x94d049bb133111ebUL;
                return x ^ (x >> 31);
            }
        }

        internal static ulong Stage(int seed, string name)
        {
            ulong h = Mix(unchecked((ulong)seed));
            for (int i = 0; i < name.Length; i++)
                h = Mix(h ^ name[i]);
            return h;
        }

        internal static float Random(ref ulong state)
        {
            state = Mix(state);
            return (state >> 40) / 16777216f;
        }

        private static float Value(int x, int y, ulong seed)
        {
            return (Mix(seed ^ unchecked((ulong)(long)x * 0x9e3779b1UL) ^ unchecked((ulong)(long)y * 0x85ebca77UL)) >> 40) / 16777216f;
        }

        internal static float Noise(float x, float y, ulong seed)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float u = x - ix, v = y - iy;
            u = u * u * (3 - 2 * u);
            v = v * v * (3 - 2 * v);
            float a = Value(ix, iy, seed), b = Value(ix + 1, iy, seed), c = Value(ix, iy + 1, seed), d = Value(ix + 1, iy + 1, seed);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        internal static float Fbm(float x, float y, ulong seed, int octaves = 4)
        {
            float sum = 0, amp = 0.5f, total = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Noise(x, y, seed + (ulong)i);
                total += amp;
                amp *= 0.5f;
                x *= 2;
                y *= 2;
            }

            return sum / total;
        }

        internal static void SortIndices(int[] indices, int count, float[] score, bool descending, CancellationToken cancel)
        {
            var scratch = new int[count];
            int[] src = indices, dst = scratch;
            for (int span = 1; span < count; span *= 2)
            {
                for (int start = 0; start < count; start += 2 * span)
                {
                    int middle = Math.Min(start + span, count), end = Math.Min(start + 2 * span, count), a = start, b = middle;
                    for (int k = start; k < end; k++)
                    {
                        Check(cancel, k);
                        bool left = b == end;
                        if (a < middle && b < end)
                        {
                            float av = score[src[a]], bv = score[src[b]];
                            left = av == bv ? src[a] < src[b] : descending ? av > bv : av < bv;
                        }

                        dst[k] = a < middle && left ? src[a++] : src[b++];
                    }
                }

                int[] swap = src;
                src = dst;
                dst = swap;
            }

            if (!ReferenceEquals(src, indices))
                Array.Copy(src, indices, count);
        }

        internal static float Cubic(float a, float b, float c, float d, float t)
        {
            return b + 0.5f * t * (c - a + t * (2 * a - 5 * b + 4 * c - d + t * (3 * (b - c) + d - a)));
        }

        internal static float Sample(Grid g, float x, float z)
        {
            x = Clamp(x, 0, g.Width - 1);
            z = Clamp(z, 0, g.Height - 1);
            int ix = (int)x, iz = (int)z, jx = Math.Min(ix + 1, g.Width - 1), jz = Math.Min(iz + 1, g.Height - 1);
            float u = x - ix, v = z - iz;
            return (g[ix, iz] * (1 - u) + g[jx, iz] * u) * (1 - v) + (g[ix, jz] * (1 - u) + g[jx, jz] * u) * v;
        }

        internal static float Slope(Grid g, int i)
        {
            int x = i % g.Width, z = i / g.Width;
            float dx = (g[Math.Min(x + 1, g.Width - 1), z] - g[Math.Max(0, x - 1), z]) / (2 * g.CellSize);
            float dz = (g[x, Math.Min(z + 1, g.Height - 1)] - g[x, Math.Max(0, z - 1)]) / (2 * g.CellSize);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }

    // Keys are immutable while queued. The insertion sequence makes equal levels FIFO.
    internal sealed class MinHeap
    {
        private readonly int[] cells;
        private readonly float[] keys;
        private readonly long[] ticks;
        private int count;
        private long tick;
        internal int Count
        {
            get
            {
                return count;
            }
        }

        internal MinHeap(int capacity)
        {
            cells = new int[capacity];
            keys = new float[capacity];
            ticks = new long[capacity];
        }

        private bool Before(float a, long at, int b)
        {
            return a < keys[b] || (a == keys[b] && at < ticks[b]);
        }

        internal void Push(int cell, float key)
        {
            int i = count++;
            long t = tick++;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (!Before(key, t, p))
                    break;
                cells[i] = cells[p];
                keys[i] = keys[p];
                ticks[i] = ticks[p];
                i = p;
            }

            cells[i] = cell;
            keys[i] = key;
            ticks[i] = t;
        }

        internal int Pop()
        {
            int result = cells[0], c = cells[--count];
            float key = keys[count];
            long t = ticks[count];
            int i = 0;
            while (i * 2 + 1 < count)
            {
                int k = i * 2 + 1;
                if (k + 1 < count && Before(keys[k + 1], ticks[k + 1], k))
                    k++;
                if (key < keys[k] || (key == keys[k] && t <= ticks[k]))
                    break;
                cells[i] = cells[k];
                keys[i] = keys[k];
                ticks[i] = ticks[k];
                i = k;
            }

            if (count > 0)
            {
                cells[i] = c;
                keys[i] = key;
                ticks[i] = t;
            }

            return result;
        }
    }
}

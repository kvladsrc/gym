using System;
using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>
    /// SplitMix64: the same sequence for a seed on every runtime, so a seed always makes the same
    /// world (System.Random's seeded sequence is an implementation detail).
    /// </summary>
    public sealed class Rng
    {
        ulong state;

        public Rng(int seed)
        {
            state = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL ^ 0x632BE59BD9B4E019UL;
        }

        /// <summary>The generator's whole state, for the saves: set back, the same numbers follow.</summary>
        public ulong State
        {
            get => state;
            set => state = value;
        }

        public ulong NextULong()
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public int NextInt() => (int)(NextULong() >> 33);

        /// <summary>Uniform in [0, 1).</summary>
        public float Value() => (NextULong() >> 40) * (1f / (1 << 24));

        public float Range(float min, float max) => min + (max - min) * Value();

        /// <summary>Uniform in [min, max).</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(NextULong() % (ulong)(max - min));

        public bool Chance(float probability) => Value() < probability;

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    /// <summary>Seeded 2D gradient noise (Perlin) with fractal sums; roughly in [-1, 1].</summary>
    public sealed class Noise2
    {
        readonly int[] perm = new int[512];

        public Noise2(int seed)
        {
            var rng = new Rng(seed);
            var p = new int[256];
            for (int i = 0; i < 256; i++)
                p[i] = i;
            rng.Shuffle(p);
            for (int i = 0; i < 512; i++)
                perm[i] = p[i & 255];
        }

        static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        static float Grad(int hash, float x, float y)
        {
            switch (hash & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        public float Sample(float x, float y)
        {
            int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
            float xf = x - xi, yf = y - yi;
            int X = xi & 255, Y = yi & 255;
            float u = Fade(xf), v = Fade(yf);
            int aa = perm[perm[X] + Y], ab = perm[perm[X] + Y + 1];
            int ba = perm[perm[X + 1] + Y], bb = perm[perm[X + 1] + Y + 1];
            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
            return Lerp(x1, x2, v);
        }

        /// <summary>Fractal sum of octaves, divided by the total amplitude.</summary>
        public float Fbm(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0, amplitude = 1, total = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += amplitude * Sample(x, y);
                total += amplitude;
                x = x * lacunarity + 17.31f;
                y = y * lacunarity - 9.73f;
                amplitude *= gain;
            }
            return sum / total;
        }
    }
}

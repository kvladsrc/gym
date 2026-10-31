using System;
using System.Threading;

namespace Procedural.Generation
{
    /// <summary>Exact separable squared-parabola EDT (Felzenszwalb–Huttenlocher), O(width * height).</summary>
    public static class EuclideanDistance
    {
        /// <summary>Distances in sample units; an empty source returns positive infinity.</summary>
        public static float[] Transform(bool[] source, int width, int height, CancellationToken cancel = default)
        {
            if (source == null || width < 1 || height < 1 || (long)width * height != source.Length)
                throw new ArgumentException("Distance field dimensions differ from source.");
            int size = Math.Max(width, height);
            var input = new double[size];
            var output = new double[size];
            var sites = new int[size];
            var breaks = new double[size + 1];
            var squared = new double[source.Length];
            var result = new float[source.Length];
            for (int z = 0; z < height; z++)
            {
                cancel.ThrowIfCancellationRequested();
                for (int x = 0; x < width; x++)
                    input[x] = source[z * width + x] ? 0 : double.PositiveInfinity;
                Line(input, output, width, sites, breaks);
                for (int x = 0; x < width; x++)
                    squared[z * width + x] = output[x];
            }

            for (int x = 0; x < width; x++)
            {
                cancel.ThrowIfCancellationRequested();
                for (int z = 0; z < height; z++)
                    input[z] = squared[z * width + x];
                Line(input, output, height, sites, breaks);
                for (int z = 0; z < height; z++)
                    result[z * width + x] = (float)Math.Sqrt(output[z]);
            }

            return result;
        }

        private static void Line(double[] f, double[] d, int length, int[] sites, double[] breaks)
        {
            int last = -1;
            for (int q = 0; q < length; q++)
            {
                if (double.IsPositiveInfinity(f[q]))
                    continue;
                double crossing = double.NegativeInfinity;
                while (last >= 0)
                {
                    int p = sites[last];
                    crossing = ((f[q] + (double)q * q) - (f[p] + (double)p * p)) / (2 * (q - p));
                    if (crossing > breaks[last])
                        break;
                    last--;
                }

                last++;
                sites[last] = q;
                breaks[last] = last == 0 ? double.NegativeInfinity : crossing;
                breaks[last + 1] = double.PositiveInfinity;
            }

            int k = 0;
            for (int q = 0; q < length; q++)
            {
                if (last < 0)
                {
                    d[q] = double.PositiveInfinity;
                    continue;
                }

                while (breaks[k + 1] < q)
                    k++;
                double delta = q - sites[k];
                d[q] = delta * delta + f[sites[k]];
            }
        }
    }
}

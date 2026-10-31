using System;
using System.Collections.Generic;
using System.Threading;

namespace Procedural.Generation
{
    // Exact Euclidean distance to the union of linearly tapered river capsules
    // and labelled lake cells (squares centred on height samples). A BVH only
    // prunes provably farther boxes; there is no raster/chamfer approximation,
    // finite search radius or nearest-centreline shortcut at variable widths.
    internal sealed class WaterProximity
    {
        internal readonly struct Bank
        {
            internal readonly double X, Z, U, V, Length, R0, R1;
            internal readonly double MinX, MaxX, MinZ, MaxZ;
            internal Bank(double ax, double az, double bx, double bz, double r0, double r1)
            {
                X = ax;
                Z = az;
                double dx = bx - ax, dz = bz - az;
                Length = Math.Sqrt(dx * dx + dz * dz);
                U = Length > 0 ? dx / Length : 1;
                V = Length > 0 ? dz / Length : 0;
                R0 = r0;
                R1 = r1;
                MinX = Math.Min(ax - r0, bx - r1);
                MaxX = Math.Max(ax + r0, bx + r1);
                MinZ = Math.Min(az - r0, bz - r1);
                MaxZ = Math.Max(az + r0, bz + r1);
            }

            internal double Distance(double x, double z)
            {
                double dx = x - X, dz = z - Z;
                if (Length <= Math.Abs(R1 - R0))
                {
                    double a = Math.Sqrt(dx * dx + dz * dz) - R0;
                    dx -= U * Length;
                    dz -= V * Length;
                    return Math.Max(0, Math.Min(a, Math.Sqrt(dx * dx + dz * dz) - R1));
                }

                double along = dx * U + dz * V, across = Math.Abs(dz * U - dx * V);
                double gradient = (R1 - R0) / Length;
                double s = Math.Max(0, Math.Min(Length, along + gradient * across / Math.Sqrt(1 - gradient * gradient)));
                double delta = s - along;
                return Math.Max(0, Math.Sqrt(delta * delta + across * across) - R0 - gradient * s);
            }
        }

        private sealed class Node
        {
            internal double MinX, MaxX, MinZ, MaxZ;
            internal int Start, Count;
            internal Node Left, Right;
            internal double LowerBound(double x, double z)
            {
                double dx = Math.Max(0, Math.Max(MinX - x, x - MaxX));
                double dz = Math.Max(0, Math.Max(MinZ - z, z - MaxZ));
                return dx * dx + dz * dz;
            }
        }

        private sealed class BankComparer : IComparer<Bank>
        {
            internal bool AlongX;
            public int Compare(Bank a, Bank b)
            {
                return AlongX ? (a.MinX + a.MaxX).CompareTo(b.MinX + b.MaxX) : (a.MinZ + a.MaxZ).CompareTo(b.MinZ + b.MaxZ);
            }
        }

        private readonly Bank[] banks;
        private readonly Node root;
        internal WaterProximity(Bank[] banks, CancellationToken cancel = default)
        {
            this.banks = (Bank[])banks.Clone();
            if (banks.Length > 0)
                root = Build(0, banks.Length, cancel);
        }

        private Node Build(int start, int count, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            var node = new Node
            {
                Start = start,
                Count = count,
                MinX = double.PositiveInfinity,
                MinZ = double.PositiveInfinity,
                MaxX = double.NegativeInfinity,
                MaxZ = double.NegativeInfinity
            };
            for (int k = start; k < start + count; k++)
            {
                node.MinX = Math.Min(node.MinX, banks[k].MinX);
                node.MaxX = Math.Max(node.MaxX, banks[k].MaxX);
                node.MinZ = Math.Min(node.MinZ, banks[k].MinZ);
                node.MaxZ = Math.Max(node.MaxZ, banks[k].MaxZ);
            }

            if (count > 8)
            {
                Array.Sort(banks, start, count, new BankComparer { AlongX = node.MaxX - node.MinX >= node.MaxZ - node.MinZ });
                int half = count / 2;
                node.Left = Build(start, half, cancel);
                node.Right = Build(start + half, count - half, cancel);
            }

            return node;
        }

        internal double Distance(double x, double z)
        {
            double best = double.PositiveInfinity;
            if (root != null)
                Search(root, x, z, ref best);
            return best;
        }

        private void Search(Node node, double x, double z, ref double best)
        {
            if (node.LowerBound(x, z) >= best * best)
                return;
            if (node.Left == null)
            {
                for (int k = node.Start; k < node.Start + node.Count; k++)
                    best = Math.Min(best, banks[k].Distance(x, z));
                return;
            }

            Node first = node.Left, second = node.Right;
            if (second.LowerBound(x, z) < first.LowerBound(x, z))
            {
                first = node.Right;
                second = node.Left;
            }

            Search(first, x, z, ref best);
            Search(second, x, z, ref best);
        }

        internal static float[] Distances(Grid g, List<RiverSegment> rivers, int[] lakeId, CancellationToken cancel)
        {
            var banks = new List<Bank>();
            foreach (RiverSegment river in rivers)
                for (int k = 0; k < river.X.Length - 1; k++)
                {
                    MathCore.Check(cancel, k);
                    banks.Add(new Bank(river.X[k], river.Z[k], river.X[k + 1], river.Z[k + 1], river.Width[k] * 0.5, river.Width[k + 1] * 0.5));
                }

            double half = g.CellSize * 0.5;
            for (int i = 0; i < lakeId.Length; i++)
            {
                MathCore.Check(cancel, i);
                if (lakeId[i] < 0)
                    continue;
                int x = i % g.Width, z = i / g.Width;
                double ax = x * (double)g.CellSize - half, az = z * (double)g.CellSize - half;
                double bx = ax + g.CellSize, bz = az + g.CellSize;
                if (x == 0 || lakeId[i - 1] < 0)
                    banks.Add(new Bank(ax, az, ax, bz, 0, 0));
                if (x == g.Width - 1 || lakeId[i + 1] < 0)
                    banks.Add(new Bank(bx, az, bx, bz, 0, 0));
                if (z == 0 || lakeId[i - g.Width] < 0)
                    banks.Add(new Bank(ax, az, bx, az, 0, 0));
                if (z == g.Height - 1 || lakeId[i + g.Width] < 0)
                    banks.Add(new Bank(ax, bz, bx, bz, 0, 0));
            }

            var tree = new WaterProximity(banks.ToArray(), cancel);
            var result = new float[g.Data.Length];
            for (int i = 0; i < result.Length; i++)
            {
                MathCore.Check(cancel, i);
                result[i] = lakeId[i] >= 0 ? 0 : (float)tree.Distance(i % g.Width * (double)g.CellSize, i / g.Width * (double)g.CellSize);
            }

            return result;
        }
    }
}

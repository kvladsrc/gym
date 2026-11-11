using System;

namespace Realms.Core
{
    /// <summary>
    /// Axial coordinates of a pointy-top hexagon. The map stores its cells in "odd-r" offset
    /// coordinates (column, row): odd rows are shifted half a cell towards +x.
    /// </summary>
    public readonly struct Hex : IEquatable<Hex>
    {
        public readonly int Q, R;

        public Hex(int q, int r)
        {
            Q = q;
            R = r;
        }

        public int S => -Q - R;

        /// <summary>
        /// The six neighbour steps, counter-clockwise seen from above, starting at +x (east). The
        /// neighbour in direction d lies at 60·d degrees, across edge d, which runs from corner d
        /// to corner d + 1 (see <see cref="HexMetrics"/>).
        /// </summary>
        public static readonly Hex[] Directions =
        {
            new Hex(1, 0), new Hex(0, 1), new Hex(-1, 1), new Hex(-1, 0), new Hex(0, -1), new Hex(1, -1),
        };

        public Hex Neighbor(int direction)
        {
            Hex d = Directions[direction];
            return new Hex(Q + d.Q, R + d.R);
        }

        public static int Distance(Hex a, Hex b) =>
            (Math.Abs(a.Q - b.Q) + Math.Abs(a.R - b.R) + Math.Abs(a.S - b.S)) / 2;

        public static Hex FromOffset(int col, int row) => new Hex(col - ((row - (row & 1)) >> 1), row);

        public int Col => Q + ((R - (R & 1)) >> 1);
        public int Row => R;

        /// <summary>The cell whose hexagon contains the cube coordinates, rounded.</summary>
        public static Hex Round(float q, float r)
        {
            float s = -q - r;
            int qi = (int)MathF.Round(q), ri = (int)MathF.Round(r), si = (int)MathF.Round(s);
            float dq = MathF.Abs(qi - q), dr = MathF.Abs(ri - r), ds = MathF.Abs(si - s);
            if (dq > dr && dq > ds)
                qi = -ri - si;
            else if (dr > ds)
                ri = -qi - si;
            return new Hex(qi, ri);
        }

        public bool Equals(Hex other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is Hex other && Equals(other);
        public override int GetHashCode() => (Q * 397) ^ R;
        public static bool operator ==(Hex a, Hex b) => a.Equals(b);
        public static bool operator !=(Hex a, Hex b) => !a.Equals(b);
        public override string ToString() => $"({Col}, {Row})";
    }

    /// <summary>
    /// World layout of the hexagons on the ground plane (x, z) with y up: unit outer radius, pointy
    /// top (a corner points to +z). Corner i lies at 60·i − 30 degrees from +x, counter-clockwise
    /// seen from above.
    /// </summary>
    public static class HexMetrics
    {
        public const float Sqrt3 = 1.7320508f;

        /// <summary>Centre to corner.</summary>
        public const float Radius = 1f;

        /// <summary>Centre to the middle of an edge.</summary>
        public const float InnerRadius = Sqrt3 * 0.5f;

        public static readonly float[] CornerX = new float[6];
        public static readonly float[] CornerZ = new float[6];

        static HexMetrics()
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = (60f * i - 30f) * MathF.PI / 180f;
                CornerX[i] = Radius * MathF.Cos(angle);
                CornerZ[i] = Radius * MathF.Sin(angle);
            }
        }

        public static float CenterX(Hex h) => Sqrt3 * (h.Q + 0.5f * h.R);
        public static float CenterZ(Hex h) => 1.5f * h.R;

        public static Hex FromWorld(float x, float z) =>
            Hex.Round((Sqrt3 / 3f * x - z / 3f) / Radius, (2f / 3f * z) / Radius);
    }
}

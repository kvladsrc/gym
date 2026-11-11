using Realms.Core;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// The line a cell's roads follow over the ground, as Terrain.shader draws them (Roads and
    /// RoadBend), for the props that keep off them. A road crosses each edge in the cell's mask at
    /// the edge's middle, square to it. In a hub, a cell with a town, a resource or a place in its
    /// middle, and where one road ends or three or more meet, the roads run straight to the
    /// middle. A road that just passes through, in at one edge and out at another not opposite,
    /// turns in an arc round the inside of the turn instead: straight pieces meeting at an angle
    /// in each cell's middle read as badly joined fragments (2026-11-10). For a gentle turn (edges
    /// two apart) the arc runs from edge to edge; for a sharp one (edges side by side) it is
    /// tighter, nearer the middle, with straight arms out to the edges, where the bridges cross.
    /// </summary>
    public static class RoadShape
    {
        /// <summary>The bit of a cell's road byte in the cells texture that marks a hub.</summary>
        public const int HubBit = 64;

        /// <summary>Whether something stands in the cell's middle, which its roads lead to.</summary>
        public static bool Hub(Cell cell) => cell.Town >= 0 || cell.Resource != Resource.None || cell.Place != Core.Place.None;

        /// <summary>Whether a road bends through the cell, in at edge <paramref name="a"/> and out at edge <paramref name="b"/>.</summary>
        public static bool Bend(byte roads, bool hub, out int a, out int b)
        {
            a = b = -1;
            if (hub)
                return false;
            for (int d = 0; d < 6; d++)
            {
                if ((roads >> d & 1) == 0)
                    continue;
                if (a < 0)
                    a = d;
                else if (b < 0)
                    b = d;
                else
                    return false;
            }
            return b >= 0 && b - a != 3;
        }

        /// <summary>How far a point, from the cell's centre, lies from the middle line of the cell's nearest road.</summary>
        public static float Distance(byte roads, bool hub, Vector2 local)
        {
            if (Bend(roads, hub, out int a, out int b))
                return BendDistance(local, a, b);
            float best = float.MaxValue;
            for (int d = 0; d < 6; d++)
            {
                if ((roads >> d & 1) == 0)
                    continue;
                Vector2 dir = Direction(d);
                float t = Mathf.Clamp(Vector2.Dot(local, dir), 0f, HexMetrics.InnerRadius);
                best = Mathf.Min(best, (local - dir * t).magnitude);
            }
            return best;
        }

        static float BendDistance(Vector2 local, int a, int b)
        {
            Vector2 ea = Direction(a), eb = Direction(b);
            bool sharp = Vector2.Dot(ea, eb) > 0f;
            // Half the angle between the arms is 30 degrees for a sharp turn and 60 for a gentle
            // one; the arc touches both arms where they end, inner from the centre.
            float half = (sharp ? 30f : 60f) * Mathf.Deg2Rad, radius = sharp ? 0.35f : 1.5f;
            float inner = Mathf.Min(radius / Mathf.Tan(half), HexMetrics.InnerRadius);
            Vector2 f = (ea + eb).normalized * (radius / Mathf.Sin(half));
            float ta = Mathf.Clamp(Vector2.Dot(local, ea), inner, HexMetrics.InnerRadius);
            float tb = Mathf.Clamp(Vector2.Dot(local, eb), inner, HexMetrics.InnerRadius);
            float d = Mathf.Min((local - ea * ta).magnitude, (local - eb * tb).magnitude);
            Vector2 w = local - f, wa = ea * inner - f, wb = eb * inner - f;
            float turn = Cross(wa, wb);
            if (Cross(wa, w) * turn >= 0f && Cross(w, wb) * turn >= 0f)
                d = Mathf.Min(d, Mathf.Abs(w.magnitude - radius));
            return d;
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        static Vector2 Direction(int d) => new Vector2(Mathf.Cos(d * Mathf.PI / 3f), Mathf.Sin(d * Mathf.PI / 3f));
    }
}

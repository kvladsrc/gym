using System.Collections.Generic;
using UnityEngine;

namespace FinishTheGame
{
    /// <summary>The story's curve on the ground (y = 0), sampled evenly by its
    /// length: a closed loop (<see cref="Ring"/>) or an open route that may
    /// pass the same places again (<see cref="Through"/>). Every place near it
    /// has its (s, t): s how far along the curve, t how far to its right
    /// (negative: to its left). On a route that passes a place twice, the
    /// place has two (s, t): <see cref="Project(Vector3, float, float)"/>
    /// finds the one near a given s.</summary>
    public class StoryPath
    {
        public readonly float Length;
        public readonly bool Closed;
        readonly Vector3[] points;
        readonly float step;

        StoryPath(IReadOnlyList<Vector3> fine, bool closed, float spacing)
        {
            Closed = closed;
            int n = fine.Count;
            var along = new float[n];
            for (int i = 1; i < n; i++) along[i] = along[i - 1] + Vector3.Distance(fine[i - 1], fine[i]);
            Length = along[n - 1];
            int count = Mathf.CeilToInt(Length / spacing);
            step = Length / count;
            points = new Vector3[closed ? count : count + 1];
            for (int i = 0, j = 0; i < points.Length; i++)
            {
                float s = Mathf.Min(i * step, Length);
                while (j < n - 2 && along[j + 1] < s) j++;
                points[i] = Vector3.Lerp(fine[j], fine[j + 1], Mathf.InverseLerp(along[j], along[j + 1], s));
            }
        }

        /// <summary>A ring round the origin, its radius waving (<see cref="RingRadius"/>),
        /// walked counter-clockwise (seen from above), so the ring's middle is on the left.</summary>
        public static StoryPath Ring(float spacing = 0.5f)
        {
            const int Fine = 16384;
            var fine = new Vector3[Fine + 1];
            for (int i = 0; i <= Fine; i++)
            {
                float angle = 2f * Mathf.PI * i / Fine;
                float r = RingRadius(angle);
                fine[i] = new Vector3(r * Mathf.Cos(angle), 0, r * Mathf.Sin(angle));
            }
            return new StoryPath(fine, true, spacing);
        }

        /// <summary>The ring's radius at the angle θ round the origin (from +x
        /// towards +z): r(θ) = 100 + 16 sin 3θ + 6 sin(5θ + 0.5) + 10 sin(2θ + 1).
        /// Its sharpest turns are of about 32 and 36 m.</summary>
        public static float RingRadius(float angle) =>
            100f + 16f * Mathf.Sin(3 * angle) + 6f * Mathf.Sin(5 * angle + 0.5f) + 10f * Mathf.Sin(2 * angle + 1);

        /// <summary>An open route through these points on the ground, smoothed
        /// (a centripetal Catmull–Rom curve through each of them).</summary>
        public static StoryPath Through(IReadOnlyList<Vector2> waypoints, float spacing = 0.5f)
        {
            var fine = new List<Vector3>();
            int last = waypoints.Count - 1;
            Vector2 At(int i) => waypoints[Mathf.Clamp(i, 0, last)];
            for (int i = 0; i < last; i++)
            {
                Vector2 p0 = At(i - 1), p1 = At(i), p2 = At(i + 1), p3 = At(i + 2);
                if (i == 0) p0 = p1 - (p2 - p1);
                if (i == last - 1) p3 = p2 + (p2 - p1);
                int pieces = Mathf.Max(4, Mathf.CeilToInt(Vector2.Distance(p1, p2) / 0.25f));
                for (int k = 0; k < pieces; k++)
                {
                    var p = CatmullRom(p0, p1, p2, p3, (float)k / pieces);
                    fine.Add(new Vector3(p.x, 0, p.y));
                }
            }
            fine.Add(new Vector3(waypoints[last].x, 0, waypoints[last].y));
            return new StoryPath(fine, false, spacing);
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float u)
        {
            float Knot(float t, Vector2 a, Vector2 b) => t + Mathf.Max(1e-4f, Mathf.Sqrt(Vector2.Distance(a, b)));
            float t0 = 0, t1 = Knot(t0, p0, p1), t2 = Knot(t1, p1, p2), t3 = Knot(t2, p2, p3);
            float t = Mathf.Lerp(t1, t2, u);
            var a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
            var a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
            var a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
            var b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
            var b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
            return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
        }

        /// <summary>s onto the curve: round the loop into [0, Length), or held to the route's ends.</summary>
        public float Wrap(float s) => Closed ? s - Length * Mathf.Floor(s / Length) : Mathf.Clamp(s, 0, Length);

        public Vector3 Position(float s)
        {
            float f = Wrap(s) / step;
            int i = (int)f;
            return Vector3.Lerp(points[Index(i)], points[Index(i + 1)], f - i);
        }

        public Vector3 Tangent(float s)
        {
            if (!Closed) s = Mathf.Clamp(s, step, Length - step);
            return (Position(s + step) - Position(s - step)).normalized;
        }

        public Vector3 Right(float s)
        {
            var t = Tangent(s);
            return new Vector3(t.z, 0, -t.x);
        }

        /// <summary>The road's heading at s: radians from +z towards +x.</summary>
        public float Heading(float s)
        {
            var t = Tangent(s);
            return Mathf.Atan2(t.x, t.z);
        }

        public Vector3 At(float s, float t, float height = 0f) => Position(s) + Right(s) * t + Vector3.up * height;

        /// <summary>A point's (s, t), looked for within <paramref name="window"/>
        /// metres of <paramref name="near"/>; the s comes back unwrapped, close
        /// to <paramref name="near"/>.</summary>
        public Vector2 Project(Vector3 p, float near, float window = 30f)
        {
            int n = Mathf.CeilToInt(window / step);
            int centre = Mathf.RoundToInt(near / step);
            float best = float.MaxValue;
            int at = centre;
            for (int k = -n; k <= n; k++)
            {
                if (!Closed && (centre + k < 0 || centre + k >= points.Length)) continue;
                var q = points[Index(centre + k)];
                float d = (q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z);
                if (d < best)
                {
                    best = d;
                    at = centre + k;
                }
            }
            // Onto the segment before or after the nearest sample.
            var a = points[Index(at - 1)];
            var b = points[Index(at)];
            var c = points[Index(at + 1)];
            float s = at * step + Along(p, b, c) * step;
            if (Along(p, b, c) <= 0f) s = (at - 1) * step + Along(p, a, b) * step;
            if (!Closed) s = Mathf.Clamp(s, 0, Length);
            var flat = new Vector3(p.x, 0, p.z);
            float t = Vector3.Dot(flat - Position(s), Right(s));
            return new Vector2(s, t);
        }

        /// <summary>A point's (s, t) anywhere: a coarse look along the whole
        /// curve, then a near one (on a route that passes it twice, the nearer pass).</summary>
        public Vector2 Project(Vector3 p)
        {
            float best = float.MaxValue, near = 0;
            for (int i = 0; i < points.Length; i += 8)
            {
                var q = points[i];
                float d = (q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z);
                if (d < best)
                {
                    best = d;
                    near = i * step;
                }
            }
            return Project(p, near, 8 * step + 2f);
        }

        int Index(int i) => Closed
            ? ((i % points.Length) + points.Length) % points.Length
            : Mathf.Clamp(i, 0, points.Length - 1);

        static float Along(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = new Vector3(b.x - a.x, 0, b.z - a.z);
            if (ab.sqrMagnitude < 1e-8f) return 0f;
            return Mathf.Clamp01(Vector3.Dot(new Vector3(p.x - a.x, 0, p.z - a.z), ab) / ab.sqrMagnitude);
        }
    }
}

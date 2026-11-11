using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realms
{
    /// <summary>
    /// A river as it is drawn: the course along the hexagon edges rounded off and sampled densely,
    /// a little into the water at the mouth, wider the more water it carries and narrow at the
    /// spring. <see cref="TerrainField"/> sets the water level along it.
    /// </summary>
    public sealed class RiverCourse
    {
        public const float Spacing = 0.08f;

        public readonly List<Vector2> Points = new List<Vector2>();

        /// <summary>The full width of the water at each point.</summary>
        public readonly List<float> Widths = new List<float>();

        /// <summary>Distance from the spring, for the flow animation.</summary>
        public readonly List<float> Along = new List<float>();

        /// <summary>The height of the water surface at each point, never rising downstream.</summary>
        public float[] Levels;

        /// <summary>How much of the gravel bank shows at each point: none out at sea or inside a bigger river.</summary>
        public float[] Banks;

        /// <summary>The river this one flows into, or -1 when it reaches the sea or a lake.</summary>
        public readonly int Joins;

        public bool HasMouth => Joins < 0;

        RiverCourse(int joins) => Joins = joins;

        /// <summary>The gravel strip each side of the water.</summary>
        public const float Bank = 0.06f;

        public static float Width(float flow) => Mathf.Min(0.11f + 0.05f * Mathf.Sqrt(flow), 0.42f);

        public static List<RiverCourse> FromWorld(World world)
        {
            var courses = new List<RiverCourse>();
            foreach (River river in world.Rivers)
            {
                var points = new List<Vector2>();
                var flows = new List<float>();
                foreach (RiverPoint p in river.Points)
                {
                    points.Add(new Vector2(p.X, p.Z));
                    flows.Add(p.Flow);
                }
                if (river.Mouth >= 0)
                {
                    // On into the water, a little past the shore.
                    Vector2 end = points[points.Count - 1];
                    var mouth = new Vector2(world.CenterX(river.Mouth), world.CenterZ(river.Mouth));
                    points.Add(end + (mouth - end).normalized * 0.5f);
                    flows.Add(flows[flows.Count - 1]);
                }
                for (int k = 0; k < 3; k++)
                    Chaikin(points, flows);
                var course = new RiverCourse(river.Joins);
                course.Sample(points, flows);
                courses.Add(course);
            }
            return courses;
        }

        /// <summary>Cuts every inner corner of the polyline, keeping the ends where they are.</summary>
        static void Chaikin(List<Vector2> points, List<float> flows)
        {
            var p = new List<Vector2> { points[0] };
            var f = new List<float> { flows[0] };
            for (int k = 0; k + 1 < points.Count; k++)
            {
                Vector2 a = points[k], b = points[k + 1];
                if (k > 0)
                {
                    p.Add(Vector2.Lerp(a, b, 0.25f));
                    f.Add(Mathf.Lerp(flows[k], flows[k + 1], 0.25f));
                }
                if (k + 2 < points.Count)
                {
                    p.Add(Vector2.Lerp(a, b, 0.75f));
                    f.Add(Mathf.Lerp(flows[k], flows[k + 1], 0.75f));
                }
            }
            p.Add(points[points.Count - 1]);
            f.Add(flows[flows.Count - 1]);
            points.Clear();
            points.AddRange(p);
            flows.Clear();
            flows.AddRange(f);
        }

        void Sample(List<Vector2> points, List<float> flows)
        {
            float total = 0;
            for (int k = 0; k + 1 < points.Count; k++)
                total += (points[k + 1] - points[k]).magnitude;
            int count = Mathf.Max(2, Mathf.CeilToInt(total / Spacing) + 1);
            float step = total / (count - 1);
            int segment = 0;
            float start = 0;
            for (int s = 0; s < count; s++)
            {
                float at = Mathf.Min(s * step, total);
                while (segment + 2 < points.Count && start + (points[segment + 1] - points[segment]).magnitude < at)
                {
                    start += (points[segment + 1] - points[segment]).magnitude;
                    segment++;
                }
                float length = (points[segment + 1] - points[segment]).magnitude;
                float t = length > 0 ? Mathf.Clamp01((at - start) / length) : 0;
                Points.Add(Vector2.Lerp(points[segment], points[segment + 1], t));
                float width = Width(Mathf.Lerp(flows[segment], flows[segment + 1], t));
                // A thin brook at the spring, widening over the first stretch.
                Widths.Add(width * Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, at / 0.9f)));
                Along.Add(at);
            }
        }

        /// <summary>
        /// One mesh for all the rivers, the bigger ones first so a tributary's water lies over the
        /// bank of the river it joins. Each point gives a row of three vertices across, water and
        /// banks together, just above the water level. uv0: across (-1..1) and the distance from the
        /// spring; uv1: the water's share of the half-width, fade at the mouth, rapids, bank.
        /// </summary>
        public static Mesh BuildMesh(List<RiverCourse> rivers)
        {
            var positions = new List<Vector3>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector4>();
            var indices = new List<int>();
            foreach (RiverCourse river in rivers)
            {
                int n = river.Points.Count, first = positions.Count;
                float length = river.Along[n - 1];
                for (int k = 0; k < n; k++)
                {
                    Vector2 p = river.Points[k];
                    Vector2 dir = river.Points[Mathf.Min(k + 1, n - 1)] - river.Points[Mathf.Max(k - 1, 0)];
                    dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector2.right;
                    var side = new Vector2(-dir.y, dir.x);
                    float water = river.Widths[k] * 0.5f, half = water + Bank;
                    // Rapids where the water drops quickly.
                    float drop = river.Levels[Mathf.Max(k - 1, 0)] - river.Levels[Mathf.Min(k + 1, n - 1)];
                    float rapids = Mathf.Clamp01(drop / (2f * Spacing) * 3f);
                    float fade = river.HasMouth ? Mathf.SmoothStep(0f, 1f, (length - river.Along[k]) / 0.45f) : 1f;
                    float y = river.Levels[k] + 0.004f;
                    for (int c = -1; c <= 1; c++)
                    {
                        Vector2 q = p + side * (half * c);
                        positions.Add(new Vector3(q.x, y, q.y));
                        uv0.Add(new Vector2(c, river.Along[k]));
                        uv1.Add(new Vector4(water / half, fade, rapids, river.Banks[k]));
                    }
                    if (k == 0)
                        continue;
                    for (int c = 0; c < 2; c++)
                    {
                        int a = first + (k - 1) * 3 + c, b = a + 1, d = a + 3, e = a + 4;
                        indices.Add(a); indices.Add(b); indices.Add(d);
                        indices.Add(b); indices.Add(e); indices.Add(d);
                    }
                }
            }
            var mesh = new Mesh { name = "Rivers", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(positions);
            var normals = new Vector3[positions.Count];
            for (int k = 0; k < normals.Length; k++)
                normals[k] = Vector3.up;
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The nearest point of the course to p: its index and the distance.</summary>
        public int Nearest(Vector2 p, out float distance)
        {
            int best = 0;
            float d2 = float.MaxValue;
            for (int k = 0; k < Points.Count; k++)
            {
                float d = (Points[k] - p).sqrMagnitude;
                if (d < d2)
                {
                    d2 = d;
                    best = k;
                }
            }
            distance = Mathf.Sqrt(d2);
            return best;
        }
    }
}

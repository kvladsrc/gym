using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realms
{
    /// <summary>
    /// Collects flat-shaded triangles with vertex colours into one mesh: the low-poly props (trees,
    /// peaks, rocks, haystacks, banners). Colours go in as screen colours and are stored linear.
    /// Alpha marks stone: 1 for everything else; below 1 the prop shader lays the rock texture over
    /// the colour, and snow where alpha rises above <see cref="Stone"/> (see <see cref="Lathe"/>).
    /// Plants added between <see cref="Sway"/> and <see cref="Still"/> bend in the wind: the second
    /// UVs carry how far each vertex moves and the plant's phase (RealmsSway in the shaders).
    /// </summary>
    public sealed class MeshKit
    {
        readonly List<Vector3> positions = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> indices = new List<int>();
        readonly List<Vector2> sways = new List<Vector2>();
        float swayFrom, swayAmount, swayPhase;

        public int VertexCount => positions.Count;

        /// <summary>
        /// What is added from now on sways in the wind, <paramref name="amount"/> world units for
        /// every unit of height above <paramref name="baseY"/>, in its own <paramref name="phase"/>
        /// (0..1), until <see cref="Still"/>.
        /// </summary>
        public void Sway(float baseY, float amount, float phase)
        {
            swayFrom = baseY;
            swayAmount = amount;
            swayPhase = phase;
        }

        /// <summary>What is added from now on stands still.</summary>
        public void Still() => swayAmount = 0f;

        /// <summary>A phase for the plant at <paramref name="at"/>, from its place rather than a random draw.</summary>
        public static float PhaseAt(Vector3 at) => Mathf.Repeat(Mathf.Sin(at.x * 12.9898f + at.z * 78.233f) * 43758.547f, 1f);

        Vector2 SwayOf(Vector3 p) => new Vector2(swayAmount * Mathf.Max(0f, p.y - swayFrom), swayPhase);

        /// <summary>Vertex alpha of bare stone; snow takes it up towards 1 (see <see cref="StoneAlpha"/>).</summary>
        public const float Stone = 0.02f;

        /// <summary>
        /// Alpha for stone at a height fraction of its solid (0 at the foot, 1 at the top): bare
        /// below <paramref name="snowFrom"/>, rising to just under 1 at the top above it.
        /// </summary>
        public static float StoneAlpha(float fraction, float snowFrom) =>
            Stone + 0.97f * Mathf.Clamp01((fraction - snowFrom) / Mathf.Max(1f - snowFrom, 1e-3f));

        /// <summary>
        /// One flat triangle, wound so that its front faces <paramref name="outward"/>. Not stone:
        /// the colour's alpha is ignored (shaded colours such as <c>color * 0.9f</c> scale it too).
        /// </summary>
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color, Vector3 outward) =>
            Triangle(a, b, c, color, outward, 1f, 1f, 1f);

        /// <summary>A flat triangle with an alpha at each corner (for stone and snow).</summary>
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color, Vector3 outward, float alphaA, float alphaB, float alphaC)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, outward) < 0)
            {
                (b, c) = (c, b);
                (alphaB, alphaC) = (alphaC, alphaB);
                n = -n;
            }
            n.Normalize();
            Color linear = color.linear;
            int i = positions.Count;
            positions.Add(a);
            positions.Add(b);
            positions.Add(c);
            sways.Add(SwayOf(a));
            sways.Add(SwayOf(b));
            sways.Add(SwayOf(c));
            float[] alphas = { alphaA, alphaB, alphaC };
            for (int k = 0; k < 3; k++)
            {
                normals.Add(n);
                linear.a = alphas[k];
                colors.Add(linear);
                indices.Add(i + k);
            }
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, Vector3 outward)
        {
            Triangle(a, b, c, color, outward);
            Triangle(a, c, d, color, outward);
        }

        /// <summary>
        /// A solid turned around a vertical axis: rings (height, radius) from the bottom up, joined
        /// by flat faces; band i (between rings i and i + 1) takes colours[i]. A ring of radius 0 is a
        /// point. Ring points jitter by <paramref name="jitter"/> (share of the radius) and each face
        /// shades a little, so the facets read. A top ring wider than 0 gets a flat cap. With
        /// <paramref name="stone"/> the faces are rock, snowed over above the height fraction
        /// <paramref name="snowFrom"/> (none when it is above 1).
        /// </summary>
        public void Lathe(Vector3 origin, float[] heights, float[] radii, Color[] bands, int sides, Rng rng,
            float jitter = 0f, float faceJitter = 0.06f, bool stone = false, float snowFrom = 2f)
        {
            int rings = heights.Length;
            var points = new Vector3[rings, sides];
            float yaw = rng.Range(0f, Mathf.PI * 2f);
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < sides; s++)
            {
                float angle = yaw + s * Mathf.PI * 2f / sides;
                float radius = radii[r] * (1f + jitter * rng.Range(-1f, 1f));
                float height = heights[r] + (r > 0 ? jitter * radii[0] * rng.Range(-0.5f, 0.5f) : 0f);
                points[r, s] = origin + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            }
            float foot = heights[0], span = Mathf.Max(heights[rings - 1] - foot, 1e-4f);
            float Alpha(Vector3 p) => stone ? StoneAlpha((p.y - origin.y - foot) / span, snowFrom) : 1f;
            for (int r = 0; r < rings - 1; r++)
            for (int s = 0; s < sides; s++)
            {
                int t = (s + 1) % sides;
                Vector3 b0 = points[r, s], b1 = points[r, t], t0 = points[r + 1, s], t1 = points[r + 1, t];
                Color color = bands[Mathf.Min(r, bands.Length - 1)] * (1f + faceJitter * rng.Range(-1f, 1f));
                Vector3 middle = (b0 + b1 + t0 + t1) * 0.25f;
                Vector3 outward = middle - new Vector3(origin.x, middle.y, origin.z);
                if (radii[r] <= 0f)
                    Triangle(b0, t1, t0, color, outward, Alpha(b0), Alpha(t1), Alpha(t0));
                else if (radii[r + 1] <= 0f)
                    Triangle(b0, b1, t0, color, outward, Alpha(b0), Alpha(b1), Alpha(t0));
                else
                {
                    Triangle(b0, b1, t1, color, outward, Alpha(b0), Alpha(b1), Alpha(t1));
                    Triangle(b0, t1, t0, color, outward, Alpha(b0), Alpha(t1), Alpha(t0));
                }
            }
            if (radii[rings - 1] > 0f)
            {
                Vector3 top = origin + Vector3.up * heights[rings - 1];
                Color color = bands[bands.Length - 1];
                for (int s = 0; s < sides; s++)
                {
                    Vector3 a = points[rings - 1, s], b = points[rings - 1, (s + 1) % sides];
                    Triangle(top, a, b, color, Vector3.up, Alpha(top), Alpha(a), Alpha(b));
                }
            }
        }

        /// <summary>A rough ball: a lathe with a sine profile, flattened by <paramref name="radii"/>; rock with <paramref name="stone"/>.</summary>
        public void Blob(Vector3 center, Vector3 radii, int sides, int rings, Color bottom, Color top, Rng rng,
            float jitter, bool stone = false)
        {
            var heights = new float[rings + 1];
            var widths = new float[rings + 1];
            var bands = new Color[rings];
            for (int r = 0; r <= rings; r++)
            {
                float a = Mathf.PI * r / rings;
                heights[r] = -Mathf.Cos(a) * radii.y;
                widths[r] = Mathf.Sin(a) * radii.x;
            }
            widths[0] = widths[rings] = 0f;
            for (int r = 0; r < rings; r++)
                bands[r] = Color.Lerp(bottom, top, (r + 0.5f) / rings);
            Lathe(center, heights, widths, bands, sides, rng, jitter, stone: stone);
        }

        /// <summary>A box turned by <paramref name="yaw"/> degrees, without its bottom.</summary>
        public void Box(Vector3 bottomCenter, Vector3 size, float yaw, Color color)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 hx = q * new Vector3(size.x / 2, 0, 0), hz = q * new Vector3(0, 0, size.z / 2);
            Vector3 up = new Vector3(0, size.y, 0);
            Vector3 c = bottomCenter;
            Vector3 p00 = c - hx - hz, p10 = c + hx - hz, p11 = c + hx + hz, p01 = c - hx + hz;
            Quad(p00, p10, p10 + up, p00 + up, color, -hz);
            Quad(p10, p11, p11 + up, p10 + up, color * 0.92f, hx);
            Quad(p11, p01, p01 + up, p11 + up, color, hz);
            Quad(p01, p00, p00 + up, p01 + up, color * 0.92f, -hx);
            Quad(p00 + up, p10 + up, p11 + up, p01 + up, color, Vector3.up);
        }

        /// <summary>A gable roof over a box of the same footprint: ridge along the box's x.</summary>
        public void Roof(Vector3 bottomCenter, Vector3 size, float yaw, Color color)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 hx = q * new Vector3(size.x / 2 + 0.02f, 0, 0), hz = q * new Vector3(0, 0, size.z / 2 + 0.03f);
            Vector3 ridge = new Vector3(0, size.y, 0);
            Vector3 c = bottomCenter;
            Vector3 a = c - hx - hz, b = c + hx - hz, d = c + hx + hz, e = c - hx + hz;
            Quad(a, b, c + hx + ridge, c - hx + ridge, color, -hz + ridge);
            Quad(e, d, c + hx + ridge, c - hx + ridge, color * 0.9f, hz + ridge);
            Triangle(a, e, c - hx + ridge, Palette.Wall, -hx);
            Triangle(b, d, c + hx + ridge, Palette.Wall, hx);
        }

        /// <summary>A prism from <paramref name="a"/> to <paramref name="b"/> with flat end caps.</summary>
        public void Cylinder(Vector3 a, Vector3 b, float radius, int sides, Color color, Color caps)
        {
            Vector3 axis = (b - a).normalized;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                Vector3 r0 = (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * radius;
                Vector3 r1 = (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * radius;
                Quad(a + r0, a + r1, b + r1, b + r0, color * (0.94f + 0.06f * Mathf.Cos(a0 * 3f)), r0 + r1);
                Triangle(a, a + r0, a + r1, caps, -axis);
                Triangle(b, b + r0, b + r1, caps, axis);
            }
        }

        /// <summary>A flat shape seen from both sides.</summary>
        public void TwoSided(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            Triangle(a, b, c, color, n);
            Triangle(a, b, c, color * 0.85f, -n);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh
            {
                name = name,
                indexFormat = positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(1, sways);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

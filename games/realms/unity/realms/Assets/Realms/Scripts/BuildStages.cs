using System.Collections.Generic;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// The capital's Great Build on the map (the economy plan's 3.4, 2026-11-11): one studio model,
    /// the bell tower, cut at two heights for the first two stages. Its first stage ends a course of
    /// stones above the door's arch, its second under the cornice below the belfry, and the third is
    /// the whole tower with the kingdom's flag on its pole. An unfinished stage has a flat top of
    /// the tower's stone. The cut parts are made once from the model's readable mesh (Setup flattens
    /// it as it does the scattered models) and kept.
    /// </summary>
    public static class BuildStages
    {
        /// <summary>The art's name of the model.</summary>
        public const string Model = "bell_tower";

        /// <summary>
        /// Where each stage ends, as a share of the tower's height: a course of stones above the
        /// door's arch, under the cornice, the whole tower. Measured on the model's side view and
        /// sections: both cuts go through the plain round wall, so their sections are rings.
        /// </summary>
        public static readonly float[] Tops = { 0.29f, 0.46f, 1f };

        /// <summary>The flat top of an unfinished stage: the middle of the tower's stone along both cuts, measured on its texture.</summary>
        static readonly Color TopStone = Palette.Hex("#9D8C71");

        static readonly Dictionary<(Mesh, float, float), Mesh> parts = new Dictionary<(Mesh, float, float), Mesh>();
        static readonly Dictionary<Material, Material> tops = new Dictionary<Material, Material>();

        /// <summary>
        /// The part of <paramref name="source"/> between the shares <paramref name="from"/> and
        /// <paramref name="to"/> of its height, standing on its own bottom: its vertices are
        /// lowered by the cut it starts at, so that the part grows from there when it rises. Below
        /// the whole height its top is closed by a flat second submesh (<see cref="TopMaterial"/>).
        /// Null when the mesh cannot be read.
        /// </summary>
        public static Mesh Part(Mesh source, float from, float to)
        {
            if (source == null || !source.isReadable)
                return null;
            if (parts.TryGetValue((source, from, to), out Mesh made) && made != null)
                return made;
            made = Cut(source, from, to);
            parts[(source, from, to)] = made;
            return made;
        }

        /// <summary>The flat top's material beside the tower's own: the same shader and light, the stone's colour, no maps.</summary>
        public static Material TopMaterial(Material walls)
        {
            if (walls == null)
                return null;
            if (tops.TryGetValue(walls, out Material top) && top != null)
                return top;
            top = new Material(walls) { name = walls.name + " (build top)" };
            top.SetTexture("_BaseMap", Texture2D.whiteTexture);
            top.SetFloat("_UseNormalMap", 0f);
            top.SetColor("_BaseColor", TopStone);
            tops[walls] = top;
            return top;
        }

        /// <summary>The model's highest point (the knob on its flagpole), in the prefab's space.</summary>
        public static Vector3 Highest(Mesh source)
        {
            Vector3[] positions = source.vertices;
            Vector3 top = positions.Length > 0 ? positions[0] : Vector3.zero;
            foreach (Vector3 p in positions)
            {
                if (p.y > top.y)
                    top = p;
            }
            return top;
        }

        /// <summary>A corner of a cut polygon: where it is, its normal and its place on the texture.</summary>
        struct Corner
        {
            public Vector3 Position, Normal;
            public Vector2 Uv;

            public static Corner Between(Corner a, Corner b, float level)
            {
                float t = Mathf.Clamp01((level - a.Position.y) / (b.Position.y - a.Position.y));
                return new Corner
                {
                    Position = Vector3.Lerp(a.Position, b.Position, t),
                    Normal = Vector3.Lerp(a.Normal, b.Normal, t).normalized,
                    Uv = Vector2.Lerp(a.Uv, b.Uv, t),
                };
            }
        }

        static Mesh Cut(Mesh source, float from, float to)
        {
            Vector3[] positions = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uvs = source.uv;
            int[] indices = source.triangles;
            if (normals.Length != positions.Length || uvs.Length != positions.Length)
                return null;
            Bounds bounds = source.bounds;
            float low = bounds.min.y + from * bounds.size.y, high = bounds.min.y + to * bounds.size.y;
            bool closed = to < 1f;
            var outPositions = new List<Vector3>();
            var outNormals = new List<Vector3>();
            var outUvs = new List<Vector2>();
            var walls = new List<int>();
            var rim = new List<(Vector3 A, Vector3 B)>();
            var polygon = new List<Corner>(5);
            var clipped = new List<Corner>(5);
            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                polygon.Clear();
                for (int k = 0; k < 3; k++)
                {
                    int i = indices[t + k];
                    polygon.Add(new Corner { Position = positions[i], Normal = normals[i], Uv = uvs[i] });
                }
                float lowest = Mathf.Min(polygon[0].Position.y, Mathf.Min(polygon[1].Position.y, polygon[2].Position.y));
                float highest = Mathf.Max(polygon[0].Position.y, Mathf.Max(polygon[1].Position.y, polygon[2].Position.y));
                if (lowest >= high || highest <= low)
                    continue;
                if (highest > high)
                {
                    Clip(polygon, clipped, high, true);
                    // The two corners on the cut are an edge of the section, which the top closes.
                    if (closed)
                    {
                        Vector3? first = null;
                        foreach (Corner c in clipped)
                        {
                            if (Mathf.Abs(c.Position.y - high) > 1e-6f)
                                continue;
                            if (first == null)
                                first = c.Position;
                            else if ((c.Position - first.Value).sqrMagnitude > 1e-12f)
                            {
                                rim.Add((first.Value, c.Position));
                                break;
                            }
                        }
                    }
                    (polygon, clipped) = (clipped, polygon);
                }
                if (lowest < low)
                {
                    Clip(polygon, clipped, low, false);
                    (polygon, clipped) = (clipped, polygon);
                }
                if (polygon.Count < 3)
                    continue;
                int start = outPositions.Count;
                foreach (Corner c in polygon)
                {
                    outPositions.Add(c.Position - new Vector3(0f, low, 0f));
                    outNormals.Add(c.Normal);
                    outUvs.Add(c.Uv);
                }
                for (int k = 1; k + 1 < polygon.Count; k++)
                {
                    walls.Add(start);
                    walls.Add(start + k);
                    walls.Add(start + k + 1);
                }
            }
            if (walls.Count == 0)
                return null;

            // The top: a fan from the section's middle over every edge of it, each triangle facing
            // up. The tower's wall is a thin shell, so the section is two close rings and their fans
            // lie on one another, the same colour and light.
            var top = new List<int>();
            if (closed && rim.Count > 0)
            {
                Vector3 middle = Vector3.zero;
                foreach ((Vector3 a, Vector3 b) in rim)
                    middle += a + b;
                middle /= 2 * rim.Count;
                int centre = outPositions.Count;
                outPositions.Add(middle - new Vector3(0f, low, 0f));
                outNormals.Add(Vector3.up);
                outUvs.Add(new Vector2(0.5f, 0.5f));
                foreach ((Vector3 a, Vector3 b) in rim)
                {
                    // Unity's front faces turn clockwise seen from their side: up, for this cross product's y over 0.
                    bool up = Vector3.Cross(a - middle, b - middle).y > 0f;
                    int ia = outPositions.Count;
                    outPositions.Add(a - new Vector3(0f, low, 0f));
                    outPositions.Add(b - new Vector3(0f, low, 0f));
                    outNormals.Add(Vector3.up);
                    outNormals.Add(Vector3.up);
                    outUvs.Add(new Vector2(0.5f, 0.5f));
                    outUvs.Add(new Vector2(0.5f, 0.5f));
                    top.Add(centre);
                    top.Add(up ? ia : ia + 1);
                    top.Add(up ? ia + 1 : ia);
                }
            }

            var mesh = new Mesh
            {
                name = $"{source.name} {from:0.00}-{to:0.00}",
                indexFormat = outPositions.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(outPositions);
            mesh.SetNormals(outNormals);
            mesh.SetUVs(0, outUvs);
            mesh.subMeshCount = top.Count > 0 ? 2 : 1;
            mesh.SetTriangles(walls, 0);
            if (top.Count > 0)
                mesh.SetTriangles(top, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Clips a convex polygon to the side of the level <paramref name="level"/> below it
        /// (<paramref name="below"/>) or above it, keeping its corners' order (Sutherland–Hodgman).
        /// </summary>
        static void Clip(List<Corner> polygon, List<Corner> result, float level, bool below)
        {
            result.Clear();
            for (int k = 0; k < polygon.Count; k++)
            {
                Corner a = polygon[k], b = polygon[(k + 1) % polygon.Count];
                bool aIn = below ? a.Position.y <= level : a.Position.y >= level;
                bool bIn = below ? b.Position.y <= level : b.Position.y >= level;
                if (aIn)
                    result.Add(a);
                if (aIn != bIn)
                {
                    Corner c = Corner.Between(a, b, level);
                    c.Position.y = level;
                    result.Add(c);
                }
            }
        }
    }
}

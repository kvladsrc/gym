using System.Collections.Generic;
using UnityEngine;

namespace FinishTheGame
{
    /// <summary>Boxes for the ribbon: cut along z into pieces no longer than
    /// a segment, since the ribbon bends the world only along z (across it,
    /// and up, it only turns and scales it: straight lines stay straight).
    /// The colour goes into the vertices: one material for all.</summary>
    public static class MeshKit
    {
        /// <summary>Flat ground round its origin, a side long, cut into cells
        /// (the land's relief, Land.Height, is laid on them).</summary>
        public static Mesh Grid(float side, int cells, Color colour, float tile = 0f)
        {
            // UVs: the ground's x, z over the tile's size (none: 0–1 over the grid).
            int across = cells + 1;
            var vertices = new Vector3[across * across];
            var normals = new Vector3[vertices.Length];
            var colours = new Color[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            for (int j = 0; j < across; j++)
                for (int i = 0; i < across; i++)
                {
                    int k = j * across + i;
                    vertices[k] = new Vector3(side * ((float)i / cells - 0.5f), 0, side * ((float)j / cells - 0.5f));
                    vertices[k].y = Land.Height(vertices[k].x, vertices[k].z);
                    normals[k] = Vector3.up;
                    colours[k] = colour;
                    uvs[k] = tile > 0 ? new Vector2(vertices[k].x, vertices[k].z) / tile : new Vector2((float)i / cells, (float)j / cells);
                }
            var triangles = new int[cells * cells * 6];
            for (int j = 0, t = 0; j < cells; j++)
                for (int i = 0; i < cells; i++, t += 6)
                {
                    int p0 = j * across + i, p1 = p0 + 1, p3 = p0 + across, p2 = p3 + 1;
                    triangles[t] = p0; triangles[t + 1] = p3; triangles[t + 2] = p2;
                    triangles[t + 3] = p0; triangles[t + 4] = p2; triangles[t + 5] = p1;
                }
            var mesh = new Mesh { name = "Grid", indexFormat = vertices.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colours;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A copy of a model's mesh, its vertices white (the painted
        /// texture gives its colour; the shaders multiply by the vertex colour).</summary>
        public static Mesh White(Mesh mesh)
        {
            var copy = Object.Instantiate(mesh);
            var colours = new Color[copy.vertexCount];
            for (int i = 0; i < colours.Length; i++) colours[i] = Color.white;
            copy.colors = colours;
            return copy;
        }

        /// <summary>A box of this size standing on its origin (y from 0 up).</summary>
        public static Mesh Box(Vector3 size, Color colour, float segment = 1f)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 h = size * 0.5f;
            var x = new Vector3(size.x, 0, 0);
            var y = new Vector3(0, size.y, 0);
            var z = new Vector3(0, 0, size.z);
            int n = Mathf.Max(1, Mathf.CeilToInt(size.z / segment));
            // Each face from a corner along du and dv, Cross(dv, du) outward.
            Face(new Vector3(-h.x, size.y, -h.z), x, z, 1, n);
            Face(new Vector3(-h.x, 0, -h.z), z, x, n, 1);
            Face(new Vector3(h.x, 0, -h.z), z, y, n, 1);
            Face(new Vector3(-h.x, 0, -h.z), y, z, 1, n);
            Face(new Vector3(-h.x, 0, h.z), y, x, 1, 1);
            Face(new Vector3(-h.x, 0, -h.z), x, y, 1, 1);

            var mesh = new Mesh { name = "Box" };
            mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            var colours = new Color[vertices.Count];
            for (int i = 0; i < colours.Length; i++) colours[i] = colour;
            mesh.colors = colours;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;

            void Face(Vector3 corner, Vector3 du, Vector3 dv, int nu, int nv)
            {
                Vector3 normal = Vector3.Cross(dv, du).normalized;
                int first = vertices.Count;
                for (int j = 0; j <= nv; j++)
                    for (int i = 0; i <= nu; i++)
                    {
                        vertices.Add(corner + du * ((float)i / nu) + dv * ((float)j / nv));
                        normals.Add(normal);
                    }
                for (int j = 0; j < nv; j++)
                    for (int i = 0; i < nu; i++)
                    {
                        int p0 = first + j * (nu + 1) + i, p1 = p0 + 1, p3 = p0 + nu + 1, p2 = p3 + 1;
                        triangles.AddRange(new[] { p0, p3, p2, p0, p2, p1 });
                    }
            }
        }
    }
}

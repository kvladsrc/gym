using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realms
{
    /// <summary>
    /// Smoke from the chimneys of a cell: a few puffs per chimney in one mesh, every corner of a
    /// puff at the chimney's top. The Realms/Smoke shader moves each puff up and along the wind by
    /// the time, grows it and fades it out, then starts it again at the chimney, so nothing is
    /// updated per frame and a plume is whole from the first frame.
    /// </summary>
    public sealed class SmokePlumes
    {
        /// <summary>The puffs in the air at once over one chimney.</summary>
        public const int Puffs = 10;

        /// <summary>How high and how far downwind a plume reaches, for the mesh's bounds.</summary>
        const float Reach = 0.7f;

        /// <summary>The material for every cell's smoke (MapView sets it); without it no smoke.</summary>
        public static Material Material;

        readonly List<Vector3> positions = new List<Vector3>();
        readonly List<Vector2> corners = new List<Vector2>();
        readonly List<Vector4> puffs = new List<Vector4>();
        readonly List<int> indices = new List<int>();

        public int VertexCount => positions.Count;

        static readonly Vector2[] Corners = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };

        /// <summary>
        /// A chimney smoking at <paramref name="top"/>; <paramref name="size"/> scales its puffs and
        /// <paramref name="seed"/> (0..1) keeps it out of step with the others.
        /// </summary>
        public void Add(Vector3 top, float size, float seed)
        {
            for (int k = 0; k < Puffs; k++)
            {
                int first = positions.Count;
                var puff = new Vector4((float)k / Puffs, seed, size, 0f);
                foreach (Vector2 corner in Corners)
                {
                    positions.Add(top);
                    corners.Add(corner);
                    puffs.Add(puff);
                }
                indices.Add(first);
                indices.Add(first + 2);
                indices.Add(first + 1);
                indices.Add(first);
                indices.Add(first + 3);
                indices.Add(first + 2);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt16 };
            mesh.SetVertices(positions);
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, puffs);
            mesh.SetTriangles(indices, 0);
            // The shader moves the puffs away from the chimneys: the bounds take in the plumes.
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(Reach * 2f, Reach * 2f, Reach * 2f));
            mesh.bounds = bounds;
            return mesh;
        }
    }
}

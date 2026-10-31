using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Procedural.World
{
    /// <summary>
    /// Draws many copies of a few meshes with GPU instancing. Instances are split into
    /// fixed world-space cells once, so whole cells behind the camera or beyond the draw
    /// distance are skipped without touching their matrices. No GameObjects are created.
    /// </summary>
    [ExecuteAlways]
    public class InstancedProps : MonoBehaviour
    {
        [Serializable]
        public class Batch
        {
            public Mesh mesh;
            /// <summary>One material per submesh.</summary>
            public Material[] materials;
            /// <summary>World transforms of every instance.</summary>
            public Matrix4x4[] matrices;
            /// <summary>World bounds of all instances, used for coarse culling.</summary>
            public Bounds bounds;

            /// <summary>Built on first draw; Add and domain reloads leave it empty.</summary>
            [NonSerialized] internal Chunk[] chunks;
        }

        /// <summary>RenderMeshInstanced accepts at most 1023 instances per call.</summary>
        const int MaxInstancesPerDraw = 1023;
        const float CellSize = 64f;

        public List<Batch> batches = new List<Batch>();
        [Tooltip("Chunks farther than this from the main camera are skipped.")]
        public float drawDistance = 250f;

        readonly Plane[] frustum = new Plane[6];

        internal sealed class Chunk
        {
            public Bounds bounds;
            public Matrix4x4[] matrices;
        }

        void LateUpdate()
        {
            if (batches == null || batches.Count == 0) return;

            Camera camera = Camera.main;
            // The Scene view camera is not Camera.main, so only cull while playing;
            // in edit mode every chunk is drawn to keep the whole level visible.
            bool cull = Application.isPlaying && camera != null;
            if (cull) GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            Vector3 eye = cull ? camera.transform.position : Vector3.zero;
            float maxDistance = Mathf.Max(0f, drawDistance);
            float maxDistanceSquared = maxDistance * maxDistance;

            for (int b = 0; b < batches.Count; b++)
            {
                var batch = batches[b];
                if (batch == null || batch.mesh == null || batch.materials == null || batch.materials.Length == 0)
                    continue;
                if (batch.matrices == null || batch.matrices.Length == 0) continue;
                if (batch.chunks == null) BuildChunks(batch);

                if (cull)
                {
                    if (batch.bounds.SqrDistance(eye) > maxDistanceSquared) continue;
                    if (!GeometryUtility.TestPlanesAABB(frustum, batch.bounds)) continue;
                }

                var mesh = batch.mesh;
                int submeshes = Mathf.Min(mesh.subMeshCount, batch.materials.Length);
                for (int submesh = 0; submesh < submeshes; submesh++)
                {
                    var material = batch.materials[submesh];
                    if (material == null) continue;

                    for (int c = 0; c < batch.chunks.Length; c++)
                    {
                        var chunk = batch.chunks[c];
                        if (chunk.matrices == null || chunk.matrices.Length == 0) continue;
                        if (cull)
                        {
                            if (chunk.bounds.SqrDistance(eye) > maxDistanceSquared) continue;
                            if (!GeometryUtility.TestPlanesAABB(frustum, chunk.bounds)) continue;
                        }

                        for (int start = 0; start < chunk.matrices.Length; start += MaxInstancesPerDraw)
                        {
                            int count = Mathf.Min(MaxInstancesPerDraw, chunk.matrices.Length - start);
                            var parameters = new RenderParams(material)
                            {
                                worldBounds = chunk.bounds,
                                shadowCastingMode = ShadowCastingMode.On,
                                receiveShadows = true
                            };
                            Graphics.RenderMeshInstanced(parameters, mesh, submesh, chunk.matrices, count, start);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds one mesh with all of its instances. Bounds come from the mesh bounds
        /// transformed by every matrix, so rotated and scaled instances are covered.
        /// </summary>
        public void Add(Mesh mesh, Material[] materials, List<Matrix4x4> matrices)
        {
            if (mesh == null || matrices == null || matrices.Count == 0) return;

            var transforms = matrices.ToArray();
            var bounds = TransformBounds(mesh.bounds, transforms[0]);
            for (int i = 1; i < transforms.Length; i++)
                bounds.Encapsulate(TransformBounds(mesh.bounds, transforms[i]));

            if (batches == null) batches = new List<Batch>();
            batches.Add(new Batch
            {
                mesh = mesh,
                materials = materials,
                matrices = transforms,
                bounds = bounds
            });
        }

        void BuildChunks(Batch batch)
        {
            var cells = new Dictionary<long, List<Matrix4x4>>();
            var order = new List<long>(batch.matrices.Length);
            foreach (var matrix in batch.matrices)
            {
                Vector3 position = matrix.MultiplyPoint3x4(Vector3.zero);
                int x = Mathf.FloorToInt(position.x / CellSize);
                int z = Mathf.FloorToInt(position.z / CellSize);
                long key = ((long)x << 32) | (uint)z;
                if (!cells.TryGetValue(key, out var list))
                {
                    list = new List<Matrix4x4>();
                    cells.Add(key, list);
                    order.Add(key);
                }
                list.Add(matrix);
            }

            var chunks = new Chunk[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                var list = cells[order[i]];
                var chunk = new Chunk { matrices = list.ToArray() };
                var bounds = TransformBounds(batch.mesh.bounds, chunk.matrices[0]);
                for (int j = 1; j < chunk.matrices.Length; j++)
                    bounds.Encapsulate(TransformBounds(batch.mesh.bounds, chunk.matrices[j]));
                chunk.bounds = bounds;
                chunks[i] = chunk;
            }
            batch.chunks = chunks;
        }

        static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(local.center);
            Vector3 extents = local.extents;
            Vector3 x = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 y = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 z = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            var worldExtents = new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(center, worldExtents * 2f);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realms
{
    /// <summary>
    /// Copies of the studio models the map scatters by the hundred (trees, stones and the towns'
    /// walls, see <see cref="PropBuilder.ScatterModels"/>), merged into one mesh per model: every
    /// copy is placed, turned and scaled on its own and carries a tint in its vertex colours,
    /// which the prop shader multiplies into the model's texture, how far it sways in the wind
    /// (trees; see <see cref="MeshKit.Sway"/>) and where it stands, so that a plant goes whole
    /// when a screen cuts the near wood away. Setup gives these models one readable mesh and one
    /// material each.
    /// </summary>
    public sealed class ModelBatch
    {
        /// <summary>A scatter model's mesh data, read once.</summary>
        public sealed class Source
        {
            public readonly Vector3[] Positions;
            public readonly Vector3[] Normals;
            public readonly Vector2[] Uvs;
            public readonly int[] Indices;
            public readonly Material Material;

            /// <summary>The model's size, its pivot at the bottom centre.</summary>
            public readonly Bounds Bounds;

            Source(Mesh mesh, Material material)
            {
                Positions = mesh.vertices;
                Normals = mesh.normals;
                Uvs = mesh.uv;
                Indices = mesh.triangles;
                Bounds = mesh.bounds;
                // The same textures, but the vertex colours as a tint.
                Material = new Material(material) { name = material.name + " (tinted)" };
                Material.SetFloat("_UseVertexColor", 1f);
                Material.SetFloat("_Sway", 1f);
            }

            /// <summary>The model of a scatter prefab, or null when it has no readable single mesh.</summary>
            public static Source Of(GameObject prefab)
            {
                if (prefab == null || !prefab.TryGetComponent(out MeshFilter filter) || !prefab.TryGetComponent(out MeshRenderer renderer))
                    return null;
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable || mesh.subMeshCount != 1 || renderer.sharedMaterial == null)
                    return null;
                var source = new Source(mesh, renderer.sharedMaterial);
                return source.Normals.Length == source.Positions.Length && source.Uvs.Length == source.Positions.Length ? source : null;
            }
        }

        readonly Dictionary<Source, Part> parts = new Dictionary<Source, Part>();

        sealed class Part
        {
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<Vector2> Sways = new List<Vector2>();
            public readonly List<Vector2> Roots = new List<Vector2>();
            public readonly List<int> Indices = new List<int>();
        }

        public bool IsEmpty => parts.Count == 0;

        /// <summary>
        /// A copy of <paramref name="model"/> standing at <paramref name="at"/>. With
        /// <paramref name="sway"/> it bends in the wind like a tree, its top moving that share of
        /// its height, its trunk staying put; such a plant also goes, whole, when it stands between
        /// a town or hero screen's camera and its subject (the prop shader's _Cutaway).
        /// </summary>
        public void Add(Source model, Vector3 at, float yaw, Vector3 scale, Color tint, float sway = 0f)
        {
            if (sway > 0f)
                model.Material.SetFloat(CutawayId, 1f);
            Append(model, Matrix4x4.TRS(at, Quaternion.Euler(0f, yaw, 0f), scale), tint,
                sway * Mathf.Max(model.Bounds.size.y, 1e-5f) * scale.y);
        }

        static readonly int CutawayId = Shader.PropertyToID("_Cutaway");

        /// <summary>
        /// A still copy of <paramref name="model"/> placed by <paramref name="place"/>, which may
        /// also shear it: a piece of wall following a slope.
        /// </summary>
        public void Add(Source model, Matrix4x4 place, Color tint) => Append(model, place, tint, 0f);

        void Append(Source model, Matrix4x4 place, Color tint, float reach)
        {
            if (!parts.TryGetValue(model, out Part part))
                parts[model] = part = new Part();
            Matrix4x4 turn = place.inverse.transpose;
            int first = part.Positions.Count;
            Color linear = tint.linear;
            linear.a = 1f;
            float bottom = model.Bounds.min.y, height = Mathf.Max(model.Bounds.size.y, 1e-5f);
            Vector3 at = place.GetColumn(3);
            float phase = MeshKit.PhaseAt(at);
            var root = new Vector2(at.x, at.z);
            for (int i = 0; i < model.Positions.Length; i++)
            {
                float up = Mathf.Clamp01((model.Positions[i].y - bottom) / height);
                part.Sways.Add(new Vector2(reach * up * up, phase));
                part.Roots.Add(root);
                part.Positions.Add(place.MultiplyPoint3x4(model.Positions[i]));
                part.Normals.Add(turn.MultiplyVector(model.Normals[i]).normalized);
                part.Uvs.Add(model.Uvs[i]);
                part.Colors.Add(linear);
            }
            foreach (int index in model.Indices)
                part.Indices.Add(first + index);
        }

        /// <summary>One child object of <paramref name="parent"/> per model, shadows on.</summary>
        public void Build(Transform parent, string name)
        {
            foreach (KeyValuePair<Source, Part> pair in parts)
            {
                Part part = pair.Value;
                var mesh = new Mesh
                {
                    name = $"Props {name} {pair.Key.Material.name}",
                    indexFormat = part.Positions.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                };
                mesh.SetVertices(part.Positions);
                mesh.SetNormals(part.Normals);
                mesh.SetUVs(0, part.Uvs);
                mesh.SetColors(part.Colors);
                mesh.SetUVs(1, part.Sways);
                mesh.SetUVs(2, part.Roots);
                mesh.SetTriangles(part.Indices, 0);
                mesh.RecalculateBounds();
                var go = new GameObject(mesh.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Key.Material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            parts.Clear();
        }
    }
}

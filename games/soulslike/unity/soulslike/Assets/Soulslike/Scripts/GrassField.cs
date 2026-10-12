using System.Collections.Generic;
using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// Grass around the camera: clumps of curved blades scattered on the ground
    /// in cells of <see cref="Cell"/> m as the camera comes near (a ray down
    /// for each: only where the ground itself is uppermost and not steep),
    /// drawn instanced, one call a cell. Where it grows and its colour are the
    /// shader's (Soulslike/Grass: the level's masks and baked colour), so the
    /// trails, rocks and water stay bare. The same scatter every time.
    /// </summary>
    public class GrassField : MonoBehaviour
    {
        public Collider ground;
        public Material material;
        /// <summary>Drawn this far, m (the shader shrinks it away over the last 8).</summary>
        public float radius = 40f;
        /// <summary>Clumps a square metre.</summary>
        public float density = 2.5f;
        public int seed = 11;

        const float Cell = 8f;
        const int BuildPerFrame = 8;

        readonly Dictionary<Vector2Int, Matrix4x4[]> cells = new();
        readonly RaycastHit[] hits = new RaycastHit[8];
        readonly List<Vector2Int> wanted = new();
        Mesh clump;
        Transform view;

        void Start()
        {
            clump = Clump(new System.Random(seed));
            material.SetFloat("_Radius", radius);
        }

        void Update()
        {
            if (view == null)
            {
                if (Camera.main == null) return;
                view = Camera.main.transform;
            }
            var at = view.position;
            var centre = new Vector2Int(Mathf.FloorToInt(at.x / Cell), Mathf.FloorToInt(at.z / Cell));
            int reach = Mathf.CeilToInt(radius / Cell) + 1;
            float near = radius + Cell * 0.75f;

            wanted.Clear();
            for (int dx = -reach; dx <= reach; dx++)
                for (int dz = -reach; dz <= reach; dz++)
                {
                    var key = new Vector2Int(centre.x + dx, centre.y + dz);
                    var middle = new Vector2((key.x + 0.5f) * Cell, (key.y + 0.5f) * Cell);
                    if (Vector2.Distance(middle, new Vector2(at.x, at.z)) <= near) wanted.Add(key);
                }
            // The nearest first: a new cell appears in front of the hero before the far ones.
            wanted.Sort((a, b) => (a - centre).sqrMagnitude.CompareTo((b - centre).sqrMagnitude));

            int built = 0;
            foreach (var key in wanted)
            {
                if (!cells.TryGetValue(key, out var clumps))
                {
                    if (built++ >= BuildPerFrame) continue;
                    cells[key] = clumps = Build(key);
                }
                if (clumps.Length > 0)
                    Graphics.DrawMeshInstanced(clump, 0, material, clumps, clumps.Length, null,
                        UnityEngine.Rendering.ShadowCastingMode.Off, true, gameObject.layer);
            }

            // Cells left far behind are let go (and rebuilt the same if come back to).
            if (cells.Count > wanted.Count * 2)
            {
                var keep = new HashSet<Vector2Int>(wanted);
                var drop = new List<Vector2Int>();
                foreach (var key in cells.Keys) if (!keep.Contains(key)) drop.Add(key);
                foreach (var key in drop) cells.Remove(key);
            }
        }

        Matrix4x4[] Build(Vector2Int key)
        {
            var random = new System.Random(unchecked(seed * 73856093 ^ key.x * 19349663 ^ key.y * 83492791));
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            int count = Mathf.RoundToInt(Cell * Cell * density);
            var clumps = new List<Matrix4x4>(count);
            for (int i = 0; i < count; i++)
            {
                var x = (key.x + R(0f, 1f)) * Cell;
                var z = (key.y + R(0f, 1f)) * Cell;
                float yaw = R(0f, 360f), size = R(0.7f, 1.3f);
                if (!Uppermost(new Vector3(x, 400f, z), out var hit)) continue;
                if (hit.collider != ground || hit.normal.y < 0.75f) continue;
                var rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, hit.normal, 0.5f))
                    * Quaternion.Euler(0f, yaw, 0f);
                clumps.Add(Matrix4x4.TRS(hit.point, rotation, Vector3.one * size));
            }
            return clumps.ToArray();
        }

        /// <summary>The highest solid surface under a point, characters
        /// (their controllers) not counted.</summary>
        bool Uppermost(Vector3 from, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(from, Vector3.down, hits, 800f, ~0, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].collider is CharacterController) continue;
                if (!found || hits[i].point.y > best.point.y) { best = hits[i]; found = true; }
            }
            return found;
        }

        /// <summary>A clump of seven curved, tapering blades, 25–50 cm. UV x:
        /// the height along the blade (0 root, 1 tip; the shader's sway and
        /// shade), y: the blade's own random shade.</summary>
        static Mesh Clump(System.Random random)
        {
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            const int blades = 7, rows = 5;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int b = 0; b < blades; b++)
            {
                float angle = R(0f, Mathf.PI * 2f), at = R(0f, 0.16f);
                var root = new Vector3(Mathf.Cos(angle) * at, 0f, Mathf.Sin(angle) * at);
                float facing = R(0f, Mathf.PI * 2f);
                var across = new Vector3(Mathf.Cos(facing), 0f, Mathf.Sin(facing));
                var lean = Vector3.Cross(across, Vector3.up) * R(0.08f, 0.22f);
                float height = R(0.25f, 0.5f), width = R(0.02f, 0.035f), shade = R(0f, 1f);
                int first = vertices.Count;
                for (int r = 0; r < rows; r++)
                {
                    float t = r / (rows - 1f);
                    var middle = root + Vector3.up * (height * t) + lean * (t * t) * height;
                    float w = width * (1f - t * 0.92f);
                    vertices.Add(middle - across * w);
                    vertices.Add(middle + across * w);
                    uvs.Add(new Vector2(t, shade));
                    uvs.Add(new Vector2(t, shade));
                }
                for (int r = 0; r < rows - 1; r++)
                {
                    int a = first + r * 2;
                    triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
            }
            var mesh = new Mesh { name = "Grass clump" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            // Lit as the ground is (straight up): a field, not shiny strips.
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            // The sway reaches out of the clump's own bounds.
            mesh.bounds = new Bounds(new Vector3(0f, 0.3f, 0f), new Vector3(1.2f, 0.8f, 1.2f));
            return mesh;
        }
    }
}

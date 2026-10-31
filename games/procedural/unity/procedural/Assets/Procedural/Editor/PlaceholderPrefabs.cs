using System.Collections.Generic;
using Procedural.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Procedural.Editor
{
    /// <summary>
    /// Builds the placeholder plant materials, meshes, prefabs and species catalog,
    /// updating existing assets in place so their GUIDs and references survive.
    /// </summary>
    static class PlaceholderPrefabs
    {
        const string Root = ProceduralPaths.Placeholders + "/Plants";
        const string CatalogPath = Root + "/SpeciesCatalog.asset";
        const string Lit = "Universal Render Pipeline/Lit";

        [MenuItem("Procedural/Rebuild Plant Placeholders", priority = 30)]
        static void Rebuild() => Ensure();

        /// <summary>Creates or refreshes every placeholder and returns the filled catalog.</summary>
        public static SpeciesCatalog Ensure()
        {
            Folders.Ensure(Root);

            var bark = Material("Bark", 0.36f, 0.27f, 0.18f);
            var pineNeedles = Material("PineNeedles", 0.16f, 0.30f, 0.16f);
            var leaves = Material("Leaves", 0.26f, 0.42f, 0.18f);
            var alderLeaves = Material("AlderLeaves", 0.30f, 0.45f, 0.22f);
            var shrub = Material("Shrub", 0.28f, 0.40f, 0.20f);
            var reed = Material("Reed", 0.55f, 0.58f, 0.32f);
            var grass = Material("Grass", 0.36f, 0.50f, 0.22f);
            var rock = Material("Rock", 0.46f, 0.45f, 0.42f);
            var stone = Material("Stone", 0.55f, 0.54f, 0.50f);
            var deadWood = Material("DeadWood", 0.42f, 0.36f, 0.30f);

            var catalog = AssetDatabase.LoadAssetAtPath<SpeciesCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SpeciesCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.species = new List<SpeciesVisual>
            {
                Visual("pine", PlantBuild.TerrainTree, Pine(bark, pineNeedles)),
                Visual("broadleaf", PlantBuild.TerrainTree, Broadleaf(bark, leaves)),
                Visual("alder", PlantBuild.TerrainTree, Alder(bark, alderLeaves)),
                Visual("shrub", PlantBuild.TerrainTree, Shrub(shrub)),
                Visual("snag", PlantBuild.TerrainTree, Snag(deadWood)),
                Visual("log", PlantBuild.SceneObject, Log(deadWood)),
                Visual("rock", PlantBuild.SceneObject, Rock(rock)),
                Visual("slab", PlantBuild.SceneObject, Slab(rock)),
                Visual("stone", PlantBuild.Instanced, Stone(stone)),
                Visual("mound", PlantBuild.Instanced, Mound(grass)),
                Visual("grass", PlantBuild.TerrainDetail, Grass(grass)),
                Visual("reed", PlantBuild.TerrainDetail, Reed(reed))
            };
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        static SpeciesVisual Visual(string id, PlantBuild build, GameObject prefab) =>
            new SpeciesVisual { id = id, build = build, prefab = prefab };

        // Trunks go on this far below the pivot (the ground line): deep roots for trees sunk on slopes.
        const float Roots = 1f;

        static GameObject Pine(Material bark, Material needles)
        {
            var parts = new MeshBuilder();
            parts.Add(Cylinder(0.25f, 4f + Roots), 0, new Vector3(0f, -Roots, 0f), Quaternion.identity);
            parts.Add(Cone(2.2f, 10f), 1, new Vector3(0f, 3f, 0f), Quaternion.identity);
            return Prefab("pine", MeshAsset("pine", parts), new[] { bark, needles }, root => Capsule(root, 0.25f, 4f));
        }

        static GameObject Broadleaf(Material bark, Material leaves)
        {
            var parts = new MeshBuilder();
            parts.Add(Cylinder(0.3f, 5f + Roots), 0, new Vector3(0f, -Roots, 0f), Quaternion.identity);
            parts.Add(Ellipsoid(3.5f, 3f, 3.5f), 1, new Vector3(0f, 8f, 0f), Quaternion.identity);
            return Prefab("broadleaf", MeshAsset("broadleaf", parts), new[] { bark, leaves }, root => Capsule(root, 0.3f, 5f));
        }

        static GameObject Alder(Material bark, Material leaves)
        {
            var parts = new MeshBuilder();
            parts.Add(Cylinder(0.22f, 3f + Roots), 0, new Vector3(0f, -Roots, 0f), Quaternion.identity);
            parts.Add(Ellipsoid(2.5f, 1.8f, 2.5f), 1, new Vector3(0f, 5f, 0f), Quaternion.identity);
            return Prefab("alder", MeshAsset("alder", parts), new[] { bark, leaves }, root => Capsule(root, 0.22f, 3f));
        }

        static GameObject Shrub(Material shrub)
        {
            var parts = new MeshBuilder();
            parts.Add(Ellipsoid(1f, 0.8f, 1f), 0, new Vector3(0f, 0.6f, 0f), Quaternion.identity);
            return Prefab("shrub", MeshAsset("shrub", parts), new[] { shrub });
        }

        static GameObject Snag(Material deadWood)
        {
            var parts = new MeshBuilder();
            parts.Add(Cylinder(0.25f, 8f + Roots), 0, new Vector3(0f, -Roots, 0f), Quaternion.identity);
            parts.Add(Cylinder(0.07f, 1.3f), 0, new Vector3(0f, 6.4f, 0f), Quaternion.Euler(0f, 0f, 72f));
            parts.Add(Cylinder(0.06f, 1.1f), 0, new Vector3(0f, 7.2f, 0f), Quaternion.Euler(0f, 120f, -62f));
            return Prefab("snag", MeshAsset("snag", parts), new[] { deadWood }, root => Capsule(root, 0.25f, 8f));
        }

        static GameObject Log(Material deadWood)
        {
            var parts = new MeshBuilder();
            parts.Add(Cylinder(0.3f, 6f), 0, new Vector3(-3f, 0.3f, 0f), Quaternion.Euler(0f, 0f, -90f));
            return Prefab("log", MeshAsset("log", parts), new[] { deadWood }, root =>
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 0; // X
                capsule.radius = 0.3f;
                capsule.height = 6f;
                capsule.center = new Vector3(0f, 0.3f, 0f);
            });
        }

        static GameObject Rock(Material rock)
        {
            var body = Icosphere(1f, 2);
            DistortRadial(body, 0.25f, 101);
            Scale(body, new Vector3(1.2f, 0.8f, 1f));

            var parts = new MeshBuilder();
            parts.Add(body, 0);
            var mesh = MeshAsset("rock", parts);
            return Prefab("rock", mesh, new[] { rock }, root => Convex(root, mesh));
        }

        static GameObject Slab(Material rock)
        {
            var size = new Vector3(2.4f, 0.5f, 1.8f);
            var body = BeveledBox(size, 0.08f);
            // Duplicate vertices on the face, edge and corner patches share positions;
            // hashing the position keeps them together, so the mesh stays watertight.
            DistortCorners(body, size * 0.15f, 211);

            var parts = new MeshBuilder();
            parts.Add(body, 0);
            var mesh = MeshAsset("slab", parts);
            return Prefab("slab", mesh, new[] { rock }, root => Convex(root, mesh));
        }

        static GameObject Stone(Material stone)
        {
            var body = Icosphere(0.3f, 1);
            DistortRadial(body, 0.30f, 307);

            var parts = new MeshBuilder();
            parts.Add(body, 0);
            var mesh = MeshAsset("stone", parts);
            return Prefab("stone", mesh, new[] { stone });
        }

        static GameObject Mound(Material grass)
        {
            var body = Ellipsoid(1.2f, 0.5f, 1.2f);
            DistortRadial(body, 0.08f, 401);

            var parts = new MeshBuilder();
            parts.Add(body, 0);
            var mesh = MeshAsset("mound", parts);
            return Prefab("mound", mesh, new[] { grass });
        }

        static GameObject Grass(Material grass)
        {
            var parts = new MeshBuilder();
            parts.Add(CrossedQuads(0.5f, 0.6f, 3), 0);
            return Prefab("grass", MeshAsset("grass", parts), new[] { grass });
        }

        static GameObject Reed(Material reed)
        {
            var parts = new MeshBuilder();
            var blade = new Vector3(0.04f, 1.6f, 0.04f);
            foreach (var position in ReedPositions)
                parts.Add(Box(blade), 0, position + new Vector3(0f, 0.8f, 0f), Quaternion.identity);
            return Prefab("reed", MeshAsset("reed", parts), new[] { reed });
        }

        static readonly Vector3[] ReedPositions =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0.12f, 0f, 0.08f),
            new Vector3(-0.1f, 0f, 0.14f),
            new Vector3(0.16f, 0f, -0.12f),
            new Vector3(-0.14f, 0f, -0.1f)
        };

        /// <summary>An icosahedron subdivided into a unit sphere, with shared vertices.</summary>
        static Part Icosphere(float radius, int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var vertices = new List<Vector3>
            {
                new Vector3(-1f, t, 0f).normalized,
                new Vector3(1f, t, 0f).normalized,
                new Vector3(-1f, -t, 0f).normalized,
                new Vector3(1f, -t, 0f).normalized,
                new Vector3(0f, -1f, t).normalized,
                new Vector3(0f, 1f, t).normalized,
                new Vector3(0f, -1f, -t).normalized,
                new Vector3(0f, 1f, -t).normalized,
                new Vector3(t, 0f, -1f).normalized,
                new Vector3(t, 0f, 1f).normalized,
                new Vector3(-t, 0f, -1f).normalized,
                new Vector3(-t, 0f, 1f).normalized
            };
            var triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            for (int level = 0; level < subdivisions; level++)
            {
                var midpoints = new Dictionary<long, int>();
                var next = new List<int>(triangles.Count * 4);
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Midpoint(vertices, midpoints, a, b);
                    int bc = Midpoint(vertices, midpoints, b, c);
                    int ca = Midpoint(vertices, midpoints, c, a);
                    next.Add(a); next.Add(ab); next.Add(ca);
                    next.Add(b); next.Add(bc); next.Add(ab);
                    next.Add(c); next.Add(ca); next.Add(bc);
                    next.Add(ab); next.Add(bc); next.Add(ca);
                }
                triangles = next;
            }

            var part = new Part();
            foreach (var vertex in vertices) part.Vertices.Add(vertex * radius);
            part.Triangles.AddRange(triangles);
            return part;
        }

        static int Midpoint(List<Vector3> vertices, Dictionary<long, int> midpoints, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (midpoints.TryGetValue(key, out int index)) return index;
            index = vertices.Count;
            vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
            midpoints.Add(key, index);
            return index;
        }

        /// <summary>Pushes every vertex in or out along its radius by a hash of its position.</summary>
        static void DistortRadial(Part part, float amount, int seed)
        {
            for (int i = 0; i < part.Vertices.Count; i++)
            {
                var vertex = part.Vertices[i];
                part.Vertices[i] = vertex * (1f + amount * SignedHash(vertex, seed));
            }
        }

        /// <summary>Pushes each corner by a hash of its position; duplicate corners move together.</summary>
        static void DistortCorners(Part part, Vector3 amount, int seed)
        {
            for (int i = 0; i < part.Vertices.Count; i++)
            {
                var vertex = part.Vertices[i];
                part.Vertices[i] = vertex + new Vector3(
                    amount.x * SignedHash(vertex, seed),
                    amount.y * SignedHash(vertex, seed + 1),
                    amount.z * SignedHash(vertex, seed + 2));
            }
        }

        static void Scale(Part part, Vector3 scale)
        {
            for (int i = 0; i < part.Vertices.Count; i++)
                part.Vertices[i] = Vector3.Scale(part.Vertices[i], scale);
        }

        static float SignedHash(Vector3 position, int seed) =>
            Hash(position, seed) * (2f / uint.MaxValue) - 1f;

        static uint Hash(Vector3 position, int seed)
        {
            unchecked
            {
                uint hash = (uint)seed * 2166136261u;
                hash = Mix(hash, position.x);
                hash = Mix(hash, position.y);
                hash = Mix(hash, position.z);
                hash ^= hash >> 16;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                hash *= 3266489917u;
                hash ^= hash >> 16;
                return hash;
            }
        }

        static uint Mix(uint hash, float value)
        {
            unchecked
            {
                hash ^= (uint)Mathf.RoundToInt(value * 10000f);
                hash *= 16777619u;
                return hash;
            }
        }

        static void Convex(GameObject root, Mesh mesh)
        {
            var collider = root.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
        }

        static Material Material(string name, float red, float green, float blue)
        {
            string path = $"{Root}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(Lit)) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.enableInstancing = true;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(red, green, blue));
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh MeshAsset(string id, MeshBuilder builder)
        {
            string name = id + "Mesh";
            string path = $"{Root}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh { name = name };
            builder.Fill(mesh);
            if (created) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static GameObject Prefab(string id, Mesh mesh, Material[] materials, System.Action<GameObject> configure = null)
        {
            var root = new GameObject(id);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = materials;
            configure?.Invoke(root);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Root}/{id}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void Capsule(GameObject root, float radius, float height)
        {
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
        }

        /// <summary>Vertices and triangle indices of one shape, in its own local space.</summary>
        sealed class Part
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();
        }

        /// <summary>Accumulates parts into one mesh with one submesh per material slot.</summary>
        sealed class MeshBuilder
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<List<int>> triangles = new List<List<int>>();

            public void Add(Part part, int submesh) => Add(part, submesh, Vector3.zero, Quaternion.identity);

            public void Add(Part part, int submesh, Vector3 offset, Quaternion rotation)
            {
                while (triangles.Count <= submesh) triangles.Add(new List<int>());
                int first = vertices.Count;
                foreach (var vertex in part.Vertices) vertices.Add(rotation * vertex + offset);
                var indices = triangles[submesh];
                foreach (int index in part.Triangles) indices.Add(first + index);
            }

            public void Fill(Mesh mesh)
            {
                mesh.Clear();
                mesh.indexFormat = IndexFormat.UInt16;
                mesh.SetVertices(vertices);
                mesh.subMeshCount = triangles.Count;
                for (int i = 0; i < triangles.Count; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }
        }

        static Part Cylinder(float radius, float height, int sides = 8)
        {
            var part = new Part();
            var bottom = Ring(part, radius, 0f, sides);
            var top = Ring(part, radius, height, sides);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                Tri(part, top[i], top[next], bottom[next]);
                Tri(part, top[i], bottom[next], bottom[i]);
            }
            int bottomCenter = part.Vertices.Count;
            part.Vertices.Add(Vector3.zero);
            int topCenter = part.Vertices.Count;
            part.Vertices.Add(new Vector3(0f, height, 0f));
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                Tri(part, topCenter, top[next], top[i]);
                Tri(part, bottomCenter, bottom[i], bottom[next]);
            }
            return part;
        }

        static Part Cone(float radius, float height, int sides = 8)
        {
            var part = new Part();
            var ring = Ring(part, radius, 0f, sides);
            int apex = part.Vertices.Count;
            part.Vertices.Add(new Vector3(0f, height, 0f));
            int center = part.Vertices.Count;
            part.Vertices.Add(Vector3.zero);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                Tri(part, ring[i], apex, ring[next]);
                Tri(part, center, ring[i], ring[next]);
            }
            return part;
        }

        static Part Ellipsoid(float rx, float ry, float rz, int latitudes = 6, int longitudes = 8)
        {
            var part = new Part();
            part.Vertices.Add(new Vector3(0f, ry, 0f));
            for (int latitude = 1; latitude < latitudes; latitude++)
            {
                float phi = Mathf.PI * latitude / latitudes;
                float y = Mathf.Cos(phi) * ry;
                float ring = Mathf.Sin(phi);
                for (int longitude = 0; longitude < longitudes; longitude++)
                {
                    float theta = 2f * Mathf.PI * longitude / longitudes;
                    part.Vertices.Add(new Vector3(ring * Mathf.Cos(theta) * rx, y, ring * Mathf.Sin(theta) * rz));
                }
            }
            int bottom = part.Vertices.Count;
            part.Vertices.Add(new Vector3(0f, -ry, 0f));

            for (int longitude = 0; longitude < longitudes; longitude++)
            {
                int next = (longitude + 1) % longitudes;
                Tri(part, 0, 1 + next, 1 + longitude);
            }
            for (int latitude = 1; latitude < latitudes - 1; latitude++)
            {
                int upper = 1 + (latitude - 1) * longitudes;
                int lower = upper + longitudes;
                for (int longitude = 0; longitude < longitudes; longitude++)
                {
                    int next = (longitude + 1) % longitudes;
                    Tri(part, upper + longitude, upper + next, lower + next);
                    Tri(part, upper + longitude, lower + next, lower + longitude);
                }
            }
            int last = 1 + (latitudes - 2) * longitudes;
            for (int longitude = 0; longitude < longitudes; longitude++)
            {
                int next = (longitude + 1) % longitudes;
                Tri(part, last + longitude, last + next, bottom);
            }
            return part;
        }

        static Part Box(Vector3 size)
        {
            var part = new Part();
            float x = size.x * 0.5f, y = size.y * 0.5f, z = size.z * 0.5f;
            Quad(part, new Vector3(-x, -y, -z), new Vector3(-x, -y, z), new Vector3(-x, y, z), new Vector3(-x, y, -z));
            Quad(part, new Vector3(x, -y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(x, -y, z));
            Quad(part, new Vector3(-x, y, -z), new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(x, y, -z));
            Quad(part, new Vector3(-x, -y, -z), new Vector3(x, -y, -z), new Vector3(x, -y, z), new Vector3(-x, -y, z));
            Quad(part, new Vector3(-x, -y, z), new Vector3(x, -y, z), new Vector3(x, y, z), new Vector3(-x, y, z));
            Quad(part, new Vector3(-x, -y, -z), new Vector3(-x, y, -z), new Vector3(x, y, -z), new Vector3(x, -y, -z));
            return part;
        }

        /// <summary>A box with its edges and corners chamfered by a small bevel.</summary>
        static Part BeveledBox(Vector3 size, float bevel)
        {
            var part = new Part();
            Vector3 half = size * 0.5f;
            float maxBevel = Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.9f;
            float b = Mathf.Clamp(bevel, 0f, maxBevel);
            Vector3 inner = half - Vector3.one * b;

            // Six inset face quads.
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    float a = sign * half[axis];
                    Vector3 p0 = Vector3.zero, p1 = Vector3.zero, p2 = Vector3.zero, p3 = Vector3.zero;
                    p0[axis] = a; p0[u] = -inner[u]; p0[v] = -inner[v];
                    p1[axis] = a; p1[u] = inner[u]; p1[v] = -inner[v];
                    p2[axis] = a; p2[u] = inner[u]; p2[v] = inner[v];
                    p3[axis] = a; p3[u] = -inner[u]; p3[v] = inner[v];
                    Vector3 outward = Vector3.zero;
                    outward[axis] = sign;
                    QuadOut(part, p0, p1, p2, p3, outward);
                }
            }

            // Twelve chamfer strips along the edges.
            for (int axis = 0; axis < 3; axis++)
            {
                for (int other = axis + 1; other < 3; other++)
                {
                    int along = 3 - axis - other;
                    for (int sa = -1; sa <= 1; sa += 2)
                    {
                        for (int sb = -1; sb <= 1; sb += 2)
                        {
                            Vector3 q0 = Vector3.zero, q1 = Vector3.zero, q2 = Vector3.zero, q3 = Vector3.zero;
                            q0[axis] = sa * half[axis]; q0[other] = sb * inner[other]; q0[along] = -inner[along];
                            q1 = q0; q1[along] = inner[along];
                            q2[axis] = sa * inner[axis]; q2[other] = sb * half[other]; q2[along] = inner[along];
                            q3 = q2; q3[along] = -inner[along];
                            Vector3 outward = Vector3.zero;
                            outward[axis] = sa; outward[other] = sb;
                            QuadOut(part, q0, q1, q2, q3, outward);
                        }
                    }
                }
            }

            // Eight corner triangles.
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 px = Vector3.zero, py = Vector3.zero, pz = Vector3.zero;
                        px[0] = sx * half[0]; px[1] = sy * inner[1]; px[2] = sz * inner[2];
                        py[0] = sx * inner[0]; py[1] = sy * half[1]; py[2] = sz * inner[2];
                        pz[0] = sx * inner[0]; pz[1] = sy * inner[1]; pz[2] = sz * half[2];
                        TriOut(part, px, py, pz, new Vector3(sx, sy, sz));
                    }
                }
            }
            return part;
        }

        static void QuadOut(Part part, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) Quad(part, a, d, c, b);
            else Quad(part, a, b, c, d);
        }

        static void TriOut(Part part, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            int first = part.Vertices.Count;
            part.Vertices.Add(a);
            part.Vertices.Add(b);
            part.Vertices.Add(c);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) Tri(part, first, first + 2, first + 1);
            else Tri(part, first, first + 1, first + 2);
        }

        static Part CrossedQuads(float width, float height, int planes)
        {
            var part = new Part();
            for (int i = 0; i < planes; i++)
            {
                float angle = Mathf.PI * i / planes;
                var right = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (width * 0.5f);
                var up = new Vector3(0f, height * 0.5f, 0f);
                var center = up;
                Quad(part, center - right - up, center + right - up, center + right + up, center - right + up);
                Quad(part, center - right - up, center - right + up, center + right + up, center + right - up);
            }
            return part;
        }

        static int[] Ring(Part part, float radius, float y, int sides)
        {
            var indices = new int[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = 2f * Mathf.PI * i / sides;
                indices[i] = part.Vertices.Count;
                part.Vertices.Add(new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius));
            }
            return indices;
        }

        static void Quad(Part part, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int first = part.Vertices.Count;
            part.Vertices.Add(a);
            part.Vertices.Add(b);
            part.Vertices.Add(c);
            part.Vertices.Add(d);
            Tri(part, first, first + 1, first + 2);
            Tri(part, first, first + 2, first + 3);
        }

        static void Tri(Part part, int a, int b, int c)
        {
            part.Triangles.Add(a);
            part.Triangles.Add(b);
            part.Triangles.Add(c);
        }
    }
}

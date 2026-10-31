using System.Collections.Generic;
using Procedural.Generation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Procedural.World
{
    /// <summary>The Unity objects of one level; assets still in memory.</summary>
    public sealed class IslandBuild
    {
        public GameObject Root;
        public TerrainData TerrainData;
        /// <summary>Layer weights [z, x, layer]; apply again after saving TerrainData as an asset.</summary>
        public float[,,] Alphamaps;
        public TreePrototype[] TreePrototypes = new TreePrototype[0];
        public TreeInstance[] Trees = new TreeInstance[0];
        public DetailPrototype[] DetailPrototypes = new DetailPrototype[0];
        /// <summary>Coverage 0..255 per detail prototype, [z, x], at DetailResolution.</summary>
        public int[][,] DetailLayers = new int[0][,];
        public int DetailResolution = 16, DetailPerPatch = 16;
        public readonly List<Mesh> Meshes = new List<Mesh>();

        /// <summary>Writes alphamaps, trees and detail layers into TerrainData (again after it became an asset).</summary>
        public void ApplyLayers()
        {
            TerrainData.SetAlphamaps(0, 0, Alphamaps);
            TerrainData.treePrototypes = TreePrototypes;
            TerrainData.SetTreeInstances(Trees, false);   // keep the core's heights: trees stand sunk on slopes
            TerrainData.SetDetailResolution(DetailResolution, DetailPerPatch);
            TerrainData.SetDetailScatterMode(DetailScatterMode.CoverageMode);
            TerrainData.detailPrototypes = DetailPrototypes;
            for (int i = 0; i < DetailLayers.Length; i++)
                TerrainData.SetDetailLayer(0, 0, i, DetailLayers[i]);
        }
    }

    /// <summary>
    /// Turns a generator result into a Terrain and water meshes. Main thread
    /// only; heights are metres with sea level at world y = 0.
    /// </summary>
    public static class IslandBuilder
    {
        // Water surfaces sit slightly above their level so carved banks do not z-fight.
        const float WaterLift = 0.03f;
        const float TerrainHeadroom = 5f;
        // The sea reaches past the camera's view so no edge shows on the horizon.
        const float SeaMargin = 8000f;
        // Tapered river tips: narrower strips make degenerate, sparkling triangles.
        const float MinRibbonWidth = 0.4f;

        public static IslandBuild Build(IslandResult result, IslandBuildAssets assets)
        {
            var build = new IslandBuild
            {
                Root = new GameObject($"Island {result.Recipe.seed} ({result.Mode})")
            };
            build.TerrainData = BuildTerrainData(result, assets, out build.Alphamaps);

            var terrainObject = Terrain.CreateTerrainGameObject(build.TerrainData);
            terrainObject.name = "Terrain";
            terrainObject.transform.SetParent(build.Root.transform, false);
            terrainObject.transform.localPosition = new Vector3(0f, -result.SeaDepth, 0f);
            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.materialTemplate = assets.terrain;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 1f;
            // Placeholder trees are plain meshes without billboards: full meshes up to the
            // tree distance, where the fog (ApplyAtmosphere) is already thick.
            terrain.treeDistance = TreeDistance(result.WorldSize);
            terrain.treeBillboardDistance = terrain.treeDistance;
            terrain.treeMaximumFullLODCount = 100000;
            terrain.detailObjectDistance = 150f;
            if (assets.species != null && result.Plants != null)
                AddPlants(build, result, assets.species);
            build.ApplyLayers();

            AddMesh(build, "Sea", SeaMesh(result.WorldSize), assets.sea);
            AddMesh(build, "Rivers", RiverMesh(result.Rivers), assets.river);
            AddMesh(build, "Lakes", LakeMesh(result), assets.lake);
            return build;
        }

        /// <summary>Trees are drawn to 700 m on a 1 km map, 1 km on a 2 km map.</summary>
        public static float TreeDistance(float worldSize) => Mathf.Clamp(0.7f * worldSize, 700f, 1000f);

        /// <summary>
        /// Haze over the open scene: exponential-squared fog that is about 70 % thick at
        /// the tree distance, so trees and the sea's edge fade instead of popping.
        /// </summary>
        public static void ApplyAtmosphere(float worldSize)
        {
            float distance = TreeDistance(worldSize);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Mathf.Sqrt(-Mathf.Log(0.3f)) / distance;
            RenderSettings.fogColor = new Color(0.62f, 0.70f, 0.78f);
        }

        static void AddPlants(IslandBuild build, IslandResult result, SpeciesCatalog catalog)
        {
            var data = build.TerrainData;
            var size = data.size;
            var treePrototypes = new List<TreePrototype>();
            var treeIndex = new int[result.Species.Length];
            var visuals = new SpeciesVisual[result.Species.Length];
            for (int k = 0; k < result.Species.Length; k++)
            {
                treeIndex[k] = -1;
                visuals[k] = catalog.Find(result.Species[k].Id);
                if (visuals[k] == null || visuals[k].prefab == null)
                {
                    Debug.LogWarning($"Procedural: no prefab for species '{result.Species[k].Id}'; it is left out.");
                    visuals[k] = null;
                    continue;
                }
                if (visuals[k].build == PlantBuild.TerrainTree)
                {
                    treeIndex[k] = treePrototypes.Count;
                    treePrototypes.Add(new TreePrototype { prefab = visuals[k].prefab });
                }
            }

            var trees = new List<TreeInstance>();
            Transform props = null;
            var instanced = new Dictionary<int, List<Matrix4x4>>();
            foreach (var p in result.Plants)
            {
                var visual = visuals[p.Species];
                if (visual == null) continue;
                if (visual.build == PlantBuild.TerrainTree)
                {
                    trees.Add(new TreeInstance
                    {
                        position = new Vector3(p.X / size.x, (p.Y + result.SeaDepth) / size.y, p.Z / size.z),
                        widthScale = p.Scale,
                        heightScale = p.Scale,
                        rotation = p.Yaw,
                        color = Color.white,
                        lightmapColor = Color.white,
                        prototypeIndex = treeIndex[p.Species]
                    });
                }
                else if (visual.build == PlantBuild.SceneObject)
                {
                    if (props == null)
                    {
                        props = new GameObject("Props").transform;
                        props.SetParent(build.Root.transform, false);
                    }
                    var instance = Object.Instantiate(visual.prefab, new Vector3(p.X, p.Y, p.Z), Rotation(p), props);
                    instance.name = visual.prefab.name;
                    instance.transform.localScale = Vector3.one * p.Scale;
                }
                else if (visual.build == PlantBuild.Instanced)
                {
                    if (!instanced.TryGetValue(p.Species, out var matrices))
                        instanced[p.Species] = matrices = new List<Matrix4x4>();
                    matrices.Add(Matrix4x4.TRS(new Vector3(p.X, p.Y, p.Z), Rotation(p), Vector3.one * p.Scale));
                }
            }
            build.TreePrototypes = treePrototypes.ToArray();
            build.Trees = trees.ToArray();
            if (instanced.Count > 0)
            {
                var drawn = build.Root.AddComponent<InstancedProps>();
                foreach (var pair in instanced)
                {
                    var prefab = visuals[pair.Key].prefab;
                    drawn.Add(prefab.GetComponent<MeshFilter>().sharedMesh, prefab.GetComponent<MeshRenderer>().sharedMaterials, pair.Value);
                }
            }

            // Detail coverage maps come in the order of the detail species. Unity may adjust the
            // requested resolution, so the maps are sampled at the resolution it reports back.
            var detailPrototypes = new List<DetailPrototype>();
            var layers = new List<int[,]>();
            build.DetailPerPatch = Mathf.Min(32, data.alphamapResolution);
            data.SetDetailResolution(data.alphamapResolution, build.DetailPerPatch);
            int a = build.DetailResolution = data.detailResolution, map = 0;
            for (int k = 0; k < result.Species.Length; k++)
            {
                if (!result.Species[k].Detail) continue;
                var coverage = result.DetailCoverage[map++];
                if (visuals[k] == null || visuals[k].build != PlantBuild.TerrainDetail) continue;
                detailPrototypes.Add(new DetailPrototype
                {
                    prototype = visuals[k].prefab,
                    usePrototypeMesh = true,
                    useInstancing = true,
                    renderMode = DetailRenderMode.VertexLit,
                    minWidth = 0.8f, maxWidth = 1.3f, minHeight = 0.8f, maxHeight = 1.4f,
                    noiseSpread = 0.3f,
                    healthyColor = Color.white,
                    dryColor = new Color(0.85f, 0.85f, 0.7f)
                });
                // A texel takes the lowest coverage of its corners: none where any corner is water.
                var layer = new int[a, a];
                float scale = (coverage.Width - 1f) / a;
                for (int z = 0; z < a; z++)
                    for (int x = 0; x < a; x++)
                    {
                        int x0 = Mathf.Min((int)(x * scale), coverage.Width - 2), z0 = Mathf.Min((int)(z * scale), coverage.Height - 2);
                        float c = Mathf.Min(Mathf.Min(coverage[x0, z0], coverage[x0 + 1, z0]), Mathf.Min(coverage[x0, z0 + 1], coverage[x0 + 1, z0 + 1]));
                        layer[z, x] = Mathf.RoundToInt(Mathf.Clamp01(c) * 255f);
                    }
                layers.Add(layer);
            }
            build.DetailPrototypes = detailPrototypes.ToArray();
            build.DetailLayers = layers.ToArray();
        }

        /// <summary>Unity yaw maps local +x to (cos, 0, -sin); roll and tilt then lay local up on the slope.</summary>
        static Quaternion Rotation(PlantInstance p)
        {
            return Quaternion.Euler(0f, p.Yaw * Mathf.Rad2Deg, 0f) * Quaternion.Euler(p.Roll * Mathf.Rad2Deg, 0f, p.Tilt * Mathf.Rad2Deg);
        }

        static TerrainData BuildTerrainData(IslandResult result, IslandBuildAssets assets, out float[,,] maps)
        {
            var height = result.Height;
            int n = height.Width;
            float sizeY = result.SeaDepth + result.PeakHeight + TerrainHeadroom;
            var data = new TerrainData { heightmapResolution = n };
            data.size = new Vector3(result.WorldSize, sizeY, result.WorldSize);

            var heights = new float[n, n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                    heights[z, x] = Mathf.Clamp01((height[x, z] + result.SeaDepth) / sizeY);
            data.SetHeights(0, 0, heights);

            // Alphamap texels sit between heightmap samples: average the four corners.
            data.terrainLayers = assets.layers;
            int a = n - 1;
            data.alphamapResolution = a;
            if (data.alphamapResolution != a)
                throw new System.InvalidOperationException($"Terrain alphamap resolution {a} was changed to {data.alphamapResolution}.");
            maps = new float[a, a, SurfaceLayer.Count];
            for (int layer = 0; layer < SurfaceLayer.Count; layer++)
            {
                var w = result.Surface[layer];
                for (int z = 0; z < a; z++)
                    for (int x = 0; x < a; x++)
                        maps[z, x, layer] = 0.25f * (w[x, z] + w[x + 1, z] + w[x, z + 1] + w[x + 1, z + 1]);
            }
            return data;
        }

        static void AddMesh(IslandBuild build, string name, Mesh mesh, Material material)
        {
            if (mesh == null) return;
            mesh.name = name;
            build.Meshes.Add(mesh);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(build.Root.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Mesh SeaMesh(float worldSize)
        {
            float lo = -SeaMargin, hi = worldSize + SeaMargin;
            var vertices = new List<Vector3>
            {
                new Vector3(lo, 0f, lo), new Vector3(lo, 0f, hi),
                new Vector3(hi, 0f, hi), new Vector3(hi, 0f, lo)
            };
            return Finish(vertices, null, new List<int> { 0, 1, 2, 0, 2, 3 });
        }

        static Mesh RiverMesh(List<RiverSegment> rivers)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var s in rivers)
            {
                int m = s.X.Length;
                if (m < 2) continue;
                int first = vertices.Count;
                float along = 0f;
                for (int i = 0; i < m; i++)
                {
                    int prev = Mathf.Max(i - 1, 0), next = Mathf.Min(i + 1, m - 1);
                    var tangent = new Vector2(s.X[next] - s.X[prev], s.Z[next] - s.Z[prev]).normalized;
                    float width = Mathf.Max(s.Width[i], MinRibbonWidth);
                    var side = new Vector2(-tangent.y, tangent.x) * (0.5f * width);
                    if (i > 0)
                        along += new Vector2(s.X[i] - s.X[i - 1], s.Z[i] - s.Z[i - 1]).magnitude;
                    float y = s.Surface[i] + WaterLift;
                    vertices.Add(new Vector3(s.X[i] - side.x, y, s.Z[i] - side.y));
                    vertices.Add(new Vector3(s.X[i] + side.x, y, s.Z[i] + side.y));
                    // v runs along the river in units of its width, ready for a flow texture.
                    float v = along / width;
                    uvs.Add(new Vector2(0f, v));
                    uvs.Add(new Vector2(1f, v));
                    if (i == 0) continue;
                    int k = first + 2 * (i - 1);
                    triangles.Add(k); triangles.Add(k + 2); triangles.Add(k + 1);
                    triangles.Add(k + 1); triangles.Add(k + 2); triangles.Add(k + 3);
                }
            }
            return triangles.Count == 0 ? null : Finish(vertices, uvs, triangles);
        }

        static Mesh LakeMesh(IslandResult result)
        {
            if (result.Lakes.Count == 0) return null;
            var levels = new Dictionary<int, float>();
            foreach (var lake in result.Lakes) levels[lake.Id] = lake.Level;

            // One quad per lake sample, grown by one sample so shores have no gaps;
            // the terrain hides the parts above water.
            var height = result.Height;
            int n = height.Width;
            float cell = height.CellSize, half = 0.5f * cell;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int id = LakeAround(result.LakeId, n, x, z);
                    if (id < 0 || !levels.TryGetValue(id, out float level)) continue;
                    float y = level + WaterLift, cx = x * cell, cz = z * cell;
                    int k = vertices.Count;
                    vertices.Add(new Vector3(cx - half, y, cz - half));
                    vertices.Add(new Vector3(cx - half, y, cz + half));
                    vertices.Add(new Vector3(cx + half, y, cz + half));
                    vertices.Add(new Vector3(cx + half, y, cz - half));
                    triangles.Add(k); triangles.Add(k + 1); triangles.Add(k + 2);
                    triangles.Add(k); triangles.Add(k + 2); triangles.Add(k + 3);
                }
            return triangles.Count == 0 ? null : Finish(vertices, null, triangles);
        }

        static int LakeAround(int[] lakeId, int n, int x, int z)
        {
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                    int id = lakeId[nz * n + nx];
                    if (id >= 0) return id;
                }
            return -1;
        }

        static Mesh Finish(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            var mesh = new Mesh
            {
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            if (uvs != null) mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            // Water faces up whichever way the strips were wound.
            if (mesh.normals[0].y < 0f)
            {
                for (int i = 0; i < triangles.Count; i += 3)
                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

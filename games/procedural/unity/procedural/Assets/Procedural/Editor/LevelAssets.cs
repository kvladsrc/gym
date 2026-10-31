using System.Diagnostics;
using System.IO;
using Procedural.Generation;
using Procedural.World;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Procedural.Editor
{
    /// <summary>Paths and build time of a saved level.</summary>
    sealed class SavedLevel
    {
        public string Report;
        public string Map;
        public double BuildSeconds;
    }

    /// <summary>
    /// Replaces the generated level: assets in Assets/Procedural/Generated, the
    /// level prefab in the generator's scene, the report and map in artifacts/.
    /// </summary>
    static class LevelAssets
    {
        public static SavedLevel Save(IslandResult result, IslandGenerator generator)
        {
            var watch = Stopwatch.StartNew();
            // Placeholders are refreshed on every bake; Studio asset sets are used as they are.
            var assets = generator.assets != null && generator.assets.IsComplete && !PlaceholderAssets.Owns(generator.assets)
                ? generator.assets
                : PlaceholderAssets.Ensure();

            if (generator.level != null) UnityEngine.Object.DestroyImmediate(generator.level);
            if (AssetDatabase.IsValidFolder(ProceduralPaths.Generated))
                AssetDatabase.DeleteAsset(ProceduralPaths.Generated);
            Folders.Ensure(ProceduralPaths.Generated);

            var build = IslandBuilder.Build(result, assets);
            AssetDatabase.CreateAsset(build.TerrainData, ProceduralPaths.Generated + "/Terrain.asset");
            // Alphamaps, trees and details set before the TerrainData became an asset are not saved with it.
            build.ApplyLayers();
            EditorUtility.SetDirty(build.TerrainData);
            foreach (var mesh in build.Meshes)
                AssetDatabase.CreateAsset(mesh, $"{ProceduralPaths.Generated}/{mesh.name}.asset");
            PrefabUtility.SaveAsPrefabAssetAndConnect(
                build.Root, ProceduralPaths.Generated + "/Island.prefab", InteractionMode.AutomatedAction);
            AssetDatabase.SaveAssets();

            generator.level = build.Root;
            IslandBuilder.ApplyAtmosphere(result.WorldSize);
            // A new island puts the walker somewhere else: back onto dry, gentle ground.
            var walker = UnityEngine.Object.FindAnyObjectByType<Walker>(UnityEngine.FindObjectsInactive.Include);
            if (walker != null)
            {
                walker.transform.position = SpawnPoint(walker.transform.position);
                EditorUtility.SetDirty(walker.transform);
            }
            EditorUtility.SetDirty(generator);
            var scene = generator.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            if (scene.path == ProceduralPaths.Scene) EditorSceneManager.SaveScene(scene);

            string name = $"{result.Recipe.preset}-{result.Recipe.seed}-{result.Mode}".ToLowerInvariant();
            Directory.CreateDirectory(ProceduralPaths.Reports);
            string report = Path.Combine(ProceduralPaths.Reports, name + ".json");
            File.WriteAllText(report, result.ReportJson);
            Directory.CreateDirectory(ProceduralPaths.Maps);
            string map = Path.Combine(ProceduralPaths.Maps, name + ".png");
            DebugMaps.WriteOverview(result, assets, map);
            DebugMaps.WriteField(result, result.Moisture, Path.Combine(ProceduralPaths.Maps, name + "-moisture.png"));
            DebugMaps.WriteField(result, result.Wetness, Path.Combine(ProceduralPaths.Maps, name + "-wetness.png"));

            return new SavedLevel { Report = report, Map = map, BuildSeconds = watch.Elapsed.TotalSeconds };
        }

        /// <summary>Dry, gentle ground nearest the island's centre: above 4 m, slope under 12°.</summary>
        public static UnityEngine.Vector3 SpawnPoint(UnityEngine.Vector3 fallback)
        {
            var terrain = UnityEngine.Terrain.activeTerrain;
            if (terrain == null) return fallback;
            var data = terrain.terrainData;
            var origin = terrain.transform.position;
            float best = float.MaxValue;
            var spawn = fallback;
            for (float z = 0.05f; z < 0.95f; z += 0.005f)
                for (float x = 0.05f; x < 0.95f; x += 0.005f)
                {
                    float height = data.GetInterpolatedHeight(x, z) + origin.y;
                    if (height < 4f || data.GetSteepness(x, z) > 12f) continue;
                    float d = (x - 0.5f) * (x - 0.5f) + (z - 0.5f) * (z - 0.5f);
                    if (d >= best) continue;
                    best = d;
                    spawn = new UnityEngine.Vector3(x * data.size.x + origin.x, height + 0.2f, z * data.size.z + origin.z);
                }
            return spawn;
        }
    }
}

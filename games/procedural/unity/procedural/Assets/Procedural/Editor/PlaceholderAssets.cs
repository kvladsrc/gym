using Procedural.Generation;
using Procedural.World;
using UnityEditor;
using UnityEngine;

namespace Procedural.Editor
{
    /// <summary>Flat-colored terrain layers and water materials, made once and committed.</summary>
    static class PlaceholderAssets
    {
        const string AssetsPath = ProceduralPaths.Placeholders + "/IslandBuildAssets.asset";
        const string Lit = "Universal Render Pipeline/Lit";
        const string TerrainLit = "Universal Render Pipeline/Terrain/Lit";

        public static bool Owns(IslandBuildAssets assets) => AssetDatabase.GetAssetPath(assets) == AssetsPath;

        /// <summary>Loads or makes the placeholders and applies the colors below, keeping GUIDs.</summary>
        public static IslandBuildAssets Ensure()
        {
            Folders.Ensure(ProceduralPaths.Placeholders);
            var assets = AssetDatabase.LoadAssetAtPath<IslandBuildAssets>(AssetsPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<IslandBuildAssets>();
                AssetDatabase.CreateAsset(assets, AssetsPath);
            }
            assets.layers = new TerrainLayer[SurfaceLayer.Count];
            assets.layers[SurfaceLayer.Sand] = Layer("Sand", new Color(0.80f, 0.74f, 0.56f));
            assets.layers[SurfaceLayer.Grass] = Layer("Grass", new Color(0.31f, 0.42f, 0.20f));
            assets.layers[SurfaceLayer.Rock] = Layer("Rock", new Color(0.44f, 0.42f, 0.39f));
            assets.layers[SurfaceLayer.Gravel] = Layer("Gravel", new Color(0.53f, 0.50f, 0.45f));
            assets.terrain = Material("Terrain", TerrainLit, Color.white, 0f);
            if (assets.terrain.HasProperty("_EnableHeightBlend")) assets.terrain.SetFloat("_EnableHeightBlend", 1f);
            if (assets.terrain.HasProperty("_HeightTransition")) assets.terrain.SetFloat("_HeightTransition", 0.2f);
            assets.terrain.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
            EditorUtility.SetDirty(assets.terrain);
            assets.sea = Material("Sea", Lit, new Color(0.07f, 0.20f, 0.33f), 0.75f);
            assets.river = Material("River", Lit, new Color(0.13f, 0.30f, 0.38f), 0.7f);
            assets.lake = Material("Lake", Lit, new Color(0.10f, 0.26f, 0.36f), 0.75f);
            assets.species = PlaceholderPrefabs.Ensure();
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            return assets;
        }

        static TerrainLayer Layer(string name, Color color)
        {
            var (albedo, normal, mask) = ProceduralTerrainTextures.Ensure(name, color);
            float tileSize = name switch
            {
                "Sand" => 3f,
                "Grass" => 2.5f,
                "Rock" => 4f,
                "Gravel" => 1.5f,
                _ => 2f
            };

            string path = $"{ProceduralPaths.Placeholders}/{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = albedo;
            layer.normalMapTexture = normal;
            layer.maskMapTexture = mask;
            layer.normalScale = 1f;
            layer.tileSize = new Vector2(tileSize, tileSize);
            layer.metallic = 0f;
            // With a mask map the URP Terrain Lit shader takes smoothness from the mask's alpha.
            layer.smoothness = 1f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        static Material Material(string name, string shader, Color color, float smoothness)
        {
            string path = $"{ProceduralPaths.Placeholders}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader)) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.enableInstancing = true;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }
    }

    static class Folders
    {
        /// <summary>Creates every missing folder of an "Assets/..." path.</summary>
        public static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}

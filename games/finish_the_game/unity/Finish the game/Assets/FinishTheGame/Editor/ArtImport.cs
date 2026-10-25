using UnityEditor;
using UnityEngine;

namespace FinishTheGame.Editor
{
    /// <summary>The studio's art (Art/, art/convert_models.py): models come in
    /// without materials (the level gives them its own, with their textures),
    /// readable (the level combines them and makes colliders from them);
    /// textures readable (their average colour, the tiles' seams).</summary>
    public class ArtImport : AssetPostprocessor
    {
        const string Art = "Assets/FinishTheGame/Art/";

        void OnPreprocessModel()
        {
            // The hero's rig is HeroAnimation's.
            if (!assetPath.StartsWith(Art) || assetPath.StartsWith(Art + "Characters/")) return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            // Blender's z-up turned into the mesh itself, not left on the root
            // node: the level takes the mesh alone.
            importer.bakeAxisConversion = true;
            // Simplified with distance (Unity's mesh LODs).
            importer.generateMeshLods = true;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Art)) return;
            var importer = (TextureImporter)assetImporter;
            importer.isReadable = true;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.wrapMode = assetPath.Contains("/Textures/") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.anisoLevel = 4;
            if (assetPath.Contains("/Sky/"))
            {
                // A panorama: round in its width, its full size.
                importer.maxTextureSize = 4096;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
            }
        }
    }
}

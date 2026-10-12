using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// Painted models (Hunyuan3D-Paint) carry their texture embedded in the
    /// FBX, which Unity does not use until it is extracted. After a model in
    /// Art imports with an untextured material, its embedded textures are
    /// extracted to a Textures folder beside it and the model is reimported:
    /// the material then finds its texture there by file name (Unity's default
    /// search; the rig server and glb_to_fbx.py name them uniquely). Models
    /// without embedded textures (the vertex-colour ones) are left as they are.
    /// </summary>
    public class PaintedImport : AssetPostprocessor
    {
        const string Art = "Assets/Soulslike/Art/";
        // Once per model and session: a texture the material still cannot
        // find must not extract and reimport forever.
        static readonly HashSet<string> tried = new HashSet<string>();

        const string Levels = Art + "Levels/";

        /// <summary>A level (tools/export_level.py) has thousands of objects:
        /// Unity's mesh LODs simplify them with distance.</summary>
        void OnPreprocessModel()
        {
            if (assetPath.StartsWith(Levels) && assetImporter is ModelImporter importer)
                importer.generateMeshLods = true;
        }

        /// <summary>Paint bakes light and shade into the texture: matte,
        /// without the imported material's default sheen (it read as plastic).</summary>
        void OnPostprocessMaterial(Material material)
        {
            if (!assetPath.StartsWith(Art)) return;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            // A level repeats its models (dozens of the same grass, trees):
            // drawn in one call per mesh.
            material.enableInstancing = true;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (!path.StartsWith(Art) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                var model = path;
                // Extraction writes assets: not in the middle of an import.
                EditorApplication.delayCall += () => Extract(model);
            }
        }

        static void Extract(string path)
        {
            // A delayCall can come while the editor is still importing (a
            // refresh of several models): extraction then does nothing, and
            // the model stayed white. Wait for the import to end.
            if (EditorApplication.isUpdating || EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += () => Extract(path);
                return;
            }
            if (!Untextured(path) || !tried.Add(path)) return;
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            var folder = Path.GetDirectoryName(path) + "/Textures";
            int before = Directory.Exists(folder) ? Directory.GetFiles(folder).Length : 0;
            Directory.CreateDirectory(folder);
            importer.ExtractTextures(folder);
            int after = Directory.GetFiles(folder).Length;
            if (after == 0) Directory.Delete(folder);
            if (after == before) return;
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        static bool Untextured(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Material material && material.mainTexture == null) return true;
            return false;
        }
    }
}

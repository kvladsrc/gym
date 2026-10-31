using System;
using Procedural.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Procedural.Editor
{
    /// <summary>The level scene: a camera, a light and the generator with its recipe.</summary>
    static class IslandScene
    {
        const string DefaultRecipe = ProceduralPaths.Recipes + "/Default.asset";

        /// <summary>The generator of the open scene; opens or creates the island scene if there is none.</summary>
        public static IslandGenerator FindOrCreate()
        {
            var generator = UnityEngine.Object.FindAnyObjectByType<IslandGenerator>();
            if (generator != null) return generator;

            var active = SceneManager.GetActiveScene();
            if (active.isDirty)
                throw new InvalidOperationException(
                    $"Procedural: save or discard the changes in '{active.name}' before opening the island scene.");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ProceduralPaths.Scene) != null)
            {
                EditorSceneManager.OpenScene(ProceduralPaths.Scene);
                generator = UnityEngine.Object.FindAnyObjectByType<IslandGenerator>();
                if (generator != null) return generator;
            }
            return Create();
        }

        static IslandGenerator Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var camera = Camera.main;
            if (camera != null)
            {
                camera.nearClipPlane = 0.5f;
                camera.farClipPlane = 12000f;
                camera.transform.SetPositionAndRotation(new Vector3(512f, 260f, -180f), Quaternion.Euler(32f, 0f, 0f));
                camera.gameObject.AddComponent<FlyCamera>();
            }

            var generator = new GameObject("Island Generator").AddComponent<IslandGenerator>();
            generator.recipe = LoadOrCreateRecipe();
            generator.assets = PlaceholderAssets.Ensure();

            Folders.Ensure(ProceduralPaths.Root + "/Scenes");
            EditorSceneManager.SaveScene(scene, ProceduralPaths.Scene);
            var scenes = EditorBuildSettings.scenes;
            if (Array.FindIndex(scenes, s => s.path == ProceduralPaths.Scene) < 0)
            {
                var list = new EditorBuildSettingsScene[scenes.Length + 1];
                list[0] = new EditorBuildSettingsScene(ProceduralPaths.Scene, true);
                Array.Copy(scenes, 0, list, 1, scenes.Length);
                EditorBuildSettings.scenes = list;
            }
            return generator;
        }

        static IslandRecipeAsset LoadOrCreateRecipe()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<IslandRecipeAsset>(DefaultRecipe);
            if (recipe != null) return recipe;
            Folders.Ensure(ProceduralPaths.Recipes);
            recipe = ScriptableObject.CreateInstance<IslandRecipeAsset>();
            AssetDatabase.CreateAsset(recipe, DefaultRecipe);
            AssetDatabase.SaveAssets();
            return recipe;
        }
    }
}

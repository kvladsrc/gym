using System;
using System.Threading;
using Procedural.Generation;
using Procedural.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Procedural.Editor
{
    /// <summary>Menu entries; MCP drives bakes through them with editor.invoke_menu.</summary>
    static class IslandMenu
    {
        [MenuItem("Procedural/Preview Island", priority = 1)]
        static void Preview() => StartOn(BakeMode.Preview);

        [MenuItem("Procedural/Bake Island", priority = 2)]
        static void Bake() => StartOn(BakeMode.Bake);

        [MenuItem("Procedural/Cancel Bake", priority = 3)]
        static void Cancel() => BakeRunner.Cancel();

        [MenuItem("Procedural/Cancel Bake", true)]
        static bool CanCancel() => BakeRunner.Running;

        [MenuItem("Procedural/Open Island Scene", priority = 20)]
        static void OpenScene() => Selection.activeObject = IslandScene.FindOrCreate();

        /// <summary>With an active walker, Play starts on foot at the island's centre; without, the camera flies.</summary>
        [MenuItem("Procedural/Toggle Walker", priority = 21)]
        static void ToggleWalker()
        {
            var generator = IslandScene.FindOrCreate();
            var walker = Object.FindAnyObjectByType<Walker>(FindObjectsInactive.Include);
            if (walker == null)
            {
                var go = new GameObject("Walker", typeof(CharacterController), typeof(Walker));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, generator.gameObject.scene);
                walker = go.GetComponent<Walker>();
            }
            else
                walker.gameObject.SetActive(!walker.gameObject.activeSelf);
            if (walker.gameObject.activeSelf)
                walker.transform.position = LevelAssets.SpawnPoint(walker.transform.position);
            var scene = generator.gameObject.scene;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            if (scene.path == ProceduralPaths.Scene)
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log(walker.gameObject.activeSelf ? "Procedural: Play walks on foot (WASD, Shift, Space, right mouse to look)." : "Procedural: Play flies (FlyCamera).");
        }

        static void StartOn(BakeMode mode)
        {
            try
            {
                BakeRunner.Start(IslandScene.FindOrCreate(), mode);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
        }
    }

    [CustomEditor(typeof(IslandGenerator))]
    class IslandGeneratorEditor : UnityEditor.Editor
    {
        UnityEditor.Editor recipeEditor;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var generator = (IslandGenerator)target;
            if (generator.recipe != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Recipe", EditorStyles.boldLabel);
                CreateCachedEditor(generator.recipe, null, ref recipeEditor);
                recipeEditor.OnInspectorGUI();
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(BakeRunner.Running || generator.recipe == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Preview")) BakeRunner.Start(generator, BakeMode.Preview);
                    if (GUILayout.Button("Bake")) BakeRunner.Start(generator, BakeMode.Bake);
                    if (GUILayout.Button("New seed"))
                    {
                        Undo.RecordObject(generator.recipe, "New seed");
                        generator.recipe.recipe.seed = UnityEngine.Random.Range(1, int.MaxValue);
                        EditorUtility.SetDirty(generator.recipe);
                    }
                }
            }
            if (BakeRunner.Running && GUILayout.Button("Cancel")) BakeRunner.Cancel();
            EditorGUILayout.HelpBox(BakeRunner.Summary, MessageType.None);
            if (BakeRunner.Running) Repaint();
        }
    }

    /// <summary>
    /// Batch mode, with the Editor closed:
    /// Unity -batchmode -projectPath … -executeMethod Procedural.Editor.Batch.Bake
    /// [-recipe Assets/Procedural/Recipes/X.asset] [-mode Preview|Bake]
    /// </summary>
    public static class Batch
    {
        public static void Bake()
        {
            int code = 1;
            try
            {
                var generator = IslandScene.FindOrCreate();
                string recipePath = Argument("-recipe");
                if (recipePath != null)
                    generator.recipe = AssetDatabase.LoadAssetAtPath<IslandRecipeAsset>(recipePath);
                var mode = Argument("-mode") == "Preview" ? BakeMode.Preview : BakeMode.Bake;
                var result = IslandPipeline.Run(generator.recipe.recipe.Clone(), mode,
                    ProceduralPaths.Checkpoints, null, CancellationToken.None);
                var saved = LevelAssets.Save(result, generator);
                Debug.Log($"Procedural: {mode} done, invariants {(result.Passed ? "passed" : "FAILED")}; {saved.Report}");
                code = result.Passed ? 0 : 2;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(code);
        }

        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}

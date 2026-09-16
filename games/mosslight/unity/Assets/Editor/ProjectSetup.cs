using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mosslight.Editor
{
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Mosslight.unity";

        [MenuItem("Mosslight/Rebuild demo scene")]
        public static void Create()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string name in new[] { "Level", "Character", "Crystal" })
            {
                string path = "Assets/Generated/" + name + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new Exception("Generate Blender assets first: " + path);
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
            }
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Generated/Materials");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var level = Instantiate("Level");
            foreach (MeshFilter mesh in level.GetComponentsInChildren<MeshFilter>())
            {
                if (!mesh.name.StartsWith("FloatingRock"))
                    mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
            }
            var root = new GameObject("Player");
            root.layer = 2;
            root.transform.position = new Vector3(0, 0.3f, -6);
            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0, 0.9f, 0);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 48;
            var character = Instantiate("Character");
            character.transform.SetParent(root.transform, false);
            foreach (Transform child in character.GetComponentsInChildren<Transform>()) child.gameObject.layer = 2;
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = 55;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 180;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.48f, 0.67f, 0.69f);
            cam.gameObject.AddComponent<AudioListener>();
            var sun = new GameObject("Afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);
            sun.color = new Color(1, 0.88f, 0.70f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.65f, 0.76f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.49f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.19f, 0.25f, 0.25f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = cam.backgroundColor;
            RenderSettings.fogStartDistance = 32;
            RenderSettings.fogEndDistance = 90;
            QualitySettings.shadowDistance = 60;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.antiAliasing = 4;
            var portal = new GameObject("Gate light").AddComponent<Light>();
            portal.type = LightType.Point;
            portal.range = 8;
            portal.color = new Color(0.3f, 1, 0.7f);
            portal.transform.position = new Vector3(0, 2.5f, -1);
            var game = new GameObject("Game").AddComponent<MosslightGame>();
            game.player = root.transform;
            game.visual = character.transform;
            game.gameCamera = cam;
            game.portalLight = portal;
            Vector3[] positions = { new Vector3(8, 1.3f, 8), new Vector3(-8, 1.3f, 8),
                new Vector3(0, 1.3f, -9), new Vector3(9, 1.3f, -7), new Vector3(-9, 1.3f, -7) };
            game.crystals = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var item = Instantiate("Crystal");
                item.name = "Light " + (i + 1);
                item.transform.position = positions[i];
                game.crystals[i] = item.transform;
                var light = item.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.3f, 1, 0.7f);
                light.intensity = 1.3f;
                light.range = 3;
            }
            foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = ConvertMaterial(materials[i]);
                renderer.sharedMaterials = materials;
            }
            PlayerSettings.companyName = "Bonfire";
            PlayerSettings.productName = "Mosslight";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64, new[] { GraphicsDeviceType.OpenGLCore });
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("MOSSLIGHT_PREPARED " + ScenePath);
        }

        private static GameObject Instantiate(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Generated/" + name + ".fbx");
            if (prefab == null) throw new Exception("Missing Blender export " + name);
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }

        private static Material ConvertMaterial(Material source)
        {
            string name = source == null ? "Default" : source.name;
            string path = "Assets/Generated/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(Shader.Find("Standard"));
            mat.name = name;
            // FBX exports surface names reliably; use the authored palette explicitly.
            mat.color = Palette(name);
            mat.SetFloat("_Glossiness", 0.15f);
            if (name.Contains("Glow"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.3f, 0.85f, 0.65f));
            }
            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Color Palette(string name)
        {
            switch (name)
            {
                case "Moss": return new Color(0.24f, 0.42f, 0.29f);
                case "Stone": return new Color(0.19f, 0.29f, 0.32f);
                case "Path": return new Color(0.66f, 0.69f, 0.53f);
                case "Trunk": return new Color(0.30f, 0.22f, 0.17f);
                case "Leaf": return new Color(0.12f, 0.32f, 0.26f);
                case "LeafLight": return new Color(0.27f, 0.51f, 0.34f);
                case "Suit": return new Color(0.94f, 0.48f, 0.18f);
                case "Visor": return new Color(0.07f, 0.20f, 0.25f);
                case "Cream": return new Color(0.91f, 0.90f, 0.72f);
                case "Glow": return new Color(0.40f, 0.94f, 0.81f);
                default: throw new Exception("Unknown Blender surface: " + name);
            }
        }

        [MenuItem("Mosslight/Validate scene")]
        public static void Validate()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<MosslightGame>();
            if (game == null || game.player == null || game.crystals.Length != 5) throw new Exception("Invalid game wiring");
            var bounds = new Bounds(game.visual.position, Vector3.zero);
            foreach (Renderer r in game.visual.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            if (bounds.size.y < 1.5f || bounds.size.y > 2.5f) throw new Exception("Character FBX scale incorrect: " + bounds.size);
            if (!Physics.Raycast(new Vector3(0, 5, -6), Vector3.down, out _, 6)) throw new Exception("Missing spawn floor collision");
            Debug.Log("MOSSLIGHT_VALIDATE character=" + bounds.size + " crystals=5");
        }

        public static void Build()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Validate();
            string output = Path.GetFullPath("../artifacts/build/Mosslight.x86_64");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneLinux64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + report.summary.result);
            Debug.Log("MOSSLIGHT_BUILD " + output);
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// A gallery of the Mixamo clips (dev.bonfirelit.mixamo) played on one of
    /// the game's rigged characters, as the game plays them (Generic, bone for
    /// bone; the hips' travel is root motion, so a clip plays in place):
    /// frames in &lt;out&gt;/frames/&lt;pack&gt;/&lt;clip&gt;/, by default the package's
    /// Gallery~. Renders in a scene of its own, then reopens the scene that
    /// was open. The editor runs in the Hub's environment, without ffmpeg:
    /// tools/encode_clips.sh &lt;out&gt; then makes the frames
    /// &lt;out&gt;/clips/&lt;pack&gt;/&lt;clip&gt;.webm and the package's Tools~/gallery.py
    /// the page of all its clips.
    ///
    ///     ClipGallery.Render("EnemyAlluringSuccubusDemoness", "/path/to/animations/mixamo/Gallery~");
    /// </summary>
    public static class ClipGallery
    {
        const string Package = "Packages/dev.bonfirelit.mixamo";
        const int Size = 320, Fps = 15;
        /// <summary>Kept through the unloading of unused assets between clips.</summary>
        const HideFlags Keep = HideFlags.DontUnloadUnusedAsset | HideFlags.HideAndDontSave;
        /// <summary>The run stops with less than this free (MB): a full run
        /// beside the studio's image model once froze the laptop.</summary>
        const int LeastFreeMemory = 3000;

        [MenuItem("Soulslike/Render Clip Gallery")]
        static void FromMenu() =>
            Render("EnemyAlluringSuccubusDemoness",
                Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../../animations/mixamo/Gallery~")));

        public static string Render(string character, string output, string only = "")
        {
            var reopen = EditorSceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(reopen) && EditorBuildSettings.scenes.Length > 0) reopen = EditorBuildSettings.scenes[0].path;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Soulslike/Art/Characters/{character}.fbx");
            if (model == null) return $"no character {character}";
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var body = (GameObject)Object.Instantiate(model);
            var hips = body.GetComponentsInChildren<Transform>().First(t => t.name == "mixamorig:Hips");
            var root = hips.parent.gameObject; // the clips' paths start at the hips
            var bounds = Bounds(body);
            // Outside play mode a SkinnedMeshRenderer is not re-skinned for a
            // camera's Render: the sampled bones would move nothing but the
            // whole body. Each frame bakes the skinned meshes into stand-ins.
            var skins = body.GetComponentsInChildren<SkinnedMeshRenderer>();
            var baked = new List<(SkinnedMeshRenderer skin, Transform proxy, Mesh mesh)>();
            foreach (var skin in skins)
            {
                var proxy = new GameObject(skin.name + " (baked)");
                var mesh = new Mesh { hideFlags = Keep };
                proxy.AddComponent<MeshFilter>().sharedMesh = mesh;
                proxy.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                baked.Add((skin, proxy.transform, mesh));
                skin.enabled = false;
            }
            void Bake()
            {
                foreach (var (skin, proxy, mesh) in baked)
                {
                    skin.BakeMesh(mesh, true);
                    proxy.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                }
            }
            float height = bounds.size.y;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.58f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.4f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.2f);
            RenderSettings.fog = false;
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 2f;
            key.color = new Color(1f, 0.95f, 0.88f);
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * height * 2f;
            var floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = Keep };
            floorMaterial.SetColor("_BaseColor", new Color(0.25f, 0.27f, 0.26f));
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.1f, 0.1f);
            camera.fieldOfView = 30f;
            var target = new RenderTexture(Size, Size, 24) { antiAliasing = 4, hideFlags = Keep };
            camera.targetTexture = target;
            var view = new Vector3(0.55f, 0.35f, 1f).normalized; // front three-quarter, a little above
            var pixels = new Texture2D(Size, Size, TextureFormat.RGB24, false) { hideFlags = Keep };

            var clips = new List<(string pack, string name, string file, float seconds)>();
            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { Package });
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath).Distinct().Where(p => p.Contains(only)).OrderBy(p => p).ToList();
            string stopped = null;
            for (int n = 0; n < paths.Count; n++)
            {
                if (FreeMemory() is { } free && free < LeastFreeMemory)
                {
                    stopped = $"stopped at {n}/{paths.Count}: {free} MB free; run again to go on";
                    break;
                }
                var path = paths[n];
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip == null) continue;
                var pack = Path.GetFileName(Path.GetDirectoryName(path));
                var name = Path.GetFileNameWithoutExtension(path);
                if (EditorUtility.DisplayCancelableProgressBar("Clip gallery", $"{pack}/{name}", n / (float)paths.Count)) break;
                var temp = Path.Combine(output, "frames", pack, name);
                int frames = Mathf.Max(1, Mathf.CeilToInt(clip.length * Fps));
                // A clip rendered (all its frames) or encoded by an earlier run
                // is kept: a run cut short goes on where it stopped.
                bool encoded = File.Exists(Path.Combine(output, "clips", pack, name + ".webm"));
                if (encoded || (Directory.Exists(temp) && Directory.GetFiles(temp, "*.png").Length == frames + 1))
                {
                    clips.Add((pack, name, $"clips/{pack}/{name}.webm", clip.length));
                    continue;
                }
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
                Directory.CreateDirectory(temp);
                // The floor under the feet and the camera's height from the
                // bones on the first frame: the clip sets the hips' height, not
                // the model's rest pose.
                clip.SampleAnimation(root, 0f);
                float feet = Mathf.Min(Bone(body, "LeftToeBase").position.y, Bone(body, "RightToeBase").position.y,
                    Bone(body, "LeftFoot").position.y, Bone(body, "RightFoot").position.y);
                float middle = feet + height / 2f;
                floor.transform.position = new Vector3(hips.position.x, feet, hips.position.z);
                for (int f = 0; f <= frames; f++)
                {
                    clip.SampleAnimation(root, Mathf.Min(f / (float)Fps, clip.length));
                    Bake();
                    var at = new Vector3(hips.position.x, middle, hips.position.z);
                    camera.transform.position = at + view * height * 2.7f;
                    camera.transform.LookAt(at);
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(temp, $"{f:0000}.png"), pixels.EncodeToPNG());
                }
                clips.Add((pack, name, $"clips/{pack}/{name}.webm", clip.length));
                // Every clip's model file stays loaded otherwise: a full run
                // grew the editor until the laptop ran out of memory.
                if (n % 20 == 19) EditorUtility.UnloadUnusedAssetsImmediate();
            }
            EditorUtility.ClearProgressBar();
            camera.targetTexture = null;
            target.Release();
            foreach (var (_, _, mesh) in baked) Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(floorMaterial);
            if (!string.IsNullOrEmpty(reopen)) EditorSceneManager.OpenScene(reopen);
            return (stopped != null ? stopped + "; " : "") + $"{clips.Count} clips: {output}";
        }

        /// <summary>MemAvailable from /proc/meminfo, MB (null where there is none).</summary>
        static int? FreeMemory()
        {
            try
            {
                foreach (var line in File.ReadLines("/proc/meminfo"))
                    if (line.StartsWith("MemAvailable:"))
                        return int.Parse(line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries)[1]) / 1024;
            }
            catch (IOException) { }
            return null;
        }

        static Transform Bone(GameObject body, string name) =>
            body.GetComponentsInChildren<Transform>().First(t => t.name == "mixamorig:" + name);

        static Bounds Bounds(GameObject body)
        {
            var renderers = body.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }
    }
}

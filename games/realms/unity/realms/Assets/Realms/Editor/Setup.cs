using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Realms.Editor
{
    /// <summary>
    /// Creates what the map scene needs from the scripts, shaders and art in the project: the
    /// materials, prefabs for the studio models and the art library, the post-processing profile,
    /// the UI panel settings, the scene and the player settings. Everything it writes lives in
    /// Assets/Realms/Generated and Assets/Realms/Art, which Git ignores; run it again after
    /// changing a shader's defaults or the art. The build runs it first.
    /// </summary>
    public static class Setup
    {
        public const string Root = "Assets/Realms";
        public const string Generated = Root + "/Generated";
        public const string ScenePath = Generated + "/Map.unity";
        const string Art = Root + "/Art";
        const string PipelinePath = "Assets/Settings/PC_RPAsset.asset";

        [MenuItem("Realms/Setup")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Realms: stop Play Mode before running Setup.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Realms: save or discard open scene changes before running Setup.");

            Folder(Generated);
            Pipeline();
            var terrain = MaterialAt(Generated + "/Terrain.mat", "Realms/Terrain");
            var water = MaterialAt(Generated + "/Water.mat", "Realms/Water");
            var river = MaterialAt(Generated + "/River.mat", "Realms/River");
            var prop = MaterialAt(Generated + "/Prop.mat", "Realms/Prop");
            var fog = MaterialAt(Generated + "/Fog.mat", "Realms/Fog");
            var flag = MaterialAt(Generated + "/Flag.mat", "Realms/Flag");
            var smoke = MaterialAt(Generated + "/Smoke.mat", "Realms/Smoke");
            var clouds = MaterialAt(Generated + "/Clouds.mat", "Realms/Clouds");
            prop.SetFloat("_UseVertexColor", 1f);
            prop.SetFloat("_Sway", 1f);
            prop.SetFloat("_Rim", 0.2f);
            var ground = GroundArray();
            foreach (var material in new[] { terrain, prop })
            {
                material.SetTexture("_Ground", ground);
                if (ground == null)
                    material.SetFloat("_GroundDetail", 0f);
            }

            int models = 0, icons = 0;
            if (AssetDatabase.IsValidFolder(Art + "/Models"))
                ArtAssets(out models, out icons);
            else
                Debug.LogWarning("Realms: no Assets/Realms/Art; run `just realms fetch-art` for the models and icons. The map uses simple stand-ins.");

            var profile = VolumeProfileAt(Generated + "/MapVolume.asset");
            var panel = PanelAt(Generated + "/RealmsPanel.asset");
            BuildScene(terrain, water, river, prop, fog, flag, smoke, clouds, profile, panel);
            Player();
            AssetDatabase.SaveAssets();
            Debug.Log($"Realms: setup done; {models} models, {icons} icons, scene {ScenePath}.");
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // MSAA for the soft low-poly edges, HDR for the bloom on borders and the selection ring
        // (while the post-processing is off, see BuildScene, they just show at full brightness),
        // the depth texture for the water. MapCamera adjusts the shadow distance to the zoom.
        static void Pipeline()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null)
                throw new FileNotFoundException("Realms: no URP asset at " + PipelinePath);
            asset.msaaSampleCount = 4;
            asset.renderScale = 1f;
            asset.supportsHDR = true;
            asset.supportsCameraDepthTexture = true;
            asset.shadowDistance = 60f;
            asset.shadowCascadeCount = 4;
            EditorUtility.SetDirty(asset);

            // No contact shade (SSAO) for now, like the post-processing (see BuildScene): the
            // players saw the picture tremble in places and want it without screen-wide effects.
            // The feature is taken out of the renderer, not switched off: a build strips the
            // resources of a switched-off SSAO, URP then cannot create the renderer at all and the
            // game opens on a black screen. When it comes back, it was a contact shade at the feet
            // of houses, walls and trees (the sun stands behind the camera, so their shadows fall
            // behind them): After Opaque, since the Realms shaders do their own light (the fog,
            // water and clouds come after it), intensity 1, radius 0.15, falloff 100.
            foreach (ScriptableRendererData data in asset.rendererDataList)
            {
                if (data == null)
                    continue;
                var serialized = new SerializedObject(data);
                SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
                SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
                for (int i = features.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty element = features.GetArrayElementAtIndex(i);
                    if (element.objectReferenceValue is not ScreenSpaceAmbientOcclusion feature)
                        continue;
                    // As URP's own renderer editor removes a feature: clear the reference, drop
                    // the entry and its map slot, then destroy the sub-asset.
                    element.objectReferenceValue = null;
                    features.DeleteArrayElementAtIndex(i);
                    if (i < map.arraySize)
                        map.DeleteArrayElementAtIndex(i);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.RemoveObjectFromAsset(feature);
                    UnityEngine.Object.DestroyImmediate(feature, true);
                    EditorUtility.SetDirty(data);
                }
            }
        }

        // The shader's defaults are the material's values: an existing material is reset to them
        // (keeping its GUID), so tuning happens in the shader files.
        static Material MaterialAt(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"Realms: shader {shaderName} is missing; look for shader errors in the console.");
            return Store(new Material(shader), path);
        }

        static Material Store(Material material, string path)
        {
            material.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(material, path);
                return material;
            }
            EditorUtility.CopySerialized(material, existing);
            UnityEngine.Object.DestroyImmediate(material);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ----- Ground textures -----

        const string GroundPath = Generated + "/Ground.png";

        /// <summary>
        /// The ground textures of Art/Textures (<see cref="GroundLayer.Names"/>) as one texture array
        /// for the terrain and the stone props, or null without the ground's own layers. Each layer
        /// is divided by its own mean colour, its colour differences softened, so the palette keeps
        /// the colour and the texture brings only its pattern; stored at half scale (1 → 0.5) in
        /// linear values, with the brightness as height in alpha. A layer as wide as the others but
        /// a whole fraction of their height (the road strip) is repeated down the layer. Without the
        /// cobbles or the road the layer is plain, and the squares and roads take their colours
        /// only. Written as a strip of layers that imports as an array.
        /// </summary>
        static Texture2DArray GroundArray()
        {
            int count = GroundLayer.ArrayCount, size = 0;
            Color32[] strip = null;
            var toLinear = new float[256];
            for (int v = 0; v < 256; v++)
                toLinear[v] = Mathf.GammaToLinearSpace(v / 255f);
            for (int layer = 0; layer < count; layer++)
            {
                // The first layer at the top of the strip, where the importer starts.
                int offset = (count - 1 - layer) * size * size;
                string path = $"{Art}/Textures/{GroundLayer.Names[layer]}.png";
                if (!File.Exists(path))
                {
                    if (layer < GroundLayer.Count)
                    {
                        Debug.LogWarning($"Realms: no {path}; run `just realms fetch-art`. The ground stays untextured.");
                        return null;
                    }
                    Debug.LogWarning($"Realms: no {path}; run `just realms fetch-art`. Its layer stays plain.");
                    for (int i = 0; i < size * size; i++)
                        strip[offset + i] = new Color32(Half(1f), Half(1f), Half(1f), Half(1f));
                    continue;
                }
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                source.LoadImage(File.ReadAllBytes(path));
                if (strip == null)
                {
                    size = source.width;
                    strip = new Color32[size * size * count];
                    offset = (count - 1 - layer) * size * size;
                }
                int height = source.height;
                if (source.width != size || height > size || size % height != 0)
                    throw new InvalidDataException($"Realms: {path} is {source.width}x{height}; the ground textures must be {size} wide and {size} high or a whole fraction of it.");
                Color32[] rows = source.GetPixels32();
                UnityEngine.Object.DestroyImmediate(source);
                var pixels = new Color32[size * size];
                for (int copy = 0; copy < size / height; copy++)
                    Array.Copy(rows, 0, pixels, copy * rows.Length, rows.Length);

                var mean = new double[3];
                foreach (Color32 c in pixels)
                {
                    mean[0] += toLinear[c.r];
                    mean[1] += toLinear[c.g];
                    mean[2] += toLinear[c.b];
                }
                for (int k = 0; k < 3; k++)
                    mean[k] /= pixels.Length;
                double meanY = 0.2126 * mean[0] + 0.7152 * mean[1] + 0.0722 * mean[2];
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 c = pixels[i];
                    float r = toLinear[c.r], g = toLinear[c.g], b = toLinear[c.b];
                    float y = (float)((0.2126 * r + 0.7152 * g + 0.0722 * b) / meanY);
                    strip[offset + i] = new Color32(Detail(r, mean[0], y), Detail(g, mean[1], y), Detail(b, mean[2], y), Half(y));
                }
            }

            // Encoded straight from the pixels: headless Unity makes no texture taller than 8192.
            byte[] png = ImageConversion.EncodeArrayToPNG(strip, GraphicsFormat.R8G8B8A8_UNorm, (uint)size, (uint)(size * count));
            if (!File.Exists(GroundPath) || !File.ReadAllBytes(GroundPath).SequenceEqual(png))
            {
                File.WriteAllBytes(GroundPath, png);
                AssetDatabase.ImportAsset(GroundPath, ImportAssetOptions.ForceUpdate);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(GroundPath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool ready = importer.textureShape == TextureImporterShape.Texture2DArray
                && settings.flipbookRows == count && settings.flipbookColumns == 1
                && !importer.sRGBTexture && importer.mipmapEnabled && !importer.alphaIsTransparency
                && importer.wrapMode == TextureWrapMode.Repeat && importer.filterMode == FilterMode.Trilinear
                && importer.anisoLevel == 4 && importer.maxTextureSize == 16384
                && importer.textureCompression == TextureImporterCompression.Compressed;
            if (!ready)
            {
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2DArray;
                importer.ReadTextureSettings(settings);
                settings.flipbookRows = count;
                settings.flipbookColumns = 1;
                importer.SetTextureSettings(settings);
                importer.sRGBTexture = false;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.maxTextureSize = 16384;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            var array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(GroundPath);
            if (array == null || array.depth != count)
                throw new InvalidDataException($"Realms: {GroundPath} did not import as an array of {count} layers.");
            return array;
        }

        // A channel against its mean: the brightness kept, the colour's own variation softened (the
        // small constant keeps near-empty channels, such as grass's blue, from flaring).
        static byte Detail(float value, double mean, float brightness)
        {
            float colour = (float)((value + 0.03) / (mean + 0.03)) / Mathf.Max(brightness, 1e-3f);
            return Half(brightness * Mathf.Pow(Mathf.Max(colour, 0f), 0.5f));
        }

        static byte Half(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt(value * 0.5f * 255f), 0, 255);

        // ----- Studio art -----

        static void ArtAssets(out int modelCount, out int iconCount)
        {
            Folder(Art + "/Prefabs");
            Folder(Art + "/Materials");
            Folder(Art + "/Resources");
            string libraryPath = Art + "/Resources/RealmsArt.asset";
            var library = AssetDatabase.LoadAssetAtPath<ArtLibrary>(libraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ArtLibrary>();
                AssetDatabase.CreateAsset(library, libraryPath);
            }
            library.models.Clear();
            library.icons.Clear();

            var shader = Shader.Find("Realms/Prop");
            foreach (string file in Directory.GetFiles(Art + "/Models", "*.glb").OrderBy(f => f, StringComparer.Ordinal))
            {
                string path = file.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(path);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (source == null)
                {
                    Debug.LogWarning($"Realms: {path} did not import as a model; skipped.");
                    continue;
                }
                var prefab = ModelPrefab(name, source, shader);
                if (prefab != null)
                    library.models.Add(new ArtLibrary.Model { name = name, prefab = prefab });
            }

            if (AssetDatabase.IsValidFolder(Art + "/Icons"))
                foreach (string file in Directory.GetFiles(Art + "/Icons", "*.png").OrderBy(f => f, StringComparer.Ordinal))
                {
                    string path = file.Replace('\\', '/');
                    IconImport(path);
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null)
                        library.icons.Add(new ArtLibrary.Icon { name = Path.GetFileNameWithoutExtension(path), texture = texture });
                }

            // The interface's own painted art: Hud.uss nine-slices the frames and buttons, the
            // icons, the crown's gems and the main menu's picture and crown are also in the library by name.
            string ui = Root + "/UI/Art";
            if (AssetDatabase.IsValidFolder(ui))
                foreach (string file in Directory.GetFiles(ui, "*.png").OrderBy(f => f, StringComparer.Ordinal))
                {
                    string path = file.Replace('\\', '/');
                    IconImport(path);
                    string name = Path.GetFileNameWithoutExtension(path);
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null && (name.StartsWith("icon_", StringComparison.Ordinal) || name.StartsWith("gem_", StringComparison.Ordinal)
                        || name.StartsWith("title_", StringComparison.Ordinal)))
                        library.icons.Add(new ArtLibrary.Icon { name = name, texture = texture });
                }

            // The flags' emblems: white silhouettes whose alpha FlagArt reads, so they stay readable.
            string emblems = ui + "/Emblems";
            if (AssetDatabase.IsValidFolder(emblems))
                foreach (string file in Directory.GetFiles(emblems, "emblem_*.png").OrderBy(f => f, StringComparer.Ordinal))
                {
                    string path = file.Replace('\\', '/');
                    IconImport(path, true);
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null)
                        library.icons.Add(new ArtLibrary.Icon { name = Path.GetFileNameWithoutExtension(path), texture = texture });
                }

            EditorUtility.SetDirty(library);
            modelCount = library.models.Count;
            iconCount = library.icons.Count;
        }

        // A prefab whose root sits on the ground at the model's bottom centre, the largest
        // dimension one unit long, facing the camera (-Z; glTF models face +Z), with the model's
        // materials converted to Realms/Prop so they share the map's light.
        static GameObject ModelPrefab(string name, GameObject source, Shader shader)
        {
            var root = new GameObject(name);
            var model = UnityEngine.Object.Instantiate(source);
            model.name = "model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model.transform.localScale = Vector3.one;

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                UnityEngine.Object.DestroyImmediate(root);
                Debug.LogWarning($"Realms: {name} has no renderers; skipped.");
                return null;
            }
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = largest > 1e-5f ? 1f / largest : 1f;
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;

            var converted = new Dictionary<Material, Material>();
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null)
                        continue;
                    if (!converted.TryGetValue(materials[i], out var material))
                    {
                        material = PropMaterial(materials[i], shader, $"{Art}/Materials/{name}_{converted.Count}.mat",
                            PropBuilder.IsTree(name) ? TreeSaturation : 1f);
                        converted[materials[i]] = material;
                    }
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            Footing(name, root);
            if (Array.IndexOf(PropBuilder.ScatterModels, name) >= 0)
                Flatten(name, root);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Art}/Prefabs/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // Where the model meets the ground (ModelFooting, for ContactShade): the box round its
        // vertices in the lowest twentieth of its height, in the root's space, leaving out the
        // outermost few in each direction (a stray stone or blade), and its height. Most studio
        // models stand on a plinth or a ground slab of their own, so this is their foot; a tree's
        // is its roots or its lowest boughs.
        static void Footing(string name, GameObject root)
        {
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
            var points = new List<Vector3>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;
                Matrix4x4 m = toRoot * filter.transform.localToWorldMatrix;
                using (Mesh.MeshDataArray data = Mesh.AcquireReadOnlyMeshData(mesh))
                {
                    var vertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp);
                    data[0].GetVertices(vertices);
                    foreach (Vector3 v in vertices)
                        points.Add(m.MultiplyPoint3x4(v));
                    vertices.Dispose();
                }
            }
            if (points.Count == 0)
                return;
            float top = points.Max(p => p.y);
            var xs = new List<float>();
            var zs = new List<float>();
            foreach (Vector3 p in points)
            {
                if (p.y <= 0.05f * top)
                {
                    xs.Add(p.x);
                    zs.Add(p.z);
                }
            }
            if (xs.Count == 0 || top <= 1e-4f)
            {
                Debug.LogWarning($"Realms: {name} has nothing near the ground; no shade at its foot.");
                return;
            }
            xs.Sort();
            zs.Sort();
            int a = Mathf.FloorToInt((xs.Count - 1) * 0.02f), b = Mathf.CeilToInt((xs.Count - 1) * 0.98f);
            var footing = root.AddComponent<ModelFooting>();
            footing.centre = new Vector2(xs[a] + xs[b], zs[a] + zs[b]) * 0.5f;
            footing.half = new Vector2(xs[b] - xs[a], zs[b] - zs[a]) * 0.5f;
            footing.height = top;
        }

        // A model the map scatters by the hundred (PropBuilder merges its copies at run time): one
        // readable mesh on the prefab's root, in the root's space, with its one material.
        static void Flatten(string name, GameObject root)
        {
            Material material = null;
            var parts = new List<CombineInstance>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null)
                    continue;
                var materials = renderer.sharedMaterials;
                for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                {
                    var own = materials[Mathf.Min(sub, materials.Length - 1)];
                    if (material != null && own != material)
                    {
                        Debug.LogWarning($"Realms: {name} has more than one material; the map places it whole.");
                        return;
                    }
                    material = own;
                    parts.Add(new CombineInstance
                    {
                        mesh = filter.sharedMesh,
                        subMeshIndex = sub,
                        transform = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix,
                    });
                }
            }
            if (parts.Count == 0 || material == null)
                return;
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            Folder(Art + "/Meshes");
            string path = $"{Art}/Meshes/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var merged = root.AddComponent<MeshRenderer>();
            merged.sharedMaterial = material;
            merged.shadowCastingMode = ShadowCastingMode.On;
            merged.receiveShadows = true;
            Debug.Log($"Realms: {name} merged for scattering ({mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles).");
        }

        /// <summary>The trees' paint a little calmer than the buildings', so the woods stay behind the towns.</summary>
        const float TreeSaturation = 0.9f;

        static Material PropMaterial(Material source, Shader shader, string path, float saturation)
        {
            var material = new Material(shader);
            foreach (string property in new[] { "baseColorTexture", "_BaseMap", "_MainTex" })
            {
                if (!source.HasProperty(property) || source.GetTexture(property) == null)
                    continue;
                material.SetTexture("_BaseMap", source.GetTexture(property));
                material.SetTextureScale("_BaseMap", source.GetTextureScale(property));
                material.SetTextureOffset("_BaseMap", source.GetTextureOffset(property));
                break;
            }
            foreach (string property in new[] { "baseColorFactor", "_BaseColor", "_Color" })
            {
                if (!source.HasProperty(property))
                    continue;
                material.SetColor("_BaseColor", source.GetColor(property));
                break;
            }
            // A normal map baked from a denser surface (Studio's 3d-bake-normals). glTF stores it as
            // raw values; if the importer made it an sRGB texture, the shader undoes the conversion.
            foreach (string property in new[] { "normalTexture", "_BumpMap", "_NormalMap" })
            {
                if (!source.HasProperty(property) || source.GetTexture(property) is not Texture2D normals)
                    continue;
                bool srgb = GraphicsFormatUtility.IsSRGBFormat(normals.graphicsFormat);
                material.SetTexture("_NormalMap", normals);
                material.SetFloat("_UseNormalMap", 1f);
                material.SetFloat("_NormalSRGB", srgb ? 1f : 0f);
                string scale = property + "_scale";
                material.SetFloat("_NormalScale", source.HasProperty(scale) ? source.GetFloat(scale) : 1f);
                Debug.Log($"Realms: {Path.GetFileNameWithoutExtension(path)} has a normal map ({normals.width} px, {(srgb ? "sRGB" : "linear")}).");
                break;
            }
            material.SetFloat("_UseVertexColor", 0f);
            material.SetFloat("_Saturation", saturation);
            material.SetFloat("_Rim", 0.25f);
            return Store(material, path);
        }

        // Icons are drawn small by UI Toolkit: uncompressed, clamped, with mipmaps against shimmer.
        // Readable ones keep a copy in memory for the scripts (the emblems).
        static void IconImport(string path, bool readable = false)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return;
            bool ready = importer.textureType == TextureImporterType.Default
                && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.mipmapEnabled && importer.alphaIsTransparency
                && importer.wrapMode == TextureWrapMode.Clamp
                && importer.filterMode == FilterMode.Trilinear
                && importer.npotScale == TextureImporterNPOTScale.None
                && importer.isReadable == readable;
            if (ready)
                return;
            importer.textureType = TextureImporterType.Default;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = readable;
            importer.SaveAndReimport();
        }

        // ----- Look and interface -----

        // No tonemapping and no extra saturation (palette colours show as chosen), bloom only
        // for what goes above white (border glow, the selection ring), a soft vignette.
        static VolumeProfile VolumeProfileAt(string path)
        {
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.4f);
            bloom.scatter.Override(0.7f);
            var color = profile.Add<ColorAdjustments>();
            color.saturation.Override(0f);
            color.contrast.Override(5f);
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.45f);
            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.None);

            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static PanelSettings PanelAt(string path)
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, path);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "/UI/RealmsTheme.tss");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        // ----- Scene and player -----

        static void BuildScene(Material terrain, Material water, Material river, Material prop, Material fog, Material flag,
            Material smoke, Material clouds, VolumeProfile profile, PanelSettings panel)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 24f, -14f), Quaternion.Euler(58f, 0f, 0f));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Hex("#34383E");
            camera.fieldOfView = 40f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            // No post-processing for now: no bloom, map vignette or blur of the town and hero
            // screens (ScreenLook). The players saw the picture tremble in places and want it
            // without screen-wide effects; the volume stays for when they come back.
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = true;
            cameraData.requiresDepthOption = CameraOverrideOption.On;
            var mapCamera = cameraObject.AddComponent<MapCamera>();

            // From behind the camera's left shoulder: lit faces turn to the player, shadows fall
            // back and to the right.
            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Color.white;
            sun.intensity = 1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            sun.transform.rotation = Quaternion.Euler(52f, 28f, 0f);
            sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.78f);
            RenderSettings.fog = false;

            var post = new GameObject("Post").AddComponent<Volume>();
            post.isGlobal = true;
            post.priority = 1f;
            post.sharedProfile = profile;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var ui = new GameObject("UI", typeof(UIDocument));
            ui.GetComponent<UIDocument>().panelSettings = panel;
            var hud = ui.AddComponent<Hud>();
            hud.style = AssetDatabase.LoadAssetAtPath<StyleSheet>(Root + "/UI/Hud.uss");
            hud.regular = AssetDatabase.LoadAssetAtPath<Font>(Root + "/UI/Fonts/Nunito-SemiBold.ttf");
            hud.bold = AssetDatabase.LoadAssetAtPath<Font>(Root + "/UI/Fonts/Nunito-ExtraBold.ttf");
            hud.black = AssetDatabase.LoadAssetAtPath<Font>(Root + "/UI/Fonts/Nunito-Black.ttf");
            if (hud.style == null || hud.regular == null || hud.bold == null || hud.black == null)
                throw new FileNotFoundException("Realms: Hud.uss or a Nunito font is missing under Assets/Realms/UI.");

            var game = new GameObject("Game");
            var view = game.AddComponent<MapView>();
            view.terrainMaterial = terrain;
            view.waterMaterial = water;
            view.riverMaterial = river;
            view.propMaterial = prop;
            view.fogMaterial = fog;
            view.flagMaterial = flag;
            view.smokeMaterial = smoke;
            view.cloudMaterial = clouds;
            var realms = game.AddComponent<RealmsGame>();
            realms.view = view;
            realms.mapCamera = mapCamera;
            realms.hud = hud;

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Realms: could not save " + ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void Player()
        {
            PlayerSettings.companyName = "Realms";
            PlayerSettings.productName = "Realms";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.show = false;
        }
    }
}

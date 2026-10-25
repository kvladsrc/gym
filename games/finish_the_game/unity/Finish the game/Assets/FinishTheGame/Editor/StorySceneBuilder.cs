using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FinishTheGame.Editor
{
    /// <summary>Finish the Game → Build Loop / Plane / Crystal Scene: a scene
    /// with the sun, the camera, the hero and the world (built when it plays),
    /// made anew. The loop bends onto the ribbon (LoopWorld); the plane is seen
    /// through a band along its route, the way ahead on the horizon
    /// (PlaneWorld); or as a crystal in a liquid that flows onto the ribbon
    /// (CrystalWorld).</summary>
    public static class StorySceneBuilder
    {
        const string Folder = "Assets/FinishTheGame";

        [MenuItem("Finish the Game/Build Loop Scene")]
        public static void BuildLoop()
        {
            var (world, ribbon, view) = Begin<LoopWorld>(Material("Ribbon", "FinishTheGame/Ribbon Lit"), 0.006f);
            world.ribbon = ribbon;
            Save("Loop");
        }

        [MenuItem("Finish the Game/Build Plane Scene")]
        public static void BuildPlane()
        {
            var (world, ribbon, view) = Begin<PlaneWorld>(Material("Band", "FinishTheGame/Band Lit"), 0.004f);
            view.pitch = 16f;
            view.height = 12f;
            ribbon.flat = 40f;
            ribbon.arc = 60f;
            ribbon.rise = 75f;
            ribbon.riseLength = 25f;
            ribbon.curl = 90f;
            ribbon.straighten = new Vector2(15f, 35f);
            var horizon = new GameObject("Horizon").AddComponent<HorizonRibbon>();
            horizon.world = world;
            horizon.ribbon = ribbon;
            horizon.material = Material("Horizon", "FinishTheGame/Horizon");
            world.ribbon = ribbon;
            world.horizon = horizon;
            Save("Plane");
        }

        [MenuItem("Finish the Game/Build Crystal Scene")]
        public static void BuildCrystal()
        {
            var (world, ribbon, view) = Begin<CrystalWorld>(Material("Band", "FinishTheGame/Band Lit"), 0.0004f);
            view.orbit = true;
            // Over the shoulder, turned by the mouse, as in any third-person game;
            // the hero walks by WASD the camera's way.
            view.pitch = 8f;
            view.height = 1.7f;
            view.distance = 5f;
            view.GetComponent<Camera>().farClipPlane = 5000f;
            view.hero.inFrame = false;
            view.hero.look = Material("Ribbon", "FinishTheGame/Ribbon Lit");
            // The studio's hero, rigged, if there is one: matte, as Paint baked his light.
            HeroAnimation.ExtractTextures();
            string rigPath = ArtFolder + "/Characters/hero.fbx";
            view.hero.rig = AssetDatabase.LoadAssetAtPath<GameObject>(rigPath);
            if (view.hero.rig != null)
            {
                view.hero.gaits = HeroAnimation.Build();
                var skin = Material("Hero", "Universal Render Pipeline/Simple Lit");
                skin.SetFloat("_Smoothness", 0.1f);
                EditorUtility.SetDirty(skin);
                view.hero.look = skin;
                // A walking pace, as in the old role-playing games: the run plays at its own speed.
                view.hero.speed = 7f;
            }
            // One world, all of it, under the evening sky, its far land in the
            // haze of the horizon's glow.
            RenderSettings.skybox = Material("Space", "FinishTheGame/Space");
            RenderSettings.fogColor = new Color(0.66f, 0.55f, 0.5f);
            RenderSettings.fogDensity = 0.0011f;
            view.GetComponent<Camera>().clearFlags = CameraClearFlags.Skybox;
            // The story's strip 100 m wide along the route; one may walk 40 m off the road.
            world.halfWidth = 50f;
            world.sideFade = 6f;
            world.roam = 40f;
            world.behind = 60f;
            world.navigation = false;
            world.texel = 0.5f;
            world.ribbon = ribbon;
            world.stripMaterial = Material("Strip", "FinishTheGame/Strip Lit");
            // All the world drawn, far things simplified (their mesh LODs).
            world.drawDistance = 2500f;
            Art(world);
            Mood(view, world);
            // The models' mesh LODs taken sooner: as good to the eye, a quarter of the triangles.
            QualitySettings.meshLodThreshold = 4f;
            // The ribbon over the world: a little ahead the road itself, with
            // its verges, lifts off the world and goes up into a funnel round
            // the hero, each turn wider and higher, out to the story's end;
            // on the way up the world on it becomes a memory, then a film of
            // pictures of the story.
            ribbon.sky = true;
            ribbon.above = 0.15f;
            ribbon.startWidth = 0.3f;
            ribbon.skyWidth = 0.09f;
            ribbon.follow = 40f;
            ribbon.level = 10f;
            ribbon.rampLength = 200f;
            ribbon.rampPitch = 26f;
            ribbon.lead = 1.3f;
            ribbon.reliefTo = 50f;
            ribbon.settleFrom = 40f;
            ribbon.settleTo = 330f;
            ribbon.memory = new Vector2(60f, 330f);
            ribbon.filmIn = new Vector2(330f, 460f);
            ribbon.calm = new Vector2(220f, 520f);
            ribbon.turns = 4f;
            ribbon.funnelRadius = new Vector2(250f, 600f);
            ribbon.funnelRise = new Vector2(12f, 30f);
            ribbon.funnelBase = 60f;
            ribbon.wobble = 0.1f;
            ribbon.sag = 20f;
            ribbon.windLength = 400f;
            ribbon.frameLength = 7f;
            ribbon.frameAspect = 3.5f;
            // One way for now: no forks.
            world.branches = false;
            var marks = new GameObject("Marks").AddComponent<StoryMarks>();
            marks.world = world;
            marks.ribbon = ribbon;
            marks.view = view;
            marks.from = 150f;
            marks.village = Icon("village");
            marks.cave = Icon("cave");
            marks.city = Icon("city");
            marks.farm = Icon("farm");
            marks.end = Icon("end");
            Object.FindAnyObjectByType<StoryMap>().shown = false;
            Quest(world, ribbon, view);
            Save("Crystal");
        }

        /// <summary>The demo's quest (SpiritQuest.cs): talks as in Gothic II,
        /// the spirit on the ribbon, Hilda at the farm and her spirit, her keys,
        /// the lines and sounds (art/queue_quest.py, art/fetch_quest.py).</summary>
        static void Quest(CrystalWorld world, RibbonBend ribbon, ForwardCamera view)
        {
            string quest = ArtFolder + "/Quest/";
            var font = AssetDatabase.LoadAssetAtPath<Font>(Folder + "/Fonts/EBGaramond12-Regular.ttf");
            AudioClip Sound(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(quest + "Sound/" + name + ".wav");
            var root = new GameObject("Quest");
            var dialogue = root.AddComponent<Dialogue>();
            dialogue.font = font;
            dialogue.view = view;
            dialogue.hero = view.hero;
            dialogue.entry = Sound("entry");

            // The spirit on the ribbon: a light, its halo, sparks and trail.
            var spirit = new GameObject("Spirit").AddComponent<RibbonSpirit>();
            spirit.ribbon = ribbon;
            spirit.eye = view.GetComponent<Camera>();
            spirit.glowLook = Material("Spirit Glow", "FinishTheGame/Glow");
            var trail = Material("Spirit Trail", "FinishTheGame/Glow");
            trail.SetFloat("_Trail", 1f);
            trail.SetFloat("_Falloff", 1.4f);
            trail.SetColor("_Tint", new Color(2.2f, 1.6f, 0.9f, 1f));
            spirit.trailLook = trail;
            spirit.hop = Sound("hop");
            spirit.flash = Sound("flash");
            spirit.vanish = Sound("vanish");

            // Hilda, the farm woman (the studio's farmer), and her spirit: the
            // finest of her, a lady in jewels and brocade (hilda_spirit; till
            // there is one, Hilda herself).
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/Characters/farmer.fbx");
            var noble = AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/Characters/hilda_spirit.fbx");
            var moves = HeroAnimation.Villager();
            var skin = Material("Villager", "Universal Render Pipeline/Simple Lit");
            skin.SetFloat("_Smoothness", 0.1f);
            Villager Person(string name, string title, bool ghostly, Material look)
            {
                var person = new GameObject(name).AddComponent<Villager>();
                person.title = title;
                person.world = world;
                person.hero = view.hero;
                person.font = font;
                person.rig = rig;
                person.moves = moves;
                person.look = look;
                person.height = 1.68f;
                person.ghostly = ghostly;
                return person;
            }
            var hilda = Person("Hilda", "Hilda", false, skin);
            var ghost = Person("Hilda's Spirit", "A spirit", true, Material("Ghost", "FinishTheGame/Ghost"));
            if (noble != null) ghost.rig = noble;
            var motes = Material("Spirit Motes", "FinishTheGame/Glow");
            motes.SetColor("_Tint", new Color(3f, 2.9f, 2.8f, 1f));
            motes.SetFloat("_Falloff", 2.5f);
            EditorUtility.SetDirty(motes);
            ghost.motes = motes;
            ghost.rays = Material("Spirit Rays", "FinishTheGame/Rays");

            var keysGlint = Material("Keys Glint", "FinishTheGame/Glow");
            keysGlint.SetColor("_Tint", new Color(2.6f, 2.2f, 1.5f, 1f));
            keysGlint.SetFloat("_Falloff", 4f);
            var play = root.AddComponent<SpiritQuest>();
            play.world = world;
            play.ribbon = ribbon;
            play.hero = view.hero;
            play.view = view;
            play.dialogue = dialogue;
            play.spirit = spirit;
            play.hilda = hilda;
            play.ghost = ghost;
            play.keysModel = AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/Models/keys.fbx");
            play.keysTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/Models/keys.png");
            play.keysLook = skin;
            play.glintLook = keysGlint;
            var lines = new System.Collections.Generic.List<AudioClip>();
            if (System.IO.Directory.Exists(quest + "Voice"))
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { quest + "Voice" }))
                    lines.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)));
            play.lines = lines.ToArray();
            play.keysSound = Sound("keys");
            play.dreamSound = Sound("dream");
            foreach (var m in new[] { spirit.glowLook, trail, skin, keysGlint }) EditorUtility.SetDirty(m);
        }

        /// <summary>A new scene with the sun, the camera, the hero, the world, its ribbon and the map, all wired.</summary>
        static (T world, RibbonBend ribbon, ForwardCamera view) Begin<T>(Material material, float fogDensity) where T : StoryWorld
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = 150f;
                EditorUtility.SetDirty(urp);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = new Color(0.62f, 0.68f, 0.72f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.42f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.18f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.fieldOfView = 55f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var view = camera.gameObject.AddComponent<ForwardCamera>();

            // The world puts it on the route when the scene plays.
            var hero = new GameObject("Hero");
            var body = hero.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.4f;
            body.center = new Vector3(0, 0.9f, 0);
            var walker = hero.AddComponent<Hero>();

            var root = new GameObject("World");
            var world = root.AddComponent<T>();
            var ribbon = root.AddComponent<RibbonBend>();
            var map = root.AddComponent<StoryMap>();

            world.material = material;
            world.hero = walker;
            world.view = view;
            ribbon.world = world;
            ribbon.view = view;
            map.world = world;
            map.view = view;
            view.world = world;
            view.hero = walker;
            walker.world = world;
            walker.view = view;
            walker.map = map;
            return (world, ribbon, view);
        }

        static void Save(string name)
        {
            string path = $"{Folder}/Scenes/{name}.unity";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var other in new[] { "Crystal", "Plane", "Loop" })
            {
                string at = $"{Folder}/Scenes/{other}.unity";
                if (System.IO.File.Exists(at)) scenes.Add(new EditorBuildSettingsScene(at, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"FinishTheGame: {name} scene built");
        }

        /// <summary>The crystal's mood, as the old role-playing games at the end
        /// of a day: a low warm sun with long soft shadows, cool blue shade, a
        /// warm haze; graded (filmic tones, a touch of warmth and grain, a
        /// vignette), the models seated by ambient occlusion.</summary>
        static void Mood(ForwardCamera view, CrystalWorld world)
        {
            var sun = Object.FindAnyObjectByType<Light>();
            sun.intensity = 2.2f;
            sun.color = new Color(1f, 0.82f, 0.6f);
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(24f, -60f, 0f);
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.42f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.38f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.15f, 0.12f);
            // The sky: the twilight at the horizon, warmer than the haze;
            // the studio's painted sky above it, if there is one.
            var sky = RenderSettings.skybox;
            if (sky != null)
            {
                sky.SetColor("_Glow", new Color(0.86f, 0.6f, 0.46f, 0.5f));
                sky.SetFloat("_GlowHeight", 0.05f);
                var painted = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/Sky/sky.png");
                sky.SetTexture("_Panorama", painted);
                sky.SetFloat("_PanoramaShare", painted != null ? 1f : 0f);
                EditorUtility.SetDirty(sky);
            }

            var data = view.GetComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            string path = $"{Folder}/Settings/Crystal Mood.asset";
            AssetDatabase.DeleteAsset(path);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var tones = profile.Add<Tonemapping>(true);
            tones.mode.Override(TonemappingMode.ACES);
            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(0.35f);
            grade.contrast.Override(12f);
            grade.saturation.Override(-6f);
            grade.colorFilter.Override(new Color(1f, 0.96f, 0.9f));
            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.42f, 0.5f, 0.62f));
            split.highlights.Override(new Color(0.75f, 0.62f, 0.45f));
            split.balance.Override(-20f);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.1f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(0.25f);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            var volume = new GameObject("Mood").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        const string ArtFolder = Folder + "/Art";

        /// <summary>Model yaws that turn each model's front to +z, and the share of
        /// its box a model fills seen with little detail (from the review).</summary>
        static readonly System.Collections.Generic.Dictionary<string, (float yaw, float bulk)> Fit = new()
        {
            ["oak"] = (0f, 0.55f),
            ["pine"] = (0f, 0.4f),
        };

        /// <summary>The studio's models and the ground's tiles, if converted (art/).</summary>
        static void Art(PlaneWorld world)
        {
            var models = new System.Collections.Generic.List<Model>();
            var decor = Decor();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ArtFolder + "/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                Mesh mesh = null;
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (sub is Mesh m) mesh = m;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/Models/{name}.png");
                if (mesh == null) continue;
                Fit.TryGetValue(name, out var fit);
                var model = new Model { name = name, mesh = mesh, texture = texture, yaw = fit.yaw, bulk = fit.bulk == 0 ? 1f : fit.bulk, colour = Average(texture) };
                if (decor.TryGetValue(name, out var d))
                {
                    model.kind = d.kind;
                    model.height = d.height;
                    if (d.kind == "tree" || d.kind == "dead" || d.kind == "bush") model.bulk = 0.5f;
                }
                bool rooted = name == "oak" || name == "pine" || model.kind == "tree" || model.kind == "dead" || model.kind == "stump";
                if (rooted) model.sink = Rooted.TryGetValue(name, out float sink) ? sink : Sink(mesh);
                models.Add(model);
            }
            world.models = models.ToArray();
            world.grassTexture = Tile("grass");
            world.roadTexture = Tile("road");
            Debug.Log($"FinishTheGame: {models.Count} models, grass {(world.grassTexture != null)}, road {(world.roadTexture != null)}");
        }

        /// <summary>How deep each tree is set into the ground, a share of its
        /// height: chosen by eye (art/sink_review.py renders each at a few
        /// depths), where its roots go into the ground as roots do; the lone
        /// root balls under some (old96, old99, old102, old103) buried. A model
        /// not here is measured (<see cref="Sink"/>).</summary>
        static readonly System.Collections.Generic.Dictionary<string, float> Rooted = new System.Collections.Generic.Dictionary<string, float>
        {
            ["oak"] = 0.05f, ["pine"] = 0.025f, ["old32"] = 0.07f, ["old34"] = 0.06f, ["old56"] = 0.05f, ["old57"] = 0.05f,
            ["old58"] = 0.045f, ["old67"] = 0.08f, ["old72"] = 0.06f, ["old94"] = 0.06f, ["old95"] = 0.05f, ["old96"] = 0.15f,
            ["old97"] = 0.05f, ["old98"] = 0.06f, ["old99"] = 0.17f, ["old100"] = 0.07f, ["old101"] = 0.06f, ["old102"] = 0.2f,
            ["old103"] = 0.18f, ["old104"] = 0.1f, ["old105"] = 0.07f, ["old106"] = 0.07f, ["old107"] = 0.06f, ["old108"] = 0.07f,
        };

        /// <summary>How deep a tree goes into the ground, a share of its height:
        /// round its trunk's axis (measured at a fifth to a third of its
        /// height), within a few girths of it, the lowest point of each of 16
        /// sectors (a root's tip, or the trunk's foot); the ground goes where
        /// three in four of them are under it, so a lone root reaching down
        /// does not hold the tree up on its tip. At most a fifth.</summary>
        static float Sink(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var bounds = mesh.bounds;
            float height = Mathf.Max(0.01f, bounds.size.y);
            // The trunk's axis and girth.
            var axis = Vector2.zero;
            int n = 0;
            foreach (var v in vertices)
            {
                float share = (v.y - bounds.min.y) / height;
                if (share < 0.2f || share > 0.34f) continue;
                axis += new Vector2(v.x, v.z);
                n++;
            }
            if (n == 0) return 0f;
            axis /= n;
            var girths = new System.Collections.Generic.List<float>();
            foreach (var v in vertices)
            {
                float share = (v.y - bounds.min.y) / height;
                if (share >= 0.2f && share <= 0.34f) girths.Add((new Vector2(v.x, v.z) - axis).magnitude);
            }
            girths.Sort();
            float girth = girths[girths.Count / 2];
            float reach = Mathf.Max(4f * girth, 0.05f * height);
            const int Sectors = 16;
            var lowest = new float[Sectors];
            for (int k = 0; k < Sectors; k++) lowest[k] = float.MaxValue;
            foreach (var v in vertices)
            {
                var off = new Vector2(v.x, v.z) - axis;
                // Not under the trunk: a tip there would be in every sector.
                if (off.magnitude < 0.8f * girth || off.magnitude > reach || v.y - bounds.min.y > 0.3f * height) continue;
                int k = (int)((Mathf.Atan2(off.y, off.x) / (2f * Mathf.PI) + 0.5f) * Sectors) % Sectors;
                lowest[k] = Mathf.Min(lowest[k], v.y - bounds.min.y);
            }
            var found = new System.Collections.Generic.List<float>();
            foreach (var l in lowest) if (l < float.MaxValue) found.Add(l);
            if (found.Count == 0) return Narrowing();
            found.Sort();
            return Mathf.Min(0.2f, Mathf.Max(Narrowing(), found[Mathf.Min(found.Count - 1, (int)(found.Count * 0.75f))] / height));

            // Also down to where the roots, spread at its foot, draw in to
            // near the trunk's girth (as measured before: the deeper wins).
            float Narrowing()
            {
                const int Bins = 50;
                var points = new System.Collections.Generic.List<Vector2>[Bins];
                for (int i = 0; i < Bins; i++) points[i] = new System.Collections.Generic.List<Vector2>();
                foreach (var v in vertices)
                {
                    int bin = (int)((v.y - bounds.min.y) / height * Bins);
                    if (bin >= 0 && bin < Bins) points[bin].Add(new Vector2(v.x, v.z));
                }
                float Spread(int b)
                {
                    if (points[b].Count < 6) return 0f;
                    var d = points[b].ConvertAll(p => (p - axis).magnitude);
                    d.Sort();
                    return d[(int)(d.Count * 0.8f)];
                }
                var trunks = new System.Collections.Generic.List<float>();
                for (int b = 10; b < 17; b++) if (points[b].Count >= 6) trunks.Add(Spread(b));
                if (trunks.Count == 0) return 0f;
                trunks.Sort();
                float trunk = trunks[trunks.Count / 2];
                for (int b = 0; b < 10; b++)
                    if (Spread(b) > 0 && Spread(b) <= trunk * 1.7f)
                        return Mathf.Min(0.2f, (float)b / Bins);
                return 0.2f;
            }
        }

        /// <summary>A pictogram for the ribbon: Art/Icons/&lt;name&gt;.png (the
        /// studio's) shrunk to a few dozen pixels and drawn without smoothing,
        /// as the ribbon's pixel art; made once, Art/Icons/&lt;name&gt; pixels.png.</summary>
        static Texture2D Icon(string name, int pixels = 48)
        {
            string source = $"{ArtFolder}/Icons/{name}.png", target = $"{ArtFolder}/Icons/{name} pixels.png";
            if (!System.IO.File.Exists(source)) return null;
            if (!System.IO.File.Exists(target))
            {
                var image = new Texture2D(2, 2);
                image.LoadImage(System.IO.File.ReadAllBytes(source));
                var small = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
                for (int y = 0; y < pixels; y++)
                    for (int x = 0; x < pixels; x++)
                    {
                        // The average of its block, alpha cut hard.
                        Color sum = Color.clear;
                        int n = 0, bx = image.width / pixels, by = image.height / pixels;
                        for (int j = 0; j < by; j += 2)
                            for (int i = 0; i < bx; i += 2)
                            {
                                sum += image.GetPixel(x * bx + i, y * by + j);
                                n++;
                            }
                        var c = sum / Mathf.Max(1, n);
                        c.a = c.a > 0.5f ? 1f : 0f;
                        small.SetPixel(x, y, c);
                    }
                small.Apply();
                System.IO.File.WriteAllBytes(target, small.EncodeToPNG());
                AssetDatabase.ImportAsset(target);
                if (AssetImporter.GetAtPath(target) is TextureImporter importer)
                {
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
        }

        [System.Serializable] class DecorEntry { public string kind; public float height; }

        /// <summary>The decor's kinds and heights (art/decor.json, copied beside the models).</summary>
        static System.Collections.Generic.Dictionary<string, DecorEntry> Decor()
        {
            var found = new System.Collections.Generic.Dictionary<string, DecorEntry>();
            string path = ArtFolder + "/Models/decor.json";
            if (!System.IO.File.Exists(path)) return found;
            // {"old14": {"asset": "...", "kind": "prop", "height": 1.2}, ...}
            var text = System.IO.File.ReadAllText(path);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text,
                "\"(\\w+)\":\\s*\\{[^}]*\"kind\":\\s*\"(\\w+)\"[^}]*\"height\":\\s*([0-9.]+)"))
                found[m.Groups[1].Value] = new DecorEntry { kind = m.Groups[2].Value, height = float.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) };
            return found;
        }

        /// <summary>A texture's average colour, from its smallest mip; ignoring
        /// the unpainted black of an atlas's gaps.</summary>
        static Color Average(Texture2D texture)
        {
            if (texture == null || !texture.isReadable) return Color.grey;
            var pixels = texture.GetPixels(Mathf.Max(0, texture.mipmapCount - 4));
            Color sum = Color.clear;
            int n = 0;
            foreach (var p in pixels)
            {
                if (p.r + p.g + p.b < 0.05f) continue;
                sum += p;
                n++;
            }
            return n == 0 ? Color.grey : sum / n;
        }

        /// <summary>The tile Art/Textures/&lt;name&gt;.png, made from the studio's image
        /// Art/Tiles/&lt;name&gt;.png: blended with itself shifted by half, through a
        /// cross-fade that hides the seam, so it tiles.</summary>
        static Texture2D Tile(string name)
        {
            string source = $"{ArtFolder}/Tiles/{name}.png", target = $"{ArtFolder}/Textures/{name}.png";
            if (!System.IO.File.Exists(source)) return null;
            if (!System.IO.File.Exists(target))
            {
                var image = new Texture2D(2, 2);
                image.LoadImage(System.IO.File.ReadAllBytes(source));
                int w = image.width, h = image.height;
                var a = image.GetPixels();
                var tiled = new Color[a.Length];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var shifted = a[((y + h / 2) % h) * w + (x + w / 2) % w];
                        // Weight of the original: 1 in the middle, 0 at the edges.
                        float wx = 1f - Mathf.Abs(2f * x / (w - 1) - 1f), wy = 1f - Mathf.Abs(2f * y / (h - 1) - 1f);
                        float weight = Mathf.SmoothStep(0f, 1f, Mathf.Min(wx, wy) * 2.5f);
                        tiled[y * w + x] = Color.Lerp(shifted, a[y * w + x], weight);
                    }
                var made = new Texture2D(w, h, TextureFormat.RGBA32, false);
                made.SetPixels(tiled);
                made.Apply();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
                System.IO.File.WriteAllBytes(target, made.EncodeToPNG());
                AssetDatabase.ImportAsset(target);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
        }

        static Material Material(string name, string shader)
        {
            string path = $"{Folder}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            material = new Material(Shader.Find(shader)) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// «The Last Ember Abbey»: the user's level of 300 x 500 m
    /// (workspace/last_ember_abbey_artistic_300x500m.blend), exported by
    /// tools/export_level.py to Art/Levels/LastEmber (Soulslike → Build Last
    /// Ember). The game goes on it from the level's own names: the start at
    /// "D / SPAWN", bonfires at "D / CAMP" (the first) and "D / CEMETERY",
    /// casters along the route curves ("D / GUIDE …", empties every 6 m) and
    /// around "D / CRYPT" and "D / TOWER", the boss at "D / OBJECTIVE".
    /// Nothing here names a position.
    /// </summary>
    public static partial class LevelBuilder
    {
        const string EmberPath = Root + "/Scenes/LastEmber.unity";
        const string EmberArt = Art + "/Levels/LastEmber/";
        const string EmberGround = "D / Landscape · exact 300 × 500 metres";

        [MenuItem("Soulslike/Build Last Ember")]
        public static void BuildEmber()
        {
            missing.Clear();
            controllers.Clear();
            floor.Clear();
            brain = null;
            posts.Clear();
            EnsureTags("Obstacle", BodyTags[0], BodyTags[1]);
            foreach (var folder in new[] { "Scenes", "Materials", "Animation" })
                Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            vertexColor = MaterialAsset("VertexColor", Shader.Find("Soulslike/VertexColorLit"), null);
            actorColor = MaterialAsset("ActorVertexColor", Shader.Find("Soulslike/VertexColorLit"), null);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(EmberArt + "LastEmber.fbx");
            if (asset == null)
            {
                Debug.LogError("Soulslike: Art/Levels/LastEmber/LastEmber.fbx is missing; run tools/export_level.py");
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Atmosphere();
            // The second is the day's (DayAndNight takes it from the K choice).
            Sky("SkyGrimshawPainting", "SkyDayGrimshaw");
            // A haze only far off: clear air to 40 m, the sky's horizon by
            // 135 m, before the trees are culled (140 m), so they come out of
            // it rather than appear. The dense near fog in the sky's colour
            // hung as a green haze (the user's review): this one starts far
            // and keeps only half of that colour.
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 135f;
            var horizon = RenderSettings.fogColor;
            RenderSettings.fogColor = Color.Lerp(new Color(horizon.grayscale, horizon.grayscale, horizon.grayscale), horizon, 0.5f);

            var level = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(level, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            level.name = "Last Ember Abbey";
            var transforms = level.GetComponentsInChildren<Transform>();
            Transform Find(string name)
            {
                var found = transforms.FirstOrDefault(t => t.name == name);
                if (found == null) Debug.LogWarning($"Soulslike: {name} not found in the level");
                return found;
            }
            var json = EmberArt + "LastEmber.json";
            var ground = File.Exists(json) ? JsonUtility.FromJson<VigilLights>(File.ReadAllText(json)).ground : null;
            EmberColliders(level, new HashSet<string>(ground ?? new string[0]));
            Lighting(json, Find);

            var land = Find(EmberGround)?.GetComponent<Renderer>();
            if (land == null) return;
            var extent = land.bounds;
            var area = new Rect(extent.min.x + 3f, extent.min.z + 3f, extent.size.x - 6f, extent.size.z - 6f);
            Walls(area, "Abbey bounds");
            // The level's ground: its images tiled and blended by its masks
            // (a bake of them was one colour); its own decals see-through.
            GroundLayers(land, EmberArt + "Textures/D_Landscape_exact_300_500_metres_TrailWetPine.png",
                EmberArt + "Textures/D_Landscape_exact_300_500_metres_AutumnRockMoss.png");
            SeeThroughDecals(level);
            Grass(land);
            // Lighter than the Last Vigil: the abbey's wood is denser, and the
            // night is a full moon's (the moon a cool silver, brighter).
            RenderSettings.ambientSkyColor *= 1.35f;
            RenderSettings.ambientEquatorColor *= 1.35f;
            RenderSettings.ambientGroundColor *= 1.25f;
            var moon = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (moon != null)
            {
                moon.color = Color.Lerp(moon.color, new Color(0.75f, 0.82f, 0.95f), 0.5f);
                // Not a daytime sun: a dim moon with faint soft shadows, the
                // lanterns the light to see by (the user's review).
                moon.intensity = 0.7f;
                moon.shadows = LightShadows.Soft;
                moon.shadowStrength = 0.35f;
            }

            var spawn = Find("D / SPAWN");
            var objective = Find("D / OBJECTIVE");
            var routes = Routes(transforms);
            var main = routes.FirstOrDefault(r => r.Key.Contains("Main")).Value ?? routes.Values.FirstOrDefault() ?? new List<Vector3>();
            var bonfires = new List<Bonfire>();
            foreach (var marker in new[] { "D / CAMP", "D / CEMETERY" })
                if (Find(marker) is { } at)
                    bonfires.Add(EmberBonfire(OnGround(at.position), NextAlong(main, at.position)));
            if (bonfires.Count == 0) return;

            var player = Player(out var camera);
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = 160f;
            // The undergrowth comes in within the haze (at 45 m, it popped up in clear air).
            camera.gameObject.AddComponent<CullDistances>().undergrowth = 90f;
            var start = spawn != null ? OnGround(spawn.position) : bonfires[0].SpawnPoint;
            player.transform.SetPositionAndRotation(start,
                Quaternion.LookRotation(Flat(NextAlong(main, start) - start, Vector3.forward)));

            Creature boss = null;
            if (objective != null)
            {
                var at = OnGround(objective.position);
                boss = Boss(at, "BossPrincess", 2.4f, "The Drawn Princess, Keeper of the Last Ember",
                    Soulslike.Training.Weapon.Sword, 800f);
                // She faces the way the hero comes.
                boss.transform.rotation = Quaternion.LookRotation(Flat(PreviousAlong(main, at) - at, Vector3.back));
                boss.theme = Theme("BossThemePrincess");
            }
            // The second boss, at the map's middle: on the main road some 35 m
            // past the second hearth (the cemetery's), facing the way the hero comes.
            Creature gaunt = null;
            if (bonfires.Count > 1 && main.Count > 1)
            {
                var at = OnGround(PastAlong(main, bonfires[1].transform.position, 35f));
                // As tall as the network saw beside one of the hero's height, the hammer in hand.
                gaunt = Boss(at, "BossGaunt", TallestCreature, "The Gaunt Warden of the Cemetery",
                    Soulslike.Training.Weapon.Hammer, 800f);
                gaunt.transform.rotation = Quaternion.LookRotation(Flat(bonfires[1].transform.position - at, Vector3.forward));
                gaunt.theme = Theme("BossThemeGaunt");
            }
            var keepClear = bonfires.Select(b => b.transform.position).ToList();
            if (spawn != null) keepClear.Add(spawn.position);
            if (gaunt != null) keepClear.Add(gaunt.transform.position);
            EmberCasters(routes, keepClear, objective != null ? objective.position : (Vector3?)null,
                new[] { Find("D / CRYPT"), Find("D / TOWER") }.Where(t => t != null).Select(t => t.position).ToList());
            if (main.Count > 1) RoadFromCamp(main, bonfires[0].transform.position);

            var fireflies = Fireflies(new Rect(-30f, -30f, 60f, 60f));
            fireflies.transform.SetParent(player.transform, false);
            LanternLights(level);
            DayAndNight(fireflies);

            var managers = new GameObject("Game");
            AssignSounds(managers.AddComponent<Sounds>());
            var game = managers.AddComponent<GameManager>();
            game.player = player;
            game.bonfire = bonfires[0];
            game.boss = boss;
            game.bosses = new[] { gaunt, boss }.Where(b => b != null).ToArray();
            managers.AddComponent<Hud>();
            managers.AddComponent<PauseMenu>();
            Show(managers);

            BakeBalance();
            EditorSceneManager.SaveScene(scene, EmberPath);
            // The game is this level alone (the Last Vigil's scene stays buildable).
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(EmberPath, true) };
            Debug.Log(missing.Count == 0
                ? "Soulslike: last ember built"
                : "Soulslike: last ember built; placeholders for " + string.Join(", ", missing));
        }

        static Vector3 Flat(Vector3 v, Vector3 otherwise)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.01f ? v.normalized : otherwise;
        }

        /// <summary>The route empties ("&lt;curve&gt; 000", ...) of each GUIDE curve, in order.</summary>
        static Dictionary<string, List<Vector3>> Routes(Transform[] transforms)
        {
            var routes = new Dictionary<string, SortedDictionary<string, Vector3>>();
            foreach (var t in transforms)
            {
                if (!t.name.Contains("GUIDE")) continue;
                var cut = t.name.LastIndexOf(' ');
                if (cut < 0 || !int.TryParse(t.name.Substring(cut + 1), out _)) continue;
                var curve = t.name.Substring(0, cut);
                if (!routes.TryGetValue(curve, out var points)) routes[curve] = points = new SortedDictionary<string, Vector3>();
                points[t.name] = t.position;
            }
            return routes.ToDictionary(r => r.Key, r => r.Value.Values.ToList());
        }

        static Vector3 NextAlong(List<Vector3> route, Vector3 at) => Along(route, at, +1);
        static Vector3 PreviousAlong(List<Vector3> route, Vector3 at) => Along(route, at, -1);

        /// <summary>The level by day as well as by night (DayNight; the pause
        /// menu switches): the night as built so far, the day under the studio's
        /// overcast Grimshaw sky (SkyDayGrimshaw, asset 01a11312d959229acf3d3e),
        /// a high soft sun with full shadows, a far pale haze, its own grade.</summary>
        static void DayAndNight(GameObject fireflies)
        {
            var choice = Object.FindAnyObjectByType<SkyChoice>();
            if (choice == null || choice.skies.Length < 2)
            {
                missing.Add("day sky");
                return;
            }
            var daySky = choice.skies[1];
            var dayHorizon = choice.fogs[1];
            RenderSettings.skybox = choice.skies[0];
            Object.DestroyImmediate(choice.gameObject);

            var cycle = new GameObject("Day and night").AddComponent<DayNight>();
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
            cycle.sun = sun;
            cycle.night = new DayNight.Look
            {
                sky = RenderSettings.skybox,
                fog = RenderSettings.fogColor,
                fogStart = RenderSettings.fogStartDistance,
                fogEnd = RenderSettings.fogEndDistance,
                ambientSky = RenderSettings.ambientSkyColor,
                ambientEquator = RenderSettings.ambientEquatorColor,
                ambientGround = RenderSettings.ambientGroundColor,
                lightColor = sun.color,
                lightIntensity = sun.intensity,
                shadowStrength = sun.shadowStrength,
                lightAngles = sun.transform.eulerAngles,
            };
            float grey = dayHorizon.grayscale;
            cycle.day = new DayNight.Look
            {
                sky = daySky,
                fog = Color.Lerp(new Color(grey, grey, grey), dayHorizon, 0.5f),
                fogStart = 50f,
                fogEnd = 150f,
                ambientSky = new Color(0.62f, 0.66f, 0.72f),
                ambientEquator = new Color(0.52f, 0.52f, 0.5f),
                ambientGround = new Color(0.3f, 0.28f, 0.25f),
                lightColor = new Color(1f, 0.94f, 0.84f),
                lightIntensity = 1.5f,
                shadowStrength = 0.85f,
                lightAngles = new Vector3(50f, -35f, 0f),
            };
            cycle.nightOnly = new[] { fireflies, GameObject.Find("Lantern lights") }.Where(g => g != null).ToArray();

            // The day's grade over the night's (Atmosphere's "Post"): its own exposure and toning.
            var path = Root + "/Materials/PostDay.asset";
            var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            T Effect<T>() where T : UnityEngine.Rendering.VolumeComponent
            {
                if (!profile.TryGet<T>(out var effect))
                {
                    effect = profile.Add<T>(true);
                    AssetDatabase.AddObjectToAsset(effect, profile);
                }
                effect.active = true;
                return effect;
            }
            var color = Effect<UnityEngine.Rendering.Universal.ColorAdjustments>();
            color.postExposure.Override(0.35f);
            color.saturation.Override(-8f);
            color.contrast.Override(8f);
            var toning = Effect<UnityEngine.Rendering.Universal.SplitToning>();
            toning.highlights.Override(new Color(1f, 0.95f, 0.88f));
            toning.shadows.Override(new Color(0.6f, 0.64f, 0.7f));
            toning.balance.Override(0f);
            EditorUtility.SetDirty(profile);
            var grade = new GameObject("Post day").AddComponent<UnityEngine.Rendering.Volume>();
            grade.isGlobal = true;
            grade.priority = 1f;
            grade.sharedProfile = profile;
            grade.weight = 0f;
            cycle.dayGrade = grade;
        }

        /// <summary>The route point some distance on from the one nearest a place.</summary>
        static Vector3 PastAlong(List<Vector3> route, Vector3 at, float distance)
        {
            int i = 0;
            for (int k = 1; k < route.Count; k++)
                if ((route[k] - at).sqrMagnitude < (route[i] - at).sqrMagnitude) i = k;
            for (float gone = 0f; i < route.Count - 1 && gone < distance; i++) gone += Vector3.Distance(route[i], route[i + 1]);
            return route[i];
        }

        /// <summary>A boss's theme (Art/Audio), the common boss music without it.</summary>
        static AudioClip Theme(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Art}/Audio/{name}.wav");

        /// <summary>The route point a few steps on (or back) from the one nearest a place.</summary>
        static Vector3 Along(List<Vector3> route, Vector3 at, int way)
        {
            if (route.Count == 0) return at + Vector3.forward * way;
            int nearest = 0;
            for (int i = 1; i < route.Count; i++)
                if ((route[i] - at).sqrMagnitude < (route[nearest] - at).sqrMagnitude) nearest = i;
            return route[Mathf.Clamp(nearest + 3 * way, 0, route.Count - 1)];
        }

        /// <summary>A bonfire with a fire model: the level's own if one is near
        /// (a name of fire, ember or hearth), else the studio's campfire.</summary>
        static Bonfire EmberBonfire(Vector3 position, Vector3 facing)
        {
            var bonfire = BonfireAt(position, facing);
            bool hasFire = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Any(r =>
                Vector3.Distance(r.bounds.center, position) < 3f &&
                new[] { "fire", "ember", "hearth" }.Any(word => r.name.ToLowerInvariant().Contains(word)));
            if (!hasFire)
            {
                var model = Model("Campfire", bonfire.transform, PrimitiveType.Cylinder);
                model.name = "Campfire";
                Fit(model, 2f, 0);
            }
            return bonfire;
        }

        /// <summary>Casters along the routes, every few points (the main route
        /// more densely), a little to either side; none near a bonfire, the
        /// start or the boss; and a pair at each side place.</summary>
        static void EmberCasters(Dictionary<string, List<Vector3>> routes, List<Vector3> keepClear, Vector3? boss, List<Vector3> places)
        {
            var models = new[]
            {
                "EnemyAlluringSuccubusDemoness", "EnemySeductiveVampireCountess", "EnemySultryVampireHuntress",
                "EnemyBeautifulUndeadBanshee", "EnemySeductiveWitchPointed",
            };
            int placed = 0;
            void Place(Vector3 spot, Vector3 facing)
            {
                Hollow(new Vector3(spot.x, 0f, spot.z), Quaternion.LookRotation(Flat(facing, Vector3.forward)).eulerAngles.y,
                    models[placed % models.Length], 1.75f, Armed(placed));
                placed++;
            }
            bool Clear(Vector3 spot) =>
                keepClear.All(p => Vector3.Distance(Flat3(p), Flat3(spot)) > FirstEnemy)
                && (boss == null || Vector3.Distance(Flat3(boss.Value), Flat3(spot)) > 25f);
            foreach (var (name, route) in routes)
            {
                int every = name.Contains("Main") ? 4 : 6;
                for (int i = every; i < route.Count - 1; i += every)
                {
                    var along = route[i + 1] - route[i - 1];
                    var side = Vector3.Cross(Vector3.up, Flat(along, Vector3.forward)) * (placed % 2 == 0 ? 2.5f : -2.5f);
                    var spot = route[i] + side;
                    if (Clear(spot)) Place(spot, -along);
                }
            }
            foreach (var place in places)
                for (int k = 0; k < 2; k++)
                {
                    var spot = place + Quaternion.Euler(0f, 90f + 180f * k, 0f) * Vector3.forward * 3f;
                    if (Clear(spot)) Place(spot, place - spot);
                }
        }

        static Vector3 Flat3(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>How far the second creature on the road from the camp comes back towards it, m.</summary>
        const float NearerCamp = 5f;

        /// <summary>The main road on from the camp (the user's play): its first
        /// creature, a hammer the hero met before looking round, taken away;
        /// the next (the bare-handed witch) brought <see cref="NearerCamp"/> m
        /// back towards the camp. The rest stay as placed.</summary>
        static void RoadFromCamp(List<Vector3> road, Vector3 camp)
        {
            int Nearest(Vector3 at)
            {
                int i = 0;
                for (int k = 1; k < road.Count; k++)
                    if ((Flat3(road[k]) - Flat3(at)).sqrMagnitude < (Flat3(road[i]) - Flat3(at)).sqrMagnitude) i = k;
                return i;
            }
            int from = Nearest(camp);
            // By the road (placed 2.5 m to its side), past the camp, in the order met.
            var met = Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)
                .Where(c => !c.isBoss)
                .Select(c => (creature: c, at: Nearest(c.transform.position)))
                .Where(x => x.at > from && Vector3.Distance(Flat3(road[x.at]), Flat3(x.creature.transform.position)) < 6f)
                .OrderBy(x => x.at).ThenBy(x => Vector3.Distance(Flat3(camp), Flat3(x.creature.transform.position)))
                .Select(x => x.creature).ToList();
            if (met.Count > 0)
            {
                Debug.Log($"Soulslike: the road from the camp: {met[0].name} ({met[0].weapon}) at {met[0].transform.position} taken away");
                Object.DestroyImmediate(met[0].gameObject);
            }
            if (met.Count > 1)
            {
                var second = met[1].transform;
                var back = Flat(PreviousAlong(road, second.position) - second.position, Vector3.zero).normalized;
                var was = second.position;
                second.position = OnGround(second.position + back * NearerCamp);
                Debug.Log($"Soulslike: the road from the camp: {met[1].name} ({met[1].weapon}) from {was} to {second.position}");
            }
        }

        /// <summary>Each of the level's lanterns (a name with "Lantern") lit: a
        /// warm point light where the lamp hangs (the top of the model) and a
        /// small glow there that blooms; none where the .blend already has a
        /// light within 2 m. No shadows: there are ~90 of them.</summary>
        static void LanternLights(GameObject level)
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .Where(l => l.type == LightType.Point).Select(l => l.transform.position).ToList();
            var glow = Additive("Lantern glow", new Color(1f, 0.72f, 0.4f) * 4f);
            var parent = new GameObject("Lantern lights").transform;
            foreach (var renderer in level.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.name.ToLowerInvariant().Contains("lantern")) continue;
                var b = renderer.bounds;
                var lamp = new Vector3(b.center.x, b.max.y - Mathf.Min(0.35f, b.size.y * 0.15f), b.center.z);
                var at = new GameObject("Lantern light").transform;
                at.SetParent(parent, false);
                at.position = lamp;
                if (!lights.Any(p => Vector3.Distance(p, lamp) < 2f))
                {
                    var light = at.gameObject.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.72f, 0.42f);
                    light.intensity = 3.5f;
                    light.range = 10f;
                    light.shadows = LightShadows.None;
                }
                var spot = at.gameObject.AddComponent<ParticleSystem>();
                spot.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = spot.main;
                main.loop = false;
                main.playOnAwake = true;
                main.duration = 1f;
                main.startLifetime = 100000f;
                main.startSpeed = 0f;
                main.startSize = 0.45f;
                main.maxParticles = 1;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var emission = spot.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                var shape = spot.shape;
                shape.enabled = false;
                at.GetComponent<ParticleSystemRenderer>().sharedMaterial = glow;
            }
        }

        /// <summary>The level's decals (D / Ground • …, planes with images of
        /// leaves, soil, needles) came in opaque, as squares: copies of their
        /// materials blended by their images' alpha.</summary>
        static void SeeThroughDecals(GameObject level)
        {
            var copies = new Dictionary<Material, Material>();
            foreach (var renderer in level.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.name.StartsWith("D / Ground •")) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null) continue;
                    if (!copies.TryGetValue(source, out var copy))
                    {
                        var file = System.Text.RegularExpressions.Regex.Replace(source.name, "[^A-Za-z0-9]+", "_").Trim('_');
                        var path = $"{Root}/Materials/Decal {file}.mat";
                        AssetDatabase.DeleteAsset(path);
                        copy = new Material(source);
                        AssetDatabase.CreateAsset(copy, path);
                        copy.SetFloat("_Surface", 1f);
                        copy.SetFloat("_Blend", 0f);
                        copy.SetOverrideTag("RenderType", "Transparent");
                        copy.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        copy.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        copy.SetInt("_ZWrite", 0);
                        copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        // No highlight or sky reflection: URP's "preserve specular"
                        // keeps them where the image is clear, a pale square.
                        copy.SetFloat("_BlendModePreserveSpecular", 0f);
                        copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        copy.SetFloat("_Smoothness", 0f);
                        copy.SetFloat("_SpecularHighlights", 0f);
                        copy.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                        copy.SetFloat("_EnvironmentReflections", 0f);
                        copy.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                        copy.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 50;
                        EditorUtility.SetDirty(copy);
                        copies[source] = copy;
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>Colliders by what an object is: the undergrowth, ground
        /// growth, the level's decals (D / Ground …) and the creek's water none
        /// (one walked on the water), the trees a capsule on the trunk, the rest
        /// its own mesh; the floor is the export's ground (LastEmber.json).</summary>
        static void EmberColliders(GameObject level, HashSet<string> ground)
        {
            foreach (var filter in level.GetComponentsInChildren<MeshFilter>())
            {
                var go = filter.gameObject;
                string name = go.name, kind = go.transform.parent != null ? go.transform.parent.name : "";
                var size = go.GetComponent<Renderer>().bounds.size;
                bool water = name.StartsWith("D / Creek ·");
                bool plant = name.Contains("Growth") || name.Contains("Ecotone") || name.Contains("understorey")
                    || name.StartsWith("D / Ground") || name.Contains("leaf cover")
                    || (kind.Contains("Understorey") && size.y < 1.6f);
                if (water)
                {
                    go.isStatic = true;
                    continue;
                }
                bool tree = (kind.Contains("woodland groves") || name.Contains("tree")) && size.y > 4f;
                if (plant)
                {
                    go.layer = CullDistances.Undergrowth;
                    go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    continue;
                }
                if (tree)
                {
                    go.layer = CullDistances.Trees;
                    var trunk = go.AddComponent<CapsuleCollider>();
                    var scale = go.transform.lossyScale;
                    trunk.direction = 1;
                    trunk.radius = Mathf.Max(0.3f, size.y * 0.035f) / Mathf.Abs(scale.x);
                    trunk.height = size.y * 0.5f / Mathf.Abs(scale.y);
                    trunk.center = filter.sharedMesh.bounds.center - Vector3.up * (filter.sharedMesh.bounds.extents.y * 0.5f);
                    go.tag = "Obstacle";
                    continue;
                }
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                go.isStatic = true;
                if (ground.Contains(name)) floor.Add(collider);
                else if (Obstacle(name, size)) go.tag = "Obstacle";
            }
        }
    }
}

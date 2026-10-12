using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// «The Last Vigil»: the level the user assembled in Blender from the
    /// studio's models (workspace/The_Last_Vigil_Woodland_Final.blend),
    /// exported by tools/export_level.py to Art/Levels/LastVigil
    /// (Soulslike → Build Last Vigil). The game goes on top of it, from the
    /// level's own names: colliders by collection and size, its lights, a
    /// bonfire at every "Landmark hearth" (the start at the one nearest
    /// "Route waypoint 000"), casters along the route waypoints, the boss on
    /// the sanctuary's platform, fighting as soon as she notices the hero.
    /// Re-export the .blend and rebuild: nothing here names a position.
    /// </summary>
    public static partial class LevelBuilder
    {
        const string VigilPath = Root + "/Scenes/LastVigil.unity";
        const string VigilArt = Art + "/Levels/LastVigil/";

        [System.Serializable]
        class VigilLight { public string name, type, target; public float[] color; public float energy, radius; }

        [System.Serializable]
        class VigilLights { public VigilLight[] lights; public string[] ground; }

        [MenuItem("Soulslike/Build Last Vigil")]
        public static void BuildVigil()
        {
            missing.Clear();
            controllers.Clear();
            floor.Clear();
            brain = null;
            posts.Clear();
            EnsureTags("Obstacle", BodyTags[0], BodyTags[1]);
            // What the builder makes is not in git: a fresh clone has no folders for it.
            foreach (var folder in new[] { "Scenes", "Materials", "Animation" })
                Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            vertexColor =MaterialAsset("VertexColor", Shader.Find("Soulslike/VertexColorLit"), null);
            actorColor = MaterialAsset("ActorVertexColor", Shader.Find("Soulslike/VertexColorLit"), null);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(VigilArt + "LastVigil.fbx");
            if (asset == null)
            {
                Debug.LogError("Soulslike: Art/Levels/LastVigil/LastVigil.fbx is missing; run tools/export_level.py");
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Atmosphere();
            Sky("SkyGrimshawPainting");

            var level = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(level, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            level.name = "Last Vigil";
            Transform Find(string name)
            {
                // The sanctuary came into the larger level as "Sanctuary | <name>".
                foreach (var t in level.GetComponentsInChildren<Transform>())
                    if (t.name == name || t.name == "Sanctuary | " + name) return t;
                Debug.LogWarning($"Soulslike: {name} not found in the level");
                return null;
            }
            var json = VigilArt + "LastVigil.json";
            var ground = File.Exists(json) ? JsonUtility.FromJson<VigilLights>(File.ReadAllText(json)).ground : null;
            Colliders(level, new HashSet<string>(ground ?? new string[0]));
            Lighting(VigilArt + "LastVigil.json", Find);
            // Night, but readable: places away from the lanterns were black.
            // Darker than it was: the fires carry the light.
            RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, new Color(0.4f, 0.47f, 0.58f), 0.4f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.32f, 0.33f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.16f, 0.15f);
            var moon = Object.FindAnyObjectByType<Light>();
            if (moon != null && moon.type == LightType.Directional) moon.intensity = Mathf.Max(moon.intensity, 1.1f);

            // The terrain's extent, minus its edge.
            var extent = Find("240 x 300 m continuous terrain").GetComponent<Renderer>().bounds;
            var area = new Rect(extent.min.x + 3f, extent.min.z + 3f, extent.size.x - 6f, extent.size.z - 6f);
            Walls(area, "Reach bounds");
            // Detail up close on the ground: a detail map and decals (LevelBuilder.Ground).
            GroundDetail(Find("240 x 300 m continuous terrain").GetComponent<Renderer>());
            Decals(area, 1400);

            var route = Route(level);
            var bonfires = Hearths(level, route);
            var bonfire = bonfires[0];
            var player = Player(out var camera);
            camera.clearFlags = CameraClearFlags.Skybox;
            // The fog hides everything past ~120 m.
            camera.farClipPlane = 160f;
            camera.gameObject.AddComponent<CullDistances>();
            player.transform.position = OnGround(bonfire.SpawnPoint);

            var boss = Sanctuary(Find);
            RouteCasters(route, bonfires);
            // Mist around the hero: over 240 x 300 m it would be too thin.
            Mist(new Rect(-35f, -35f, 70f, 70f));
            var mist = GameObject.Find("Mist");
            mist.transform.SetParent(player.transform, false);
            mist.transform.localPosition = Vector3.up * 1.2f;

            var managers = new GameObject("Game");
            AssignSounds(managers.AddComponent<Sounds>());
            var game = managers.AddComponent<GameManager>();
            game.player = player;
            game.bonfire = bonfire;
            game.boss = boss;
            managers.AddComponent<Hud>();
            Show(managers);
            managers.AddComponent<PauseMenu>();

            BakeBalance();
            EditorSceneManager.SaveScene(scene, VigilPath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == VigilPath);
            scenes.Insert(0, new EditorBuildSettingsScene(VigilPath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log(missing.Count == 0
                ? "Soulslike: last vigil built"
                : "Soulslike: last vigil built; placeholders for " + string.Join(", ", missing));
        }

        /// <summary>Colliders by the collection an object came from: grass and
        /// ferns none, trees their trunk, the rest its own mesh. The floor
        /// props and actors stand on is the level's ground as the export names
        /// it (LastVigil.json): the terrain, the roads, the steps, the
        /// sanctuary's platform; not a roof or a statue.
        /// Repeated plants and trees are not static (instanced), the rest is.</summary>
        static void Colliders(GameObject level, HashSet<string> ground)
        {
            foreach (Transform group in level.transform)
            {
                string kind = group.name;
                foreach (var filter in group.GetComponentsInChildren<MeshFilter>())
                {
                    var go = filter.gameObject;
                    string name = go.name;
                    var size = go.GetComponent<Renderer>().bounds.size;
                    // Undergrowth by name in the old sanctuary, by size in the
                    // forest floor (named "Reach asset N" there).
                    bool plant = name.Contains("Grass") || name.Contains("Ferns") || name.Contains("Bramble")
                        || (kind.Contains("floor") && size.y < 1.6f);
                    bool tree = kind.Contains("canopy")
                        || (size.y > 6f && (name.Contains("Fir") || name.Contains("Cypress") || name.Contains("tree")));
                    if (name.Contains("Lantern ember")) { Ember(go); continue; }
                    if (name.Contains("Still water") || name.Contains("still lake")) { go.isStatic = true; continue; }
                    if (plant)
                    {
                        // Thousands of small casters: no shadows, not drawn far off.
                        go.layer = CullDistances.Undergrowth;
                        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        continue;
                    }
                    if (tree)
                    {
                        go.layer = CullDistances.Trees;
                        var height = go.GetComponent<Renderer>().bounds.size.y;
                        var trunk = go.AddComponent<CapsuleCollider>();
                        var scale = go.transform.lossyScale;
                        // The model's up is its local y after the FBX import.
                        trunk.direction = 1;
                        trunk.radius = Mathf.Max(0.3f, height * 0.035f) / Mathf.Abs(scale.x);
                        trunk.height = height * 0.5f / Mathf.Abs(scale.y);
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

        /// <summary>The route's waypoints in order ("Route waypoint 000", ...).</summary>
        static List<Vector3> Route(GameObject level)
        {
            var points = new SortedDictionary<string, Vector3>();
            foreach (var t in level.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("Route waypoint")) points[t.name] = t.position;
            return new List<Vector3>(points.Values);
        }

        /// <summary>A bonfire at every "Landmark hearth" light, facing on along
        /// the route; the first is the one nearest the route's start.</summary>
        static List<Bonfire> Hearths(GameObject level, List<Vector3> route)
        {
            var bonfires = new List<Bonfire>();
            foreach (var t in level.GetComponentsInChildren<Transform>())
            {
                // The hearth's light and its fire share the name: one bonfire each.
                if (!t.name.StartsWith("Landmark hearth")) continue;
                if (bonfires.Exists(b => Vector3.Distance(b.transform.position, OnGround(t.position)) < 4f)) continue;
                int nearest = 0;
                for (int i = 1; i < route.Count; i++)
                    if ((route[i] - t.position).sqrMagnitude < (route[nearest] - t.position).sqrMagnitude) nearest = i;
                var next = route[Mathf.Min(nearest + 1, route.Count - 1)];
                bonfires.Add(BonfireAt(OnGround(t.position), next));
            }
            if (route.Count > 0)
                bonfires.Sort((a, b) => (a.transform.position - route[0]).sqrMagnitude.CompareTo((b.transform.position - route[0]).sqrMagnitude));
            return bonfires;
        }

        /// <summary>Casters along the route, every third waypoint, a little to
        /// either side; none near a bonfire or past the sanctuary's stairs.</summary>
        static void RouteCasters(List<Vector3> route, List<Bonfire> bonfires)
        {
            var models = new[]
            {
                "EnemyAlluringSuccubusDemoness", "EnemySeductiveVampireCountess", "EnemySultryVampireHuntress",
                "EnemyBeautifulUndeadBanshee", "EnemySeductiveWitchPointed",
            };
            int placed = 0;
            for (int i = 3; i < route.Count - 3; i += 3)
            {
                var along = route[i + 1] - route[i - 1];
                along.y = 0f;
                var side = Vector3.Cross(Vector3.up, along.normalized) * (placed % 2 == 0 ? 2.5f : -2.5f);
                var spot = route[i] + side;
                bool nearFire = bonfires.Exists(b => Vector3.Distance(b.transform.position, spot) < FirstEnemy);
                if (nearFire) continue;
                Hollow(new Vector3(spot.x, 0f, spot.z), Quaternion.LookRotation(-along).eulerAngles.y,
                    models[placed % models.Length], 1.75f, Armed(placed));
                placed++;
            }
        }

        static void Ember(GameObject ember)
        {
            var glow = MaterialAsset("Ember", Shader.Find("Universal Render Pipeline/Lit"), null);
            glow.SetColor("_BaseColor", new Color(1f, 0.55f, 0.2f));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.15f) * 6f);
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            ember.GetComponent<Renderer>().sharedMaterial = glow;
        }

        /// <summary>A level's Blender lights (its JSON, at the empties
        /// of the same names): points as Unity point lights (Blender watts to a
        /// rough intensity and range), the sun as the directional light; the
        /// area lights' blue becomes the ambient sky.</summary>
        static void Lighting(string path, System.Func<string, Transform> find)
        {
            if (!File.Exists(path)) { missing.Add("LastVigil.json"); return; }
            var data = JsonUtility.FromJson<VigilLights>(File.ReadAllText(path));
            var sun = Object.FindAnyObjectByType<Light>();
            foreach (var entry in data.lights)
            {
                var at = find(entry.name);
                if (at == null) continue;
                var color = new Color(entry.color[0], entry.color[1], entry.color[2]);
                switch (entry.type)
                {
                    case "POINT":
                        var light = new GameObject(entry.name).AddComponent<Light>();
                        light.type = LightType.Point;
                        light.color = color;
                        light.intensity = 1.6f * Mathf.Sqrt(entry.energy / 100f);
                        light.range = 6f + Mathf.Sqrt(entry.energy) * 0.6f;
                        light.transform.SetParent(at, false);
                        break;
                    case "SUN":
                        var target = find(entry.target);
                        if (sun == null || target == null) break;
                        sun.color = color;
                        sun.intensity = 0.6f * entry.energy;
                        sun.transform.rotation = Quaternion.LookRotation(target.position - at.position);
                        break;
                    case "AREA":
                        RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, color * 0.55f, 0.5f);
                        break;
                }
            }
        }

        static void Walls(Rect area, string name)
        {
            var parent = new GameObject(name).transform;
            void Box(Vector3 centre, Vector3 size)
            {
                var box = new GameObject("Bound").AddComponent<BoxCollider>();
                box.tag = "Obstacle";
                box.transform.SetParent(parent, false);
                box.transform.position = centre;
                box.size = size;
            }
            var c = area.center;
            Box(new Vector3(area.xMin, 5f, c.y), new Vector3(1f, 30f, area.height));
            Box(new Vector3(area.xMax, 5f, c.y), new Vector3(1f, 30f, area.height));
            Box(new Vector3(c.x, 5f, area.yMin), new Vector3(area.width, 30f, 1f));
            Box(new Vector3(c.x, 5f, area.yMax), new Vector3(area.width, 30f, 1f));
        }

        /// <summary>A bonfire (light, flame, checkpoint) facing a point: the
        /// hero rises in front of it, that way.</summary>
        static Bonfire BonfireAt(Vector3 position, Vector3 facing)
        {
            var root = new GameObject("Bonfire");
            var towards = facing - position;
            towards.y = 0f;
            root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(towards.sqrMagnitude > 0.01f ? towards : Vector3.forward));
            var light = new GameObject("Flame").AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.2f);
            light.intensity = 5f;
            light.range = 10f;
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = Vector3.up * 1.2f;
            Flame(root.transform.position + Vector3.up * 0.3f, root.transform);
            Sparks(root.transform.position + Vector3.up * 0.5f, root.transform);
            var bonfire = root.AddComponent<Bonfire>();
            bonfire.flame = light;
            return bonfire;
        }

        /// <summary>The boss on the sanctuary's paving before the chapel,
        /// fighting as soon as she notices the hero (no fog gate); invisible
        /// walls on the platform's edges but the stairs.</summary>
        static Creature Sanctuary(System.Func<string, Transform> find)
        {
            var paving = find("Sanctuary paving").GetComponent<Renderer>().bounds;
            var bottom = find("Processional stair 00").position;
            Transform top = null;
            for (int i = 8; i >= 0 && top == null; i--) top = find($"Processional stair {i:00}");
            var inward = paving.center - bottom;
            inward.y = 0f;
            inward.Normalize();
            var bossAt = paving.center - inward * (paving.extents.z * 0.25f);
            // The user's drawing, made a princess (Qwen → Hunyuan3D → Paint → rig).
            var boss = Boss(bossAt, "BossPrincess", 2.4f, "The Drawn Princess, Keeper of the Last Vigil",
                Soulslike.Training.Weapon.Sword, 800f);
            boss.transform.rotation = Quaternion.LookRotation(-inward);
            // The platform's edges but the stairs.
            var parent = new GameObject("Arena bounds").transform;
            var right = Vector3.Cross(Vector3.up, inward);
            void Box(Vector3 centre, Vector3 size, Quaternion rotation)
            {
                var box = new GameObject("Bound").AddComponent<BoxCollider>();
                box.tag = "Obstacle";
                box.transform.SetParent(parent, false);
                box.transform.SetPositionAndRotation(centre, rotation);
                box.size = size;
            }
            var facing = Quaternion.LookRotation(inward);
            float halfW = paving.extents.x, halfD = paving.extents.z;
            var middle = paving.center + Vector3.up * 3f;
            Box(middle + right * halfW, new Vector3(0.6f, 8f, halfD * 2f), facing);
            Box(middle - right * halfW, new Vector3(0.6f, 8f, halfD * 2f), facing);
            Box(middle + inward * halfD, new Vector3(halfW * 2f, 8f, 0.6f), facing);
            // The front edge beside the stairs (3 m either side of them stays open).
            float side = (halfW - 3f) / 2f;
            Box(middle - inward * halfD + right * (3f + side), new Vector3(side * 2f, 8f, 0.6f), facing);
            Box(middle - inward * halfD - right * (3f + side), new Vector3(side * 2f, 8f, 0.6f), facing);
            return boss;
        }

        /// <summary>No caster within this of a bonfire.</summary>
        const float FirstEnemy = 12f;
    }
}

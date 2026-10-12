using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Soulslike.Training;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// What the level builder (LevelBuilder.Vigil.cs) puts in a level: the
    /// hero, enemies and boss with their animators, weapons and voices, the
    /// atmosphere, sky and mist, the floor props and actors stand on. A
    /// missing model becomes a grey primitive, so a level stays playable
    /// while art is in progress.
    /// </summary>
    public static partial class LevelBuilder
    {
        const string Root = "Assets/Soulslike";
        const string Art = Root + "/Art";
        static readonly Color FogColor = new Color(0.20f, 0.21f, 0.25f);

        static readonly Dictionary<string, (RuntimeAnimatorController controller, CharacterAnimator.Timing[] timings, CharacterAnimator.Moves moves)> controllers = new();
        static Material vertexColor;
        static Material actorColor;
        static readonly List<string> missing = new List<string>();

        // Environment

        static void Atmosphere()
        {
            RenderSettings.fog = true;
            // Exponential, not squared: haze from a few metres on, one solid
            // fog rather than clear air and a wall (~78 % at 30 m, ~99 % at 100 m).
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.05f;
            RenderSettings.fogColor = FogColor;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.34f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.29f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.14f);
            RenderSettings.skybox = null;

            var sun = new GameObject("Dusk light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.82f, 0.68f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(28f, -35f, 0f);

            var volume = new GameObject("Post").AddComponent<Volume>();
            volume.isGlobal = true;
            var profilePath = Root + "/Materials/Post.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            // Applied on every build, so a change here reaches the asset.
            T Effect<T>() where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var effect))
                {
                    effect = profile.Add<T>(true);
                    AssetDatabase.AddObjectToAsset(effect, profile);
                }
                effect.active = true;
                return effect;
            }
            Effect<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var color = Effect<ColorAdjustments>();
            // Less grey than before: the generated level is already muted.
            color.saturation.Override(-12f);
            color.contrast.Override(10f);
            // A moonlit night: 0.1 and 0.4 were too dark, 0.8 and 0.6 too light.
            color.postExposure.Override(0.35f);
            // Warm light, cool shadows: colour where the textures are flat.
            var toning = Effect<SplitToning>();
            toning.highlights.Override(new Color(1f, 0.86f, 0.7f));
            toning.shadows.Override(new Color(0.58f, 0.63f, 0.74f));
            toning.balance.Override(-15f);
            // Neutral (a blue grade of the Last Ember was tried and dropped).
            var balance = Effect<WhiteBalance>();
            balance.temperature.Override(0f);
            balance.tint.Override(0f);
            var bloom = Effect<Bloom>();
            bloom.intensity.Override(0.7f);
            bloom.threshold.Override(1.05f);
            Effect<Vignette>().intensity.Override(0.38f);
            // The generated models' flaws show most at a distance and in
            // flat texture areas: a soft far blur and a light grain.
            var focus = Effect<DepthOfField>();
            focus.mode.Override(DepthOfFieldMode.Gaussian);
            focus.gaussianStart.Override(50f);
            focus.gaussianEnd.Override(140f);
            focus.gaussianMaxRadius.Override(0.8f);
            var grain = Effect<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.1f);
            grain.response.Override(0.8f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            volume.sharedProfile = profile;
        }

        /// <summary>A point lifted onto the level's floor.</summary>
        static Vector3 OnGround(Vector3 position)
        {
            if (FloorHeight(position) is { } floor) position.y = floor;
            return position;
        }

        // Actors

        /// <summary>The hero's height, m; the tallest creature the network
        /// saw beside one so tall (the training drew both from 1.4–2.2 m: at
        /// most 2.2 / 1.4 as tall as the other).</summary>
        const float HeroHeight = 1.85f;
        const float TallestCreature = HeroHeight * 2.2f / 1.4f;

        /// <summary>The hero (Hero): the rigged knight with a duellist's body
        /// (DuelBody), in a person's hands (HumanFighter, which also moves the
        /// <paramref name="camera"/>), health 400.</summary>
        static Hero Player(out Camera camera)
        {
            var root = Actor("Player", "HeroKnight", Vector3.zero, 0f, DuelHeight, 0.35f);
            root.GetComponent<Health>().max = 400f;
            var fighter = DuelBody(root, BodyTags[0]);
            root.AddComponent<HumanFighter>().body = fighter;
            var hero = root.AddComponent<Hero>();
            hero.height = HeroHeight;
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = FogColor;
            camera.farClipPlane = 120f;
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            cameraObject.AddComponent<AudioListener>();
            return hero;
        }

        /// <summary>A rank-and-file creature: 200 health (the training's duellists'); none where its square
        /// would make a place in three (<see cref="Crowded"/>).</summary>
        static Creature Hollow(Vector3 position, float yaw, string model, float height, Soulslike.Training.Weapon weapon)
        {
            if (Crowded(OnGround(position))) return null;
            return CreatureAt(position, yaw, "Creature " + model, model, height, weapon, 200f);
        }

        /// <summary>The creatures' posts so far in this level (each the centre
        /// of its square, Creature.halfSize a side's half).</summary>
        static readonly List<Vector3> posts = new List<Vector3>();
        /// <summary>The squares <see cref="Crowded"/> spaces the posts by, m (half a side).</summary>
        const float SquareHalf = 7f;
        /// <summary>Half the side of the square a creature fights in, m: its
        /// leash. At <see cref="SquareHalf"/> they turned home at once, the
        /// hero a step away; 1.5 times that (the posts spaced as before).</summary>
        const float Leash = 10.5f;

        /// <summary>Whether a square at <paramref name="post"/> would put some
        /// place in three squares, so three creatures fighting the hero at
        /// once. Equal squares, axis-aligned: three share a place exactly when
        /// each two overlap (an overlap on each axis is one of intervals, and
        /// intervals that meet in pairs meet all together).</summary>
        static bool Crowded(Vector3 post)
        {
            bool Overlap(Vector3 a, Vector3 b) => Mathf.Abs(a.x - b.x) < 2f * SquareHalf && Mathf.Abs(a.z - b.z) < 2f * SquareHalf;
            var near = posts.Where(p => Overlap(p, post)).ToList();
            for (int i = 0; i < near.Count; i++)
                for (int j = i + 1; j < near.Count; j++)
                    if (Overlap(near[i], near[j])) return true;
            return false;
        }

        /// <summary>A boss: a creature with a title (and a theme, set by the level).</summary>
        static Creature Boss(Vector3 position, string model, float height, string title, Soulslike.Training.Weapon weapon, float health)
        {
            var boss = CreatureAt(position, 180f, "Boss", model, height, weapon, health);
            boss.title = title;
            boss.isBoss = true;
            return boss;
        }

        /// <summary>A creature (Creature): the model with a duellist's body
        /// (DuelBody, scaled to <paramref name="height"/>, within what the
        /// network saw beside the hero), fought by the trained network
        /// (FighterAgent, <see cref="Brain"/>, run in the game), its grid
        /// seeing the hero's hurtboxes and the obstacles (the trees too, on
        /// their own layer).</summary>
        static Creature CreatureAt(Vector3 position, float yaw, string name, string model, float height,
            Soulslike.Training.Weapon weapon, float health)
        {
            var root = Actor(name, model, position, yaw, DuelHeight, 0.35f);
            root.GetComponent<Health>().max = health;
            Voice(root, model);
            var fighter = DuelBody(root, BodyTags[1]);
            var agent = Agent<FighterAgent>(root, "Fighter", 0, FighterAgent.Observations(fighter.Actions),
                FighterAgent.Actions(fighter.Actions), BodyTags[0]);
            agent.Body = fighter;
            // Its decisions asked by the creature itself, only while it fights
            // (a requester asks them whether it is enabled or not).
            Object.DestroyImmediate(root.GetComponent<Unity.MLAgents.DecisionRequester>());
            var parameters = root.GetComponent<BehaviorParameters>();
            parameters.Model = Brain();
            parameters.BehaviorType = BehaviorType.InferenceOnly;
            // On the processor (Burst): a small network, batches of one.
            parameters.InferenceDevice = InferenceDevice.Burst;
            root.GetComponentInChildren<GridSensorComponent>().ColliderMask |= 1 << CullDistances.Trees;
            var creature = root.AddComponent<Creature>();
            creature.halfSize = Leash;
            posts.Add(root.transform.position);
            creature.weapon = weapon;
            creature.height = Mathf.Clamp(height, HeroHeight * 1.4f / 2.2f, TallestCreature);
            return creature;
        }

        /// <summary>The balance (training/balance.json) put in the scene: what
        /// its player plays by without the file (Balance).</summary>
        static void BakeBalance()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../training/balance.json"));
            new GameObject("Balance").AddComponent<BalanceHolder>().json = File.ReadAllText(path);
        }

        /// <summary>The duellists' actions, for the balance's panel to check
        /// its rules by (training/actions.json, training/balance.py): each one's
        /// name, its clip (the Mixamo package's "Pack/file"), what it needs in
        /// hand (any: anyone may take it), and what strikes in it (the weapon
        /// held, a limb, or nothing); the roll last. The network knows each by
        /// its place in the list.</summary>
        static void ExportActions(Fighter fighter)
        {
            var actions = new List<Dictionary<string, string>>();
            var clips = CharacterAnimator.DuelAttacks.Select(a => a.clip).Concat(CharacterAnimator.DuelMoves).ToArray();
            for (int i = 0; i < fighter.Attacks; i++)
            {
                string strikes = !fighter.strikes[i] ? null
                    : fighter.strikeLimb[i] >= 2 || fighter.attackWeapon[i] == Training.Weapon.Hands ? "limb" : "weapon";
                actions.Add(new Dictionary<string, string>
                {
                    ["name"] = fighter.attackNames[i],
                    ["clip"] = clips[i],
                    ["needs"] = fighter.attackCommon[i] ? "any" : Balance.Name(fighter.attackWeapon[i]),
                    ["strikes"] = strikes,
                });
            }
            actions.Add(new Dictionary<string, string>
            {
                ["name"] = "roll", ["clip"] = CharacterAnimator.Duelist.roll, ["needs"] = "any", ["strikes"] = null,
            });
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../training/actions.json"));
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(new { actions }, Newtonsoft.Json.Formatting.Indented) + "\n");
        }

        static Unity.InferenceEngine.ModelAsset brain;
        /// <summary>The training's newest exported network
        /// (training/results/duel/Fighter), copied to Art/Brains/Fighter.onnx
        /// for the game: each level build takes up the latest.</summary>
        static Unity.InferenceEngine.ModelAsset Brain()
        {
            if (brain != null) return brain;
            const string path = Art + "/Brains/Fighter.onnx";
            var results = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../training/results/duel/Fighter"));
            var newest = Directory.Exists(results)
                ? Directory.GetFiles(results, "Fighter-*.onnx")
                    .OrderByDescending(f => long.TryParse(Path.GetFileNameWithoutExtension(f).Substring("Fighter-".Length), out var n) ? n : -1)
                    .FirstOrDefault()
                : null;
            if (newest != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.Copy(newest, path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log($"Soulslike: creatures fight with {Path.GetFileName(newest)}");
            }
            brain = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(path);
            if (brain == null) missing.Add("the network (training/results/duel/Fighter/*.onnx)");
            return brain;
        }

        /// <summary>The creatures' weapons along a route, in turn: bare hands, a sword, a hammer.</summary>
        static Soulslike.Training.Weapon Armed(int index) =>
            new[] { Soulslike.Training.Weapon.Hands, Soulslike.Training.Weapon.Sword, Soulslike.Training.Weapon.Hammer }[index % 3];

        /// <summary>What the creatures' grids see as an obstacle (the tag
        /// "Obstacle"): only what plainly stands in the way, a tree, a big
        /// stone, a stump, a house, a wall; at least a metre tall and 0.8 m
        /// across; no plant (the undergrowth, hedges, planted edges: the low
        /// ones have no collider, the tall ones are let through here) and no
        /// lantern, whatever its size.</summary>
        static bool Obstacle(string name, Vector3 size)
        {
            var lower = name.ToLowerInvariant();
            if (Soft.Any(lower.Contains)) return false;
            return size.y >= 1f && Mathf.Max(size.x, size.z) >= 0.8f;
        }

        static readonly string[] Soft =
        {
            "understorey", "undergrowth", "canopy", "planted", "hedge", "shrub", "bush", "thicket", "vegetation",
            "fern", "grass", "growth", "lantern",
        };

        /// <summary>The level's fights shown and heard (FightShow): sparks,
        /// the blows' and swings' sounds.</summary>
        static void Show(GameObject managers)
        {
            var show = managers.AddComponent<FightShow>();
            show.hitSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "/Audio/SwordHit.wav");
            show.swingSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "/Audio/SwordSwing.wav");
            show.effectShader = Shader.Find("Sprites/Default");
        }

        /// <summary>A bone of a rigged model, by its Mixamo name ("RightHand").</summary>
        static Transform Bone(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (t.name == "mixamorig:" + name) return t;
            return null;
        }

        /// <summary>A rigged model plays its Mixamo clips in place (the body is
        /// moved by its controller); null timing for a model without a rig.
        /// Generic: the clips' paths start at the hips, so the Animator sits on
        /// the rig's node above them (Armature), not on the model.</summary>
        static CharacterAnimator.Timing[] Animate(GameObject root, string name, CharacterAnimator.Spec spec, float runSpeed)
        {
            var hips = Bone(root, "Hips");
            if (hips == null || hips.parent == null) return null;
            var instance = PrefabUtility.GetOutermostPrefabInstanceRoot(hips.gameObject);
            if (instance != null) PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            foreach (var other in root.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(other);
            var animator = hips.parent.gameObject.AddComponent<Animator>();
            // One controller per character type: rebuilding it for every hollow
            // would delete the one the previous hollows use.
            if (!controllers.TryGetValue(name, out var built))
            {
                var controller = CharacterAnimator.Build(name, spec, runSpeed, out var made, out var moves);
                built = controllers[name] = (controller, made, moves);
            }
            animator.runtimeAnimatorController = built.controller;
            var timing = built.timings;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return timing;
        }

        /// <summary>A weapon model (Art/Models) in the right hand: a separate
        /// model on the hand bone, since one baked into the rig bends with the
        /// hand. <paramref name="grip"/>: where the hand holds it, a share of its
        /// height from the bottom; <paramref name="endUp"/>: the blade or head is
        /// above the grip in the model (an axe) rather than below (a sword
        /// standing on its point). Placed from the hand's bones in the rest
        /// pose: the grip between the wrist and the middle finger's knuckle, the
        /// blade out on the thumb's side (little finger → index finger).</summary>
        static Transform Weapon(GameObject root, string model, float length, float grip, bool endUp)
        {
            var hand = Bone(root, "RightHand");
            var middle = Bone(root, "RightHandMiddle1");
            var index = Bone(root, "RightHandIndex1");
            var little = Bone(root, "RightHandPinky1");
            if (hand == null || middle == null || index == null || little == null) return null;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Art}/Models/{model}.fbx");
            if (asset == null) return null;
            float height = asset.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.y;

            var holder = new GameObject(model).transform;
            holder.SetParent(hand, false);
            holder.position = Vector3.Lerp(hand.position, middle.position, 0.7f);
            // Turned in the hand as the training's duellist holds it (Grip):
            // from each model's own fingers it was up to 60° apart (the
            // hero's), and the blows land where the network learned them.
            holder.localRotation = Grip(endUp) ?? Quaternion.Inverse(hand.rotation) * FingersGrip(index, little, endUp);
            // The holder in world metres, whatever the bones' scale.
            holder.localScale = Vector3.one / hand.lossyScale.x;
            var weapon = Model(model, holder, PrimitiveType.Cube);
            weapon.transform.localScale = Vector3.one * (length / height);
            weapon.transform.localPosition = -Vector3.up * (length * grip);
            return holder;
        }

        /// <summary>A weapon's way across the knuckles, from the index's root
        /// to the little finger's (the weapon's end up or down).</summary>
        static Quaternion FingersGrip(Transform index, Transform little, bool endUp) =>
            Quaternion.FromToRotation(endUp ? Vector3.up : Vector3.down, (index.position - little.position).normalized);

        static readonly Dictionary<bool, Quaternion?> grips = new Dictionary<bool, Quaternion?>();

        /// <summary>A weapon's turn in the right hand's bone, as the training's
        /// duellist (<see cref="DuelModel"/>) holds it: the same for every
        /// model (one Mixamo skeleton, its bones turned alike). Null without
        /// that model.</summary>
        static Quaternion? Grip(bool endUp)
        {
            if (grips.TryGetValue(endUp, out var known)) return known;
            Quaternion? grip = null;
            var duellist = AssetDatabase.LoadAssetAtPath<GameObject>($"{Art}/Characters/{DuelModel}.fbx");
            if (duellist != null && Bone(duellist, "RightHand") is { } hand
                && Bone(duellist, "RightHandIndex1") is { } index && Bone(duellist, "RightHandPinky1") is { } little)
                grip = Quaternion.Inverse(hand.rotation) * FingersGrip(index, little, endUp);
            grips[endUp] = grip;
            return grip;
        }

        /// <summary>The character's spoken lines (the studio's XTTS:
        /// Art/Audio/Voices/&lt;model&gt;/&lt;kind&gt;_&lt;n&gt;.wav), if she has any.</summary>
        static void Voice(GameObject root, string model)
        {
            var folder = $"{Art}/Audio/Voices/{model}";
            if (!AssetDatabase.IsValidFolder(folder)) return;
            AudioClip[] Lines(string kind)
            {
                var clips = new List<AudioClip>();
                foreach (var guid in AssetDatabase.FindAssets($"{kind}_ t:AudioClip", new[] { folder }))
                    clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)));
                return clips.ToArray();
            }
            var voice = root.AddComponent<EnemyVoice>();
            voice.notice = Lines("notice");
            voice.attack = Lines("attack");
            voice.hurt = Lines("hurt");
            voice.death = Lines("death");
        }

        /// <summary>A moving body (controller, health, procedural pose) with the model as its child.</summary>
        static GameObject Actor(string name, string model, Vector3 position, float yaw, float height, float radius)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(OnGround(position), Quaternion.Euler(0f, yaw, 0f));
            var body = root.AddComponent<CharacterController>();
            body.height = height;
            body.radius = radius;
            body.center = Vector3.up * height / 2f;
            body.stepOffset = 0.3f;
            root.AddComponent<Health>();
            var visual = Model(model, root.transform, PrimitiveType.Capsule);
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
                if (!Painted(r)) r.sharedMaterial = actorColor;
            Fit(visual, height, 1);
            // The controller keeps its capsule skinWidth above the ground: the
            // feet go down by as much, or every body hovers.
            visual.transform.localPosition -= Vector3.up * body.skinWidth;
            var pose = root.AddComponent<Procedural>();
            pose.visual = visual.transform;
            return root;
        }

        /// <summary>A small additive particle flame (an effect, not an asset).</summary>
        static void Flame(Vector3 position, Transform parent)
        {
            var go = new GameObject("Flame particles");
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f, 0.8f), new Color(1f, 0.3f, 0.05f, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var emission = particles.emission;
            emission.rateOverTime = 40f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Additive("Flame", Color.white);
        }

        /// <summary>Sparks rising from a fire: small, bright (they bloom),
        /// lifted by the heat and turning in it as they fade.</summary>
        static void Sparks(Vector3 position, Transform parent)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.35f), new Color(1f, 0.45f, 0.1f));
            main.gravityModifier = -0.12f; // the heat lifts them
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            var emission = particles.emission;
            emission.rateOverTime = 16f;
            // Now and then a log settles: a burst.
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 14, 0, 2.7f) { probability = 0.5f } });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.7f;
            noise.frequency = 1.1f;
            noise.scrollSpeed = 0.6f;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Additive("Spark", new Color(1f, 0.7f, 0.4f) * 5f);
        }

        /// <summary>Rare fireflies in the undergrowth around the hero: slow,
        /// wandering, blinking; pale green, bright enough to bloom.</summary>
        static GameObject Fireflies(Rect area)
        {
            var go = new GameObject("Fireflies");
            go.transform.position = new Vector3(area.center.x, 1.6f, area.center.y);
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 1f, 0.45f), new Color(1f, 0.92f, 0.5f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // Rare: a few at a time, not a swarm.
            main.maxParticles = 40;
            var emission = particles.emission;
            emission.rateOverTime = 4f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area.width, 2.6f, area.height);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.15f;
            noise.damping = true;
            // Two or three slow blinks a life.
            var blink = particles.colorOverLifetime;
            blink.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.1f, 0.3f),
                    new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0.15f, 0.62f), new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1f),
                });
            blink.color = gradient;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Additive("Firefly", new Color(0.75f, 1f, 0.5f) * 4f);
            return go;
        }

        /// <summary>An additive particle material; a colour above 1 blooms.</summary>
        static Material Additive(string name, Color color)
        {
            var material = MaterialAsset(name, Shader.Find("Universal Render Pipeline/Particles/Unlit"), null);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f); // additive
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.One);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            material.SetColor("_BaseColor", color);
            return material;
        }

        // Assets

        /// <summary>The studio model (a rigged character from Art/Characters, or an
        /// FBX from Blender in Art/Models) with the vertex-colour material, or a placeholder.</summary>
        static GameObject Model(string name, Transform parent, PrimitiveType placeholder)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Art}/Characters/{name}.fbx")
                ?? AssetDatabase.LoadAssetAtPath<GameObject>($"{Art}/Models/{name}.fbx");
            GameObject visual;
            if (asset != null)
            {
                visual = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
                foreach (var r in visual.GetComponentsInChildren<Renderer>())
                    if (!Painted(r)) r.sharedMaterial = vertexColor;
            }
            else
            {
                if (!missing.Contains(name)) missing.Add(name);
                visual = GameObject.CreatePrimitive(placeholder);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.SetParent(parent, false);
                // Pivot at the feet, like the exported models.
                var child = visual.transform;
                child.localPosition = Vector3.up * (placeholder == PrimitiveType.Capsule ? 1f : 0.5f);
                var holder = new GameObject(name + " (placeholder)");
                holder.transform.SetParent(parent, false);
                child.SetParent(holder.transform, true);
                visual = holder;
            }
            visual.name = "Visual";
            return visual;
        }

        /// <summary>A model painted by the studio (Hunyuan3D-Paint): its
        /// imported material carries the texture.</summary>
        static bool Painted(Renderer renderer) =>
            renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_BaseMap")
            && renderer.sharedMaterial.GetTexture("_BaseMap") != null;

        /// <summary>Scale a model uniformly so its size along an axis (0 x, 1 y) is <paramref name="size"/> m.</summary>
        static void Fit(GameObject root, float size, int axis)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float current = axis == 0 ? bounds.size.x : bounds.size.y;
            if (current > 0.001f) root.transform.localScale *= size / current;
        }

        static Material MaterialAsset(string name, Shader shader, Texture2D texture)
        {
            var path = $"{Root}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (texture != null) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor") && texture == null)
                material.SetColor("_BaseColor", new Color(0.35f, 0.34f, 0.32f));
            EditorUtility.SetDirty(material);
            return material;
        }

        static void AssignSounds(Sounds sounds)
        {
            AudioClip Clip(string name)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Art}/Audio/{name}.wav");
                if (clip == null && !missing.Contains(name)) missing.Add(name);
                return clip;
            }
            sounds.ambientMusic = Clip("AmbientMusic");
            sounds.bossMusic = Clip("BossMusic");
            sounds.swordSwing = Clip("SwordSwing");
            sounds.swordHit = Clip("SwordHit");
            sounds.hurt = Clip("Hurt");
            sounds.roll = Clip("Roll");
            var steps = new List<AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets("Footstep t:AudioClip", new[] { $"{Art}/Audio" }))
                steps.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)));
            if (steps.Count == 0 && !missing.Contains("Footstep")) missing.Add("Footstep");
            sounds.footsteps = steps.ToArray();
            sounds.enemyDeath = Clip("EnemyDeath");
            sounds.bossRoar = Clip("BossRoar");
            sounds.bonfire = Clip("Bonfire");
            sounds.rest = Clip("FogGate");
            sounds.deathSting = Clip("DeathSting");
        }

        /// <summary>The ground tiles' colliders: the floor props stand on.</summary>
        static readonly List<Collider> floor = new List<Collider>();
        /// <summary>The highest ground tile under a point, if any.</summary>
        static float? FloorHeight(Vector3 point)
        {
            float? best = null;
            var ray = new Ray(new Vector3(point.x, 200f, point.z), Vector3.down);
            foreach (var collider in floor)
                if (collider != null && collider.Raycast(ray, out var hit, 400f) && (best == null || hit.point.y > best))
                    best = hit.point.y;
            return best;
        }

        /// <summary>Studio sky panoramas (equirectangular): the first is the
        /// skybox, its fog the horizon's colour; with more than one, K in the
        /// game goes through them (SkyChoice) to compare.</summary>
        static void Sky(params string[] names)
        {
            var found = new List<string>();
            var skies = new List<Material>();
            var fogs = new List<Color>();
            foreach (var name in names)
            {
                var path = $"{Art}/Textures/Sky/{name}.png";
                if (AssetImporter.GetAtPath(path) is TextureImporter importer
                    && (importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp
                        || importer.maxTextureSize < 4096 || !importer.isReadable))
                {
                    // Mipmaps and wrapping show a seam where the panorama closes;
                    // readable, so the fog can take the horizon's colour.
                    importer.mipmapEnabled = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.maxTextureSize = 4096;
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null)
                {
                    if (!missing.Contains(name)) missing.Add(name);
                    continue;
                }
                var material = MaterialAsset("Sky " + name, Shader.Find("Skybox/Panoramic"), null);
                material.SetTexture("_MainTex", texture);
                material.SetFloat("_Mapping", 1f);   // latitude-longitude
                material.SetFloat("_ImageType", 0f); // 360°
                material.SetFloat("_Exposure", 1f);
                material.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f)); // as painted
                // The fog is the sky just above the horizon, averaged all the
                // way round (one pixel could be the moon), a little darker: the
                // trees fade into the sky, not into a grey wall in front of it.
                var horizon = Color.black;
                for (int i = 0; i < 64; i++)
                    for (int j = 0; j < 4; j++)
                        horizon += texture.GetPixelBilinear(i / 64f, 0.5f + j * 0.02f);
                horizon /= 64 * 4;
                found.Add(name);
                skies.Add(material);
                fogs.Add(Color.Lerp(horizon, Color.black, 0.25f));
            }
            if (skies.Count == 0) return;
            RenderSettings.skybox = skies[0];
            RenderSettings.fogColor = fogs[0];
            if (skies.Count < 2) return;
            var choice = new GameObject("Sky choice").AddComponent<SkyChoice>();
            choice.names = found.ToArray();
            choice.skies = skies.ToArray();
            choice.fogs = fogs.ToArray();
        }

        static void Mist(Rect area)
        {
            var mist = new GameObject("Mist");
            mist.transform.position = new Vector3(area.center.x, 1.2f, area.center.y);
            var particles = mist.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 20f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 26f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            // Many large faint puffs overlapping into a layer: a few dense ones
            // read as separate clouds.
            main.startSize = new ParticleSystem.MinMaxCurve(12f, 20f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var tint = RenderSettings.fogColor;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(tint.r, tint.g, tint.b, 0.07f), new Color(tint.r * 1.1f, tint.g * 1.1f, tint.b * 1.1f, 0.12f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var emission = particles.emission;
            emission.rateOverTime = 18f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area.width, 1f, area.height);
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var drift = particles.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            drift.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.1f);
            var renderer = mist.GetComponent<ParticleSystemRenderer>();
            renderer.sortMode = ParticleSystemSortMode.Distance;
            var material = MaterialAsset("Mist", Shader.Find("Universal Render Pipeline/Particles/Unlit"), null);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // Soft where a puff meets the ground or a gravestone.
            material.SetFloat("_SoftParticlesEnabled", 1f);
            material.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 2.5f, 0f, 0f));
            material.EnableKeyword("_SOFTPARTICLES_ON");
            // Puffs close to the camera fade, so the view is not a grey wall.
            material.SetFloat("_CameraFadingEnabled", 1f);
            material.SetVector("_CameraFadeParams", new Vector4(1.5f, 1f / 4f, 0f, 0f));
            material.EnableKeyword("_FADING_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            material.SetColor("_BaseColor", Color.white);
            renderer.sharedMaterial = material;
        }

    }
}

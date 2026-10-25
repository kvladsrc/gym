using System.Linq;
using Soulslike.Training;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// The training arena (Scenes/Arena.unity, not in the game's build): a few
    /// square arenas side by side, each walled, with trunks and wall pieces
    /// laid out anew every episode, and in each two duellists of one body and
    /// one network (Fighter, FighterAgent, DuelEpisode) learning against each
    /// other (training/train.sh). Its own player build, Builds/Arena, runs
    /// without graphics for the trainer.
    /// </summary>
    public static partial class LevelBuilder
    {
        const string ArenaPath = Root + "/Scenes/Arena.unity";
        const string ArenaBuild = "Builds/Arena/Arena.x86_64";
        /// <summary>The same arena for training/record.sh, built apart so a
        /// running training's player is not replaced under it.</summary>
        const string ArenaViewBuild = "Builds/ArenaView/Arena.x86_64";
        /// <summary>Arenas in the scene (each an environment; the trainer runs
        /// several builds besides); their spacing, m.</summary>
        const int Arenas = 4;
        // 30 m a side: the walls far off, as the game's creatures have none
        // (at 24 m once, then 14, the agents ran in circles; the time-out
        // costs -2 now).
        const float ArenaSpacing = 60f, ArenaHalf = 15f;
        /// <summary>The grid's cells, m, and count a side (8 m across, centred
        /// 2 m before the body: 6 m ahead, 2 m behind; Fighter.eyes), as the user set. At 0.5 m a body
        /// filled up to 3 x 3 cells (a cell is marked if anything touches it).</summary>
        const float GridCell = 0.25f;
        const int GridCells = 32;
        /// <summary>A decision every this many academy steps (ML-Agents steps
        /// with the physics, 0.02 s): 0.2 s, 5 a second, no faster than a
        /// person reacts.</summary>
        const int DecisionPeriod = 10;

        [MenuItem("Soulslike/Build Arena")]
        public static void BuildArena()
        {
            missing.Clear();
            controllers.Clear();
            floor.Clear();
            EnsureTags("Hero", "Boss", "Obstacle", "Fighter", BodyTags[0], BodyTags[1]);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Atmosphere();
            RenderSettings.fog = false;

            for (int i = 0; i < Arenas; i++)
            {
                var centre = new Vector3((i % 2) * ArenaSpacing, 0f, (i / 2) * ArenaSpacing);
                OneArena(centre, i);
            }
            // Films the first arena when asked to (-record), idle otherwise.
            var recorder = new GameObject("Recorder").AddComponent<ArenaRecorder>();
            recorder.arena = GameObject.Find("Arena 0").GetComponent<DuelEpisode>();
            const string Audio = Root + "/Art/Audio/";
            recorder.hitSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "SwordHit.wav");
            recorder.swingSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "SwordSwing.wav");
            recorder.effectShader = Shader.Find("Sprites/Default");
            recorder.flatShader = Shader.Find("Universal Render Pipeline/Unlit");

            BakeBalance();
            ExportActions(Object.FindAnyObjectByType<Fighter>());
            EditorSceneManager.SaveScene(scene, ArenaPath);
            Debug.Log(missing.Count == 0
                ? "Soulslike: arena built"
                : "Soulslike: arena built; placeholders for " + string.Join(", ", missing));
        }

        static void OneArena(Vector3 centre, int index)
        {
            var root = new GameObject($"Arena {index}").transform;
            root.position = centre;
            // A mesh the episode bends each fight (DuelEpisode.Shape), flat here.
            var ground = new GameObject("Floor");
            ground.transform.SetParent(root, false);
            var groundMesh = DuelEpisode.FlatGround(ArenaHalf);
            ground.AddComponent<MeshFilter>().sharedMesh = groundMesh;
            ground.AddComponent<MeshRenderer>().sharedMaterial = ArenaFloor();
            var groundCollider = ground.AddComponent<MeshCollider>();
            groundCollider.sharedMesh = groundMesh;
            floor.Add(groundCollider);
            for (int side = 0; side < 4; side++)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Wall";
                wall.tag = "Obstacle";
                wall.transform.SetParent(root, false);
                bool alongX = side < 2;
                float sign = side % 2 == 0 ? 1f : -1f;
                // From under the floor's lowest to 2 m over its highest (DuelEpisode.bumps).
                wall.transform.localPosition = alongX ? new Vector3(0f, 1f, sign * ArenaHalf) : new Vector3(sign * ArenaHalf, 1f, 0f);
                wall.transform.localScale = alongX ? new Vector3(ArenaHalf * 2f, 4f, 0.4f) : new Vector3(0.4f, 4f, ArenaHalf * 2f);
            }
            // What the episodes lay out (DuelEpisode): up to 12 trunks and 4 wall
            // pieces, as thick on the 30 m floor as 3 and 1 were on 14 m.
            var trees = new Transform[12];
            for (int t = 0; t < trees.Length; t++)
            {
                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = "Tree";
                trunk.tag = "Obstacle";
                trunk.transform.SetParent(root, false);
                trunk.transform.localScale = new Vector3(0.8f, 2.5f, 0.8f);
                trunk.transform.localPosition = new Vector3(t * 2f - 11f, 2.5f, 3f);
                trees[t] = trunk.transform;
            }
            var pieces = new Transform[4];
            for (int w = 0; w < pieces.Length; w++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "Wall piece";
                piece.tag = "Obstacle";
                piece.transform.SetParent(root, false);
                piece.transform.localScale = new Vector3(3f, 2f, 0.5f);
                piece.transform.localPosition = new Vector3(w * 4f - 4f, 1f, -3f);
                pieces[w] = piece.transform;
            }

            var one = Duellist(centre + new Vector3(0f, 0f, -5f), "Fighter A", BodyTags[0], index == 0);
            var two = Duellist(centre + new Vector3(0f, 0f, 5f), "Fighter B", BodyTags[1], false);
            one.transform.SetParent(root, true);
            two.transform.SetParent(root, true);
            one.opponent = two;
            two.opponent = one;

            var episode = root.gameObject.AddComponent<DuelEpisode>();
            episode.halfSize = ArenaHalf;
            episode.ground = ground.GetComponent<MeshFilter>();
            episode.groundCollider = groundCollider;
            episode.trees = trees;
            episode.walls = pieces;
            // One network for both: the same behaviour, a team each (symmetric self-play).
            int observations = FighterAgent.Observations(one.Actions);
            episode.first = Agent<FighterAgent>(one.gameObject, "Fighter", 0, observations, FighterAgent.Actions(one.Actions), BodyTags[1]);
            episode.second = Agent<FighterAgent>(two.gameObject, "Fighter", 1, observations, FighterAgent.Actions(two.Actions), BodyTags[0]);
            foreach (var (agent, body) in new[] { (episode.first, one), (episode.second, two) })
            {
                agent.Body = body;
                agent.Arena = episode;
            }
        }

        const string DuelModel = "EnemySultryVampireHuntress";
        /// <summary>Each side's hurtboxes' tag: the other's grid shows them
        /// (the body where it is, a kick's leg and all), not its controller's
        /// capsule (where a clip's spin or cartwheel has left it); its own
        /// grid does not.</summary>
        static readonly string[] BodyTags = { "Body A", "Body B" };

        /// <summary>A duellist (Fighter): the game's rank-and-file enemy at
        /// 1.8 m (the episode scales it) with a duellist's body (DuelBody).
        /// <paramref name="camera"/>: a follow camera, to watch in the editor.</summary>
        static Fighter Duellist(Vector3 at, string name, string bodyTag, bool camera)
        {
            var root = Actor(name, DuelModel, at, 0f, DuelHeight, 0.35f);
            root.tag = "Fighter";
            // ~12 plain sword hits (16 each, 80–120 %): at 400 the fights ran
            // out of time; combos (and adrenaline) shorten it.
            root.GetComponent<Health>().max = 200f;
            var fighter = DuelBody(root, bodyTag);

            if (camera)
            {
                var view = new GameObject("Main Camera");
                view.tag = "MainCamera";
                view.AddComponent<Camera>().farClipPlane = 120f;
                view.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
                view.AddComponent<AudioListener>();
                view.AddComponent<ThirdPersonCamera>().target = root.transform;
            }
            return fighter;
        }

        /// <summary>The height a duellist's body is built at (Fighter.Equip
        /// scales it to its own).</summary>
        const float DuelHeight = 1.8f;

        /// <summary>A duellist's body on a rigged actor built at <see
        /// cref="DuelHeight"/> (the training's, the game's hero and creatures
        /// alike): its hurtboxes (tagged <paramref name="bodyTag"/>, which the
        /// other's grid sees), the duellist's clips (CharacterAnimator.Duelist)
        /// with every blow measured on its own rig, and a sword and a hammer
        /// in the right hand, of which it shows one or none.</summary>
        static Fighter DuelBody(GameObject root, string bodyTag)
        {
            Hurtboxes(root, DuelHeight);
            foreach (var hurt in root.GetComponentsInChildren<Collider>(true))
                if (hurt.gameObject.layer == Fighter.HurtboxLayer) hurt.tag = bodyTag;
            var fighter = root.AddComponent<Fighter>();
            // Forearms and shins: the sword's blade does not hurt them bare-handed.
            var proof = new[] { "LeftForeArm", "RightForeArm", "LeftLeg", "RightLeg" }.Select(b => "Hurtbox " + b).ToArray();
            fighter.swordProof = root.GetComponentsInChildren<Collider>(true)
                .Where(c => c.gameObject.layer == Fighter.HurtboxLayer && proof.Contains(c.name)).ToArray();
            fighter.hips = Bone(root, "Hips");
            fighter.head = Bone(root, "Head");
            Animate(root, "Duelist", CharacterAnimator.Duelist, fighter.speed);
            fighter.animator = root.GetComponentInChildren<Animator>();
            MeasureDuel(root, fighter, controllers["Duelist"].moves);

            if (Weapon(root, "Nice/MagicSword", 1.2f, 0.85f, endUp: false) is { } sword)
            {
                fighter.sword = sword.gameObject;
                fighter.swordBase = Marker(sword, "Blade base", -Vector3.up * 0.12f);
                fighter.swordTip = Marker(sword, "Blade tip", -Vector3.up * 1.2f * 0.85f);
                sword.gameObject.SetActive(false);
            }
            else missing.Add("duel sword");
            if (Weapon(root, "GauntHammer", 1.3f, 0.22f, endUp: true) is { } hammer)
            {
                fighter.hammer = hammer.gameObject;
                fighter.hammerBase = Marker(hammer, "Handle", Vector3.zero);
                fighter.hammerHead = Marker(hammer, "Head", Vector3.up * (1.3f * (1f - 0.22f) - 0.2f));
                hammer.gameObject.SetActive(false);
            }
            else missing.Add("duel hammer");
            return fighter;
        }

        /// <summary>The arena floor's look (watch.sh, play.sh): cells of 25 cm,
        /// a darker line each metre, from the middle (the floor's uv is in
        /// metres). A texture of one metre, repeated.</summary>
        static Material ArenaFloor()
        {
            const int Size = 256, Cell = Size / 4;
            var path = $"{Root}/Materials/Arena grid.asset";
            AssetDatabase.DeleteAsset(path);
            var grid = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "Arena grid", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8,
            };
            var fill = new Color(0.46f, 0.45f, 0.43f);
            var cell = new Color(0.22f, 0.21f, 0.20f);
            var metre = new Color(0.06f, 0.05f, 0.05f);
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    // Lines on both sides of each edge, so a repeat halves none.
                    int ex = Mathf.Min(x % Cell, Cell - 1 - x % Cell), ey = Mathf.Min(y % Cell, Cell - 1 - y % Cell);
                    int mx = Mathf.Min(x, Size - 1 - x), my = Mathf.Min(y, Size - 1 - y);
                    // 25 cm lines ~0.8 cm wide and dark (paler, they vanished a
                    // few metres off), the metre's ~3 cm and darker still.
                    pixels[y * Size + x] = mx < 4 || my < 4 ? metre : ex < 1 || ey < 1 ? cell : fill;
                }
            grid.SetPixels(pixels);
            grid.Apply(true);
            AssetDatabase.CreateAsset(grid, path);
            var material = MaterialAsset("Arena floor", Shader.Find("Universal Render Pipeline/Lit"), grid);
            material.SetColor("_BaseColor", Color.white);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Transform Marker(Transform parent, string name, Vector3 local)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.localPosition = local;
            return marker;
        }

        /// <summary>Every duel blow on the rig: the hand or foot that moves
        /// fastest at its peak, the frames it moves faster than
        /// StrikeThreshold of that (its window, s), its length, what it needs
        /// in hand (CharacterAnimator.DuelAttacks), and the way its clip
        /// carries the body. The quick moves (DuelMoves) strike nothing.</summary>
        static void MeasureDuel(GameObject root, Fighter fighter, CharacterAnimator.Moves moves)
        {
            var limbs = new[] { "RightHand", "LeftHand", "RightFoot", "LeftFoot" }.Select(n => Bone(root, n)).ToArray();
            var armature = Bone(root, "Hips").parent.gameObject;
            int count = moves.attacks.Count;
            fighter.limbs = limbs;
            fighter.limbTips = new[] { "RightHandMiddle1", "LeftHandMiddle1", "RightToeBase", "LeftToeBase" }.Select(n => Bone(root, n)).ToArray();
            fighter.knees = new[] { null, null, Bone(root, "RightLeg"), Bone(root, "LeftLeg") };
            // The blows, then the quick moves (no blow, any hands).
            // A common blow strikes with the limbs (as bare hands do).
            fighter.attackWeapon = CharacterAnimator.DuelAttacks.Select(a => a.weapon ?? Soulslike.Training.Weapon.Hands)
                .Concat(CharacterAnimator.DuelMoves.Select(_ => Soulslike.Training.Weapon.Hands)).ToArray();
            fighter.attackCommon = CharacterAnimator.DuelAttacks.Select(a => a.weapon == null)
                .Concat(CharacterAnimator.DuelMoves.Select(_ => true)).ToArray();
            fighter.attackNames = CharacterAnimator.DuelAttacks.Select(a => a.clip)
                .Concat(CharacterAnimator.DuelMoves).Select(c => c.Substring(c.IndexOf('/') + 1)).ToArray();
            fighter.strikes = CharacterAnimator.DuelAttacks.Select(_ => true)
                .Concat(CharacterAnimator.DuelMoves.Select(_ => false)).ToArray();
            fighter.travelForward = new float[count];
            fighter.strikeLimb2 = Enumerable.Repeat(-1, count).ToArray();
            fighter.travelSide = new float[count];
            var hips = Bone(root, "Hips");
            var body = root.transform;
            fighter.strikeStart = new float[count];
            fighter.strikeEnd = new float[count];
            fighter.attackLength = new float[count];
            fighter.strikeLimb = new int[count];
            const float step = 1f / 60f;
            for (int i = 0; i < count; i++)
            {
                var clip = moves.attacks[i];
                int samples = Mathf.Max(2, Mathf.CeilToInt(clip.length / step));
                var speeds = new float[4, samples];
                var last = new Vector3[4];
                for (int n = 0; n < samples; n++)
                {
                    clip.SampleAnimation(armature, n * step);
                    for (int k = 0; k < 4; k++)
                    {
                        var p = limbs[k].position;
                        if (n > 0) speeds[k, n] = (p - last[k]).magnitude / step;
                        last[k] = p;
                    }
                }
                int from = Mathf.FloorToInt(samples * 0.1f), to = Mathf.CeilToInt(samples * 0.9f);
                // A weapon's blow is led by the hands (a step can outrun them;
                // the weapon itself strikes); a kick (with a weapon or without)
                // and a bare-handed blow by any limb.
                bool anyLimb = fighter.attackWeapon[i] == Soulslike.Training.Weapon.Hands
                    || i < CharacterAnimator.DuelAttacks.Length && CharacterAnimator.DuelAttacks[i].kick;
                int limbs4 = anyLimb ? 4 : 2;
                int peak = from, limb = 0;
                for (int n = from; n < to; n++)
                    for (int k = 0; k < limbs4; k++)
                        if (speeds[k, n] > speeds[limb, peak]) { peak = n; limb = k; }
                int start = peak, end = peak;
                while (start > from && speeds[limb, start - 1] > speeds[limb, peak] * StrikeThreshold) start--;
                while (end < to - 1 && speeds[limb, end + 1] > speeds[limb, peak] * StrikeThreshold) end++;
                fighter.strikeStart[i] = start * step;
                fighter.strikeEnd[i] = end * step;
                fighter.attackLength[i] = clip.length;
                fighter.strikeLimb[i] = limb;
                // Bare-handed, the fastest limb of the other kind too (a kick's
                // swinging arm, a punch's foot), if it moves fast as well.
                if (limbs4 == 4)
                {
                    int other = -1, otherPeak = from;
                    for (int n = from; n < to; n++)
                        for (int k = limb < 2 ? 2 : 0; k < (limb < 2 ? 4 : 2); k++)
                            if (other < 0 || speeds[k, n] > speeds[other, otherPeak]) { other = k; otherPeak = n; }
                    if (other >= 0 && speeds[other, otherPeak] > speeds[limb, peak] * 0.6f)
                        fighter.strikeLimb2[i] = other;
                }
                // The way the clip's root goes over it (the in-place copy left
                // it out; the body moves it), in the body's frame.
                var original = CharacterAnimator.Original(clip);
                original.SampleAnimation(armature, 0f);
                var from0 = hips.position;
                original.SampleAnimation(armature, original.length);
                var way = body.InverseTransformVector(hips.position - from0);
                fighter.travelForward[i] = way.z;
                fighter.travelSide[i] = way.x;
            }
            moves.attacks[0].SampleAnimation(armature, 0f);
        }

        /// <summary>An agent on a fighter: its behaviour (the name the trainer
        /// knows it by, its team for self-play), decisions every
        /// <see cref="DecisionPeriod"/> frames, and the grid around it showing
        /// the opponent's hurtboxes (by their tag) and the obstacles.</summary>
        static T Agent<T>(GameObject body, string behavior, int team, int observations, ActionSpec actions, string opponentTag)
            where T : Agent
        {
            var parameters = body.AddComponent<BehaviorParameters>();
            parameters.BehaviorName = behavior;
            parameters.TeamId = team;
            parameters.BrainParameters.VectorObservationSize = observations;
            parameters.BrainParameters.ActionSpec = actions;
            var agent = body.AddComponent<T>();
            agent.MaxStep = 0;
            var requester = body.AddComponent<DecisionRequester>();
            requester.DecisionPeriod = DecisionPeriod;
            // An action once a decision: a speed is raised once, not every frame
            // until the next (the speeds themselves carry on: Inertia).
            requester.TakeActionsBetweenDecisions = false;
            // On its own child (Fighter.eyes): ahead of the body, and in metres
            // whatever the body's size.
            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(body.transform, false);
            body.GetComponent<Fighter>().eyes = eyes;
            var grid = eyes.gameObject.AddComponent<GridSensorComponent>();
            grid.SensorName = "Grid";
            grid.AgentGameObject = body;
            grid.CellScale = new Vector3(GridCell, 4f, GridCell);
            grid.GridSize = new Vector3Int(GridCells, 1, GridCells);
            grid.DetectableTags = new[] { opponentTag, "Obstacle" };
            grid.ColliderMask = 1 << 0 | 1 << Fighter.HurtboxLayer;
            grid.RotateWithAgent = true;
            grid.CompressionType = SensorCompressionType.None;
            return agent;
        }

        static void EnsureTags(params string[] tags)
        {
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var list = manager.FindProperty("tags");
            foreach (var tag in tags)
            {
                bool present = Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).stringValue == tag);
                if (present) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = tag;
            }
            manager.ApplyModifiedProperties();
        }

        /// <summary>The arena alone as a Linux player, for the trainer
        /// (training/train.sh runs it without graphics).</summary>
        [MenuItem("Soulslike/Build Arena Player")]
        public static void BuildArenaPlayer() => BuildArenaTo(ArenaBuild, "arena player");

        /// <summary>The arena for filming the trained pair (training/record.sh).</summary>
        [MenuItem("Soulslike/Build Arena Viewer")]
        public static void BuildArenaViewer() => BuildArenaTo(ArenaViewBuild, "arena viewer");

        static void BuildArenaTo(string path, string what)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ArenaPath },
                locationPathName = path,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
            });
            Debug.Log($"Soulslike: {what} {report.summary.result}, {report.summary.totalErrors} errors");
        }
    }
}

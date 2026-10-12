using System.Linq;
using Soulslike.Training;
using Unity.MLAgents;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// A look at the duel's combos in the editor ("Soulslike/Combo Demo"): the
    /// arena in play mode, its agents stilled, two fighters of 1.8 m face to
    /// face ("Sword on Hammer": the sword's queue alone, at a target holding a
    /// hammer, which stops the blade it meets). The first throws every blow of a sword, then of a hammer, then
    /// bare-handed (kicks too), each queue in a random order, one after
    /// another, at the second, which stands still and cannot die; and round again, shuffled
    /// anew: to see how the blows join. A landed blow's recovery is as the
    /// menu item says (Fighter.Recovery). ComboDemoView shows the
    /// hurtboxes, the blows' paths and hits, and keeps the camera and the
    /// striker's distance. A label over them names the series; the console
    /// tells each hit's time from the last, and how far each miss stayed.
    /// </summary>
    public static partial class LevelBuilder
    {
        /// <summary>The pause between queues, s.</summary>
        const float DemoRest = 1.5f;
        /// <summary>Each queue: what is held, its name, and the ground kept
        /// between the bodies (m).</summary>
        static readonly (Training.Weapon weapon, string name, float gap)[] DemoQueues =
        {
            (Training.Weapon.Sword, "Sword", 0.6f),
            (Training.Weapon.Hammer, "Hammer", 0.6f),
            (Training.Weapon.Hands, "Hands and feet", 0.1f),
        };
        /// <summary>"Rare Blows": the blows the agents all but never take or
        /// land (training/actions.sh), by name, in this order, each queue
        /// with what is held.</summary>
        static readonly (Training.Weapon weapon, string name, float gap, string[] blows)[] RareQueues =
        {
            (Training.Weapon.Hands, "Common: kicks and a punch", 0.1f, new[]
            {
                "mma_high_kick", "punching_1", "mma_spinning_back_kick", "mma_side_kick",
                "roundhouse_kick", "side_kick", "kicking_1", "spin_flip_kick",
            }),
            (Training.Weapon.Sword, "Rare: kicks with a sword", 0.1f, new[]
            {
                "great_sword_kick", "great_sword_kick_2", "sword_and_shield_kick",
            }),
            (Training.Weapon.Hammer, "Rare: kicks with a hammer", 0.1f, new[]
            {
                "standing_melee_attack_kick_ver_1", "standing_melee_attack_kick_ver_2",
            }),
        };
        static bool demoRare;
        static Fighter.AfterHit demoRecovery;
        static string demoMode;
        /// <summary>The sword alone, at a target holding a hammer (it blocks).</summary>
        static bool demoShield;
        static ComboDemoView demoView;

        static Fighter demoStriker, demoTarget;
        static DuelEpisode demoArena;
        static TextMesh demoLabel;
        static int[] demoBlows;
        static int demoRound = -1, demoStep;
        static double demoRestUntil, demoLastHit, demoStart;
        static string demoKind;

        [MenuItem("Soulslike/Combo Demo/No Cancel")]
        static void ComboDemoFull() => ComboDemo(Fighter.AfterHit.Full, "no cancel");

        [MenuItem("Soulslike/Combo Demo/Faster ×1.5")]
        static void ComboDemoFaster() => ComboDemo(Fighter.AfterHit.Faster, "faster ×1.5");

        [MenuItem("Soulslike/Combo Demo/Cancel")]
        static void ComboDemoCancel() => ComboDemo(Fighter.AfterHit.Cancel, "cancel");

        [MenuItem("Soulslike/Combo Demo/Sword on Hammer")]
        static void ComboDemoShield() => ComboDemo(Fighter.AfterHit.Faster, "the hammer holds the sword's blows", true);

        [MenuItem("Soulslike/Combo Demo/Rare Blows")]
        static void ComboDemoRare() => ComboDemo(Fighter.AfterHit.Faster, "in order", rare: true);

        static void ComboDemo(Fighter.AfterHit recovery, string mode, bool shield = false, bool rare = false)
        {
            demoRecovery = recovery;
            demoMode = mode;
            demoShield = shield;
            demoRare = rare;
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ArenaPath);
            EditorApplication.playModeStateChanged -= DemoState;
            EditorApplication.playModeStateChanged += DemoState;
            EditorApplication.isPlaying = true;
        }

        static void DemoState(PlayModeStateChange change)
        {
            // After the arena's own Start (it lays out and equips).
            if (change == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += DemoBegin;
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= DemoDrive;
                EditorApplication.playModeStateChanged -= DemoState;
                Fighter.Recovery = Fighter.AfterHit.Faster;
            }
        }

        static void DemoBegin()
        {
            foreach (var requester in Object.FindObjectsByType<DecisionRequester>(FindObjectsSortMode.None)) requester.enabled = false;
            foreach (var agent in Object.FindObjectsByType<FighterAgent>(FindObjectsSortMode.None)) agent.enabled = false;
            foreach (var chase in Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None)) chase.enabled = false;
            var arenas = Object.FindObjectsByType<DuelEpisode>(FindObjectsSortMode.None);
            foreach (var arena in arenas) arena.enabled = false;
            demoArena = arenas.OrderBy(a => a.transform.position.x + a.transform.position.z).First();
            foreach (var piece in demoArena.trees.Concat(demoArena.walls)) piece.gameObject.SetActive(false);
            // The other arenas' fighters out of the way.
            foreach (var arena in arenas)
                if (arena != demoArena)
                    foreach (var agent in new[] { arena.first, arena.second }) agent.gameObject.SetActive(false);
            demoStriker = demoArena.first.Body;
            demoTarget = demoArena.second.Body;
            demoStriker.Struck -= DemoStruck;
            demoStriker.Struck += DemoStruck;
            demoLabel = new GameObject("Combo demo label").AddComponent<TextMesh>();
            demoLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            demoLabel.GetComponent<MeshRenderer>().sharedMaterial = demoLabel.font.material;
            demoLabel.characterSize = 0.04f;
            demoLabel.fontSize = 64;
            demoLabel.anchor = TextAnchor.MiddleCenter;
            demoLabel.color = Color.white;
            demoView = demoStriker.gameObject.AddComponent<ComboDemoView>();
            demoView.striker = demoStriker;
            demoView.target = demoTarget;
            demoView.label = demoLabel.transform;
            const string Audio = Root + "/Art/Audio/";
            demoView.hitSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "SwordHit.wav");
            demoView.swingSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "SwordSwing.wav");
            demoView.hurtVoices = new[] { "hurt_0", "hurt_1" }
                .Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "Voices/EnemySultryVampireHuntress/" + n + ".wav"))
                .Where(c => c != null).ToArray();
            // The arena (made for training) has no ears.
            if (Object.FindAnyObjectByType<AudioListener>() == null && Camera.main != null)
                Camera.main.gameObject.AddComponent<AudioListener>();
            demoRound = -1;
            DemoNext();
            EditorApplication.update -= DemoDrive;
            EditorApplication.update += DemoDrive;
        }

        /// <summary>The next queue: its weapon in hand, its blows shuffled, face to face anew.</summary>
        static void DemoNext()
        {
            int queues = demoRare ? RareQueues.Length : DemoQueues.Length;
            demoRound = demoShield ? 0 : (demoRound + 1) % queues;
            var (weapon, kind, gap) = demoRare
                ? (RareQueues[demoRound].weapon, RareQueues[demoRound].name, RareQueues[demoRound].gap)
                : DemoQueues[demoRound];
            Fighter.Recovery = demoRecovery;
            demoStriker.Equip(weapon, 1.8f);
            demoTarget.Equip(demoShield ? Training.Weapon.Hammer : Training.Weapon.Hands, 1.8f);
            // Never dying: the queue goes on to its end.
            demoTarget.Health.max = 1e6f;
            var f = demoStriker;
            demoBlows = demoRare
                ? RareQueues[demoRound].blows.Select(n => System.Array.IndexOf(f.attackNames, n)).Where(i => i >= 0).ToArray()
                : Enumerable.Range(0, f.Attacks)
                    .Where(i => f.strikes[i] && f.attackWeapon[i] == weapon)
                    .OrderBy(_ => Random.value).ToArray();
            demoView.gap = gap;
            float apart = gap + 2f * demoStriker.GetComponent<CharacterController>().radius;
            demoStriker.ResetAt(demoArena.Centre - Vector3.right * apart / 2f, 90f);
            demoTarget.ResetAt(demoArena.Centre + Vector3.right * apart / 2f, -90f);
            demoStep = 0;
            demoLastHit = -1;
            demoStart = Time.timeAsDouble;
            demoKind = kind + " — " + demoMode;
            demoLabel.text = demoKind;
            Debug.Log($"Combo demo: {demoLabel.text}: " + string.Join(", ", demoBlows.Select(i =>
                CharacterAnimator.DuelAttacks[i].clip.Split('/').Last() + $" {f.attackLength[i]:F2} s, window {f.strikeStart[i]:F2}–{f.strikeEnd[i]:F2}")));
        }

        static void DemoDrive()
        {
            if (!EditorApplication.isPlaying || demoStriker == null) return;
            double now = Time.timeAsDouble;
            if (now < demoRestUntil || demoStriker.Busy) return;
            // Still walking back to the target (ComboDemoView steers).
            if (demoRestUntil <= 0 && demoStep < demoBlows.Length && !demoView.Ready) return;
            if (demoRestUntil > 0)
            {
                demoRestUntil = 0;
                DemoNext();
                return;
            }
            if (demoStep < demoBlows.Length)
            {
                int blow = demoBlows[demoStep++];
                demoLabel.text = $"{demoKind}\n{demoStep}/{demoBlows.Length}: {CharacterAnimator.DuelAttacks[blow].clip.Split('/').Last()}";
                demoStriker.Act(blow);
                return;
            }
            Debug.Log($"Combo demo: {demoKind}: the queue took {now - demoStart:F2} s");
            demoRestUntil = now + DemoRest;
        }

        static void DemoStruck(bool landed, float damage)
        {
            double now = Time.timeAsDouble;
            string since = demoLastHit >= 0 ? $", {now - demoLastHit:F2} s after the last hit" : "";
            if (landed) demoLastHit = now;
            string blow = demoStep > 0 ? CharacterAnimator.DuelAttacks[demoBlows[demoStep - 1]].clip.Split('/').Last() : "";
            if (landed) Debug.Log($"Combo demo: {blow}: hit, combo {demoStriker.Combo}, {damage:F0} damage{since}");
        }
    }
}

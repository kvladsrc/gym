using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Soulslike.EditorTools
{
    /// <summary>
    /// Animator controllers for the rigged characters from Mixamo clips (the
    /// shared package dev.bonfirelit.mixamo), rebuilt with the level. The
    /// parameters are those Fighter sets: Speed (m/s),
    /// Attack, Roll, Jump and Hit (triggers), AttackIndex (which attack),
    /// Dead; with a drawn stance (the hero's great sword) also Drawn (0 the
    /// weapon sheathed, 1 drawn), Draw and Sheathe. The
    /// clips are played in place
    /// (Animation/Clips): the controllers move the bodies.
    /// </summary>
    public static class CharacterAnimator
    {
        const string Package = "Packages/dev.bonfirelit.mixamo/";
        /// <summary>A sprint's speed over the run's; a quick side step's share of the run.</summary>
        const float SprintFactor = 1.6f, SidewaysShare = 0.75f;
        /// <summary>The roll clip played this much faster (1.4 s of it in 0.9 s).</summary>
        const float RollClipSpeed = 1.4f / 0.9f;

        public class Spec
        {
            public string idle, walk, run, attack, hit, death, roll, jump;
            /// <summary>A faster run past the run, at <see cref="SprintFactor"/> times its speed.</summary>
            public string sprint;
            /// <summary>More attacks after <see cref="attack"/> (AttackIndex 1, 2, ...);
            /// their blows land where the limbs move fastest (measured).</summary>
            public string[] attacks = { };
            public float attackSpeed = 1f, hitSpeed = 1f, rollSpeed = 1f;
            /// <summary>Where the blow lands, a share of the attack clip (the
            /// fastest hand, measured).</summary>
            public float attackHit = 0.4f;
            /// <summary>Share of the roll clip played (the rest is standing still).</summary>
            public float rollEnd = 0.9f;
            /// <summary>A second stance, the weapon drawn: its idle, walk, run and
            /// sprint, blended in by Drawn; <see cref="draw"/> draws it, the same
            /// clip backwards sheathes it.</summary>
            public string drawnIdle, drawnWalk, drawnRun, drawnSprint, draw;
            /// <summary>The jump from a run (RunJump), beside <see cref="jump"/>.</summary>
            public string runJump;
            /// <summary>Steps back and to the sides, facing ahead (a boss keeping
            /// her eyes on the hero): with them the gaits blend by the body's
            /// velocity in its own frame (MoveX across, MoveZ ahead), not by
            /// speed alone.</summary>
            public string walkBack, walkLeft, walkRight;
            /// <summary>The same for the drawn stance (the hero under command
            /// turns by himself and steps aside facing ahead): each side a walk
            /// and a faster step, placed at their own speeds.</summary>
            public string drawnBack, drawnLeft, drawnRight, drawnLeftFast, drawnRightFast;
        }

        /// <summary>What the controller needs of the stance and attack clips.</summary>
        public class Moves
        {
            public AnimationClip draw;
            /// <summary>The attack clips (AttackIndex order), to measure their blows on the rig.</summary>
            public System.Collections.Generic.List<AnimationClip> attacks = new();
            /// <summary>The jumps (the standing one, then the running one): takeoff,
            /// time in the air and length, s (<see cref="JumpTiming"/>).</summary>
            public System.Collections.Generic.List<(float takeoff, float air, float length)> jumps = new();
        }

        /// <summary>The blow and the end of the attack, seconds, at the attack's speed.</summary>
        public struct Timing
        {
            public float hit, length;
        }

        /// <summary>The training duel's blows (LevelBuilder.Arena, Fighter): every
        /// melee attack of the packs, each with what it needs in hand (none:
        /// common, any fighter may throw it) and whether a foot leads it.
        /// Capoeira only bare-handed; the kicks and punches not of a weapon's
        /// pack anyone; a sword's or a hammer's only with that weapon. The
        /// kicks of the weapon packs hold the weapon through them, so they need
        /// it too, but strike with the foot. The network knows a blow by its
        /// place here: a clip replaced keeps its place (the policy relearns
        /// what that action does, not its shape).</summary>
        public static readonly (string clip, Training.Weapon? weapon, bool kick)[] DuelAttacks =
        {
            ("Capoeira Pack/armada", Training.Weapon.Hands, false),
            ("Capoeira Pack/bencao", Training.Weapon.Hands, false),
            ("Capoeira Pack/chapa_2", Training.Weapon.Hands, false),
            ("Capoeira Pack/chapa_giratoria_2", Training.Weapon.Hands, false),
            ("Capoeira Pack/chapa_giratoria", Training.Weapon.Hands, false),
            ("Capoeira Pack/martelo_2", Training.Weapon.Hands, false),
            ("Capoeira Pack/martelo_3", Training.Weapon.Hands, false),
            ("Capoeira Pack/martelo_do_chau", Training.Weapon.Hands, false),
            ("Capoeira Pack/meia_lua_de_compasso", Training.Weapon.Hands, false),
            ("Capoeira Pack/meia_lua_de_frente", Training.Weapon.Hands, false),
            ("Capoeira Pack/pontera", Training.Weapon.Hands, false),
            ("Capoeira Pack/queshada_1", Training.Weapon.Hands, false),
            ("Capoeira Pack/queshada_2", Training.Weapon.Hands, false),
            ("Capoeira Pack/rasteira_1", Training.Weapon.Hands, false),
            ("Capoeira Pack/rasteira_2", Training.Weapon.Hands, false),
            ("Pro Melee Axe Pack/standing_melee_attack_kick_ver_1", Training.Weapon.Hammer, true),
            ("Pro Melee Axe Pack/standing_melee_attack_kick_ver_2", Training.Weapon.Hammer, true),
            ("Great Sword Pack/great_sword_kick", Training.Weapon.Sword, true),
            ("Great Sword Pack/great_sword_kick_2", Training.Weapon.Sword, true),
            ("Pro Sword and Shield Pack/sword_and_shield_kick", Training.Weapon.Sword, true),
            // Common blows, anyone's. In the boxing's places, but for its two
            // kept: punching_1 (the quickest action, a quarter of the
            // bare-handed's) and kicking_1 (the one blow stepping in); the
            // boxing landed only at arm's length, and was all but unused.
            ("Singles/mma_high_kick", null, false),
            ("Boxing/punching_1", null, false),
            ("Singles/mma_spinning_back_kick", null, false),
            ("Singles/mma_side_kick", null, false),
            ("Singles/roundhouse_kick", null, false),
            ("Singles/side_kick", null, false),
            ("Boxing/kicking_1", null, false),
            ("Singles/spin_flip_kick", null, false),
            ("Great Sword Pack/great_sword_attack", Training.Weapon.Sword, false),
            ("Great Sword Pack/great_sword_high_spin_attack", Training.Weapon.Sword, false),
            ("Great Sword Pack/great_sword_jump_attack", Training.Weapon.Sword, false),
            ("Great Sword Pack/great_sword_slash", Training.Weapon.Sword, false),
            ("Great Sword Pack/great_sword_slash_2", Training.Weapon.Sword, false),
            ("Great Sword Pack/great_sword_slash_3", Training.Weapon.Sword, false),
            ("Great Sword Pack/great_sword_slash_4", Training.Weapon.Sword, false),
            // In great sword slash (5)'s place (the network's actions keep their
            // indices): that one and sword and shield slash (5), alternated,
            // were most of the sword's blows.
            ("Singles/stable_sword_outward_slash", Training.Weapon.Sword, false),
            ("Great Sword Pack/great_sword_slide_attack", Training.Weapon.Sword, false),
            ("Pro Sword and Shield Pack/sword_and_shield_attack", Training.Weapon.Sword, false),
            ("Pro Sword and Shield Pack/sword_and_shield_attack_2", Training.Weapon.Sword, false),
            ("Pro Sword and Shield Pack/sword_and_shield_attack_3", Training.Weapon.Sword, false),
            ("Pro Sword and Shield Pack/sword_and_shield_attack_4", Training.Weapon.Sword, false),
            ("Pro Sword and Shield Pack/sword_and_shield_slash", Training.Weapon.Sword, false),
            ("Pro Sword and Shield Pack/sword_and_shield_slash_2", Training.Weapon.Sword, false),
            ("Pro Sword and Shield Pack/sword_and_shield_slash_3", Training.Weapon.Sword, false),
            ("Pro Sword and Shield Pack/sword_and_shield_slash_4", Training.Weapon.Sword, false),
            ("Pro Sword and Shield Pack/sword_and_shield_slash_5", Training.Weapon.Sword, false),
            ("Pro Melee Axe Pack/standing_melee_attack_360_high", Training.Weapon.Hammer, false),
            ("Pro Melee Axe Pack/standing_melee_attack_360_low", Training.Weapon.Hammer, false),
            ("Pro Melee Axe Pack/standing_melee_attack_backhand", Training.Weapon.Hammer, false),
            ("Pro Melee Axe Pack/standing_melee_attack_downward", Training.Weapon.Hammer, false),
            ("Pro Melee Axe Pack/standing_melee_attack_horizontal", Training.Weapon.Hammer, false),
            ("Pro Melee Axe Pack/standing_melee_combo_attack_ver_1", Training.Weapon.Hammer, false),
            ("Pro Melee Axe Pack/standing_melee_combo_attack_ver_2", Training.Weapon.Hammer, false),
            ("Pro Melee Axe Pack/standing_melee_combo_attack_ver_3", Training.Weapon.Hammer, false),
            ("Pro Melee Axe Pack/standing_melee_run_jump_attack", Training.Weapon.Hammer, false),
        };

        /// <summary>The duel's quick moves (the user's Mixamo picks): no blow, the
        /// body carried out of the way; any fighter may make them.</summary>
        public static readonly string[] DuelMoves =
        {
            "Dodges/aerial_evade",
            "Dodges/dodging_back",
            "Dodges/esquiva_2",
        };

        /// <summary>The training duellist: walks, runs and steps aside and back
        /// with empty hands (the axeman's side steps), rolls, flinches, dies;
        /// strikes with any of <see cref="DuelAttacks"/>, or moves quickly with
        /// any of <see cref="DuelMoves"/> (AttackIndex after the blows).</summary>
        public static readonly Spec Duelist = new Spec
        {
            idle = "Pro Melee Axe Pack/unarmed_idle",
            walk = "Pro Melee Axe Pack/unarmed_walk_forward",
            run = "Pro Melee Axe Pack/unarmed_run_forward",
            walkBack = "Pro Melee Axe Pack/unarmed_walk_back",
            walkLeft = "Pro Melee Axe Pack/standing_walk_left",
            walkRight = "Pro Melee Axe Pack/standing_walk_right",
            attack = DuelAttacks[0].clip,
            attackHit = -1f,
            attacks = DuelAttacks.Skip(1).Select(a => a.clip).Concat(DuelMoves).ToArray(),
            hit = "Lite Magic Pack/standing_react_small_from_front",
            death = "Lite Magic Pack/standing_react_death_backward",
            roll = "Action Adventure Pack/falling_to_roll",
            rollSpeed = RollClipSpeed,
            rollEnd = 1.4f / 1.8f - 0.05f,
        };

        const string InPlace = "Assets/Soulslike/Animation/Clips/";

        public static AnimatorController Build(string name, Spec spec, float runSpeed, out Timing[] timings, out Moves moves)
        {
            moves = new Moves();
            string path = $"Assets/Soulslike/Animation/{name}.controller";
            System.IO.Directory.CreateDirectory(InPlace);
            building = name;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            bool strafes = spec.walkLeft != null && spec.walkRight != null && spec.walkBack != null;
            bool drawnStrafes = spec.drawnLeft != null && spec.drawnRight != null && spec.drawnBack != null;
            if (strafes || drawnStrafes)
            {
                controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
                controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            }
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Roll", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            bool stances = spec.drawnIdle != null;
            if (stances)
            {
                controller.AddParameter("Drawn", AnimatorControllerParameterType.Float);
                controller.AddParameter("Draw", AnimatorControllerParameterType.Trigger);
                controller.AddParameter("Sheathe", AnimatorControllerParameterType.Trigger);
            }
            var machine = controller.layers[0].stateMachine;

            // Idle → walk → run by ground speed; the run is retimed to the
            // character's speed, so the feet do not slide.
            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree);
            float sprintSpeed = runSpeed * SprintFactor;
            if (strafes && !stances)
            {
                // Each gait where its velocity is: ahead (the walk at its own
                // speed, the run at the character's), back and the sides at the
                // character's speed (a commanded step is that fast), retimed.
                tree.blendType = BlendTreeType.FreeformDirectional2D;
                tree.blendParameter = "MoveX";
                tree.blendParameterY = "MoveZ";
                void Add(AnimationClip clip, Vector2 at, float timeScale)
                {
                    tree.AddChild(clip, at);
                    var children = tree.children;
                    children[^1].timeScale = timeScale;
                    tree.children = children;
                }
                foreach (var (clip, at, timeScale) in Gaits(spec.idle, spec.walk, spec.run, null, runSpeed, sprintSpeed))
                    Add(clip, new Vector2(0f, at), timeScale);
                foreach (var (step, way) in new[] { (spec.walkBack, Vector2.down), (spec.walkLeft, Vector2.left), (spec.walkRight, Vector2.right) })
                {
                    var clip = Clip(step);
                    Add(clip, way * runSpeed, runSpeed / Speed(clip));
                }
            }
            else if (!stances)
            {
                tree.blendType = BlendTreeType.Simple1D;
                tree.blendParameter = "Speed";
                tree.useAutomaticThresholds = false;
                foreach (var (clip, at, timeScale) in Gaits(spec.idle, spec.walk, spec.run, spec.sprint, runSpeed, sprintSpeed))
                {
                    tree.AddChild(clip, at);
                    var children = tree.children;
                    children[^1].timeScale = timeScale;
                    tree.children = children;
                }
            }
            else if (drawnStrafes)
            {
                // The stance (Drawn) chooses between two trees: sheathed by
                // speed alone; drawn by the velocity in the body's frame, ahead
                // the gaits, back and aside the steps, each at its own speed
                // and, as fast as the hero can go that way, retimed.
                tree.blendType = BlendTreeType.Simple1D;
                tree.blendParameter = "Drawn";
                tree.useAutomaticThresholds = false;
                var sheathed = tree.CreateBlendTreeChild(0f);
                sheathed.name = "Sheathed";
                sheathed.blendType = BlendTreeType.Simple1D;
                sheathed.blendParameter = "Speed";
                sheathed.useAutomaticThresholds = false;
                foreach (var (clip, at, timeScale) in Gaits(spec.idle, spec.walk, spec.run, spec.sprint, runSpeed, sprintSpeed))
                    Child(sheathed, clip, at, timeScale);
                var drawn = tree.CreateBlendTreeChild(1f);
                drawn.name = "Drawn";
                drawn.blendType = BlendTreeType.FreeformDirectional2D;
                drawn.blendParameter = "MoveX";
                drawn.blendParameterY = "MoveZ";
                foreach (var (clip, at, timeScale) in Gaits(spec.drawnIdle, spec.drawnWalk, spec.drawnRun, spec.drawnSprint, runSpeed, sprintSpeed))
                    Child(drawn, clip, new Vector2(0f, at), timeScale);
                float aside = runSpeed * SidewaysShare;
                foreach (var (slow, fast, way) in new[]
                {
                    (spec.drawnBack, spec.drawnBack, Vector2.down),
                    (spec.drawnLeft, spec.drawnLeftFast ?? spec.drawnLeft, Vector2.left),
                    (spec.drawnRight, spec.drawnRightFast ?? spec.drawnRight, Vector2.right),
                })
                {
                    var walk = Clip(slow);
                    float walkSpeed = Mathf.Min(Speed(walk), aside * 0.6f);
                    Child(drawn, walk, way * walkSpeed, 1f);
                    var quick = Clip(fast);
                    Child(drawn, quick, way * aside, aside / Speed(quick));
                }
            }
            else
            {
                // Speed across, the stance (Drawn 0 sheathed, 1 drawn) up: the
                // controller eases Drawn, so a stance blends into the other.
                tree.blendType = BlendTreeType.FreeformCartesian2D;
                tree.blendParameter = "Speed";
                tree.blendParameterY = "Drawn";
                foreach (var (stance, gaits) in new[]
                {
                    (0f, Gaits(spec.idle, spec.walk, spec.run, spec.sprint, runSpeed, sprintSpeed)),
                    (1f, Gaits(spec.drawnIdle, spec.drawnWalk, spec.drawnRun, spec.drawnSprint, runSpeed, sprintSpeed)),
                })
                    foreach (var (clip, at, timeScale) in gaits)
                    {
                        tree.AddChild(clip, new Vector2(at, stance));
                        var children = tree.children;
                        children[^1].timeScale = timeScale;
                        tree.children = children;
                    }
            }
            machine.defaultState = locomotion;

            // The attacks, one state each, picked by AttackIndex with the Attack trigger.
            var attackNames = new[] { spec.attack }.Concat(spec.attacks).ToArray();
            timings = new Timing[attackNames.Length];
            for (int i = 0; i < attackNames.Length; i++)
            {
                var clip = Clip(attackNames[i]);
                moves.attacks.Add(clip);
                float share = i == 0 && spec.attackHit > 0f ? spec.attackHit : HitMoment(originals[clip]);
                timings[i] = new Timing { hit = share * clip.length / spec.attackSpeed, length = clip.length / spec.attackSpeed };
                var state = OneShot(machine, "Attack " + i, clip, spec.attackSpeed, locomotion, 0.9f);
                var enter = machine.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                enter.AddCondition(AnimatorConditionMode.Equals, i, "AttackIndex");
                enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                enter.duration = 0.08f;
                enter.canTransitionToSelf = true;
            }
            var hit = OneShot(machine, "Hit", Clip(spec.hit), spec.hitSpeed, locomotion, 0.9f);
            var oneShots = new[] { (hit, "Hit") }.ToList();
            if (spec.roll != null)
                oneShots.Add((OneShot(machine, "Roll", Clip(spec.roll), spec.rollSpeed, locomotion, spec.rollEnd), "Roll"));
            foreach (var (jump, trigger) in new[] { (spec.jump, "Jump"), (spec.runJump, "RunJump") })
            {
                if (jump == null) continue;
                if (trigger == "RunJump") controller.AddParameter("RunJump", AnimatorControllerParameterType.Trigger);
                var clip = Clip(jump, grounded: true);
                moves.jumps.Add(JumpTiming(originals[clip]));
                oneShots.Add((OneShot(machine, trigger, clip, 1f, locomotion, 0.9f), trigger));
            }
            if (stances)
            {
                // Sheathing is drawing backwards: the pack has no sheathing clip.
                moves.draw = Clip(spec.draw);
                oneShots.Add((OneShot(machine, "Draw", moves.draw, 1f, locomotion, 0.9f), "Draw"));
                oneShots.Add((OneShot(machine, "Sheathe", Clip(spec.draw, reversed: true), 1f, locomotion, 0.9f), "Sheathe"));
            }
            var death = machine.AddState("Death");
            death.motion = Clip(spec.death);

            foreach (var (state, trigger) in oneShots)
            {
                var enter = machine.AddAnyStateTransition(state);
                enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                enter.duration = 0.08f;
                enter.canTransitionToSelf = true;
            }
            var die = machine.AddAnyStateTransition(death);
            die.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            die.duration = 0.1f;
            die.canTransitionToSelf = false;
            var rise = death.AddTransition(locomotion);
            rise.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            rise.hasExitTime = false;
            rise.duration = 0f;
            AssetDatabase.SaveAssets();
            return controller;
        }

        static void Child(BlendTree tree, Motion clip, float at, float timeScale)
        {
            tree.AddChild(clip, at);
            var children = tree.children;
            children[^1].timeScale = timeScale;
            tree.children = children;
        }

        static void Child(BlendTree tree, Motion clip, Vector2 at, float timeScale)
        {
            tree.AddChild(clip, at);
            var children = tree.children;
            children[^1].timeScale = timeScale;
            tree.children = children;
        }

        /// <summary>Idle, walk, run and sprint at their speeds on the blend; the
        /// run and sprint retimed to the character's, so the feet do not slide.</summary>
        static System.Collections.Generic.List<(AnimationClip clip, float at, float timeScale)> Gaits(
            string idle, string walking, string running, string sprinting, float runSpeed, float sprintSpeed)
        {
            var walk = Clip(walking);
            var run = Clip(running);
            var gaits = new System.Collections.Generic.List<(AnimationClip, float, float)>
            {
                (Clip(idle), 0f, 1f),
                (walk, Mathf.Min(Speed(walk), runSpeed * 0.5f), 1f),
                (run, runSpeed, runSpeed / Speed(run)),
            };
            if (sprinting != null)
            {
                var sprint = Clip(sprinting);
                gaits.Add((sprint, sprintSpeed, sprintSpeed / Speed(sprint)));
            }
            return gaits;
        }

        static AnimatorState OneShot(AnimatorStateMachine machine, string name, Motion clip, float speed, AnimatorState back, float exitTime)
        {
            var state = machine.AddState(name);
            state.motion = clip;
            state.speed = speed;
            var exit = state.AddTransition(back);
            exit.hasExitTime = true;
            exit.exitTime = exitTime;
            exit.duration = 0.15f;
            return state;
        }

        static readonly string[] Limbs =
        {
            "RightArm", "RightForeArm", "RightHand", "LeftArm", "LeftForeArm", "LeftHand",
            "RightUpLeg", "RightLeg", "RightFoot", "LeftUpLeg", "LeftLeg", "LeftFoot",
        };

        /// <summary>Where a blow lands, a share of the clip: the moment the
        /// arms and legs turn fastest (summed angular speed of their bones),
        /// between 15 % and 85 % (the wind-up and the recovery are slow).</summary>
        static float HitMoment(AnimationClip clip)
        {
            var bones = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, AnimationCurve>>();
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                string bone = binding.path.Split('/').Last();
                if (!Limbs.Any(limb => bone == "mixamorig:" + limb)) continue;
                if (!binding.propertyName.StartsWith("m_LocalRotation") && !binding.propertyName.StartsWith("localEulerAngles")) continue;
                if (!bones.TryGetValue(binding.path, out var curves)) bones[binding.path] = curves = new();
                curves[binding.propertyName] = AnimationUtility.GetEditorCurve(clip, binding);
            }
            Quaternion Rotation(System.Collections.Generic.Dictionary<string, AnimationCurve> c, float t)
            {
                if (c.TryGetValue("m_LocalRotation.w", out var w))
                    return new Quaternion(c["m_LocalRotation.x"].Evaluate(t), c["m_LocalRotation.y"].Evaluate(t), c["m_LocalRotation.z"].Evaluate(t), w.Evaluate(t));
                float E(string axis) => c.TryGetValue("localEulerAnglesRaw." + axis, out var e) ? e.Evaluate(t) : 0f;
                return Quaternion.Euler(E("x"), E("y"), E("z"));
            }
            const float step = 1f / 60f;
            float best = 0.45f, fastest = -1f;
            for (float t = clip.length * 0.15f; t < clip.length * 0.85f; t += step)
            {
                float speed = 0f;
                foreach (var curves in bones.Values)
                    if (curves.Count >= 3) speed += Quaternion.Angle(Rotation(curves, t), Rotation(curves, t + step));
                if (speed > fastest) { fastest = speed; best = t / clip.length; }
            }
            return best;
        }

        const float Rate = 60f;

        /// <summary>A jump clip's takeoff and landing from its hips: off the
        /// ground when they rise past their first height after the crouch,
        /// down when they sink below it again after the top.</summary>
        static (float takeoff, float air, float length) JumpTiming(AnimationClip clip)
        {
            var height = AnimationUtility.GetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("mixamorig:Hips", typeof(Transform), "m_LocalPosition.y"));
            if (height == null) return (0.15f, 0.67f, clip.length);
            float rest = height.Evaluate(0f), top = 0f, highest = float.MinValue;
            for (float t = 0f; t <= clip.length; t += 1f / Rate)
                if (height.Evaluate(t) > highest) { highest = height.Evaluate(t); top = t; }
            float takeoff = 0f, landing = clip.length;
            for (float t = top; t >= 0f; t -= 1f / Rate)
                if (height.Evaluate(t) <= rest) { takeoff = t; break; }
            for (float t = top; t <= clip.length; t += 1f / Rate)
                if (height.Evaluate(t) <= rest) { landing = t; break; }
            return (takeoff, landing - takeoff, clip.length);
        }

        /// <summary>Every curve of the clip played from its end to its start.</summary>
        static void Reverse(AnimationClip clip)
        {
            float length = clip.length;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                var back = new AnimationCurve();
                for (float t = 0f; t <= length + 1e-4f; t += 1f / Rate)
                    back.AddKey(Mathf.Min(t, length), curve.Evaluate(length - Mathf.Min(t, length)));
                AnimationUtility.SetEditorCurve(clip, binding, back);
            }
        }

        static readonly System.Collections.Generic.Dictionary<AnimationClip, AnimationClip> originals = new();

        /// <summary>The Mixamo clip an in-place copy was made from (its root motion kept).</summary>
        public static AnimationClip Original(AnimationClip clip) => originals.TryGetValue(clip, out var source) ? source : clip;
        /// <summary>The controller being built: its clips are its own copies,
        /// so that two characters sharing a clip do not overwrite each other's.</summary>
        static string building = "";

        /// <summary>The speed a clip was authored at: its root motion, m/s
        /// (read from the original: the in-place copy has none).</summary>
        static float Speed(AnimationClip clip)
        {
            var source = originals.TryGetValue(clip, out var original) ? original : clip;
            return Mathf.Max(0.1f, new Vector2(source.averageSpeed.x, source.averageSpeed.z).magnitude);
        }

        /// <summary>A Mixamo clip played in place, as Mixamo's "In Place": a
        /// copy with the hips' steady drift across the ground (the line from the
        /// first frame to the last) taken out; their bounce and sway stay.
        /// Without it the hips ran ahead of the body and snapped back every loop.
        /// <paramref name="grounded"/> (a jump): the hips' rise above their first
        /// height is taken out too, the crouches stay; the controller lifts the
        /// body, or the jump would count twice.
        /// <paramref name="reversed"/>: played backwards (sheathing from drawing).</summary>
        static AnimationClip Clip(string name, bool grounded = false, bool reversed = false)
        {
            var source = AssetDatabase.LoadAllAssetsAtPath(Package + name + ".fbx")
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (source == null)
            {
                Debug.LogError($"Mixamo clip missing: {Package}{name}.fbx");
                return null;
            }
            var copy = Object.Instantiate(source);
            copy.name = $"{building} {name.Split('/').Last()}{(reversed ? " backwards" : "")}";
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                if (grounded && binding.propertyName == "m_LocalPosition.y" && binding.path.EndsWith("mixamorig:Hips"))
                {
                    var height = AnimationUtility.GetEditorCurve(source, binding);
                    float rest = height.Evaluate(0f);
                    var flat = new AnimationCurve();
                    // Resampled: clamping the keys alone leaves overshooting tangents.
                    for (float t = 0f; t <= source.length + 1e-4f; t += 1f / 60f)
                        flat.AddKey(t, Mathf.Min(height.Evaluate(t), rest));
                    AnimationUtility.SetEditorCurve(copy, binding, flat);
                    continue;
                }
                bool across = binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z";
                if (!across || !binding.path.EndsWith("mixamorig:Hips")) continue;
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                float length = Mathf.Max(source.length, 1e-3f);
                float slope = (curve.Evaluate(source.length) - curve.Evaluate(0f)) / length;
                var keys = curve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i].value -= slope * keys[i].time;
                    keys[i].inTangent -= slope;
                    keys[i].outTangent -= slope;
                }
                curve.keys = keys;
                AnimationUtility.SetEditorCurve(copy, binding, curve);
            }
            if (reversed) Reverse(copy);
            var path = InPlace + copy.name + ".anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            originals[copy] = source;
            return copy;
        }
    }
}

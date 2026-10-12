using System.Linq;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Soulslike.Training
{
    /// <summary>
    /// A person's hands on a duellist (the arena viewer's <c>-play</c>,
    /// training/play.sh), as games are played: the same body, blows, energy
    /// and adrenaline as the agent's, but its speeds set at once (W held,
    /// full speed ahead; none held, standing), not by the agent's inertia. A
    /// blow pressed waits for the next decision it is free at
    /// (FighterAgent.Heuristic asks this).
    ///
    /// The mouse turns the camera about it, and it keeps turning to face the
    /// camera's way (as fast as its turning goes); W and S ahead and back, A
    /// and D aside, its own way.
    ///
    /// The left button three blows of what is in hand (its <see
    /// cref="Chains"/> by name, else its three quickest led by the hand or
    /// the weapon), always in that order, one a press (a combo;
    /// from the first again after <see cref="ChainPause"/> s without a
    /// press); the right one heavy blow, its longest; F the quickest kick.
    /// Shift a quick move the way WASD points (back without), Space the roll.
    /// </summary>
    public class HumanFighter : MonoBehaviour
    {
        public Fighter body;
        /// <summary>Degrees the camera turns per pixel of mouse travel.</summary>
        public float mouseDegrees = 0.15f;
        /// <summary>The camera behind it and up from its head, m.</summary>
        public float cameraBack = 4f, cameraUp = 1.6f;
        float yaw = float.NaN, pitch = 15f;

        int pending = -1;
        Weapon listedFor = (Weapon)(-1);
        /// <summary>A pause, s, after which the left button's chain begins anew.</summary>
        public const float ChainPause = 1.5f;
        /// <summary>The left button's blows by weapon, by name (one missing
        /// leaves the quickest three).</summary>
        public static readonly string[][] Chains =
        {
            null,
            new[] { "great_sword_slash", "sword_and_shield_slash_5", "stable_sword_outward_slash" },
            null,
        };
        int[] chain = { }, moves = { };
        int heavy = -1, kick = -1, link;
        float lastLight = -99f;

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null || PauseMenu.Open) return;
            if (listedFor != body.Held) List();
            if (float.IsNaN(yaw)) yaw = body.transform.eulerAngles.y;
            var look = mouse.delta.ReadValue();
            yaw += look.x * mouseDegrees;
            pitch = Mathf.Clamp(pitch - look.y * mouseDegrees, -10f, 60f);
            Steer(Walk(keyboard));
            if (mouse.leftButton.wasPressedThisFrame) pending = Light();
            else if (mouse.rightButton.wasPressedThisFrame) pending = heavy;
            else if (keyboard.fKey.wasPressedThisFrame) pending = kick;
            else if (keyboard.spaceKey.wasPressedThisFrame) pending = body.RollAction;
            else if (keyboard.leftShiftKey.wasPressedThisFrame) pending = Move(keyboard);
        }

        /// <summary>The blows for what is in hand: the light chain, the heavy
        /// blow, the kick; and the quick moves.</summary>
        void List()
        {
            listedFor = body.Held;
            var open = Enumerable.Range(0, body.Attacks)
                .Where(i => body.strikes[i] && body.Usable(i, body.Held))
                .OrderBy(i => body.attackLength[i]).ToArray();
            // With a weapon, its own blows; bare, the hands'. A blow whose
            // other fast limb is a foot (a boxing kick, measured as led by its
            // swinging arm) is a kick.
            bool Kick(int i) => body.strikeLimb[i] >= 2 || i < body.strikeLimb2.Length && body.strikeLimb2[i] >= 2;
            var led = open.Where(i => !Kick(i) && body.attackWeapon[i] == body.Held).ToArray();
            var named = Chains[(int)body.Held]?.Select(n => System.Array.IndexOf(body.attackNames, n)).ToArray();
            chain = named != null && named.All(i => i >= 0) ? named : led.Take(3).ToArray();
            heavy = led.Length > 0 ? led[led.Length - 1] : -1;
            kick = open.Where(Kick).DefaultIfEmpty(-1).First();
            moves = Enumerable.Range(0, body.Attacks).Where(i => !body.strikes[i]).ToArray();
            link = 0;
        }

        /// <summary>The light chain's next blow (its first again after a
        /// pause); the chain moves on only when it starts (Fill), so presses
        /// while busy skip nothing.</summary>
        int Light()
        {
            if (chain.Length == 0) return -1;
            if (Time.time - lastLight > ChainPause) link = 0;
            return chain[link % chain.Length];
        }

        /// <summary>The quick move whose way best matches WASD's (back without).</summary>
        int Move(Keyboard keyboard)
        {
            var way = Walk(keyboard);
            if (way.sqrMagnitude < 0.01f) way = Vector2.down;
            return moves.Length == 0 ? -1 : moves.OrderByDescending(i =>
                Vector2.Dot(new Vector2(body.travelSide[i], body.travelForward[i]).normalized, way.normalized)).First();
        }

        static Vector2 Walk(Keyboard keyboard) => new Vector2(
            (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
            (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));

        /// <summary>The speeds, at once: WASD its own way, turning to the camera's.</summary>
        void Steer(Vector2 walk)
        {
            var motion = body.motion;
            motion.forward = walk.y;
            motion.side = walk.x;
            float angle = Mathf.DeltaAngle(body.transform.eulerAngles.y, yaw);
            motion.turn = Mathf.Clamp(angle / 30f, -1f, 1f);
            body.motion = motion;
        }

        /// <summary>The camera behind it where the mouse turned it, over its head.</summary>
        void LateUpdate()
        {
            if (float.IsNaN(yaw)) return;
            var view = Camera.main;
            if (view == null) return;
            var turn = Quaternion.Euler(pitch, yaw, 0f);
            var head = body.transform.position + Vector3.up * cameraUp * body.Size;
            var place = head - turn * Vector3.forward * cameraBack;
            // A trunk or a wall between: the camera comes in front of it.
            var reach = place - head;
            if (Physics.SphereCast(head, 0.3f, reach.normalized, out var hit, reach.magnitude, 1 << 0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<Fighter>() == null)
                place = head + reach.normalized * Mathf.Max(0.3f, hit.distance - 0.1f);
            view.transform.SetPositionAndRotation(place, Quaternion.LookRotation(head - place));
        }

        /// <summary>This decision's choices: the speeds kept (Steer sets them
        /// each frame); the blow pressed, once it may start.</summary>
        public void Fill(ActionSegment<int> d)
        {
            d[0] = d[1] = d[2] = 0;
            d[3] = 0;
            if (pending >= 0 && body.Allowed(pending))
            {
                d[3] = pending + 1;
                if (chain.Length > 0 && pending == chain[link % chain.Length])
                {
                    link++;
                    lastLight = Time.time;
                }
                pending = -1;
            }
        }
    }
}

using System;
using UnityEngine;

namespace Soulslike.Training
{
    /// <summary>
    /// A duellist of the training arena, driven by an agent (FighterAgent),
    /// the same body and blows on both sides. Moves by its speeds (Inertia:
    /// ahead, aside, turning; nothing turns it to the other by itself), and
    /// may leave its turning to focus on the other (Inertia.focus), and
    /// acts by index: one of its attacks (those of its weapon and those of
    /// bare hands) or the roll. A blow is the striking point — the blade, the
    /// hammer's head, or the fist, foot or shin — swept through the clip's
    /// middle phases against the other's hurtboxes; it does damage by that point's
    /// speed on contact. A hit staggers: the other's action is lost. Blows and
    /// rolls spend energy (<see cref="energySpent"/>), which the agent pays
    /// for; moving and turning are free.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public class Fighter : MonoBehaviour
    {
        /// <summary>The layer of the bodies' hurtboxes (triggers on the bones,
        /// LevelBuilder.Hurtboxes), which blows are swept against.</summary>
        public const int HurtboxLayer = 27;

        /// <summary>Its hurtboxes a sword's blade does not hurt while it is
        /// bare-handed: the forearms (elbow to hand) and the shins (knee to
        /// foot), its guard against the sword (LevelBuilder.DuelBody). Other
        /// blows land on them, and with a weapon in hand the blade does too.</summary>
        public Collider[] swordProof = { };

        public Fighter opponent;
        /// <summary>Its blows land on any fighter (the game's hero, among many);
        /// else only on its opponent (a creature does not strike its kind).</summary>
        public bool strikesAnyone;
        public Animator animator;

        [Header("Blows, measured by the builder")]
        /// <summary>Per action of the animator (AttackIndex: the blows, then the
        /// quick moves): what it needs in hand (bare hands: the limbs strike),
        /// or whether anyone may take it (<see cref="attackCommon"/>); whether it strikes at all (a
        /// quick move does not); its window (s from its start), its length (s),
        /// the limb that leads it (into <see cref="limbs"/>: right hand, left
        /// hand, right foot, left foot); and the way its clip carries the body
        /// over its length, m at 1.8 m tall, ahead and to the right.</summary>
        public Weapon[] attackWeapon = { };
        public bool[] attackCommon = { };
        public bool[] strikes = { };
        /// <summary>Each action's clip by name (its pack's folder left off),
        /// for a person's controls to pick blows by (HumanFighter).</summary>
        public string[] attackNames = { };
        public float[] strikeStart = { }, strikeEnd = { }, attackLength = { };
        public int[] strikeLimb = { };
        public float[] travelForward = { }, travelSide = { };
        /// <summary>A bare-handed blow's other striking limb, when it too moves
        /// fast (a kick's swinging arm, a punch's step); -1 none.</summary>
        public int[] strikeLimb2 = { };
        public Transform[] limbs = { };
        /// <summary>What of each limb strikes bare: from <see cref="limbs"/>
        /// (the hand, the foot) to its tip (the knuckles, the toes), and for a
        /// foot also the shin, from the knee down to it.</summary>
        public Transform[] limbTips = { }, knees = { };

        /// <summary>Where the agent's grid is centred (FighterAgent's
        /// GridSensorComponent sits on it): <see cref="eyesAhead"/> m before the
        /// body's hips (<see cref="Ground"/>), as the controller faces, unscaled by its size (a grid on the scaled body spread its cells
        /// apart for the tall and crowded them for the short, its boxes not).</summary>
        public Transform eyes;
        public float eyesAhead = 2f;
        /// <summary>The rig's hips: where the body really is while a clip
        /// carries it off its controller's line (a spin, a cartwheel).</summary>
        public Transform hips;
        /// <summary>Where the body stands, on the ground under its hips (its
        /// controller's place without a rig).</summary>
        public Vector3 Ground => hips != null ? new Vector3(hips.position.x, transform.position.y, hips.position.z) : transform.position;
        /// <summary>The rig's head: one of the points the agents watch (<see cref="Watched"/>).</summary>
        public Transform head;
        /// <summary>A body's height at <see cref="Size"/> 1, m.</summary>
        public const float Tall = 1.8f;
        /// <summary>Into <see cref="limbs"/>: the left hand, the right, the left foot, the right.</summary>
        static readonly int[] WatchedLimbs = { 1, 0, 3, 2 };

        /// <summary>Where a point the agents watch is (Fight.Points): 0 the
        /// hips, 1 the head, then the left and right hand's knuckles, the left
        /// and right foot's toes; the hips where the rig has none.</summary>
        public Vector3 Watched(int point)
        {
            Transform at = null;
            if (point == 1) at = head;
            else if (point >= 2)
            {
                int limb = WatchedLimbs[point - 2];
                at = limb < limbTips.Length && limbTips[limb] != null ? limbTips[limb] : limb < limbs.Length ? limbs[limb] : null;
            }
            return at != null ? at.position : hips != null ? hips.position : transform.position;
        }

        [Header("Weapons")]
        public GameObject sword, hammer;
        /// <summary>Each weapon's striking part, from its base to its end.</summary>
        public Transform swordBase, swordTip, hammerBase, hammerHead;

        [Header("Body, at 1.8 m (scaled with the size)")]
        /// <summary>The most speed, m/s, ahead; to a side and back, shares of
        /// it (the agent's speeds are the same either way, -1 to 1: these
        /// make its real pace).</summary>
        public float speed = 4f, turnSpeed = 360f, sideways = 0.65f, backwards = 0.6f;
        /// <summary>The roll's length at its own speed, s.</summary>
        public float rollLength = 0.9f;
        public float limbRadius = 0.07f, swordRadius = 0.05f, hammerRadius = 0.1f;
        /// <summary>Half the hammer head's width, across the handle, m; and
        /// the share of the handle next to the head that strikes too, and its
        /// radius (the head alone was too hard to land).</summary>
        public float hammerHalfWidth = 0.17f, hammerShaft = 0.5f, shaftRadius = 0.06f;
        /// <summary>The striking point's speed at which a blow does 120 % of
        /// its damage (Balance): the speed counts within 80–120 % (a sweeping
        /// blow is not worth several quick ones). Then the combo's factor
        /// (<see cref="ComboFactor"/>) and the adrenaline's
        /// (<see cref="LevelFactor"/>) multiply it.</summary>
        public float handsSpeed = 8f, swordSpeed = 10f, hammerSpeed = 9f;

        /// <summary>Each action's (the roll last) damage, speed and stagger
        /// with the weapon held, from the balance (Balance): worked out again
        /// when either changes.</summary>
        float[] damageOf = { }, speedOf = { }, staggerOf = { };
        int balanceVersion = -1;
        Weapon balanceHeld;
        static int warnedVersion = -1;

        [Header("Energy")]
        /// <summary>Any action's cost per second of its clip, whatever strikes
        /// (Act). Moving costs nothing (it costing a little did not help them
        /// move).</summary>
        public float blowCost = 0.01f;
        /// <summary>From adrenaline level A on, an action costs this share of
        /// its energy. At 0.2 a fight's blows cost more than standing to the
        /// time-out, and they stopped meeting.</summary>
        public const float AdrenalineEnergy = 0.05f;

        public Inertia motion;
        /// <summary>Energy spent since the agent last took it.</summary>
        public float energySpent;

        public Health Health { get; private set; }
        public Weapon Held { get; private set; }
        /// <summary>Height over 1.8 m.</summary>
        public float Size { get; private set; } = 1f;
        public int Attacks => attackWeapon.Length;
        /// <summary>Attacks, then the roll.</summary>
        public int Actions => Attacks + 1;
        public int RollAction => Attacks;
        /// <summary>What it is doing (-1 nothing, else an action) and how far in (0–1).</summary>
        public int Doing => current;
        public float Progress => current < 0 ? 0f : Mathf.Clamp01(clock / Length(current));
        public bool Staggered => stagger > 0f;
        /// <summary>Whether no blow hurts it now: inside its roll's or quick
        /// move's window (<see cref="Balance.Evade"/>, shares of the action).
        /// A blow touching it then is evaded (<see cref="Evaded"/>).</summary>
        public bool Untouchable => stagger <= 0f && current >= 0 && Evades(current)
            && Progress >= Balance.Current.Evade.from && Progress < Balance.Current.Evade.to;

        /// <summary>Whether an action is an evade: the roll or a quick move (no blow).</summary>
        bool Evades(int action) => action == RollAction || action < strikes.Length && !strikes[action];

        /// <summary>Whether its blow may still be dropped for an evade: not
        /// past <see cref="Balance.Window.cancel"/> of its length (its wind-up,
        /// before it can strike), and not staggered. Its energy stays spent.</summary>
        bool Cancellable => stagger <= 0f && current >= 0 && !Evades(current)
            && Progress < Balance.Current.Evade.cancel;
        /// <summary>Busy till its action reaches its <see cref="FreePhase"/>,
        /// or (a blow that landed, <see cref="AfterHit.Cancel"/>) till <see
        /// cref="CancelAfter"/> s past the hit, whichever is sooner: then any
        /// action may cut the rest of it short.</summary>
        public bool Busy => stagger > 0f || current >= 0 && clock < Mathf.Min(cancelFrom, FreeFrom(current));
        public const float CancelAfter = 0.1f;
        /// <summary>The phase (of <see cref="Fight.Phases"/>, from 0) from
        /// which any action — a blow, the roll, a quick move — may begin
        /// without waiting for the one in progress to end: the 4th of 5, the
        /// last of a blow's striking ones.</summary>
        public const int FreePhase = 3;
        float FreeFrom(int action) => Length(action) * FreePhase / Fight.Phases;
        /// <summary>Doing nothing and not staggered: its action played to its
        /// end (a flask, a change of weapon wait for it, not for <see cref="Busy"/>).</summary>
        public bool Idle => stagger <= 0f && current < 0;
        /// <summary>A landed blow's recovery, the rest of its clip after it
        /// lands: played as it is, played <see cref="FasterRecovery"/> times
        /// as fast, or giving way to the next action; so that a hit opens a
        /// combo and a miss pays in full (to its <see cref="FreePhase"/>,
        /// where any action may cut it short).</summary>
        public enum AfterHit { Full, Faster, Cancel }
        public const float FasterRecovery = 1.5f;
        /// <summary>The recovery after a hit (the editor's combo demo compares them).</summary>
        public static AfterHit Recovery = AfterHit.Faster;
        float pace = 1f;

        /// <summary>A blow's clip may land again (a combo's clip strikes more
        /// than once): <see cref="Rehit"/> s after a hit it touches nothing,
        /// then strikes anew; each of its hits continues the combo.</summary>
        public const float Rehit = 0.5f;

        /// <summary>Each hit as it lands, or a blow as its clip ends having missed: landed or not, and the damage.
        /// <see cref="Combo"/> is already counted by then.</summary>
        public event Action<bool, float> Struck;
        /// <summary>Each frame a striking part is open: its path's segment
        /// (from its base to its end) and radius; and a blow landing, where.
        /// For the editor's combo demo (ComboDemoView).</summary>
        public event Action<Vector3, Vector3, float> Swept;
        public event Action<Vector3> Hit;
        /// <summary>A sword's blow met the other's hammer (<see cref="Guard"/>),
        /// where: it strikes no more (its clip plays on; told as a miss).</summary>
        public event Action<Vector3> Blocked;
        /// <summary>An action begun (an attack's index, or <see cref="RollAction"/>).</summary>
        public event Action<int> Acted;

        /// <summary>The weapon's striking capsules while its blow is open: each
        /// from where it was last frame (a, b) to where it is now (a2, b2), and
        /// its radius; <see cref="Blades"/> of them this frame (the sword one,
        /// the hammer its head and shaft), none bare-handed. For the stats'
        /// weapons crossing (DuelEpisode).</summary>
        public struct BladePath
        {
            public Vector3 a, b, a2, b2;
            public float radius;
        }
        public readonly BladePath[] Blade = new BladePath[2];
        public int Blades { get; private set; }
        /// <summary>The hammer's head and the upper half of its shaft, as its
        /// blows strike (the whole shaft stopped a quarter of the sword's
        /// blows, and the hammer won two in three), every frame, in a blow or
        /// not: a shield against a sword. A sword's open blade touching it (between the
        /// frames too) strikes no more in that blow (<see cref="Blocked"/>); bare
        /// limbs pass it. <see cref="Guards"/> of them, none with another weapon.</summary>
        public readonly BladePath[] Guard = new BladePath[2];
        public int Guards { get; private set; }
        /// <summary>Blows of the weapon held begun, of them cut short by a
        /// blow taken, and of them blocked (a sword's by a hammer), since the
        /// episode began (stats).</summary>
        public int WeaponBlows, WeaponBlowsCut, WeaponBlowsBlocked;

        /// <summary>A combo: blows landing one after another, each a different
        /// one from the last, each within <see cref="ComboWindow"/> s of the
        /// last; a miss, a blow taken or the time running out ends it.
        /// <see cref="Combo"/> is its length (0 none, 1 a first hit), and
        /// <see cref="SinceLanded"/> the time since its last hit.</summary>
        public const float ComboWindow = 2.5f;
        public int Combo { get; private set; }

        /// <summary>The damage factor of a combo's n-th hit: 20 % more each,
        /// with no end (100, 120, 140, 160 %…).</summary>
        public static float ComboFactor(int n) => 1f + 0.2f * (Mathf.Max(1, n) - 1);

        /// <summary>
        /// Adrenaline, as a level (0, A, B, C, D, S: 0–5): +1 while the other is
        /// within <see cref="ZoneReach"/> m (between the bodies; at once, in and
        /// out), and +1 for each whole point gathered: a hit given gives damage ×
        /// <see cref="HitPoints"/>, a blow evaded <see cref="EvadePoints"/> (a
        /// hit taken nothing: trading blows must not lift both).
        /// Gathered points hold <see cref="Hold"/> s after the last, then fall
        /// <see cref="Decay"/> a second. Its level multiplies the damage this
        /// body does (<see cref="LevelFactor"/>), and from A on actions cost
        /// <see cref="AdrenalineEnergy"/> of their energy.
        /// </summary>
        public const float ZoneReach = 3f, HitPoints = 1f / 30f, EvadePoints = 0.7f, Hold = 1f, Decay = 0.35f;
        public static readonly float[] LevelFactor = { 1f, 1.1f, 1.25f, 1.4f, 1.6f, 2f };
        public const int Levels = 6;
        public float Gathered { get; private set; }
        public int Level => Mathf.Min(Levels - 1,
            (opponent != null && Gap() <= ZoneReach ? 1 : 0)
            + Mathf.FloorToInt(Gathered));
        /// <summary>The adrenaline as one number, 0–1: the level with the
        /// points towards the next (the agents see it), 1 at S.</summary>
        public float Adrenaline => Mathf.Clamp01(((opponent != null && Gap() <= ZoneReach ? 1 : 0) + Gathered) / (Levels - 1));
        /// <summary>The level has risen, to this.</summary>
        public event Action<int> LevelRose;
        float hold;
        int lastLevel;

        void Pump(float points)
        {
            Gathered += points;
            hold = Hold;
        }

        /// <summary>A blow evaded: it touched the other inside its evade's
        /// window (<see cref="Untouchable"/>), or a hammer blocked it. Raised by
        /// the striker; the other gains <see cref="EvadePoints"/>.</summary>
        public event Action Evaded;
        /// <summary>A blow dropped in its wind-up for an evade (<see cref="Cancellable"/>).</summary>
        public event Action Cancelled;
        public float SinceLanded { get; private set; } = ComboWindow;
        int lastLanded = -1;

        CharacterController body;
        int current = -1;
        float clock, stagger, gravity, damageDone, cancelFrom = float.MaxValue;
        /// <summary>Landed at all in this clip; how often; a hit not yet told;
        /// the miss told; till when it touches nothing, s.</summary>
        /// <summary>This blow met a hammer (blocked) or a body in its evade (evaded): it strikes no more.</summary>
        bool landed, fresh, reported, blocked, evaded;
        /// <summary>This blow went past its wind-up (counted in <see cref="WeaponBlows"/>; one dropped before is not).</summary>
        bool counted;
        int hits;
        float rearm;
        Trace lead, follow, leadShin, followShin, shaft;
        Vector3 rollWay;
        /// <summary>The hurtboxes a sweep touches (both bodies have ~15).</summary>
        readonly Collider[] touched = new Collider[32];

        void Awake()
        {
            body = GetComponent<CharacterController>();
            Health = GetComponent<Health>();
        }

        /// <summary>Its height and weapon for an episode.</summary>
        public void Equip(Weapon weapon, float height)
        {
            Held = weapon;
            Size = height / Tall;
            transform.localScale = Vector3.one * Size;
            if (sword != null) sword.SetActive(weapon == Weapon.Sword);
            if (hammer != null) hammer.SetActive(weapon == Weapon.Hammer);
            if (eyes != null)
            {
                eyes.localScale = Vector3.one / Size;
                eyes.localPosition = Vector3.forward * eyesAhead / Size;
            }
            Rebalance();
        }

        public void ResetAt(Vector3 position, float yaw)
        {
            body.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            body.enabled = true;
            Health.Restore();
            Health.Invulnerable = false;
            current = -1;
            stagger = 0f;
            gravity = 0f;
            Combo = 0;
            SinceLanded = ComboWindow;
            lastLanded = -1;
            Gathered = 0f;
            hold = 0f;
            lastLevel = 0;
            motion.Stop();
            motion.focus = false;
            energySpent = 0f;
            Blades = Guards = 0;
            WeaponBlows = WeaponBlowsCut = WeaponBlowsBlocked = 0;
            Rebalance();
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }

        /// <summary>Each action's damage, speed and stagger, from the balance
        /// (Balance), if it or the weapon held has changed: as it takes up a
        /// weapon or starts anew, so that a fight keeps one balance. What strikes is
        /// as <see cref="Sweeps"/> has it: the weapon held in its own blows,
        /// a limb in a kick or a bare-handed one.</summary>
        void Rebalance()
        {
            var balance = Balance.Current;
            if (balanceVersion == Balance.Version && balanceHeld == Held && speedOf.Length == Actions) return;
            balanceVersion = Balance.Version;
            balanceHeld = Held;
            damageOf = new float[Actions];
            speedOf = new float[Actions];
            staggerOf = new float[Actions];
            for (int i = 0; i < Actions; i++)
            {
                string strikesWith = null;
                if (i < Attacks && strikes[i])
                {
                    var with = strikeLimb[i] >= 2 || attackWeapon[i] == Weapon.Hands ? Weapon.Hands : Held;
                    strikesWith = with == Weapon.Hands ? "limb" : Balance.Name(with);
                }
                (damageOf[i], speedOf[i], staggerOf[i]) = balance.Of(Held, i == RollAction ? "roll" : attackNames[i], strikesWith);
            }
            if (warnedVersion == Balance.Version) return;
            warnedVersion = Balance.Version;
            foreach (var rule in balance.Rules)
                if (!string.IsNullOrEmpty(rule.blow) && rule.blow != "roll" && Array.IndexOf(attackNames, rule.blow) < 0)
                    Debug.LogWarning($"Soulslike: the balance names no such animation: {rule.blow}");
        }

        /// <summary>Starts an action if it may; whether it did.</summary>
        public bool Allowed(int action) =>
            !Health.Dead && action >= 0 && action <= RollAction && Usable(action, Held)
            && (!Busy || Cancellable && Evades(action));

        /// <summary>Whether a fighter holding this may take the action: the
        /// roll and the common ones anyone, the rest only with what they need.</summary>
        public bool Usable(int action, Weapon held) =>
            action == RollAction || action < attackCommon.Length && attackCommon[action] || attackWeapon[action] == held;

        public bool Act(int action)
        {
            if (!Allowed(action)) return false;
            // Allowed while busy: an evade dropping a blow in its wind-up.
            if (Busy) Cancelled?.Invoke();
            // A blow cut short from its free phase on, having missed: told as
            // a miss, as at its clip's end.
            else if (current >= 0 && current < Attacks && strikes[current] && counted && !landed && !reported)
            {
                reported = true;
                Combo = 0;
                Struck?.Invoke(false, 0f);
            }
            if (balanceHeld != Held || speedOf.Length != Actions) Rebalance();
            float cost = Level >= 1 ? AdrenalineEnergy : 1f;
            current = action;
            clock = 0f;
            cancelFrom = float.MaxValue;
            pace = speedOf[action];
            if (action == RollAction)
            {
                var way = transform.forward * motion.forward + transform.right * motion.side;
                rollWay = way.sqrMagnitude > 0.01f ? way.normalized : transform.forward;
                transform.rotation = Quaternion.LookRotation(rollWay);
                motion.Stop();
                animator?.SetTrigger("Roll");
                energySpent += blowCost * rollLength / pace * cost;
                Acted?.Invoke(action);
                return true;
            }
            // Any action stops the speeds (Inertia): the only way to stand.
            motion.Stop();
            landed = fresh = reported = blocked = evaded = false;
            hits = 0;
            rearm = 0f;
            // Every striking part starts its path anew (a part left on swept
            // from where the last action left it).
            lead.on = follow.on = leadShin.on = followShin.on = shaft.on = false;
            damageDone = 0f;
            counted = false;
            animator?.SetInteger("AttackIndex", action);
            animator?.SetTrigger("Attack");
            // By the clip's length alone, whatever strikes.
            energySpent += blowCost * attackLength[action] / pace * cost;
            Acted?.Invoke(action);
            return true;
        }

        /// <summary>A blow landed on this body: damage, and the action in
        /// progress lost to a stagger of <paramref name="daze"/> s.</summary>
        public bool TakeHit(float damage, float daze)
        {
            if (!Health.Damage(damage)) return false;
            if (current >= 0 && current != RollAction && Held != Weapon.Hands && attackWeapon[current] == Held)
                WeaponBlowsCut++;
            if (Health.Dead)
            {
                animator?.SetBool("Dead", true);
                current = -1;
                return true;
            }
            current = -1;
            Health.Invulnerable = false;
            stagger = daze;
            Combo = 0;
            animator?.SetTrigger("Hit");
            return true;
        }

        /// <summary>The ground between this body and the other's, m: between
        /// their hips (where the bodies are, not where a clip has left their
        /// controllers), less the controllers' radii.</summary>
        float Gap()
        {
            var line = opponent.Ground - Ground;
            line.y = 0f;
            return line.magnitude - (body.radius * Size + opponent.body.radius * opponent.Size);
        }

        float Length(int action) => action == RollAction ? rollLength : attackLength[action];

        void Update()
        {
            bool guarded = Guards > 0;
            Blades = Guards = 0;
            if (Health.Dead) return;
            float dt = Time.deltaTime;
            SinceLanded = Mathf.Min(ComboWindow, SinceLanded + dt);
            hold -= dt;
            if (hold <= 0f) Gathered = Mathf.Max(0f, Gathered - Decay * dt);
            int level = Level;
            if (level > lastLevel) LevelRose?.Invoke(level);
            lastLevel = level;
            if (SinceLanded >= ComboWindow) Combo = 0;
            var move = Vector3.zero;
            if (stagger > 0f) stagger -= dt;
            else if (current == RollAction)
            {
                clock += dt * pace;
                float t = clock / rollLength;
                // Untouchable in its evade's window (Untouchable); else it saves
                // only by taking the body (and its hurtboxes) out of the blow's way.
                move = rollWay * Mathf.Lerp(5f, 1.5f, t) * Size * pace;
                if (t >= 1f) current = -1;
            }
            else if (current >= 0)
            {
                clock += dt * pace;
                if (strikes[current])
                {
                    if (!counted && Progress >= Balance.Current.Evade.cancel)
                    {
                        counted = true;
                        if (Held != Weapon.Hands && attackWeapon[current] == Held) WeaponBlows++;
                    }
                    Sweeps(dt);
                    // Told as it lands, or as its clip ends having missed.
                    rearm -= dt;
                    if (fresh)
                    {
                        fresh = false;
                        Combo = Combo > 0 && (hits > 1 || current != lastLanded) ? Combo + 1 : 1;
                        lastLanded = current;
                        SinceLanded = 0f;
                        if (hits == 1)
                        {
                            if (Recovery == AfterHit.Cancel) cancelFrom = clock + CancelAfter;
                            else if (Recovery == AfterHit.Faster) pace = FasterRecovery * speedOf[current];
                        }
                        Struck?.Invoke(true, damageDone);
                    }
                    else if (!landed && !reported && clock >= attackLength[current])
                    {
                        reported = true;
                        Combo = 0;
                        Struck?.Invoke(false, 0f);
                    }
                }
                // The clip carries the body its own way (played in place, so
                // the body moves the line its root took, evenly).
                float length = Mathf.Max(attackLength[current], 0.01f);
                move = (transform.forward * travelForward[current] + transform.right * travelSide[current]) * (Size / length * pace);
                if (clock >= attackLength[current]) current = -1;
            }
            else
            {
                if (motion.focus && opponent != null)
                {
                    // To the other, at the most turning speed.
                    var to = opponent.Ground - transform.position;
                    to.y = 0f;
                    float angle = to.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(transform.forward, to, Vector3.up) : 0f;
                    transform.Rotate(0f, Mathf.Clamp(angle, -turnSpeed * dt, turnSpeed * dt), 0f);
                }
                else transform.Rotate(0f, motion.turn * turnSpeed * dt, 0f);
                // Ahead and aside at once no faster than either alone (the
                // two together at most 1), then each by its direction's pace.
                var way = Vector2.ClampMagnitude(new Vector2(motion.side, motion.forward), 1f);
                move = (transform.forward * way.y * (way.y < 0f ? backwards : 1f)
                        + transform.right * way.x * sideways) * speed * Size;
            }
            gravity = body.isGrounded ? -2f : gravity - 20f * dt;
            if (body.enabled) body.Move((move + Vector3.up * gravity) * dt);
            if (current < 0) pace = 1f;
            if (animator != null)
            {
                animator.speed = current >= 0 ? pace : 1f;
                var local = transform.InverseTransformDirection(new Vector3(move.x, 0f, move.z)) / Size;
                bool still = current >= 0 || stagger > 0f;
                animator.SetFloat("Speed", still ? 0f : local.magnitude, 0.1f, dt);
                animator.SetFloat("MoveX", still ? 0f : local.x, 0.1f, dt);
                animator.SetFloat("MoveZ", still ? 0f : local.z, 0.1f, dt);
            }
            if (Held == Weapon.Hammer && hammerHead != null)
            {
                var across = hammerHead.right * hammerHalfWidth * Size;
                Keep(ref Guard[0], hammerHead.position - across, hammerHead.position + across, hammerRadius * Size, guarded);
                Keep(ref Guard[1], Vector3.Lerp(hammerHead.position, hammerBase.position, hammerShaft), hammerHead.position, shaftRadius * Size, guarded);
                Guards = 2;
            }
        }

        /// <summary>The grid follows the body where the clip has carried it
        /// (its hips), turned as the controller faces, after the frame's pose.</summary>
        void LateUpdate()
        {
            if (eyes != null) eyes.position = Ground + transform.forward * eyesAhead;
        }

        /// <summary>A part's path from where it was last frame (if it was) to now.</summary>
        static void Keep(ref BladePath path, Vector3 a, Vector3 b, float radius, bool was)
        {
            path = new BladePath { a = was ? path.a2 : a, b = was ? path.b2 : b, a2 = a, b2 = b, radius = radius };
        }

        /// <summary>A striking part's path between frames.</summary>
        struct Trace
        {
            public bool on;
            public Vector3 a, b;
        }

        /// <summary>The leading limb's (or the weapon's) path, and a bare-handed
        /// blow's other limb's (a kick's swinging arm, a punch's foot), in the
        /// clip's middle phases (<see cref="Fight.Phases"/>: the 2nd to the 4th
        /// of 5): a blow lands wherever it touches the other then, not only in
        /// its measured window (that, around the striking point's top speed,
        /// often missed the touch one sees), but not as it only begins (the
        /// body still where the last action left it) nor in its recovery.</summary>
        void Sweeps(float dt)
        {
            int limb = strikeLimb[current];
            bool foot = limb >= 2;
            var with = foot || attackWeapon[current] == Weapon.Hands ? Weapon.Hands : Held;
            float phase = clock / attackLength[current] * Fight.Phases;
            bool open = phase >= 1f && phase < Fight.Phases - 1;
            if (with == Weapon.Sword && swordTip != null)
                Sweep(ref lead, open, swordBase.position, swordTip.position, swordRadius * Size, swordSpeed, dt, true);
            else if (with == Weapon.Hammer && hammerHead != null)
            {
                var across = hammerHead.right * hammerHalfWidth * Size;
                Sweep(ref lead, open, hammerHead.position - across, hammerHead.position + across, hammerRadius * Size, hammerSpeed, dt, true);
                var grip = Vector3.Lerp(hammerHead.position, hammerBase.position, hammerShaft);
                Sweep(ref shaft, open, grip, hammerHead.position, shaftRadius * Size, hammerSpeed, dt, true);
            }
            else
                SweepLimb(ref lead, ref leadShin, open, limb, dt);
            int other = current < strikeLimb2.Length ? strikeLimb2[current] : -1;
            if (other >= 0)
            {
                SweepLimb(ref follow, ref followShin, open, other, dt);
            }
        }

        /// <summary>Whether this body's hammer (<see cref="Guard"/>, at
        /// <paramref name="f"/> of its way since the last frame) meets the part
        /// from <paramref name="a"/> to <paramref name="b"/>, and where.</summary>
        bool Shields(Vector3 a, Vector3 b, float radius, float f, out Vector3 met)
        {
            met = default;
            for (int i = 0; i < Guards; i++)
            {
                var g = Guard[i];
                if (Apart(a, b, Vector3.Lerp(g.a, g.a2, f), Vector3.Lerp(g.b, g.b2, f), out var on, out var at) <= radius + g.radius)
                {
                    met = (on + at) / 2f;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether two parts touched along their paths since the last
        /// frame (at the same moments, a few between the frames).</summary>
        public static bool Cross(BladePath p, BladePath q)
        {
            float reach = p.radius + q.radius;
            const int steps = 4;
            for (int k = 0; k <= steps; k++)
            {
                float f = k / (float)steps;
                if (Apart(Vector3.Lerp(p.a, p.a2, f), Vector3.Lerp(p.b, p.b2, f),
                          Vector3.Lerp(q.a, q.a2, f), Vector3.Lerp(q.b, q.b2, f), out _, out _) <= reach)
                    return true;
            }
            return false;
        }

        /// <summary>The shortest distance between segments p1–q1 and p2–q2, and
        /// its ends on each (Ericson, Real-Time Collision Detection, 5.1.9).</summary>
        public static float Apart(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= 1e-8f && e <= 1e-8f)
            {
                s = t = 0f;
            }
            else if (a <= 1e-8f)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-8f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                    s = denom > 1e-8f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }
            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
            return (c1 - c2).magnitude;
        }

        /// <summary>A bare limb: the fist, or the foot and the shin.</summary>
        void SweepLimb(ref Trace end, ref Trace shin, bool open, int limb, float dt)
        {
            var joint = limbs[limb].position;
            var tip = limb < limbTips.Length && limbTips[limb] != null ? limbTips[limb].position : joint;
            Sweep(ref end, open, joint, tip, limbRadius * Size, handsSpeed, dt);
            if (limb < knees.Length && knees[limb] != null)
                Sweep(ref shin, open, knees[limb].position, joint, limbRadius * Size, handsSpeed, dt);
        }

        /// <summary>A striking part's path since the last frame, as a few
        /// capsules, against the other's hurtboxes; damage by the action's
        /// (Balance) and its end's speed, the combo and the adrenaline. A blow
        /// lands once, whichever part lands it.</summary>
        void Sweep(ref Trace trace, bool open, Vector3 a, Vector3 b, float radius, float fullSpeed, float dt, bool weapon = false)
        {
            if (!trace.on || !open)
            {
                trace.a = a;
                trace.b = b;
                trace.on = open;
                if (!open) return;
            }
            if (weapon && Blades < Blade.Length)
                Blade[Blades++] = new BladePath { a = trace.a, b = trace.b, a2 = a, b2 = b, radius = radius };
            Swept?.Invoke(a, b, radius);
            if (!blocked && !evaded && dt > 0f)
            {
                float pointSpeed = (b - trace.b).magnitude / dt;
                const int steps = 4;
                for (int k = 1; k <= steps && !blocked && !evaded; k++)
                {
                    float f = k / (float)steps;
                    var from = Vector3.Lerp(trace.a, a, f);
                    var to = Vector3.Lerp(trace.b, b, f);
                    // The hammer first: a blade meeting it on its way to the body is stopped.
                    if (weapon && Held == Weapon.Sword && opponent != null && opponent.Shields(from, to, radius, f, out var met))
                    {
                        blocked = true;
                        WeaponBlowsBlocked++;
                        Blocked?.Invoke(met);
                        // A block is the hammer's holder's evade.
                        opponent.Pump(EvadePoints);
                        Evaded?.Invoke();
                        break;
                    }
                    if (rearm > 0f) continue;
                    int found = Physics.OverlapCapsuleNonAlloc(from, to, radius, touched, 1 << HurtboxLayer, QueryTriggerInteraction.Collide);
                    for (int n = 0; n < found; n++)
                    {
                        var hurt = touched[n];
                        var other = hurt.GetComponentInParent<Fighter>();
                        if (other == null || other == this || !strikesAnyone && other != opponent) continue;
                        // The blade on a bare-handed one's forearm or shin: nothing; a body part it touches too may still be hit.
                        if (weapon && Held == Weapon.Sword && other.Held == Weapon.Hands && System.Array.IndexOf(other.swordProof, hurt) >= 0) continue;
                        // Inside its evade's window: the blow passes, evaded, and strikes it no more.
                        if (other.Untouchable)
                        {
                            evaded = true;
                            other.Pump(EvadePoints);
                            Evaded?.Invoke();
                            break;
                        }
                        rearm = Rehit;
                        // This hit's place in the combo (counted for good as it is told, in Update).
                        int next = Combo > 0 && (landed || current != lastLanded) && SinceLanded < ComboWindow ? Combo + 1 : 1;
                        landed = true;
                        float damage = damageOf[current] * (0.8f + 0.4f * Mathf.Clamp01(pointSpeed / fullSpeed))
                            * ComboFactor(next) * LevelFactor[Level];
                        if (other.TakeHit(damage, staggerOf[current]))
                        {
                            Hit?.Invoke(hurt.ClosestPoint(to));
                            hits++;
                            fresh = true;
                            damageDone = damage;
                            Pump(damage * HitPoints);
                        }
                        break;
                    }
                }
            }
            trace.a = a;
            trace.b = b;
        }
    }
}

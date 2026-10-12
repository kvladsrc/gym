using Soulslike.Training;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Soulslike
{
    /// <summary>
    /// The hero: a duellist's body (Fighter) in a person's hands (HumanFighter),
    /// as the arena viewer's -play has it (training/play.sh): the same blows,
    /// roll, energy and adrenaline as the creatures it meets (Creature), its
    /// blows landing on any of them. A blow pressed starts at the next
    /// decision it is free at, as the agents' do (<see cref="DecisionPeriod"/>).
    ///
    /// Besides: 1, 2, 3 take up the sword, the hammer, bare hands (between
    /// actions); R drinks a flask; it rises again at a bonfire (GameManager).
    /// It faces the creature fighting it (its adrenaline, evasions and the
    /// hammer's guard are counted against that one).
    /// </summary>
    [RequireComponent(typeof(Fighter), typeof(HumanFighter))]
    public class Hero : MonoBehaviour
    {
        public float height = 1.85f;
        public Weapon weapon = Weapon.Sword;
        public int maxFlasks = 3;
        public float flaskHeal = 90f;
        /// <summary>Physics steps (0.02 s) between its decisions: 5 a second, the agents' pace.</summary>
        public const int DecisionPeriod = 10;

        public Fighter Body { get; private set; }
        public Health Health => Body.Health;
        public int Flasks { get; private set; }

        HumanFighter hands;
        Creature[] creatures = { };
        int steps;
        readonly int[] choice = new int[4];

        void Awake()
        {
            Body = GetComponent<Fighter>();
            hands = GetComponent<HumanFighter>();
            hands.body = Body;
            Body.strikesAnyone = true;
            Body.Equip(weapon, height);
            Flasks = maxFlasks;
            // Its own Health: the Fighter's may not have woken yet.
            GetComponent<Health>().Damaged += _ => Sounds.At(Sounds.Instance?.hurt, transform.position, 0.8f);
        }

        void Start()
        {
            creatures = FindObjectsByType<Creature>(FindObjectsSortMode.None);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Respawn(Vector3 position, float yaw)
        {
            Body.ResetAt(position, yaw);
            Body.Equip(weapon, height);
            Flasks = maxFlasks;
        }

        void Update()
        {
            Body.opponent = Facing();
            var keyboard = Keyboard.current;
            if (keyboard == null || PauseMenu.Open || Health.Dead) return;
            if (keyboard.digit1Key.wasPressedThisFrame) Arm(Weapon.Sword);
            else if (keyboard.digit2Key.wasPressedThisFrame) Arm(Weapon.Hammer);
            else if (keyboard.digit3Key.wasPressedThisFrame) Arm(Weapon.Hands);
            if (keyboard.rKey.wasPressedThisFrame && !Body.Busy && Flasks > 0 && Health.Current < Health.max)
            {
                Flasks--;
                Health.Heal(flaskHeal);
            }
        }

        void Arm(Weapon held)
        {
            if (Body.Busy || held == weapon) return;
            weapon = held;
            Body.Equip(weapon, height);
        }

        /// <summary>The creature fighting it, the nearest of them; else none.</summary>
        Fighter Facing()
        {
            Fighter nearest = null;
            float best = float.MaxValue;
            foreach (var c in creatures)
            {
                if (c == null || !c.Engaged || c.Health.Dead) continue;
                float distance = (c.Body.Ground - Body.Ground).sqrMagnitude;
                if (distance < best) { best = distance; nearest = c.Body; }
            }
            return nearest;
        }

        /// <summary>A decision, as the agents take theirs: the speeds as the
        /// keys hold them, the blow pressed if it may start.</summary>
        void FixedUpdate()
        {
            if (++steps % DecisionPeriod != 0 || PauseMenu.Open) return;
            hands.Fill(new ActionSegment<int>(choice));
            Body.motion.Push(choice[0], choice[1], choice[2]);
            if (choice[3] > 0) Body.Act(choice[3] - 1);
        }
    }
}

using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Soulslike.Training
{
    /// <summary>
    /// A duellist's agent: one network for both sides (ML-Agents symmetric
    /// self-play, teams 0 and 1 of the behaviour "Fighter").
    ///
    /// Eyes: a grid around it (GridSensorComponent: the fighters and the
    /// obstacles), and numbers (<see cref="Observations"/>): the other's
    /// bearing, distance, facing, speed and turning; its own speeds; both
    /// healths; whether it is busy; the fight's clock (DuelEpisode.Elapsed); its own and the other's action, each as a
    /// one-hot of the action and a one-hot of its phase; stagger of both
    /// (nothing is invulnerable: a hit is the striking point meeting the
    /// body's own hurtboxes); its size against the other's; both weapons;
    /// its place in the arena; its own last <see cref="Remembered"/> actions;
    /// its combo (Fighter.Combo) and the time since its last hit; its own and
    /// the other's adrenaline level (Fighter.Level), and how far it is towards
    /// its next gathered level.
    ///
    /// Actions, once a decision: its speeds ahead, aside and turning (each
    /// kept or ordered up or down: Inertia; an action stops them) and an action: none, an attack by
    /// index, or the roll.
    ///
    /// Rewards by the curriculum's stage (the trainer's "stage" parameter),
    /// each for its own behaviour alone (what an earlier stage taught has to
    /// pay its way in the next): 0 for turning to face the other, 1 for
    /// closing in (both as the change since the last decision), 2 the fight
    /// itself (DuelEpisode: damage, the win, the loss, the time-out). Always:
    /// the energy spent. Nothing else: combos and adrenaline pay through the
    /// damage they add (rewards for repeats, evasions and standing apart were
    /// tried, and dropped as steering the fight by hand).
    /// </summary>
    public class FighterAgent : Agent
    {
        public Fighter Body;
        /// <summary>Its square and clock: the training arena's, or its post in the game (Creature).</summary>
        public DuelGround Arena;
        /// <summary>A person playing this side (the viewer's -play), whose
        /// choices the heuristic then gives.</summary>
        public HumanFighter human;

        /// <summary>A unit of energy's cost in reward: a blow ~0.04–0.2 (at 1,
        /// one landing in five paid for swinging in place).</summary>
        public const float EnergyWeight = 8f;
        /// <summary>Closing in by this many metres is worth 1 (stage 1).</summary>
        const float ClosingScale = 10f;
        /// <summary>How many of its own last actions it sees.</summary>
        public const int Remembered = 3;
        /// <summary>Decisions in this episode, and those at adrenaline 0.</summary>
        public int Decisions { get; private set; }
        public int Apart { get; private set; }

        Fight.Track other;
        /// <summary>The other's points and my own at my last snapshot (Fight.Points).</summary>
        readonly Vector3[] pointsWere = new Vector3[Fight.PointCount], ownWere = new Vector3[Fight.PointCount];
        float pointsTime, ownTime;
        /// <summary>The points' numbers at the last decision (the other's,
        /// then its own: place, speed, place…), and the frame they were seen
        /// in (the ground under its hips, its rotation, its height), for GridView.</summary>
        public readonly Vector3[] SeenPoints = new Vector3[4 * Fight.PointCount];
        public Vector3 SeenFrom { get; private set; }
        public Quaternion SeenTurn { get; private set; }
        public float SeenTall { get; private set; }
        float lastAngle = -1f, lastDistance;
        /// <summary>Its last actions, newest first (-1: none yet).</summary>
        readonly int[] history = { -1, -1, -1 };

        public static int Observations(int actions) => 24 + 2 * Fight.PointObservations + 2 + 2 * (actions + 1 + Fight.Phases) + Remembered * (actions + 1);

        /// <summary>Ahead, aside (keep, raise, lower); turning (keep, raise,
        /// lower, focus on the other: Inertia.focus); the action (none, the
        /// attacks, the roll).</summary>
        public static ActionSpec Actions(int actions) => ActionSpec.MakeDiscrete(3, 3, 4, actions + 1);

        public static int Stage => Mathf.RoundToInt(Academy.Instance.EnvironmentParameters.GetWithDefault("stage", 2f));

        /// <summary>A new episode: no change to reward yet.</summary>
        public void Restart()
        {
            lastAngle = -1f;
            // The other's speed is measured from its last place: not across
            // episodes (its first decision saw a leap to the new start).
            other = default;
            pointsTime = ownTime = 0f;
            for (int i = 0; i < history.Length; i++) history[i] = -1;
            Decisions = Apart = 0;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            var me = Body.transform;
            var them = Body.opponent;
            Fight.Relative(sensor, Body, them, ref other);
            Fight.Points(sensor, Body, them, pointsWere, ref pointsTime, SeenPoints, 0);
            Fight.Points(sensor, Body, Body, ownWere, ref ownTime, SeenPoints, 2 * Fight.PointCount);
            SeenFrom = Body.Ground;
            SeenTurn = me.rotation;
            SeenTall = Fighter.Tall * Body.Size;
            Fight.Motion(sensor, Body.motion);
            sensor.AddObservation(Body.Health.Current / Body.Health.max);
            sensor.AddObservation(them.Health.Current / them.Health.max);
            sensor.AddObservation(Body.Busy ? 1f : 0f);
            // The fight's clock, 0 to 1 at the time-out: without it a time-out
            // came at random to the agent, and pressed it to nothing.
            sensor.AddObservation(Arena.Elapsed);
            Fight.Doing(sensor, Body);
            Fight.Doing(sensor, them);
            sensor.AddObservation(Body.Staggered ? 1f : 0f);
            sensor.AddObservation(them.Staggered ? 1f : 0f);
            sensor.AddObservation(Mathf.Log(Body.Size / them.Size));
            sensor.AddOneHotObservation((int)Body.Held, 3);
            sensor.AddOneHotObservation((int)them.Held, 3);
            Fight.Place(sensor, me, Arena.Centre, Arena.HalfSize);
            foreach (int action in history) sensor.AddOneHotObservation(action + 1, Body.Actions + 1);
            sensor.AddObservation(Mathf.Min(Body.Combo, 5) / 5f);
            sensor.AddObservation(Body.SinceLanded / Fighter.ComboWindow);
            // Both adrenalines, levels and the points towards the next in one
            // number each (the other's way up too, unseen as levels alone).
            sensor.AddObservation(Body.Adrenaline);
            sensor.AddObservation(them.Adrenaline);
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask mask)
        {
            for (int action = 0; action < Body.Actions; action++)
                if (!Body.Allowed(action)) mask.SetActionEnabled(3, action + 1, false);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var d = actions.DiscreteActions;
            Body.motion.Push(d[0], d[1], d[2]);
            if (d[3] > 0 && Body.Act(d[3] - 1))
            {
                int action = d[3] - 1;
                for (int i = history.Length - 1; i > 0; i--) history[i] = history[i - 1];
                history[0] = action;
            }

            int stage = Stage;
            var to = Body.opponent.transform.position - Body.transform.position;
            to.y = 0f;
            float angle = Mathf.Abs(Vector3.SignedAngle(Body.transform.forward, to, Vector3.up)) / 180f;
            float distance = to.magnitude;
            // Each stage pays for its own behaviour alone: what an earlier one
            // taught has to earn its keep by helping the new one.
            if (lastAngle >= 0f)
            {
                if (stage == 0) AddReward(lastAngle - angle);
                else if (stage == 1) AddReward((lastDistance - distance) / ClosingScale);
            }
            lastAngle = angle;
            lastDistance = distance;
            Decisions++;
            if (Body.Level == 0) Apart++;
            AddReward(-Body.energySpent * EnergyWeight);
            Body.energySpent = 0f;
        }

        /// <summary>A crude hand-written duellist, to try the arena without a
        /// model: turn to the other, close in, strike with whatever it may.</summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var d = actionsOut.DiscreteActions;
            if (human != null)
            {
                human.Fill(d);
                return;
            }
            var to = Body.opponent.transform.position - Body.transform.position;
            to.y = 0f;
            float angle = Vector3.SignedAngle(Body.transform.forward, to, Vector3.up);
            float wantTurn = Mathf.Clamp(angle / 60f, -1f, 1f);
            d[2] = wantTurn > Body.motion.turn + 0.1f ? 1 : wantTurn < Body.motion.turn - 0.1f ? 2 : 0;
            float wantAhead = to.magnitude > 2f && Mathf.Abs(angle) < 45f ? 1f : 0f;
            d[0] = wantAhead > Body.motion.forward + 0.1f ? 1 : wantAhead < Body.motion.forward - 0.1f ? 2 : 0;
            d[1] = Body.motion.side > 0.1f ? 2 : Body.motion.side < -0.1f ? 1 : 0;
            d[3] = 0;
            if (to.magnitude < 2.5f && Mathf.Abs(angle) < 25f)
                for (int tries = 0; tries < 20; tries++)
                {
                    int action = Random.Range(0, Body.Attacks);
                    if (Body.Allowed(action)) { d[3] = action + 1; break; }
                }
        }
    }
}

using Soulslike.Training;
using Unity.MLAgents;
using UnityEngine;

namespace Soulslike
{
    /// <summary>
    /// A creature of the level, fought by the trained duellist's network
    /// (FighterAgent, the training's "Fighter" model) on a duellist's body
    /// (Fighter), in the conditions it trained in:
    ///
    /// - Its post is the centre of its square (14 x 14 m): it is kept inside
    ///   it. The network sees its place as in the training's arena (30 x 30
    ///   m, its walls far off), centred on the post.
    /// - It stands at its post, the network silent, till the hero steps into
    ///   its square; then the network fights, its memory new. The hero out of
    ///   the square, or dead, ends the fight: it walks back to its post and is
    ///   whole again. The level lays the creatures out so that no place is in
    ///   more than two squares: at most two fight the hero at once.
    /// - The fight's clock runs from the moment it engages, as the
    ///   training's ran from an episode's start, and stops at <see
    ///   cref="Hold"/> s, half the training's limit (<see cref="Limit"/> s):
    ///   there is no time-out here, and about half the training's fights
    ///   lasted that long (late in the limit it saw little, and that little
    ///   with a time-out coming).
    /// </summary>
    [RequireComponent(typeof(Fighter), typeof(FighterAgent))]
    public class Creature : DuelGround
    {
        public string title = "";
        public bool isBoss;
        /// <summary>A boss's own theme (GameManager plays it as the hero nears).</summary>
        public AudioClip theme;
        public Weapon weapon;
        public float height = 1.8f;
        /// <summary>Half the side of its square round its post, m: where it
        /// fights the hero.</summary>
        public float halfSize = 7f;
        /// <summary>Half the training arena's side, m: its place as the
        /// network sees it is measured against this, not its square.</summary>
        public const float ArenaHalf = 15f;
        /// <summary>The training's time limit, s, and where the fight's clock stops.</summary>
        public const float Limit = 90f, Hold = 45f;

        public Fighter Body { get; private set; }
        public Health Health => Body.Health;
        public bool Engaged => state == State.Fighting;

        public override Vector3 Centre => home;
        public override float HalfSize => ArenaHalf;
        public override float Elapsed => Mathf.Min(Time.time - began, Hold) / Limit;

        enum State { Idle, Fighting, Returning }
        State state;
        FighterAgent agent;
        /// <summary>Physics steps between its decisions while it fights: the training's 5 a second.</summary>
        public const int DecisionPeriod = 10;
        int steps;
        CharacterController controller;
        EnemyVoice voice;
        Vector3 home;
        /// <summary>When the fight began (Time.time).</summary>
        float began;
        float homeYaw;
        bool deathTold;

        void Awake()
        {
            Body = GetComponent<Fighter>();
            agent = GetComponent<FighterAgent>();
            controller = GetComponent<CharacterController>();
            voice = GetComponent<EnemyVoice>();
            home = transform.position;
            homeYaw = transform.eulerAngles.y;
            agent.Body = Body;
            agent.Arena = this;
            Body.Equip(weapon, height);
            Academy.Instance.AgentPreStep += Decide;
            if (voice != null)
            {
                // Its own Health: the Fighter's may not have woken yet.
                var health = GetComponent<Health>();
                health.Damaged += _ => { if (!health.Dead) voice.Hurt(); };
                Body.Acted += _ => voice.Attack();
            }
        }

        void OnDestroy()
        {
            if (Academy.IsInitialized) Academy.Instance.AgentPreStep -= Decide;
        }

        /// <summary>The network asked, as the training's requester asked it:
        /// every <see cref="DecisionPeriod"/> steps, the first as the fight
        /// begins; and only while it fights.</summary>
        void Decide(int academyStep)
        {
            if (state != State.Fighting || steps++ % DecisionPeriod != 0) return;
            agent.RequestDecision();
        }

        void Start()
        {
            if (Body.opponent == null) Body.opponent = FindFirstObjectByType<Hero>()?.Body;
        }

        /// <summary>Back at its post, whole, the network silent (the hero rested or rose again).</summary>
        public void Revive()
        {
            controller.enabled = true;
            Body.ResetAt(home, homeYaw);
            Body.Equip(weapon, height);
            Silence();
            deathTold = false;
        }

        void Silence()
        {
            state = State.Idle;
            Body.motion.Stop();
            Body.motion.focus = false;
        }

        void Update()
        {
            if (Body.Health.Dead)
            {
                if (deathTold) return;
                deathTold = true;
                Silence();
                // The body lies where it fell: nothing walks into it.
                controller.enabled = false;
                voice?.Death();
                Sounds.At(Sounds.Instance?.enemyDeath, transform.position);
                GameManager.Instance?.OnEnemyDied(this);
                return;
            }
            var hero = Body.opponent;
            bool near = hero != null && !hero.Health.Dead && Inside(hero.Ground);
            switch (state)
            {
                case State.Idle:
                case State.Returning:
                    if (near) Engage();
                    else if (state == State.Returning) Return();
                    break;
                case State.Fighting:
                    if (!near) state = State.Returning;
                    break;
            }
        }

        /// <summary>Whether a place is in its square.</summary>
        public bool Inside(Vector3 at) => Mathf.Abs(at.x - home.x) <= halfSize && Mathf.Abs(at.z - home.z) <= halfSize;

        /// <summary>The hero in its square: the network takes
        /// over, its memory and its view of the hero's motion new.</summary>
        void Engage()
        {
            state = State.Fighting;
            began = Time.time;
            agent.EndEpisode();
            agent.Restart();
            Body.motion.Stop();
            steps = 0;
            voice?.Notice();
        }

        /// <summary>Walks back to its post (between its actions), and stands
        /// there whole, facing as it did.</summary>
        void Return()
        {
            if (Body.Busy) return;
            var to = Flat(home - Body.Ground);
            if (to.magnitude < 0.6f)
            {
                Revive();
                return;
            }
            float angle = Vector3.SignedAngle(transform.forward, to, Vector3.up);
            var motion = Body.motion;
            motion.focus = false;
            motion.turn = Mathf.Clamp(angle / 30f, -1f, 1f);
            motion.forward = Mathf.Abs(angle) < 45f ? 1f : 0f;
            motion.side = 0f;
            Body.motion = motion;
        }

        /// <summary>Inside its square, as the arena's walls kept it.</summary>
        void LateUpdate()
        {
            if (Body.Health.Dead) return;
            float edge = halfSize - controller.radius * Body.Size;
            var at = transform.position;
            var kept = new Vector3(Mathf.Clamp(at.x, home.x - edge, home.x + edge), at.y, Mathf.Clamp(at.z, home.z - edge, home.z + edge));
            if (kept == at) return;
            controller.enabled = false;
            transform.position = kept;
            controller.enabled = true;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}

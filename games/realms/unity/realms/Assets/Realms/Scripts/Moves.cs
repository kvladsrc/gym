using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Realms
{
    /// <summary>
    /// A studio creature's own moves: the clips its GLB carries (captured from video with AnyMove,
    /// see art.json), played on its skeleton with short blends between them. Setup puts this on
    /// the prefab of every model that has clips; the map and the battle play its loops, its
    /// bite, its flinch and its death, and keep the drawn <see cref="Idle"/> for the models
    /// without them. Nothing plays
    /// until it is asked, so a portrait shows the model as it was made.
    /// </summary>
    public sealed class Moves : MonoBehaviour
    {
        /// <summary>
        /// The clips' names: three loops and the one-shots: the attack (a wolf's bite, the boar's
        /// tusk charge), the flinch from a blow and the collapse when it is killed.
        /// </summary>
        public const string Stand = "Stand", Sit = "Sit", Run = "Run", Bite = "Bite", Hurt = "Hurt", Die = "Die";

        /// <summary>The clips, by name (Setup).</summary>
        public AnimationClip[] clips = new AnimationClip[0];

        /// <summary>
        /// In <see cref="Bite"/>: when the jaws are furthest forward, and when the head starts
        /// to go there, in seconds of the clip (Setup measures both from the head bone).
        /// </summary>
        public float strike = -1f, windup;

        /// <summary>
        /// The bite plays from this long before its windup, in the clip's own seconds: the
        /// gathering before the leap.
        /// </summary>
        public const float Lead = 0.3f;

        /// <summary>
        /// How far the head goes forward from where the bite starts to the strike, in the
        /// model's largest dimensions (Setup): the battle closes in only for the rest of the way.
        /// </summary>
        public float biteReach;

        /// <summary>
        /// How fast <see cref="Run"/> carries the creature over the ground at its own speed, in
        /// the model's largest dimensions a second (Setup, from the clip with root motion).
        /// </summary>
        public float runSpeed;

        /// <summary>
        /// The loop it plays from the start, from a random point of it so a pack does not move
        /// as one; none leaves the model as it was made.
        /// </summary>
        public string hold;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] players;
        float[] weights;
        int current = -1;
        float fade = 0.2f;
        bool once;
        string then;

        public bool Has(string name) => IndexOf(name) >= 0;

        /// <summary>How long a clip is at its own speed, in seconds (0 without it).</summary>
        public float Length(string name)
        {
            int i = IndexOf(name);
            return i < 0 ? 0f : clips[i].length;
        }

        void Start()
        {
            if (current < 0 && !string.IsNullOrEmpty(hold))
                Loop(hold, 1f, 0f);
        }

        int IndexOf(string name)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].name == name)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Plays a loop from <paramref name="phase"/> of its length (a random point when below
        /// zero), blending over <paramref name="blend"/> seconds; false without that clip.
        /// </summary>
        public bool Loop(string name, float speed = 1f, float blend = 0.25f, float phase = -1f)
        {
            int i = IndexOf(name);
            if (i < 0 || !Ready())
                return false;
            if (i == current && !once)
            {
                players[i].SetSpeed(speed);
                return true;
            }
            Begin(i, (phase < 0f ? Random.value : phase) * clips[i].length, speed, blend, false);
            return true;
        }

        /// <summary>
        /// Plays a clip once from <paramref name="from"/> seconds, then holds its last frame, or
        /// goes on to the loop <paramref name="then"/> when one is given and no other clip was
        /// asked for meanwhile; false without that clip.
        /// </summary>
        public bool Once(string name, float from = 0f, float speed = 1f, float blend = 0.15f, string then = null)
        {
            int i = IndexOf(name);
            if (i < 0 || !Ready())
                return false;
            Begin(i, Mathf.Clamp(from, 0f, clips[i].length), speed, blend, true);
            this.then = then;
            return true;
        }

        /// <summary>Where the playing clip is, in its own seconds.</summary>
        public float Clock => current >= 0 && players != null ? (float)players[current].GetTime() : 0f;

        /// <summary>Changes how fast the playing clip runs (0 holds it).</summary>
        public void Speed(float speed)
        {
            if (current >= 0 && players != null)
                players[current].SetSpeed(speed);
        }

        void Begin(int i, float time, float speed, float blend, bool oneShot)
        {
            // The clips that were showing fade out from where they are, a one-shot holding its
            // pose so it never runs round to its start; this one fades in.
            if (once && current >= 0 && current != i)
                players[current].SetSpeed(0.0);
            current = i;
            once = oneShot;
            then = null;
            fade = blend;
            players[i].SetTime(time);
            players[i].SetSpeed(speed);
            players[i].Play();
            if (blend <= 0f)
            {
                for (int k = 0; k < weights.Length; k++)
                    weights[k] = k == i ? 1f : 0f;
                Apply();
            }
        }

        bool Ready()
        {
            if (players != null)
                return true;
            var animator = GetComponentInChildren<Animator>(true);
            if (animator == null || clips.Length == 0)
                return false;
            graph = PlayableGraph.Create(name + " moves");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Moves", animator);
            mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            output.SetSourcePlayable(mixer);
            players = new AnimationClipPlayable[clips.Length];
            weights = new float[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                players[i] = AnimationClipPlayable.Create(graph, clips[i]);
                players[i].SetApplyFootIK(false);
                players[i].Pause();
                graph.Connect(players[i], 0, mixer, i);
                mixer.SetInputWeight(i, 0f);
            }
            graph.Play();
            return true;
        }

        void Update()
        {
            if (players == null || current < 0)
                return;
            float step = fade > 0f ? Time.deltaTime / fade : 1f;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = Mathf.MoveTowards(weights[i], i == current ? 1f : 0f, step);
                if (weights[i] <= 0f && i != current && players[i].GetPlayState() == PlayState.Playing)
                    players[i].Pause();
            }
            Apply();
            if (once)
            {
                // The clips loop by themselves: a one-shot stops on its last frame instead,
                // before the frame's step would carry it round to its first, and goes on to
                // the loop it was given.
                double end = clips[current].length - 1.0 / 120.0;
                AnimationClipPlayable player = players[current];
                if (player.GetTime() + Time.deltaTime * player.GetSpeed() >= end)
                {
                    player.SetTime(end);
                    player.SetSpeed(0.0);
                    if (then != null)
                    {
                        string loop = then;
                        then = null;
                        Loop(loop);
                    }
                }
            }
        }

        /// <summary>The mixer's weights, scaled to sum to one so a blend never shrinks the pose.</summary>
        void Apply()
        {
            float sum = 0f;
            foreach (float w in weights)
                sum += w;
            for (int i = 0; i < weights.Length; i++)
                mixer.SetInputWeight(i, sum > 1e-4f ? weights[i] / sum : i == current ? 1f : 0f);
        }

        void OnDestroy()
        {
            if (graph.IsValid())
                graph.Destroy();
        }
    }
}

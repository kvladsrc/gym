using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Soulslike.Training
{
    /// <summary>
    /// Films the first arena when the player is started with
    /// <c>-record &lt;folder&gt;</c> (training/record.sh): a camera over the
    /// first fighter's shoulder, turned to the second (as a lock-on in the
    /// genre): the second is always ahead in view, so it keeps its distance
    /// (drawing back as they part took it out of the arena); every second frame (30 a second of game time: ML-Agents steps the
    /// game a fixed 1/60 s a frame however slow the drawing) written there as
    /// JPEGs for ffmpeg. Both ways a hit shows sparks (DuelShow); watching,
    /// all is drawn plainly, unlit (Flat); one also hears the blows and
    /// sees both fighters' health, adrenaline
    /// and combo on the screen (not in the film's frames: the camera draws
    /// them, not the screen). Otherwise it does nothing, and training runs
    /// without graphics as before.
    /// </summary>
    public class ArenaRecorder : MonoBehaviour
    {
        public DuelEpisode arena;
        public int width = 1280, height = 720;
        /// <summary>Behind the first fighter, up and aside (over the shoulder), m,
        /// as tall as it is (its size scales them).</summary>
        public float back = 3.6f, up = 2.1f, aside = 0.8f;
        /// <summary>How quickly the camera follows, 1/s.</summary>
        public float follow = 4f;
        /// <summary>The hits' and swings' sounds (no cries: watching, they
        /// tired), the sparks' shader and the unlit one (set by the arena's
        /// builder).</summary>
        public AudioClip hitSound, swingSound;
        public Shader effectShader, flatShader;

        string folder;
        Camera view;
        RenderTexture target;
        Texture2D frame;
        int frames, written;
        Material glass;
        Fighter[] fighters = { };
        readonly int[] lastDoing = { -1, -1 };
        readonly float[] lastProgress = new float[2], lastDamage = new float[2], sinceRise = { 99f, 99f };

        void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-record");
            // -watch (training/watch.sh): the same camera on the screen, no
            // frames written; -play (training/play.sh) the same, the first
            // fighter a person's (HumanFighter), the second the model's.
            bool play = Array.IndexOf(args, "-play") >= 0;
            bool watch = play || Array.IndexOf(args, "-watch") >= 0;
            if ((at < 0 || at + 1 >= args.Length) && !watch || arena == null)
            {
                enabled = false;
                return;
            }
            if (!watch)
            {
                folder = args[at + 1];
                Directory.CreateDirectory(folder);
            }
            // The window losing focus must not pause the player: the trainer's
            // Python side then waited for it in vain (a time-out, "Unity took
            // too long to respond").
            Application.runInBackground = true;
            // The hero's follow camera would take the cursor and the view.
            foreach (var chase in FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None)) chase.enabled = false;
            view = Camera.main;
            if (view == null)
            {
                view = new GameObject("Recording camera").AddComponent<Camera>();
                view.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            }
            view.fieldOfView = 60f;
            Aim(1f);
            Show(watch);
            Flat();
            if (play) Play();
            if (watch) return;
            target = new RenderTexture(width, height, 24);
            view.targetTexture = target;
            frame = new Texture2D(width, height, TextureFormat.RGB24, false);
            StartCoroutine(Film());
        }

        /// <summary>The first fighter given to the person: its agent asks
        /// HumanFighter instead of the trainer; the cursor held by the window.</summary>
        void Play()
        {
            var agent = arena.first;
            var human = agent.gameObject.AddComponent<HumanFighter>();
            human.body = agent.Body;
            agent.human = human;
            agent.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorType =
                Unity.MLAgents.Policies.BehaviorType.HeuristicOnly;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>Plain drawing, all seen alike: every surface unlit (its
        /// colour and texture as they are, no light, shading, shadows or
        /// reflections), the floor its own grid of 25 cm cells and metres
        /// (LevelBuilder.ArenaFloor: distances read at a glance), the walls
        /// and obstacles dark, no post-processing (filming, watching and
        /// playing only; training draws nothing).</summary>
        void Flat()
        {
            if (flatShader == null) return;
            var made = new System.Collections.Generic.Dictionary<Material, Material>();
            foreach (var shape in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (shape is ParticleSystemRenderer || shape is LineRenderer) continue;
                var tone = shape.CompareTag("Obstacle") ? new Color(0.25f, 0.23f, 0.22f)
                    : shape.name == "Floor" ? new Color(1.3f, 1.3f, 1.3f) : (Color?)null;
                var materials = shape.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var old = materials[i];
                    if (old == null) continue;
                    if (tone == null && made.TryGetValue(old, out var known))
                    {
                        materials[i] = known;
                        continue;
                    }
                    var flat = new Material(flatShader);
                    if (old.HasProperty("_BaseMap"))
                    {
                        flat.SetTexture("_BaseMap", old.GetTexture("_BaseMap"));
                        flat.SetTextureScale("_BaseMap", old.GetTextureScale("_BaseMap"));
                    }
                    flat.SetColor("_BaseColor", tone ?? (old.HasProperty("_BaseColor") ? old.GetColor("_BaseColor") : Color.white));
                    if (tone == null) made[old] = flat;
                    materials[i] = flat;
                }
                shape.sharedMaterials = materials;
                shape.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            RenderSettings.fog = false;
            var data = view.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data != null) data.renderPostProcessing = false;
        }

        /// <summary>The first arena's hits and swings shown and heard.</summary>
        void Show(bool watch)
        {
            if (effectShader != null) glass = new Material(effectShader);
            if (watch && FindAnyObjectByType<AudioListener>() == null) view.gameObject.AddComponent<AudioListener>();
            fighters = new[] { arena.first.Body, arena.second.Body };
            for (int i = 0; i < 2; i++)
            {
                var me = fighters[i];
                int k = i;
                me.Hit += at => DuelShow.Hit(at, DuelShow.StrikingWith(me), glass, hitSound, null);
                me.Blocked += at => DuelShow.Block(at, glass);
                me.Struck += (landed, damage) => { if (landed) lastDamage[k] = damage; };
                me.LevelRose += level =>
                {
                    sinceRise[k] = 0f;
                    DuelShow.LevelUp(me, level, glass);
                };
            }
        }

        void Update()
        {
            for (int i = 0; i < fighters.Length; i++)
            {
                var me = fighters[i];
                sinceRise[i] += Time.deltaTime;
                // A blow begun (or the same begun anew): its swing heard.
                if (me.Doing >= 0 && me.Doing < me.Attacks && (me.Doing != lastDoing[i] || me.Progress < lastProgress[i]))
                    DuelShow.Play(swingSound, me.transform.position + Vector3.up, 0.5f, 1f);
                lastDoing[i] = me.Doing;
                lastProgress[i] = me.Progress;
            }
        }

        void OnGUI()
        {
            if (fighters.Length < 2) return;
            bool playing = arena.first.human != null;
            DuelShow.Panel(new Rect(28f, 24f, 380f, 110f), playing ? "You" : "Near", fighters[0], lastDamage[0], sinceRise[0]);
            DuelShow.Panel(new Rect(Screen.width - 408f, 24f, 380f, 110f), playing ? "Model" : "Far", fighters[1], lastDamage[1], sinceRise[1]);
        }

        void LateUpdate()
        {
            // Playing, the person's camera (HumanFighter) instead.
            if (arena.first.human != null) return;
            Aim(1f - Mathf.Exp(-follow * Time.deltaTime));
        }

        /// <summary>Behind the first fighter on the line to the second, looking
        /// between them (nearer the second); <paramref name="share"/> of the way there.</summary>
        void Aim(float share)
        {
            if (arena == null || arena.first == null || arena.second == null) return;
            var a = arena.first.transform.position;
            var b = arena.second.transform.position;
            var line = b - a;
            line.y = 0f;
            float apart = line.magnitude;
            var ahead = apart > 0.1f ? line / apart : arena.first.transform.forward;
            var right = Vector3.Cross(Vector3.up, ahead);
            float size = arena.first.Body.Size;
            var place = a + (-ahead * back + Vector3.up * up + right * aside) * size;
            // A trunk or a wall between the fighter and the camera: the camera
            // comes in front of it, as a game's camera does.
            var head = a + Vector3.up * 1.6f * size;
            var reach = place - head;
            if (Physics.SphereCast(head, 0.3f, reach.normalized, out var hit, reach.magnitude, 1 << 0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<Fighter>() == null)
                place = head + reach.normalized * Mathf.Max(0.3f, hit.distance - 0.1f);
            // Between them, nearer the second while close, at the second's chest far off.
            var look = Vector3.Lerp(a, b, Mathf.Clamp01(0.5f + apart / 20f)) + Vector3.up * 1.2f * size;
            var turn = Quaternion.LookRotation(look - place);
            view.transform.SetPositionAndRotation(
                Vector3.Lerp(view.transform.position, place, share),
                Quaternion.Slerp(view.transform.rotation, turn, share));
        }

        IEnumerator Film()
        {
            var end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;
                if (frames++ % 2 != 0) continue;
                var was = RenderTexture.active;
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(folder, $"frame{written++:D5}.jpg"), frame.EncodeToJPG(90));
            }
        }
    }
}

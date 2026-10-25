using System.Collections;
using UnityEngine;

namespace FinishTheGame
{
    /// <summary>One of the valley's people (the studio's rigged character):
    /// stands, looks about, kneels and rises, walks on the ground; her name
    /// shows over her head when the hero is near and looks her way (as in
    /// Gothic, the one in focus). Or a spirit in a likeness
    /// (<see cref="ghostly"/>): hovering, bobbing, no name over its head,
    /// coming into being and going (<see cref="Fade"/>; Ghost.shader), a white
    /// radiance with a diamond's fire: rays of light out of it, some split
    /// into rainbows (Rays.shader), motes rising off it, its light on the ground. The moves are her controller's states: Idle,
    /// Look, Kneel, Rise, Walk.</summary>
    public class Villager : MonoBehaviour
    {
        public string title;
        public StoryWorld world;
        public Hero hero;
        public Font font;
        public GameObject rig;
        public RuntimeAnimatorController moves;
        public Material look;
        public float height = 1.7f;
        /// <summary>Within this, m, her name shows.</summary>
        public float named = 9f;
        /// <summary>A spirit: hovering this high, m, bobbing; light, not lit.</summary>
        public bool ghostly;
        public float hover = 0.25f;
        /// <summary>A spirit's motes' look (Glow.shader) and its rays' (Rays.shader); the rays' size, in its heights.</summary>
        public Material motes, rays;
        public float raysSize = 2.3f;
        /// <summary>Her walking pace, m/s.</summary>
        public float pace = 1.35f;

        public Speaker Speaker { get; private set; }
        /// <summary>Searching the ground about her place.</summary>
        public bool searching;

        Animator animator;
        AudioSource voice;
        Vector3 home;
        Renderer[] looks;
        ParticleSystem rising;
        Light glow;
        Transform shine;
        Material shineMaterial;
        float faded = 1f;
        GUIStyle nameStyle;
        Coroutine search;

        void Awake()
        {
            animator = Rigs.Dress(rig, transform, height, moves, look);
            looks = GetComponentsInChildren<Renderer>();
            foreach (var r in looks) r.shadowCastingMode = ghostly ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = ghostly ? 0f : 0.4f;
            // (A spirit hovers: its body is already that high.)
            Speaker = new Speaker { name = title, body = transform, eyes = height * 0.92f, voice = voice, beside = ghostly };
            if (ghostly) Spirited();
            else
            {
                var body = gameObject.AddComponent<CapsuleCollider>();
                body.height = height;
                body.radius = 0.35f;
                body.center = Vector3.up * (height / 2f);
            }
        }

        /// <summary>A spirit's motes, rising off its body and drifting up, and its light.</summary>
        void Spirited()
        {
            var skin = GetComponentInChildren<SkinnedMeshRenderer>();
            var go = new GameObject("Motes");
            go.transform.SetParent(transform, false);
            rising = go.AddComponent<ParticleSystem>();
            rising.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rising.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            // White, some catching a colour of the rainbow.
            main.startColor = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 0.55f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.55f), 0.62f), new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0.7f),
                    new GradientColorKey(new Color(0.55f, 1f, 0.6f), 0.78f), new GradientColorKey(new Color(0.55f, 0.8f, 1f), 0.86f),
                    new GradientColorKey(new Color(0.8f, 0.6f, 1f), 0.94f),
                },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) },
            }) { mode = ParticleSystemGradientMode.RandomColor };
            main.gravityModifier = -0.045f;
            main.maxParticles = 600;
            var shape = rising.shape;
            if (skin != null)
            {
                shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                shape.skinnedMeshRenderer = skin;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.4f;
            }
            var drift = rising.noise;
            drift.enabled = true;
            drift.strength = 0.25f;
            drift.frequency = 0.6f;
            var fade = rising.colorOverLifetime;
            fade.enabled = true;
            fade.color = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) },
            };
            var drawn = go.GetComponent<ParticleSystemRenderer>();
            drawn.sharedMaterial = motes;
            drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = Vector3.up * (height * 0.55f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.95f, 0.88f);
            glow.range = 7f;
            glow.shadows = LightShadows.None;
            rising.Play();
            if (rays == null) return;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = "Rays";
            shine = quad.transform;
            shine.SetParent(transform, false);
            shineMaterial = new Material(rays);
            var lit = quad.GetComponent<MeshRenderer>();
            lit.sharedMaterial = shineMaterial;
            lit.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Her voice's sound made strange: a spirit's echo.</summary>
        public void Haunted()
        {
            var echo = gameObject.AddComponent<AudioEchoFilter>();
            echo.delay = 160f;
            echo.decayRatio = 0.35f;
            echo.wetMix = 0.45f;
            var reverb = gameObject.AddComponent<AudioReverbFilter>();
            reverb.reverbPreset = AudioReverbPreset.Cave;
            voice.pitch = 0.94f;
        }

        /// <summary>Stood at a place, turned to face a point.</summary>
        public void Stand(Vector3 at, Vector3 facing)
        {
            transform.position = Ground(at);
            Face(facing, true);
            home = transform.position;
        }

        public void Play(string move, float blend = 0.3f) => animator.CrossFadeInFixedTime(move, blend);

        public void Face(Vector3 at, bool now = false)
        {
            var to = at - transform.position;
            to.y = 0;
            if (to.sqrMagnitude < 1e-4f) return;
            var turned = Quaternion.LookRotation(to);
            transform.rotation = now ? turned : Quaternion.RotateTowards(transform.rotation, turned, 240f * Time.deltaTime);
        }

        public IEnumerator TurnTo(Vector3 at)
        {
            for (float t = 0; t < 0.6f; t += Time.deltaTime)
            {
                Face(at);
                yield return null;
            }
        }

        /// <summary>The ground under a place (as the world lays it), m.</summary>
        Vector3 Ground(Vector3 at)
        {
            var level = world.ToLevel(at);
            level.y = Land.Height(level.x, level.z);
            return world.FromLevel(level) + Vector3.up * (ghostly ? hover : 0f);
        }

        public IEnumerator WalkTo(Vector3 to)
        {
            Play("Walk", 0.25f);
            while (true)
            {
                var way = to - transform.position;
                way.y = 0;
                if (way.magnitude < 0.12f) break;
                Face(to);
                transform.position = Ground(transform.position + way.normalized * Mathf.Min(way.magnitude, pace * Time.deltaTime));
                yield return null;
            }
            Play("Idle", 0.3f);
        }

        public IEnumerator Kneel(float seconds)
        {
            Play("Kneel", 0.35f);
            yield return new WaitForSeconds(seconds);
            Play("Rise", 0.2f);
            yield return new WaitForSeconds(1.1f);
            Play("Idle", 0.3f);
        }

        /// <summary>Comes into sight (1) or goes (0), a spirit's light.</summary>
        public IEnumerator Fade(float to, float seconds)
        {
            float from = to > 0.5f ? 0f : 1f;
            for (float t = 0; t <= seconds; t += Time.deltaTime)
            {
                SetFade(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            SetFade(to);
        }

        public void SetFade(float amount)
        {
            faded = amount;
            foreach (var r in looks)
            {
                r.enabled = amount > 0.001f;
                foreach (var m in r.materials) m.SetFloat("_Fade", amount);
            }
            if (rising != null)
            {
                var emission = rising.emission;
                // Coming into being or going, it sheds the most.
                emission.rateOverTime = amount <= 0.001f ? 0f : 60f + 140f * (1f - Mathf.Abs(2f * amount - 1f)) * (amount < 0.999f ? 1f : 0f);
                glow.intensity = 2.4f * amount;
                glow.enabled = amount > 0.001f;
                if (shine != null)
                {
                    shine.gameObject.SetActive(amount > 0.001f);
                }
            }
        }

        void Update()
        {
            if (ghostly)
            {
                var p = transform.position;
                var ground = Ground(p);
                transform.position = new Vector3(p.x, ground.y + 0.08f * Mathf.Sin(Time.time * 1.7f), p.z);
                // Where its feet are and how tall it is, for its look.
                var frame = new Vector4(transform.position.y, height, 0, 0);
                if (faded > 0.001f)
                    foreach (var r in looks)
                        foreach (var m in r.materials) m.SetVector("_Frame", frame);
            }
            if (searching && search == null) search = StartCoroutine(Search());
            if (!searching && search != null)
            {
                StopCoroutine(search);
                search = null;
                Play("Idle", 0.3f);
            }
        }

        /// <summary>Looking about, kneeling to feel the grass, a few steps on, about her place.</summary>
        IEnumerator Search()
        {
            int turn = 0;
            while (true)
            {
                Play("Look", 0.4f);
                yield return new WaitForSeconds(3.5f);
                yield return Kneel(2.6f);
                var round = Quaternion.Euler(0, 140f * ++turn, 0) * Vector3.forward * 2.2f;
                yield return WalkTo(home + round);
            }
        }

        void LateUpdate()
        {
            // The rays: from its breast, turned to the camera, a little behind it.
            var eye = Camera.main;
            if (shine == null || eye == null || !shine.gameObject.activeSelf) return;
            var breast = transform.position + Vector3.up * (height * 0.62f);
            var toward = breast - eye.transform.position;
            shine.SetPositionAndRotation(breast + toward.normalized * 0.4f, Quaternion.LookRotation(toward, eye.transform.up));
            shine.localScale = Vector3.one * (height * raysSize);
            // Quiet when the camera is close (a talk): the face is not lost in them.
            shineMaterial.SetFloat("_Fade", faded * faded * Mathf.Lerp(0.25f, 1f, Mathf.InverseLerp(2f, 6f, toward.magnitude)));
        }

        void OnGUI()
        {
            if (ghostly || Dialogue.Cinematic || hero == null || Event.current.type != EventType.Repaint) return;
            var eye = Camera.main;
            if (eye == null || Vector3.Distance(hero.transform.position, transform.position) > named) return;
            var head = transform.position + Vector3.up * (height + 0.35f);
            var screen = eye.WorldToScreenPoint(head);
            if (screen.z <= 0) return;
            var centre = new Vector2(screen.x / Screen.width - 0.5f, screen.y / Screen.height - 0.5f);
            if (centre.magnitude > 0.3f) return;
            nameStyle ??= new GUIStyle(GUI.skin.label) { font = font, alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.height / 36f), normal = { textColor = new Color(0.96f, 0.94f, 0.9f) } };
            var rect = new Rect(screen.x - 150, Screen.height - screen.y - 20, 300, 40);
            var colour = nameStyle.normal.textColor;
            nameStyle.normal.textColor = new Color(0, 0, 0, 0.75f);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), title, nameStyle);
            nameStyle.normal.textColor = colour;
            GUI.Label(rect, title, nameStyle);
        }
    }
}

using UnityEngine;

namespace Soulslike.Training
{
    /// <summary>
    /// What makes a duel watchable, for the editor's combo demo
    /// (ComboDemoView) and the arena viewer (ArenaRecorder): sparks and a
    /// sound where a blow lands, a flash and a ring of steel where a hammer
    /// stops a sword, a swing's sound, a ring as a fighter's
    /// adrenaline rises, and a fighter's health, adrenaline and combo drawn on
    /// the screen. Training never calls it.
    /// </summary>
    public static class DuelShow
    {
        static readonly string[] LevelNames = { "0", "A", "B", "C", "D", "S" };
        /// <summary>Each level's colour: from pale to burning.</summary>
        static readonly Color[] LevelColors =
        {
            new Color(0.75f, 0.75f, 0.75f), new Color(1f, 0.9f, 0.4f), new Color(1f, 0.7f, 0.2f),
            new Color(1f, 0.45f, 0.1f), new Color(1f, 0.2f, 0.1f), new Color(1f, 0.2f, 0.8f),
        };
        /// <summary>How long a risen level's letter flares on the panel, s.</summary>
        const float Flare = 0.8f;
        static GUIStyle text, big;

        /// <summary>A fighter's adrenaline rose to <paramref name="level"/>: a
        /// ring spreads on the ground about it.</summary>
        public static void LevelUp(Fighter fighter, int level, Material glass)
        {
            var go = new GameObject("Level up ring");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = glass;
            line.positionCount = 48;
            line.loop = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var ring = go.AddComponent<LevelUpRing>();
            ring.follow = fighter.transform;
            ring.color = LevelColors[level];
            ring.reach = 1f + 0.4f * level;
            ring.life = 0.4f + 0.1f * level;
        }

        /// <summary>What a fighter's blow now strikes with: its weapon, or
        /// hands for a bare blow or a weapon pack's kick.</summary>
        public static Weapon StrikingWith(Fighter fighter)
        {
            int doing = fighter.Doing;
            if (doing < 0 || doing >= fighter.Attacks || fighter.strikeLimb[doing] >= 2) return Weapon.Hands;
            return fighter.attackWeapon[doing];
        }

        /// <summary>A hit: sparks, its sound (lower for the hammer, lower yet
        /// bare), and now and then the target's cry.</summary>
        public static void Hit(Vector3 at, Weapon with, Material glass, AudioClip hitSound, AudioClip[] cries)
        {
            Play(hitSound, at, 1f, with == Weapon.Sword ? 1f : with == Weapon.Hammer ? 0.75f : 0.6f);
            if (cries != null && cries.Length > 0 && Random.value < 0.4f)
                Play(cries[Random.Range(0, cries.Length)], at, 0.8f, 1f);
            Sparks(at, with == Weapon.Hands ? new Color(1f, 0.85f, 0.7f) : new Color(1f, 0.7f, 0.2f), glass);
        }

        /// <summary>A sword's blow stopped by a hammer (Fighter.Blocked): a
        /// ring of steel, a white flash and cold sparks, many and fast.</summary>
        public static void Block(Vector3 at, Material glass)
        {
            Play(Clang, at, 0.9f, 1f);
            Sparks(at, new Color(0.75f, 0.9f, 1f), glass, 48, 9f);
            Flash(at, glass);
        }

        static AudioClip clang;
        /// <summary>Steel on steel, made here (no recording of it): a few
        /// inharmonic partials of a struck bar dying away, the higher sooner,
        /// over a click of noise.</summary>
        static AudioClip Clang
        {
            get
            {
                if (clang != null) return clang;
                const int rate = 44100;
                const float length = 0.9f;
                float[] tones = { 1180f, 1735f, 2410f, 3360f, 4650f, 6020f };
                float[] loud = { 1f, 0.75f, 0.55f, 0.4f, 0.3f, 0.2f };
                float[] dying = { 0.45f, 0.32f, 0.22f, 0.15f, 0.1f, 0.07f };
                var data = new float[(int)(rate * length)];
                var noise = new System.Random(7);
                float most = 0f;
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float v = 0f;
                    for (int k = 0; k < tones.Length; k++)
                        v += loud[k] * Mathf.Exp(-t / dying[k]) * Mathf.Sin(2f * Mathf.PI * tones[k] * t);
                    v += (float)(noise.NextDouble() * 2.0 - 1.0) * 1.5f * Mathf.Exp(-t / 0.004f);
                    data[i] = v;
                    most = Mathf.Max(most, Mathf.Abs(v));
                }
                for (int i = 0; i < data.Length; i++) data[i] *= 0.8f / most;
                clang = AudioClip.Create("Clang", data.Length, 1, rate, false);
                clang.SetData(data, 0);
                return clang;
            }
        }

        /// <summary>A white star where the blades met, gone in a blink.</summary>
        static void Flash(Vector3 at, Material glass)
        {
            var go = new GameObject("Block flash");
            go.transform.position = at;
            var flash = go.AddComponent<ParticleSystem>();
            flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = flash.main;
            main.duration = 0.05f;
            main.loop = false;
            main.startLifetime = 0.12f;
            main.startSpeed = 0f;
            main.startSize = 0.45f;
            main.startColor = new Color(1f, 1f, 1f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = flash.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = flash.shape;
            shape.enabled = false;
            var grow = flash.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1f));
            var fade = flash.colorOverLifetime;
            fade.enabled = true;
            fade.color = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) },
            };
            var look = go.GetComponent<ParticleSystemRenderer>();
            look.sharedMaterial = Glow(glass);
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flash.Play();
            Object.Destroy(go, 0.5f);
        }

        static Material glow;
        /// <summary>The glass with a soft round spot in it (a bare quad would
        /// show its corners).</summary>
        static Material Glow(Material glass)
        {
            if (glow != null) return glow;
            const int side = 64;
            var spot = new Texture2D(side, side, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    float r = new Vector2(x + 0.5f - side / 2f, y + 0.5f - side / 2f).magnitude / (side / 2f);
                    float a = Mathf.Clamp01(1f - r);
                    spot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            spot.Apply();
            glow = new Material(glass) { mainTexture = spot };
            return glow;
        }

        public static void Play(AudioClip clip, Vector3 at, float volume, float pitch)
        {
            if (clip == null) return;
            var sound = new GameObject("Sound " + clip.name);
            sound.transform.position = at;
            var source = sound.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch * Random.Range(0.94f, 1.06f);
            source.spatialBlend = 0.7f;
            source.Play();
            Object.Destroy(sound, clip.length / source.pitch + 0.1f);
        }

        /// <summary>A burst of sparks flying off the hit, falling.</summary>
        public static void Sparks(Vector3 at, Color color, Material glass, int count = 24, float speed = 6f)
        {
            var go = new GameObject("Hit sparks");
            go.transform.position = at;
            var sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed / 3f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = color;
            main.gravityModifier = 1.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = sparks.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var fade = sparks.colorOverLifetime;
            fade.enabled = true;
            fade.color = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) },
            };
            var look = go.GetComponent<ParticleSystemRenderer>();
            look.sharedMaterial = glass;
            look.renderMode = ParticleSystemRenderMode.Stretch;
            look.velocityScale = 0.04f;
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sparks.Play();
            Object.Destroy(go, 1f);
        }

        /// <summary>A fighter's panel (call from OnGUI): its health (<paramref
        /// name="health"/> of <paramref name="most"/>), adrenaline (level, its
        /// damage factor, the points towards the next; the letter flares for a
        /// while after it rose, <paramref name="sinceRise"/> s ago), combo and
        /// last hit; on a dark ground, in large shadowed letters.</summary>
        public static void Panel(Rect at, string title, Fighter fighter, float health, float most, float lastDamage, float sinceRise)
        {
            Styles();
            GUI.DrawTexture(new Rect(at.x - 10f, at.y - 8f, at.width + 20f, at.height + 16f), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, false, 0f, new Color(0f, 0f, 0f, 0.6f), 0f, 8f);
            Shadowed(new Rect(at.x, at.y, at.width, 24f), title, text, Color.white);
            var row = new Rect(at.x, at.y + 28f, at.width - 60f, 24f);
            Bar(row, "HP", health / most, $"{health:F0} / {most:F0}", new Color(0.75f, 0.12f, 0.12f));
            row.y += 30f;
            int level = fighter.Level;
            Bar(row, "Adrenaline", fighter.Gathered - Mathf.Floor(fighter.Gathered),
                $"×{Fighter.LevelFactor[level]:0.##} damage", new Color(0.85f, 0.45f, 0.05f));
            // The level's letter, right of the bars: flaring big after a rise.
            float flare = Mathf.Clamp01(1f - sinceRise / Flare);
            int size = Mathf.RoundToInt(Mathf.Lerp(34f, 60f, flare));
            big.fontSize = size;
            var letter = new Rect(at.x + at.width - 56f, at.y + 22f - (size - 34f) / 2f, 60f, size + 8f);
            Shadowed(letter, LevelNames[level], big, Color.Lerp(LevelColors[level], Color.white, flare * 0.6f));
            row.y += 30f;
            row.width = at.width;
            Shadowed(row, $"Combo {fighter.Combo} (×{Fighter.ComboFactor(Mathf.Max(1, fighter.Combo)):0.##}), last blow {lastDamage:F0}",
                text, Color.white);
        }

        public static void Panel(Rect at, string title, Fighter fighter, float lastDamage, float sinceRise) =>
            Panel(at, title, fighter, fighter.Health.Current, fighter.Health.max, lastDamage, sinceRise);

        static void Styles()
        {
            if (text != null) return;
            text = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            big = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
        }

        static void Shadowed(Rect at, string line, GUIStyle style, Color color)
        {
            style.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
            GUI.Label(new Rect(at.x + 2f, at.y + 2f, at.width, at.height), line, style);
            style.normal.textColor = color;
            GUI.Label(at, line, style);
        }

        static void Bar(Rect at, string name, float share, string value, Color color)
        {
            Shadowed(new Rect(at.x, at.y, 110f, at.height), name, text, Color.white);
            var bar = new Rect(at.x + 110f, at.y + 2f, at.width - 110f, at.height - 4f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(0.15f, 0.15f, 0.15f, 0.9f), 0f, 3f);
            var full = bar;
            full.width *= Mathf.Clamp01(share);
            GUI.DrawTexture(full, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, color, 0f, 3f);
            Shadowed(new Rect(bar.x + 8f, at.y, bar.width - 8f, at.height), value, text, Color.white);
        }
    }
}

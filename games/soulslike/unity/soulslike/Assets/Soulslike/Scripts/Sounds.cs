using UnityEngine;

namespace Soulslike
{
    /// <summary>The level's sounds and music, all made by the asset studio.</summary>
    public class Sounds : MonoBehaviour
    {
        public static Sounds Instance { get; private set; }

        [Header("Music")]
        public AudioClip ambientMusic;
        public AudioClip bossMusic;

        [Header("Effects")]
        public AudioClip swordSwing;
        public AudioClip swordHit;
        public AudioClip hurt;
        public AudioClip roll;
        public AudioClip[] footsteps = new AudioClip[0];
        public AudioClip enemyDeath;
        public AudioClip bossRoar;
        public AudioClip bonfire;
        /// <summary>A bonfire lit (rest).</summary>
        public AudioClip rest;
        public AudioClip deathSting;

        [Header("Mix")]
        public float musicVolume = 0.5f;
        public float swingVolume = 0.5f;
        public float hitVolume = 1f;
        public float rollVolume = 0.8f;
        public float footstepVolume = 0.3f;

        Loop music;

        void Awake()
        {
            Instance = this;
            music = new Loop(gameObject, spatial: false, musicVolume, music: true);
        }

        void Start() => PlayMusic(ambientMusic);

        void Update() => music.Update();

        public void PlayMusic(AudioClip clip)
        {
            // The same clip after StopMusic must start again (a death stops it).
            if (clip == null || (music.Clip == clip && music.Playing)) return;
            music.Play(clip);
        }

        public void StopMusic() => music.Stop();

        public AudioClip Footstep => footsteps.Length == 0 ? null : footsteps[Random.Range(0, footsteps.Length)];

        /// <summary>A one-shot 3D sound at a position, slightly varied in pitch.</summary>
        public static void At(AudioClip clip, Vector3 position, float volume = 1f, float from = 0f)
        {
            if (clip == null) return;
            var go = new GameObject("Sound " + clip.name);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume * Soulslike.AudioVolume.Effects;
            source.pitch = Random.Range(0.93f, 1.07f);
            source.spatialBlend = 0.85f;
            source.minDistance = 3f;
            source.maxDistance = 40f;
            source.time = Mathf.Min(from, clip.length - 0.05f);
            source.Play();
            Destroy(go, (clip.length - source.time) / source.pitch + 0.1f);
        }

        /// <summary>A spoken line at a position: no pitch jitter (a voice is
        /// recognisable), heard from farther than an effect.</summary>
        public static void Voice(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            var go = new GameObject("Voice " + clip.name);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume * Soulslike.AudioVolume.Effects;
            source.spatialBlend = 0.6f;
            source.minDistance = 10f;
            source.maxDistance = 60f;
            source.Play();
            Destroy(go, clip.length + 0.1f);
        }

        /// <summary>A sound without position (stings), from <paramref name="from"/> seconds in.</summary>
        public static void Flat(AudioClip clip, float volume = 1f, float from = 0f)
        {
            if (clip == null || Instance == null) return;
            var go = new GameObject("Sting " + clip.name);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume * Soulslike.AudioVolume.Effects;
            source.spatialBlend = 0f;
            source.time = Mathf.Min(from, clip.length - 0.05f);
            source.Play();
            Destroy(go, clip.length - source.time + 0.1f);
        }

        /// <summary>The audible part of a clip: where it rises above, and last
        /// falls below, -40 dB (generated music fades in and out).</summary>
        public static Vector2 Audible(AudioClip clip)
        {
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0)) return new Vector2(0f, clip.length);
            const float threshold = 0.01f; // -40 dBFS
            int window = clip.frequency / 20 * clip.channels; // 50 ms
            int first = 0, last = data.Length;
            for (int i = 0; i + window <= data.Length; i += window)
                if (Rms(data, i, window) > threshold) { first = i; break; }
            for (int i = data.Length - window; i >= 0; i -= window)
                if (Rms(data, i, window) > threshold) { last = i + window; break; }
            float perSecond = clip.frequency * clip.channels;
            return new Vector2(first / perSecond, last / perSecond);
        }

        static float Rms(float[] data, int start, int count)
        {
            double sum = 0;
            for (int i = start; i < start + count; i++) sum += data[i] * data[i];
            return Mathf.Sqrt((float)(sum / count));
        }

        /// <summary>
        /// A seamless loop of a clip's audible part: two sources, the next
        /// one starting from the audible beginning and crossfading in while
        /// the current one plays its last seconds.
        /// </summary>
        public class Loop
        {
            readonly AudioSource[] sources;
            readonly float baseVolume;
            readonly bool music;
            int current;
            /// <summary>Its volume as the player's settings have it now.</summary>
            float volume => baseVolume * (music ? Soulslike.AudioVolume.Music : Soulslike.AudioVolume.Effects);
            Vector2 range;
            float fadeStart = -1f;
            const float Fade = 2.5f;

            public AudioClip Clip { get; private set; }
            public bool Playing => sources[current].isPlaying;

            public Loop(GameObject owner, bool spatial, float volume, float minDistance = 3f, float maxDistance = 30f, bool music = false)
            {
                baseVolume = volume;
                this.music = music;
                sources = new AudioSource[2];
                for (int i = 0; i < 2; i++)
                {
                    var source = owner.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = spatial ? 1f : 0f;
                    source.minDistance = minDistance;
                    source.maxDistance = maxDistance;
                    sources[i] = source;
                }
            }

            public void Play(AudioClip clip)
            {
                Stop();
                Clip = clip;
                range = Audible(clip);
                var source = sources[current];
                source.clip = clip;
                source.volume = volume;
                source.time = range.x;
                source.Play();
            }

            public void Stop()
            {
                foreach (var s in sources) s.Stop();
                fadeStart = -1f;
            }

            public void Update()
            {
                var active = sources[current];
                if (Clip == null || !active.isPlaying) return;
                // A volume changed in the menu applies at once.
                if (fadeStart < 0f) active.volume = volume;
                float fade = Mathf.Min(Fade, (range.y - range.x) * 0.25f);
                if (fadeStart < 0f && active.time >= range.y - fade)
                {
                    var next = sources[1 - current];
                    next.clip = Clip;
                    next.time = range.x;
                    next.volume = 0f;
                    next.Play();
                    fadeStart = Time.unscaledTime;
                }
                if (fadeStart >= 0f)
                {
                    // Equal-power crossfade.
                    float t = Mathf.Clamp01((Time.unscaledTime - fadeStart) / fade);
                    active.volume = volume * Mathf.Cos(t * Mathf.PI / 2f);
                    sources[1 - current].volume = volume * Mathf.Sin(t * Mathf.PI / 2f);
                    if (t >= 1f)
                    {
                        active.Stop();
                        current = 1 - current;
                        fadeStart = -1f;
                    }
                }
            }
        }
    }
}

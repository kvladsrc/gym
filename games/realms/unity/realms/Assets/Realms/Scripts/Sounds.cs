using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// The game's sounds by name: the name of each is its entry in <see cref="SoundLibrary"/>
    /// (sounds.json). The sound plan of 2026-11-10 and its fal.ai amendments of 2026-11-12 (Claude
    /// Fable 5.2) say where each plays; a sound the library lacks is simply not heard.
    /// </summary>
    public enum Sfx
    {
        // The interface.
        Click,
        ClickOff,
        Tick,
        DialogOpen,
        Accept,
        Back,
        PanelOpen,
        ScreenIn,
        Save,

        // The turn and the kingdom.
        TurnEnd,
        Coins,
        CoinsBig,
        Build,
        BuildStone,
        Stake,
        Grew,
        Warn,
        Rival,
        Recruit,
        Settlers,

        // The hero on the map.
        HeroPick,
        PathSet,
        StepGrass,
        StepStone,
        StepSand,
        PickWood,
        PickStone,
        Shrine,
        Reveal,
        ChestOpen,

        // Battles.
        BattleStart,
        Round,
        Swing,
        HitBlade,
        HitBeast,
        BowShot,
        ArrowHit,
        Fall,
        Defend,
        Heal,
        Web,
        Surrender,
        Flee,
        Retreat,
        StepHoof,
        VoiceBoar,
        VoiceWolf,
        VoiceGoblin,
        VoiceSkeleton,
        VoiceSpider,
        VoiceTroll,

        // Signs: short melodies, always the same.
        Reward,
        LevelUp,
        QuestDone,
        TownLevel,
        HeroHired,
        NewTown,
        Gem,
        Victory,
        Defeat,
        GameWon,
    }

    /// <summary>
    /// The volume sliders, 0 to 10, kept between runs: the whole game and each group. A slider
    /// gives (s/10)^1.5, so 8 is about -3 dB and 0 is silence (the sound plan's П7).
    /// </summary>
    public static class SoundSettings
    {
        const string MasterKey = "Realms.Volume.Master";
        static readonly string[] Keys = { "Realms.Volume.Interface", "Realms.Volume.World", "Realms.Volume.Ambience", "Realms.Volume.Music" };
        static readonly int[] Defaults = { 7, 8, 7, 6 };
        const int MasterDefault = 8;

        static int[] levels;
        static int master = -1;

        static void Read()
        {
            if (levels != null)
                return;
            levels = new int[Keys.Length];
            for (int i = 0; i < Keys.Length; i++)
                levels[i] = Mathf.Clamp(PlayerPrefs.GetInt(Keys[i], Defaults[i]), 0, 10);
            master = Mathf.Clamp(PlayerPrefs.GetInt(MasterKey, MasterDefault), 0, 10);
        }

        public static int Master
        {
            get
            {
                Read();
                return master;
            }
            set
            {
                Read();
                master = Mathf.Clamp(value, 0, 10);
                PlayerPrefs.SetInt(MasterKey, master);
            }
        }

        public static int Level(SoundGroup group)
        {
            Read();
            return levels[(int)group];
        }

        public static void SetLevel(SoundGroup group, int value)
        {
            Read();
            levels[(int)group] = Mathf.Clamp(value, 0, 10);
            PlayerPrefs.SetInt(Keys[(int)group], levels[(int)group]);
        }

        /// <summary>Every slider at its default, kept («Вернуть как было» in the settings).</summary>
        public static void Reset()
        {
            Master = MasterDefault;
            for (int i = 0; i < Keys.Length; i++)
                SetLevel((SoundGroup)i, Defaults[i]);
        }

        /// <summary>
        /// The sliders at their defaults for this run only, the players' own staying saved: a
        /// capture records the mix that the sound plan's numbers are for.
        /// </summary>
        public static void UseDefaultsForThisRun()
        {
            levels = (int[])Defaults.Clone();
            master = MasterDefault;
        }

        /// <summary>The sliders as a line for the logs: the whole game, then each group.</summary>
        public static string Describe()
        {
            Read();
            return $"{master} | {string.Join(" ", levels)}";
        }

        /// <summary>The factor of the group's sounds: the whole game's slider times the group's.</summary>
        public static float Gain(SoundGroup group) => Curve(Master) * Curve(Level(group));

        static float Curve(int s) => s <= 0 ? 0f : Mathf.Pow(s / 10f, 1.5f);
    }

    /// <summary>
    /// Plays the game's sounds: a dozen voices, at most <c>limit</c> of one sound at once and
    /// not twice within its <c>cooldown</c>, the least important giving way when the voices run
    /// out; random pitch and level on the frequent sounds, never on the signs; the music and the
    /// ambience stepping back while a sign sounds (the sound plan's П4). With
    /// <see cref="LogPath"/> set (the captures) every sound played is written there.
    /// </summary>
    public sealed class Sounds : MonoBehaviour
    {
        sealed class Voice
        {
            public AudioSource source;
            public string name;
            public SoundGroup group;
            public int priority;
            public float started, level;
            public int frame;
        }

        const int VoiceCount = 12;

        static Sounds instance;

        /// <summary>Where the captures keep the sounds played (sounds.log); null outside them.</summary>
        public static string LogPath;

        /// <summary>How far the camera is, 0 near to 1 far: sounds on the map fade to half with it.</summary>
        public static float Far;

        SoundLibrary library;
        readonly Dictionary<string, SoundLibrary.Entry> entries = new Dictionary<string, SoundLibrary.Entry>();
        readonly Dictionary<string, float> lastStart = new Dictionary<string, float>();
        readonly Dictionary<string, int> lastTake = new Dictionary<string, int>();
        readonly HashSet<string> missing = new HashSet<string>();
        readonly List<Voice> voices = new List<Voice>();
        float musicDuck = 1f, ambienceDuck = 1f, signUntil = -100f;
        StreamWriter log;

        /// <summary>The library, or null when the game has no sounds.</summary>
        public static SoundLibrary Library => instance != null ? instance.library : null;

        /// <summary>The music's level under the signs now, 0 to 1.</summary>
        public static float MusicDuck => instance != null ? instance.musicDuck : 1f;

        /// <summary>The ambience's level under the signs now, 0 to 1.</summary>
        public static float AmbienceDuck => instance != null ? instance.ambienceDuck : 1f;

        /// <summary>Seconds since the last sign ended (negative while one sounds).</summary>
        public static float SinceSign => instance != null ? Time.unscaledTime - instance.signUntil : 100f;

        /// <summary>The last time an interface sound answered the players: the music rests when they are away.</summary>
        public static float LastTouch { get; private set; }

        /// <summary>The players did something (a press, a key, a turn).</summary>
        public static void Touch() => LastTouch = Time.unscaledTime;

        public static Sounds Create(GameObject host)
        {
            var sounds = host.AddComponent<Sounds>();
            return sounds;
        }

        void Awake()
        {
            instance = this;
            LastTouch = Time.unscaledTime;
            library = SoundLibrary.Load();
            if (library != null)
            {
                foreach (SoundLibrary.Entry entry in library.entries)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.name))
                        entries[entry.name] = entry;
                }
            }
            for (int i = 0; i < VoiceCount; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.loop = false;
                source.dopplerLevel = 0f;
                voices.Add(new Voice { source = source, priority = -1 });
            }
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
            log?.Dispose();
            log = null;
        }

        /// <summary>Plays the sound; <paramref name="semitones"/> shifts it, <paramref name="pan"/> -1 left to 1 right.</summary>
        public static bool Play(Sfx sfx, float gain = 1f, float semitones = 0f, float pan = 0f) =>
            instance != null && instance.Begin(sfx.ToString(), gain, semitones, pan);

        /// <summary>Plays the sound of a place on the map: to the side it is on the screen, softer from afar.</summary>
        public static bool PlayAt(Sfx sfx, Vector3 point, float gain = 1f, float semitones = 0f)
        {
            if (instance == null)
                return false;
            float pan = 0f;
            Camera camera = Camera.main;
            if (camera != null)
            {
                Vector3 at = camera.WorldToViewportPoint(point);
                if (at.z > 0f)
                    pan = Mathf.Clamp((at.x - 0.5f) * 1.2f, -0.5f, 0.5f);
            }
            return instance.Begin(sfx.ToString(), gain * Mathf.Lerp(1f, 0.5f, Far), semitones, pan);
        }

        /// <summary>Plays the sound after <paramref name="delay"/> seconds of real time.</summary>
        public static void Later(Sfx sfx, float delay, float gain = 1f, float semitones = 0f)
        {
            if (instance != null)
                instance.StartCoroutine(instance.After(sfx.ToString(), delay, gain, semitones, 0f));
        }

        System.Collections.IEnumerator After(string name, float delay, float gain, float semitones, float pan)
        {
            yield return new WaitForSecondsRealtime(delay);
            Begin(name, gain, semitones, pan);
        }

        /// <summary>Whether the library has the sound.</summary>
        public static bool Has(Sfx sfx) =>
            instance != null && instance.entries.TryGetValue(sfx.ToString(), out SoundLibrary.Entry entry) && entry.clips.Count > 0;

        bool Begin(string name, float gain, float semitones, float pan)
        {
            if (!entries.TryGetValue(name, out SoundLibrary.Entry entry) || entry.clips.Count == 0)
            {
                if (missing.Add(name))
                    Write($"no clip {name}");
                return false;
            }
            float now = Time.unscaledTime;
            // The same sound twice in one frame is heard once, a little louder (the plan's П4).
            foreach (Voice playing in voices)
            {
                if (playing.name == name && playing.frame == Time.frameCount && playing.source.isPlaying)
                {
                    playing.level = Mathf.Min(1f, playing.level * 1.26f);
                    playing.source.volume = Level(playing);
                    return true;
                }
            }
            if (lastStart.TryGetValue(name, out float last) && now - last < entry.cooldown)
                return false;
            // One tune at a time: a tune that comes while another plays waits for its end (the
            // capture of 2026-11-12 logged a finished task's tune over the victory's), and one that
            // would wait longer than six seconds is no news any more.
            if (entry.symbolic && now < signUntil - 0.05f)
            {
                float wait = signUntil - now + 0.15f;
                if (wait > 6f)
                    return false;
                Write($"{name} waits {wait:0.00} s for the tune before it");
                StartCoroutine(After(name, wait, gain, semitones, pan));
                return true;
            }
            Voice voice = null;
            int count = 0;
            Voice oldest = null;
            foreach (Voice playing in voices)
            {
                if (playing.name != name || !playing.source.isPlaying)
                    continue;
                count++;
                if (oldest == null || playing.started < oldest.started)
                    oldest = playing;
            }
            if (count >= Mathf.Max(1, entry.limit))
            {
                // An interface sound answers every press: the newest takes the oldest's place.
                if (entry.group != SoundGroup.Interface)
                    return false;
                voice = oldest;
                voice.source.Stop();
            }
            voice ??= Free(entry.priority);
            if (voice == null)
                return false;
            int take = Take(name, entry);
            float level = entry.volume * gain;
            float pitch = Mathf.Pow(2f, semitones / 12f);
            if (!entry.symbolic)
            {
                level *= Mathf.Pow(10f, UnityEngine.Random.Range(-entry.spread, entry.spread) / 20f);
                pitch *= 1f + UnityEngine.Random.Range(-entry.pitch, entry.pitch);
            }
            AudioClip clip = entry.clips[take];
            voice.name = name;
            voice.group = entry.group;
            voice.priority = entry.priority;
            voice.started = now;
            voice.frame = Time.frameCount;
            voice.level = Mathf.Clamp01(level);
            voice.source.clip = clip;
            voice.source.pitch = pitch;
            voice.source.panStereo = Mathf.Clamp(pan, -1f, 1f);
            voice.source.volume = Level(voice);
            voice.source.Play();
            lastStart[name] = now;
            if (entry.symbolic)
                signUntil = Mathf.Max(signUntil, now + clip.length / Mathf.Max(0.1f, pitch));
            Write($"{name} {take + 1}/{entry.clips.Count} level {voice.level:0.00} pitch {pitch:0.000} pan {pan:0.00}");
            return true;
        }

        static float Level(Voice voice) => voice.level * SoundSettings.Gain(voice.group);

        /// <summary>A free voice, or the least important and oldest one if it matters no more than <paramref name="priority"/>.</summary>
        Voice Free(int priority)
        {
            Voice weakest = null;
            foreach (Voice voice in voices)
            {
                if (!voice.source.isPlaying)
                    return voice;
                if (weakest == null || voice.priority < weakest.priority
                    || (voice.priority == weakest.priority && voice.started < weakest.started))
                    weakest = voice;
            }
            if (weakest == null || weakest.priority > priority)
                return null;
            weakest.source.Stop();
            return weakest;
        }

        /// <summary>A take of the sound: a sign always the first, the others never the same twice running.</summary>
        int Take(string name, SoundLibrary.Entry entry)
        {
            int count = entry.clips.Count;
            if (count == 1 || entry.symbolic)
                return 0;
            int last = lastTake.TryGetValue(name, out int l) ? l : -1;
            int take = UnityEngine.Random.Range(0, last >= 0 ? count - 1 : count);
            if (last >= 0 && take >= last)
                take++;
            lastTake[name] = take;
            return take;
        }

        void Update()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            bool sign = now < signUntil;
            // Down by 12 dB (the music) and 6 dB (the ambience) in 0.15 s, back in a second.
            musicDuck = Toward(musicDuck, sign ? 0.25f : 1f, dt);
            ambienceDuck = Toward(ambienceDuck, sign ? 0.5f : 1f, dt);
            // The sliders reach the sounds already playing.
            foreach (Voice voice in voices)
            {
                if (voice.source.isPlaying)
                    voice.source.volume = Level(voice);
            }
        }

        static float Toward(float value, float target, float dt) =>
            Mathf.MoveTowards(value, target, dt / (target < value ? 0.15f : 1f));

        void Write(string line)
        {
            if (LogPath == null)
                return;
            try
            {
                log ??= new StreamWriter(LogPath, true);
                // Real time, as the capture's mix.wav runs on it.
                log.WriteLine($"{Time.frameCount}\t{Time.realtimeSinceStartupAsDouble:0.000}\t{line}");
                log.Flush();
            }
            catch (IOException)
            {
                LogPath = null;
            }
        }

        /// <summary>A line in the sounds' log of the captures, such as the music's or the ambience's changes.</summary>
        public static void Note(string line) => instance?.Write(line);
    }
}

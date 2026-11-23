using System.Collections.Generic;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// The music, in phrases with silence between them rather than all the time (the sound plan's
    /// П6 and its fal.ai amendments of 2026-11-12): the theme once in the main menu (again after
    /// 90 s there) and after a won crown; on the map a phrase after the turn's report or 20–60 s
    /// after the last one, none while the players have been away six minutes; in battle the
    /// battle pieces one after another with a breath between them, gone in 0.8 s at the end.
    /// Library entries: MusicTheme, MusicMap (the phrases), MusicBattle and MusicLair (a lair's
    /// or a troll's fight; the battle pieces stand in for it when it has none).
    /// </summary>
    public sealed class Music : MonoBehaviour
    {
        public enum Mood
        {
            Silent,
            Menu,
            Map,
            Battle,
        }

        const float FadeIn = 1f, FadeOut = 0.8f, PhraseTail = 3f, Away = 360f;

        static Music instance;

        readonly AudioSource[] sources = new AudioSource[2];
        readonly float[] fade = new float[2], fadeSpeed = new float[2];
        readonly bool[] logged = new bool[2];
        readonly SoundLibrary.Entry[] playing = new SoundLibrary.Entry[2];
        readonly List<AudioClip> recent = new List<AudioClip>();
        int current;
        Mood mood = Mood.Silent;
        float nextAt = float.MaxValue, battleNextAt = float.MaxValue;
        bool lairFight;
        int battleTake;

        public static Mood Now => instance != null ? instance.mood : Mood.Silent;

        public static Music Create(GameObject host) => host.AddComponent<Music>();

        void Awake()
        {
            instance = this;
            for (int i = 0; i < 2; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
                sources[i].loop = false;
            }
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        static SoundLibrary.Entry Entry(string name) => Sounds.Library?.Find(name);

        /// <summary>The main menu: the theme now, the sea and the wind after it.</summary>
        public static void Menu()
        {
            if (instance == null || instance.mood == Mood.Menu)
                return;
            instance.mood = Mood.Menu;
            instance.FadeAll();
            instance.nextAt = Time.unscaledTime + 0.5f;
        }

        /// <summary>The map: silence first, a phrase after the next turn's report or in a while.</summary>
        public static void Map()
        {
            if (instance == null || instance.mood == Mood.Map)
                return;
            bool fromBattle = instance.mood == Mood.Battle;
            instance.mood = Mood.Map;
            instance.FadeAll();
            instance.nextAt = Time.unscaledTime + (fromBattle ? Random.Range(20f, 40f) : Random.Range(6f, 15f));
        }

        /// <summary>The turn's report is shown: a phrase starts after its signs, unless one plays or one just ended.</summary>
        public static void TurnEnded()
        {
            if (instance == null || instance.mood != Mood.Map || instance.Busy)
                return;
            instance.nextAt = Mathf.Min(instance.nextAt, Time.unscaledTime + 2.5f);
        }

        /// <summary>A battle begins: its music after the battle's opening drums.</summary>
        public static void Battle(bool lair)
        {
            if (instance == null)
                return;
            instance.mood = Mood.Battle;
            instance.lairFight = lair;
            instance.battleTake = 0;
            instance.FadeAll();
            instance.battleNextAt = Time.unscaledTime + 1.6f;
        }

        /// <summary>The battle's end: the music leaves in 0.8 s for the result's sign.</summary>
        public static void BattleEnd()
        {
            if (instance == null || instance.mood != Mood.Battle)
                return;
            instance.FadeAll();
            instance.battleNextAt = float.MaxValue;
        }

        /// <summary>The crown won: the theme once more, after the sign.</summary>
        public static void Crowned()
        {
            if (instance == null)
                return;
            instance.FadeAll();
            instance.StartCoroutine(instance.ThemeLater(4f));
        }

        System.Collections.IEnumerator ThemeLater(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Begin(Entry("MusicTheme"), false);
        }

        /// <summary>
        /// The music's slider moved: if no music plays, a phrase of the moment starts at once, so
        /// that its level can be heard (the sound plan's П7): the theme in the main menu, a battle
        /// piece in a battle, a map phrase elsewhere.
        /// </summary>
        public static void Sample()
        {
            if (instance == null || instance.sources[0].isPlaying || instance.sources[1].isPlaying)
                return;
            Music m = instance;
            SoundLibrary.Entry entry = m.mood switch
            {
                Mood.Menu => Entry("MusicTheme"),
                Mood.Battle => m.lairFight ? Entry("MusicLair") ?? Entry("MusicBattle") : Entry("MusicBattle") ?? Entry("MusicLair"),
                _ => Entry("MusicMap"),
            };
            if (!m.Begin(entry, false))
                return;
            float after = Time.unscaledTime + m.sources[m.current].clip.length;
            if (m.mood == Mood.Map)
                m.nextAt = after + Random.Range(20f, 60f);
            else if (m.mood == Mood.Menu)
                m.nextAt = after + 90f;
            else if (m.mood == Mood.Battle)
                m.battleNextAt = after + 1.5f;
        }

        /// <summary>Silence, at once softly.</summary>
        public static void Stop()
        {
            if (instance == null)
                return;
            instance.mood = Mood.Silent;
            instance.FadeAll();
        }

        bool Busy => sources[current].isPlaying && fadeSpeed[current] >= 0f;

        void FadeAll()
        {
            for (int i = 0; i < 2; i++)
            {
                if (sources[i].isPlaying)
                    fadeSpeed[i] = -1f / FadeOut;
            }
        }

        /// <summary>Starts a clip of the entry on the free source; false when there is none.</summary>
        bool Begin(SoundLibrary.Entry entry, bool loop, int take = -1)
        {
            if (entry == null || entry.clips.Count == 0)
                return false;
            AudioClip clip;
            if (take >= 0)
                clip = entry.clips[take % entry.clips.Count];
            else
            {
                // Not one of the last two heard.
                var choices = new List<AudioClip>();
                foreach (AudioClip c in entry.clips)
                {
                    if (!recent.Contains(c))
                        choices.Add(c);
                }
                if (choices.Count == 0)
                    choices.AddRange(entry.clips);
                clip = choices[Random.Range(0, choices.Count)];
            }
            recent.Add(clip);
            while (recent.Count > 2)
                recent.RemoveAt(0);
            current = 1 - current;
            AudioSource source = sources[current];
            if (logged[current] && source.isPlaying)
                Sounds.Note($"music stops {source.clip.name}");
            source.Stop();
            logged[current] = true;
            source.clip = clip;
            source.loop = loop;
            source.time = 0f;
            playing[current] = entry;
            fade[current] = 0f;
            fadeSpeed[current] = 1f / FadeIn;
            source.volume = 0f;
            source.Play();
            Sounds.Note($"music {entry.name} {clip.name}");
            return true;
        }

        void Update()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            for (int i = 0; i < 2; i++)
            {
                AudioSource source = sources[i];
                if (!source.isPlaying)
                {
                    // The sound log's ends, for measuring how much of the time music plays.
                    if (logged[i])
                        Sounds.Note($"music ends {source.clip.name}");
                    logged[i] = false;
                    continue;
                }
                fade[i] = Mathf.Clamp01(fade[i] + fadeSpeed[i] * dt);
                if (fadeSpeed[i] < 0f && fade[i] <= 0f)
                {
                    Sounds.Note($"music fades out {source.clip.name}");
                    logged[i] = false;
                    source.Stop();
                    continue;
                }
                // A phrase that does not end by itself is led away over its last seconds.
                float tail = 1f;
                if (!source.loop && source.clip != null)
                    tail = Mathf.Clamp01((source.clip.length - source.time) / PhraseTail);
                SoundLibrary.Entry entry = playing[i];
                float level = entry != null ? entry.volume : 1f;
                source.volume = level * fade[i] * tail * SoundSettings.Gain(SoundGroup.Music) * Sounds.MusicDuck;
            }

            bool busy = sources[0].isPlaying || sources[1].isPlaying;
            switch (mood)
            {
                case Mood.Menu:
                    if (!busy && now >= nextAt)
                    {
                        Begin(Entry("MusicTheme"), false);
                        nextAt = now + 90f + (sources[current].clip != null ? sources[current].clip.length : 0f);
                    }
                    break;
                case Mood.Map:
                    // No new phrase while a sign sounds or just after it, nor for players away.
                    if (!busy && now >= nextAt && Sounds.SinceSign > 5f && now - Sounds.LastTouch < Away)
                    {
                        if (Begin(Entry("MusicMap"), false))
                            nextAt = now + sources[current].clip.length + Random.Range(20f, 60f);
                        else
                            nextAt = float.MaxValue;
                    }
                    break;
                case Mood.Battle:
                    if (!busy && now >= battleNextAt)
                    {
                        SoundLibrary.Entry fight = lairFight ? Entry("MusicLair") ?? Entry("MusicBattle") : Entry("MusicBattle") ?? Entry("MusicLair");
                        if (Begin(fight, false, battleTake++))
                            battleNextAt = now + sources[current].clip.length + 1.5f;
                        else
                            battleNextAt = float.MaxValue;
                    }
                    break;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Realms
{
    /// <summary>The volume slider a sound answers to.</summary>
    public enum SoundGroup
    {
        Interface,
        World,
        Ambience,
        Music,
    }

    /// <summary>
    /// The sounds fetched from Assets Studio (tools/fetch_sounds.py, after sounds.json) as the
    /// editor's Setup imported them: for every name its takes and how it is played. Lives in
    /// Art/Resources/RealmsSounds.asset, which is not in Git; without it the game is silent and
    /// plays the same.
    /// </summary>
    public sealed class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string name;
            public SoundGroup group;

            /// <summary>The level against the other sounds, 0 to 1 (the mix, not the file).</summary>
            public float volume = 1f;

            /// <summary>The random change of pitch, as a share either way (0.04: ±4 %).</summary>
            public float pitch;

            /// <summary>The random change of loudness either way, in decibels.</summary>
            public float spread;

            /// <summary>How many of it may sound at once.</summary>
            public int limit = 2;

            /// <summary>The least time between two starts, seconds.</summary>
            public float cooldown = 0.05f;

            /// <summary>Which sound gives way when the voices run out: the lower first.</summary>
            public int priority = 1;

            /// <summary>A sign (a jingle, the turn's end): always the same take, pitch and level, and the music steps back for it.</summary>
            public bool symbolic;

            public List<AudioClip> clips = new List<AudioClip>();
        }

        public List<Entry> entries = new List<Entry>();

        public static SoundLibrary Load() => Resources.Load<SoundLibrary>("RealmsSounds");

        public Entry Find(string name)
        {
            foreach (Entry entry in entries)
            {
                if (entry.name == name)
                    return entry;
            }
            return null;
        }
    }
}

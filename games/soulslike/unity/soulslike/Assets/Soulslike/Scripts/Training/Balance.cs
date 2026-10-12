using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Soulslike.Training
{
    /// <summary>
    /// The fighters' balance, as data (training/balance.json): each action's
    /// damage, speed and stagger (the time a hit stands the other still).
    ///
    /// - <see cref="Base"/>: a hit's damage and stagger by what strikes (the
    ///   sword, the hammer, a limb).
    /// - <see cref="Rules"/>: factors, each for what it names: the weapon in
    ///   hand (<see cref="Rule.held"/>: sword, hammer, hands) and the
    ///   animation (<see cref="Rule.blow"/>: a clip's name, or roll), either,
    ///   both, or neither (all). Every rule naming an action multiplies it.
    ///
    /// The speed plays the whole animation that much faster; its damage (by
    /// the striking point's speed) and its energy (by its length) follow.
    /// The file is read again as an episode begins when it has changed
    /// (<see cref="Refresh"/>): a running training takes a change at its next
    /// fights. In a player it is SOULSLIKE_BALANCE's file (train.sh sets it),
    /// in the editor training's own; else (the game) the copy its builder put
    /// in the scene (<see cref="BalanceHolder"/>).
    /// </summary>
    public class Balance
    {
        public class Part
        {
            public float damage, stagger;
            public string note;
        }

        public class Rule
        {
            public string held, blow, note;
            public float damage = 1f, speed = 1f, stagger = 1f;
        }

        [JsonProperty("base")] public Dictionary<string, Part> Base = new();
        [JsonProperty("rules")] public List<Rule> Rules = new();

        /// <summary>Changes each time another balance is taken in (fighters
        /// work their actions out again).</summary>
        public static int Version { get; private set; }
        /// <summary>A short hash of the balance in use (the stats'
        /// Balance/version), 0–999999.</summary>
        public static int Hash { get; private set; }

        static Balance current;
        static string path;
        static DateTime written;
        static float checkedAt = float.NegativeInfinity;

        public static Balance Current
        {
            get
            {
                if (current == null) Load();
                return current;
            }
        }

        /// <summary>Reads the file again if it has changed (at most every few seconds).</summary>
        public static void Refresh()
        {
            if (current == null) { Load(); return; }
            if (path == null || Time.realtimeSinceStartup - checkedAt < 2f) return;
            checkedAt = Time.realtimeSinceStartup;
            if (File.Exists(path) && File.GetLastWriteTimeUtc(path) != written) Read();
        }

        static void Load()
        {
            path = Environment.GetEnvironmentVariable("SOULSLIKE_BALANCE");
            if (string.IsNullOrEmpty(path) && Application.isEditor)
                path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../training/balance.json"));
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                checkedAt = Time.realtimeSinceStartup;
                if (Read()) return;
            }
            path = null;
            var holder = UnityEngine.Object.FindAnyObjectByType<BalanceHolder>();
            if (holder == null || !Take(holder.json))
            {
                Debug.LogError("Soulslike: no balance (training/balance.json, or the scene's)");
                Take("{}");
            }
        }

        static bool Read()
        {
            written = File.GetLastWriteTimeUtc(path);
            try
            {
                return Take(File.ReadAllText(path));
            }
            catch (IOException e)
            {
                // Being written: the next look reads it.
                written = default;
                Debug.LogWarning($"Soulslike: balance not read: {e.Message}");
                return current != null;
            }
        }

        /// <summary>Takes a balance in, if it reads; else keeps the one in use.</summary>
        static bool Take(string json)
        {
            Balance read;
            try
            {
                read = JsonConvert.DeserializeObject<Balance>(json);
            }
            catch (JsonException e)
            {
                Debug.LogWarning($"Soulslike: balance not taken: {e.Message}");
                return false;
            }
            if (read == null) return false;
            string text = JsonConvert.SerializeObject(read);
            int hash = 0;
            unchecked
            {
                foreach (char c in text) hash = hash * 31 + c;
            }
            hash = (int)((uint)hash % 1000000u);
            if (current != null && hash == Hash) return true;
            current = read;
            Hash = hash;
            Version++;
            Debug.Log($"Soulslike: balance {Hash:D6}");
            return true;
        }

        /// <summary>An action's damage, speed and stagger: its striking part's
        /// base (none: 0) and the factors of the rules naming it.</summary>
        public (float damage, float speed, float stagger) Of(Weapon held, string blow, string strikes)
        {
            float damage = 0f, speed = 1f, stagger = 0f;
            if (strikes != null && Base.TryGetValue(strikes, out var part))
            {
                damage = part.damage;
                stagger = part.stagger;
            }
            string hand = Name(held);
            foreach (var rule in Rules)
            {
                if (!string.IsNullOrEmpty(rule.held) && rule.held != hand) continue;
                if (!string.IsNullOrEmpty(rule.blow) && rule.blow != blow) continue;
                damage *= rule.damage;
                speed *= rule.speed;
                stagger *= rule.stagger;
            }
            return (damage, speed, stagger);
        }

        /// <summary>A weapon's name in the file: sword, hammer, hands.</summary>
        public static string Name(Weapon weapon) => weapon.ToString().ToLowerInvariant();
    }
}

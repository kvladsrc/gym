using System;
using UnityEngine;

namespace Realms
{
    /// <summary>The languages of the game: Russian and English, and the debug "long" one.</summary>
    public enum Language
    {
        Russian,
        English,

        /// <summary>English stretched by about 40 %, to see where a line will not fit (`-lang long`).</summary>
        Long,
    }

    /// <summary>
    /// The language the game speaks (the interface plan's 3.6, 2026-11-11). Every phrase is
    /// written in place as <c>T("русский", "English")</c> (with <c>using static Realms.Lang;</c>),
    /// so a phrase without its English does not compile, as the plan asks of its abstract class,
    /// without moving 790 phrases away from the code that shows them. A phrase with a number or a
    /// name is built whole inside each language: Russian counts with <see cref="Words.Plural"/>,
    /// English with <see cref="En"/>. The game opens in the system's language (Russian on a
    /// Russian system, English elsewhere) until the players choose one in the menu; `-lang ru`,
    /// `-lang en` or `-lang long` chooses one for this run only.
    /// </summary>
    public static class Lang
    {
        const string Key = "Realms.Language";

        static bool ready;
        static Language now;

        /// <summary>The language the interface speaks now.</summary>
        public static Language Now
        {
            get
            {
                if (!ready)
                    Start();
                return now;
            }
        }

        public static bool Russian => Now == Language.Russian;

        /// <summary>The phrase in the language the interface speaks.</summary>
        public static string T(string ru, string en)
        {
            switch (Now)
            {
                case Language.Russian: return ru;
                case Language.Long: return Stretched(en);
                default: return en;
            }
        }

        /// <summary>The English word for a count: "1 spearman", "2 spearmen".</summary>
        public static string En(int n, string one, string many) => n == 1 || n == -1 ? one : many;

        /// <summary>An English place: 1st, 2nd, 3rd, 4th, 11th, 21st.</summary>
        public static string Ordinal(int n)
        {
            int tens = Math.Abs(n) % 100, ones = Math.Abs(n) % 10;
            string end = tens >= 11 && tens <= 13 ? "th" : ones == 1 ? "st" : ones == 2 ? "nd" : ones == 3 ? "rd" : "th";
            return n + end;
        }

        /// <summary>The language's own name, never translated: the players look for their own.</summary>
        public static string SelfName(Language language) =>
            language == Language.Russian ? "\u0420\u0443\u0441\u0441\u043a\u0438\u0439" : "English";

        /// <summary>
        /// The scene started again in another language (RealmsGame.SwitchLanguage); with
        /// <see cref="BackIntoGame"/> the game the players left opens again under the loading screen.
        /// </summary>
        public static bool Restarted, BackIntoGame;

        /// <summary>The players chose a language: kept for the next start (a capture keeps nothing).</summary>
        public static void Choose(Language language, bool keep)
        {
            ready = true;
            now = language;
            if (!keep)
                return;
            PlayerPrefs.SetString(Key, language == Language.Russian ? "ru" : "en");
            PlayerPrefs.Save();
        }

        static void Start()
        {
            ready = true;
            // The rules keep the names' Russian keys; the language shows them.
            Core.Names.Shown = NameWords.Shown;
            string[] args = Environment.GetCommandLineArgs();
            bool capture = false, promo = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-lang" && i + 1 < args.Length && Parse(args[i + 1], out now))
                {
                    Debug.Log($"Language: {now} (-lang)");
                    return;
                }
                capture |= args[i] == "-realmsCapture";
                promo |= args[i] == "-realmsPromo";
            }
            // The captures and the promo are staged, the players' own choice neither read nor
            // changed (a capture taken while the players had just tried English came out in
            // English, 2026-11-11): the captures in Russian unless -lang says otherwise, the promo
            // always in English (the family wants the video in English only).
            if (capture || promo)
            {
                now = promo ? Language.English : Language.Russian;
                Debug.Log($"Language: {now} (a staged run)");
                return;
            }
            if (Parse(PlayerPrefs.GetString(Key, ""), out now))
            {
                Debug.Log($"Language: {now} (the players' choice)");
                return;
            }
            // Players from before the English version played in Russian: it stays theirs, whatever
            // the system says (the family's laptop runs a C locale, which reads as English).
            if (PlayerPrefs.HasKey("Realms.Flag") || PlayerPrefs.HasKey("Realms.Difficulty") || PlayerPrefs.HasKey("Realms.Guide.Turns"))
            {
                Choose(Language.Russian, true);
                Debug.Log("Language: Russian (played before the English version)");
                return;
            }
            SystemLanguage system = Application.systemLanguage;
            now = system == SystemLanguage.Russian || system == SystemLanguage.Belarusian || system == SystemLanguage.Ukrainian
                ? Language.Russian
                : Language.English;
            Debug.Log($"Language: {now} (the system's {system})");
        }

        static bool Parse(string word, out Language language)
        {
            switch (word)
            {
                case "ru": language = Language.Russian; return true;
                case "en": language = Language.English; return true;
                case "long": language = Language.Long; return true;
                default: language = Language.Russian; return false;
            }
        }

        /// <summary>
        /// The English stretched by about 40 % where it reads: each word's vowels doubled in turn
        /// until the line is long enough, keeping numbers and braces as they are.
        /// </summary>
        static string Stretched(string en)
        {
            if (string.IsNullOrEmpty(en))
                return en;
            int want = (int)Math.Ceiling(en.Length * 0.4);
            var text = new System.Text.StringBuilder(en.Length + want);
            bool tag = false;
            foreach (char c in en)
            {
                text.Append(c);
                // Rich text tags (<b>, <color=…>) stay as they are.
                if (c == '<')
                    tag = true;
                else if (c == '>')
                    tag = false;
                else if (!tag && want > 0 && "aeiouAEIOU".IndexOf(c) >= 0)
                {
                    text.Append(char.ToLowerInvariant(c));
                    want--;
                }
            }
            if (want > 0)
                text.Append('~', want);
            return text.ToString();
        }
    }
}

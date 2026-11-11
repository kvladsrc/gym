using System.Collections.Generic;
using Realms.Core;

namespace Realms
{
    /// <summary>
    /// The names of kingdoms, towns and heroes in English. The rules keep the Russian name as the
    /// name's key (Core/Names.cs, HeroRules.Names, Names.Numbered), so a save opens in either language;
    /// the language gives the word shown (the interface plan's 3.6). The English tables stand beside
    /// the Russian ones in Core/Names.cs.
    /// </summary>
    public static class NameWords
    {
        static Dictionary<string, string> english;

        /// <summary>The name as the language shows it; a name the players typed stays as typed.</summary>
        public static string Shown(string name)
        {
            if (string.IsNullOrEmpty(name) || Lang.Russian)
                return name;
            if (english == null)
            {
                english = new Dictionary<string, string>();
                Add(Names.Kingdoms, Names.KingdomsEnglish);
                Add(Names.Cities, Names.CitiesEnglish);
                Add(HeroRules.Names, Names.HeroesEnglish);
            }
            if (english.TryGetValue(name, out string shown))
                return Lang.T(name, shown);
            if (name.StartsWith(Names.NumberedTown, System.StringComparison.Ordinal))
                return Lang.T(name, "Town " + name.Substring(Names.NumberedTown.Length));
            return name;
        }

        static void Add(string[] russian, string[] words)
        {
            for (int i = 0; i < russian.Length && i < words.Length; i++)
                english[russian[i]] = words[i];
        }
    }
}

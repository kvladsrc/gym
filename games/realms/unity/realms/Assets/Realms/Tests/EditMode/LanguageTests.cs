using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>
    /// Both languages stay whole while the game grows (the interface plan's 3.6, 2026-11-11). A
    /// phrase is written in place as <c>T("русский", "English")</c>, so the compiler wants both;
    /// these tests catch a Russian phrase written into the code without T, and keep the names'
    /// tables of the two languages in step.
    /// </summary>
    public sealed class LanguageTests
    {
        static readonly Regex Cyrillic = new Regex("[А-Яа-яЁё]");

        /// <summary>Assets/Realms, from where this file was compiled.</summary>
        static string Realms([CallerFilePath] string here = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", ".."));

        [Test]
        public void EveryRussianPhraseOfTheScriptsHasItsEnglish()
        {
            var bare = new List<string>();
            foreach (string path in Directory.GetFiles(Path.Combine(Realms(), "Scripts"), "*.cs"))
            {
                string src = File.ReadAllText(path);
                foreach ((int start, int end) in Literals(src))
                {
                    string literal = src.Substring(start, end - start);
                    if (!Cyrillic.IsMatch(literal))
                        continue;
                    string before = src.Substring(0, start).TrimEnd();
                    bool wrapped = before.EndsWith("T(") && (before.Length < 3 || !char.IsLetterOrDigit(before[before.Length - 3]));
                    if (!wrapped)
                        bare.Add($"{Path.GetFileName(path)}:{Line(src, start)} {literal}");
                }
            }
            Assert.IsEmpty(bare, "Russian written without its English: wrap it as T(\"…\", \"…\")");
        }

        [Test]
        public void TheRulesHoldNoRussianButTheNames()
        {
            var found = new List<string>();
            foreach (string path in Directory.GetFiles(Path.Combine(Realms(), "Core"), "*.cs"))
            {
                string name = Path.GetFileName(path);
                if (name == "Names.cs" || name == "Heroes.cs")
                    continue;
                string src = File.ReadAllText(path);
                foreach ((int start, int end) in Literals(src))
                {
                    if (Cyrillic.IsMatch(src.Substring(start, end - start)))
                        found.Add($"{name}:{Line(src, start)}");
                }
            }
            Assert.IsEmpty(found, "Words for the players belong to the view's language, not to the rules");
        }

        [Test]
        public void EveryNameHasItsEnglishOne()
        {
            Check(Names.Kingdoms, Names.KingdomsEnglish);
            Check(Names.Cities, Names.CitiesEnglish);
            Check(HeroRules.Names, Names.HeroesEnglish);

            static void Check(string[] russian, string[] english)
            {
                Assert.AreEqual(russian.Length, english.Length);
                Assert.AreEqual(english.Length, new HashSet<string>(english).Count, "two names alike");
                foreach (string name in english)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(name));
                    Assert.IsFalse(Cyrillic.IsMatch(name), name);
                }
            }
        }

        [Test]
        public void ANameIsShownThroughTheLanguageAndKeptAsItsKey()
        {
            var world = WorldGen.Generate(4242);
            var shown = Names.Shown;
            try
            {
                Names.Shown = name => "[" + name + "]";
                Assert.AreEqual("[" + world.Countries[0].Name + "]", world.Countries[0].Label);
                Assert.AreEqual("[" + world.Cities[0].Name + "]", world.Cities[0].Label);
                CollectionAssert.Contains(Names.Kingdoms, world.Countries[0].Name);
            }
            finally
            {
                Names.Shown = shown;
            }
        }

        static int Line(string src, int at)
        {
            int line = 1;
            for (int i = 0; i < at; i++)
            {
                if (src[i] == '\n')
                    line++;
            }
            return line;
        }

        /// <summary>The outermost string literals of C# source, comments and characters skipped.</summary>
        static IEnumerable<(int, int)> Literals(string src)
        {
            int i = 0, n = src.Length;
            while (i < n)
            {
                char c = src[i];
                if (c == '/' && i + 1 < n && src[i + 1] == '/')
                {
                    int k = src.IndexOf('\n', i);
                    i = k < 0 ? n : k;
                    continue;
                }
                if (c == '/' && i + 1 < n && src[i + 1] == '*')
                {
                    i = src.IndexOf("*/", i + 2) + 2;
                    continue;
                }
                if (c == '\'')
                {
                    i = SkipChar(src, i);
                    continue;
                }
                if (c == '"' || ((c == '$' || c == '@') && i + 2 < n && (src[i + 1] == '"' || ((src[i + 1] == '$' || src[i + 1] == '@') && src[i + 2] == '"'))))
                {
                    int end = StringEnd(src, i);
                    yield return (i, end);
                    i = end;
                    continue;
                }
                i++;
            }
        }

        static int StringEnd(string src, int i)
        {
            int j = i;
            bool interpolated = false, verbatim = false;
            while (src[j] == '$' || src[j] == '@')
            {
                interpolated |= src[j] == '$';
                verbatim |= src[j] == '@';
                j++;
            }
            j++;
            while (true)
            {
                char c = src[j];
                if (verbatim && c == '"')
                {
                    if (src[j + 1] == '"')
                    {
                        j += 2;
                        continue;
                    }
                    return j + 1;
                }
                if (!verbatim && c == '\\')
                {
                    j += 2;
                    continue;
                }
                if (c == '"')
                    return j + 1;
                if (interpolated && c == '{')
                {
                    if (src[j + 1] == '{')
                    {
                        j += 2;
                        continue;
                    }
                    j = HoleEnd(src, j + 1);
                    continue;
                }
                j++;
            }
        }

        static int HoleEnd(string src, int j)
        {
            int depth = 0;
            while (true)
            {
                char c = src[j];
                if (c == '"' || ((c == '$' || c == '@') && (src[j + 1] == '"' || src[j + 1] == '$' || src[j + 1] == '@')))
                {
                    j = StringEnd(src, j);
                    continue;
                }
                if (c == '\'')
                {
                    j = SkipChar(src, j);
                    continue;
                }
                if (c == '(' || c == '{' || c == '[')
                    depth++;
                else if (c == ')' || c == ']')
                    depth--;
                else if (c == '}')
                {
                    if (depth == 0)
                        return j + 1;
                    depth--;
                }
                j++;
            }
        }

        static int SkipChar(string src, int i)
        {
            int j = i + 1;
            j += src[j] == '\\' ? 2 : 1;
            while (src[j] != '\'')
                j++;
            return j + 1;
        }
    }
}

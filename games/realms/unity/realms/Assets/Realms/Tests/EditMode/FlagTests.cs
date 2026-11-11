using System.Collections.Generic;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    public class FlagTests
    {
        [Test]
        public void EveryFlagSurvivesItsCode()
        {
            for (int p = 0; p < Flags.Patterns; p++)
            {
                for (int e = 0; e < Flags.Emblems; e++)
                {
                    var flag = new Flag((FlagPattern)p, (p + e) % Flags.Tinctures, (p * 3 + 1) % Flags.Tinctures, (Emblem)e, e % Flags.Tinctures);
                    Assert.IsTrue(Flag.TryParse(flag.Code, out Flag back), flag.Code);
                    Assert.AreEqual(flag, back, flag.Code);
                }
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("1.2.3")]
        [TestCase("0.0.0.0.0.0")]
        [TestCase("a.b.c.d.e")]
        [TestCase("-1.0.0.0.0")]
        [TestCase(" 1.2.3.4.5")]
        [TestCase("11.0.0.0.0")]
        [TestCase("0.10.0.0.0")]
        [TestCase("0.0.10.0.0")]
        [TestCase("0.0.0.19.0")]
        [TestCase("0.0.0.0.10")]
        public void AnythingElseIsNoFlag(string code)
        {
            Assert.IsFalse(Flag.TryParse(code, out _));
        }

        /// <summary>
        /// The generated flags stay the same for a map, fly the kingdom's colour and keep heraldry's
        /// rule (a metal on a colour or a colour on a metal), so every part reads; patterns that
        /// would cut through an emblem have none.
        /// </summary>
        [Test]
        public void GeneratedFlagsAreStableAndReadable()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                for (int country = 0; country < Flags.KingdomTincture.Length; country++)
                {
                    Flag flag = Flags.Generate(seed, country);
                    Assert.AreEqual(flag, Flags.Generate(seed, country));
                    Assert.AreEqual(Flags.KingdomTincture[country], flag.Field);
                    Assert.AreNotEqual(Flags.Metal(flag.Field), Flags.Metal(flag.Second), flag.Code);
                    if (flag.Emblem == Emblem.None)
                    {
                        Assert.AreNotEqual(FlagPattern.Plain, flag.Pattern, flag.Code);
                        continue;
                    }
                    Assert.That(new[] { FlagPattern.Plain, FlagPattern.Border, FlagPattern.Band }, Does.Contain(flag.Pattern), flag.Code);
                    int under = flag.Pattern == FlagPattern.Band ? flag.Second : flag.Field;
                    Assert.AreNotEqual(Flags.Metal(under), Flags.Metal(flag.EmblemColour), flag.Code);
                }
            }
        }

        [Test]
        public void EveryPatternButThePlainShowsBothColours()
        {
            for (int p = 0; p < Flags.Patterns; p++)
            {
                var seen = new HashSet<int>();
                for (int y = 0; y < 20; y++)
                {
                    for (int x = 0; x < 30; x++)
                        seen.Add(Flags.Part((FlagPattern)p, (x + 0.5f) / 30f, (y + 0.5f) / 20f));
                }
                Assert.AreEqual((FlagPattern)p == FlagPattern.Plain ? 1 : 2, seen.Count, ((FlagPattern)p).ToString());
            }
        }

        [Test]
        public void EveryKingdomOfANewMapHasItsOwnFlag()
        {
            foreach (int seed in new[] { 3, 42, 2026 })
            {
                World world = WorldGen.Generate(seed);
                var codes = new HashSet<string>();
                foreach (Country country in world.Countries)
                {
                    Assert.AreEqual(Flags.Generate(seed, country.Index), country.Flag);
                Assert.AreEqual(Names.Kingdoms[country.Flag.Field], country.Name);
                    codes.Add(country.Flag.Code);
                }
                Assert.AreEqual(world.Countries.Count, codes.Count);
            }
        }

        /// <summary>
        /// A field is a kingdom's colour on the map, so taking another kingdom's colour gives that
        /// kingdom the old one; its flag keeps its pattern and emblem and still reads.
        /// </summary>
        [Test]
        public void ClaimingAColourSwapsItWithItsKingdom()
        {
            World world = WorldGen.Generate(42);
            int me = world.Player, them = (me + 1) % world.Countries.Count;
            Flag mine = world.Countries[me].Flag, theirs = world.Countries[them].Flag;
            var flag = new Flag(FlagPattern.Border, theirs.Field, Flags.Metal(theirs.Field) ? Flags.Black : Flags.White, Emblem.Dragon, Flags.Metal(theirs.Field) ? Flags.Black : Flags.White);

            Assert.IsTrue(Flags.Claim(world, me, flag));

            Assert.AreEqual(flag, world.Countries[me].Flag);
            Flag swapped = world.Countries[them].Flag;
            Assert.AreEqual(mine.Field, swapped.Field);
            Assert.AreEqual(Names.Kingdoms[theirs.Field], world.Countries[me].Name);
            Assert.AreEqual(Names.Kingdoms[mine.Field], world.Countries[them].Name);
            Assert.AreEqual(theirs.Pattern, swapped.Pattern);
            Assert.AreEqual(theirs.Emblem, swapped.Emblem);
            Assert.AreNotEqual(Flags.Metal(swapped.Field), Flags.Metal(swapped.Second), swapped.Code);
            AssertFieldsDiffer(world);
        }

        [Test]
        public void ANewEmblemOnTheSameFieldChangesNoColour()
        {
            World world = WorldGen.Generate(7);
            Flag before = world.Countries[world.Player].Flag;
            var others = new List<string>();
            foreach (Country country in world.Countries)
                others.Add(country.Flag.Code);
            Flag flag = before;
            flag.Emblem = before.Emblem == Emblem.Lion ? Emblem.Eagle : Emblem.Lion;

            Assert.IsFalse(Flags.Claim(world, world.Player, flag));

            for (int c = 0; c < world.Countries.Count; c++)
                Assert.AreEqual(c == world.Player ? flag.Code : others[c], world.Countries[c].Flag.Code);
        }

        /// <summary>Any field the players pick, one after another, leaves every kingdom a colour of its own.</summary>
        [Test]
        public void EveryFieldKeepsTheKingdomsApart()
        {
            World world = WorldGen.Generate(2026);
            for (int round = 0; round < 3; round++)
            {
                for (int t = 0; t < Flags.Tinctures; t++)
                {
                    Flag flag = world.Countries[world.Player].Flag;
                    flag.Field = (byte)((t * 7 + round) % Flags.Tinctures);
                    Flags.Claim(world, world.Player, flag);
                    Assert.AreEqual(flag.Field, world.Countries[world.Player].Flag.Field);
                    Assert.AreEqual(Names.Kingdoms[flag.Field], world.Countries[world.Player].Name);
                    AssertFieldsDiffer(world);
                }
            }
        }

        [Test]
        public void EveryColourNamesAKingdomOfItsOwn()
        {
            Assert.AreEqual(Flags.Tinctures, Names.Kingdoms.Length);
            Assert.AreEqual(Names.Kingdoms.Length, new HashSet<string>(Names.Kingdoms).Count);
        }

        static void AssertFieldsDiffer(World world)
        {
            var fields = new HashSet<int>();
            foreach (Country country in world.Countries)
            {
                Assert.IsTrue(fields.Add(country.Flag.Field), country.Flag.Code);
                Assert.AreEqual(Names.Kingdoms[country.Flag.Field], country.Name);
            }
        }
    }
}

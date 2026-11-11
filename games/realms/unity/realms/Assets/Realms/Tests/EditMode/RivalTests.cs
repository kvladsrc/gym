using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>The bot's hero and hiring (the economy plan's step 1b, 2026-11-11), which the captures and the measurer play.</summary>
    public class RivalTests
    {
        /// <summary>A 14 × 9 meadow with one kingdom, its capital (5 houses and a quarter, as in a new game) at (2, 4) and the first hero in it; all of it seen.</summary>
        static GameState Meadow(out Hero hero)
        {
            var world = new World(14, 9, 5);
            foreach (Cell c in world.Cells)
                c.Biome = Biome.Meadow;
            var country = new Country { Index = 0, Name = "A" };
            world.Countries.Add(country);
            var capital = new City { Index = 0, Name = "C", Country = 0, Cell = world.IndexOf(2, 4), Capital = true, Houses = 5 };
            world.Cities.Add(capital);
            world.Cells[capital.Cell].City = 0;
            country.Cities.Add(0);
            country.Capital = 0;
            int quarter = world.Neighbor(capital.Cell, 1);
            world.Cells[quarter].District = 0;
            capital.Districts.Add(quarter);
            Hex home = world.Cells[capital.Cell].Hex;
            foreach (Cell c in world.Cells)
            {
                if (Hex.Distance(c.Hex, home) <= 1)
                    c.Owner = 0;
            }
            var state = new GameState(world);
            for (int i = 0; i < state.Seen.Length; i++)
                state.Seen[i] = true;
            hero = state.Heroes[0];
            return state;
        }

        static void Group(GameState state, int cell, Monster monster, int count)
        {
            Cell c = state.World.Cells[cell];
            c.Monster = monster;
            c.Count = c.FirstCount = count;
        }

        [Test]
        public void TheHeroFightsAGroupHeClearlyBeatsAndPassesAStrongOne()
        {
            GameState state = Meadow(out Hero hero);
            World world = state.World;
            int troll = world.IndexOf(3, 4), goblins = world.IndexOf(2, 6);
            Group(state, troll, Monster.Troll, 30);
            Group(state, goblins, Monster.Goblins, 2);
            Rival.HeroDeeds deeds = Rival.HeroTurn(state, hero);
            Assert.AreEqual(1, deeds.Fights);
            Assert.AreEqual(1, deeds.Won);
            Assert.AreEqual(Monster.None, world.Cells[goblins].Monster);
            Assert.AreEqual(goblins, hero.Cell);
            Assert.AreEqual(30, world.Cells[troll].Count, "the trolls are left alone");
        }

        [Test]
        public void WithNothingToDoTheHeroGoesToTheEdgeOfTheKnownLand()
        {
            GameState state = Meadow(out Hero hero);
            World world = state.World;
            Hex home = world.Cells[hero.Cell].Hex;
            foreach (Cell c in world.Cells)
                state.Seen[c.Index] = Hex.Distance(c.Hex, home) <= 3;
            int seen = 0;
            foreach (bool s in state.Seen)
                seen += s ? 1 : 0;
            Rival.HeroDeeds deeds = Rival.HeroTurn(state, hero);
            Assert.Greater(deeds.Steps, 0);
            int now = 0;
            foreach (bool s in state.Seen)
                now += s ? 1 : 0;
            Assert.Greater(now, seen, "he opened the fog");
        }

        [Test]
        public void HiringAndArmingKeepTheReserve()
        {
            GameState state = Meadow(out Hero hero);
            state.Treasury[0] = new Stock(40, 20, 15, 2);
            Assert.AreEqual(0, Rival.Arm(state, 0), "6 wood leave less than the 10 kept");
            state.Treasury[0] = new Stock(40, 20, 16, 2);
            Assert.AreEqual(1, Rival.Arm(state, 0), "the barracks; the archery range waits");
            Assert.IsTrue(state.World.Cities[0].Buildings.Contains(TownBuilding.Barracks));
            Assert.AreEqual(2, Rival.Hire(state, 0), "4 gold each, 30 kept");
            Assert.AreEqual(new Stock(32, 18, 10, 0), state.Treasury[0]);
            Assert.AreEqual(HeroRules.StartSpearmen + 2, hero.Army[0].Count, "they join the hero in the town");
        }
    }
}

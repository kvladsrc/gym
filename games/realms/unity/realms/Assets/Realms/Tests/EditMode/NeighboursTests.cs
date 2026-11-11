using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>The neighbouring kingdoms' turns (the economy plan's step 2, 2026-11-11).</summary>
    public class NeighboursTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 42, 100 };

        static readonly Difficulty[] Paces = { Difficulty.Calm, Difficulty.Normal, Difficulty.Race };

        /// <summary>What a kingdom has built: field buildings on its land, its towns' buildings and quarters.</summary>
        static int Built(World world, int country) =>
            world.Cells.Count(c => c.Owner == country && c.Building != FieldBuilding.None) +
            world.Countries[country].Cities.Sum(i => world.Cities[i].Buildings.Count + world.Cities[i].Districts.Count);

        [TestCaseSource(nameof(Seeds))]
        public void NeighboursBuyAtTheirPaceWithWhatTheyHaveAndLeaveThePlayersAlone(int seed)
        {
            foreach (Difficulty pace in Paces)
            {
                World world = WorldGen.Generate(seed);
                var state = new GameState(world) { Difficulty = pace };
                int count = world.Countries.Count, player = world.Player;
                int[] built = Enumerable.Range(0, count).Select(k => Built(world, k)).ToArray();
                int bought = 0;
                for (int turn = 1; turn <= 40; turn++)
                {
                    int allowed = Rival.Purchases(pace, state.Turn);
                    state.EndTurn();
                    for (int k = 0; k < count; k++)
                    {
                        int now = Built(world, k);
                        if (k == player)
                            Assert.AreEqual(built[k], now, $"{pace}, turn {turn}: the idle players' kingdom got nothing built");
                        else
                            Assert.That(now - built[k], Is.InRange(0, allowed), $"{pace}, turn {turn}, kingdom {k}");
                        if (k != player)
                            bought += now - built[k];
                        built[k] = now;
                        Stock t = state.Treasury[k];
                        Assert.That(t.Gold >= 0 && t.Food >= 0 && t.Wood >= 0 && t.Stone >= 0, $"{pace}, turn {turn}, kingdom {k}: {t}");
                    }
                }
                Assert.That(bought, Is.GreaterThan(5 * (count - 1)), $"{pace}: the neighbours build");
            }
        }

        [Test]
        public void WithoutTheirTurnsTheNeighboursBuildNothing()
        {
            World world = WorldGen.Generate(7);
            var state = new GameState(world) { RivalsPlay = false };
            int[] built = Enumerable.Range(0, world.Countries.Count).Select(k => Built(world, k)).ToArray();
            for (int turn = 0; turn < 20; turn++)
                state.EndTurn();
            for (int k = 0; k < built.Length; k++)
                Assert.AreEqual(built[k], Built(world, k));
        }

        [Test]
        public void PurchasesSpreadThePaceOverTheTurns()
        {
            int[] Turns(Difficulty pace) => Enumerable.Range(1, 6).Select(t => Rival.Purchases(pace, t)).ToArray();
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 0, 0, 1 }, Turns(Difficulty.Calm));
            CollectionAssert.AreEqual(new[] { 0, 1, 1, 0, 1, 1 }, Turns(Difficulty.Normal));
            CollectionAssert.AreEqual(new[] { 1, 2, 1, 2, 1, 2 }, Turns(Difficulty.Race));
            Assert.AreEqual(8, Enumerable.Range(1, 12).Sum(t => Rival.Purchases(2f / 3f, t)), "two things in three turns");
        }

        [Test]
        public void EveryKingdomHasATemperAndTheMapsGiveThemAll()
        {
            var orders = new HashSet<string>();
            for (int seed = 1; seed <= 40; seed++)
            {
                for (int k = 0; k < 6; k++)
                {
                    Gem[] aims = Rival.AimsFor(seed, k);
                    CollectionAssert.AreEquivalent(Rival.Pursued, aims, "each gem it goes after once");
                    CollectionAssert.AreEqual(aims, Rival.AimsFor(seed, k), "the same map, the same neighbours");
                    orders.Add(string.Join(",", aims));
                }
            }
            Assert.AreEqual(24, orders.Count, "all 24 orders of four gems");
        }

        [Test]
        public void TheAimIsTheTempersFirstGemNotWon()
        {
            var state = new GameState(WorldGen.Generate(3));
            int k = (state.World.Player + 1) % state.World.Countries.Count;
            // A lair within the troops' reach, so the hunter's gem may be aimed at.
            Cell lair = state.World.Cells.First(c => c.IsLand && c.Owner < 0 && c.Monster == Monster.None
                && Troops.Distance(state.World, k, c.Index) <= 3);
            lair.Lair = true;
            lair.Monster = Monster.Boar;
            lair.Count = lair.FirstCount = 6;
            state.Aims[k] = new[] { Gem.Towns, Gem.Hunter, Gem.Capital, Gem.Lands };
            Gem[] aims = state.Aims[k];
            Assert.AreEqual(aims[0], state.Aim(k));
            state.Gems[k].Add(new GemWon(aims[0], 10));
            Assert.AreEqual(aims[1], state.Aim(k));
            state.Gems[k].Add(new GemWon(aims[2], 11));
            Assert.AreEqual(aims[1], state.Aim(k), "won out of order");
            state.Gems[k].Add(new GemWon(aims[1], 12));
            Assert.AreEqual(aims[3], state.Aim(k));
            state.Gems[k].Add(new GemWon(aims[3], 13));
            Assert.IsNull(state.Aim(k));
        }

        [Test]
        public void TheHuntersGemIsNotAimedAtWithNoLairWithinReach()
        {
            var state = new GameState(WorldGen.Generate(3));
            int k = (state.World.Player + 1) % state.World.Countries.Count;
            foreach (Cell c in state.World.Cells)
            {
                if (c.Lair && Troops.Distance(state.World, k, c.Index) <= Troops.Reach)
                {
                    c.Lair = false;
                    c.Monster = Monster.None;
                    c.Count = c.FirstCount = 0;
                }
            }
            state.Aims[k] = new[] { Gem.Hunter, Gem.Towns, Gem.Capital, Gem.Lands };
            Assert.AreEqual(Gem.Towns, state.Aim(k));
        }

        [Test]
        public void TheNeighboursNewQuartersAreToldForTheGround()
        {
            int quarters = 0;
            foreach (int seed in Seeds)
            {
                World world = WorldGen.Generate(seed);
                var state = new GameState(world) { Difficulty = Difficulty.Race };
                for (int turn = 0; turn < 60; turn++)
                {
                    TurnReport report = state.EndTurn();
                    foreach (int cell in report.Reshaped)
                    {
                        Cell c = world.Cells[cell];
                        Assert.That(c.District, Is.GreaterThanOrEqualTo(0), "a quarter");
                        Assert.AreNotEqual(world.Player, world.Cities[c.District].Country, "of a neighbour's town");
                        CollectionAssert.Contains(report.Changed, cell, "and dressed again");
                        quarters++;
                    }
                }
            }
            Assert.That(quarters, Is.GreaterThan(0), "the neighbours' towns grew onto quarters");
        }
    }
}

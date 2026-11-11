using System.Collections.Generic;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>The crown's gems and the end of the game (the economy and fun plans of 2026-11-11).</summary>
    public class CrownTests
    {
        /// <summary>
        /// A meadow with two kingdoms: the players' capital (5 houses) in the west, a neighbour's in
        /// the east, each holding the cells round it; the players have their first hero.
        /// </summary>
        static GameState Two(int player = 0)
        {
            var world = new World(14, 9, 5);
            foreach (Cell c in world.Cells)
                c.Biome = Biome.Meadow;
            AddKingdom(world, 2, 4);
            AddKingdom(world, 11, 4);
            world.Player = player;
            var state = new GameState(world);
            for (int i = 0; i < state.Seen.Length; i++)
                state.Seen[i] = true;
            return state;
        }

        static void AddKingdom(World world, int col, int row)
        {
            int k = world.Countries.Count;
            var country = new Country { Index = k, Name = "K" + k };
            world.Countries.Add(country);
            var capital = new City
            {
                Index = world.Cities.Count, Name = "C" + k, Country = k, Cell = world.IndexOf(col, row), Capital = true, Houses = 5,
            };
            world.Cities.Add(capital);
            world.Cells[capital.Cell].City = capital.Index;
            country.Cities.Add(capital.Index);
            country.Capital = capital.Index;
            Hex home = world.Cells[capital.Cell].Hex;
            foreach (Cell c in world.Cells)
            {
                if (Hex.Distance(c.Hex, home) <= 1)
                    c.Owner = k;
            }
        }

        static City Capital(GameState state, int country) => state.World.Cities[state.World.Countries[country].Capital];

        /// <summary>
        /// Three gems' feats: the capital at the royal size, three lairs, and a hero of the fifth
        /// level or, for a kingdom without a hero, twenty more cells of land.
        /// </summary>
        static void ThreeFeats(GameState state, int country)
        {
            Capital(state, country).Houses = 11;
            state.Lairs[country] = Crown.HunterGoal;
            List<Hero> heroes = state.HeroesOf(country);
            if (heroes.Count > 0)
                heroes[0].Level = Crown.HeroGoal;
            else
                Own(state, country, Crown.LandsGoal);
        }

        /// <summary>Gives the kingdom that many more of the free land cells.</summary>
        static void Own(GameState state, int country, int cells)
        {
            foreach (Cell c in state.World.Cells)
            {
                if (cells == 0)
                    break;
                if (c.Owner < 0 && c.IsLand)
                {
                    c.Owner = country;
                    cells--;
                }
            }
            Assert.AreEqual(0, cells, "room for the land");
        }

        [Test]
        public void ANewGameHasNoGemsAndNoWinner()
        {
            GameState state = Two();
            Assert.IsEmpty(state.CheckCrown());
            Assert.AreEqual(-1, state.Winner);
            Assert.AreEqual(0, state.CrownPlace(0));
            foreach (Gem gem in Crown.Order)
                Assert.IsFalse(Crown.Reached(state, 0, gem), gem.ToString());
            Assert.AreEqual((2, Crown.CapitalLevel), Crown.Progress(state, 0, Gem.Capital), "five houses are the second level");
            Assert.AreEqual((0, Crown.LandsGoal), Crown.Progress(state, 0, Gem.Lands), "land counts from the start");
        }

        [Test]
        public void AGemComesWithItsFeatAndStays()
        {
            GameState state = Two();
            City capital = Capital(state, 0);
            capital.Houses = 10;
            Assert.IsEmpty(state.CheckCrown(), "ten houses are the third level");
            capital.Houses = 11;
            CollectionAssert.AreEqual(new[] { (0, Gem.Capital) }, state.CheckCrown());
            Assert.IsTrue(state.HasGem(0, Gem.Capital));
            Assert.AreEqual(state.Turn, state.Gems[0][0].Turn);
            capital.Houses = 5;
            Assert.IsEmpty(state.CheckCrown(), "won once");
            Assert.IsTrue(state.HasGem(0, Gem.Capital), "and kept");
            Assert.IsFalse(state.HasGem(1, Gem.Capital), "the neighbour's crown is its own");
        }

        [Test]
        public void TheLandsGemCountsTheCellsWonSinceTheStart()
        {
            GameState state = Two();
            Own(state, 0, Crown.LandsGoal - 1);
            Assert.IsEmpty(state.CheckCrown());
            Own(state, 0, 1);
            CollectionAssert.AreEqual(new[] { (0, Gem.Lands) }, state.CheckCrown());
        }

        [Test]
        public void TheTownsGemWantsFourTownsOfTheThirdLevel()
        {
            GameState state = Two();
            World world = state.World;
            Capital(state, 0).Houses = 8;
            int[] at = { world.IndexOf(4, 2), world.IndexOf(4, 6), world.IndexOf(6, 4) };
            foreach (int cell in at)
            {
                var town = new City { Index = world.Cities.Count, Name = "T", Country = 0, Cell = cell, Houses = 8 };
                world.Cities.Add(town);
                world.Cells[cell].City = town.Index;
                world.Cells[cell].Owner = 0;
                world.Countries[0].Cities.Add(town.Index);
            }
            world.Cities[world.Cities.Count - 1].Houses = 7;
            Assert.AreEqual((3, Crown.TownsGoal), Crown.Progress(state, 0, Gem.Towns));
            Assert.IsFalse(state.HasGem(0, Gem.Towns));
            world.Cities[world.Cities.Count - 1].Houses = 8;
            CollectionAssert.Contains(state.CheckCrown(), (0, Gem.Towns));
        }

        [Test]
        public void AClearedLairCountsForItsKingdomsHunter()
        {
            GameState state = Two();
            Hero hero = state.HeroesOf(0)[0];
            int cell = state.World.IndexOf(3, 4);
            Cell c = state.World.Cells[cell];
            c.Monster = Monster.Goblins;
            c.Count = c.FirstCount = 3;
            c.Lair = true;
            Assert.AreEqual(StepKind.Fight, state.Step(hero, cell).Kind);
            Battle battle = state.StartBattle(hero, cell);
            battle.AutoToEnd(accept: false);
            Assert.AreEqual(Outcome.Won, battle.Outcome);
            state.EndBattle(hero, cell, battle);
            Assert.AreEqual(1, state.Lairs[0]);
            Assert.AreEqual(0, state.Lairs[1]);
            Assert.AreEqual((1, Crown.HunterGoal), Crown.Progress(state, 0, Gem.Hunter));
        }

        [Test]
        public void TheThirdGemSetsTheCrown()
        {
            GameState state = Two();
            Capital(state, 0).Houses = 11;
            state.Lairs[0] = Crown.HunterGoal;
            state.CheckCrown();
            Assert.AreEqual(0, state.Crowned[0], "two gems");
            Assert.AreEqual(-1, state.Winner);
            state.Turn = 7;
            state.HeroesOf(0)[0].Level = Crown.HeroGoal;
            CollectionAssert.AreEqual(new[] { (0, Gem.Hero) }, state.CheckCrown());
            Assert.AreEqual(7, state.Crowned[0]);
            Assert.AreEqual(0, state.Winner);
            Assert.AreEqual(1, state.CrownPlace(0));
            Assert.AreEqual(0, state.CrownPlace(1));
        }

        [Test]
        public void ANeighboursEarlierCrownComesFirst()
        {
            GameState state = Two();
            ThreeFeats(state, 1);
            state.Turn = 20;
            state.CheckCrown();
            Assert.AreEqual(20, state.Crowned[1]);
            Assert.AreEqual(1, state.Winner);
            state.Turn = 25;
            ThreeFeats(state, 0);
            state.CheckCrown();
            Assert.AreEqual(1, state.Winner, "the first crown wins");
            Assert.AreEqual(2, state.CrownPlace(0));
            Assert.AreEqual(1, state.CrownPlace(1));
        }

        [Test]
        public void CrownsSetOnTheSameTurnShareThePlaceAndThePlayersWin(
            [Values(0, 1)] int player)
        {
            GameState state = Two(player);
            ThreeFeats(state, 0);
            ThreeFeats(state, 1);
            List<(int Country, Gem Gem)> won = state.CheckCrown();
            Assert.AreEqual(player, won[0].Country, "the players' gems come first");
            Assert.AreEqual(player, state.Winner);
            Assert.AreEqual(1, state.CrownPlace(0));
            Assert.AreEqual(1, state.CrownPlace(1));
        }

        [Test]
        public void TheTurnReportTellsTheGemsTheTurnWon()
        {
            GameState state = Two();
            Capital(state, 1).Houses = 11;
            TurnReport report = state.EndTurn();
            var gems = report.Events.FindAll(e => e.Kind == TurnEventKind.GemSet);
            Assert.AreEqual(1, gems.Count);
            Assert.AreEqual(1, gems[0].Country);
            Assert.AreEqual(Gem.Capital, gems[0].Gem);
            Assert.AreEqual(state.Turn, state.Gems[1][0].Turn, "on the new turn");
        }
    }
}

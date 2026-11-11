using System.Linq;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>The lairs' raids: the fun plan's Ф3 (2026-11-11).</summary>
    public class RaidTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 42, 100 };

        /// <summary>
        /// A plain with the kingdom's capital in the west, a farm beside it on the lair's side and a
        /// goblins' lair of nine eight steps east.
        /// </summary>
        static GameState Lair(bool walls, out City town, out int field, out int lair, int lairCol = 12)
        {
            World world = InfluenceTests.Plain(21, 11, 1);
            town = InfluenceTests.Town(world, 0, 4, 5, 5, true);
            if (walls)
                town.Buildings.Add(TownBuilding.Walls);
            lair = world.IndexOf(lairCol, 5);
            Cell den = world.Cells[lair];
            den.Monster = Monster.Goblins;
            den.Count = den.FirstCount = 9;
            den.Lair = true;
            Hex at = den.Hex;
            int home = town.Cell;
            field = Enumerable.Range(0, 6).Select(d => world.Neighbor(home, d))
                .OrderBy(c => Hex.Distance(world.Cells[c].Hex, at)).First();
            world.Cells[field].Resource = Resource.Wheat;
            world.Cells[field].Building = FieldBuilding.Farm;
            return new GameState(world);
        }

        [Test]
        public void ABandWalksToTheNearestTownStandsOnItsFieldAndStopsItsGrowthForThreeTurns()
        {
            GameState state = Lair(false, out City town, out int field, out int lair);
            World world = state.World;
            var report = new TurnReport();
            Raid raid = Raids.Send(state, lair, report);
            Assert.IsNotNull(raid);
            Assert.AreEqual(1, Hex.Distance(world.Cells[raid.Cell].Hex, world.Cells[lair].Hex), "out of the lair's door");
            Cell band = world.Cells[raid.Cell];
            Assert.IsTrue(band.Band);
            Assert.AreEqual(Monster.Goblins, band.Monster);
            Assert.AreEqual(3, band.Count, "a third of the lair");
            Assert.AreEqual(9, world.Cells[lair].Count, "the lair keeps its own");
            Assert.AreEqual(town.Index, raid.Town);
            Assert.AreEqual(6, raid.Steps, "seven steps to the farm, one already walked");
            Assert.AreEqual(2, Raids.TurnsLeft(raid));
            TurnEvent sent = report.Events.Single(e => e.Kind == TurnEventKind.RaidSent);
            Assert.AreEqual(town.Index, sent.City);
            Assert.AreEqual(Monster.Goblins, sent.Monster);

            int fieldFood = Economy.Yield(world.Cells[field]).Food;
            Assert.IsFalse(state.EndTurn().Events.Any(e => e.Kind == TurnEventKind.RaidArrived));
            Assert.AreEqual(3, raid.Steps);
            report = state.EndTurn();
            Assert.That(report.Events.Any(e => e.Kind == TurnEventKind.RaidArrived), "two turns as said");
            Assert.AreEqual(field, raid.Cell, "it stands on the farm");
            Assert.AreEqual(Raids.StayTurns, raid.Stay);
            Assert.IsTrue(world.Cells[field].Band);
            Assert.That(fieldFood, Is.GreaterThan(0));
            Assert.AreEqual(FoodWithout(world, field) - fieldFood, Economy.Income(world, 0).Food,
                "the farm gives nothing while the band stands on it");

            int houses = town.Houses, growth = town.Growth;
            state.Treasury[0] += new Stock(0, 500, 0, 0);
            for (int turn = 0; turn < Raids.StayTurns; turn++)
            {
                report = state.EndTurn();
                Assert.AreEqual(GrowthStop.Raid, town.Stop, $"turn {turn} of the stay");
            }
            Assert.AreEqual(houses, town.Houses);
            Assert.AreEqual(growth, town.Growth);
            Assert.That(report.Events.Any(e => e.Kind == TurnEventKind.RaidLeft), "then it goes back");
            Assert.IsEmpty(state.Bands);
            Assert.AreEqual(Monster.None, world.Cells[field].Monster);
            Assert.IsFalse(world.Cells[field].Band);
            Assert.AreEqual(FieldBuilding.Farm, world.Cells[field].Building, "nothing is destroyed");
            Assert.AreEqual(Raids.StayTurns, state.RaidTurns[0]);
            Assert.AreEqual(1, state.RaidsSent[0]);
            state.EndTurn();
            Assert.AreNotEqual(GrowthStop.Raid, town.Stop, "and the town grows again");
        }

        /// <summary>The kingdom's food a turn as if no band stood on the cell.</summary>
        static int FoodWithout(World world, int cell)
        {
            Cell c = world.Cells[cell];
            Monster monster = c.Monster;
            c.Monster = Monster.None;
            int food = Economy.Income(world, 0).Food;
            c.Monster = monster;
            return food;
        }

        [Test]
        public void WallsBeatABandOffAndTakeItsLoot()
        {
            GameState state = Lair(true, out City town, out _, out int lair);
            Raid raid = Raids.Send(state, lair, new TurnReport());
            state.EndTurn();
            TurnReport report = state.EndTurn();
            TurnEvent repelled = report.Events.Single(e => e.Kind == TurnEventKind.RaidRepelled);
            Assert.AreEqual(town.Index, repelled.City);
            Assert.AreEqual(GameState.Reward(Monster.Goblins, 3, false).Gold, repelled.Amount);
            Assert.That(repelled.Amount, Is.GreaterThan(0));
            Assert.IsEmpty(state.Bands);
            Assert.AreEqual(Monster.None, state.World.Cells[raid.Cell].Monster);
            Assert.AreEqual(1, state.RaidsRepelled[0]);
            Assert.AreEqual(0, state.RaidTurns[0]);
            Assert.AreNotEqual(GrowthStop.Raid, town.Stop);
        }

        [Test]
        public void AHeroBeatsABandOnItsWay()
        {
            GameState state = Lair(false, out _, out _, out int lair);
            Raid raid = Raids.Send(state, lair, new TurnReport());
            Hero hero = state.Heroes[0];
            Battle battle = state.StartBattle(hero, raid.Cell);
            battle.AutoToEnd(accept: false);
            Assert.AreEqual(Outcome.Won, battle.Outcome);
            int cell = raid.Cell;
            state.EndBattle(hero, cell, battle);
            Assert.IsFalse(state.World.Cells[cell].Band);
            state.EndTurn();
            Assert.IsEmpty(state.Bands);
            Assert.AreEqual(1, state.RaidsBeaten[0]);
            Assert.AreEqual(0, state.RaidTurns[0]);
        }

        [Test]
        public void ALairRaidsOnItsTurnsUntilItIsCleared()
        {
            GameState state = Lair(true, out _, out _, out int lair);
            for (int turn = 0; turn < 40; turn++)
                state.EndTurn();
            Assert.That(state.RaidsSent[0], Is.InRange(2, 3), "one in twelve turns from the 12th-15th");
            Assert.IsTrue(state.Turn >= Raids.FirstTurn);
            Cell den = state.World.Cells[lair];
            den.Monster = Monster.None;
            den.Count = den.FirstCount = 0;
            den.Lair = false;
            for (int turn = 0; turn < 4; turn++)
                state.EndTurn();
            int sent = state.RaidsSent[0];
            for (int turn = 0; turn < 20; turn++)
                state.EndTurn();
            Assert.AreEqual(sent, state.RaidsSent[0], "a cleared lair sends no more");
            Assert.IsEmpty(state.Bands);
        }

        [Test]
        public void AKingdomMeetsOneBandAtATimeFromTheLairThatRestedLongest()
        {
            GameState state = Lair(false, out City town, out _, out int lair);
            World world = state.World;
            // A second lair of wolves south-east, as near to the capital as the first.
            int second = world.IndexOf(11, 8);
            Cell den = world.Cells[second];
            den.Monster = Monster.Wolves;
            den.Count = den.FirstCount = 9;
            den.Lair = true;
            int first = Raids.FirstFor(world, 0);
            while (state.Turn < first - 1)
                state.EndTurn();
            Assert.IsEmpty(state.Bands, "no band before the kingdom's first raid turn");
            state.EndTurn();
            Assert.AreEqual(1, state.Bands.Count, "one band, not one a lair");
            int sentFrom = state.Bands[0].Lair;
            for (int turn = 1; turn < Raids.Every; turn++)
            {
                state.EndTurn();
                Assert.That(state.Bands.Count, Is.LessThanOrEqualTo(1));
                Assert.AreEqual(1, state.RaidsSent[0], $"turn {turn} after the first band");
            }
            state.EndTurn();
            Assert.AreEqual(2, state.RaidsSent[0], "the next band twelve turns later");
            Assert.AreEqual(1, state.Bands.Count);
            Assert.AreNotEqual(sentFrom, state.Bands[0].Lair, "from the lair that rested");
            Assert.That(new[] { lair, second }, Does.Contain(state.Bands[0].Lair));
            Assert.AreEqual(town.Index, state.Bands[0].Town);
        }

        [Test]
        public void TheFirstRaidComesOnTheTwelfthToFifteenthTurn()
        {
            foreach (int seed in Seeds)
            {
                World world = WorldGen.Generate(seed);
                for (int k = 0; k < world.Countries.Count; k++)
                    Assert.That(Raids.FirstFor(world, k), Is.InRange(12, 15));
            }
        }

        [Test]
        public void NoBandForATownOutOfReach()
        {
            GameState state = Lair(false, out _, out _, out int lair, lairCol: 16);
            Assert.IsNull(Raids.Send(state, lair, new TurnReport()));
            Assert.IsEmpty(state.Bands);
        }

        [Test]
        public void BandsDoNotGrow()
        {
            GameState state = Lair(false, out _, out _, out int lair);
            state.World.Cells[lair].Count = 30;
            Raid raid = Raids.Send(state, lair, new TurnReport());
            int count = state.World.Cells[raid.Cell].Count;
            state.Turn = Wilds.GrowthTurns;
            state.EndTurn();
            Assert.AreEqual(count, state.World.Cells[raid.Cell].Count);
        }

        [TestCaseSource(nameof(Seeds))]
        public void OnTheMapsBandsWalkOnFreeLandAndEveryBandIsListed(int seed)
        {
            World world = WorldGen.Generate(seed);
            var state = new GameState(world);
            for (int turn = 0; turn < 60; turn++)
            {
                state.EndTurn();
                var listed = state.Bands.Select(r => r.Cell).ToHashSet();
                Assert.AreEqual(state.Bands.Count, listed.Count, "one band a cell");
                foreach (Cell c in world.Cells)
                {
                    Assert.AreEqual(listed.Contains(c.Index), c.Band, $"turn {state.Turn}, cell {c.Index}");
                    if (!c.Band)
                        continue;
                    Assert.IsTrue(c.IsLand && c.Town < 0 && c.Place == Place.None && c.Monster != Monster.None && !c.Lair);
                }
            }
            Assert.That(state.RaidsSent.Sum(), Is.GreaterThan(0), "the lairs raid");
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>The kingdom's stake (вешка): the territory plan's П3, without buying (2026-11-11).</summary>
    public class StakeTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 42, 100 };

        /// <summary>
        /// A plain with a capital of five houses in the west and a small town in the east; the stake
        /// cell lies four steps east of the capital, six from the town.
        /// </summary>
        static World TwoTowns(out City capital, out City town, out int stake)
        {
            World world = InfluenceTests.Plain(21, 11, 1);
            capital = InfluenceTests.Town(world, 0, 4, 5, 5, true);
            town = InfluenceTests.Town(world, 0, 14, 5, 3, false);
            stake = world.IndexOf(8, 5);
            return world;
        }

        /// <summary>Turns of growth until the cell is the kingdom's; the Border events of the town go into <paramref name="taken"/>.</summary>
        static int TurnsUntilTaken(GameState state, int cell, int town, List<int> taken, List<TurnEvent> events)
        {
            for (int turn = 1; turn <= 80; turn++)
            {
                var report = new TurnReport();
                Influence.Grow(state, report);
                taken?.AddRange(report.Events.Where(e => e.Kind == TurnEventKind.Border && e.City == town).Select(e => e.Cell));
                events?.AddRange(report.Events);
                if (state.World.Cells[cell].Owner >= 0)
                    return turn;
            }
            return -1;
        }

        [Test]
        public void TheNearestTownWalksTheWayToTheStakeSooner()
        {
            World without = TwoTowns(out _, out _, out int cell);
            int plain = TurnsUntilTaken(new GameState(without), cell, -1, null, null);

            World world = TwoTowns(out City capital, out City town, out cell);
            var state = new GameState(world);
            var way = new List<int>();
            Assert.AreEqual(capital.Index, Influence.StakeWay(world, 0, cell, way), "the capital is nearer");
            Assert.AreEqual(3, way.Count, "the wild cells two, three and four steps out");
            Assert.AreEqual(cell, way[way.Count - 1]);
            Assert.AreEqual(6, Influence.StakeTurns(world, 0, cell, out int leader), "three cells at three influence a turn");
            Assert.AreEqual(capital.Index, leader);

            Assert.IsTrue(state.SetStake(0, cell));
            var taken = new List<int>();
            var events = new List<TurnEvent>();
            int staked = TurnsUntilTaken(state, cell, capital.Index, taken, events);
            Assert.AreEqual(6, staked, "as the card said");
            Assert.That(plain < 0 || plain > 2 * staked, $"without the stake the capital fills its rings first: {plain} turns, -1 for more than 80");
            CollectionAssert.AreEqual(way, taken, "the capital took the way's cells and nothing else");
            Assert.AreEqual(-1, world.Countries[0].Stake, "the stake goes when the cell is taken");
            TurnEvent reached = events.Single(e => e.Kind == TurnEventKind.StakeReached);
            Assert.AreEqual(capital.Index, reached.City);
            Assert.AreEqual(cell, reached.Cell);
            Assert.AreEqual(1, events.Count(e => e.Kind == TurnEventKind.Border && e.City == town.Index), "the small town took its own cell, as without the stake");
        }

        [Test]
        public void AStakeGoesWhenAnotherKingdomTakesTheCellFirst()
        {
            World world = InfluenceTests.Plain(21, 11, 2);
            InfluenceTests.Town(world, 0, 4, 5, 5, true);
            InfluenceTests.Town(world, 1, 14, 5, 5, true);
            int cell = world.IndexOf(9, 5), theirs = world.IndexOf(10, 3);
            var state = new GameState(world);
            Assert.IsTrue(state.SetStake(0, cell));
            Assert.IsTrue(state.SetStake(1, theirs));

            var report = new TurnReport();
            Influence.Take(state, 1, cell, report);
            Assert.AreEqual(-1, world.Countries[0].Stake);
            TurnEvent lost = report.Events.Single(e => e.Kind == TurnEventKind.StakeLost);
            Assert.AreEqual(1, lost.Country, "«синие успели раньше»");
            Assert.AreEqual(-1, lost.City);

            report = new TurnReport();
            Influence.Take(state, 0, theirs, report);
            Assert.AreEqual(-1, world.Countries[1].Stake, "the neighbour's stake goes too");
            Assert.IsEmpty(report.Events, "and the players are not told of it");
        }

        [Test]
        public void NoStakeOnTakenLandTheSeaMonstersBesideALairNearAnotherHomeOrOutOfReach()
        {
            World world = InfluenceTests.Plain(25, 15, 2);
            City capital = InfluenceTests.Town(world, 0, 7, 7, 5, true);
            InfluenceTests.Town(world, 1, 13, 7, 5, true);
            int sea = world.IndexOf(5, 7), group = world.IndexOf(7, 4), beside = world.IndexOf(7, 10), lair = world.IndexOf(7, 11);
            world.Cells[sea].Biome = Biome.Ocean;
            world.Cells[group].Monster = Monster.Wolves;
            world.Cells[group].Count = 4;
            world.Cells[lair].Monster = Monster.Goblins;
            world.Cells[lair].Count = 6;
            world.Cells[lair].Lair = true;
            int near = world.IndexOf(11, 7), far = world.IndexOf(0, 0), own = world.IndexOf(8, 7), fine = world.IndexOf(4, 9);
            Assert.That(Hex.Distance(world.Cells[far].Hex, world.Cells[capital.Cell].Hex), Is.GreaterThan(Influence.Reach(capital)));
            var state = new GameState(world);

            Assert.IsTrue(state.SetStake(0, fine), "open plains in reach");
            foreach ((int cell, string what) in new[] { (sea, "the sea"), (group, "monsters"), (beside, "beside a lair"), (lair, "a lair"),
                (near, "the other kingdom's home"), (far, "out of reach"), (own, "the kingdom's own land") })
            {
                Assert.IsFalse(Influence.CanStake(world, 0, cell), what);
                Assert.IsFalse(state.SetStake(0, cell), what);
                Assert.AreEqual(fine, world.Countries[0].Stake, $"{what}: the stake stays where it was");
            }
            Assert.IsTrue(state.SetStake(0, -1));
            Assert.AreEqual(-1, world.Countries[0].Stake, "taken away");
            Assert.AreEqual(1, state.StakesSet);
        }

        [Test]
        public void TheWayGoesAroundMonsters()
        {
            World world = InfluenceTests.Plain(21, 11, 1);
            City capital = InfluenceTests.Town(world, 0, 4, 5, 5, true);
            int cell = world.IndexOf(8, 5), group = world.IndexOf(6, 5);
            world.Cells[group].Monster = Monster.Wolves;
            world.Cells[group].Count = 4;
            var way = new List<int>();
            Assert.AreEqual(capital.Index, Influence.StakeWay(world, 0, cell, way));
            CollectionAssert.DoesNotContain(way, group);
            Assert.IsTrue(way.All(c => Influence.Open(world, c)));
        }

        [Test]
        public void TheStakeTaskAsksForOneStakeAndPaysWood()
        {
            World world = TwoTowns(out _, out _, out int cell);
            var state = new GameState(world);
            state.Done.AddRange(new[] { Quest.Farm, Quest.LumberCamp, Quest.Fight });
            CollectionAssert.Contains(state.Offered(), Quest.Stake, "after the first tasks");
            Stock before = state.Treasury[0];
            Assert.IsEmpty(state.CheckQuests().Where(q => q == Quest.Stake));
            state.SetStake(0, cell);
            CollectionAssert.Contains(state.CheckQuests(), Quest.Stake);
            Assert.AreEqual(before.Wood + 5, state.Treasury[0].Wood);
            state.SetStake(0, -1);
            Assert.IsEmpty(state.CheckQuests(), "paid once");
        }

        [Test]
        public void TheBotStakesTheNearestResourceAndNotWhenTurnedOff()
        {
            World world = InfluenceTests.Plain(21, 11, 1);
            InfluenceTests.Town(world, 0, 4, 5, 5, true);
            int near = world.IndexOf(7, 5), far = world.IndexOf(4, 10);
            world.Cells[near].Resource = Resource.Stone;
            world.Cells[far].Resource = Resource.Wheat;
            var state = new GameState(world) { BotsStake = false };
            Influence.BotStake(state, 0);
            Assert.AreEqual(-1, world.Countries[0].Stake);
            state.BotsStake = true;
            Influence.BotStake(state, 0);
            Assert.AreEqual(near, world.Countries[0].Stake);
            world.Cells[near].Owner = 0;
            world.Countries[0].Stake = -1;
            Influence.BotStake(state, 0);
            Assert.AreEqual(far, world.Countries[0].Stake, "the next one when the first is taken");
        }

        [TestCaseSource(nameof(Seeds))]
        public void OnTheMapsEveryStakeStandsOnWildLandBetweenTurns(int seed)
        {
            World world = WorldGen.Generate(seed);
            var state = new GameState(world) { Difficulty = Difficulty.Race };
            int set = 0;
            for (int turn = 0; turn < 40; turn++)
            {
                state.EndTurn();
                foreach (Country k in world.Countries)
                {
                    if (k.Stake < 0)
                        continue;
                    set++;
                    Assert.Less(world.Cells[k.Stake].Owner, 0, $"turn {turn}, {k.Name}");
                    Assert.AreNotEqual(Resource.None, world.Cells[k.Stake].Resource, "the bots stake resources");
                }
                Assert.AreEqual(-1, world.Countries[world.Player].Stake, "the players set their own");
            }
            Assert.That(set, Is.GreaterThan(0), "the neighbours use stakes");
        }
    }
}

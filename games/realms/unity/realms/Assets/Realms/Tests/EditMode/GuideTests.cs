using System.Linq;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>The conditions of the guiding lines (the interface plan's 3.3, 2026-11-11).</summary>
    public class GuideTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 42, 100 };

        [Test]
        public void TheBuildRingsStandOnTheKingdomsFreeFittingCellsNearestTheCapital()
        {
            foreach (int seed in Seeds)
            {
                var state = new GameState(WorldGen.Generate(seed));
                World world = state.World;
                int player = world.Player;
                foreach (FieldBuilding building in new[] { FieldBuilding.Farm, FieldBuilding.LumberCamp })
                {
                    var all = Guide.BuildSpots(state, player, building, int.MaxValue);
                    var spots = Guide.BuildSpots(state, player, building);
                    Assert.LessOrEqual(spots.Count, 3);
                    CollectionAssert.AreEqual(all.Take(3), spots, $"seed {seed}: the first three");
                    foreach (int cell in all)
                    {
                        Cell c = world.Cells[cell];
                        Assert.AreEqual(player, c.Owner, $"seed {seed}");
                        Assert.AreEqual(building, Economy.BuildingFor(c.Resource), $"seed {seed}");
                        Assert.AreEqual(FieldBuilding.None, c.Building, $"seed {seed}");
                    }
                    int expected = world.Cells.Count(c => c.Owner == player && c.City < 0 && c.Building == FieldBuilding.None
                        && c.Monster == Monster.None && c.Resource != Resource.None && Economy.BuildingFor(c.Resource) == building);
                    Assert.AreEqual(expected, all.Count, $"seed {seed}: every fitting cell");
                    if (all.Count == 0)
                        continue;
                    state.Treasury[player] += Economy.Cost(building);
                    Assert.AreEqual(Refusal.None, state.Build(player, all[0]), $"seed {seed}");
                    CollectionAssert.DoesNotContain(Guide.BuildSpots(state, player, building, int.MaxValue), all[0], $"seed {seed}: built");
                }
            }
        }

        [Test]
        public void TheEasyGroupIsOneTheHeroSurelyBeatsAndReaches()
        {
            int found = 0;
            foreach (int seed in Seeds)
            {
                var state = new GameState(WorldGen.Generate(seed));
                foreach (Hero hero in state.HeroesOf(state.World.Player))
                {
                    int group = Guide.EasyGroup(state, hero);
                    if (group < 0)
                        continue;
                    found++;
                    Assert.IsTrue(state.Seen[group], $"seed {seed}: seen");
                    Assert.AreNotEqual(Monster.None, state.World.Cells[group].Monster, $"seed {seed}");
                    Assert.GreaterOrEqual(state.OddsOf(hero, group, Rival.OddsRuns).Win, Rival.SureWin, $"seed {seed}");
                    Assert.IsNotNull(state.Path(hero, group), $"seed {seed}: a way");
                }
            }
            Assert.Greater(found, 0, "some start has a group the hero beats");

            var lone = new GameState(WorldGen.Generate(1));
            Hero bare = lone.HeroesOf(lone.World.Player)[0];
            bare.Army.Clear();
            Assert.AreEqual(-1, Guide.EasyGroup(lone, bare), "no army, no easy group");
        }

        [Test]
        public void TheEndTurnCallsWhenNoTaskOnOfferCanMove()
        {
            foreach (int seed in Seeds)
            {
                var state = new GameState(WorldGen.Generate(seed));
                int player = state.World.Player;
                CollectionAssert.AreEqual(new[] { Quest.Farm, Quest.LumberCamp, Quest.Fight }, state.Offered(), $"seed {seed}");
                Assert.IsFalse(Guide.NothingToDo(state), $"seed {seed}: a new game has things to do");

                foreach (Hero hero in state.HeroesOf(player))
                    hero.Moves = 0;
                state.Treasury[player] = new Stock(0, 0, 0, 0);
                Assert.IsFalse(Guide.CanAdvance(state, Quest.Fight), $"seed {seed}: no moves");
                Assert.IsFalse(Guide.CanAdvance(state, Quest.Farm), $"seed {seed}: no gold");
                Assert.IsTrue(Guide.NothingToDo(state), $"seed {seed}: nothing left");

                if (Guide.BuildSpots(state, player, FieldBuilding.Farm).Count > 0)
                {
                    state.Treasury[player] = Economy.Cost(FieldBuilding.Farm);
                    Assert.IsTrue(Guide.CanAdvance(state, Quest.Farm), $"seed {seed}: the farm paid");
                    Assert.IsFalse(Guide.NothingToDo(state), $"seed {seed}");
                }
            }
        }

        [Test]
        public void TheStakeRingIsOnASeenCellTheKingdomMayStake()
        {
            int found = 0;
            foreach (int seed in Seeds)
            {
                var state = new GameState(WorldGen.Generate(seed));
                World world = state.World;
                int spot = Guide.StakeSpot(state, world.Player);
                if (spot < 0)
                    continue;
                found++;
                Assert.IsTrue(state.Seen[spot], $"seed {seed}");
                Assert.IsTrue(Influence.CanStake(world, world.Player, spot), $"seed {seed}");
                Assert.AreNotEqual(Resource.None, world.Cells[spot].Resource, $"seed {seed}: a resource");
                Assert.IsTrue(state.SetStake(world.Player, spot), $"seed {seed}");
            }
            Assert.Greater(found, 0, "some start has a stake to set");
        }

        [Test]
        public void TheBarracksGoInTheCapitalFirst()
        {
            foreach (int seed in Seeds)
            {
                var state = new GameState(WorldGen.Generate(seed));
                World world = state.World;
                int town = Guide.TownToBuild(state, world.Player, TownBuilding.Barracks);
                Assert.GreaterOrEqual(town, 0, $"seed {seed}");
                Assert.IsTrue(world.Cities[town].Capital, $"seed {seed}");
                world.Cities[town].Buildings.Add(TownBuilding.Barracks);
                Assert.AreNotEqual(town, Guide.TownToBuild(state, world.Player, TownBuilding.Barracks), $"seed {seed}: built");
            }
        }
    }
}

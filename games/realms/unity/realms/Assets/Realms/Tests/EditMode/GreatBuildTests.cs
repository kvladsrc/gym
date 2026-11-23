using System;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>The Great Build (the economy plan's 3.4, 2026-11-11).</summary>
    public class GreatBuildTests
    {
        /// <summary>A kingdom of a capital and a village on plains, its capital a city with walls and quarters, and plenty in the treasury.</summary>
        static GameState Kingdom(out City capital, out City village)
        {
            var world = new World(9, 9, 0);
            foreach (Cell c in world.Cells)
                c.Biome = Biome.Plains;
            var country = new Country { Index = 0, Name = "A" };
            world.Countries.Add(country);
            capital = Town(world, 4, 4, true, 8);
            // Two quarters: seventeen places for its eight houses.
            for (int d = 0; d < 2; d++)
            {
                int quarter = world.Neighbor(capital.Cell, d);
                world.Cells[quarter].District = capital.Index;
                capital.Districts.Add(quarter);
            }
            village = Town(world, 7, 7, false, 3);
            country.Capital = capital.Index;
            foreach (Cell c in world.Cells)
                c.Owner = 0;
            var state = new GameState(world);
            state.Treasury[0] = new Stock(500, 0, 500, 500);
            return state;
        }

        static City Town(World world, int x, int y, bool capital, int houses)
        {
            int cell = world.IndexOf(x, y);
            var city = new City { Index = world.Cities.Count, Name = "T" + world.Cities.Count, Country = 0, Cell = cell, Capital = capital, Houses = houses };
            world.Cities.Add(city);
            world.Countries[0].Cities.Add(city.Index);
            world.Cells[cell].City = city.Index;
            return city;
        }

        [Test]
        public void OnlyACapitalWithACastleRaisesIt()
        {
            GameState state = Kingdom(out City capital, out City village);
            Assert.AreEqual(Refusal.NotTheCapital, state.RaiseBuild(0, village.Index));
            Assert.AreEqual(Refusal.NoCastle, state.RaiseBuild(0, capital.Index));
            capital.Buildings.Add(TownBuilding.Walls);
            Assert.AreEqual(Refusal.NoCastle, state.RaiseBuild(0, capital.Index), "walls are not a castle");
            capital.Buildings.Add(TownBuilding.Castle);
            Assert.AreEqual(Refusal.None, state.RaiseBuild(0, capital.Index));
            Assert.AreEqual(1, capital.BuildStage);
        }

        [Test]
        public void TheStagesComeInOrderEachPaidForAndThenItStands()
        {
            GameState state = Kingdom(out City capital, out _);
            capital.Buildings.Add(TownBuilding.Walls);
            capital.Buildings.Add(TownBuilding.Castle);
            for (int stage = 1; stage <= GreatBuild.Stages; stage++)
            {
                Assert.AreEqual(stage, GreatBuild.Next(capital));
                Stock before = state.Treasury[0];
                Assert.AreEqual(Refusal.None, state.RaiseBuild(0, capital.Index), $"stage {stage}");
                Assert.AreEqual(stage, capital.BuildStage);
                Assert.AreEqual(before - GreatBuild.Cost(stage), state.Treasury[0], $"stage {stage}'s price");
            }
            Assert.AreEqual(0, GreatBuild.Next(capital));
            Stock left = state.Treasury[0];
            Assert.AreEqual(Refusal.AlreadyBuilt, state.RaiseBuild(0, capital.Index));
            Assert.AreEqual(left, state.Treasury[0]);
        }

        [Test]
        public void AStageItCannotPayForTakesNothing()
        {
            GameState state = Kingdom(out City capital, out _);
            capital.Buildings.Add(TownBuilding.Walls);
            capital.Buildings.Add(TownBuilding.Castle);
            Stock price = GreatBuild.Cost(1);
            state.Treasury[0] = price - new Stock(0, 0, 0, 1);
            Assert.AreEqual(Refusal.NotEnough, state.RaiseBuild(0, capital.Index));
            Assert.AreEqual(0, capital.BuildStage);
            Assert.AreEqual(price - new Stock(0, 0, 0, 1), state.Treasury[0]);
            Assert.AreEqual(Refusal.NotYours, state.RaiseBuild(1, capital.Index), "another kingdom's capital");
        }

        [Test]
        public void TheFirstTwoStagesBringGoldAndTheThirdNone()
        {
            GameState state = Kingdom(out City capital, out _);
            capital.Buildings.Add(TownBuilding.Walls);
            capital.Buildings.Add(TownBuilding.Castle);
            int[] gold = new int[GreatBuild.Stages + 1];
            Stock start = Economy.Income(state.World, 0);
            gold[0] = start.Gold;
            for (int stage = 1; stage <= GreatBuild.Stages; stage++)
            {
                state.RaiseBuild(0, capital.Index);
                gold[stage] = Economy.Income(state.World, 0).Gold;
            }
            Assert.AreEqual(gold[0] + GreatBuild.GoldPerStage, gold[1]);
            Assert.AreEqual(gold[1] + GreatBuild.GoldPerStage, gold[2]);
            Assert.AreEqual(gold[2], gold[3]);
            Stock end = Economy.Income(state.World, 0);
            Assert.AreEqual(start.Food, end.Food, "the build brings only gold");
            Assert.AreEqual(start.Wood, end.Wood);
            Assert.AreEqual(start.Stone, end.Stone);
        }

        [Test]
        public void ASaveKeepsTheStageAndThePlace()
        {
            var state = new GameState(WorldGen.Generate(5));
            World world = state.World;
            City capital = world.Cities[world.Countries[world.Player].Capital];
            capital.BuildStage = 2;
            capital.BuildPlace = 1;
            GameState loaded = Save.FromBytes(Save.ToBytes(new SaveGame { State = state })).State;
            Assert.AreEqual(2, loaded.World.Cities[capital.Index].BuildStage);
            Assert.AreEqual(1, loaded.World.Cities[capital.Index].BuildPlace);
        }

        [Test]
        public void TheFirstStageTakesAFreePlaceAndKeepsIt()
        {
            GameState state = Kingdom(out City capital, out _);
            capital.Buildings.Add(TownBuilding.Walls);
            capital.Buildings.Add(TownBuilding.Castle);
            int free = Towns.Free(capital);
            int first = Array.IndexOf(Towns.Layout(capital), Towns.FreeLot);
            Assert.AreEqual(Refusal.None, state.RaiseBuild(0, capital.Index));
            Assert.AreEqual(first, capital.BuildPlace);
            Assert.AreEqual(free - 1, Towns.Free(capital));
            Assert.AreEqual(Towns.BuildLot, Towns.Layout(capital)[first]);
            // A new house goes round it, and the next stages take no other place.
            capital.Houses++;
            Assert.AreEqual(Towns.BuildLot, Towns.Layout(capital)[first]);
            Assert.AreEqual(Refusal.None, state.RaiseBuild(0, capital.Index));
            Assert.AreEqual(Refusal.None, state.RaiseBuild(0, capital.Index));
            Assert.AreEqual(free - 2, Towns.Free(capital));
            Assert.AreEqual(first, capital.BuildPlace);
        }

        [Test]
        public void AFullTownHasNoPlaceForTheFirstStage()
        {
            GameState state = Kingdom(out City capital, out _);
            capital.Buildings.Add(TownBuilding.Walls);
            capital.Buildings.Add(TownBuilding.Castle);
            capital.Houses = Towns.Places(capital);
            Stock before = state.Treasury[0];
            Assert.AreEqual(Refusal.NoSlot, state.RaiseBuild(0, capital.Index));
            Assert.AreEqual(0, capital.BuildStage);
            Assert.AreEqual(-1, capital.BuildPlace);
            Assert.AreEqual(before, state.Treasury[0]);
        }

        [Test]
        public void ABuildFromAnOlderSaveStandsOnTheFirstPlaceLeft()
        {
            GameState state = Kingdom(out City capital, out _);
            // As a save older than the place reads it: a stage raised, no place kept.
            capital.BuildStage = 1;
            int[] lots = Towns.Layout(capital);
            Assert.AreEqual(capital.Houses, Array.IndexOf(lots, Towns.BuildLot), "after the houses");
            Assert.AreEqual(Towns.Places(capital) - capital.Houses - 1, Towns.Free(capital));
        }
    }
}

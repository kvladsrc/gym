using System.Collections.Generic;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    public class EconomyTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 42, 100, 777, 1234, 2026, 31337, 65535, 99991 };

        /// <summary>
        /// A 9 × 9 plain with one kingdom: the capital (5 houses, one quarter, as in a new game) in
        /// the middle, a village (3 houses) two cells east, and every cell within two steps of the
        /// capital owned.
        /// </summary>
        static GameState Plain(out City capital, out City village)
        {
            var world = new World(9, 9, 0);
            foreach (Cell c in world.Cells)
                c.Biome = Biome.Plains;
            var country = new Country { Index = 0, Name = "A" };
            world.Countries.Add(country);
            capital = AddCity(world, 4, 4, true, 5);
            village = AddCity(world, 6, 4, false, 3);
            country.Capital = capital.Index;
            Hex home = world.Cells[capital.Cell].Hex;
            foreach (Cell c in world.Cells)
            {
                if (Hex.Distance(c.Hex, home) <= 2)
                    c.Owner = 0;
            }
            world.Cells[village.Cell].Owner = 0;
            AddQuarters(world, capital, 1);
            return new GameState(world);
        }

        /// <summary>Gives the town quarters on its empty neighbours that touch no other town's centre.</summary>
        static void AddQuarters(World world, City city, int count)
        {
            for (int d = 0; d < 6 && count > 0; d++)
            {
                int j = world.Neighbor(city.Cell, d);
                if (j < 0 || world.Cells[j].District >= 0 || Towns.Beside(world, j) != city.Index)
                    continue;
                world.Cells[j].District = city.Index;
                city.Districts.Add(j);
                count--;
            }
            Assert.AreEqual(0, count, "room for the quarters");
        }

        /// <summary>
        /// A street joins the quarter to its town's centre: the walls open a gate on it and a river
        /// between gets a bridge (the players asked for both, 2026-11-11).
        /// </summary>
        static void AssertJoined(World world, City city, int quarter)
        {
            int d = 0;
            while (d < 6 && world.Neighbor(city.Cell, d) != quarter)
                d++;
            Assert.Less(d, 6, $"{city.Name}: the quarter is beside its centre");
            Assert.AreNotEqual(0, world.Cells[city.Cell].Roads >> d & 1, $"{city.Name}: a street leaves the centre for the quarter");
            Assert.AreNotEqual(0, world.Cells[quarter].Roads >> (d + 3) % 6 & 1, $"{city.Name}: the street reaches the quarter");
        }

        /// <summary>A neighbour of the town that touches no other town's centre and has no quarter, at the given distance from another cell.</summary>
        static int Neighbour(World world, City city, int from, int distance)
        {
            for (int d = 0; d < 6; d++)
            {
                int j = world.Neighbor(city.Cell, d);
                if (j >= 0 && world.Cells[j].District < 0 && Towns.Beside(world, j) == city.Index
                    && Hex.Distance(world.Cells[j].Hex, world.Cells[from].Hex) == distance)
                    return j;
            }
            Assert.Fail($"no neighbour {distance} steps away");
            return -1;
        }

        static City AddCity(World world, int col, int row, bool capital, int houses)
        {
            var city = new City
            {
                Index = world.Cities.Count, Name = "C" + world.Cities.Count, Country = 0, Cell = world.IndexOf(col, row),
                Capital = capital, Houses = houses,
            };
            world.Cities.Add(city);
            world.Cells[city.Cell].City = city.Index;
            world.Countries[0].Cities.Add(city.Index);
            return city;
        }

        static Cell OwnedEmptyCell(World world)
        {
            foreach (Cell c in world.Cells)
            {
                if (c.Owner == 0 && c.Town < 0 && c.Resource == Resource.None)
                    return c;
            }
            return null;
        }

        [Test]
        public void ResourcesGiveNothingWithoutTheirBuilding()
        {
            GameState state = Plain(out _, out _);
            Cell field = OwnedEmptyCell(state.World);
            field.Resource = Resource.Wheat;
            Stock before = Economy.Income(state.World, 0);
            Assert.AreEqual(Refusal.None, state.Build(0, field.Index));
            Assert.AreEqual(FieldBuilding.Farm, field.Building);
            Assert.AreEqual(Economy.Start - new Stock(0, 0, 5, 0), state.Treasury[0]);
            Assert.AreEqual(before + new Stock(0, 2, 0, 0), Economy.Income(state.World, 0));
        }

        [Test]
        public void AFarmBesideARiverGivesMore()
        {
            var cell = new Cell { Biome = Biome.Plains, Resource = Resource.Wheat, Building = FieldBuilding.Farm };
            Assert.AreEqual(2, Economy.Yield(cell).Food);
            cell.Rivers = 1;
            Assert.AreEqual(3, Economy.Yield(cell).Food);
            Assert.AreEqual(3, Economy.Potential(cell).Food);
        }

        [Test]
        public void FieldBuildingsAreRefusedWithAReason()
        {
            GameState state = Plain(out City capital, out _);
            World world = state.World;
            Cell field = OwnedEmptyCell(world);
            Assert.AreEqual(Refusal.NoResource, state.Build(0, field.Index));
            Assert.AreEqual(Refusal.NoResource, state.Build(0, capital.Cell));
            field.Resource = Resource.Gold;
            state.Treasury[0] = new Stock(20, 10, 7, 5);
            Assert.AreEqual(Refusal.NotEnough, state.Build(0, field.Index), "a gold mine costs 8 wood and 4 stone");
            state.Treasury[0] += new Stock(0, 0, 10, 10);
            Assert.AreEqual(Refusal.None, state.Build(0, field.Index));
            Assert.AreEqual(Refusal.AlreadyBuilt, state.Build(0, field.Index));
            Cell wild = world.Cells[world.IndexOf(0, 0)];
            wild.Resource = Resource.Wood;
            Assert.AreEqual(Refusal.NotYours, state.Build(0, wild.Index));
        }

        [Test]
        public void LevelsFollowTheHouses()
        {
            int[] levels = { 1, 1, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4 };
            for (int houses = 0; houses < levels.Length; houses++)
                Assert.AreEqual(levels[houses], Towns.Level(houses), $"{houses} houses");
        }

        [TestCaseSource(nameof(Seeds))]
        public void CapitalsStartWithAQuarter(int seed)
        {
            World world = WorldGen.Generate(seed);
            foreach (Country country in world.Countries)
            {
                City capital = world.Cities[country.Capital];
                Assert.AreEqual(1, capital.Districts.Count, capital.Name);
                Cell quarter = world.Cells[capital.Districts[0]];
                Assert.AreEqual(capital.Index, quarter.District);
                Assert.AreEqual(capital.Index, quarter.Town);
                Assert.AreEqual(country.Index, quarter.Owner);
                Assert.That(quarter.IsLand && quarter.Biome != Biome.Mountains && quarter.Resource == Resource.None);
                Assert.AreEqual(1, Hex.Distance(quarter.Hex, world.Cells[capital.Cell].Hex));
                Assert.Greater(Towns.Free(capital), 0, "the capital can still grow");
                AssertJoined(world, capital, capital.Districts[0]);
            }
            foreach (City city in world.Cities)
            {
                if (!city.Capital)
                    Assert.IsEmpty(city.Districts, city.Name);
            }
        }

        [Test]
        public void TownsHavePlacesOnTheirCells()
        {
            GameState state = Plain(out City capital, out City village);
            World world = state.World;
            Assert.AreEqual(Towns.CentrePlaces + Towns.DistrictPlaces, Towns.Places(capital));
            Assert.AreEqual(5, Towns.Taken(capital));
            Assert.AreEqual(6, Towns.Free(capital));
            Assert.AreEqual(Towns.CentrePlaces, Towns.Places(village));
            Assert.AreEqual(2, Towns.Free(village));
            int quarter = capital.Districts[0];
            Assert.AreEqual(0, Towns.FirstPlace(capital, capital.Cell));
            Assert.AreEqual(Towns.CentrePlaces, Towns.FirstPlace(capital, quarter));
            Assert.AreEqual(-1, Towns.FirstPlace(capital, village.Cell));
            Assert.AreEqual(quarter, Towns.CellOf(capital, Towns.CentrePlaces + 2, out int spot));
            Assert.AreEqual(2, spot);
            Assert.AreEqual(capital.Cell, Towns.CellOf(capital, 4, out spot));
            Assert.AreEqual(4, spot);
            CollectionAssert.AreEqual(new[] { Towns.HouseLot, Towns.HouseLot, Towns.HouseLot, Towns.FreeLot, Towns.FreeLot },
                Towns.Layout(village));
        }

        [Test]
        public void HousesAndBuildingsNeverMove()
        {
            GameState state = Plain(out City capital, out City village);
            World world = state.World;
            state.Treasury[0] += new Stock(100, 0, 100, 100);
            int[] before = Towns.Layout(village);
            void Same(string step)
            {
                int[] after = Towns.Layout(village);
                for (int p = 0; p < before.Length; p++)
                {
                    if (before[p] != Towns.FreeLot)
                        Assert.AreEqual(before[p], after[p], $"{step}: place {p}");
                }
                before = after;
            }
            Assert.AreEqual(Refusal.None, state.Build(0, village.Index, TownBuilding.Market));
            Assert.AreEqual(3, village.Places[TownBuilding.Market], "the first free place");
            Same("market");
            village.Houses++;
            Same("fourth house");
            Assert.AreEqual(Refusal.NoSlot, state.Build(0, village.Index, TownBuilding.Granary));
            AddQuarters(world, village, 1);
            Same("quarter");
            village.Houses++;
            Same("fifth house");
            Assert.AreEqual(Refusal.None, state.Build(0, village.Index, TownBuilding.Granary));
            Same("granary");
            Assert.AreEqual(Refusal.None, state.Build(0, village.Index, TownBuilding.Tavern));
            Same("tavern");
            village.Houses += 2;
            Same("two houses");
            village.Buildings.Add(TownBuilding.Walls);
            Same("walls");
            CollectionAssert.AreEqual(new[]
            {
                Towns.HouseLot, Towns.HouseLot, Towns.HouseLot, (int)TownBuilding.Market, Towns.HouseLot,
                Towns.HouseLot, (int)TownBuilding.Granary, (int)TownBuilding.Tavern, Towns.HouseLot, Towns.HouseLot,
                Towns.FreeLot,
            }, Towns.Layout(village));
        }

        [Test]
        public void AFullTownStopsUntilItGrowsOntoANeighbour()
        {
            GameState state = Plain(out City capital, out City village);
            World world = state.World;
            capital.Houses = Towns.MaxHouses;
            state.Treasury[0] += new Stock(100, 0, 100, 100);
            Assert.AreEqual(Refusal.None, state.Build(0, village.Index, TownBuilding.Market));
            Assert.AreEqual(Refusal.None, state.Build(0, village.Index, TownBuilding.Granary));
            Assert.AreEqual(0, Towns.Free(village));
            Assert.AreEqual(GrowthStop.Room, Towns.Limit(village));
            state.Treasury[0].Food = 100;
            TurnReport report = state.EndTurn();
            Assert.AreEqual(3, village.Houses);
            Assert.AreEqual(GrowthStop.Room, village.Stop);
            Assert.AreEqual(2, Stops(report), "the village has no room, the capital is full");

            int far = world.IndexOf(4, 7);
            Assert.AreEqual(Refusal.NotBesideTown, state.Expand(0, far));
            // Neighbours three steps from the capital lie outside the kingdom.
            Assert.AreEqual(Refusal.NotYours, state.Expand(0, Neighbour(world, village, capital.Cell, 3)));
            int cell = Neighbour(world, village, capital.Cell, 2);
            world.Cells[cell].Biome = Biome.Mountains;
            Assert.AreEqual(Refusal.BadGround, state.Expand(0, cell));
            world.Cells[cell].Biome = Biome.Plains;
            world.Cells[cell].Resource = Resource.Stone;
            Assert.AreEqual(Refusal.Taken, state.Expand(0, cell));
            world.Cells[cell].Resource = Resource.None;
            Stock money = state.Treasury[0];
            state.Treasury[0] = new Stock();
            Assert.AreEqual(Refusal.NotEnough, state.Expand(0, cell));
            state.Treasury[0] = money;

            Assert.AreEqual(Refusal.None, state.Expand(0, cell));
            Assert.AreEqual(money - Towns.DistrictCost, state.Treasury[0]);
            CollectionAssert.AreEqual(new[] { cell }, village.Districts);
            Assert.AreEqual(village.Index, world.Cells[cell].District);
            Assert.AreEqual(village.Index, world.Cells[cell].Town);
            Assert.AreEqual(Towns.DistrictPlaces, Towns.Free(village));
            Assert.AreEqual(GrowthStop.None, Towns.Limit(village));
            Assert.AreEqual(Refusal.Taken, state.Expand(0, cell));
            Assert.AreEqual(Refusal.HasRoom, state.Expand(0, Neighbour(world, village, capital.Cell, 2)));
            Assert.AreEqual(Refusal.NoResource, state.Build(0, cell), "a quarter takes no field building");
        }

        [Test]
        public void AFullTownChoosesWhereToGrowAwayFromARiver()
        {
            GameState state = Plain(out City capital, out City village);
            World world = state.World;
            capital.Houses = Towns.MaxHouses;
            state.Treasury[0] += new Stock(100, 0, 100, 100);
            CollectionAssert.IsEmpty(Towns.ExpansionCells(world, village), "a town with room does not grow");
            Assert.AreEqual(Refusal.None, state.Build(0, village.Index, TownBuilding.Market));
            Assert.AreEqual(Refusal.None, state.Build(0, village.Index, TownBuilding.Granary));
            List<int> cells = Towns.ExpansionCells(world, village);
            Assert.That(cells.Count, Is.GreaterThan(1));
            foreach (int cell in cells)
            {
                Assert.AreEqual(Refusal.None, Towns.CanExpand(world, state.Treasury[0], 0, cell, out int town));
                Assert.AreEqual(village.Index, town);
            }

            // A river between the centre and the best cell puts that cell last.
            int first = cells[0];
            int d = 0;
            while (world.Neighbor(village.Cell, d) != first)
                d++;
            world.Cells[village.Cell].Rivers |= (byte)(1 << d);
            List<int> again = Towns.ExpansionCells(world, village);
            Assert.AreEqual(cells.Count, again.Count);
            Assert.AreNotEqual(first, again[0]);
            Assert.AreEqual(first, again[again.Count - 1]);
            Assert.AreEqual(Refusal.None, state.Expand(0, again[0]));
            AssertJoined(world, village, again[0]);
            CollectionAssert.IsEmpty(Towns.ExpansionCells(world, village), "the new quarter has room");
        }

        [TestCaseSource(nameof(Seeds))]
        public void CapitalsStartAsTownsAndTheOthersAsVillages(int seed)
        {
            World world = WorldGen.Generate(seed);
            foreach (City city in world.Cities)
                Assert.AreEqual(city.Capital ? 2 : 1, Towns.Level(city), city.Name);
        }

        [Test]
        public void AVillageGrowsWithFood()
        {
            GameState state = Plain(out City capital, out City village);
            capital.Houses = Towns.MaxHouses;
            state.Treasury[0].Food = 100;
            // The fourth house needs 2 × 3 = 6 food, 2 a turn.
            for (int turn = 0; turn < 2; turn++)
                state.EndTurn();
            Assert.AreEqual(3, village.Houses);
            Assert.AreEqual(4, village.Growth);
            TurnReport report = state.EndTurn();
            Assert.AreEqual(4, village.Houses);
            Assert.AreEqual(0, village.Growth);
            CollectionAssert.Contains(report.Changed, village.Cell);
            Assert.AreEqual(TurnEventKind.Grew, report.Events[0].Kind);
            // The fifth (8 food) makes it a town.
            for (int turn = 0; turn < 3; turn++)
                state.EndTurn();
            report = state.EndTurn();
            Assert.AreEqual(5, village.Houses);
            Assert.AreEqual(TurnEventKind.LevelUp, report.Events[0].Kind);
        }

        [Test]
        public void AGranaryDoublesTheGrowth()
        {
            GameState state = Plain(out City capital, out City village);
            capital.Houses = Towns.MaxHouses;
            village.Buildings.Add(TownBuilding.Granary);
            AddQuarters(state.World, village, 1);
            state.Treasury[0].Food = 100;
            state.EndTurn();
            state.EndTurn();
            Assert.AreEqual(4, village.Houses, "6 food at 4 a turn");
            Assert.AreEqual(Towns.GranaryIntake, Towns.Demand(state.World, 0));
        }

        [Test]
        public void TownsWaitForATavernThenStopAtTheTop()
        {
            GameState state = Plain(out City capital, out City village);
            village.Houses = Towns.TavernHouses;
            AddQuarters(state.World, village, 1);
            capital.Houses = Towns.MaxHouses;
            state.Treasury[0].Food = 100;
            TurnReport report = state.EndTurn();
            Assert.AreEqual(Towns.TavernHouses, village.Houses);
            Assert.AreEqual(GrowthStop.Tavern, village.Stop);
            Assert.AreEqual(GrowthStop.Full, capital.Stop);
            Assert.AreEqual(2, Stops(report), "each stop is told once");
            Assert.AreEqual(0, Stops(state.EndTurn()));
            Assert.AreEqual(0, report.GrowthFood);
            village.Buildings.Add(TownBuilding.Tavern);
            state.EndTurn();
            Assert.AreEqual(2, village.Growth);
            Assert.AreEqual(GrowthStop.None, village.Stop);
        }

        static int Stops(TurnReport report) => report.Events.FindAll(e => e.Kind == TurnEventKind.Stopped).Count;

        [Test]
        public void WhenFoodRunsShortTheLaterTownsWait()
        {
            GameState state = Plain(out City capital, out City village);
            // Food income is 2 (capital) + 1 (village); both want 2.
            state.Treasury[0].Food = 0;
            TurnReport report = state.EndTurn();
            Assert.AreEqual(2, capital.Growth);
            Assert.AreEqual(1, village.Growth);
            Assert.AreEqual(0, state.Treasury[0].Food);
            Assert.AreEqual(3, report.GrowthFood);
            Assert.AreEqual(GrowthStop.Food, village.Stop);
            Assert.AreEqual(TurnEventKind.Stopped, report.Events[0].Kind);
            Assert.AreEqual(village.Index, report.Events[0].City);
        }

        [Test]
        public void TownBuildingsNeedTheirLevelAndAPlace()
        {
            GameState state = Plain(out City capital, out City village);
            state.Treasury[0] += new Stock(100, 0, 100, 100);
            Assert.AreEqual(Refusal.LevelTooLow, state.Build(0, village.Index, TownBuilding.Tavern));
            Stock income = Economy.Income(state.World, 0);
            Assert.AreEqual(Refusal.None, state.Build(0, village.Index, TownBuilding.Market));
            Assert.AreEqual(income.Gold + 2, Economy.Income(state.World, 0).Gold);
            Assert.AreEqual(Refusal.AlreadyBuilt, state.Build(0, village.Index, TownBuilding.Market));
            Assert.AreEqual(Refusal.None, state.Build(0, village.Index, TownBuilding.Granary));
            village.Houses = 5;
            Assert.AreEqual(Refusal.NoSlot, state.Build(0, village.Index, TownBuilding.Tavern), "five houses and two buildings fill the centre");
            AddQuarters(state.World, village, 1);
            Assert.AreEqual(Refusal.None, state.Build(0, village.Index, TownBuilding.Tavern));
            Assert.AreEqual(Refusal.None, state.Build(0, capital.Index, TownBuilding.Tavern));
            state.Treasury[0] = new Stock();
            Assert.AreEqual(Refusal.NotEnough, state.Build(0, capital.Index, TownBuilding.Market));
        }

        [Test]
        public void WallsAndACastleTakeNoPlaceAndTheCastleStandsInsideWalls()
        {
            GameState state = Plain(out City capital, out City village);
            state.Treasury[0] += new Stock(100, 0, 100, 100);
            Assert.AreEqual(Refusal.LevelTooLow, state.Build(0, village.Index, TownBuilding.Walls));
            Assert.AreEqual(Refusal.NotACity, state.Build(0, capital.Index, TownBuilding.Castle));
            capital.Houses = 8;
            Assert.AreEqual(Refusal.NoWalls, state.Build(0, capital.Index, TownBuilding.Castle));
            AddQuarters(state.World, capital, 1);
            foreach (TownBuilding building in Economy.TownBuildings)
                Assert.AreEqual(Refusal.None, state.Build(0, capital.Index, building), building.ToString());
            Assert.AreEqual(Economy.TownBuildings.Length, Towns.Used(capital));
            int free = Towns.Free(capital);
            Stock before = state.Treasury[0];
            Assert.AreEqual(Refusal.None, state.Build(0, capital.Index, TownBuilding.Walls));
            Assert.AreEqual(before - Economy.Cost(TownBuilding.Walls), state.Treasury[0]);
            Assert.AreEqual(Refusal.None, state.Build(0, capital.Index, TownBuilding.Castle));
            Assert.AreEqual(Economy.TownBuildings.Length, Towns.Used(capital));
            Assert.AreEqual(free, Towns.Free(capital));
            Assert.AreEqual(Refusal.AlreadyBuilt, state.Build(0, capital.Index, TownBuilding.Walls));
        }

        [Test]
        public void ACityGrowsIntoARoyalCityOnlyWithACastle()
        {
            GameState state = Plain(out City capital, out City village);
            capital.Houses = Towns.MaxHouses;
            village.Houses = Towns.CastleHouses;
            village.Buildings.Add(TownBuilding.Tavern);
            AddQuarters(state.World, village, 2);
            state.Treasury[0].Food = 100;
            TurnReport report = state.EndTurn();
            Assert.AreEqual(GrowthStop.Castle, village.Stop);
            Assert.AreEqual(2, Stops(report), "the village waits for a castle, the capital is full");
            Assert.AreEqual(GrowthStop.Full, capital.Stop);
            village.Buildings.Add(TownBuilding.Walls);
            village.Buildings.Add(TownBuilding.Castle);
            state.EndTurn();
            Assert.AreEqual(GrowthStop.None, village.Stop);
            for (int turn = 0; turn < 60; turn++)
            {
                state.Treasury[0].Food = 100;
                state.EndTurn();
            }
            Assert.AreEqual(Towns.MaxHouses, village.Houses);
            Assert.AreEqual(4, Towns.Level(village));
            Assert.AreEqual(3, Towns.Free(village), "17 places: 13 houses and the tavern");
        }

        [Test]
        public void TaxesGrowWithTheLevel()
        {
            GameState state = Plain(out City capital, out City village);
            Assert.AreEqual(new Stock(4, 2, 1, 0), Economy.CityYield(capital));
            Assert.AreEqual(new Stock(2, 1, 0, 0), Economy.CityYield(village));
            village.Houses = 8;
            Assert.AreEqual(4, Economy.CityYield(village).Gold);
        }

        [Test]
        public void TasksPayTheirRewardOnce()
        {
            GameState state = Plain(out _, out _);
            CollectionAssert.AreEqual(new[] { Quest.Farm, Quest.LumberCamp, Quest.Fight }, state.Offered());
            Cell field = OwnedEmptyCell(state.World);
            field.Resource = Resource.Wheat;
            state.Build(0, field.Index);
            Stock before = state.Treasury[0];
            CollectionAssert.AreEqual(new[] { Quest.Farm }, state.CheckQuests());
            Assert.AreEqual(before + Quests.Reward(Quest.Farm), state.Treasury[0]);
            Assert.IsEmpty(state.CheckQuests());
            CollectionAssert.AreEqual(new[] { Quest.LumberCamp, Quest.Fight, Quest.Chest }, state.Offered());
        }

        [TestCaseSource(nameof(Seeds))]
        public void ASensibleKingdomGrowsOnSchedule(int seed)
        {
            World world = WorldGen.Generate(seed);
            var state = new GameState(world);
            int town = 0, city = 0;
            for (int turn = 1; turn <= 40; turn++)
            {
                // The other kingdoms play at the turn's end, as many things a turn as the difficulty allows.
                Rival.PlayATurn(state, world.Player);
                state.EndTurn();
                for (int k = 0; k < world.Countries.Count; k++)
                {
                    Stock t = state.Treasury[k];
                    Assert.That(t.Gold >= 0 && t.Food >= 0 && t.Wood >= 0 && t.Stone >= 0, $"turn {turn}, kingdom {k}: {t}");
                }
                foreach (int index in world.Countries[world.Player].Cities)
                {
                    City c = world.Cities[index];
                    if (town == 0 && !c.Capital && Towns.Level(c) >= 2)
                        town = turn;
                    if (city == 0 && Towns.Level(c) >= 3)
                        city = turn;
                }
            }
            Stock income = Economy.Income(world, world.Player);
            TestContext.WriteLine($"seed {seed}: first town at turn {town}, first city at turn {city}, income at 40: {income}, " +
                $"grown {state.Grown[world.Player]}, tasks {state.Done.Count}");
            Assert.That(town, Is.InRange(3, 12), "the first village becomes a town");
            Assert.That(city, Is.InRange(8, 25), "the first level-3 town");
        }

        [TestCaseSource(nameof(Seeds))]
        public void IdleKingdomsNeverGoIntoDebt(int seed)
        {
            var state = new GameState(WorldGen.Generate(seed));
            for (int turn = 0; turn < 40; turn++)
                state.EndTurn();
            foreach (Stock t in state.Treasury)
                Assert.That(t.Gold >= 0 && t.Food >= 0 && t.Wood >= 0 && t.Stone >= 0, t.ToString());
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>The land grows by itself from the towns (the territory plan of 2026-11-11).</summary>
    public class InfluenceTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 42, 100, 777, 1234, 2026, 31337, 65535, 99991 };

        /// <summary>A plain of the given size with the given number of kingdoms and no towns yet.</summary>
        internal static World Plain(int width, int height, int kingdoms)
        {
            var world = new World(width, height, 0);
            foreach (Cell c in world.Cells)
                c.Biome = Biome.Plains;
            for (int k = 0; k < kingdoms; k++)
                world.Countries.Add(new Country { Index = k, Name = "K" + k });
            return world;
        }

        /// <summary>A town of the kingdom with the cells within <paramref name="ring"/> steps of it owned.</summary>
        internal static City Town(World world, int country, int col, int row, int houses, bool capital, int ring = 1)
        {
            var city = new City
            {
                Index = world.Cities.Count, Name = "T" + world.Cities.Count, Country = country, Cell = world.IndexOf(col, row),
                Capital = capital, Houses = houses,
            };
            world.Cities.Add(city);
            world.Cells[city.Cell].City = city.Index;
            world.Countries[country].Cities.Add(city.Index);
            if (capital)
                world.Countries[country].Capital = city.Index;
            Hex home = world.Cells[city.Cell].Hex;
            foreach (Cell c in world.Cells)
            {
                if (c.IsLand && Hex.Distance(c.Hex, home) <= ring)
                    c.Owner = country;
            }
            return city;
        }

        static int Owned(World world, City city, int within)
        {
            Hex home = world.Cells[city.Cell].Hex;
            return world.Cells.Count(c => c.Owner == city.Country && c.IsLand && Hex.Distance(c.Hex, home) <= within);
        }

        [Test]
        public void ABiggerTownTakesItsFirstCellSooner()
        {
            World world = Plain(27, 9, 1);
            City capital = Town(world, 0, 4, 4, 5, true);
            City small = Town(world, 0, 13, 4, 3, false);
            City big = Town(world, 0, 22, 4, 5, false);
            var state = new GameState(world);
            var first = new Dictionary<City, int>();
            for (int turn = 1; turn <= 6; turn++)
            {
                Influence.Grow(state, new TurnReport());
                foreach (City city in new[] { capital, small, big })
                {
                    if (!first.ContainsKey(city) && Owned(world, city, 3) > 7)
                        first[city] = turn;
                }
            }
            Assert.AreEqual(2, first[capital], "a capital of 5 houses: level 2 and the capital's 1");
            Assert.AreEqual(3, first[big], "a town of 5 houses: level 2");
            Assert.AreEqual(6, first[small], "a town of 3 houses: level 1");
            Assert.AreEqual(3 + 2 + 1, state.Grown[0], "18, 12 and 6 influence in six turns");
        }

        [Test]
        public void ATownTakesTheCheapestCellAndAResourceFirst()
        {
            World world = Plain(15, 15, 1);
            City capital = Town(world, 0, 7, 7, 5, true);
            Hex home = world.Cells[capital.Cell].Hex;
            List<int> ring = world.Cells.Where(c => Hex.Distance(c.Hex, home) == 2).Select(c => c.Index).ToList();
            Assert.AreEqual(ring.Min(), Influence.Next(world, capital), "among equals the lower index");

            world.Cells[ring.Max()].Resource = Resource.Wood;
            Assert.AreEqual(ring.Max(), Influence.Next(world, capital), "a resource first");

            world.Cells[ring.Max()].Resource = Resource.None;
            foreach (int cell in ring)
                world.Cells[cell].Biome = Biome.Forest;
            int open = ring[ring.Count / 2];
            world.Cells[open].Biome = Biome.Plains;
            Assert.AreEqual(open, Influence.Next(world, capital), "a plain before the forests");
        }

        [Test]
        public void NoTownTakesTheSeaMonstersTheirLairsOrAnotherKingdomsHome()
        {
            World world = Plain(19, 15, 2);
            City capital = Town(world, 0, 7, 7, 5, true);
            City other = Town(world, 1, 11, 7, 5, true);
            int sea = world.IndexOf(5, 7), group = world.IndexOf(7, 5), beside = world.IndexOf(7, 9), lair = world.IndexOf(7, 10);
            world.Cells[sea].Biome = Biome.Ocean;
            world.Cells[group].Monster = Monster.Wolves;
            world.Cells[group].Count = 4;
            world.Cells[lair].Monster = Monster.Goblins;
            world.Cells[lair].Count = 6;
            world.Cells[lair].Lair = true;
            int near = world.IndexOf(9, 7);
            Assert.That(Hex.Distance(world.Cells[near].Hex, world.Cells[other.Cell].Hex), Is.AtMost(Influence.HomeCircle));
            Assert.IsTrue(Enumerable.Range(0, 6).Any(d => world.Neighbor(beside, d) == lair), "the cell is beside the lair");

            Assert.IsFalse(Influence.Takeable(world, 0, sea), "the sea");
            Assert.IsFalse(Influence.Takeable(world, 0, group), "a group of monsters");
            Assert.IsFalse(Influence.Takeable(world, 0, beside), "beside a lair");
            Assert.IsFalse(Influence.Takeable(world, 0, near), "the other kingdom's home");
            Hex home = world.Cells[capital.Cell].Hex;
            Assert.IsTrue(world.Cells.Any(c => Hex.Distance(c.Hex, home) == 2 && Influence.Takeable(world, 0, c.Index)), "open plains beside the land");

            var state = new GameState(world);
            for (int turn = 0; turn < 30; turn++)
                Influence.Grow(state, new TurnReport());
            foreach (int cell in new[] { sea, group, beside, lair, near })
                Assert.AreNotEqual(0, world.Cells[cell].Owner, $"cell {world.Cells[cell].Hex}");
            Assert.That(state.Grown[0], Is.GreaterThan(10), "and it took the rest");
        }

        [Test]
        public void ACellTwoKingdomsWantGoesToTheCheaperWay()
        {
            // A strip of land in the sea: the kingdoms' lands end one cell apart, and that cell is
            // all either can take. The west capital is two steps nearer.
            World world = Strip(out City west, out City east, 2, 12);
            int between = world.IndexOf(6, 4);
            west.Influence = east.Influence = Influence.CellPrice;
            var state = new GameState(world);
            var report = new TurnReport();
            Influence.Grow(state, report);
            Assert.AreEqual(0, world.Cells[between].Owner);
            Assert.That(east.Influence, Is.EqualTo(Influence.CellPrice), "the east kept its influence, up to the price");
            Assert.IsTrue(east.LandFull);
        }

        [Test]
        public void AnEvenClaimGoesToTheKingdomWithLessLand()
        {
            // Both capitals four steps from the cell; the west has a second row of land.
            World world = Strip(out City west, out City east, 2, 10);
            for (int col = 1; col <= 5; col++)
            {
                world.Cells[world.IndexOf(col, 3)].Biome = Biome.Plains;
                world.Cells[world.IndexOf(col, 3)].Owner = 0;
            }
            int between = world.IndexOf(6, 4);
            Assert.AreEqual(-1, world.Cells[world.IndexOf(6, 3)].Owner);
            west.Influence = east.Influence = Influence.CellPrice;
            var state = new GameState(world);
            Influence.Grow(state, new TurnReport());
            Assert.AreEqual(1, world.Cells[between].Owner);
        }

        /// <summary>
        /// A strip of plain on row 4 of a sea, columns 1-13: the west kingdom holds columns 1-5 with
        /// its capital at <paramref name="westCol"/>, the east holds 7-13 with its capital at
        /// <paramref name="eastCol"/>, and column 6 is wild.
        /// </summary>
        static World Strip(out City west, out City east, int westCol, int eastCol)
        {
            World world = Plain(15, 9, 2);
            foreach (Cell c in world.Cells)
                c.Biome = c.Row == 4 && c.Col >= 1 && c.Col <= 13 ? Biome.Plains : Biome.Ocean;
            west = Town(world, 0, westCol, 4, 5, true, 0);
            east = Town(world, 1, eastCol, 4, 5, true, 0);
            for (int col = 1; col <= 13; col++)
                world.Cells[world.IndexOf(col, 4)].Owner = col < 6 ? 0 : col > 6 ? 1 : -1;
            return world;
        }

        [Test]
        public void ATownWithNowhereToGrowStoresNoMoreThanACell()
        {
            World world = Plain(9, 9, 1);
            foreach (Cell c in world.Cells)
                c.Biome = Biome.Ocean;
            Hex home = Hex.FromOffset(4, 4);
            foreach (Cell c in world.Cells)
            {
                if (Hex.Distance(c.Hex, home) <= 1)
                    c.Biome = Biome.Plains;
            }
            City capital = Town(world, 0, 4, 4, 5, true);
            var state = new GameState(world);
            int warned = 0;
            for (int turn = 0; turn < 10; turn++)
            {
                var report = new TurnReport();
                Influence.Grow(state, report);
                warned += report.Events.Count(e => e.Kind == TurnEventKind.LandFull);
                Assert.That(capital.Influence, Is.AtMost(Influence.CellPrice));
            }
            Assert.IsTrue(capital.LandFull);
            Assert.AreEqual(1, warned, "the players are told once");
            Assert.AreEqual(-1, Influence.TurnsTo(world, 0, world.IndexOf(6, 4), out _), "the sea is never taken");
        }

        [Test]
        public void ACellClearedOfMonstersJoinsTheLandItTouchesOrIsWithinReach()
        {
            World world = Plain(31, 9, 2);
            City capital = Town(world, 0, 4, 4, 5, true);
            City other = Town(world, 1, 26, 4, 5, true);
            Assert.IsTrue(Influence.TakesCleared(world, 0, world.IndexOf(6, 4)), "beside the land");
            Assert.IsTrue(Influence.TakesCleared(world, 0, world.IndexOf(8, 4)), "within the capital's reach");
            Assert.IsFalse(Influence.TakesCleared(world, 0, world.IndexOf(4 + Influence.Reach(capital) + 2, 4)), "beyond it");
            Assert.IsFalse(Influence.TakesCleared(world, 0, world.IndexOf(24, 4)), "the other kingdom's home");
            Assert.IsFalse(Influence.TakesCleared(world, 0, world.IndexOf(5, 4)), "already the kingdom's");
            Assert.That(Hex.Distance(world.Cells[world.IndexOf(24, 4)].Hex, world.Cells[other.Cell].Hex), Is.AtMost(Influence.HomeCircle));
        }

        [TestCaseSource(nameof(Seeds))]
        public void NoCellChangesHandsAndEveryTakenCellIsDressed(int seed)
        {
            World world = WorldGen.Generate(seed);
            var state = new GameState(world);
            int[] owners = world.Cells.Select(c => c.Owner).ToArray();
            for (int turn = 0; turn < 60; turn++)
            {
                var lairs = new HashSet<int>(world.Cells.Where(c => c.Lair).Select(c => c.Index));
                TurnReport report = state.EndTurn();
                var changed = new HashSet<int>(report.Changed);
                var gained = new int[world.Countries.Count];
                foreach (Cell c in world.Cells)
                {
                    if (owners[c.Index] >= 0)
                        Assert.AreEqual(owners[c.Index], c.Owner, $"turn {state.Turn}: {c.Hex} changed hands");
                    else if (c.Owner >= 0)
                    {
                        Assert.IsTrue(changed.Contains(c.Index), $"turn {state.Turn}: {c.Hex} taken but not in the report");
                        // The shallows follow the land (ShoreOwner): the kingdoms' list counts the land its towns
                        // took, not a lair's cell a neighbour's troop won (Troops), as it does not count a hero's.
                        if (c.IsLand && !lairs.Contains(c.Index))
                            gained[c.Owner]++;
                    }
                    owners[c.Index] = c.Owner;
                }
                CollectionAssert.AreEqual(gained, state.GrownLastTurn, $"turn {state.Turn}: the cells taken last turn");
                Assert.AreEqual(gained[world.Player], report.Events.Count(e => e.Kind == TurnEventKind.Border),
                    $"turn {state.Turn}: a report line for each cell the players' towns took");
            }
            Assert.That(state.Grown.Sum(), Is.GreaterThan(0));
        }

        [Test]
        public void ASeedGrowsTheSameBorders()
        {
            string Borders()
            {
                var state = new GameState(WorldGen.Generate(42));
                for (int turn = 0; turn < 30; turn++)
                    state.EndTurn();
                return string.Join(",", state.World.Cells.Select(c => c.Owner));
            }
            Assert.AreEqual(Borders(), Borders());
        }

        [TestCaseSource(nameof(Seeds))]
        public void EveryKingdomHasATownWithLandToTakeAtTheStart(int seed)
        {
            // Not every town: a capital on a small island may hold all its land from the start (seed 2).
            World world = WorldGen.Generate(seed);
            foreach (Country country in world.Countries)
            {
                Assert.IsTrue(country.Cities.Any(i => Influence.Next(world, world.Cities[i]) >= 0), country.Name);
            }
        }
    }
}

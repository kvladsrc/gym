using System;
using System.Collections.Generic;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    public class WorldGenTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 42, 100, 777, 1234, 2026, 31337, 65535, 99991 };

        static string Fingerprint(World world)
        {
            var parts = new List<string>();
            foreach (Cell c in world.Cells)
                parts.Add($"{(int)c.Biome}{c.Owner}{c.City}{(int)c.Resource}{(int)c.Monster}");
            foreach (City city in world.Cities)
                parts.Add($"{city.Name}@{city.Cell}");
            return string.Join(",", parts);
        }

        [Test]
        public void ASeedAlwaysMakesTheSameWorld()
        {
            Assert.AreEqual(Fingerprint(WorldGen.Generate(42)), Fingerprint(WorldGen.Generate(42)));
            Assert.AreNotEqual(Fingerprint(WorldGen.Generate(42)), Fingerprint(WorldGen.Generate(43)));
        }

        [TestCaseSource(nameof(Seeds))]
        public void LandAndSea(int seed)
        {
            World world = WorldGen.Generate(seed);
            int land = 0;
            foreach (Cell c in world.Cells)
            {
                if (c.IsLand)
                    land++;
                bool rim = c.Col == 0 || c.Row == 0 || c.Col == world.Width - 1 || c.Row == world.Height - 1;
                if (rim)
                    Assert.IsTrue(c.IsWater, $"land on the map's rim at {c.Hex}");
            }
            float share = land / (float)world.Cells.Length;
            Assert.That(share, Is.InRange(0.38f, 0.5f), "land share");
        }

        [TestCaseSource(nameof(Seeds))]
        public void SixWholeKingdoms(int seed)
        {
            World world = WorldGen.Generate(seed);
            Assert.AreEqual(6, world.Countries.Count);
            foreach (Country country in world.Countries)
            {
                City capital = world.Cities[country.Capital];
                Cell home = world.Cells[capital.Cell];
                Assert.IsTrue(capital.Capital);
                Assert.IsTrue(home.IsLand, $"{country.Name}: capital on water");
                Assert.AreEqual(country.Index, home.Owner);

                // All of the kingdom's land is reachable from the capital over its own land.
                var seen = new HashSet<int> { home.Index };
                var queue = new Queue<int>();
                queue.Enqueue(home.Index);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    for (int d = 0; d < 6; d++)
                    {
                        int j = world.Neighbor(i, d);
                        if (j >= 0 && world.Cells[j].IsLand && world.Cells[j].Owner == country.Index && seen.Add(j))
                            queue.Enqueue(j);
                    }
                }
                int land = 0;
                foreach (Cell c in world.Cells)
                {
                    if (c.IsLand && c.Owner == country.Index)
                        land++;
                }
                Assert.AreEqual(land, seen.Count, $"{country.Name}: land split in parts");
                Assert.That(land, Is.GreaterThanOrEqualTo(12), $"{country.Name}: too small");
                Assert.That(country.Cities.Count, Is.InRange(2, 4), $"{country.Name}: cities");
            }
        }

        [TestCaseSource(nameof(Seeds))]
        public void EveryKingdomHasWheatWoodStoneAndGold(int seed)
        {
            World world = WorldGen.Generate(seed);
            foreach (Country country in world.Countries)
            {
                foreach (Resource resource in new[] { Resource.Wheat, Resource.Wood, Resource.Stone, Resource.Gold })
                {
                    bool found = false;
                    foreach (Cell c in world.Cells)
                        found |= c.Owner == country.Index && c.IsLand && c.Resource == resource;
                    Assert.IsTrue(found, $"{country.Name}: no {resource}");
                }
            }
        }

        [TestCaseSource(nameof(Seeds))]
        public void CitiesLairsAndWaters(int seed)
        {
            World world = WorldGen.Generate(seed);
            foreach (City city in world.Cities)
            {
                Cell cell = world.Cells[city.Cell];
                Assert.IsTrue(cell.IsLand);
                Assert.AreEqual(city.Country, cell.Owner);
                Assert.AreEqual(city.Index, cell.City);
                foreach (City other in world.Cities)
                {
                    if (other != city)
                        Assert.That(Hex.Distance(cell.Hex, world.Cells[other.Cell].Hex), Is.GreaterThanOrEqualTo(3));
                }
            }
            int lairs = 0;
            foreach (Cell c in world.Cells)
            {
                if (c.Monster != Monster.None)
                {
                    lairs++;
                    Assert.IsTrue(c.IsLand && c.Owner < 0 && c.City < 0, $"lair at {c.Hex}");
                }
                if (c.Biome == Biome.Ocean)
                    Assert.Less(c.Owner, 0, $"open sea owned at {c.Hex}");
                if (c.Resource != Resource.None)
                    Assert.AreEqual(Resource.Fish == c.Resource, c.IsWater, $"{c.Resource} at {c.Hex}");
            }
            Assert.That(lairs, Is.GreaterThanOrEqualTo(4));
        }

        [TestCaseSource(nameof(Seeds))]
        public void RoadsJoinEveryTownToItsCapital(int seed)
        {
            World world = WorldGen.Generate(seed);
            foreach (Cell c in world.Cells)
            {
                for (int d = 0; d < 6; d++)
                {
                    if ((c.Roads >> d & 1) == 0)
                        continue;
                    Assert.IsTrue(c.IsLand, $"road on water at {c.Hex}");
                    int j = world.Neighbor(c.Index, d);
                    Assert.GreaterOrEqual(j, 0, $"road off the map at {c.Hex}");
                    Assert.AreEqual(1, world.Cells[j].Roads >> ((d + 3) % 6) & 1, $"one-way road at {c.Hex}");
                }
            }
            foreach (Country country in world.Countries)
            {
                int capital = world.Cities[country.Capital].Cell;
                var seen = new HashSet<int> { capital };
                var queue = new Queue<int>();
                queue.Enqueue(capital);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    for (int d = 0; d < 6; d++)
                    {
                        if ((world.Cells[i].Roads >> d & 1) != 0 && seen.Add(world.Neighbor(i, d)))
                            queue.Enqueue(world.Neighbor(i, d));
                    }
                }
                foreach (int city in country.Cities)
                    Assert.IsTrue(seen.Contains(world.Cities[city].Cell), $"{world.Cities[city].Name} has no road to {country.Name}'s capital");
            }
        }

        [TestCaseSource(nameof(Seeds))]
        public void RiversRunBetweenLandCellsDownToWater(int seed)
        {
            World world = WorldGen.Generate(seed);
            TestContext.WriteLine($"seed {seed}: {world.Rivers.Count} rivers");
            Assert.That(world.Rivers.Count, Is.AtLeast(3), "rivers");
            for (int r = 0; r < world.Rivers.Count; r++)
            {
                River river = world.Rivers[r];
                List<RiverPoint> points = river.Points;
                Assert.That(points.Count, Is.AtLeast(5), $"river {r} length");
                for (int k = 1; k < points.Count; k++)
                {
                    float dx = points[k].X - points[k - 1].X, dz = points[k].Z - points[k - 1].Z;
                    Assert.AreEqual(1f, MathF.Sqrt(dx * dx + dz * dz), 1e-3f, $"river {r}: points {k - 1} and {k} are not one edge apart");
                    Assert.That(points[k].Flow, Is.GreaterThanOrEqualTo(points[k - 1].Flow), $"river {r} shrinks at {k}");
                }
                RiverPoint end = points[points.Count - 1];
                if (river.Joins >= 0)
                {
                    Assert.That(river.Joins, Is.LessThan(r), $"river {r} joins a later river");
                    Assert.IsTrue(world.Rivers[river.Joins].Points.Exists(p => p.X == end.X && p.Z == end.Z), $"river {r} ends off the river it joins");
                }
                else
                {
                    Assert.That(river.Mouth, Is.GreaterThanOrEqualTo(0), $"river {r} has neither mouth nor confluence");
                    Assert.IsTrue(world.Cells[river.Mouth].IsWater, $"river {r} ends on land");
                    float dx = end.X - world.CenterX(river.Mouth), dz = end.Z - world.CenterZ(river.Mouth);
                    Assert.AreEqual(1f, MathF.Sqrt(dx * dx + dz * dz), 1e-3f, $"river {r} does not reach its mouth");
                }
            }
            // Both cells on a river edge are land and both know of the river.
            foreach (Cell cell in world.Cells)
            {
                for (int d = 0; d < 6; d++)
                {
                    if ((cell.Rivers >> d & 1) == 0)
                        continue;
                    int other = world.Neighbor(cell.Index, d);
                    Assert.IsTrue(cell.IsLand, $"river along water at {cell.Hex}");
                    Assert.That(other, Is.GreaterThanOrEqualTo(0), $"river off the map at {cell.Hex}");
                    Assert.IsTrue(world.Cells[other].IsLand, $"river along water at {world.Cells[other].Hex}");
                    Assert.AreEqual(1, world.Cells[other].Rivers >> ((d + 3) % 6) & 1, $"{world.Cells[other].Hex} misses the river at {cell.Hex}");
                }
            }
        }

        [Test]
        public void TurnsPayIncome()
        {
            var state = new GameState(WorldGen.Generate(7));
            Stock before = state.Treasury[0];
            Stock income = Economy.Income(state.World, 0);
            Assert.Greater(income.Gold, 0);
            Assert.Greater(income.Food, 0);
            TurnReport report = state.EndTurn();
            Assert.AreEqual(2, state.Turn);
            Assert.AreEqual(income, report.Income);
            Assert.AreEqual(before.Gold + income.Gold, state.Treasury[0].Gold);
            Assert.Greater(report.GrowthFood, 0, "the towns take food for their growth");
            Assert.AreEqual(before.Food + income.Food - report.GrowthFood, state.Treasury[0].Food);
        }
    }
}

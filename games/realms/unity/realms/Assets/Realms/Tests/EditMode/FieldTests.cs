using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>
    /// The fields' patterns and events (the combat plan of 2026-11-11, 3.5; <see cref="Battle.Patterns"/>,
    /// <see cref="FieldEvent"/>): the plan's checks on 500 seeds, where an ambush starts, and the chest.
    /// </summary>
    public class FieldTests
    {
        static readonly Biome[] Grounds = { Biome.Meadow, Biome.Forest, Biome.Hills, Biome.Mountains };

        static Battle Field(Biome ground, int seed, FieldEvent fieldEvent = FieldEvent.None, Monster monster = Monster.Goblins,
            int count = 20) =>
            new Battle(new[] { new Stack(Unit.Spearmen, 10), new Stack(Unit.Archers, 10) }, 0, 0, monster, count, ground, seed,
                fieldEvent);

        static int Col(int cell) => cell % Battle.Width;

        static int Row(int cell) => cell / Battle.Width;

        static IEnumerable<Fighter> Monsters(Battle battle) => battle.Fighters.Where(f => f.Side == Side.Monsters);

        static int Count(Battle battle, BattleEventKind kind) => battle.Events.Count(e => e.Kind == kind);

        /// <summary>The cells without obstacles reached from the left edge over cells without obstacles.</summary>
        static HashSet<int> FromLeftEdge(Battle battle)
        {
            var reached = new HashSet<int>();
            var queue = new Queue<int>();
            for (int r = 0; r < Battle.Height; r++)
            {
                int cell = r * Battle.Width;
                if (!battle.Blocked[cell] && reached.Add(cell))
                    queue.Enqueue(cell);
            }
            while (queue.Count > 0)
            {
                foreach (int n in Battle.NeighboursOf(queue.Dequeue()))
                {
                    if (!battle.Blocked[n] && reached.Add(n))
                        queue.Enqueue(n);
                }
            }
            return reached;
        }

        [Test]
        public void TheGroundChoosesThePattern()
        {
            Assert.AreEqual(FieldShape.Grove, Battle.ShapeOf(Biome.Forest));
            Assert.AreEqual(FieldShape.Gorge, Battle.ShapeOf(Biome.Hills));
            Assert.AreEqual(FieldShape.Gorge, Battle.ShapeOf(Biome.Mountains));
            Assert.AreEqual(FieldShape.Open, Battle.ShapeOf(Biome.Meadow));
            Assert.AreEqual(FieldShape.Open, Battle.ShapeOf(Biome.Plains));
        }

        /// <summary>The plan's check: in every pattern on 500 seeds both starts are free and every free cell can be reached.</summary>
        [Test]
        public void EveryPatternKeepsBothStartsFreeAndAWayToEveryFreeCell()
        {
            Assert.IsTrue(Battle.Patterns, "the patterns are on");
            foreach (Biome ground in Grounds)
            {
                for (int seed = 0; seed < 500; seed++)
                {
                    Battle battle = Field(ground, seed);
                    for (int cell = 0; cell < battle.Blocked.Length; cell++)
                    {
                        if (battle.Blocked[cell])
                            Assert.That(Col(cell), Is.InRange(2, 8), $"{ground}, seed {seed}: an obstacle in column {Col(cell)}");
                    }
                    int free = battle.Blocked.Count(b => !b);
                    Assert.AreEqual(free, FromLeftEdge(battle).Count, $"{ground}, seed {seed}: a free cell is cut off");
                }
            }
        }

        [Test]
        public void TheGroveIsTwoClumpsOfTreesAndTheGorgeAWallWithTwoGaps()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                Battle grove = Field(Biome.Forest, seed);
                Assert.AreEqual(4, grove.Blocked.Count(b => b), $"seed {seed}: the grove's trees");
                Battle gorge = Field(Biome.Hills, seed);
                int[] stones = Enumerable.Range(0, gorge.Blocked.Length).Where(c => gorge.Blocked[c]).ToArray();
                Assert.AreEqual(3, stones.Length, $"seed {seed}: five rows of the wall less two gaps");
                foreach (int stone in stones)
                {
                    Assert.That(Col(stone), Is.InRange(4, 6), $"seed {seed}");
                    Assert.That(Row(stone), Is.InRange(1, 5), $"seed {seed}: the edges stay open");
                }
            }
        }

        [Test]
        public void AnAmbushPutsTheMonstersInColumnsSixToEight()
        {
            foreach (Biome ground in Grounds)
            {
                for (int seed = 0; seed < 200; seed++)
                {
                    Battle battle = Field(ground, seed, FieldEvent.Ambush, Monster.Wolves, 40);
                    Assert.AreEqual(4, Monsters(battle).Count());
                    foreach (Fighter f in Monsters(battle))
                        Assert.That(Col(f.Cell), Is.InRange(6, 8), $"{ground}, seed {seed}");
                    Assert.AreEqual(FieldEvent.Ambush, battle.FieldEvent);
                    Assert.AreEqual(-1, battle.Chest, "no chest on an ambush's field");
                }
            }
            foreach (Fighter f in Monsters(Field(Biome.Meadow, 1, FieldEvent.None, Monster.Wolves, 40)))
                Assert.AreEqual(Battle.Width - 1, Col(f.Cell), "without an ambush at the far edge");
        }

        [Test]
        public void TheChestLiesOnAFreeCellInTheMiddleThatBothSidesReach()
        {
            foreach (Biome ground in Grounds)
            {
                for (int seed = 0; seed < 200; seed++)
                {
                    Battle battle = Field(ground, seed, FieldEvent.Loot);
                    int chest = battle.Chest;
                    Assert.That(Col(chest), Is.InRange(4, 6), $"{ground}, seed {seed}");
                    Assert.That(Row(chest), Is.InRange(1, 5), $"{ground}, seed {seed}");
                    Assert.IsTrue(battle.Free(chest));
                    Assert.IsTrue(FromLeftEdge(battle).Contains(chest), $"{ground}, seed {seed}: cut off");
                    Assert.IsFalse(battle.ChestTaken);
                }
            }
            Assert.AreEqual(-1, Field(Biome.Meadow, 1).Chest, "no chest without the event");
        }

        [Test]
        public void MonstersLeaveTheChestLying()
        {
            Battle battle = Field(Biome.Meadow, 3, FieldEvent.Loot, Monster.Goblins, 1);
            Fighter goblins = Monsters(battle).Single();
            Assert.AreSame(goblins, battle.Current, "goblins are faster than the spearmen");
            int chest = battle.Chest;
            goblins.Cell = Battle.NeighboursOf(chest).First(n => battle.Free(n));
            Assert.IsTrue(battle.Walk(chest));
            Assert.AreEqual(chest, battle.Chest);
            Assert.IsFalse(battle.ChestTaken);
            Assert.AreEqual(0, Count(battle, BattleEventKind.Chest));
        }

        [Test]
        public void APlayersStackTakesTheChestOnce()
        {
            // Skeletons are as fast as the spearmen, who act first at equal speed.
            Battle battle = Field(Biome.Meadow, 3, FieldEvent.Loot, Monster.Skeletons, 4);
            battle.ChestGold = 15;
            Fighter spears = battle.Fighters[0];
            Assert.AreSame(spears, battle.Current);
            int chest = battle.Chest;
            spears.Cell = Battle.NeighboursOf(chest).First(n => battle.Free(n));
            Assert.IsTrue(battle.Walk(chest));
            Assert.IsTrue(battle.ChestTaken);
            Assert.AreEqual(-1, battle.Chest, "gone from the field");
            BattleEvent taken = battle.Events.Single(e => e.Kind == BattleEventKind.Chest);
            Assert.AreEqual(spears.Id, taken.Actor);
            Assert.AreEqual(15, taken.Damage, "the event carries the gold to show");
            battle.AutoToEnd();
            Assert.AreEqual(1, Count(battle, BattleEventKind.Chest));
        }

        /// <summary>A 14 × 9 meadow with one kingdom, its capital at (2, 4), all of it seen.</summary>
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
            foreach (Cell c in world.Cells)
            {
                if (Hex.Distance(c.Hex, world.Cells[capital.Cell].Hex) <= 1)
                    c.Owner = 0;
            }
            var state = new GameState(world);
            for (int i = 0; i < state.Seen.Length; i++)
                state.Seen[i] = true;
            hero = state.Heroes[0];
            return state;
        }

        [Test]
        public void TheChestIsPaidOnceWhateverTheEnd()
        {
            GameState state = Meadow(out Hero hero);
            int cell = state.World.IndexOf(5, 4);
            Cell group = state.World.Cells[cell];
            group.Monster = Monster.Troll;
            group.Count = group.FirstCount = 2;
            var battle = new Battle(hero.Army, hero.Attack, hero.Defence, Monster.Troll, 2, Biome.Meadow, 9, FieldEvent.Loot)
            {
                ChestGold = 12,
            };
            Fighter first = battle.Current;
            Assert.AreEqual(Side.Players, first.Side, "trolls are as slow as the spearmen");
            first.Cell = Battle.NeighboursOf(battle.Chest).First(n => battle.Free(n));
            Assert.IsTrue(battle.Walk(battle.Chest));
            // The players run away at once: the chest still goes with them.
            battle.Retreat();
            int gold = state.Treasury[hero.Country].Gold;
            FightReport report = state.EndBattle(hero, cell, battle);
            Assert.AreNotEqual(Outcome.Won, battle.Outcome);
            Assert.AreEqual(12, report.ChestGold);
            Assert.AreEqual(gold + 12, state.Treasury[hero.Country].Gold);
            Assert.AreEqual(FieldEvent.Loot, report.FieldEvent);
        }

        [Test]
        public void TheFieldsEventIsTheSameForTheSameCellAndTurnAndAboutOneFightInFour()
        {
            GameState state = Meadow(out _);
            int ambush = 0, loot = 0, cells = state.World.Cells.Length;
            for (int cell = 0; cell < cells; cell++)
            {
                FieldEvent e = state.FieldEventOf(cell);
                Assert.AreEqual(e, state.FieldEventOf(cell));
                if (e == FieldEvent.Ambush)
                    ambush++;
                else if (e == FieldEvent.Loot)
                {
                    loot++;
                    Assert.That(state.ChestGoldOf(cell), Is.InRange(10, 20));
                }
            }
            // 126 cells: an eighth is about 16 of each.
            Assert.That(ambush, Is.InRange(6, 28));
            Assert.That(loot, Is.InRange(6, 28));
        }
    }
}

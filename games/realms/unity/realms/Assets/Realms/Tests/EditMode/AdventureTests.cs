using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    public class AdventureTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 42, 100, 777, 1234, 2026, 31337, 65535, 99991 };

        /// <summary>
        /// A 14 × 9 meadow with one kingdom: the capital (5 houses and a quarter, as in a new game) at
        /// (2, 4) and every cell within one step of it owned; the whole map is seen and the first hero
        /// stands in the capital.
        /// </summary>
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
            int quarter = world.Neighbor(capital.Cell, 1);
            world.Cells[quarter].District = 0;
            capital.Districts.Add(quarter);
            Hex home = world.Cells[capital.Cell].Hex;
            foreach (Cell c in world.Cells)
            {
                if (Hex.Distance(c.Hex, home) <= 1)
                    c.Owner = 0;
            }
            var state = new GameState(world);
            for (int i = 0; i < state.Seen.Length; i++)
                state.Seen[i] = true;
            hero = state.Heroes[0];
            return state;
        }

        static int At(GameState state, int col, int row) => state.World.IndexOf(col, row);

        static void Group(GameState state, int cell, Monster monster, int count)
        {
            Cell c = state.World.Cells[cell];
            c.Monster = monster;
            c.Count = c.FirstCount = count;
        }

        [Test]
        public void TheFirstHeroStartsInTheCapitalWithSpearmenAndArchers()
        {
            GameState state = Meadow(out Hero hero);
            Assert.AreEqual(1, state.Heroes.Count);
            Assert.AreEqual(state.World.Cities[0].Cell, hero.Cell);
            Assert.AreEqual(HeroRules.MovesPerTurn, hero.Moves);
            Assert.AreEqual(2, hero.Army.Count);
            Assert.AreEqual(Unit.Spearmen, hero.Army[0].Unit);
            Assert.AreEqual(HeroRules.StartSpearmen, hero.Army[0].Count);
            Assert.AreEqual(Unit.Archers, hero.Army[1].Unit);
            Assert.AreEqual(HeroRules.StartArchers, hero.Army[1].Count);
        }

        [Test]
        public void StepsCostRoadsBiomesAndRivers()
        {
            GameState state = Meadow(out _);
            World world = state.World;
            int from = At(state, 6, 4), east = world.Neighbor(from, 0);
            Assert.AreEqual(2, HeroRules.StepCost(world, from, 0), "meadow");
            world.Cells[east].Biome = Biome.Forest;
            Assert.AreEqual(3, HeroRules.StepCost(world, from, 0), "forest");
            world.Cells[east].Biome = Biome.Mountains;
            Assert.AreEqual(4, HeroRules.StepCost(world, from, 0), "mountains");
            world.Cells[from].Rivers = 1;
            Assert.AreEqual(6, HeroRules.StepCost(world, from, 0), "mountains across a river");
            world.Cells[from].Roads = 1;
            Assert.AreEqual(1, HeroRules.StepCost(world, from, 0), "a road with a bridge");
        }

        [Test]
        public void PathsGoAroundWaterFogAndForeignTowns()
        {
            GameState state = Meadow(out Hero hero);
            World world = state.World;
            for (int row = 1; row < world.Height; row++)
                world.Cells[At(state, 5, row)].Biome = Biome.Shallows;
            HeroPath path = state.Path(hero, At(state, 8, 4));
            Assert.IsNotNull(path);
            Assert.AreEqual(At(state, 8, 4), path.Target);
            foreach (int cell in path.Cells)
                Assert.IsTrue(world.Cells[cell].IsLand, $"through water at {world.Cells[cell].Hex}");
            Assert.That(path.Cells, Does.Contain(At(state, 5, 0)), "around the water's end");
            Assert.IsNull(state.Path(hero, At(state, 5, 4)), "into the water");
            state.Seen[At(state, 5, 0)] = false;
            Assert.IsNull(state.Path(hero, At(state, 8, 4)), "the only way lies in the fog");
            state.Seen[At(state, 5, 0)] = true;
            var other = new Country { Index = 1, Name = "B" };
            world.Countries.Add(other);
            var town = new City { Index = 1, Name = "D", Country = 1, Cell = At(state, 5, 0), Houses = 3 };
            world.Cities.Add(town);
            world.Cells[town.Cell].City = 1;
            Assert.IsNull(state.Path(hero, At(state, 8, 4)), "through another kingdom's town");
        }

        [Test]
        public void ALongPathTakesSeveralTurns()
        {
            GameState state = Meadow(out Hero hero);
            HeroPath path = state.Path(hero, At(state, 12, 4));
            Assert.AreEqual(10, path.Cells.Count);
            Assert.AreEqual(8, path.Now, "16 moves are eight meadow steps");
            Assert.AreEqual(1, path.Turns[9]);
            for (int i = 0; i < path.Now; i++)
                Assert.AreEqual(StepKind.Moved, state.Step(hero, path.Cells[i]).Kind);
            Assert.AreEqual(0, hero.Moves);
            Assert.AreEqual(StepKind.NoMoves, state.Step(hero, path.Cells[8]).Kind);
            state.EndTurn();
            Assert.AreEqual(HeroRules.MovesPerTurn, hero.Moves);
            Assert.AreEqual(StepKind.Moved, state.Step(hero, path.Cells[8]).Kind);
        }

        [Test]
        public void HeroesOpenTheFogAroundThem()
        {
            GameState state = Meadow(out Hero hero);
            for (int i = 0; i < state.Seen.Length; i++)
                state.Seen[i] = false;
            state.Seen[At(state, 3, 4)] = true;
            state.Step(hero, At(state, 3, 4));
            Assert.IsTrue(state.Seen[At(state, 6, 4)], "three steps away");
            Assert.IsFalse(state.Seen[At(state, 7, 4)], "four steps away");
        }

        [Test]
        public void ABeatenGroupLeavesItsCellAndPays()
        {
            GameState state = Meadow(out Hero hero);
            int cell = At(state, 3, 4);
            Group(state, cell, Monster.Goblins, 3);
            CollectionAssert.DoesNotContain(state.Path(hero, At(state, 4, 4)).Cells, cell, "paths go around a group");
            Assert.AreEqual(cell, state.Path(hero, cell).Target, "and end at it");
            Assert.AreEqual(StepKind.Fight, state.Step(hero, cell).Kind);
            Assert.AreEqual(state.World.Cities[0].Cell, hero.Cell, "the hero waits for the fight");
            Battle battle = state.StartBattle(hero, cell);
            battle.AutoToEnd(accept: false);
            Assert.AreEqual(Outcome.Won, battle.Outcome);
            int gold = state.Treasury[0].Gold;
            FightReport report = state.EndBattle(hero, cell, battle);
            Assert.AreEqual(Monster.None, state.World.Cells[cell].Monster);
            Assert.AreEqual(cell, hero.Cell);
            Assert.AreEqual(3, report.Killed);
            Assert.AreEqual(15, report.Experience, "the health of the goblins killed");
            Assert.AreEqual(15, hero.Experience);
            Assert.AreEqual(gold + report.Reward.Gold, state.Treasury[0].Gold);
            Assert.AreEqual(1, state.Victories);
            CollectionAssert.Contains(state.CheckQuests(), Quest.Fight);
        }

        [Test]
        public void AGroupBeatenBesideTheLandLeavesItsCellToTheKingdom()
        {
            GameState state = Meadow(out Hero hero);
            int cell = At(state, 4, 4);
            Group(state, cell, Monster.Goblins, 3);
            Assert.AreEqual(-1, state.World.Cells[cell].Owner);
            Assert.AreEqual(StepKind.Moved, state.Step(hero, At(state, 3, 4)).Kind);
            Assert.AreEqual(StepKind.Fight, state.Step(hero, cell).Kind);
            Battle battle = state.StartBattle(hero, cell);
            battle.AutoToEnd(accept: false);
            Assert.AreEqual(Outcome.Won, battle.Outcome);
            FightReport report = state.EndBattle(hero, cell, battle);
            Assert.IsTrue(report.Took);
            Assert.AreEqual(0, state.World.Cells[cell].Owner);
        }

        [Test]
        public void MonstersLetGoLeaveTheCellAndPayTheWholeReward()
        {
            GameState state = Meadow(out Hero hero);
            hero.Army.Clear();
            hero.Army.Add(new Stack(Unit.Spearmen, 16));
            hero.Army.Add(new Stack(Unit.Archers, 8));
            int cell = At(state, 3, 4);
            Group(state, cell, Monster.Goblins, 20);
            Assert.AreEqual(StepKind.Fight, state.Step(hero, cell).Kind);
            Battle battle = null;
            for (int seed = 0; seed < 50 && battle == null; seed++)
            {
                var b = new Battle(hero.Army, hero.Attack, hero.Defence, Monster.Goblins, 20, Biome.Meadow, seed);
                b.AutoToEnd();
                if (b.Surrendered)
                    battle = b;
            }
            Assert.IsNotNull(battle, "goblins gave up in one of 50 fights");
            int left = battle.MonstersLeft();
            FightReport report = state.EndBattle(hero, cell, battle);
            Assert.AreEqual(Outcome.Won, report.Outcome);
            Assert.IsTrue(report.Surrendered);
            Assert.AreEqual(20 - left, report.Killed);
            Assert.AreEqual(GameState.Reward(Monster.Goblins, 20, false), report.Reward, "the whole group's reward");
            Assert.AreEqual(Monster.None, state.World.Cells[cell].Monster, "the goblins let go are gone");
            Assert.AreEqual(cell, hero.Cell);
            Assert.AreEqual(1, state.Victories);
        }

        [Test]
        public void ALostFightSendsTheHeroHomeToTheMilitia()
        {
            GameState state = Meadow(out Hero hero);
            hero.Army.Clear();
            hero.Army.Add(new Stack(Unit.Spearmen, 1));
            state.Step(hero, At(state, 3, 4));
            int cell = At(state, 4, 4);
            Group(state, cell, Monster.Troll, 2);
            Assert.AreEqual(StepKind.Fight, state.Step(hero, cell).Kind);
            Battle battle = state.StartBattle(hero, cell);
            battle.AutoToEnd();
            Assert.AreEqual(Outcome.Lost, battle.Outcome);
            FightReport report = state.EndBattle(hero, cell, battle);
            Assert.AreEqual(state.World.Cities[0].Cell, hero.Cell);
            Assert.AreEqual(hero.Cell, report.Home);
            Assert.IsFalse(report.Retreated);
            Assert.AreEqual(HeroRules.Militia, report.Militia);
            Assert.AreEqual(1, hero.Army.Count);
            Assert.AreEqual(Unit.Spearmen, hero.Army[0].Unit);
            Assert.AreEqual(HeroRules.Militia, hero.Army[0].Count);
            Assert.AreEqual(0, hero.Moves);
            Assert.AreEqual(2, state.World.Cells[cell].Count);
            Assert.AreEqual(StepKind.NoMoves, state.Step(hero, At(state, 3, 4)).Kind);
        }

        [Test]
        public void ARetreatSendsTheHeroHomeWithTheLiving()
        {
            GameState state = Meadow(out Hero hero);
            hero.Army.Clear();
            hero.Army.Add(new Stack(Unit.Spearmen, 10));
            hero.Army.Add(new Stack(Unit.Archers, 4));
            state.Step(hero, At(state, 3, 4));
            int cell = At(state, 4, 4);
            Group(state, cell, Monster.Troll, 2);
            Assert.AreEqual(StepKind.Fight, state.Step(hero, cell).Kind);
            Battle battle = state.StartBattle(hero, cell);
            battle.Retreat();
            FightReport report = state.EndBattle(hero, cell, battle);
            Assert.IsTrue(report.Retreated);
            Assert.AreEqual(0, report.Militia, "no militia for a retreat");
            Assert.AreEqual(state.World.Cities[0].Cell, hero.Cell);
            Assert.AreEqual(new[] { 7, 3 }, hero.Army.Select(s => s.Count).ToArray());
            Assert.AreEqual(0, hero.Moves);
            Assert.AreEqual(2, state.World.Cells[cell].Count);
            Assert.AreEqual(Monster.Troll, state.World.Cells[cell].Monster);
        }

        [Test]
        public void AHarderFightPaysMoreForTheSameGroup()
        {
            GameState state = Meadow(out Hero hero);
            int cell = At(state, 3, 4);
            Group(state, cell, Monster.Goblins, 3);
            Assert.AreEqual(StepKind.Fight, state.Step(hero, cell).Kind);
            var even = new Odds(0.5f, new float[hero.Army.Count]);
            Stock reward = GameState.Reward(Monster.Goblins, 3, false);
            Assert.AreEqual(reward * 2, state.RewardOf(cell, even), "what the fight's window shows");
            Battle battle = state.StartBattle(hero, cell, even);
            battle.AutoToEnd();
            Assert.AreEqual(Outcome.Won, battle.Outcome);
            FightReport report = state.EndBattle(hero, cell, battle);
            Assert.AreEqual(2, report.RewardFactor);
            Assert.AreEqual(reward * 2, report.Reward);
            int easy = At(state, 2, 6);
            Group(state, easy, Monster.Goblins, 2);
            Assert.AreEqual(1, state.StartBattle(hero, easy).RewardFactor, "an easy fight, its odds worked out by the game");
        }

        [Test]
        public void AHeroWithoutAnArmyCannotFight()
        {
            GameState state = Meadow(out Hero hero);
            hero.Army.Clear();
            int cell = At(state, 3, 4);
            Group(state, cell, Monster.Goblins, 2);
            Assert.AreEqual(StepKind.Blocked, state.Step(hero, cell).Kind);
        }

        [Test]
        public void ChestsGiveGoldOrExperience()
        {
            GameState state = Meadow(out Hero hero);
            int a = At(state, 3, 4), b = At(state, 4, 4);
            state.World.Cells[a].Place = Place.Chest;
            state.World.Cells[b].Place = Place.Chest;
            StepResult step = state.Step(hero, a);
            Assert.AreEqual(StepKind.Visited, step.Kind);
            Assert.IsTrue(step.Chest);
            Assert.AreEqual(Place.None, state.World.Cells[a].Place);
            int xp = state.Chest.Experience;
            Assert.That(xp, Is.InRange(80, 150));
            state.TakeChest(false);
            Assert.AreEqual(xp, hero.Experience);
            Assert.IsNull(state.Chest);
            state.Step(hero, b);
            int gold = state.Treasury[0].Gold, found = state.Chest.Gold;
            Assert.That(found, Is.InRange(8, 15));
            state.TakeChest(true);
            Assert.AreEqual(gold + found, state.Treasury[0].Gold);
            Assert.AreEqual(2, state.ChestsOpened);
        }

        [Test]
        public void PilesGiveGoodsAndDisappear()
        {
            GameState state = Meadow(out Hero hero);
            int cell = At(state, 3, 4);
            state.World.Cells[cell].Place = Place.WoodPile;
            int wood = state.Treasury[0].Wood;
            StepResult step = state.Step(hero, cell);
            Assert.That(step.Gained.Wood, Is.InRange(5, 8));
            Assert.AreEqual(wood + step.Gained.Wood, state.Treasury[0].Wood);
            Assert.AreEqual(Place.None, state.World.Cells[cell].Place);
        }

        [Test]
        public void AShrineHelpsEachHeroOnce()
        {
            GameState state = Meadow(out Hero hero);
            int shrine = At(state, 3, 4);
            state.World.Cells[shrine].Place = Place.ShrineDefence;
            state.Step(hero, shrine);
            Assert.AreEqual(2, hero.Defence);
            state.Step(hero, At(state, 4, 4));
            StepResult again = state.Step(hero, shrine);
            Assert.IsTrue(again.Again);
            Assert.AreEqual(2, hero.Defence);
            Assert.AreEqual(Place.ShrineDefence, state.World.Cells[shrine].Place, "shrines stay");
        }

        [Test]
        public void AWatchtowerOpensTheFogFarAround()
        {
            GameState state = Meadow(out Hero hero);
            for (int i = 0; i < state.Seen.Length; i++)
                state.Seen[i] = false;
            int tower = At(state, 3, 4);
            state.Seen[tower] = true;
            state.World.Cells[tower].Place = Place.Watchtower;
            state.Step(hero, tower);
            Assert.IsTrue(state.Seen[At(state, 10, 4)], "seven steps away");
            Assert.IsFalse(state.Seen[At(state, 11, 4)], "eight steps away");
        }

        [Test]
        public void LevelsNeedMoreExperienceEachTime()
        {
            Assert.AreEqual(100, HeroRules.NextLevel(1));
            Assert.AreEqual(300, HeroRules.NextLevel(2));
            Assert.AreEqual(600, HeroRules.NextLevel(3));
            Assert.AreEqual(1000, HeroRules.NextLevel(4));
            var hero = new Hero();
            Assert.AreEqual(2, GameState.Gain(hero, 350));
            Assert.AreEqual(3, hero.Level);
            Assert.AreEqual(2, hero.LevelUps);
            Assert.IsTrue(GameState.ChooseLevelUp(hero, true));
            Assert.IsTrue(GameState.ChooseLevelUp(hero, false));
            Assert.IsFalse(GameState.ChooseLevelUp(hero, true));
            Assert.AreEqual(2, hero.Attack);
            Assert.AreEqual(2, hero.Defence);
        }

        [Test]
        public void ArmyBuildingsFillTheirTownsPools()
        {
            GameState state = Meadow(out _);
            state.Treasury[0] = new Stock(100, 100, 100, 100);
            Assert.AreEqual(Refusal.NoBuilding, state.CanRecruit(0, 0, Unit.Spearmen, 1));
            Assert.AreEqual(Refusal.None, state.Build(0, 0, TownBuilding.Barracks));
            Assert.AreEqual(4, state.World.Cities[0].Recruits[(int)Unit.Spearmen], "the first turn's recruits");
            for (int turn = 0; turn < 5; turn++)
                state.EndTurn();
            Assert.AreEqual(12, state.World.Cities[0].Recruits[(int)Unit.Spearmen], "three turns' worth at most");
            Assert.AreEqual(Refusal.NoRecruits, state.CanRecruit(0, 0, Unit.Spearmen, 13));
        }

        [Test]
        public void HiredUnitsJoinTheHeroInTownOrWaitInTheGarrison()
        {
            GameState state = Meadow(out Hero hero);
            state.Treasury[0] = new Stock(100, 100, 100, 100);
            state.Build(0, 0, TownBuilding.Barracks);
            Assert.AreEqual(Refusal.None, state.Recruit(0, 0, Unit.Spearmen, 2));
            Assert.AreEqual(HeroRules.StartSpearmen + 2, hero.Army[0].Count);
            Assert.AreEqual(new Stock(92, 98, 94, 98), state.Treasury[0], "4 gold and 1 food each, after the barracks");
            state.Step(hero, At(state, 3, 4));
            Assert.AreEqual(Refusal.None, state.Recruit(0, 0, Unit.Spearmen, 2));
            Assert.AreEqual(1, state.World.Cities[0].Garrison.Count);
            Assert.AreEqual(Refusal.NoRecruits, state.Recruit(0, 0, Unit.Spearmen, 1), "four a turn");
            StepResult back = state.Step(hero, state.World.Cities[0].Cell);
            Assert.IsTrue(back.Garrison);
            Assert.AreEqual(HeroRules.StartSpearmen + 4, hero.Army[0].Count);
            Assert.IsEmpty(state.World.Cities[0].Garrison);
        }

        [Test]
        public void TavernsHireUpToThreeHeroes()
        {
            GameState state = Meadow(out Hero first);
            state.Treasury[0] = new Stock(100, 100, 100, 100);
            Assert.AreEqual(Refusal.NoTavern, state.HireHero(0, 0));
            state.Build(0, 0, TownBuilding.Tavern);
            Assert.AreEqual(Refusal.Occupied, state.HireHero(0, 0), "the first hero stands in the town");
            state.Step(first, At(state, 3, 4));
            Assert.AreEqual(Refusal.None, state.HireHero(0, 0));
            Hero second = state.Heroes[1];
            Assert.AreEqual(state.World.Cities[0].Cell, second.Cell);
            Assert.AreEqual(HeroRules.HireSpearmen, second.Army[0].Count);
            Assert.AreNotEqual(first.Name, second.Name);
            Assert.AreEqual(100 - 10 - HeroRules.HireGold, state.Treasury[0].Gold);
            state.Step(second, At(state, 2, 3));
            Assert.AreEqual(Refusal.None, state.HireHero(0, 0));
            state.Step(state.Heroes[2], At(state, 1, 4));
            Assert.AreEqual(Refusal.TooManyHeroes, state.HireHero(0, 0));
        }

        [Test]
        public void HeroesDoNotShareACellButMeetFromTheNextOne()
        {
            GameState state = Meadow(out Hero first);
            state.Treasury[0] = new Stock(100, 100, 100, 100);
            state.Build(0, 0, TownBuilding.Tavern);
            state.Step(first, At(state, 3, 4));
            state.HireHero(0, 0);
            Hero second = state.Heroes[1];
            int moves = second.Moves;
            Assert.AreEqual(StepKind.Meet, state.Step(second, At(state, 3, 4)).Kind);
            Assert.AreEqual(state.World.Cities[0].Cell, second.Cell, "meeting is no step");
            Assert.AreEqual(moves, second.Moves);
            CollectionAssert.AreEqual(new[] { first }, state.Neighbours(second));
            Assert.AreSame(state.World.Cities[0], state.TownOf(second));
            Assert.IsNull(state.TownOf(first));

            // A way can end at a fellow hero (to meet him), never pass through him.
            HeroPath way = state.Path(second, At(state, 3, 4));
            Assert.IsNotNull(way);
            Assert.AreEqual(1, way.Cells.Count);
            HeroPath past = state.Path(second, At(state, 4, 4));
            Assert.IsFalse(past.Cells.Contains(At(state, 3, 4)));
        }

        [Test]
        public void HeroesExchangeUnits()
        {
            var a = new List<Stack> { new Stack(Unit.Spearmen, 10), new Stack(Unit.Archers, 4) };
            var b = new List<Stack> { new Stack(Unit.Riders, 2) };
            Assert.AreEqual(3, Armies.Move(a, b, Unit.Spearmen, 3));
            Assert.AreEqual(7, Armies.Count(a, Unit.Spearmen));
            Assert.AreEqual(3, Armies.Count(b, Unit.Spearmen));
            Assert.AreEqual(4, Armies.Move(a, b, Unit.Archers, 99), "no more than there are");
            Assert.AreEqual(1, a.Count, "the emptied stack goes");
            Assert.AreEqual(0, Armies.Move(a, b, Unit.Riders, 1), "none to give");
            var full = new List<Stack>();
            foreach (Unit unit in new[] { Unit.Goblins, Unit.Boars, Unit.Wolves, Unit.Bandits, Unit.Troll })
                full.Add(new Stack(unit, 1));
            Assert.AreEqual(0, Armies.Move(a, full, Unit.Spearmen, 1), "no room for a sixth stack");
            Assert.AreEqual(7, Armies.Count(a, Unit.Spearmen));
        }

        [Test]
        public void ArmiesHoldFiveStacks()
        {
            var army = new List<Stack>();
            Assert.IsTrue(Armies.Add(army, Unit.Spearmen, 3));
            Assert.IsTrue(Armies.Add(army, Unit.Spearmen, 2));
            Assert.AreEqual(1, army.Count);
            Assert.AreEqual(5, army[0].Count);
            foreach (Unit unit in new[] { Unit.Archers, Unit.Riders, Unit.Goblins, Unit.Wolves })
                Assert.IsTrue(Armies.Add(army, unit, 1));
            Assert.IsFalse(Armies.Add(army, Unit.Troll, 1));
            Assert.IsTrue(Armies.Add(army, Unit.Riders, 1), "a stack of the kind takes more");
        }

        [Test]
        public void GroupsGrowEveryFourTurnsUpToTwiceTheirSize()
        {
            GameState state = Meadow(out _);
            int cell = At(state, 10, 4);
            Group(state, cell, Monster.Goblins, 12);
            for (int turn = 1; turn < 4; turn++)
                state.EndTurn();
            Assert.AreEqual(12, state.World.Cells[cell].Count, "turn 4");
            TurnReport report = state.EndTurn();
            Assert.AreEqual(14, state.World.Cells[cell].Count, "turn 5: a sixth more");
            Assert.That(report.Events.Exists(e => e.Kind == TurnEventKind.MonstersGrew));
            for (int turn = 0; turn < 100; turn++)
                state.EndTurn();
            Assert.AreEqual(24, state.World.Cells[cell].Count);
        }

        [TestCaseSource(nameof(Seeds))]
        public void TheMapHasGroupsLairsAndPlacesOnTheWildLand(int seed)
        {
            World world = WorldGen.Generate(seed);
            Assert.AreEqual(70, world.Width);
            Assert.AreEqual(42, world.Height);
            int groups = 0, lairs = 0, towers = 0, shrines = 0, chests = 0;
            foreach (Cell c in world.Cells)
            {
                if (c.Monster != Monster.None)
                {
                    groups++;
                    lairs += c.Lair ? 1 : 0;
                    Assert.IsTrue(c.IsLand && c.Owner < 0 && c.City < 0, $"group at {c.Hex}");
                    Assert.That(c.Count, Is.Positive);
                    Assert.AreEqual(c.FirstCount, c.Count);
                    if (c.Monster == Monster.Troll)
                        Assert.That(c.Count, Is.AtMost(Wilds.MaxTrolls));
                }
                if (c.Place != Place.None)
                    Assert.IsTrue(c.IsLand && c.Owner < 0 && c.City < 0 && c.Resource == Resource.None, $"{c.Place} at {c.Hex}");
                towers += c.Place == Place.Watchtower ? 1 : 0;
                shrines += c.Place == Place.ShrineAttack || c.Place == Place.ShrineDefence ? 1 : 0;
                chests += c.Place == Place.Chest ? 1 : 0;
            }
            TestContext.WriteLine($"seed {seed}: {groups} groups, {lairs} lairs, {chests} chests, {shrines} shrines, {towers} towers");
            // One group to 15 wild cells: since the kingdoms start with what their towns reach
            // (Influence.Trim), there is more wild land and so more groups (81-92 on these seeds).
            Assert.That(groups, Is.InRange(25, 100));
            Assert.That(lairs, Is.AtLeast(3));
            Assert.That(towers, Is.AtLeast(2));
            Assert.AreEqual(4, shrines);
            Assert.That(chests, Is.AtLeast(5));
        }

        [TestCaseSource(nameof(Seeds))]
        public void TheFogHidesAllButTheKingdomAndItsSurroundings(int seed)
        {
            var state = new GameState(WorldGen.Generate(seed));
            int seen = 0;
            foreach (Cell c in state.World.Cells)
            {
                if (c.Owner == state.World.Player)
                    Assert.IsTrue(state.Seen[c.Index], $"own land hidden at {c.Hex}");
                seen += state.Seen[c.Index] ? 1 : 0;
            }
            Assert.That(seen, Is.LessThan(state.World.Cells.Length / 4));
        }

        [TestCaseSource(nameof(Seeds))]
        public void TheFirstHeroCanWinAFightNearHome(int seed)
        {
            var state = new GameState(WorldGen.Generate(seed));
            Hero hero = state.Heroes[0];
            int[] depth = WorldGen.WildDepth(state.World);
            float best = 0;
            int first = 0;
            foreach (Cell c in state.World.Cells)
            {
                if (c.Monster == Monster.None || depth[c.Index] > 3)
                    continue;
                first++;
                best = System.Math.Max(best, state.OddsOf(hero, c.Index).Win);
            }
            Assert.That(first, Is.AtLeast(Wilds.FirstGroups), "groups at the edge of the wild");
            Assert.That(best, Is.AtLeast(Odds.Easy), "an easy fight among them");
        }

        [TestCaseSource(nameof(Seeds))]
        public void GroupsDeepInTheWildAreStronger(int seed)
        {
            World world = WorldGen.Generate(seed);
            int[] depth = WorldGen.WildDepth(world);
            float near = 0, far = 0;
            int nearCount = 0, farCount = 0;
            foreach (Cell c in world.Cells)
            {
                if (c.Monster == Monster.None)
                    continue;
                float worth = c.Count * Units.Worth(Units.Of(c.Monster));
                if (depth[c.Index] <= 4)
                {
                    near += worth;
                    nearCount++;
                    Assert.That(c.Monster, Is.EqualTo(Monster.Goblins).Or.EqualTo(Monster.Boar), $"near at {c.Hex}");
                }
                else if (depth[c.Index] >= 15)
                {
                    far += worth;
                    farCount++;
                }
            }
            Assert.That(far / farCount, Is.GreaterThan(2 * near / nearCount));
        }

        [TestCaseSource(nameof(Seeds))]
        public void ThePlayersCanWalkToTheWild(int seed)
        {
            World world = WorldGen.Generate(seed);
            int[] depth = WorldGen.WildDepth(world);
            int wild = 0, reached = 0;
            foreach (Cell c in world.Cells)
            {
                if (!c.IsLand || c.Owner >= 0 || c.City >= 0)
                    continue;
                wild++;
                reached += depth[c.Index] != int.MaxValue ? 1 : 0;
                if (c.Monster != Monster.None || c.Place != Place.None)
                    Assert.AreNotEqual(int.MaxValue, depth[c.Index], $"{c.Monster} {c.Place} out of reach at {c.Hex}");
            }
            Assert.That(reached, Is.GreaterThan(wild * 9 / 10));
        }

        [Test]
        public void TheWildBeginsOneStepPastTheKingdom()
        {
            GameState state = Meadow(out _);
            int[] depth = WorldGen.WildDepth(state.World);
            Assert.AreEqual(0, depth[At(state, 3, 4)], "the kingdom");
            Assert.AreEqual(1, depth[At(state, 4, 4)]);
            Assert.AreEqual(5, depth[At(state, 8, 4)]);
            foreach (Cell c in state.World.Cells)
            {
                if (Hex.Distance(c.Hex, state.World.Cells[At(state, 2, 4)].Hex) == 2)
                    c.Owner = 0;
                if (Hex.Distance(c.Hex, state.World.Cells[At(state, 2, 4)].Hex) == 3)
                    c.Owner = 1;
            }
            depth = WorldGen.WildDepth(state.World);
            Assert.AreEqual(1, depth[At(state, 6, 4)], "past another kingdom's land");
            Assert.AreEqual(2, depth[At(state, 7, 4)]);
        }
    }
}

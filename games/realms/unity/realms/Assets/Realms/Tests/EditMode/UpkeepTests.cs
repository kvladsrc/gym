using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>The army's soft upkeep (the economy plan's 3.4, 2026-11-12).</summary>
    public class UpkeepTests
    {
        /// <summary>A meadow with the players' capital in the west and a neighbour's in the east; the players have their first hero.</summary>
        static GameState Two()
        {
            var world = new World(14, 9, 5);
            foreach (Cell c in world.Cells)
                c.Biome = Biome.Meadow;
            AddKingdom(world, 2, 4);
            AddKingdom(world, 11, 4);
            world.Player = 0;
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

        static Hero First(GameState state) => state.HeroesOf(state.World.Player)[0];

        /// <summary>Sets the hero's army to that many spearmen.</summary>
        static void Spearmen(Hero hero, int count)
        {
            hero.Army.Clear();
            hero.Army.Add(new Stack(Unit.Spearmen, count));
        }

        [Test]
        public void FifteenWarriorsEatNothingAndEachTenMoreEatOne()
        {
            Assert.AreEqual(0, Upkeep.Food(0));
            Assert.AreEqual(0, Upkeep.Food(15));
            Assert.AreEqual(1, Upkeep.Food(16));
            Assert.AreEqual(1, Upkeep.Food(25));
            Assert.AreEqual(2, Upkeep.Food(26));
            Assert.AreEqual(2, Upkeep.Food(35));
            Assert.AreEqual(3, Upkeep.Food(36));
        }

        [Test]
        public void TheFirstArmyEatsNothing()
        {
            GameState state = Two();
            Assert.AreEqual(HeroRules.StartSpearmen + HeroRules.StartArchers, Upkeep.Warriors(state, 0));
            Assert.AreEqual(0, Upkeep.Of(state, 0), "the starting army is fed for free");
            Assert.AreEqual(0, Upkeep.Of(state, 1), "a kingdom without heroes or garrisons eats nothing");
        }

        [Test]
        public void ARiderCountsAsTwoAndGarrisonsEatToo()
        {
            GameState state = Two();
            Hero hero = First(state);
            hero.Army.Clear();
            hero.Army.Add(new Stack(Unit.Spearmen, 10));
            hero.Army.Add(new Stack(Unit.Riders, 3));
            Assert.AreEqual(16, Upkeep.Warriors(state, 0));
            Assert.AreEqual(1, Upkeep.Of(state, 0));
            City capital = state.World.Cities[state.World.Countries[0].Capital];
            capital.Garrison.Add(new Stack(Unit.Archers, 10));
            Assert.AreEqual(26, Upkeep.Warriors(state, 0));
            Assert.AreEqual(2, Upkeep.Of(state, 0));
        }

        [Test]
        public void AHireSaysHowMuchMoreTheArmyEats()
        {
            GameState state = Two();
            Spearmen(First(state), 15);
            Assert.AreEqual(1, Upkeep.More(state, 0, Unit.Spearmen, 1), "the sixteenth warrior");
            Assert.AreEqual(1, Upkeep.More(state, 0, Unit.Spearmen, 10));
            Assert.AreEqual(2, Upkeep.More(state, 0, Unit.Spearmen, 11));
            Assert.AreEqual(1, Upkeep.More(state, 0, Unit.Riders, 1), "a rider is two");
            Spearmen(First(state), 16);
            Assert.AreEqual(0, Upkeep.More(state, 0, Unit.Spearmen, 9), "still one food for 16-25");
        }

        [Test]
        public void AFedArmyEatsBeforeTheTownsAndWalksAsFar()
        {
            GameState state = Two();
            Hero hero = First(state);
            Spearmen(hero, 35);
            state.Treasury[0] = new Stock(0, 50, 0, 0);
            TurnReport report = state.EndTurn();
            Assert.AreEqual(2, report.ArmyFood);
            Assert.IsFalse(report.Events.Exists(e => e.Kind == TurnEventKind.ArmyHungry));
            Assert.AreEqual(HeroRules.MovesPerTurn, hero.Moves);
            Assert.AreEqual(35, hero.Army[0].Count, "nobody leaves");
        }

        [Test]
        public void AHungryArmyWalksHalfTheNextTurnAndLosesNobody()
        {
            GameState state = Two();
            Hero hero = First(state);
            int income = Economy.Income(state.World, 0).Food;
            // An army that wants five more than the harvest, with empty stores.
            int warriors = Upkeep.Free + (income + 5) * Upkeep.PerFood;
            Spearmen(hero, warriors);
            state.Treasury[0] = new Stock(0, 0, 0, 0);
            TurnReport report = state.EndTurn();
            Assert.AreEqual(income, report.ArmyFood, "it ate the whole harvest");
            Assert.AreEqual(0, report.GrowthFood, "nothing was left for the towns");
            TurnEvent hunger = report.Events.Find(e => e.Kind == TurnEventKind.ArmyHungry);
            Assert.AreEqual(TurnEventKind.ArmyHungry, hunger.Kind);
            Assert.AreEqual(5, hunger.Amount, "five food short");
            Assert.AreEqual(HeroRules.HungryMoves, hero.Moves);
            Assert.AreEqual(warriors, hero.Army[0].Count, "nobody dies or leaves");
            Assert.AreEqual(0, state.Treasury[0].Food, "the stores do not go below nothing");

            // Fed again, it walks the whole way.
            state.Treasury[0] = new Stock(0, 100, 0, 0);
            report = state.EndTurn();
            Assert.IsFalse(report.Events.Exists(e => e.Kind == TurnEventKind.ArmyHungry));
            Assert.AreEqual(HeroRules.MovesPerTurn, hero.Moves);
        }
    }
}

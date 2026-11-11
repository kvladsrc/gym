using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    public class SettlersTests
    {
        /// <summary>
        /// A 14 × 9 meadow with one kingdom: the capital at (2, 4), every cell within one step of it
        /// owned, the whole map seen and the first hero in the capital with plenty of goods.
        /// </summary>
        static GameState Meadow(out Hero hero)
        {
            var world = new World(14, 9, 5);
            foreach (Cell c in world.Cells)
                c.Biome = Biome.Meadow;
            world.Countries.Add(new Country { Index = 0, Name = "A" });
            City capital = world.AddCity(0, world.IndexOf(2, 4), true, 5, "C");
            world.Countries[0].Capital = capital.Index;
            Hex home = world.Cells[capital.Cell].Hex;
            foreach (Cell c in world.Cells)
            {
                if (Hex.Distance(c.Hex, home) <= 1)
                    c.Owner = 0;
            }
            var state = new GameState(world);
            for (int i = 0; i < state.Seen.Length; i++)
                state.Seen[i] = true;
            state.Treasury[0] = new Stock(100, 100, 0, 0);
            hero = state.Heroes[0];
            return state;
        }

        static int At(GameState state, int col, int row) => state.World.IndexOf(col, row);

        [Test]
        public void EveryTownMakesTheNextSettlersDearer()
        {
            GameState state = Meadow(out _);
            World world = state.World;
            Assert.AreEqual(new Stock(5, 8, 0, 0), Settling.Cost(world, 0));
            world.AddCity(0, At(state, 9, 4), false, 2, "D");
            Assert.AreEqual(new Stock(10, 16, 0, 0), Settling.Cost(world, 0));
        }

        [Test]
        public void TheCartJoinsTheHeroInTheTownElseWaitsThere()
        {
            GameState state = Meadow(out Hero hero);
            City capital = state.World.Cities[0];
            Assert.AreEqual(Refusal.None, state.GatherSettlers(0, 0));
            Assert.IsTrue(hero.Settlers);
            Assert.IsFalse(capital.Settlers);
            Assert.AreEqual(new Stock(95, 92, 0, 0), state.Treasury[0]);

            Assert.AreEqual(Refusal.None, state.GatherSettlers(0, 0), "the hero leads one cart already");
            Assert.IsTrue(capital.Settlers);
            Assert.AreEqual(Refusal.SettlersWaiting, state.GatherSettlers(0, 0));

            state.Treasury[0] = new Stock(4, 100, 0, 0);
            capital.Settlers = false;
            Assert.AreEqual(Refusal.NotEnough, state.CanGatherSettlers(0, 0));
        }

        [Test]
        public void AHeroComingIntoTheTownTakesTheWaitingCart()
        {
            GameState state = Meadow(out Hero hero);
            City capital = state.World.Cities[0];
            int east = At(state, 3, 4);
            Assert.AreEqual(StepKind.Moved, state.Step(hero, east).Kind);
            Assert.AreEqual(Refusal.None, state.GatherSettlers(0, 0));
            Assert.IsTrue(capital.Settlers);
            Assert.IsFalse(hero.Settlers);

            StepResult back = state.Step(hero, capital.Cell);
            Assert.AreEqual(StepKind.Moved, back.Kind);
            Assert.IsTrue(back.Settlers);
            Assert.IsTrue(hero.Settlers);
            Assert.IsFalse(capital.Settlers);
        }

        [Test]
        public void ATownNeedsSettlersAndFreeLandFarFromTowns()
        {
            GameState state = Meadow(out Hero hero);
            World world = state.World;
            hero.Cell = At(state, 7, 4);
            Assert.AreEqual(Refusal.NoSettlers, state.CanFound(hero));
            hero.Settlers = true;
            Assert.AreEqual(Refusal.None, state.CanFound(hero));

            hero.Cell = At(state, 4, 4);
            Assert.AreEqual(Refusal.TooClose, state.CanFound(hero), "two steps from the capital");
            hero.Cell = At(state, 5, 4);
            Assert.AreEqual(Refusal.None, state.CanFound(hero), "three steps");

            Cell c = world.Cells[At(state, 7, 4)];
            hero.Cell = c.Index;
            c.Biome = Biome.Mountains;
            Assert.AreEqual(Refusal.BadGround, state.CanFound(hero));
            c.Biome = Biome.Hills;
            Assert.AreEqual(Refusal.None, state.CanFound(hero));
            c.Resource = Resource.Wheat;
            Assert.AreEqual(Refusal.Taken, state.CanFound(hero));
            c.Resource = Resource.None;
            c.Place = Place.ShrineAttack;
            Assert.AreEqual(Refusal.Taken, state.CanFound(hero));
            c.Place = Place.None;
            world.Countries.Add(new Country { Index = 1, Name = "B" });
            c.Owner = 1;
            Assert.AreEqual(Refusal.NotYours, state.CanFound(hero));
            c.Owner = 0;
            Assert.AreEqual(Refusal.None, state.CanFound(hero), "the kingdom's own land");
        }

        [Test]
        public void ANewVillageTakesTheWildCellsAroundIt()
        {
            GameState state = Meadow(out Hero hero);
            World world = state.World;
            int at = At(state, 8, 4);
            int[] around = new int[6];
            for (int d = 0; d < 6; d++)
                around[d] = world.Neighbor(at, d);
            world.Cells[around[0]].Biome = Biome.Ocean;
            world.Cells[around[1]].Biome = Biome.Shallows;
            world.Cells[around[2]].Monster = Monster.Goblins;
            world.Countries.Add(new Country { Index = 1, Name = "B" });
            world.Cells[around[3]].Owner = 1;
            hero.Cell = at;
            hero.Settlers = true;

            Assert.AreEqual(Refusal.None, state.Found(hero, "  Дедов Двор  "));
            City town = world.Cities[1];
            Assert.AreEqual("Дедов Двор", town.Name);
            Assert.AreEqual(0, town.Country);
            Assert.AreEqual(at, town.Cell);
            Assert.AreEqual(Settling.Houses, town.Houses);
            Assert.IsFalse(town.Capital);
            Assert.AreEqual(1, world.Cells[at].City);
            CollectionAssert.Contains(world.Countries[0].Cities, 1);
            Assert.IsFalse(hero.Settlers);
            Assert.AreEqual(1, state.TownsFounded);

            Assert.AreEqual(0, world.Cells[at].Owner);
            Assert.AreEqual(-1, world.Cells[around[0]].Owner, "open sea");
            Assert.AreEqual(0, world.Cells[around[1]].Owner, "shallows");
            Assert.AreEqual(-1, world.Cells[around[2]].Owner, "a monster group's cell");
            Assert.AreEqual(1, world.Cells[around[3]].Owner, "another kingdom's cell");
            Assert.AreEqual(0, world.Cells[around[4]].Owner);
            Assert.AreEqual(0, world.Cells[around[5]].Owner);

            Assert.IsTrue(Quests.Reached(state, 0, Quest.Settle));
            Assert.AreEqual(Refusal.NoSettlers, state.Found(hero, "x"));
        }

        [Test]
        public void ANamelessTownGetsAFreeName()
        {
            GameState state = Meadow(out Hero hero);
            World world = state.World;
            hero.Cell = At(state, 8, 4);
            hero.Settlers = true;
            string suggested = Settling.NewName(world);
            Assert.AreNotEqual("C", suggested);
            CollectionAssert.Contains(Names.Cities, suggested);
            Assert.AreEqual(Refusal.None, state.Found(hero, "   "));
            Assert.AreEqual(suggested, world.Cities[1].Name);
            Assert.AreNotEqual(suggested, Settling.NewName(world));
        }

        [Test]
        public void TownNamesAreTrimmedAndCutShort()
        {
            Assert.AreEqual("Ольховка", Settling.CleanName(" Ольховка ", "x"));
            Assert.AreEqual("x", Settling.CleanName(null, "x"));
            Assert.AreEqual(Settling.NameLength, Settling.CleanName(new string('а', 40), "x").Length);
        }

        [Test]
        public void AHeroHiredInATownTakesItsWaitingCart()
        {
            GameState state = Meadow(out Hero first);
            City capital = state.World.Cities[0];
            capital.Buildings.Add(TownBuilding.Tavern);
            Assert.AreEqual(StepKind.Moved, state.Step(first, At(state, 3, 4)).Kind);
            Assert.AreEqual(Refusal.None, state.GatherSettlers(0, 0));
            Assert.AreEqual(Refusal.None, state.HireHero(0, 0));
            Assert.IsTrue(state.Heroes[1].Settlers);
            Assert.IsFalse(capital.Settlers);
        }
    }
}

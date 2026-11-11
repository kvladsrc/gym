using System.Linq;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>The neighbours' troops to the lairs: the economy plan's step 4 (2026-11-11).</summary>
    public class TroopTests
    {
        static readonly int[] Seeds = { 1, 2, 3, 7, 42, 100 };

        /// <summary>
        /// A plain with the players' capital in the west, a neighbour's in the east and a boars'
        /// lair of nine four steps west of the neighbour's; the neighbour has gold for a troop and
        /// buys nothing on its own.
        /// </summary>
        static GameState Plain(out int lair)
        {
            World world = InfluenceTests.Plain(25, 11, 2);
            InfluenceTests.Town(world, 0, 3, 5, 5, true);
            InfluenceTests.Town(world, 1, 20, 5, 5, true);
            lair = world.IndexOf(16, 5);
            Cell den = world.Cells[lair];
            den.Monster = Monster.Boar;
            den.Count = den.FirstCount = 9;
            den.Lair = true;
            var state = new GameState(world) { RivalsPlay = false };
            state.Treasury[1] += new Stock(200, 50, 0, 0);
            return state;
        }

        [Test]
        public void ATroopIsSpearmenWorthAFifthMoreThanTheGroup()
        {
            foreach (Monster monster in new[] { Monster.Goblins, Monster.Boar, Monster.Wolves, Monster.Spiders, Monster.Troll })
            {
                foreach (int count in new[] { 1, 5, 12, 30 })
                {
                    int men = Troops.Men(monster, count);
                    float group = Troops.Margin * count * Units.Worth(Units.Of(monster));
                    Assert.That(men * Units.Worth(Unit.Spearmen), Is.GreaterThanOrEqualTo(group - 1e-2f), $"{count} {monster}");
                    Assert.That((men - 1) * Units.Worth(Unit.Spearmen), Is.LessThan(group), $"{count} {monster}: no more than needed");
                    Assert.AreEqual(Units.Cost(Unit.Spearmen) * men, Troops.Price(monster, count));
                }
            }
        }

        [Test]
        public void ATroopPaysStandsThreeTurnsUnderItsFlagAndClearsTheLairForItsKingdom()
        {
            GameState state = Plain(out int lair);
            World world = state.World;
            Stock before = state.Treasury[1];
            Assert.IsFalse(Troops.Send(state, 0), "the players have heroes");
            Assert.IsTrue(Troops.Send(state, 1));
            Troop troop = Troops.At(state, lair);
            Assert.IsNotNull(troop);
            Assert.AreEqual(1, troop.Country);
            Assert.AreEqual(Troops.StandTurns, troop.Turns);
            Assert.AreEqual(Troops.Men(Monster.Boar, 9), troop.Men);
            Assert.AreEqual(before - Troops.Price(Monster.Boar, 9), state.Treasury[1]);
            Assert.IsFalse(Troops.Send(state, 1), "one troop at a time");

            for (int left = Troops.StandTurns - 1; left >= 1; left--)
            {
                state.EndTurn();
                Assert.AreEqual(left, troop.Turns, "the flag counts down");
                Assert.IsTrue(world.Cells[lair].Lair);
            }
            int gold = state.Treasury[1].Gold;
            TurnReport report = state.EndTurn();
            Cell c = world.Cells[lair];
            Assert.IsFalse(c.Lair);
            Assert.AreEqual(Monster.None, c.Monster);
            Assert.IsEmpty(state.LairTroops);
            Assert.AreEqual(1, state.Lairs[1], "it counts for the hunter's gem");
            Assert.AreEqual(1, state.TroopsWon[1]);
            Assert.That(state.Treasury[1].Gold, Is.GreaterThanOrEqualTo(gold + GameState.Reward(Monster.Boar, 9, true).Gold));
            Assert.AreEqual(1, c.Owner, "the cell goes to its land");
            Assert.That(report.Changed, Does.Contain(lair));
            Assert.IsFalse(report.Events.Any(e => e.Kind == TurnEventKind.TroopCleared), "the players have not seen the lair");
        }

        [Test]
        public void WhenTheHeroIsFirstTheTroopGoesHomeWithHalfItsPrice()
        {
            GameState state = Plain(out int lair);
            Assert.IsTrue(Troops.Send(state, 1));
            Troop troop = state.LairTroops.Single();
            Stock left = state.Treasury[1];
            // The players' hero beat the lair (Adventure.EndBattle).
            Cell c = state.World.Cells[lair];
            c.Monster = Monster.None;
            c.Count = c.FirstCount = 0;
            c.Lair = false;
            state.Seen[lair] = true;
            Stock income = Economy.Income(state.World, 1);
            TurnReport report = state.EndTurn();
            Assert.IsEmpty(state.LairTroops);
            Assert.AreEqual(1, state.TroopsRecalled[1]);
            Assert.AreEqual(0, state.Lairs[1]);
            Assert.AreEqual(left.Gold + troop.Paid.Gold / 2 + income.Gold, state.Treasury[1].Gold);
            TurnEvent told = report.Events.Single(e => e.Kind == TurnEventKind.TroopRecalled);
            Assert.AreEqual(1, told.Country);
            Assert.AreEqual(troop.Paid.Gold / 2, told.Amount);
        }

        [Test]
        public void ALairUnderATroopSendsNoBands()
        {
            int Bands(bool troop)
            {
                GameState state = Plain(out _);
                while (state.Turn < Raids.FirstFor(state.World, 1) - 1)
                    state.EndTurn();
                if (troop)
                    Assert.IsTrue(Troops.Send(state, 1));
                state.EndTurn();
                return state.RaidsSent[1];
            }
            Assert.AreEqual(1, Bands(false), "the lair raids the neighbour on its turn");
            Assert.AreEqual(0, Bands(true));
        }

        [Test]
        public void ANeighbourSendsItsTroopToTheNearestLairItCanPayFor()
        {
            GameState state = Plain(out int lair);
            World world = state.World;
            int near = world.IndexOf(18, 3);
            Cell big = world.Cells[near];
            big.Monster = Monster.Troll;
            big.Count = big.FirstCount = 6;
            big.Lair = true;
            Stock trolls = Troops.Price(Monster.Troll, 6);

            // Far beyond what it saves in a few turns: it does not save for the trolls.
            state.Treasury[1] = Troops.Price(Monster.Boar, 9);
            Assert.IsFalse(Troops.CanSave(state, 1, trolls));
            Assert.AreEqual(lair, Troops.Choose(state, 1, out bool tooDear), "the trolls are nearer, but too dear");
            Assert.IsFalse(tooDear, "nothing to save for");

            // A little short: it saves for them.
            state.Treasury[1] = trolls - new Stock(1, 0, 0, 0);
            Assert.IsTrue(Troops.CanSave(state, 1, trolls));
            Assert.AreEqual(lair, Troops.Choose(state, 1, out tooDear));
            Assert.IsTrue(tooDear);

            state.Treasury[1] += new Stock(1000, 1000, 0, 0);
            Assert.AreEqual(near, Troops.Choose(state, 1, out _));
            Assert.AreEqual(-1, Troops.Choose(state, 0, out _), "the players' town is too far from both");
        }

        [Test]
        public void ALairBeyondTheNeighboursSavingsIsNoHunt()
        {
            GameState state = Plain(out int lair);
            Cell den = state.World.Cells[lair];
            den.Monster = Monster.Troll;
            den.Count = den.FirstCount = 12;
            state.Treasury[1] = new Stock(20, 5, 0, 0);
            Assert.IsFalse(Troops.Hunting(state, 1), "the trolls are past what it saves in a few turns");
            state.Treasury[1] = Troops.Price(Monster.Troll, 12);
            Assert.IsTrue(Troops.Hunting(state, 1));
        }

        [Test]
        public void TheNeighboursSendTroopsAndWinLairsWithoutDebt()
        {
            int sent = 0, won = 0;
            foreach (int seed in Seeds)
            {
                var state = new GameState(WorldGen.Generate(seed));
                World world = state.World;
                for (int turn = 0; turn < 60; turn++)
                {
                    state.EndTurn();
                    for (int k = 0; k < world.Countries.Count; k++)
                    {
                        Stock t = state.Treasury[k];
                        Assert.That(t.Gold >= 0 && t.Wood >= 0 && t.Stone >= 0, $"seed {seed} turn {state.Turn}: kingdom {k} in debt");
                    }
                    Assert.AreEqual(state.LairTroops.Count, state.LairTroops.Select(t => t.Country).Distinct().Count(), "one troop a kingdom");
                    Assert.IsFalse(state.LairTroops.Any(t => t.Country == world.Player));
                    foreach (Troop troop in state.LairTroops)
                        Assert.IsTrue(world.Cells[troop.Lair].Lair && world.Cells[troop.Lair].Monster != Monster.None);
                }
                sent += state.TroopsSent.Sum();
                won += state.TroopsWon.Sum();
            }
            Assert.That(sent, Is.GreaterThan(0));
            Assert.That(won, Is.GreaterThan(0));
        }
    }
}

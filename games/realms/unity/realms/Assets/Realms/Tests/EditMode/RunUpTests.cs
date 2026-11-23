using System.Linq;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>The run-up, «Разбег» (the combat plan of 2026-11-11, 3.3; <see cref="Ability.RunUp"/>).</summary>
    public class RunUpTests
    {
        /// <summary>Five riders at (1, 3) with a fixed roll of 4, before a big goblin stack three cells away, on a meadow without obstacles.</summary>
        static Battle Field(out Fighter riders, out Fighter goblins)
        {
            var battle = new Battle(new[] { new Stack(Unit.Riders, 5) }, 0, 0, Monster.Goblins, 1, Biome.Meadow, 1);
            for (int i = 0; i < battle.Blocked.Length; i++)
                battle.Blocked[i] = false;
            riders = battle.Fighters[0];
            goblins = battle.Fighters[1];
            riders.Stats = new UnitStats(22, 7, 6, 4, 4, 7, Ability.RunUp);
            riders.Cell = Cell(1, 3);
            goblins.Count = 100;
            goblins.Cell = Cell(4, 3);
            Assert.AreEqual(3, Battle.Distance(riders.Cell, goblins.Cell));
            Assert.AreSame(riders, battle.Current, "riders are faster");
            return battle;
        }

        static int Cell(int col, int row) => Battle.IndexOf(Hex.FromOffset(col, row));

        /// <summary>A free cell beside the target this many cells from the start.</summary>
        static int Beside(Battle battle, Fighter target, int start, int cells) =>
            Battle.NeighboursOf(target.Cell).First(n => battle.Free(n) && Battle.Distance(start, n) == cells);

        static BattleEvent Blow(Battle battle) => battle.Events.Find(e => e.Kind == BattleEventKind.Strike);

        static int Plain(Battle battle, Fighter a, Fighter t) => (int)System.MathF.Round(battle.Damage(a, t, false, 4));

        [Test]
        public void TwoCellsAwayIsNoRunUp()
        {
            Battle battle = Field(out Fighter riders, out Fighter goblins);
            int plain = Plain(battle, riders, goblins);
            Assert.IsTrue(battle.Strike(goblins, Beside(battle, goblins, riders.Cell, 2)));
            Assert.IsFalse(Blow(battle).RunUp);
            Assert.AreEqual(plain, Blow(battle).Damage);
        }

        [Test]
        public void ThreeCellsAwayIsARunUpHalfAsStrongAgain()
        {
            Battle battle = Field(out Fighter riders, out Fighter goblins);
            int plain = Plain(battle, riders, goblins);
            int runUp = (int)System.MathF.Round(Battle.RunUpFactor * battle.Damage(riders, goblins, false, 4));
            int from = Beside(battle, goblins, riders.Cell, 3);
            Assert.IsTrue(battle.RunUpAt(goblins, from));
            Assert.IsTrue(battle.Strike(goblins, from));
            Assert.IsTrue(Blow(battle).RunUp);
            Assert.AreEqual(runUp, Blow(battle).Damage);
            Assert.Greater(Blow(battle).Damage, plain);
        }

        [Test]
        public void GoingRoundDoesNotCount()
        {
            // The cell two from the start, the cells between them blocked: the walk goes round,
            // three steps or more, and ends two cells from where it began.
            Battle battle = Field(out Fighter riders, out Fighter goblins);
            int from = Beside(battle, goblins, riders.Cell, 2);
            foreach (int n in Battle.NeighboursOf(from).Intersect(Battle.NeighboursOf(riders.Cell)))
                battle.Blocked[n] = true;
            Assert.IsTrue(battle.Strike(goblins, from));
            BattleEvent walk = battle.Events.Find(e => e.Kind == BattleEventKind.Walk);
            Assert.That(walk.Path.Count, Is.GreaterThanOrEqualTo(3), "round the blocked cells");
            Assert.IsFalse(Blow(battle).RunUp);
        }

        [Test]
        public void StandingStillIsNoRunUp()
        {
            Battle battle = Field(out Fighter riders, out Fighter goblins);
            goblins.Cell = Battle.NeighboursOf(riders.Cell).First(n => battle.Free(n));
            Assert.IsTrue(battle.Strike(goblins, riders.Cell));
            Assert.IsFalse(Blow(battle).RunUp);
        }

        [Test]
        public void TheStrikeBackIsPlain()
        {
            Battle battle = Field(out Fighter riders, out Fighter goblins);
            Assert.IsTrue(battle.Strike(goblins, Beside(battle, goblins, riders.Cell, 3)));
            BattleEvent back = battle.Events.Find(e => e.Kind == BattleEventKind.StrikeBack);
            Assert.IsNotNull(back);
            Assert.IsFalse(back.RunUp);
        }

        /// <summary>The cover's field: ten spearmen at (2, 3), thirty archers at (3, 3), and boars with a fixed roll of 2.</summary>
        static Battle Spears(out Fighter spears, out Fighter archers, out Fighter boars)
        {
            var battle = new Battle(new[] { new Stack(Unit.Spearmen, 10), new Stack(Unit.Archers, 30) }, 0, 0, Monster.Boar, 1,
                Biome.Meadow, 1);
            for (int i = 0; i < battle.Blocked.Length; i++)
                battle.Blocked[i] = false;
            spears = battle.Fighters[0];
            archers = battle.Fighters[1];
            boars = battle.Fighters[2];
            spears.Cell = Cell(2, 3);
            archers.Cell = Cell(3, 3);
            boars.Stats = new UnitStats(8, 4, 2, 2, 2, 6, Ability.RunUp);
            boars.Count = 30;
            return battle;
        }

        /// <summary>A free cell this many cells from <paramref name="cell"/> and at least three from <paramref name="away"/>, or -1.</summary>
        static int Start(Battle battle, int cell, int away)
        {
            for (int c = 0; c < Battle.Width * Battle.Height; c++)
            {
                if (battle.Free(c) && Battle.Distance(c, cell) == Battle.RunUpCells && Battle.Distance(c, away) >= 2)
                    return c;
            }
            return -1;
        }

        [Test]
        public void TheSpearsTakeTheRunUpAway()
        {
            Battle battle = Spears(out Fighter spears, out Fighter archers, out Fighter boars);
            int covered = Battle.NeighboursOf(spears.Cell).Intersect(Battle.NeighboursOf(archers.Cell)).First();
            boars.Cell = Start(battle, covered, spears.Cell);
            Assert.GreaterOrEqual(boars.Cell, 0);
            Assert.AreSame(boars, battle.Current, "boars are faster");
            Assert.IsFalse(battle.RunUpAt(archers, covered), "the card does not promise it");
            Assert.IsTrue(battle.Strike(archers, covered));
            Assert.IsNotNull(battle.Events.Find(e => e.Kind == BattleEventKind.Cover));
            Assert.IsFalse(Blow(battle).RunUp);
        }

        [Test]
        public void BoarsRunUpWhereNoSpearsStand()
        {
            Battle battle = Spears(out Fighter spears, out Fighter archers, out Fighter boars);
            int open = Battle.NeighboursOf(archers.Cell).First(n =>
                battle.Free(n) && Battle.Distance(n, spears.Cell) >= 2);
            boars.Cell = Start(battle, open, spears.Cell);
            Assert.GreaterOrEqual(boars.Cell, 0);
            Assert.IsTrue(battle.Strike(archers, open));
            Assert.IsNull(battle.Events.Find(e => e.Kind == BattleEventKind.Cover));
            Assert.IsTrue(Blow(battle).RunUp);
        }

        [Test]
        public void TheAutoBattleTakesTheRunUp()
        {
            Battle battle = Field(out Fighter riders, out Fighter goblins);
            battle.Auto();
            Assert.IsTrue(Blow(battle).RunUp, "a cell three away rather than the nearest");
            Assert.AreEqual(3, Battle.Distance(Cell(1, 3), riders.Cell));
        }

        [Test]
        public void TheExpectedDamageKnowsTheRunUp()
        {
            Battle battle = Field(out Fighter riders, out Fighter goblins);
            float plain = battle.Expected(riders, goblins, false);
            Assert.AreEqual(plain, battle.StrikeExpected(riders, goblins, Beside(battle, goblins, riders.Cell, 2)), 1e-3f);
            Assert.AreEqual(Battle.RunUpFactor * plain, battle.StrikeExpected(riders, goblins, Beside(battle, goblins, riders.Cell, 3)),
                1e-3f);
        }
    }
}

using System.Linq;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>The spears' cover (the combat plan of 2026-11-11, 3.3; <see cref="Ability.Cover"/>).</summary>
    public class CoverTests
    {
        /// <summary>Ten spearmen at (2, 3) with thirty archers beside them at (3, 3), on a meadow without obstacles.</summary>
        static Battle Field(Monster monster, int count, out Fighter spears, out Fighter archers)
        {
            var battle = new Battle(new[] { new Stack(Unit.Spearmen, 10), new Stack(Unit.Archers, 30) }, 0, 0, monster, count,
                Biome.Meadow, 1);
            for (int i = 0; i < battle.Blocked.Length; i++)
                battle.Blocked[i] = false;
            spears = battle.Fighters[0];
            archers = battle.Fighters[1];
            spears.Cell = Cell(2, 3);
            archers.Cell = Cell(3, 3);
            return battle;
        }

        static int Cell(int col, int row) => Battle.IndexOf(Hex.FromOffset(col, row));

        static Fighter Monster(Battle battle, int k = 0) => battle.Fighters.FindAll(f => f.Side == Side.Monsters)[k];

        static int Count(Battle battle, BattleEventKind kind) => battle.Events.FindAll(e => e.Kind == kind).Count;

        /// <summary>The two cells beside both stacks.</summary>
        static int[] Between(Fighter a, Fighter b) => Battle.NeighboursOf(a.Cell).Intersect(Battle.NeighboursOf(b.Cell)).ToArray();

        [Test]
        public void TheSpearsStrikeFirstAtAnEnemyStrikingTheirNeighbourAndItStrikesWithThoseLeft()
        {
            Battle battle = Field(Core.Monster.Boar, 1, out Fighter spears, out Fighter archers);
            Fighter boars = Monster(battle);
            Assert.AreSame(boars, battle.Current, "boars are faster");
            // Fixed rolls: the spears do 3 a man, the boars 2.
            spears.Stats = new UnitStats(10, 4, 5, 3, 3, 4, Ability.Cover);
            boars.Stats = new UnitStats(8, 4, 2, 2, 2, 6);
            boars.Count = 30;
            boars.Cell = Between(spears, archers)[0];
            Assert.IsTrue(battle.Strike(archers, boars.Cell));
            BattleEvent cover = battle.Events.Find(e => e.Kind == BattleEventKind.Cover);
            BattleEvent strike = battle.Events.Find(e => e.Kind == BattleEventKind.Strike);
            Assert.IsNotNull(cover);
            Assert.AreEqual(spears.Id, cover.Actor);
            Assert.AreEqual(boars.Id, cover.Target);
            Assert.Less(battle.Events.IndexOf(cover), battle.Events.IndexOf(strike), "the spears first");
            // 10 × 3 × 1.2 = 36 of 240: 26 boars are left, and they strike 26 × 2 × 1.1.
            Assert.AreEqual(36, cover.Damage);
            Assert.AreEqual(4, cover.Killed);
            Assert.AreEqual(57, strike.Damage, "the boars left strike, not all thirty");
            Assert.AreEqual(1, Count(battle, BattleEventKind.StrikeBack), "the archers still strike back");
            Assert.IsTrue(spears.Covered);
        }

        [Test]
        public void TheSpearsCoverOnceARound()
        {
            Battle battle = Field(Core.Monster.Boar, 2, out Fighter spears, out Fighter archers);
            int[] between = Between(spears, archers);
            Monster(battle, 0).Cell = between[0];
            Monster(battle, 1).Cell = between[1];
            Monster(battle, 0).Count = Monster(battle, 1).Count = 30;
            Assert.IsTrue(battle.Strike(archers, between[0]));
            Assert.AreSame(Monster(battle, 1), battle.Current);
            Assert.IsTrue(battle.Strike(archers, between[1]));
            Assert.AreEqual(1, Count(battle, BattleEventKind.Cover), "spent on the first boars");
            Assert.AreEqual(2, Count(battle, BattleEventKind.Strike));
        }

        [Test]
        public void TheCoverComesBackEachRound()
        {
            Battle battle = Field(Core.Monster.Boar, 1, out Fighter spears, out Fighter archers);
            Fighter boars = Monster(battle);
            boars.Count = 20;
            boars.Cell = Between(spears, archers)[0];
            Assert.IsTrue(battle.Strike(archers, boars.Cell));
            while (battle.Round == 1)
                battle.Defend();
            Assert.IsFalse(spears.Covered);
            Assert.AreSame(boars, battle.Current);
            Assert.IsTrue(battle.Strike(archers, boars.Cell));
            Assert.AreEqual(2, Count(battle, BattleEventKind.Cover));
        }

        [Test]
        public void StruckThemselvesTheSpearsStrikeFirstInsteadOfBack()
        {
            Battle battle = Field(Core.Monster.Boar, 2, out Fighter spears, out Fighter archers);
            int[] between = Between(spears, archers);
            Monster(battle, 0).Cell = between[0];
            Monster(battle, 1).Cell = between[1];
            Monster(battle, 0).Count = Monster(battle, 1).Count = 30;
            spears.Count = 40;
            Assert.IsTrue(battle.Strike(spears, between[0]));
            Assert.AreEqual(1, Count(battle, BattleEventKind.Cover));
            Assert.AreEqual(0, Count(battle, BattleEventKind.StrikeBack), "the first blow took the place of striking back");
            Assert.IsTrue(battle.Strike(spears, between[1]));
            Assert.AreEqual(1, Count(battle, BattleEventKind.Cover), "the cover is spent");
            Assert.AreEqual(1, Count(battle, BattleEventKind.StrikeBack), "striking back is not");
        }

        [Test]
        public void NotFromTheFarSideOfTheNeighbour()
        {
            Battle battle = Field(Core.Monster.Boar, 1, out Fighter spears, out Fighter archers);
            Fighter boars = Monster(battle);
            boars.Count = 30;
            boars.Cell = Cell(4, 3);
            Assert.AreEqual(2, Battle.Distance(boars.Cell, spears.Cell));
            Assert.IsTrue(battle.Strike(archers, boars.Cell));
            Assert.AreEqual(0, Count(battle, BattleEventKind.Cover));
            Assert.AreEqual(1, Count(battle, BattleEventKind.StrikeBack));
            Assert.IsFalse(spears.Covered);
        }

        [Test]
        public void ShotsGoThroughTheCover()
        {
            Battle battle = Field(Core.Monster.Bandits, 1, out Fighter spears, out Fighter archers);
            Fighter bandits = Monster(battle);
            bandits.Count = 30;
            while (battle.Current.Side == Side.Players)
                battle.Defend();
            Assert.AreSame(bandits, battle.Current);
            Assert.IsTrue(battle.Shoot(archers));
            Assert.AreEqual(0, Count(battle, BattleEventKind.Cover));
            Assert.IsFalse(spears.Covered);
        }

        [Test]
        public void WolvesMeetTheSpearsToo()
        {
            Battle battle = Field(Core.Monster.Wolves, 1, out Fighter spears, out Fighter archers);
            Fighter wolves = Monster(battle);
            wolves.Count = 30;
            wolves.Cell = Between(spears, archers)[0];
            Assert.IsTrue(battle.Strike(archers, wolves.Cell));
            Assert.AreEqual(1, Count(battle, BattleEventKind.Cover));
            Assert.AreEqual(0, Count(battle, BattleEventKind.StrikeBack), "nobody strikes back at wolves");
        }

        [Test]
        public void ACoverThatKillsTheStrikerEndsItsTurn()
        {
            Battle battle = Field(Core.Monster.Boar, 1, out Fighter spears, out Fighter archers);
            Fighter boars = Monster(battle);
            boars.Count = 1;
            boars.Cell = Between(spears, archers)[0];
            Assert.IsTrue(battle.Strike(archers, boars.Cell));
            Assert.IsFalse(boars.Alive);
            Assert.AreEqual(0, Count(battle, BattleEventKind.Strike));
            Assert.AreEqual(Outcome.Won, battle.Outcome);
        }

        /// <summary>A cell two steps from the archers, one from a covered cell beside them and two or more from every uncovered one.</summary>
        static int Lure(Fighter spears, Fighter archers)
        {
            int[] covered = Between(spears, archers);
            int[] open = Battle.NeighboursOf(archers.Cell).Where(n => n != spears.Cell && !covered.Contains(n)).ToArray();
            for (int cell = 0; cell < Battle.Width * Battle.Height; cell++)
            {
                if (cell == spears.Cell || cell == archers.Cell || Battle.Distance(cell, archers.Cell) != 2 ||
                    Battle.Distance(cell, spears.Cell) < 2)
                    continue;
                if (covered.Any(c => Battle.Distance(cell, c) == 1) && open.All(o => Battle.Distance(cell, o) >= 2))
                    return cell;
            }
            Assert.Fail("no such cell");
            return -1;
        }

        [Test]
        public void WolvesLookForASideNobodyCovers()
        {
            Battle battle = Field(Core.Monster.Wolves, 1, out Fighter spears, out Fighter archers);
            Fighter wolves = Monster(battle);
            archers.Count = 5;
            wolves.Count = 4;
            wolves.Cell = Lure(spears, archers);
            Assert.AreSame(wolves, battle.Current);
            battle.Auto();
            BattleEvent strike = battle.Events.Find(e => e.Kind == BattleEventKind.Strike);
            Assert.IsNotNull(strike);
            Assert.AreEqual(archers.Id, strike.Target, "the weakest still");
            Assert.AreEqual(0, Count(battle, BattleEventKind.Cover), "from a side the spears do not reach");
            Assert.That(Battle.Distance(wolves.Cell, spears.Cell), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void BoarsRunOntoTheSpears()
        {
            Battle battle = Field(Core.Monster.Boar, 1, out Fighter spears, out Fighter archers);
            Fighter boars = Monster(battle);
            boars.Count = 30;
            boars.Cell = Lure(spears, archers);
            Assert.AreSame(boars, battle.Current);
            battle.Auto();
            Assert.AreEqual(1, Count(battle, BattleEventKind.Cover), "the nearest cell, spears or not");
        }

        [Test]
        public void WolvesLeaveACoveredWeakStackForAnOpenOne()
        {
            // Archers in a corner: every cell beside them is beside the spears too. A second, bigger
            // stack of archers stands alone.
            var battle = new Battle(new[] { new Stack(Unit.Spearmen, 10), new Stack(Unit.Archers, 4), new Stack(Unit.Archers, 6) },
                0, 0, Core.Monster.Wolves, 1, Biome.Meadow, 1);
            for (int i = 0; i < battle.Blocked.Length; i++)
                battle.Blocked[i] = false;
            Fighter spears = battle.Fighters[0], corner = battle.Fighters[1], alone = battle.Fighters[2];
            corner.Cell = Cell(0, 0);
            spears.Cell = Battle.NeighboursOf(corner.Cell).First();
            foreach (int n in Battle.NeighboursOf(corner.Cell))
                Assume.That(n == spears.Cell || Battle.Distance(n, spears.Cell) == 1, "a corner the spears cover");
            alone.Cell = Cell(1, 6);
            Fighter wolves = Monster(battle);
            wolves.Count = 3;
            wolves.Cell = Cell(5, 3);
            Assert.AreSame(wolves, battle.Current);
            battle.Auto();
            BattleEvent strike = battle.Events.Find(e => e.Kind == BattleEventKind.Strike);
            Assert.IsNotNull(strike, "wolves reach either");
            Assert.AreEqual(alone.Id, strike.Target, "not the weaker archers under the spears");
            Assert.AreEqual(0, Count(battle, BattleEventKind.Cover));
        }
    }
}

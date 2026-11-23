using System.Linq;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>
    /// The monsters' habits beside their tempers (the combat plan of 2026-11-11, 3.1): the troll
    /// tells its target, wolves circle, skeletons hold their line, and the cells a monster could
    /// strike next turn.
    /// </summary>
    public class HabitsTests
    {
        static Battle Field(Monster monster, int count, params Stack[] army)
        {
            var battle = new Battle(army, 0, 0, monster, count, Biome.Meadow, 1);
            for (int i = 0; i < battle.Blocked.Length; i++)
                battle.Blocked[i] = false;
            return battle;
        }

        static int Cell(int col, int row) => Battle.IndexOf(Hex.FromOffset(col, row));

        static Fighter Monster(Battle battle, int k = 0) => battle.Fighters.FindAll(f => f.Side == Side.Monsters)[k];

        static void DefendUntilMonsters(Battle battle)
        {
            while (battle.Current.Side == Side.Players)
                battle.Defend();
        }

        static BattleEvent Last(Battle battle, BattleEventKind kind) => battle.Events.FindLast(e => e.Kind == kind);

        [Test]
        public void TheTrollTellsTheBiggestStackAtTheStart()
        {
            Battle battle = Field(Core.Monster.Troll, 1, new Stack(Unit.Spearmen, 12), new Stack(Unit.Archers, 4));
            Assert.AreEqual(battle.Fighters[0].Id, Monster(battle).Aim, "120 health against 32");
        }

        [Test]
        public void TheTrollKeepsItsTargetUntilItFalls()
        {
            Battle battle = Field(Core.Monster.Troll, 1, new Stack(Unit.Spearmen, 12), new Stack(Unit.Archers, 4));
            Fighter spearmen = battle.Fighters[0], archers = battle.Fighters[1], troll = Monster(battle);
            archers.Count = 40; // now the biggest by far
            troll.Cell = Cell(8, 3);
            spearmen.Cell = Cell(6, 3);
            archers.Cell = Cell(7, 2);
            DefendUntilMonsters(battle);
            Assert.AreSame(troll, battle.Current);
            battle.Auto();
            Assert.AreEqual(spearmen.Id, Last(battle, BattleEventKind.Strike).Target, "the stack it told, not the biggest now");
            spearmen.Count = 0;
            battle.Defend();
            Assert.AreEqual(archers.Id, troll.Aim, "the next once the first fell");
        }

        [Test]
        public void TheTrollWalksPastAStackBesideItTowardsItsTarget()
        {
            Battle battle = Field(Core.Monster.Troll, 1, new Stack(Unit.Spearmen, 12), new Stack(Unit.Archers, 4));
            Fighter spearmen = battle.Fighters[0], archers = battle.Fighters[1], troll = Monster(battle);
            troll.Cell = Cell(10, 3);
            archers.Cell = Cell(9, 3);
            spearmen.Cell = Cell(0, 3);
            int before = Battle.Distance(troll.Cell, spearmen.Cell);
            DefendUntilMonsters(battle);
            battle.Auto();
            Assert.IsNull(Last(battle, BattleEventKind.Strike), "not the archers beside it");
            Assert.Less(Battle.Distance(troll.Cell, spearmen.Cell), before);
        }

        [Test]
        public void WolvesCircleOutOfTheFootStacksReach()
        {
            Battle battle = Field(Core.Monster.Wolves, 1, new Stack(Unit.Spearmen, 10), new Stack(Unit.Archers, 4));
            Fighter spearmen = battle.Fighters[0], archers = battle.Fighters[1], wolves = Monster(battle);
            wolves.Count = 6;
            wolves.Cell = Cell(10, 3);
            spearmen.Cell = Cell(0, 3);
            archers.Cell = Cell(0, 5);
            Assert.AreSame(wolves, battle.Current, "wolves are the fastest");
            battle.Auto();
            Assert.IsNull(Last(battle, BattleEventKind.Strike), "out of reach this turn");
            Assert.Less(wolves.Cell % Battle.Width, 10, "they came closer");
            foreach (Fighter g in new[] { spearmen, archers })
                Assert.IsFalse(battle.StrikeZone(g).Contains(wolves.Cell), $"out of the {g.Unit}' reach");
        }

        [Test]
        public void WolvesGoStraightInAfterTheThirdRound()
        {
            Battle battle = Field(Core.Monster.Wolves, 1, new Stack(Unit.Spearmen, 10), new Stack(Unit.Archers, 4));
            Fighter spearmen = battle.Fighters[0], archers = battle.Fighters[1], wolves = Monster(battle);
            wolves.Count = 6;
            wolves.Cell = Cell(10, 3);
            spearmen.Cell = Cell(0, 3);
            archers.Cell = Cell(0, 5);
            while (battle.Round <= Battle.CircleRounds)
                battle.Defend();
            Assert.AreSame(wolves, battle.Current);
            battle.Auto();
            Assert.IsTrue(battle.StrikeZone(spearmen).Contains(wolves.Cell) || battle.StrikeZone(archers).Contains(wolves.Cell),
                "the full way in");
        }

        [Test]
        public void SkeletonsHoldTheirLine()
        {
            foreach (bool alone in new[] { false, true })
            {
                Battle battle = Field(Core.Monster.Skeletons, 2, new Stack(Unit.Spearmen, 10));
                Fighter spearmen = battle.Fighters[0], front = Monster(battle, 0), rear = Monster(battle, 1);
                spearmen.Cell = Cell(0, 1);
                front.Cell = Cell(7, 1);
                rear.Cell = Cell(10, 5);
                if (alone)
                    rear.Count = 0;
                DefendUntilMonsters(battle);
                Assert.AreSame(front, battle.Current);
                battle.Auto();
                int col = front.Cell % Battle.Width;
                if (alone)
                    Assert.AreEqual(3, col, "its full walk");
                else
                    Assert.AreEqual(10 - front.Stats.Speed - 1, col, "a column before where the rearmost can get this round");
            }
        }

        [Test]
        public void TheStrikeZoneIsAWalkAndAStepAway()
        {
            Battle battle = Field(Core.Monster.Troll, 1, new Stack(Unit.Spearmen, 12));
            Fighter troll = Monster(battle);
            troll.Cell = Cell(10, 3);
            battle.Fighters[0].Cell = Cell(0, 0);
            var zone = battle.StrikeZone(troll);
            Assert.IsFalse(zone.Contains(troll.Cell));
            Assert.IsTrue(zone.Contains(Cell(10 - troll.Stats.Speed - 1, 3)));
            Assert.IsFalse(zone.Contains(Cell(10 - troll.Stats.Speed - 2, 3)));
            Assert.IsTrue(Battle.NeighboursOf(troll.Cell).All(zone.Contains));
        }
    }
}

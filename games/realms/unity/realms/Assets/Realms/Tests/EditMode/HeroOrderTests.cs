using System.Linq;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>
    /// The hero's order, one a battle (the combat plan of 2026-11-11, 3.4; <see cref="Battle.GiveOrder"/>).
    /// The battles here are given one whatever <see cref="Battle.OrdersPerBattle"/> is.
    /// </summary>
    public class HeroOrderTests
    {
        static Battle Field(Monster monster, int count, params Stack[] army)
        {
            var battle = new Battle(army, 0, 0, monster, count, Biome.Meadow, 1) { OrdersLeft = 1 };
            for (int i = 0; i < battle.Blocked.Length; i++)
                battle.Blocked[i] = false;
            return battle;
        }

        /// <summary>Ten spearmen and four archers against a troll, as slow as they: the spearmen act first.</summary>
        static Battle Troll(out Fighter spearmen, out Fighter archers, out Fighter troll)
        {
            Battle battle = Field(Monster.Troll, 1, new Stack(Unit.Spearmen, 10), new Stack(Unit.Archers, 4));
            spearmen = battle.Fighters[0];
            archers = battle.Fighters[1];
            troll = battle.Fighters[2];
            Assert.AreSame(spearmen, battle.Current);
            return battle;
        }

        static int Away(Battle battle) => battle.Reachable().First(c => c != battle.Current.Cell);

        static bool Acts(BattleEvent e) =>
            e.Kind is BattleEventKind.Walk or BattleEventKind.Strike or BattleEventKind.Shoot or BattleEventKind.Defend;

        [Test]
        public void TheAttackOrderGivesTheSameStackASecondAction()
        {
            Battle battle = Troll(out Fighter spearmen, out _, out _);
            Assert.IsTrue(battle.GiveOrder(HeroOrder.Attack));
            Assert.AreSame(spearmen, battle.Current, "the order spends nothing");
            Assert.IsTrue(battle.Walk(Away(battle)));
            Assert.AreSame(spearmen, battle.Current, "once more");
            Assert.IsTrue(battle.Walk(Away(battle)));
            Assert.AreNotSame(spearmen, battle.Current, "then the next stack");
        }

        [Test]
        public void TheSecondActionIsHalfAsStrong()
        {
            Battle battle = Troll(out Fighter spearmen, out _, out Fighter troll);
            float whole = battle.Expected(spearmen, troll, false);
            Assert.IsTrue(battle.GiveOrder(HeroOrder.Attack));
            Assert.IsFalse(battle.ActsAgain(spearmen), "the first action is whole");
            Assert.AreEqual(whole, battle.Expected(spearmen, troll, false), 1e-3f);
            Assert.IsTrue(battle.Walk(Away(battle)));
            Assert.IsTrue(battle.ActsAgain(spearmen));
            Assert.AreEqual(Battle.AgainFactor * whole, battle.Expected(spearmen, troll, false), 1e-3f);
            battle.Defend();
            Assert.IsFalse(battle.ActsAgain(spearmen));
        }

        [Test]
        public void OneOrderABattle()
        {
            Battle battle = Troll(out _, out _, out _);
            Assert.IsTrue(battle.CanOrder);
            Assert.IsTrue(battle.GiveOrder(HeroOrder.Shields));
            Assert.IsFalse(battle.CanOrder);
            Assert.IsFalse(battle.GiveOrder(HeroOrder.Attack));
            battle.Defend();
            battle.Defend();
            Assert.IsFalse(battle.CanOrder, "not the next round either");
            Assert.AreEqual(1, battle.Events.Count(e => e.Kind == BattleEventKind.Order));
        }

        [Test]
        public void ShieldsDoNotSpendTheTurn()
        {
            Battle battle = Troll(out Fighter spearmen, out Fighter archers, out Fighter troll);
            float before = battle.Damage(troll, archers, false, 10);
            Assert.IsTrue(battle.GiveOrder(HeroOrder.Shields));
            Assert.AreSame(spearmen, battle.Current, "the turn goes on");
            Assert.IsTrue(spearmen.Defending && archers.Defending);
            Assert.Less(battle.Damage(troll, archers, false, 10), before);
        }

        [Test]
        public void ShieldsLastUntilEachStacksNextTurn()
        {
            Battle battle = Troll(out Fighter spearmen, out Fighter archers, out _);
            battle.GiveOrder(HeroOrder.Shields);
            Assert.IsTrue(battle.Walk(Away(battle)));
            Assert.AreSame(archers, battle.Current);
            Assert.IsFalse(archers.Defending, "its own turn came");
            Assert.IsTrue(spearmen.Defending, "until its next turn");
        }

        [Test]
        public void NoOrderUntilTheGameGivesOne()
        {
            var battle = new Battle(new[] { new Stack(Unit.Spearmen, 10) }, 0, 0, Monster.Troll, 1, Biome.Meadow, 1);
            Assert.AreEqual(Battle.OrdersPerBattle, battle.OrdersLeft);
            Assert.AreEqual(Battle.OrdersPerBattle > 0, battle.CanOrder);
        }

        [Test]
        public void MonstersGiveNoOrders()
        {
            Battle battle = Field(Monster.Wolves, 1, new Stack(Unit.Spearmen, 10));
            Assert.AreEqual(Side.Monsters, battle.Current.Side, "wolves are faster");
            Assert.IsFalse(battle.CanOrder);
            Assert.IsFalse(battle.GiveOrder(HeroOrder.Attack));
        }

        [Test]
        public void TheAutoBattleOrdersTheAttackAtTheStrongestStacksFirstBlow()
        {
            var battle = new Battle(new[] { new Stack(Unit.Spearmen, 10), new Stack(Unit.Archers, 4) }, 0, 0, Monster.Goblins, 20,
                Biome.Meadow, 3) { OrdersLeft = 1 };
            battle.AutoToEnd(false); // fighting on when the goblins offer to give up, so the second action comes
            var orders = battle.Events.Where(e => e.Kind == BattleEventKind.Order).ToList();
            Assert.AreEqual(1, orders.Count);
            BattleEvent order = orders[0];
            Assert.AreEqual(HeroOrder.Attack, order.Order);
            Assert.AreEqual(battle.Fighters[0].Id, order.Actor, "the spearmen are worth the most");
            int at = battle.Events.IndexOf(order);
            Assert.IsFalse(battle.Events.Take(at).Any(e => e.Actor == order.Actor && e.Kind == BattleEventKind.Strike),
                "their first blow");
            var after = battle.Events.Skip(at + 1).Where(Acts).ToList();
            Assert.AreEqual(BattleEventKind.Strike, after.First(e => e.Kind != BattleEventKind.Walk).Kind);
            int first = after.FindIndex(e => e.Kind == BattleEventKind.Strike);
            Assert.AreEqual(order.Actor, after.Skip(first + 1).First(e => e.Actor >= 0 && e.Kind != BattleEventKind.StrikeBack).Actor,
                "the same stack acts next");
        }
    }
}

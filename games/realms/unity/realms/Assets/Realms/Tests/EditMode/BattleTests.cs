using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    public class BattleTests
    {
        /// <summary>A fight on a meadow without obstacles.</summary>
        static Battle Field(Monster monster, int count, int attack, int defence, params Stack[] army)
        {
            var battle = new Battle(army, attack, defence, monster, count, Biome.Meadow, 1);
            for (int i = 0; i < battle.Blocked.Length; i++)
                battle.Blocked[i] = false;
            return battle;
        }

        static Battle Field(Monster monster, int count, params Stack[] army) => Field(monster, count, 0, 0, army);

        static int Cell(int col, int row) => Battle.IndexOf(Hex.FromOffset(col, row));

        static Fighter Monster(Battle battle, int k = 0) => battle.Fighters.FindAll(f => f.Side == Side.Monsters)[k];

        static int Count(Battle battle, BattleEventKind kind) => battle.Events.FindAll(e => e.Kind == kind).Count;

        [Test]
        public void ArmiesStandOnOppositeSidesAndMonstersSplitIntoStacks()
        {
            Battle battle = Field(Core.Monster.Goblins, 10, new Stack(Unit.Spearmen, 10), new Stack(Unit.Archers, 4));
            Assert.AreEqual(6, battle.Fighters.Count);
            Assert.AreEqual(Cell(0, 2), battle.Fighters[0].Cell);
            Assert.AreEqual(Cell(0, 4), battle.Fighters[1].Cell);
            var counts = new List<int>();
            for (int k = 0; k < 4; k++)
            {
                Assert.AreEqual(Battle.Width - 1, Battle.HexOf(Monster(battle, k).Cell).Col);
                counts.Add(Monster(battle, k).Count);
            }
            CollectionAssert.AreEqual(new[] { 3, 3, 2, 2 }, counts);
        }

        [Test]
        public void TheFastestActFirst()
        {
            Battle battle = Field(Core.Monster.Wolves, 2, new Stack(Unit.Spearmen, 5), new Stack(Unit.Riders, 2));
            List<Fighter> queue = battle.Queue();
            CollectionAssert.AreEqual(new[] { Unit.Wolves, Unit.Wolves, Unit.Riders, Unit.Spearmen },
                queue.ConvertAll(f => f.Unit));
            Assert.AreSame(queue[0], battle.Current);
        }

        [Test]
        public void TheNextRoundListsTheLivingFastestFirst()
        {
            Battle battle = Field(Core.Monster.Wolves, 2, new Stack(Unit.Spearmen, 5), new Stack(Unit.Riders, 2));
            Monster(battle).Count = 0;
            CollectionAssert.AreEqual(new[] { Unit.Wolves, Unit.Riders, Unit.Spearmen },
                battle.NextRound().ConvertAll(f => f.Unit));
        }

        [Test]
        public void DamageGrowsWithAttackOverDefence()
        {
            Battle battle = Field(Core.Monster.Goblins, 4, new Stack(Unit.Spearmen, 10));
            Fighter spearmen = battle.Fighters[0], goblins = Monster(battle);
            Assert.AreEqual(10 * 2 * 1.3f, battle.Damage(spearmen, goblins, false, 2), 1e-4, "attack 4 on defence 1");
            Assert.AreEqual(1 * 2 / 1.1f, battle.Damage(goblins, spearmen, false, 2), 1e-4, "attack 3 on defence 5");
            spearmen.Defending = true;
            Assert.AreEqual(1 * 2 / 1.25f, battle.Damage(goblins, spearmen, false, 2), 1e-4, "defending: defence 8");
            Battle strong = Field(Core.Monster.Goblins, 4, 30, 0, new Stack(Unit.Spearmen, 10));
            Assert.AreEqual(10 * 2 * 3f, strong.Damage(strong.Fighters[0], Monster(strong), false, 2), 1e-4,
                "at most three times");
        }

        [Test]
        public void ShotsLoseForceFarAwayAndInMelee()
        {
            Battle battle = Field(Core.Monster.Goblins, 4, new Stack(Unit.Archers, 10));
            Fighter archers = battle.Fighters[0], goblins = Monster(battle);
            goblins.Cell = Cell(5, 3);
            Assert.AreEqual(10 * 2 * 1.4f, battle.Damage(archers, goblins, true, 2), 1e-4, "five cells away");
            Assert.IsTrue(battle.CanShoot(archers));
            goblins.Cell = Cell(9, 3);
            Assert.AreEqual(10 * 2 * 1.4f / 2, battle.Damage(archers, goblins, true, 2), 1e-4, "nine cells away");
            goblins.Cell = Cell(1, 3);
            Assert.IsFalse(battle.CanShoot(archers), "an enemy beside them");
            Assert.AreEqual(10 * 2 * 1.4f / 2, battle.Damage(archers, goblins, false, 2), 1e-4, "archers in melee");
            Battle bones = Field(Core.Monster.Skeletons, 4, new Stack(Unit.Archers, 10));
            Monster(bones).Cell = Cell(5, 3);
            Assert.AreEqual(10 * 2 / 2f, bones.Damage(bones.Fighters[0], Monster(bones), true, 2), 1e-4,
                "arrows do half to skeletons");
        }

        [Test]
        public void AStackStrikesBackOnceARound()
        {
            Battle battle = Field(Core.Monster.Skeletons, 40, new Stack(Unit.Riders, 10), new Stack(Unit.Riders, 10));
            Fighter target = Monster(battle);
            target.Cell = Cell(0, 3);
            Assert.IsTrue(battle.Strike(target, battle.Fighters[0].Cell));
            Assert.AreEqual(1, Count(battle, BattleEventKind.StrikeBack));
            Assert.IsTrue(battle.Strike(target, battle.Fighters[1].Cell));
            Assert.AreEqual(1, Count(battle, BattleEventKind.StrikeBack), "already struck back this round");
            Assert.AreEqual(Side.Monsters, battle.Current.Side);
        }

        [Test]
        public void NoneStrikeBackAtWolves()
        {
            Battle battle = Field(Core.Monster.Skeletons, 40, new Stack(Unit.Wolves, 10));
            Fighter target = Monster(battle);
            target.Cell = Cell(1, 3);
            Assert.IsTrue(battle.Strike(target, battle.Fighters[0].Cell));
            Assert.AreEqual(1, Count(battle, BattleEventKind.Strike));
            Assert.AreEqual(0, Count(battle, BattleEventKind.StrikeBack));
        }

        [Test]
        public void AStrikeFromAfarWalksFirst()
        {
            Battle battle = Field(Core.Monster.Skeletons, 40, new Stack(Unit.Riders, 10));
            Fighter riders = battle.Fighters[0], target = Monster(battle);
            target.Cell = Cell(5, 3);
            Assert.IsTrue(battle.Strike(target));
            Assert.AreEqual(1, Battle.Distance(riders.Cell, target.Cell));
            Assert.AreEqual(1, Count(battle, BattleEventKind.Walk));
            Assert.AreEqual(Side.Monsters, battle.Current.Side);
        }

        [Test]
        public void AWebHoldsAStackForItsNextTurn()
        {
            Battle battle = Field(Core.Monster.Spiders, 10, new Stack(Unit.Spearmen, 5));
            Fighter spearmen = battle.Fighters[0], spider = Monster(battle);
            spider.Cell = Cell(1, 3);
            Assert.AreSame(spider, battle.Current);
            Assert.IsTrue(battle.Strike(spearmen, spider.Cell));
            Assert.AreEqual(1, Count(battle, BattleEventKind.Web));
            while (battle.Current.Side == Side.Monsters)
                battle.Defend();
            Assert.AreSame(spearmen, battle.Current);
            Assert.AreEqual(0, battle.Reach(spearmen));
            CollectionAssert.AreEqual(new[] { spearmen.Cell }, battle.Reachable());
            Assert.IsFalse(battle.Walk(Cell(0, 2)));
            Assert.AreEqual(spearmen.Cell, battle.StrikeCell(spider), "a stuck stack still strikes");
            battle.Defend();
            while (battle.Current.Side == Side.Monsters)
                battle.Defend();
            Assert.AreEqual(Units.Of(Unit.Spearmen).Speed, battle.Reach(spearmen), "free again");
        }

        [Test]
        public void TheTrollHealsEveryRound()
        {
            Battle battle = Field(Core.Monster.Troll, 1, new Stack(Unit.Spearmen, 5));
            Fighter troll = Monster(battle);
            troll.Cell = Cell(1, 3);
            Assert.IsTrue(battle.Strike(troll, battle.Fighters[0].Cell));
            Assert.That(troll.Top, Is.LessThan(60));
            Assert.AreSame(troll, battle.Current);
            battle.Defend();
            Assert.AreEqual(2, battle.Round);
            Assert.AreEqual(60, troll.Top);
            Assert.AreEqual(1, Count(battle, BattleEventKind.Heal));
        }

        [Test]
        public void TheSameSeedFightsTheSameWay()
        {
            var army = new[] { new Stack(Unit.Spearmen, 10), new Stack(Unit.Archers, 4) };
            var a = new Battle(army, 1, 1, Core.Monster.Wolves, 6, Biome.Forest, 77);
            var b = new Battle(army, 1, 1, Core.Monster.Wolves, 6, Biome.Forest, 77);
            CollectionAssert.AreEqual(a.Blocked, b.Blocked);
            a.AutoToEnd();
            b.AutoToEnd();
            Assert.AreEqual(a.Outcome, b.Outcome);
            Assert.AreEqual(a.Events.Count, b.Events.Count);
            for (int i = 0; i < a.Fighters.Count; i++)
                Assert.AreEqual(a.Fighters[i].Count, b.Fighters[i].Count);
        }

        [Test]
        public void SurvivorsGoBackToTheHerosStacks()
        {
            var army = new List<Stack> { new Stack(Unit.Spearmen, 10), new Stack(Unit.Archers, 4) };
            var battle = new Battle(army, 1, 1, Core.Monster.Goblins, 3, Biome.Meadow, 5);
            battle.AutoToEnd(accept: false);
            Assert.AreEqual(Outcome.Won, battle.Outcome);
            Assert.AreEqual(0, battle.MonstersLeft());
            Assert.AreEqual(15, battle.Experience);
            battle.Fighters[1].Count = 0;
            battle.Survivors(army);
            Assert.AreEqual(1, army.Count);
            Assert.AreEqual(battle.Fighters[0].Count, army[0].Count);
        }

        [Test]
        public void ARetreatLeadsTheLivingAwayLessAQuarter()
        {
            var army = new List<Stack> { new Stack(Unit.Spearmen, 10), new Stack(Unit.Archers, 4), new Stack(Unit.Riders, 1) };
            var battle = new Battle(army, 1, 1, Core.Monster.Troll, 3, Biome.Meadow, 5);
            battle.Retreat();
            Assert.AreEqual(Outcome.Lost, battle.Outcome);
            Assert.IsTrue(battle.Retreated);
            Assert.IsNull(battle.Current);
            battle.Survivors(army);
            Assert.AreEqual(new[] { 7, 3, 1 }, army.Select(s => s.Count).ToArray(), "a quarter of each, rounded");
            var beaten = new List<Stack> { new Stack(Unit.Spearmen, 1) };
            var lost = new Battle(beaten, 1, 1, Core.Monster.Troll, 3, Biome.Meadow, 5);
            lost.AutoToEnd();
            Assert.AreEqual(Outcome.Lost, lost.Outcome);
            Assert.IsFalse(lost.Retreated);
            lost.Survivors(beaten);
            Assert.IsEmpty(beaten);
        }

        [Test]
        public void HarderFightsPayMore()
        {
            Assert.AreEqual(1, Odds.RewardFactor(0.95f));
            Assert.AreEqual(1, Odds.RewardFactor(Odds.Easy));
            Assert.AreEqual(2, Odds.RewardFactor(0.6f));
            Assert.AreEqual(2, Odds.RewardFactor(Odds.Even));
            Assert.AreEqual(3, Odds.RewardFactor(0.2f));
            Assert.AreEqual(3, Odds.RewardFactor(0f));
        }

        [Test]
        public void OddsTellEasyFightsFromHopelessOnes()
        {
            var army = new[] { new Stack(Unit.Spearmen, 10), new Stack(Unit.Archers, 4) };
            Odds easy = Odds.Of(army, 1, 1, Core.Monster.Goblins, 4, Biome.Meadow, 1);
            Assert.That(easy.Win, Is.AtLeast(0.95f));
            Assert.AreEqual(2, easy.Losses.Length);
            Odds hopeless = Odds.Of(army, 1, 1, Core.Monster.Troll, 3, Biome.Meadow, 1);
            Assert.That(hopeless.Win, Is.AtMost(0.05f));
            Assert.That(hopeless.Losses[0], Is.GreaterThan(easy.Losses[0]));
        }

        /// <summary>The players' stacks defend until a monster's turn comes.</summary>
        static void DefendUntilMonsters(Battle battle)
        {
            while (battle.Current.Side == Side.Players)
                battle.Defend();
        }

        /// <summary>The target of the last melee strike or shot.</summary>
        static Fighter LastTarget(Battle battle)
        {
            BattleEvent e = battle.Events.FindLast(x => x.Kind == BattleEventKind.Strike || x.Kind == BattleEventKind.Shoot);
            return e == null ? null : battle.Fighters[e.Target];
        }

        [Test]
        public void GoblinsPileOntoTheStackOthersStruck()
        {
            Battle battle = Field(Core.Monster.Goblins, 2, new Stack(Unit.Spearmen, 5), new Stack(Unit.Archers, 5));
            Fighter spearmen = battle.Fighters[0], archers = battle.Fighters[1];
            Fighter first = Monster(battle), second = Monster(battle, 1);
            spearmen.Cell = Cell(3, 3);
            archers.Cell = Cell(7, 1);
            first.Cell = Cell(5, 3);
            second.Cell = Cell(8, 2);
            Assert.AreSame(first, battle.Current);
            battle.Auto();
            Assert.AreSame(spearmen, LastTarget(battle), "the first goes for the nearest");
            Assert.AreSame(second, battle.Current);
            Assert.AreEqual(1, Battle.Distance(second.Cell, archers.Cell), "the archers stand right beside it");
            battle.Auto();
            Assert.AreSame(spearmen, LastTarget(battle), "the next one piles on");
            Assert.AreEqual(1, Battle.Distance(second.Cell, spearmen.Cell));
        }

        [Test]
        public void BoarsChargeTheStackInTheirRow()
        {
            Battle battle = Field(Core.Monster.Boar, 1, new Stack(Unit.Spearmen, 5), new Stack(Unit.Archers, 5));
            Fighter spearmen = battle.Fighters[0], archers = battle.Fighters[1], boars = Monster(battle);
            boars.Cell = Cell(10, 3);
            spearmen.Cell = Cell(4, 3);
            archers.Cell = Cell(7, 1);
            Assert.Less(Battle.Distance(boars.Cell, archers.Cell), Battle.Distance(boars.Cell, spearmen.Cell));
            Assert.AreSame(boars, battle.Current);
            battle.Auto();
            Assert.AreSame(spearmen, LastTarget(battle));
        }

        [Test]
        public void WolvesGoForTheWeakest()
        {
            Battle battle = Field(Core.Monster.Wolves, 1, new Stack(Unit.Spearmen, 3), new Stack(Unit.Archers, 10));
            Fighter spearmen = battle.Fighters[0], wolves = Monster(battle);
            wolves.Cell = Cell(6, 3);
            Assert.AreSame(wolves, battle.Current);
            battle.Auto();
            Assert.AreSame(spearmen, LastTarget(battle), "30 health against the archers' 80");
        }

        [Test]
        public void BanditsShootTheNearest()
        {
            Battle battle = Field(Core.Monster.Bandits, 1, new Stack(Unit.Spearmen, 5), new Stack(Unit.Archers, 5));
            Fighter spearmen = battle.Fighters[0], archers = battle.Fighters[1], bandits = Monster(battle);
            bandits.Cell = Cell(10, 3);
            spearmen.Cell = Cell(6, 3);
            archers.Cell = Cell(1, 3);
            DefendUntilMonsters(battle);
            Assert.AreSame(bandits, battle.Current);
            battle.Auto();
            Assert.AreEqual(1, Count(battle, BattleEventKind.Shoot));
            Assert.AreSame(spearmen, LastTarget(battle));
        }

        [Test]
        public void SkeletonsLeaveTheArchersForLast()
        {
            Battle battle = Field(Core.Monster.Skeletons, 1, new Stack(Unit.Spearmen, 5), new Stack(Unit.Archers, 5));
            Fighter spearmen = battle.Fighters[0], archers = battle.Fighters[1], skeletons = Monster(battle);
            skeletons.Cell = Cell(10, 3);
            archers.Cell = Cell(8, 3);
            spearmen.Cell = Cell(7, 5);
            Assert.Less(Battle.Distance(skeletons.Cell, archers.Cell), Battle.Distance(skeletons.Cell, spearmen.Cell));
            DefendUntilMonsters(battle);
            battle.Auto();
            Assert.AreSame(spearmen, LastTarget(battle));
        }

        [Test]
        public void SpidersCatchTheFastestNotCaughtYet()
        {
            foreach (bool webbed in new[] { false, true })
            {
                Battle battle = Field(Core.Monster.Spiders, 1, new Stack(Unit.Spearmen, 5), new Stack(Unit.Riders, 2));
                Fighter spearmen = battle.Fighters[0], riders = battle.Fighters[1], spiders = Monster(battle);
                spiders.Cell = Cell(6, 3);
                riders.Cell = Cell(3, 3);
                spearmen.Cell = Cell(5, 1);
                Assert.Less(Battle.Distance(spiders.Cell, spearmen.Cell), Battle.Distance(spiders.Cell, riders.Cell));
                Assert.AreSame(riders, battle.Current, "riders are the fastest");
                riders.Webbed = webbed;
                DefendUntilMonsters(battle);
                battle.Auto();
                Assert.AreSame(webbed ? spearmen : riders, LastTarget(battle), webbed ? "riders already caught" : "riders free");
            }
        }

        [Test]
        public void TheTrollGoesForTheBiggestStack()
        {
            Battle battle = Field(Core.Monster.Troll, 1, new Stack(Unit.Spearmen, 12), new Stack(Unit.Archers, 4));
            Fighter spearmen = battle.Fighters[0], archers = battle.Fighters[1], troll = Monster(battle);
            troll.Cell = Cell(10, 3);
            spearmen.Cell = Cell(6, 3);
            archers.Cell = Cell(8, 2);
            Assert.Less(Battle.Distance(troll.Cell, archers.Cell), Battle.Distance(troll.Cell, spearmen.Cell));
            DefendUntilMonsters(battle);
            battle.Auto();
            Assert.AreSame(spearmen, LastTarget(battle));
        }

        [Test]
        public void AMonsterWithNoneInReachWalksTowardsTheOneItsTemperPicks()
        {
            Battle battle = Field(Core.Monster.Troll, 1, new Stack(Unit.Spearmen, 12), new Stack(Unit.Archers, 4));
            Fighter spearmen = battle.Fighters[0], archers = battle.Fighters[1], troll = Monster(battle);
            troll.Cell = Cell(10, 3);
            spearmen.Cell = Cell(0, 1);
            archers.Cell = Cell(5, 5);
            Assert.AreEqual(11, Battle.Distance(troll.Cell, spearmen.Cell));
            Assert.AreEqual(6, Battle.Distance(troll.Cell, archers.Cell), "the archers are nearer, but out of reach too");
            DefendUntilMonsters(battle);
            battle.Auto();
            Assert.AreEqual(1, Count(battle, BattleEventKind.Walk));
            Assert.AreEqual(0, Count(battle, BattleEventKind.Strike));
            Assert.AreEqual(11 - Units.Of(Unit.Troll).Speed, Battle.Distance(troll.Cell, spearmen.Cell));
        }

        [Test]
        public void TheHiredUnitsHaveNoTemper()
        {
            foreach (Unit unit in Units.Hired)
                Assert.AreEqual(Temper.None, Units.TemperOf(unit));
            foreach (Monster monster in new[]
                     {
                         Core.Monster.Goblins, Core.Monster.Boar, Core.Monster.Wolves, Core.Monster.Bandits,
                         Core.Monster.Skeletons, Core.Monster.Spiders, Core.Monster.Troll,
                     })
                Assert.AreNotEqual(Temper.None, Units.TemperOf(Units.Of(monster)), monster.ToString());
        }

        [Test]
        public void EveryAutoBattleEnds()
        {
            var army = new[] { new Stack(Unit.Spearmen, 12), new Stack(Unit.Archers, 6), new Stack(Unit.Riders, 3) };
            foreach (Monster monster in new[]
                     {
                         Core.Monster.Goblins, Core.Monster.Boar, Core.Monster.Wolves, Core.Monster.Bandits,
                         Core.Monster.Skeletons, Core.Monster.Spiders, Core.Monster.Troll,
                     })
            {
                foreach (Biome ground in new[]
                         { Biome.Beach, Biome.Meadow, Biome.Plains, Biome.Forest, Biome.Hills, Biome.Mountains })
                {
                    for (int seed = 0; seed < 200; seed++)
                    {
                        int count = 1 + seed % 20;
                        var battle = new Battle(army, 2, 2, monster, count, ground, seed);
                        battle.AutoToEnd();
                        Assert.AreNotEqual(Outcome.Running, battle.Outcome);
                        Assert.That(battle.Round, Is.AtMost(Battle.MaxRounds), $"{monster} ×{count} on {ground}, seed {seed}");
                    }
                }
            }
        }

        static readonly Stack[] Strong = { new Stack(Unit.Spearmen, 16), new Stack(Unit.Archers, 8), new Stack(Unit.Riders, 4) };

        /// <summary>How many of 200 fights of a strong army against the group bring an offer to give up.</summary>
        static int Offers(Monster monster, int count)
        {
            int offers = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var battle = new Battle(Strong, 2, 2, monster, count, Biome.Meadow, seed);
                battle.AutoToEnd(accept: false);
                int n = Count(battle, BattleEventKind.Surrender);
                Assert.That(n, Is.AtMost(1), $"{monster}, seed {seed}: one offer a fight at most");
                Assert.IsFalse(battle.Surrendered);
                offers += n;
            }
            return offers;
        }

        [Test]
        public void BeatenMonstersOfferToGiveUpByTheirKind()
        {
            Assert.AreEqual(0, Offers(Core.Monster.Skeletons, 12), "the dead never give up");
            int goblins = Offers(Core.Monster.Goblins, 20), troll = Offers(Core.Monster.Troll, 2);
            Assert.That(goblins, Is.InRange(70, 130), "goblins give up in about half of the fights");
            Assert.That(troll, Is.GreaterThan(0).And.LessThan(goblins / 2), "the troll seldom");
        }

        /// <summary>Plays a strong army's fight against 20 goblins until they offer to give up; null if they never do.</summary>
        static Battle UntilOffered(int seed)
        {
            var battle = new Battle(Strong, 2, 2, Core.Monster.Goblins, 20, Biome.Meadow, seed);
            while (battle.Outcome == Outcome.Running && !battle.Offered)
                battle.Auto();
            return battle.Offered ? battle : null;
        }

        [Test]
        public void LettingTheMonstersGoWinsTheFightWithThemAlive()
        {
            Battle battle = null;
            for (int seed = 0; seed < 50 && battle == null; seed++)
                battle = UntilOffered(seed);
            Assert.IsNotNull(battle, "goblins offered to give up in one of 50 fights");
            Assert.AreEqual(BattleEventKind.Surrender, battle.Events[battle.Events.Count - 1].Kind);
            Assert.AreEqual(Side.Monsters, battle.Fighters[battle.Events[battle.Events.Count - 1].Actor].Side);
            int left = battle.MonstersLeft();
            Assert.That(left, Is.GreaterThan(0));
            int experience = battle.Experience;
            battle.Accept();
            Assert.AreEqual(Outcome.Won, battle.Outcome);
            Assert.IsTrue(battle.Surrendered);
            Assert.IsFalse(battle.Offered);
            Assert.IsNull(battle.Current);
            Assert.AreEqual(left, battle.MonstersLeft(), "the monsters let go run away alive");
            Assert.AreEqual(experience, battle.Experience, "experience only for the monsters killed");
        }

        [Test]
        public void TurningTheOfferDownOrActingFightsOnWithoutAnotherOffer()
        {
            int tried = 0;
            for (int seed = 0; seed < 50 && tried < 2; seed++)
            {
                Battle battle = UntilOffered(seed);
                if (battle == null)
                    continue;
                Fighter acting = battle.Current;
                if (tried++ == 0)
                {
                    battle.FightOn();
                    Assert.AreSame(acting, battle.Current, "the same stack acts next");
                }
                else
                    battle.Auto();
                Assert.IsFalse(battle.Offered);
                battle.AutoToEnd();
                Assert.AreNotEqual(Outcome.Running, battle.Outcome);
                Assert.IsFalse(battle.Surrendered);
                Assert.AreEqual(1, Count(battle, BattleEventKind.Surrender));
            }
            Assert.AreEqual(2, tried, "goblins offered to give up in two of 50 fights");
        }

        [Test]
        public void TheQuickBattleTakesTheOffer()
        {
            int taken = 0;
            for (int seed = 0; seed < 50; seed++)
            {
                var battle = new Battle(Strong, 2, 2, Core.Monster.Goblins, 20, Biome.Meadow, seed);
                battle.AutoToEnd();
                Assert.AreNotEqual(Outcome.Running, battle.Outcome);
                if (Count(battle, BattleEventKind.Surrender) == 1)
                {
                    Assert.IsTrue(battle.Surrendered);
                    Assert.AreEqual(Outcome.Won, battle.Outcome);
                    taken++;
                }
            }
            Assert.That(taken, Is.GreaterThan(0));
        }

        [Test]
        public void EveryMonsterKindHasAChanceToGiveUpButTheDead()
        {
            foreach (Monster monster in new[]
                     {
                         Core.Monster.Goblins, Core.Monster.Boar, Core.Monster.Wolves, Core.Monster.Bandits,
                         Core.Monster.Spiders, Core.Monster.Troll,
                     })
                Assert.That(Units.YieldChance(Units.Of(monster)), Is.GreaterThan(0f).And.LessThan(1f), monster.ToString());
            Assert.AreEqual(0f, Units.YieldChance(Unit.Skeletons));
            foreach (Unit unit in new[] { Unit.Spearmen, Unit.Archers, Unit.Riders })
                Assert.AreEqual(0f, Units.YieldChance(unit));
        }
    }
}

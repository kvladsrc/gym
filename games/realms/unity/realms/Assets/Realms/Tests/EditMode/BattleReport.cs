using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    /// <summary>
    /// The measurer of the battle plan (2026-11-10): auto battles of every monster kind at four
    /// depths against three armies, on every ground where <see cref="Wilds.Kind"/> puts that kind
    /// at that depth, and of the lairs, 200 seeds each. It reports wins and losses, how long the
    /// fights last, when the first blow lands, whom the monsters strike, what the stacks spend
    /// their turns on and how many different openings the seeds give, so that the battles can be
    /// compared before and after each change of the rules. Both sides fight by the auto battle,
    /// as <see cref="Odds"/> does, and the players let go monsters that offer to give up. It
    /// asserts nothing and runs only when asked for by name:
    /// REALMS_BATTLE_REPORT=report.md dotnet test --filter Name=BattleReportTable
    /// </summary>
    public class BattleReport
    {
        static readonly Biome[] Grounds = { Biome.Meadow, Biome.Plains, Biome.Forest, Biome.Hills, Biome.Mountains };
        static readonly int[] Depths = { 1, 6, 12, 18 };
        const int Seeds = 200;

        /// <summary>Lairs' groups are this much bigger (WorldGen.TryGroup).</summary>
        const float LairLuck = 1.3f;

        sealed class Army
        {
            public string Name;
            public int Attack, Defence;
            public (Unit Unit, int Count)[] Stacks;
        }

        // The hero gains a point of attack or defence a level; these stand for levels 1, about 4
        // and about 8.
        static readonly Army[] Armies =
        {
            new Army { Name = "Start: 10 spearmen, 4 archers; hero 1/1", Attack = 1, Defence = 1,
                Stacks = new[] { (Unit.Spearmen, 10), (Unit.Archers, 4) } },
            new Army { Name = "Middle: 14 spearmen, 8 archers, 3 riders; hero 3/2", Attack = 3, Defence = 2,
                Stacks = new[] { (Unit.Spearmen, 14), (Unit.Archers, 8), (Unit.Riders, 3) } },
            new Army { Name = "Late: 20 spearmen, 12 archers, 8 riders; hero 5/4", Attack = 5, Defence = 4,
                Stacks = new[] { (Unit.Spearmen, 20), (Unit.Archers, 12), (Unit.Riders, 8) } },
        };

        static readonly Unit[] Mine = { Unit.Spearmen, Unit.Archers, Unit.Riders };

        /// <summary>What a stack did with its turn: walked only, struck, shot or defended.</summary>
        enum Turn { Walk, Strike, Shoot, Defend }

        sealed class Tally
        {
            public int Battles, Won, GaveUp, Capped, ClashBy2, Openings, Grounds, Covers, MonsterStrikes, MonsterRunUps, PlayerRunUps, Orders,
                OrderRounds;
            public double Losses;
            public readonly List<int> Rounds = new List<int>();
            public readonly int[] FirstTarget = new int[3], AllTargets = new int[3];
            public readonly int[] PlayerTurns = new int[4], MonsterTurns = new int[4];

            public void Add(Tally t)
            {
                Battles += t.Battles;
                Won += t.Won;
                GaveUp += t.GaveUp;
                Capped += t.Capped;
                ClashBy2 += t.ClashBy2;
                Covers += t.Covers;
                MonsterStrikes += t.MonsterStrikes;
                MonsterRunUps += t.MonsterRunUps;
                PlayerRunUps += t.PlayerRunUps;
                Orders += t.Orders;
                OrderRounds += t.OrderRounds;
                Losses += t.Losses;
                Rounds.AddRange(t.Rounds);
                for (int i = 0; i < 3; i++)
                {
                    FirstTarget[i] += t.FirstTarget[i];
                    AllTargets[i] += t.AllTargets[i];
                }
                for (int i = 0; i < 4; i++)
                {
                    PlayerTurns[i] += t.PlayerTurns[i];
                    MonsterTurns[i] += t.MonsterTurns[i];
                }
            }
        }

        [Test, Explicit("A report, not a check")]
        public void BattleReportTable()
        {
            var clock = Stopwatch.StartNew();
            var text = new StringBuilder();
            text.AppendLine("# Auto battles");
            text.AppendLine();
            text.AppendLine($"{Seeds} seeds on each ground where the kind stands at that depth (M meadow, P plains, " +
                "F forest, H hills, Mt mountains); groups of average luck, lairs 1.3 times bigger. " +
                "Gave up: battles the monsters ended by giving up, let go by the players. " +
                "Losses: units of the army lost, a lost battle counting as all. " +
                "Blows: melee strikes and shots of the monsters at the players' spearmen / archers / riders. " +
                "Turns: what the stacks did with a turn: walked only / struck / shot / defended. " +
                "Openings: different sequences of who did what to whom in the first two rounds, per 200 seeds. " +
                "Covers: the spears' cover (struck first) per battle, and the share of the monsters' melee strikes it met. " +
                "Run-ups: strikes with a run-up per battle, the monsters' / the players'. " +
                "Order: battles in which the players' auto battle gave the hero's order, and the mean round of it.");
            foreach (Army army in Armies)
            {
                text.AppendLine();
                text.AppendLine($"## {army.Name}");
                text.AppendLine();
                text.AppendLine("| Monsters | Depth | Ground | Count | Won | Gave up | Losses | Rounds (p10-p90; long) | Hit 50 | First melee by round 2 | " +
                    "First monster blow Sp/Ar/Ri | All monster blows Sp/Ar/Ri | Player turns W/St/Sh/D | Monster turns W/St/Sh/D | Openings | Covers (of strikes) | Run-ups M/P | Order (round) |");
                text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
                var all = new Tally();
                foreach (Monster monster in Enum.GetValues(typeof(Monster)))
                {
                    if (monster == Monster.None)
                        continue;
                    foreach (int depth in Depths)
                    {
                        Biome[] grounds = Grounds.Where(g => Stands(monster, depth, g)).ToArray();
                        if (grounds.Length > 0)
                        {
                            int count = Wilds.Size(monster, depth, 1f);
                            Tally tally = Measure(army, monster, count, grounds);
                            all.Add(tally);
                            text.AppendLine(Row(monster.ToString(), depth, grounds, count, tally));
                        }
                        if (Wilds.LairKind(depth) == monster)
                        {
                            int count = Wilds.Size(monster, depth, LairLuck);
                            Tally tally = Measure(army, monster, count, Grounds);
                            all.Add(tally);
                            text.AppendLine(Row(monster + " lair", depth, Grounds, count, tally));
                        }
                    }
                }
                text.AppendLine(Row("All rows", 0, null, 0, all));
            }
            text.AppendLine();
            text.AppendLine($"Took {clock.Elapsed.TotalSeconds:0} s.");
            string path = Environment.GetEnvironmentVariable("REALMS_BATTLE_REPORT");
            if (string.IsNullOrEmpty(path))
                TestContext.Progress.WriteLine(text.ToString());
            else
                File.WriteAllText(path, text.ToString());
        }

        /// <summary>Whether <see cref="Wilds.Kind"/> ever puts the kind on the ground at the depth.</summary>
        static bool Stands(Monster monster, int depth, Biome ground)
        {
            var rng = new Rng(depth * 31 + (int)ground);
            for (int i = 0; i < 400; i++)
            {
                if (Wilds.Kind(rng, depth, ground) == monster)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The fields of the combat plan's 3.5 (2026-11-12): the same fights on the open field, the
        /// grove and the gorge, with nothing happening, an ambush and a chest, against the old
        /// scattered obstacles; the plan wants no pattern to move the odds by more than 15 points.
        /// Each army meets every kind at one depth, 200 seeds each. It runs only when asked for:
        /// REALMS_FIELD_REPORT=fields.md dotnet test --filter Name=FieldReportTable
        /// </summary>
        [Test, Explicit("A report, not a check")]
        public void FieldReportTable()
        {
            var clock = Stopwatch.StartNew();
            var text = new StringBuilder();
            text.AppendLine("# Auto battles by field");
            text.AppendLine();
            text.AppendLine($"{Seeds} seeds a cell. Old: the scattered obstacles before the patterns, on meadow / forest / hills. " +
                "Open, Grove, Gorge: the patterns (meadow, forest, hills), nothing happening. Ambush and Loot: the same three patterns " +
                "with the event. Won %, then rounds (mean; share over 8). Chest: the share of loot fights in which the players took it.");
            Biome[] grounds = { Biome.Meadow, Biome.Forest, Biome.Hills };
            int[] depths = { 6, 12, 18 };
            bool patterns = Battle.Patterns, shelterRule = Battle.ShelterRule;
            try
            {
                for (int a = 0; a < Armies.Length; a++)
                {
                    Army army = Armies[a];
                    text.AppendLine();
                    text.AppendLine($"## {army.Name}, depth {depths[a]}");
                    text.AppendLine();
                    text.AppendLine("| Monsters | Count | Old M/F/H | Open / Grove / Gorge | With shelter | Ambush O/G/Go | Loot O/G/Go | Rounds O/G/Go | Rounds with shelter | Chest taken |");
                    text.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
                    foreach (Monster monster in Enum.GetValues(typeof(Monster)))
                    {
                        if (monster == Monster.None)
                            continue;
                        int count = Wilds.Size(monster, depths[a], 1f);
                        Battle.Patterns = false;
                        var old = grounds.Select(g => Field(army, monster, count, g, FieldEvent.None)).ToArray();
                        Battle.Patterns = true;
                        var plain = grounds.Select(g => Field(army, monster, count, g, FieldEvent.None)).ToArray();
                        var ambush = grounds.Select(g => Field(army, monster, count, g, FieldEvent.Ambush)).ToArray();
                        var loot = grounds.Select(g => Field(army, monster, count, g, FieldEvent.Loot)).ToArray();
                        Battle.ShelterRule = true;
                        var shelter = grounds.Select(g => Field(army, monster, count, g, FieldEvent.None)).ToArray();
                        Battle.ShelterRule = shelterRule;
                        string Wins((float Won, float Rounds, float Long, float Chest)[] r) => string.Join(" / ", r.Select(x => $"{x.Won:0}"));
                        string Rounds((float Won, float Rounds, float Long, float Chest)[] r) => string.Join(" / ", r.Select(x => $"{x.Rounds:0.0} ({x.Long:0}%)"));
                        text.AppendLine($"| {monster} | {count} | {Wins(old)} | {Wins(plain)} | {Wins(shelter)} | {Wins(ambush)} | {Wins(loot)} | {Rounds(plain)} | " +
                            $"{Rounds(shelter)} | {string.Join(" / ", loot.Select(x => $"{x.Chest:0}%"))} |");
                    }
                }
            }
            finally
            {
                Battle.Patterns = patterns;
                Battle.ShelterRule = shelterRule;
            }
            text.AppendLine();
            text.AppendLine($"Took {clock.Elapsed.TotalSeconds:0} s.");
            string path = Environment.GetEnvironmentVariable("REALMS_FIELD_REPORT");
            if (string.IsNullOrEmpty(path))
                TestContext.Progress.WriteLine(text.ToString());
            else
                File.WriteAllText(path, text.ToString());
        }

        /// <summary>Won %, mean rounds, % of fights over eight rounds and % of chests taken, over the seeds.</summary>
        static (float Won, float Rounds, float Long, float Chest) Field(Army army, Monster monster, int count, Biome ground, FieldEvent fieldEvent)
        {
            int won = 0, rounds = 0, longer = 0, chests = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                var stacks = army.Stacks.Select(s => new Stack(s.Unit, s.Count)).ToList();
                var battle = new Battle(stacks, army.Attack, army.Defence, monster, count, ground,
                    seed * 7919 + (int)ground * 104729 + 17, fieldEvent);
                battle.AutoToEnd();
                if (battle.Outcome == Outcome.Won)
                    won++;
                rounds += battle.Round;
                if (battle.Round > 8)
                    longer++;
                if (battle.ChestTaken)
                    chests++;
            }
            return (100f * won / Seeds, (float)rounds / Seeds, 100f * longer / Seeds, 100f * chests / Seeds);
        }

        static Tally Measure(Army army, Monster monster, int count, Biome[] grounds)
        {
            var tally = new Tally();
            foreach (Biome ground in grounds)
            {
                var openings = new HashSet<string>();
                for (int seed = 0; seed < Seeds; seed++)
                {
                    var stacks = army.Stacks.Select(s => new Stack(s.Unit, s.Count)).ToList();
                    var battle = new Battle(stacks, army.Attack, army.Defence, monster, count, ground,
                        seed * 7919 + (int)ground * 104729 + 17);
                    battle.AutoToEnd();
                    if (battle.OrderRound > 0)
                    {
                        tally.Orders++;
                        tally.OrderRounds += battle.OrderRound;
                    }
                    openings.Add(Read(battle, tally));
                }
                tally.Openings += openings.Count;
                tally.Grounds++;
            }
            return tally;
        }

        /// <summary>Adds one finished battle to the tally; returns its opening.</summary>
        static string Read(Battle battle, Tally tally)
        {
            tally.Battles++;
            if (battle.Outcome == Outcome.Won)
                tally.Won++;
            else if (battle.Round >= Battle.MaxRounds)
                tally.Capped++;
            if (battle.Surrendered)
                tally.GaveUp++;
            tally.Rounds.Add(battle.Round);
            int start = 0, left = 0;
            foreach (Fighter f in battle.Fighters)
            {
                if (f.Side != Side.Players)
                    continue;
                start += f.StartCount;
                left += battle.Outcome == Outcome.Won ? f.Count : 0;
            }
            tally.Losses += start > 0 ? 1.0 - (double)left / start : 0;

            var opening = new StringBuilder();
            int round = 0, walker = -1, clash = 0, covered = -1;
            bool monsterBlow = false;
            void Count(int actor, Turn turn)
            {
                Fighter f = battle.Fighters[actor];
                (f.Side == Side.Players ? tally.PlayerTurns : tally.MonsterTurns)[(int)turn]++;
            }
            // A monster the spears struck first and that fell before its own strike still spent its turn striking.
            void Fell()
            {
                if (covered < 0)
                    return;
                tally.MonsterStrikes++;
                Count(covered, Turn.Strike);
                if (walker == covered)
                    walker = -1;
                covered = -1;
            }
            foreach (BattleEvent e in battle.Events)
            {
                if (covered >= 0 && !(e.Kind == BattleEventKind.Strike && e.Actor == covered))
                    Fell();
                switch (e.Kind)
                {
                    case BattleEventKind.Cover:
                        tally.Covers++;
                        covered = e.Target;
                        if (round <= 2)
                            opening.Append(Short(battle, e.Actor)).Append("-cov-").Append(Short(battle, e.Target)).Append(' ');
                        break;
                    case BattleEventKind.Round:
                        if (walker >= 0)
                            Count(walker, Turn.Walk);
                        walker = -1;
                        round++;
                        break;
                    case BattleEventKind.Walk:
                        if (walker >= 0)
                            Count(walker, Turn.Walk);
                        walker = e.Actor;
                        if (round <= 2)
                            opening.Append(Short(battle, e.Actor)).Append("-w ");
                        break;
                    case BattleEventKind.Strike:
                    case BattleEventKind.Shoot:
                    case BattleEventKind.Defend:
                        if (walker >= 0 && walker != e.Actor)
                            Count(walker, Turn.Walk);
                        walker = -1;
                        Turn turn = e.Kind == BattleEventKind.Strike ? Turn.Strike
                            : e.Kind == BattleEventKind.Shoot ? Turn.Shoot : Turn.Defend;
                        Count(e.Actor, turn);
                        if (round <= 2)
                        {
                            opening.Append(Short(battle, e.Actor)).Append(turn == Turn.Strike ? "-st-" : turn == Turn.Shoot ? "-sh-" : "-d");
                            if (turn != Turn.Defend)
                                opening.Append(Short(battle, e.Target));
                            opening.Append(' ');
                        }
                        if (e.Kind == BattleEventKind.Strike && clash == 0)
                            clash = round;
                        if (e.Kind == BattleEventKind.Strike && battle.Fighters[e.Actor].Side == Side.Monsters)
                            tally.MonsterStrikes++;
                        if (e.Kind == BattleEventKind.Strike)
                            covered = -1;
                        if (e.Kind == BattleEventKind.Strike && e.RunUp)
                        {
                            if (battle.Fighters[e.Actor].Side == Side.Monsters)
                                tally.MonsterRunUps++;
                            else
                                tally.PlayerRunUps++;
                        }
                        if (turn != Turn.Defend && battle.Fighters[e.Actor].Side == Side.Monsters)
                        {
                            int k = Array.IndexOf(Mine, battle.Fighters[e.Target].Unit);
                            if (k >= 0)
                            {
                                tally.AllTargets[k]++;
                                if (!monsterBlow)
                                    tally.FirstTarget[k]++;
                            }
                            monsterBlow = true;
                        }
                        break;
                }
            }
            Fell();
            if (walker >= 0)
                Count(walker, Turn.Walk);
            if (clash > 0 && clash <= 2)
                tally.ClashBy2++;
            return opening.ToString();
        }

        static string Short(Battle battle, int fighter) => battle.Fighters[fighter].Unit.ToString().Substring(0, 2);

        static string Abbreviation(Biome ground) => ground == Biome.Mountains ? "Mt" : ground.ToString().Substring(0, 1);

        /// <summary>A battle longer than this many rounds counts as long: the fun plan wants at most 5% of them (2026-11-11).</summary>
        const int LongRounds = 8;

        static string Row(string monsters, int depth, Biome[] grounds, int count, Tally t)
        {
            t.Rounds.Sort();
            int n = t.Rounds.Count;
            string Shares(int[] values)
            {
                int sum = values.Sum();
                return sum == 0 ? "-" : string.Join("/", values.Select(v => $"{100.0 * v / sum:0}"));
            }
            string where = grounds == null ? "" : string.Join(" ", grounds.Select(Abbreviation));
            string openings = t.Grounds == 0 ? "" : $"{(double)t.Openings / t.Grounds:0}";
            return $"| {monsters} | {(depth > 0 ? depth.ToString() : "")} | {where} | {(count > 0 ? count.ToString() : "")} | " +
                $"{100.0 * t.Won / t.Battles:0}% | {100.0 * t.GaveUp / t.Battles:0}% | {100 * t.Losses / t.Battles:0}% | " +
                $"{t.Rounds.Average():0.0} ({t.Rounds[(int)(0.1 * (n - 1))]}-{t.Rounds[(int)(0.9 * (n - 1))]}; {100.0 * t.Rounds.Count(r => r > LongRounds) / n:0}% over {LongRounds}) | {t.Capped} | " +
                $"{100.0 * t.ClashBy2 / t.Battles:0}% | {Shares(t.FirstTarget)} | {Shares(t.AllTargets)} | " +
                $"{Shares(t.PlayerTurns)} | {Shares(t.MonsterTurns)} | {openings} | " +
                $"{(double)t.Covers / t.Battles:0.0} ({(t.MonsterStrikes == 0 ? 0 : 100.0 * t.Covers / t.MonsterStrikes):0}%) | " +
                $"{(double)t.MonsterRunUps / t.Battles:0.0}/{(double)t.PlayerRunUps / t.Battles:0.0} | " +
                $"{100.0 * t.Orders / t.Battles:0}% ({(t.Orders == 0 ? 0 : (double)t.OrderRounds / t.Orders):0.0}) |";
        }
    }
}

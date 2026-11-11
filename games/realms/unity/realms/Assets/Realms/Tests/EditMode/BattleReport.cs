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
            public int Battles, Won, GaveUp, Capped, ClashBy2, Openings, Grounds;
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
                "Openings: different sequences of who did what to whom in the first two rounds, per 200 seeds.");
            foreach (Army army in Armies)
            {
                text.AppendLine();
                text.AppendLine($"## {army.Name}");
                text.AppendLine();
                text.AppendLine("| Monsters | Depth | Ground | Count | Won | Gave up | Losses | Rounds (p10-p90) | Hit 50 | First melee by round 2 | " +
                    "First monster blow Sp/Ar/Ri | All monster blows Sp/Ar/Ri | Player turns W/St/Sh/D | Monster turns W/St/Sh/D | Openings |");
                text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
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
            int round = 0, walker = -1, clash = 0;
            bool monsterBlow = false;
            void Count(int actor, Turn turn)
            {
                Fighter f = battle.Fighters[actor];
                (f.Side == Side.Players ? tally.PlayerTurns : tally.MonsterTurns)[(int)turn]++;
            }
            foreach (BattleEvent e in battle.Events)
            {
                switch (e.Kind)
                {
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
            if (walker >= 0)
                Count(walker, Turn.Walk);
            if (clash > 0 && clash <= 2)
                tally.ClashBy2++;
            return opening.ToString();
        }

        static string Short(Battle battle, int fighter) => battle.Fighters[fighter].Unit.ToString().Substring(0, 2);

        static string Abbreviation(Biome ground) => ground == Biome.Mountains ? "Mt" : ground.ToString().Substring(0, 1);

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
                $"{t.Rounds.Average():0.0} ({t.Rounds[(int)(0.1 * (n - 1))]}-{t.Rounds[(int)(0.9 * (n - 1))]}) | {t.Capped} | " +
                $"{100.0 * t.ClashBy2 / t.Battles:0}% | {Shares(t.FirstTarget)} | {Shares(t.AllTargets)} | " +
                $"{Shares(t.PlayerTurns)} | {Shares(t.MonsterTurns)} | {openings} |";
        }
    }
}

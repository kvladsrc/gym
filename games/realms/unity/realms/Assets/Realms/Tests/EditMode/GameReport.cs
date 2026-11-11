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
    /// The measurer of the economy and fun plans (2026-11-11): whole games on 20 maps of 80 turns,
    /// the players' kingdom played by <see cref="Rival"/>, the other kingdoms by the game's own
    /// neighbours' turn (<see cref="GameState.EndTurn"/>) at the difficulty in
    /// REALMS_GAME_DIFFICULTY (calm, normal or race; normal by default), or idle, as before the
    /// plan's step 2, with REALMS_GAME_PASSIVE set. It reports when the towns reach their levels, the treasury and income over the game,
    /// where each good went, the turns with nothing to do, the longest stretches without anything
    /// new, why towns stopped growing, the tasks, the land, the neighbours and the monsters, so that
    /// each step of the plans can be judged against the game before it. The bot builds and grows
    /// towns onto quarters; its hero hires with what is left and fights the groups he clearly beats
    /// (<see cref="Rival.HeroTurn"/>); it founds no towns. The land of every kingdom grows by itself
    /// from its towns (<see cref="Influence"/>), measured against the territory plan's targets. With
    /// REALMS_GAME_NO_HERO set the hero stays at home, as in the reports before 2026-11-11 noon.
    /// It asserts nothing and runs only when asked for by name:
    /// REALMS_GAME_REPORT=report.md dotnet test --filter Name=GameReportTable
    /// </summary>
    public class GameReport
    {
        const int Turns = 80;

        /// <summary>Maps played: 20, or REALMS_GAME_MAPS for finer shares when the numbers are tuned.</summary>
        static readonly int Maps = int.TryParse(Environment.GetEnvironmentVariable("REALMS_GAME_MAPS"), out int maps) && maps > 0 ? maps : 20;

        /// <summary>
        /// With REALMS_GAME_RATE set, the measurer plays the other kingdoms itself at that many things
        /// a turn, just before the turn's end, as the game does at a difficulty: for tuning the
        /// difficulties' paces.
        /// </summary>
        static readonly float Rate = float.TryParse(Environment.GetEnvironmentVariable("REALMS_GAME_RATE"),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float rate) ? rate : -1f;

        static readonly bool WithHero = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("REALMS_GAME_NO_HERO"));

        static readonly bool Passive = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("REALMS_GAME_PASSIVE"));

        /// <summary>With REALMS_GAME_NO_STAKE set no bot sets a stake (<see cref="GameState.BotsStake"/>), to see what the stake gives.</summary>
        static readonly bool NoStake = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("REALMS_GAME_NO_STAKE"));

        static readonly Difficulty Level = (Environment.GetEnvironmentVariable("REALMS_GAME_DIFFICULTY") ?? "").ToLowerInvariant() switch
        {
            "calm" => Difficulty.Calm,
            "race" => Difficulty.Race,
            _ => Difficulty.Normal,
        };

        /// <summary>The turns the gap between the first and the second kingdom's gems is shown at.</summary>
        static readonly int[] GapTurns = { 20, 40, 60 };

        /// <summary>The turns the treasury is shown at: what the players see at the start of the turn.</summary>
        static readonly int[] Shown = { 1, 5, 10, 15, 20, 30, 40, 50, 60, 70, 80 };

        /// <summary>A stock bigger than this many turns of its income does nothing: a dead good.</summary>
        const int DeadTurns = 10;

        static readonly string[] Goods = { "gold", "food", "wood", "stone" };

        static int Of(Stock s, int good) => good == 0 ? s.Gold : good == 1 ? s.Food : good == 2 ? s.Wood : s.Stone;

        /// <summary>Where the goods came from and went, for one good.</summary>
        enum Flow { Income, Tasks, Spoils, FieldBuildings, TownBuildings, Quarters, Other, Army, Growth }

        sealed class Game
        {
            public int Seed, TownsAtStart, Empty, LongestEmpty, LastNew, LongestWithoutNew, Grown;
            public int CapitalLevel3, CapitalLevel4, TownLevel2, TownLevel3, TownLevel4;
            public int LandAtStart, LandAtEnd, QuestsDone;
            public readonly Dictionary<int, Stock> Treasury = new Dictionary<int, Stock>();
            public readonly Dictionary<int, Stock> Income = new Dictionary<int, Stock>();
            public readonly long[,] Flows = new long[Enum.GetValues(typeof(Flow)).Length, 4];
            public readonly Dictionary<GrowthStop, int> Stops = new Dictionary<GrowthStop, int>();
            public int TownTurns;
            public readonly Dictionary<string, int> FirstBuilt = new Dictionary<string, int>();
            public readonly Dictionary<Quest, int> QuestTurn = new Dictionary<Quest, int>();
            public readonly List<(bool Mine, Gem Gem, int Turn)> Gems = new List<(bool, Gem, int)>();
            public int Crowned, RivalCount;
            public readonly List<Gem> RivalAims = new List<Gem>();
            public int Hired, Fights, Won, Lairs, HeroIdle, HeroLevel;
            public readonly Dictionary<int, (int Level, float Army, int Fights, int Won, int Lairs)> Hero =
                new Dictionary<int, (int, float, int, int, int)>();
            public int FirstCrown = int.MaxValue;
            public readonly Dictionary<int, (double Towns, double Houses, double Land, double Built, double Gold)> Neighbours =
                new Dictionary<int, (double, double, double, double, double)>();
            /// <summary>Each other kingdom's gems with its temper's first gem and the turn each came on.</summary>
            public readonly List<(Gem Aim, Gem Gem, int Turn)> RivalGems = new List<(Gem, Gem, int)>();
            /// <summary>The first turn another kingdom set its crown (0: never); times the kingdom with the most gems changed.</summary>
            public int RivalCrown, LeaderChanges;
            /// <summary>The first kingdom's gems less the second's, at <see cref="GapTurns"/>.</summary>
            public readonly int[] Gap = new int[3];
            /// <summary>Milliseconds the turn's end took, all and the longest.</summary>
            public double EndTurnMs, EndTurnMaxMs;
            public readonly Dictionary<int, (int Groups, int Lairs, int Units)> Monsters =
                new Dictionary<int, (int, int, int)>();

            // The land of all kingdoms (the territory plan's checks). "At turn t" is what the
            // players see on turn t: index 1 is the start, index t + 1 follows turn t's end.
            public int[][] LandAt;
            public int[] WildAt;
            /// <summary>Kingdoms with wheat, wood, stone and gold inside their land at the start.</summary>
            public int AllFour;
            /// <summary>Town-turns with nowhere to grow, and all town-turns, before turn 30 and after 45.</summary>
            public int FullEarly, TownsEarly, FullLate, TownsLate;
            /// <summary>Each kingdom's first turn its capital had nowhere to grow, and its land first touched another's (0: never).</summary>
            public int[] CapitalFull, Meet;
            /// <summary>Wild land within the towns' reach at the start: shut by monsters, and all.</summary>
            public int Blocked, InReach;
            /// <summary>Wild cells on turn 30 within reach of two kingdoms, and of any.</summary>
            public int Contested, Reachable;

            // The stake (the territory plan's П3): the first turn a resource that was wild at the
            // start came inside the players' land (0: never), such resources by turns 20 and 40,
            // and the players' stakes the towns reached and the neighbours took first.
            public int FirstResource, ResourcesBy20, ResourcesBy40, StakesReached, StakesLost;

            // The raids (the fun plan's Ф3): bands sent against the players' towns, beaten off by
            // walls, beaten by the hero, town-turns lost, the first turn one came; the same for one
            // other kingdom on average.
            public int RaidsSent, RaidsRepelled, RaidsBeaten, RaidTurns, FirstRaid;
            public double OthersSent, OthersRepelled, OthersTurns;
            public int WalledAt40, RaidTurnsAt45, SentAt45;

            // The neighbours' troops (the economy plan's step 4): the lairs at the start, the
            // players' and all the neighbours' by turn 45 and by the end, the lairs left, and the
            // troops sent, won and gone home because the players were first.
            public int LairsAtStart, LairsAt45, OthersLairsAt45, OthersLairs, LairsLeft, TroopsSent, TroopsWon, TroopsRecalled;
            public int FirstTroop;
        }

        [Test, Explicit("A report, not a check")]
        public void GameReportTable()
        {
            var clock = Stopwatch.StartNew();
            var games = new List<Game>();
            for (int map = 1; map <= Maps; map++)
                games.Add(Play(map));

            var text = new StringBuilder();
            text.AppendLine("# Games played by the economy bot");
            text.AppendLine();
            text.AppendLine($"{Maps} maps (seeds 1-{Maps}) × {Turns} turns. The players' kingdom is played by `Rival.PlayATurn`: " +
                "it builds and grows towns onto quarters, and founds no towns. " +
                (WithHero
                    ? "Then `Rival.Arm` builds the capital's barracks, archery range and stables and `Rival.Hire` hires in every town, " +
                      "each with what is left above a reserve, and `Rival.HeroTurn` sends the hero " +
                      "after a group he beats by worth (×1.5) and odds (60%), a place worth a visit, a garrison to fetch or, with " +
                      "none, the edge of the known land; his fights are played by the auto battle. "
                    : "Its hero stays at home and nobody is hired (REALMS_GAME_NO_HERO). ") +
                (Passive
                    ? "The other kingdoms only get their income, town growth and the land their towns take (REALMS_GAME_PASSIVE). "
                    : Rate >= 0 ? $"The other kingdoms play at {Rate:0.##} things a turn (REALMS_GAME_RATE), first what their temper's gem wants. "
                    : $"The other kingdoms play their turns in `GameState.EndTurn` at the difficulty {Level}: " +
                      $"{Rival.Pace(Level).things} {(Rival.Pace(Level).things == 1 ? "thing" : "things")} in {Rival.Pace(Level).turns} turns, " +
                      "first what their temper's gem wants. ") +
                "A turn is the one the players see: turn 1 is the start, and a town's level counts from the turn it shows. " +
                "Empty: a turn in which the bot found nothing to build or grow onto. New: the first building of a kind, " +
                "a task done, a town's new level, " + (WithHero ? "a gem, the hero's new level or a lair cleared. " : "or a gem. ") +
                "Never: not within the game's 80 turns.");

            text.AppendLine();
            text.AppendLine("## Each map");
            text.AppendLine();
            text.AppendLine("| Seed | Towns at start | Capital level 3 / 4 | Other town level 2 / 3 / 4 | Tasks done (of 19) | " +
                "Land at start → 80 | Grown | Empty turns | Longest empty run | Last new | Longest without new |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (Game g in games)
            {
                text.AppendLine($"| {g.Seed} | {g.TownsAtStart} | {T(g.CapitalLevel3)} / {T(g.CapitalLevel4)} | " +
                    $"{T(g.TownLevel2)} / {T(g.TownLevel3)} / {T(g.TownLevel4)} | {g.QuestsDone} | {g.LandAtStart} → {g.LandAtEnd} | " +
                    $"{g.Grown} | {g.Empty} | {g.LongestEmpty} | {g.LastNew} | {g.LongestWithoutNew} |");
            }
            text.AppendLine($"| Mean | {games.Average(g => g.TownsAtStart):0.0} | {Mean(games, g => g.CapitalLevel3)} / {Mean(games, g => g.CapitalLevel4)} | " +
                $"{Mean(games, g => g.TownLevel2)} / {Mean(games, g => g.TownLevel3)} / {Mean(games, g => g.TownLevel4)} | " +
                $"{games.Average(g => g.QuestsDone):0.0} | {games.Average(g => g.LandAtStart):0} → {games.Average(g => g.LandAtEnd):0} | " +
                $"{games.Average(g => g.Grown):0.0} | {games.Average(g => g.Empty):0.0} | {games.Average(g => g.LongestEmpty):0.0} | " +
                $"{games.Average(g => g.LastNew):0} | {games.Average(g => g.LongestWithoutNew):0.0} |");

            text.AppendLine();
            text.AppendLine("## Treasury and income (mean of the maps)");
            text.AppendLine();
            text.AppendLine($"Dead: maps on which the stock is bigger than {DeadTurns} turns of its income.");
            text.AppendLine();
            text.AppendLine("| Turn | Gold | Food | Wood | Stone | Income gold / food / wood / stone | Dead gold / food / wood / stone |");
            text.AppendLine("|---|---|---|---|---|---|---|");
            foreach (int turn in Shown)
            {
                var row = new StringBuilder($"| {turn} |");
                for (int good = 0; good < 4; good++)
                    row.Append($" {games.Average(g => Of(g.Treasury[turn], good)):0} |");
                row.Append(" " + string.Join(" / ", Enumerable.Range(0, 4).Select(good => $"{games.Average(g => Of(g.Income[turn], good)):+0.0;-0.0;0}")) + " |");
                row.Append(" " + string.Join(" / ", Enumerable.Range(0, 4).Select(good => games.Count(g =>
                    Of(g.Income[turn], good) > 0 && Of(g.Treasury[turn], good) > DeadTurns * Of(g.Income[turn], good)).ToString())) + " |");
                text.AppendLine(row.ToString());
            }

            text.AppendLine();
            text.AppendLine($"## Where the goods went (all maps, turns 1-{Turns})");
            text.AppendLine();
            text.AppendLine("Came in: towns' and fields' income / tasks' rewards / the hero's spoils (fights' rewards and piles). " +
                "Went to: field buildings / town buildings / quarters / anything else (nothing should, now that land is not bought) / " +
                "the army / town growth, as shares of all that " +
                "came in. Left: what the treasury held at the end.");
            text.AppendLine();
            text.AppendLine("| Good | Came in: income / tasks / spoils | Field buildings | Town buildings | Quarters | Other | Army | Growth | Left |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|");
            for (int good = 0; good < 4; good++)
            {
                long Sum(Flow flow) => games.Sum(g => g.Flows[(int)flow, good]);
                long came = Sum(Flow.Income) + Sum(Flow.Tasks) + Sum(Flow.Spoils);
                string Share(Flow flow) => came == 0 ? "-" : $"{100.0 * Sum(flow) / came:0}%";
                long left = games.Sum(g => (long)Of(g.Treasury[Turns], good));
                text.AppendLine($"| {Goods[good]} | {Sum(Flow.Income)} / {Sum(Flow.Tasks)} / {Sum(Flow.Spoils)} | {Share(Flow.FieldBuildings)} | " +
                    $"{Share(Flow.TownBuildings)} | {Share(Flow.Quarters)} | {Share(Flow.Other)} | {Share(Flow.Army)} | {Share(Flow.Growth)} | " +
                    $"{(came == 0 ? "-" : $"{100.0 * left / came:0}%")} |");
            }

            text.AppendLine();
            text.AppendLine("## Why the players' towns did not grow (share of town-turns)");
            text.AppendLine();
            int townTurns = games.Sum(g => g.TownTurns);
            text.AppendLine("| Grew | " + string.Join(" | ", Enum.GetValues(typeof(GrowthStop)).Cast<GrowthStop>()
                .Where(s => s != GrowthStop.None).Select(s => s.ToString())) + " |");
            text.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---|", Enum.GetValues(typeof(GrowthStop)).Length - 1)));
            text.AppendLine("| " + string.Join(" | ", Enum.GetValues(typeof(GrowthStop)).Cast<GrowthStop>()
                .Select(s => $"{100.0 * games.Sum(g => g.Stops.TryGetValue(s, out int n) ? n : 0) / townTurns:0}%")) + " |");

            text.AppendLine();
            text.AppendLine("## First of each building");
            text.AppendLine();
            text.AppendLine("| Building | Maps | Turn: mean (earliest-latest) |");
            text.AppendLine("|---|---|---|");
            var kinds = Economy.FieldBuildings.Select(b => b.ToString())
                .Concat(Economy.TownBuildings.Concat(Economy.Fortifications).Select(b => b.ToString())).Append("Quarter");
            foreach (string kind in kinds)
            {
                List<int> turns = games.Where(g => g.FirstBuilt.ContainsKey(kind)).Select(g => g.FirstBuilt[kind]).ToList();
                text.AppendLine($"| {kind} | {turns.Count} | {(turns.Count == 0 ? "never" : $"{turns.Average():0} ({turns.Min()}-{turns.Max()})")} |");
            }

            text.AppendLine();
            text.AppendLine("## Tasks");
            text.AppendLine();
            text.AppendLine("| Task | Maps | Turn done: mean (earliest-latest) |");
            text.AppendLine("|---|---|---|");
            foreach (Quest quest in Quests.Order)
            {
                List<int> turns = games.Where(g => g.QuestTurn.ContainsKey(quest)).Select(g => g.QuestTurn[quest]).ToList();
                text.AppendLine($"| {quest} | {turns.Count} | {(turns.Count == 0 ? "never" : $"{turns.Average():0} ({turns.Min()}-{turns.Max()})")} |");
            }

            text.AppendLine();
            text.AppendLine($"## The crown ({Crown.Needed} gems of {Crown.Order.Length})");
            text.AppendLine();
            text.AppendLine(Passive ? "The other kingdoms do not play: their towns only grow by themselves." :
                $"The other kingdoms play at {(Rate >= 0 ? $"{Rate:0.##} things a turn" : Level.ToString())}; they have no heroes, their troops clear lairs.");
            text.AppendLine();
            text.AppendLine("| Gem | Players' kingdom: maps | Turn: mean (earliest-latest) | Other kingdoms: kingdoms (of all) | Turn: mean |");
            text.AppendLine("|---|---|---|---|---|");
            int rivals = games.Sum(g => g.RivalCount);
            foreach (Gem gem in Crown.Order)
            {
                List<int> mine = games.SelectMany(g => g.Gems.Where(x => x.Mine && x.Gem == gem).Select(x => x.Turn)).ToList();
                List<int> theirs = games.SelectMany(g => g.Gems.Where(x => !x.Mine && x.Gem == gem).Select(x => x.Turn)).ToList();
                text.AppendLine($"| {gem} | {mine.Count} | {(mine.Count == 0 ? "never" : $"{mine.Average():0} ({mine.Min()}-{mine.Max()})")} | " +
                    $"{theirs.Count} ({rivals}) | {(theirs.Count == 0 ? "never" : $"{theirs.Average():0}")} |");
            }
            List<int> crowned = games.Where(g => g.Crowned > 0).Select(g => g.Crowned).ToList();
            List<int> first = games.Where(g => g.FirstCrown < int.MaxValue).Select(g => g.FirstCrown).ToList();
            text.AppendLine();
            text.AppendLine($"Players' crown set on {crowned.Count} of {games.Count} maps" +
                (crowned.Count == 0 ? "." : $", turn {crowned.Average():0} ({crowned.Min()}-{crowned.Max()}).") +
                $" A crown of anyone on {first.Count} maps" + (first.Count == 0 ? "." : $", first on turn {first.Average():0} ({first.Min()}-{first.Max()}).") +
                $" The players first on {games.Count(g => g.Crowned > 0 && g.Crowned <= g.FirstCrown)} maps.");
            List<int> rivalCrowns = games.Where(g => g.RivalCrown > 0).Select(g => g.RivalCrown).ToList();
            text.AppendLine();
            text.AppendLine($"Another kingdom set its crown on {rivalCrowns.Count} maps" +
                (rivalCrowns.Count == 0 ? "." : $", first on turn {rivalCrowns.Average():0} ({rivalCrowns.Min()}-{rivalCrowns.Max()}).") +
                $" The kingdom with the most gems changed {games.Average(g => g.LeaderChanges):0.0} times a game " +
                $"(0-{games.Max(g => g.LeaderChanges)}). The first kingdom's gems less the second's: " +
                string.Join(", ", GapTurns.Select((t, i) => $"turn {t} {games.Average(g => g.Gap[i]):0.0}")) + ".");
            text.AppendLine();
            text.AppendLine($"The turn's end took {games.Sum(g => g.EndTurnMs) / (games.Count * Turns):0.0} ms on average, " +
                $"{games.Max(g => g.EndTurnMaxMs):0} ms at most (the core only, without drawing).");
            if (!Passive)
            {
                text.AppendLine();
                text.AppendLine("The other kingdoms' gems by their temper's first gem (kingdoms with it; gems won and the mean turn):");
                text.AppendLine();
                text.AppendLine("| Temper first | Kingdoms | " + string.Join(" | ", Crown.Order.Select(g => g.ToString())) + " |");
                text.AppendLine("|---|---|" + string.Concat(Crown.Order.Select(_ => "---|")));
                foreach (Gem aim in Rival.Pursued)
                {
                    int kingdoms = games.Sum(g => g.RivalAims.Count(a => a == aim));
                    var cells = Crown.Order.Select(gem =>
                    {
                        List<int> turns = games.SelectMany(g => g.RivalGems.Where(x => x.Aim == aim && x.Gem == gem).Select(x => x.Turn)).ToList();
                        return turns.Count == 0 ? "0" : $"{turns.Count} ({turns.Average():0})";
                    });
                    text.AppendLine($"| {aim} | {kingdoms} | " + string.Join(" | ", cells) + " |");
                }
            }

            text.AppendLine();
            text.AppendLine("## The other kingdoms (mean of one kingdom)");
            text.AppendLine();
            text.AppendLine("Built: field and town buildings and quarters; gold: what the treasury holds.");
            text.AppendLine();
            text.AppendLine("| Turn | Towns | Houses | Land | Built | Gold |");
            text.AppendLine("|---|---|---|---|---|---|");
            foreach (int turn in Shown)
            {
                text.AppendLine($"| {turn} | {games.Average(g => g.Neighbours[turn].Towns):0.0} | {games.Average(g => g.Neighbours[turn].Houses):0.0} | " +
                    $"{games.Average(g => g.Neighbours[turn].Land):0} | {games.Average(g => g.Neighbours[turn].Built):0.0} | " +
                    $"{games.Average(g => g.Neighbours[turn].Gold):0} |");
            }

            LandSection(text, games);
            RaidSection(text, games);
            TroopSection(text, games);

            if (WithHero)
            {
                text.AppendLine();
                text.AppendLine("## The players' hero (mean of the maps)");
                text.AppendLine();
                text.AppendLine("Army: the worth of his units (`Armies.Worth`; the start army is " +
                    $"{Wilds.StartWorth:0}). Fights, won and lairs count from the start.");
                text.AppendLine();
                text.AppendLine("| Turn | Level | Army | Fights | Won | Lairs |");
                text.AppendLine("|---|---|---|---|---|---|");
                foreach (int turn in Shown)
                {
                    text.AppendLine($"| {turn} | {games.Average(g => g.Hero[turn].Level):0.0} | {games.Average(g => g.Hero[turn].Army):0} | " +
                        $"{games.Average(g => g.Hero[turn].Fights):0.0} | {games.Average(g => g.Hero[turn].Won):0.0} | " +
                        $"{games.Average(g => g.Hero[turn].Lairs):0.0} |");
                }
                text.AppendLine();
                text.AppendLine("| Seed | Units hired | Fights | Won | Lairs | Level at the end | Turns he did nothing |");
                text.AppendLine("|---|---|---|---|---|---|---|");
                foreach (Game g in games)
                    text.AppendLine($"| {g.Seed} | {g.Hired} | {g.Fights} | {g.Won} | {g.Lairs} | {g.HeroLevel} | {g.HeroIdle} |");
                text.AppendLine($"| Mean | {games.Average(g => g.Hired):0} | {games.Average(g => g.Fights):0.0} | {games.Average(g => g.Won):0.0} | " +
                    $"{games.Average(g => g.Lairs):0.0} | {games.Average(g => g.HeroLevel):0.0} | {games.Average(g => g.HeroIdle):0.0} |");
            }

            text.AppendLine();
            text.AppendLine(WithHero ? "## Monsters (mean of the maps)" : "## Monsters (nobody fights them; mean of the maps)");
            text.AppendLine();
            text.AppendLine("| Turn | Groups | Lairs | Units |");
            text.AppendLine("|---|---|---|---|");
            foreach (int turn in Shown)
            {
                text.AppendLine($"| {turn} | {games.Average(g => g.Monsters[turn].Groups):0} | {games.Average(g => g.Monsters[turn].Lairs):0.0} | " +
                    $"{games.Average(g => g.Monsters[turn].Units):0} |");
            }

            text.AppendLine();
            text.AppendLine($"Took {clock.Elapsed.TotalSeconds:0.0} s.");
            string path = Environment.GetEnvironmentVariable("REALMS_GAME_REPORT");
            if (string.IsNullOrEmpty(path))
                TestContext.Progress.WriteLine(text.ToString());
            else
                File.WriteAllText(path, text.ToString());
        }

        static string T(int turn) => turn > 0 ? turn.ToString() : "never";

        /// <summary>The territory plan's checks (section 5), every kingdom of every map.</summary>
        /// <summary>The raids: the fun plan's Ф3 and the economy plan's check (town-turns lost, at most 5 a game for the bot).</summary>
        static void RaidSection(StringBuilder text, List<Game> games)
        {
            text.AppendLine();
            text.AppendLine("## Raids (the fun plan's Ф3)");
            text.AppendLine();
            text.AppendLine($"From turn {Raids.FirstTurn}-{Raids.FirstTurn + 3} a kingdom meets one band at a time and at most one every {Raids.Every} turns, " +
                $"from a lair within {Raids.Reach} steps whose nearest town is its own. Town-turns: turns a town did not grow while a band stood at it.");
            text.AppendLine();
            text.AppendLine("| A game | Sent | Beaten off by walls | Beaten by the hero | Town-turns lost | First raid |");
            text.AppendLine("|---|---|---|---|---|---|");
            List<int> first = games.Where(g => g.FirstRaid > 0).Select(g => g.FirstRaid).ToList();
            text.AppendLine($"| Players | {games.Average(g => g.RaidsSent):0.0} ({games.Min(g => g.RaidsSent)}-{games.Max(g => g.RaidsSent)}) | " +
                $"{games.Average(g => g.RaidsRepelled):0.0} | {games.Average(g => g.RaidsBeaten):0.0} | " +
                $"{games.Average(g => g.RaidTurns):0.0} ({games.Min(g => g.RaidTurns)}-{games.Max(g => g.RaidTurns)}) | " +
                (first.Count == 0 ? "never" : $"turn {first.Average():0} on {first.Count} maps") + " |");
            text.AppendLine($"| One other kingdom | {games.Average(g => g.OthersSent):0.0} | {games.Average(g => g.OthersRepelled):0.0} | — | " +
                $"{games.Average(g => g.OthersTurns):0.0} | |");
            text.AppendLine();
            text.AppendLine($"The players' walled towns on turn 40: {games.Average(g => g.WalledAt40):0.0}; " +
                $"maps with more than 5 town-turns lost: {games.Count(g => g.RaidTurns > 5)} of {games.Count}.");
            text.AppendLine();
            text.AppendLine($"By turn 45 (the short game): {games.Average(g => g.SentAt45):0.0} bands sent at the players, " +
                $"{games.Average(g => g.RaidTurnsAt45):0.0} town-turns lost ({games.Min(g => g.RaidTurnsAt45)}-{games.Max(g => g.RaidTurnsAt45)}), " +
                $"more than 5 on {games.Count(g => g.RaidTurnsAt45 > 5)} of {games.Count} maps.");
        }

        /// <summary>The neighbours' troops to the lairs: the economy plan's step 4 and its check, how many lairs go to the players.</summary>
        static void TroopSection(StringBuilder text, List<Game> games)
        {
            text.AppendLine();
            text.AppendLine("## Troops (the economy plan's step 4)");
            text.AppendLine();
            text.AppendLine($"A neighbour sends one troop at a time to the nearest lair within {Troops.Reach} steps of its towns that it can pay for: " +
                $"spearmen worth {Troops.Margin} times the group; {Troops.StandTurns} turns later the lair is its. The players' lairs are the hero's.");
            text.AppendLine();
            text.AppendLine("| Lairs at the start | Players' by 45 | All neighbours' by 45 | Players' at the end | All neighbours' at the end | Left | Troops sent | Won | Gone home | First troop |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            List<int> first = games.Where(g => g.FirstTroop > 0).Select(g => g.FirstTroop).ToList();
            string Range(Func<Game, int> f) => $"{games.Average(f):0.0} ({games.Min(f)}-{games.Max(f)})";
            text.AppendLine($"| {Range(g => g.LairsAtStart)} | {Range(g => g.LairsAt45)} | {Range(g => g.OthersLairsAt45)} | {Range(g => g.Lairs)} | " +
                $"{Range(g => g.OthersLairs)} | {games.Average(g => g.LairsLeft):0.0} | {games.Average(g => g.TroopsSent):0.0} | " +
                $"{games.Average(g => g.TroopsWon):0.0} | {games.Average(g => g.TroopsRecalled):0.0} | " +
                (first.Count == 0 ? "never" : $"turn {first.Average():0} on {first.Count} maps") + " |");
            text.AppendLine();
            text.AppendLine($"The players got 3 lairs (the hunter's gem) on {games.Count(g => g.Lairs >= Crown.HunterGoal)} of {games.Count} maps; " +
                $"lairs went more to the players than to all the neighbours together on {games.Count(g => g.Lairs > g.OthersLairs)} maps.");
        }

        static void LandSection(StringBuilder text, List<Game> games)
        {
            text.AppendLine();
            text.AppendLine("## Land (the territory plan's checks)");
            text.AppendLine();
            text.AppendLine("Every kingdom of every map; the players' kingdom is the one the bot plays, the others only " +
                "grow by themselves. \"On turn t\": what the players see on turn t, turn 1 being the start, so the gain " +
                "by turn 30 is what the ends of turns 1-29 brought. Wild land: land nobody holds, on the whole map.");
            text.AppendLine();
            // The players' kingdom is the first (WorldGen makes it so).
            var all = games.SelectMany(g => g.LandAt).ToList();
            var mine = games.Select(g => g.LandAt[0]).ToList();
            string Gain(List<int[]> lands, int from, int to, bool perTurn)
            {
                List<double> gains = lands.Select(l => (double)(l[to] - l[from]) / (perTurn ? to - from : 1)).ToList();
                return perTurn ? $"{gains.Average():0.00}" : $"{gains.Average():0} ({gains.Min():0}-{gains.Max():0})";
            }
            string Wild(int turn) => $"{100.0 * games.Sum(g => g.WildAt[turn]) / games.Sum(g => g.WildAt[1]):0}%";
            string Share(int part, int whole) => whole == 0 ? "-" : $"{100.0 * part / whole:0}%";
            string MedianMin(List<int> turns, int of) => turns.Count == 0 ? "never" :
                $"{Median(turns):0} / {turns.Min()}" + (turns.Count < of ? $" (never: {of - turns.Count} of {of})" : "");
            int kingdoms = all.Count;
            List<int> capitals = games.SelectMany(g => g.CapitalFull).Where(t => t > 0).ToList();
            List<int> meets = games.SelectMany(g => g.Meet).Where(t => t > 0).ToList();
            List<double> ratio = games.Select(g => (double)g.LandAt[0][60] / g.LandAt.Skip(1).Average(l => l[60])).ToList();
            List<double> spread = games.Select(g => (double)g.LandAt.Skip(1).Max(l => l[60]) / Math.Max(1, g.LandAt.Skip(1).Min(l => l[60]))).ToList();
            List<int> gemMine = games.SelectMany(g => g.Gems.Where(x => x.Mine && x.Gem == Gem.Lands).Select(x => x.Turn)).ToList();
            List<int> gemOthers = games.SelectMany(g => g.Gems.Where(x => !x.Mine && x.Gem == Gem.Lands).Select(x => x.Turn)).ToList();
            string GemTurn(List<int> turns, int of) => turns.Count == 0 ? $"never (of {of})" : $"{turns.Average():0} ({turns.Count} of {of})";

            text.AppendLine("| What | All kingdoms | Players' kingdom | Target |");
            text.AppendLine("|---|---|---|---|");
            text.AppendLine($"| Land at the start, median / minimum | {Median(all.Select(l => l[1]).ToList()):0} / {all.Min(l => l[1])} | " +
                $"{Median(mine.Select(l => l[1]).ToList()):0} / {mine.Min(l => l[1])} | 40-60 / ≥ 25 |");
            text.AppendLine($"| Wheat, wood, stone and gold inside at the start | {games.Sum(g => g.AllFour)} of {kingdoms} | | all |");
            text.AppendLine($"| Cells a turn, turns 1-12 | {Gain(all, 1, 13, true)} | {Gain(mine, 1, 13, true)} | 0.7-1.3 |");
            text.AppendLine($"| Cells a turn, turns 13-30 | {Gain(all, 13, 31, true)} | {Gain(mine, 13, 31, true)} | 1.2-2.5 |");
            text.AppendLine($"| Cells a turn, turns 31-48 | {Gain(all, 31, 49, true)} | {Gain(mine, 31, 49, true)} | 1.8-3.5 |");
            text.AppendLine($"| Gain by turn 30 | {Gain(all, 1, 30, false)} | {Gain(mine, 1, 30, false)} | 30-60 |");
            text.AppendLine($"| Gain by turn 60 | {Gain(all, 1, 60, false)} | {Gain(mine, 1, 60, false)} | 90-140 |");
            text.AppendLine($"| Wild land left on turn 30 | {Wild(30)} | | ≥ 60% |");
            text.AppendLine($"| Wild land left on turn 60 | {Wild(60)} | | 25-45% |");
            text.AppendLine($"| Town-turns with nowhere to grow, before turn 30 | {Share(games.Sum(g => g.FullEarly), games.Sum(g => g.TownsEarly))} | | < 15% |");
            text.AppendLine($"| Town-turns with nowhere to grow, after turn 45 | {Share(games.Sum(g => g.FullLate), games.Sum(g => g.TownsLate))} | | 20-50% |");
            text.AppendLine($"| Capital's first turn with nowhere to grow, median / earliest | {MedianMin(capitals, kingdoms)} | " +
                $"{MedianMin(games.Select(g => g.CapitalFull[0]).Where(t => t > 0).ToList(), games.Count)} | ≥ 10 |");
            text.AppendLine($"| Wild land within the towns' reach at the start shut by monsters | {Share(games.Sum(g => g.Blocked), games.Sum(g => g.InReach))} | | 10-30% |");
            text.AppendLine($"| First meeting of borders, median / earliest | {MedianMin(meets, kingdoms)} | " +
                $"{MedianMin(games.Select(g => g.Meet[0]).Where(t => t > 0).ToList(), games.Count)} | 15-35 / ≥ 8 |");
            text.AppendLine($"| Wild cells in reach on turn 30 that two kingdoms reach | {Share(games.Sum(g => g.Contested), games.Sum(g => g.Reachable))} | | 5-15% |");
            text.AppendLine($"| Players' land to the others' mean on turn 60 | | {ratio.Average():0.00} ({ratio.Min():0.00}-{ratio.Max():0.00}) | ≥ 1.4 |");
            text.AppendLine($"| Leader to laggard among the others on turn 60 | {spread.Average():0.00} (up to {spread.Max():0.00}) | | ≤ 2 |");
            text.AppendLine($"| Turn of the Lands gem ({Crown.LandsGoal} cells) | {GemTurn(gemOthers, kingdoms - games.Count)} | {GemTurn(gemMine, games.Count)} | 30-40 |");

            text.AppendLine();
            text.AppendLine(NoStake ? "No bot sets a stake (REALMS_GAME_NO_STAKE)." :
                "The bots set a stake on the wild resource nearest to their towns when they have none (`Influence.BotStake`).");
            text.AppendLine();
            text.AppendLine("| Players' kingdom | Mean | Range |");
            text.AppendLine("|---|---|---|");
            List<int> firsts = games.Where(g => g.FirstResource > 0).Select(g => g.FirstResource).ToList();
            text.AppendLine($"| Turn the first wild resource came inside | {(firsts.Count == 0 ? "never" : $"{firsts.Average():0.0}")} | " +
                $"{(firsts.Count == 0 ? "" : $"{firsts.Min()}-{firsts.Max()}")}{(firsts.Count < games.Count ? $", never on {games.Count - firsts.Count}" : "")} |");
            text.AppendLine($"| Wild resources inside by turn 20 | {games.Average(g => g.ResourcesBy20):0.0} | {games.Min(g => g.ResourcesBy20)}-{games.Max(g => g.ResourcesBy20)} |");
            text.AppendLine($"| Wild resources inside by turn 40 | {games.Average(g => g.ResourcesBy40):0.0} | {games.Min(g => g.ResourcesBy40)}-{games.Max(g => g.ResourcesBy40)} |");
            text.AppendLine($"| Stakes reached / taken first by a neighbour, a game | {games.Average(g => g.StakesReached):0.0} / {games.Average(g => g.StakesLost):0.0} | |");

            text.AppendLine();
            text.AppendLine("| Turn | Players' land | Others' land (mean) | Wild land left |");
            text.AppendLine("|---|---|---|---|");
            foreach (int turn in Shown)
            {
                text.AppendLine($"| {turn} | {mine.Average(l => l[turn]):0} | {games.Average(g => g.LandAt.Skip(1).Average(l => l[turn])):0} | {Wild(turn)} |");
            }
        }

        static double Median(List<int> values)
        {
            List<int> sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            return n == 0 ? 0 : n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
        }

        static readonly Resource[] Basics = { Resource.Wheat, Resource.Wood, Resource.Stone, Resource.Gold };

        /// <summary>The land, the wild land and the borders' first meetings on a turn.</summary>
        static void NoteLand(World world, Game game, int turn)
        {
            int count = world.Countries.Count;
            for (int k = 0; k < count; k++)
                game.LandAt[k][turn] = Land(world, k);
            game.WildAt[turn] = world.Cells.Count(c => c.IsLand && c.Owner < 0);
            foreach (Cell c in world.Cells)
            {
                if (!c.IsLand || c.Owner < 0 || game.Meet[c.Owner] > 0)
                    continue;
                for (int d = 0; d < 6; d++)
                {
                    int j = world.Neighbor(c.Index, d);
                    if (j < 0 || !world.Cells[j].IsLand || world.Cells[j].Owner < 0 || world.Cells[j].Owner == c.Owner)
                        continue;
                    game.Meet[c.Owner] = turn;
                    break;
                }
            }
        }

        /// <summary>The start: the four basic goods inside, and how much of the towns' reach monsters shut.</summary>
        static void NoteStart(World world, Game game)
        {
            int count = world.Countries.Count;
            game.LandAt = Enumerable.Range(0, count).Select(_ => new int[Turns + 2]).ToArray();
            game.WildAt = new int[Turns + 2];
            game.CapitalFull = new int[count];
            game.Meet = new int[count];
            for (int k = 0; k < count; k++)
            {
                if (Basics.All(r => world.Cells.Any(c => c.Owner == k && c.IsLand && c.Resource == r)))
                    game.AllFour++;
                var reach = new HashSet<int>();
                foreach (int index in world.Countries[k].Cities)
                {
                    City city = world.Cities[index];
                    foreach (Cell c in world.Cells)
                    {
                        if (c.IsLand && c.Owner < 0 && Hex.Distance(c.Hex, world.Cells[city.Cell].Hex) <= Influence.Reach(city) &&
                            !Influence.NearOtherTown(world, k, c.Index))
                            reach.Add(c.Index);
                    }
                }
                game.InReach += reach.Count;
                game.Blocked += reach.Count(i => !Influence.Open(world, i));
            }
            NoteLand(world, game, 1);
        }

        /// <summary>The wild cells two kingdoms' towns reach, of all the wild cells any reaches.</summary>
        static void NoteContest(World world, Game game)
        {
            var reached = new Dictionary<int, int>();
            for (int k = 0; k < world.Countries.Count; k++)
            {
                var mine = new HashSet<int>();
                foreach (int index in world.Countries[k].Cities)
                {
                    City city = world.Cities[index];
                    foreach (int cell in Influence.Walk(world, city, Influence.Reach(city)).Keys)
                    {
                        if (world.Cells[cell].Owner < 0)
                            mine.Add(cell);
                    }
                }
                foreach (int cell in mine)
                    reached[cell] = (reached.TryGetValue(cell, out int n) ? n : 0) + 1;
            }
            game.Reachable += reached.Count;
            game.Contested += reached.Values.Count(n => n > 1);
        }

        /// <summary>The mean turn of the maps that got there, and how many did.</summary>
        static string Mean(List<Game> games, Func<Game, int> turn)
        {
            List<int> reached = games.Select(turn).Where(t => t > 0).ToList();
            return reached.Count == 0 ? "never" : reached.Count == games.Count ? $"{reached.Average():0}" : $"{reached.Average():0} ({reached.Count}/{games.Count})";
        }

        static Game Play(int seed)
        {
            World world = WorldGen.Generate(seed);
            var state = new GameState(world) { Difficulty = Level, RivalsPlay = !Passive && Rate < 0, BotsStake = !NoStake };
            int player = world.Player;
            var wildResources = new HashSet<int>(world.Cells.Where(c => c.Owner < 0 && c.IsLand && c.Resource != Resource.None).Select(c => c.Index));
            int leader = -1;
            Country mine = world.Countries[player];
            var game = new Game
            {
                Seed = seed, TownsAtStart = mine.Cities.Count, LandAtStart = Land(world, player), LairsAtStart = world.Cells.Count(c => c.Lair),
            };
            NoteStart(world, game);
            var levels = new Dictionary<int, int>();
            foreach (int index in mine.Cities)
                levels[index] = Towns.Level(world.Cities[index]);
            int emptyRun = 0, lastNew = 1;

            void Note(int turn)
            {
                if (game.Treasury.ContainsKey(turn) || Array.IndexOf(Shown, turn) < 0)
                    return;
                game.Treasury[turn] = state.Treasury[player];
                game.Income[turn] = Economy.Income(world, player);
                var others = Enumerable.Range(0, world.Countries.Count).Where(k => k != player).ToList();
                game.Neighbours[turn] = (others.Average(k => world.Countries[k].Cities.Count),
                    others.Average(k => world.Countries[k].Cities.Sum(c => world.Cities[c].Houses)),
                    others.Average(k => Land(world, k)), others.Average(k => Built(world, k)), others.Average(k => state.Treasury[k].Gold));
                int groups = 0, lairs = 0, units = 0;
                foreach (Cell c in world.Cells)
                {
                    if (c.Monster == Monster.None)
                        continue;
                    groups++;
                    lairs += c.Lair ? 1 : 0;
                    units += c.Count;
                }
                game.Monsters[turn] = (groups, lairs, units);
                List<Hero> heroes = state.HeroesOf(player);
                game.Hero[turn] = (heroes.Count == 0 ? 0 : heroes.Max(h => h.Level), heroes.Sum(h => Armies.Worth(h.Army)),
                    game.Fights, game.Won, state.Lairs[player]);
            }

            void New(int turn)
            {
                game.LongestWithoutNew = Math.Max(game.LongestWithoutNew, turn - lastNew - 1);
                lastNew = turn;
            }

            for (int turn = 1; turn <= Turns; turn++)
            {
                Note(turn);
                // What stood before the bot's turn, to tell what its spending went to.
                Stock before = state.Treasury[player];
                var fields = new HashSet<int>(world.Cells.Where(c => c.Owner == player && c.Building != FieldBuilding.None).Select(c => c.Index));
                var buildings = mine.Cities.ToDictionary(i => i, i => new HashSet<TownBuilding>(world.Cities[i].Buildings));
                var quarters = mine.Cities.ToDictionary(i => i, i => world.Cities[i].Districts.Count);

                int done = Rival.PlayATurn(state, player);
                bool built = false, fresh = false;
                Stock army = default, spoils = default;
                if (WithHero)
                {
                    done += Rival.Arm(state, player);
                    // The bot's buildings are paid for before it hires.
                    Stock beforeHire = state.Treasury[player];
                    game.Hired += Rival.Hire(state, player);
                    army = beforeHire - state.Treasury[player];
                    int level = state.HeroesOf(player).Sum(h => h.Level), lairs = state.Lairs[player];
                    bool acted = false;
                    foreach (Hero hero in state.HeroesOf(player))
                    {
                        Stock beforeWalk = state.Treasury[player];
                        Rival.HeroDeeds deeds = Rival.HeroTurn(state, hero);
                        spoils += state.Treasury[player] - beforeWalk;
                        game.Fights += deeds.Fights;
                        game.Won += deeds.Won;
                        acted |= deeds.Steps > 0 || deeds.Fights > 0;
                    }
                    if (!acted)
                        game.HeroIdle++;
                    built |= state.HeroesOf(player).Sum(h => h.Level) > level || state.Lairs[player] > lairs;
                }
                Stock building = default, town = default, quarter = default;
                foreach (Cell c in world.Cells)
                {
                    if (c.Owner != player || c.Building == FieldBuilding.None || fields.Contains(c.Index))
                        continue;
                    building += Economy.Cost(c.Building);
                    built |= First(game, c.Building.ToString(), turn);
                }
                foreach (int index in mine.Cities)
                {
                    City city = world.Cities[index];
                    foreach (TownBuilding b in city.Buildings)
                    {
                        if (buildings.TryGetValue(index, out HashSet<TownBuilding> had) && had.Contains(b))
                            continue;
                        town += Economy.Cost(b);
                        built |= First(game, b.ToString(), turn);
                    }
                    int grew = city.Districts.Count - (quarters.TryGetValue(index, out int q) ? q : 0);
                    for (int i = 0; i < grew; i++)
                        quarter += Towns.DistrictCost;
                    if (grew > 0)
                        built |= First(game, "Quarter", turn);
                }
                Stock spent = before - state.Treasury[player];
                Stock other = spent - building - town - quarter - army + spoils;
                Add(game, Flow.FieldBuildings, building);
                Add(game, Flow.TownBuildings, town);
                Add(game, Flow.Quarters, quarter);
                Add(game, Flow.Other, other);
                Add(game, Flow.Army, army);
                Add(game, Flow.Spoils, spoils);

                if (done == 0)
                {
                    game.Empty++;
                    emptyRun++;
                    game.LongestEmpty = Math.Max(game.LongestEmpty, emptyRun);
                }
                else
                {
                    emptyRun = 0;
                }

                if (Rate >= 0 && !Passive)
                {
                    for (int k = 0; k < world.Countries.Count; k++)
                    {
                        if (k != player)
                            Rival.PlayATurn(state, k, Rival.Purchases(Rate, state.Turn), state.Aim(k));
                    }
                }
                Stock income = Economy.Income(world, player);
                var watch = Stopwatch.StartNew();
                TurnReport report = state.EndTurn();
                double ms = watch.Elapsed.TotalMilliseconds;
                game.EndTurnMs += ms;
                game.EndTurnMaxMs = Math.Max(game.EndTurnMaxMs, ms);
                int now = state.Turn;
                Add(game, Flow.Income, income);
                var food = new Stock(0, report.GrowthFood, 0, 0);
                Add(game, Flow.Growth, food);
                foreach (TurnEvent e in report.Events)
                {
                    if (e.Kind == TurnEventKind.GemSet)
                    {
                        game.Gems.Add((e.Country == player, e.Gem, now));
                        fresh |= e.Country == player;
                        if (e.Country != player)
                            game.RivalGems.Add((state.Aims[e.Country][0], e.Gem, now));
                    }
                    if (e.Kind == TurnEventKind.RaidSent)
                    {
                        fresh = true;
                        if (game.FirstRaid == 0)
                            game.FirstRaid = now;
                    }
                    if (e.Kind == TurnEventKind.RaidRepelled)
                        Add(game, Flow.Spoils, new Stock(e.Amount, 0, 0, 0));
                    game.StakesReached += e.Kind == TurnEventKind.StakeReached ? 1 : 0;
                    game.StakesLost += e.Kind == TurnEventKind.StakeLost ? 1 : 0;
                    if (e.Kind != TurnEventKind.QuestDone)
                        continue;
                    Add(game, Flow.Tasks, Quests.Reward(e.Quest));
                    game.QuestTurn[e.Quest] = now;
                    fresh = true;
                }
                NoteLand(world, game, now);
                int gained = wildResources.Count(i => world.Cells[i].Owner == player);
                if (gained > 0 && game.FirstResource == 0)
                    game.FirstResource = now;
                if (now == 20)
                    game.ResourcesBy20 = gained;
                if (now == 40)
                {
                    game.ResourcesBy40 = gained;
                    game.WalledAt40 = world.Countries[player].Cities.Count(i => world.Cities[i].Buildings.Contains(TownBuilding.Walls));
                }
                if (now == 45)
                {
                    game.RaidTurnsAt45 = state.RaidTurns[player];
                    game.SentAt45 = state.RaidsSent[player];
                    game.LairsAt45 = state.Lairs[player];
                    game.OthersLairsAt45 = state.Lairs.Sum() - state.Lairs[player];
                }
                if (game.FirstTroop == 0 && state.TroopsSent.Sum() > 0)
                    game.FirstTroop = now;
                // The kingdom with the most gems, when it is one; a tie keeps the last one.
                int most = state.Gems.Max(g => g.Count);
                List<int> first = Enumerable.Range(0, state.Gems.Length).Where(k => state.Gems[k].Count == most).ToList();
                if (most > 0 && first.Count == 1 && first[0] != leader)
                {
                    if (leader >= 0)
                        game.LeaderChanges++;
                    leader = first[0];
                }
                int gapAt = Array.IndexOf(GapTurns, now);
                if (gapAt >= 0)
                {
                    List<int> counts = state.Gems.Select(g => g.Count).OrderByDescending(c => c).ToList();
                    game.Gap[gapAt] = counts[0] - counts[1];
                }
                foreach (City city in world.Cities)
                {
                    if (now < 30)
                    {
                        game.TownsEarly++;
                        game.FullEarly += city.LandFull ? 1 : 0;
                    }
                    else if (now > 45)
                    {
                        game.TownsLate++;
                        game.FullLate += city.LandFull ? 1 : 0;
                    }
                    if (city.Capital && city.LandFull && game.CapitalFull[city.Country] == 0)
                        game.CapitalFull[city.Country] = now;
                }
                if (now == 30)
                    NoteContest(world, game);
                foreach (int index in mine.Cities)
                {
                    City city = world.Cities[index];
                    game.TownTurns++;
                    game.Stops[city.Stop] = (game.Stops.TryGetValue(city.Stop, out int n) ? n : 0) + 1;
                    int level = Towns.Level(city);
                    if (levels.TryGetValue(index, out int was) && level > was)
                    {
                        fresh = true;
                        if (city.Capital)
                        {
                            if (level >= 3 && game.CapitalLevel3 == 0)
                                game.CapitalLevel3 = now;
                            if (level >= 4 && game.CapitalLevel4 == 0)
                                game.CapitalLevel4 = now;
                        }
                        else
                        {
                            if (level >= 2 && game.TownLevel2 == 0)
                                game.TownLevel2 = now;
                            if (level >= 3 && game.TownLevel3 == 0)
                                game.TownLevel3 = now;
                            if (level >= 4 && game.TownLevel4 == 0)
                                game.TownLevel4 = now;
                        }
                    }
                    levels[index] = level;
                }
                // Builds and the hero's deeds count on the turn they were made, tasks and levels on the turn they show.
                if (built)
                    New(turn);
                if (fresh && now <= Turns)
                    New(now);
            }
            game.LastNew = lastNew;
            New(Turns + 1);
            game.LandAtEnd = Land(world, player);
            game.Grown = state.Grown[player];
            game.QuestsDone = state.Done.Count;
            game.Crowned = state.Crowned[player];
            game.Lairs = state.Lairs[player];
            game.OthersLairs = state.Lairs.Sum() - game.Lairs;
            game.LairsLeft = world.Cells.Count(c => c.Lair);
            game.TroopsSent = state.TroopsSent.Sum();
            game.TroopsWon = state.TroopsWon.Sum();
            game.TroopsRecalled = state.TroopsRecalled.Sum();
            game.RaidsSent = state.RaidsSent[player];
            game.RaidsRepelled = state.RaidsRepelled[player];
            game.RaidsBeaten = state.RaidsBeaten[player];
            game.RaidTurns = state.RaidTurns[player];
            int others = world.Countries.Count - 1;
            game.OthersSent = (state.RaidsSent.Sum() - game.RaidsSent) / (double)others;
            game.OthersRepelled = (state.RaidsRepelled.Sum() - game.RaidsRepelled) / (double)others;
            game.OthersTurns = (state.RaidTurns.Sum() - game.RaidTurns) / (double)others;
            List<Hero> mineHeroes = state.HeroesOf(player);
            game.HeroLevel = mineHeroes.Count == 0 ? 0 : mineHeroes.Max(h => h.Level);
            game.RivalCount = world.Countries.Count - 1;
            for (int k = 0; k < state.Crowned.Length; k++)
            {
                int turn = state.Crowned[k];
                if (turn > 0)
                    game.FirstCrown = Math.Min(game.FirstCrown, turn);
                if (k != player && turn > 0 && (game.RivalCrown == 0 || turn < game.RivalCrown))
                    game.RivalCrown = turn;
                if (k != player)
                    game.RivalAims.Add(state.Aims[k][0]);
            }
            Note(Turns);
            return game;
        }

        static bool First(Game game, string kind, int turn)
        {
            if (game.FirstBuilt.ContainsKey(kind))
                return false;
            game.FirstBuilt[kind] = turn;
            return true;
        }

        static void Add(Game game, Flow flow, Stock stock)
        {
            for (int good = 0; good < 4; good++)
                game.Flows[(int)flow, good] += Of(stock, good);
        }

        static int Land(World world, int country) => world.Cells.Count(c => c.Owner == country && c.IsLand);

        /// <summary>The kingdom's field buildings, town buildings and quarters.</summary>
        static int Built(World world, int country) =>
            world.Cells.Count(c => c.Owner == country && c.Building != FieldBuilding.None) +
            world.Countries[country].Cities.Sum(i => world.Cities[i].Buildings.Count + world.Cities[i].Districts.Count);
    }
}

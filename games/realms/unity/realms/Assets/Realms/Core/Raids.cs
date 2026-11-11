using System;
using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>
    /// A band (ватага) a lair sent against a town: the fun plan's Ф3 (2026-11-11). The band is a
    /// monster group standing on <see cref="Cell"/> (that cell's monster and count, marked
    /// <see cref="Core.Cell.Band"/>), so a hero meets and fights it as any other group.
    /// </summary>
    public sealed class Raid
    {
        /// <summary>The cell of the lair it came out of.</summary>
        public int Lair;

        /// <summary>The town it goes for (index into <see cref="World.Cities"/>).</summary>
        public int Town;

        /// <summary>The cell it stands on now.</summary>
        public int Cell;

        /// <summary>Turns it still stands at the town; 0 while it walks there.</summary>
        public int Stay;

        /// <summary>Turns it has been out.</summary>
        public int Age;

        /// <summary>Steps left to the town on its last walk (for the turns the players see).</summary>
        public int Steps;
    }

    /// <summary>
    /// The lairs' raids, the fun plan's Ф3: a lair sends bands to its nearest town of any kingdom
    /// within <see cref="Reach"/> steps. A kingdom meets one band at a time and at most one every
    /// <see cref="Every"/> turns, from the 12th-15th turn on (<see cref="FirstFor"/>); of the lairs
    /// whose nearest town is its own, the one that rested longest sends it; a lair under a
    /// neighbour's troop (<see cref="Troops"/>) sends none. The band walks <see cref="Speed"/> steps a turn. A walled town beats it off and
    /// takes a small loot; at a town without walls it stands <see cref="StayTurns"/> turns on one
    /// of the town's fields, and the town does not grow and the field gives nothing meanwhile.
    /// Nothing is destroyed. A hero may beat the band on its way or at the town, and clearing
    /// the lair ends its raids.
    /// </summary>
    public static class Raids
    {
        /// <summary>The turn the first lairs send bands.</summary>
        public const int FirstTurn = 12;

        /// <summary>
        /// A kingdom meets at most one band in this many turns. The first measure, with every lair
        /// raiding every 8 turns, cost the players' bot 8.5 town-turns a game (0-47) and the
        /// neighbours 22 (workspace notes of 2026-11-11), against the plan's 5.
        /// </summary>
        public const int Every = 12;

        /// <summary>Steps a band walks a turn.</summary>
        public const int Speed = 3;

        /// <summary>The most steps from a lair to the cell its band stands on at the town.</summary>
        public const int Reach = 9;

        /// <summary>Turns a band stands at a town without walls.</summary>
        public const int StayTurns = 3;

        /// <summary>A band that has not got through in this many turns goes back.</summary>
        public const int MaxAge = 8;

        /// <summary>A band is a third of its lair's group.</summary>
        public static int Size(int lairCount) => Math.Max(1, (int)MathF.Round(lairCount / 3f));

        /// <summary>The first turn a band may come for the kingdom: 12-15 by the map and the kingdom.</summary>
        public static int FirstFor(World world, int country) => FirstTurn + (int)((uint)(world.Seed + country) % 4u);

        /// <summary>Whether a band may step onto the cell: land with no town, place, group or hero.</summary>
        static bool Free(GameState state, int cell)
        {
            Cell c = state.World.Cells[cell];
            return c.IsLand && c.Town < 0 && c.Place == Place.None && c.Monster == Monster.None && state.HeroAt(cell) == null;
        }

        /// <summary>
        /// Steps from <paramref name="from"/> over free cells up to <paramref name="limit"/>
        /// (-1 where not reached); <paramref name="previous"/> gets the cell each is reached from.
        /// </summary>
        static int[] Walk(GameState state, int from, int limit, int[] previous)
        {
            World world = state.World;
            var steps = new int[world.Cells.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i] = -1;
                previous[i] = -1;
            }
            var queue = new Queue<int>();
            steps[from] = 0;
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                if (steps[i] >= limit)
                    continue;
                for (int d = 0; d < 6; d++)
                {
                    int j = world.Neighbor(i, d);
                    if (j < 0 || steps[j] >= 0 || !Free(state, j))
                        continue;
                    steps[j] = steps[i] + 1;
                    previous[j] = i;
                    queue.Enqueue(j);
                }
            }
            return steps;
        }

        /// <summary>
        /// Where a band stands at the town: the nearest of the kingdom's fields beside the town's
        /// centre or quarters, else the nearest free cell beside them, or -1 when the walk reaches none.
        /// </summary>
        static int Goal(GameState state, int town, int[] steps)
        {
            World world = state.World;
            City city = world.Cities[town];
            int best = -1, bestScore = int.MaxValue;
            void Around(int cell)
            {
                for (int d = 0; d < 6; d++)
                {
                    int j = world.Neighbor(cell, d);
                    if (j < 0 || steps[j] < 0 || world.Cells[j].Town >= 0)
                        continue;
                    Cell c = world.Cells[j];
                    int score = steps[j] + (c.Owner == city.Country && c.Building != FieldBuilding.None ? 0 : 100);
                    if (score < bestScore || (score == bestScore && j < best))
                    {
                        best = j;
                        bestScore = score;
                    }
                }
            }
            Around(city.Cell);
            foreach (int district in city.Districts)
                Around(district);
            return best;
        }

        /// <summary>The band on the cell's raid, or null.</summary>
        public static Raid At(GameState state, int cell)
        {
            foreach (Raid raid in state.Bands)
            {
                if (raid.Cell == cell)
                    return raid;
            }
            return null;
        }

        /// <summary>Whether a band stands at the town now (it does not grow).</summary>
        public static bool AtTown(GameState state, int town)
        {
            foreach (Raid raid in state.Bands)
            {
                if (raid.Town == town && raid.Stay > 0)
                    return true;
            }
            return false;
        }

        /// <summary>Roughly how many turns until the walking band reaches its town (at least 1).</summary>
        public static int TurnsLeft(Raid raid) => Math.Max(1, (raid.Steps + Speed - 1) / Speed);

        /// <summary>
        /// The raids' part of the turn's end (after <see cref="GameState.Turn"/> moved on): bands
        /// beaten by heroes are struck off, standing bands count down and go, walking bands walk,
        /// and the lairs whose turn it is send new ones.
        /// </summary>
        public static void Play(GameState state, TurnReport report)
        {
            World world = state.World;
            var previous = new int[world.Cells.Length];
            for (int r = state.Bands.Count - 1; r >= 0; r--)
            {
                Raid raid = state.Bands[r];
                Cell at = world.Cells[raid.Cell];
                City city = world.Cities[raid.Town];
                if (at.Monster == Monster.None || !at.Band)
                {
                    // A hero beat it (or let it go after it gave up).
                    state.RaidsBeaten[city.Country]++;
                    state.Bands.RemoveAt(r);
                    continue;
                }
                raid.Age++;
                if (raid.Stay > 0)
                {
                    if (--raid.Stay == 0)
                        Leave(state, r, report, TurnEventKind.RaidLeft);
                    continue;
                }
                int[] steps = Walk(state, raid.Cell, 2 * Reach, previous);
                int goal = Goal(state, raid.Town, steps);
                if (goal < 0)
                {
                    if (raid.Age >= MaxAge)
                        Leave(state, r, report, TurnEventKind.RaidLeft);
                    continue;
                }
                if (goal == raid.Cell)
                {
                    raid.Steps = 0;
                    Arrive(state, r, report);
                    continue;
                }
                var way = new List<int>();
                for (int i = goal; i != raid.Cell; i = previous[i])
                    way.Add(i);
                way.Reverse();
                int to = way[Math.Min(Speed, way.Count) - 1];
                Move(world, raid, to, report);
                raid.Steps = way.Count - Math.Min(Speed, way.Count);
                if (to == goal)
                    Arrive(state, r, report);
                else if (raid.Age >= MaxAge)
                    Leave(state, r, report, TurnEventKind.RaidLeft);
            }

            // The kingdoms whose time has come draw a band from the lair that rested longest
            // (the nearest on a tie) among those whose nearest town is theirs.
            int countries = world.Countries.Count;
            var ready = new bool[countries];
            bool any = false;
            for (int k = 0; k < countries; k++)
            {
                ready[k] = state.Turn >= FirstFor(world, k) && state.Turn - state.LastRaid[k] >= Every;
                any |= ready[k];
            }
            foreach (Raid raid in state.Bands)
                ready[world.Cities[raid.Town].Country] = false;
            if (!any)
                return;
            var lairOf = new int[countries];
            var lairSteps = new int[countries];
            for (int k = 0; k < countries; k++)
                lairOf[k] = -1;
            foreach (Cell lair in world.Cells)
            {
                if (!lair.Lair || lair.Monster == Monster.None || Out(state, lair.Index) || Core.Troops.At(state, lair.Index) != null)
                    continue;
                int town = Target(state, lair.Index, previous, out _, out int steps);
                if (town < 0)
                    continue;
                int k = world.Cities[town].Country;
                if (!ready[k])
                    continue;
                int rested = state.Turn - state.LairSent.GetValueOrDefault(lair.Index, int.MinValue / 2);
                int best = lairOf[k];
                int bestRested = best < 0 ? int.MinValue : state.Turn - state.LairSent.GetValueOrDefault(best, int.MinValue / 2);
                if (best < 0 || rested > bestRested || (rested == bestRested && steps < lairSteps[k]))
                {
                    lairOf[k] = lair.Index;
                    lairSteps[k] = steps;
                }
            }
            for (int k = 0; k < countries; k++)
            {
                if (lairOf[k] >= 0)
                    Send(state, lairOf[k], report);
            }
        }

        /// <summary>
        /// The lair's nearest town within <see cref="Reach"/> steps (-1 when none), the cell its band
        /// would stand on and the steps there; <paramref name="previous"/> gets the walk.
        /// </summary>
        static int Target(GameState state, int lairCell, int[] previous, out int goal, out int steps)
        {
            World world = state.World;
            int[] walk = Walk(state, lairCell, Reach, previous);
            int town = -1;
            goal = -1;
            for (int t = 0; t < world.Cities.Count; t++)
            {
                int g = Goal(state, t, walk);
                if (g >= 0 && (goal < 0 || walk[g] < walk[goal]))
                {
                    town = t;
                    goal = g;
                }
            }
            steps = goal >= 0 ? walk[goal] : 0;
            return town;
        }

        /// <summary>Whether a band of the lair is still out.</summary>
        static bool Out(GameState state, int lair)
        {
            foreach (Raid raid in state.Bands)
            {
                if (raid.Lair == lair)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The lair sends a band to the nearest town within <see cref="Reach"/> steps, if any: it
        /// comes out onto the first cell of its way.
        /// </summary>
        public static Raid Send(GameState state, int lairCell, TurnReport report)
        {
            World world = state.World;
            Cell lair = world.Cells[lairCell];
            var previous = new int[world.Cells.Length];
            int town = Target(state, lairCell, previous, out int goal, out int steps);
            if (town < 0)
                return null;
            int door = goal;
            while (previous[door] != lair.Index)
                door = previous[door];
            var raid = new Raid { Lair = lair.Index, Town = town, Cell = lair.Index, Steps = steps - 1 };
            Cell c = world.Cells[door];
            c.Monster = lair.Monster;
            c.Count = c.FirstCount = Size(lair.Count);
            c.Band = true;
            raid.Cell = door;
            state.Bands.Add(raid);
            report.Changed.Add(door);
            int country = world.Cities[town].Country;
            state.RaidsSent[country]++;
            state.LastRaid[country] = state.Turn;
            state.LairSent[lair.Index] = state.Turn;
            if (country == world.Player)
                report.Events.Add(new TurnEvent(TurnEventKind.RaidSent, town, country: country, cell: door, monster: c.Monster));
            if (door == goal)
                Arrive(state, state.Bands.Count - 1, report);
            return raid;
        }

        /// <summary>Moves the band onto another cell.</summary>
        static void Move(World world, Raid raid, int to, TurnReport report)
        {
            Cell from = world.Cells[raid.Cell], c = world.Cells[to];
            c.Monster = from.Monster;
            c.Count = from.Count;
            c.FirstCount = from.FirstCount;
            c.Band = true;
            from.Monster = Monster.None;
            from.Count = from.FirstCount = 0;
            from.Band = false;
            report.Changed.Add(raid.Cell);
            report.Changed.Add(to);
            raid.Cell = to;
        }

        /// <summary>
        /// The band is at the town: walls beat it off for a small loot (the reward of a group of
        /// its size); without walls it stands there for <see cref="StayTurns"/> turns.
        /// </summary>
        static void Arrive(GameState state, int r, TurnReport report)
        {
            Raid raid = state.Bands[r];
            World world = state.World;
            City city = world.Cities[raid.Town];
            Cell c = world.Cells[raid.Cell];
            if (city.Buildings.Contains(TownBuilding.Walls))
            {
                Stock loot = GameState.Reward(c.Monster, c.Count, false);
                state.Treasury[city.Country] += loot;
                state.RaidsRepelled[city.Country]++;
                if (city.Country == world.Player)
                    report.Events.Add(new TurnEvent(TurnEventKind.RaidRepelled, raid.Town, country: city.Country, cell: raid.Cell,
                        monster: c.Monster, amount: loot.Gold));
                Leave(state, r, report, null);
                return;
            }
            raid.Stay = StayTurns;
            raid.Steps = 0;
            if (city.Country == world.Player)
                report.Events.Add(new TurnEvent(TurnEventKind.RaidArrived, raid.Town, country: city.Country, cell: raid.Cell, monster: c.Monster));
        }

        /// <summary>The band goes back to its lair: off the map.</summary>
        static void Leave(GameState state, int r, TurnReport report, TurnEventKind? tell)
        {
            Raid raid = state.Bands[r];
            World world = state.World;
            Cell c = world.Cells[raid.Cell];
            int country = world.Cities[raid.Town].Country;
            if (tell.HasValue && country == world.Player)
                report.Events.Add(new TurnEvent(tell.Value, raid.Town, country: country, cell: raid.Cell, monster: c.Monster));
            c.Monster = Monster.None;
            c.Count = c.FirstCount = 0;
            c.Band = false;
            report.Changed.Add(raid.Cell);
            state.Bands.RemoveAt(r);
        }
    }

    public sealed partial class GameState
    {
        /// <summary>The bands out of the lairs on raids (<see cref="Core.Raids"/>).</summary>
        public readonly List<Raid> Bands = new List<Raid>();

        /// <summary>Per kingdom: bands sent against its towns, beaten off by walls, beaten by heroes, and town-turns lost to them.</summary>
        public int[] RaidsSent, RaidsRepelled, RaidsBeaten, RaidTurns;

        /// <summary>Per kingdom: the turn the last band was sent against it.</summary>
        public int[] LastRaid;

        /// <summary>Per lair cell: the turn it last sent a band.</summary>
        public readonly Dictionary<int, int> LairSent = new Dictionary<int, int>();

        void InitRaids()
        {
            int n = World.Countries.Count;
            LastRaid = new int[n];
            for (int k = 0; k < n; k++)
                LastRaid[k] = int.MinValue / 2;
            RaidsSent = new int[n];
            RaidsRepelled = new int[n];
            RaidsBeaten = new int[n];
            RaidTurns = new int[n];
        }
    }
}

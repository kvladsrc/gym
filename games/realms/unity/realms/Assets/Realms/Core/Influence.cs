using System;
using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>
    /// A kingdom's land grows by itself from its towns, with no buying (the territory plan of
    /// 2026-11-11; the players chose "it grows by itself, no buying"). Every turn each town gains
    /// influence by its size (<see cref="Power"/>); for <see cref="CellPrice"/> of it the town takes
    /// the cheapest wild cell beside its kingdom's land within its <see cref="Reach"/>, by the walk
    /// over its own and wild land. A cell, once taken, never changes hands. The kingdoms' land at
    /// the start is cut to what their towns reach (<see cref="Trim"/>), so every town has a ring to fill.
    /// A kingdom may set one free stake (вешка) on a wild cell its towns reach: the town with the
    /// cheapest way to it then takes the cells of that way first (the territory plan's П3, without
    /// its «взять сейчас», as the players chose no buying).
    /// </summary>
    public static class Influence
    {
        /// <summary>The influence one cell costs.</summary>
        public const int CellPrice = 6;

        /// <summary>A resource cell counts this much cheaper, so a town takes it first.</summary>
        public const float ResourcePull = 1.5f;

        /// <summary>A step across a river costs this much more: a river holds a border.</summary>
        public const float RiverStep = 1f;

        /// <summary>No kingdom takes a wild cell this near another kingdom's town.</summary>
        public const int HomeCircle = 2;

        /// <summary>Influence a turn: the town's level, one more for a capital and one for a market.</summary>
        public static int Power(City city) =>
            Towns.Level(city) + (city.Capital ? 1 : 0) + (city.Buildings.Contains(TownBuilding.Market) ? 1 : 0);

        /// <summary>How far a town of the first level takes land, in the walk's cost (a meadow step costs 1); each level adds one.</summary>
        public const int FirstReach = 6;

        /// <summary>The kingdoms start with what their towns reach less this, the ring each town fills first.</summary>
        public const int StartRing = 3;

        /// <summary>How far the town takes land, in the walk's cost (a meadow step costs 1).</summary>
        public static int Reach(City city) => FirstReach - 1 + Towns.Level(city);

        /// <summary>The cost of the step from one cell to its neighbour across edge <paramref name="direction"/>.</summary>
        internal static float Step(World world, int from, int direction, int to) =>
            WorldGen.StepCost(world.Cells[to].Biome) + (((world.Cells[from].Rivers >> direction) & 1) != 0 ? RiverStep : 0f);

        /// <summary>
        /// The cells the town's walk gets to within <paramref name="reach"/> and what it costs: over
        /// the kingdom's land and over wild land the kingdom may take (<see cref="Open"/>), or, with
        /// <paramref name="ownOnly"/>, over its own land alone.
        /// </summary>
        public static Dictionary<int, float> Walk(World world, City city, float reach, bool ownOnly = false)
            => Walk(world, city, reach, ownOnly, null);

        /// <summary>
        /// The walk of <see cref="Walk(World, City, float, bool)"/>; with <paramref name="previous"/>
        /// it also notes the cell each cell is reached from and keeps off the wild cells the kingdom
        /// may not take (near another kingdom's town), so the way it gives can be taken cell by cell.
        /// </summary>
        static Dictionary<int, float> Walk(World world, City city, float reach, bool ownOnly, Dictionary<int, int> previous)
        {
            var cost = new Dictionary<int, float> { [city.Cell] = 0f };
            var heap = new MinHeap();
            heap.Push(0f, city.Cell);
            while (heap.Count > 0)
            {
                heap.Pop(out float d, out int i);
                if (d > cost[i])
                    continue;
                for (int dir = 0; dir < 6; dir++)
                {
                    int j = world.Neighbor(i, dir);
                    if (j < 0)
                        continue;
                    Cell c = world.Cells[j];
                    if (c.IsWater || (c.Owner != city.Country && (ownOnly || c.Owner >= 0 || !Open(world, j))))
                        continue;
                    if (previous != null && c.Owner < 0 && NearOtherTown(world, city.Country, j))
                        continue;
                    float next = d + Step(world, i, dir, j);
                    if (next <= reach + 1e-4f && (!cost.TryGetValue(j, out float old) || next < old))
                    {
                        cost[j] = next;
                        if (previous != null)
                            previous[j] = i;
                        heap.Push(next, j);
                    }
                }
            }
            return cost;
        }

        /// <summary>Wild land a border may cross: no monster group on it and no lair beside it.</summary>
        public static bool Open(World world, int cell)
        {
            Cell c = world.Cells[cell];
            if (c.IsWater || c.Monster != Monster.None)
                return false;
            for (int d = 0; d < 6; d++)
            {
                int j = world.Neighbor(cell, d);
                if (j >= 0 && world.Cells[j].Lair)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Whether the kingdom may take the wild cell: open land (<see cref="Open"/>) beside its own
        /// land and farther than <see cref="HomeCircle"/> steps from every other kingdom's town.
        /// </summary>
        public static bool Takeable(World world, int country, int cell)
        {
            Cell c = world.Cells[cell];
            if (c.Owner >= 0 || !Open(world, cell))
                return false;
            bool beside = false;
            for (int d = 0; d < 6 && !beside; d++)
            {
                int j = world.Neighbor(cell, d);
                beside = j >= 0 && world.Cells[j].IsLand && world.Cells[j].Owner == country;
            }
            return beside && !NearOtherTown(world, country, cell);
        }

        /// <summary>The cell the town takes next, or -1 when it has nowhere to grow.</summary>
        public static int Next(World world, City city) => Next(world, city, Walk(world, city, Reach(city)));

        static int Next(World world, City city, Dictionary<int, float> walk)
        {
            int stake = world.Countries[city.Country].Stake;
            if (stake >= 0)
            {
                var way = new List<int>();
                if (StakeWay(world, city.Country, stake, way) == city.Index && way.Count > 0
                    && walk.ContainsKey(way[0]) && Takeable(world, city.Country, way[0]))
                    return way[0];
            }
            int best = -1;
            float bestScore = float.PositiveInfinity;
            foreach (KeyValuePair<int, float> step in walk)
            {
                if (world.Cells[step.Key].Owner >= 0 || !Takeable(world, city.Country, step.Key))
                    continue;
                float score = Score(world, step.Key, step.Value);
                if (score < bestScore || (score == bestScore && step.Key < best))
                {
                    best = step.Key;
                    bestScore = score;
                }
            }
            return best;
        }

        static float Score(World world, int cell, float cost) =>
            cost - (world.Cells[cell].Resource != Resource.None ? ResourcePull : 0f);

        /// <summary>
        /// The town that goes to a stake on the cell: the kingdom's town with the cheapest way there
        /// within its reach (the lower index on a tie), or -1 when none reaches it. <paramref name="way"/>
        /// gets the way's wild cells from the town out, the stake's cell last.
        /// </summary>
        public static int StakeWay(World world, int country, int cell, List<int> way = null)
        {
            way?.Clear();
            if (cell < 0 || world.Cells[cell].Owner >= 0)
                return -1;
            int best = -1;
            float bestCost = float.PositiveInfinity;
            Dictionary<int, int> bestPrevious = null;
            foreach (int index in world.Countries[country].Cities)
            {
                City city = world.Cities[index];
                var previous = new Dictionary<int, int>();
                Dictionary<int, float> walk = Walk(world, city, Reach(city), false, previous);
                if (walk.TryGetValue(cell, out float cost) && cost < bestCost - 1e-4f)
                {
                    best = index;
                    bestCost = cost;
                    bestPrevious = previous;
                }
            }
            if (best < 0 || way == null)
                return best;
            for (int i = cell; i != world.Cities[best].Cell; i = bestPrevious[i])
            {
                if (world.Cells[i].Owner < 0)
                    way.Add(i);
            }
            way.Reverse();
            return best;
        }

        /// <summary>
        /// Roughly how many turns until the kingdom's border reaches the wild cell with a stake on it,
        /// and which town walks there (-1 and -1 when no town reaches it): the way's cells, each for
        /// <see cref="CellPrice"/>, at the town's influence a turn.
        /// </summary>
        public static int StakeTurns(World world, int country, int cell, out int town)
        {
            var way = new List<int>();
            town = StakeWay(world, country, cell, way);
            if (town < 0 || way.Count == 0 || !Open(world, cell) || NearOtherTown(world, country, cell))
            {
                town = -1;
                return -1;
            }
            City city = world.Cities[town];
            return Math.Max(1, (int)Math.Ceiling((way.Count * CellPrice - city.Influence) / (double)Math.Max(1, Power(city))));
        }

        /// <summary>Whether the kingdom may set its stake on the cell: wild open land one of its towns has a way to.</summary>
        public static bool CanStake(World world, int country, int cell) => StakeTurns(world, country, cell, out _) > 0;

        /// <summary>
        /// Stakes on cells that somebody took go; when another kingdom took the players' one, the
        /// report says so («успели раньше»), and when their own town got there, that it did.
        /// </summary>
        static void DropTakenStakes(World world, TurnReport report, int town = -1)
        {
            foreach (Country k in world.Countries)
            {
                if (k.Stake < 0 || world.Cells[k.Stake].Owner < 0)
                    continue;
                int owner = world.Cells[k.Stake].Owner;
                if (k.Index == world.Player && report != null)
                    report.Events.Add(owner == k.Index
                        ? new TurnEvent(TurnEventKind.StakeReached, town, country: owner, cell: k.Stake)
                        : new TurnEvent(TurnEventKind.StakeLost, -1, country: owner, cell: k.Stake));
                k.Stake = -1;
            }
        }

        /// <summary>
        /// Every town gains its influence and takes cells while it has enough, in rounds: each town
        /// with the price names its next cell, and a cell two towns named goes to the cheaper way,
        /// then to the kingdom with less land (<see cref="Before"/>). A town with nowhere to grow
        /// keeps no more than one cell's worth (<see cref="City.LandFull"/>).
        /// </summary>
        public static void Grow(GameState state, TurnReport report)
        {
            World world = state.World;
            // A town founded on a stake, or a lair's cell won, took it outside the growth.
            DropTakenStakes(world, report);
            foreach (City city in world.Cities)
                city.Influence += Power(city);
            Array.Clear(state.GrownLastTurn, 0, state.GrownLastTurn.Length);
            int[] land = new int[world.Countries.Count];
            foreach (Cell c in world.Cells)
            {
                if (c.Owner >= 0 && c.IsLand)
                    land[c.Owner]++;
            }
            // Every town with the price names its cell; a cell two towns named goes to the cheaper
            // way, then to the kingdom with less land, then to the lower index. The others name
            // another cell in the next round, until nobody takes anything.
            var named = new Dictionary<int, (City City, float Cost)>();
            for (bool took = true; took;)
            {
                took = false;
                named.Clear();
                foreach (City city in world.Cities)
                {
                    if (city.Influence < CellPrice)
                        continue;
                    Dictionary<int, float> walk = Walk(world, city, Reach(city));
                    int cell = Next(world, city, walk);
                    if (cell < 0)
                        continue;
                    float cost = walk[cell];
                    if (!named.TryGetValue(cell, out var rival) || Before(city, cost, rival.City, rival.Cost, land))
                        named[cell] = (city, cost);
                }
                foreach (var pair in named)
                {
                    City city = pair.Value.City;
                    city.Influence -= CellPrice;
                    Take(state, city.Country, pair.Key, report, city.Index);
                    land[city.Country]++;
                    state.Grown[city.Country]++;
                    state.GrownLastTurn[city.Country]++;
                    if (city.Country == world.Player)
                        report.Events.Add(new TurnEvent(TurnEventKind.Border, city.Index, cell: pair.Key));
                    took = true;
                }
            }
            foreach (City city in world.Cities)
            {
                bool full = Next(world, city) < 0;
                if (full)
                    city.Influence = Math.Min(city.Influence, CellPrice);
                if (full && !city.LandFull && city.Country == world.Player)
                    report.Events.Add(new TurnEvent(TurnEventKind.LandFull, city.Index));
                city.LandFull = full;
            }
        }

        /// <summary>
        /// Whether a town's claim on a cell comes before another's: the cheaper way, then the
        /// kingdom with less land, then the lower index.
        /// </summary>
        static bool Before(City city, float cost, City other, float otherCost, int[] land)
        {
            if (Math.Abs(cost - otherCost) > 1e-4f)
                return cost < otherCost;
            if (city.Country != other.Country && land[city.Country] != land[other.Country])
                return land[city.Country] < land[other.Country];
            return city.Country != other.Country ? city.Country < other.Country : city.Index < other.Index;
        }

        /// <summary>
        /// Whether a cell just cleared of monsters goes to the winners' land: wild land beside it or
        /// within one of their towns' reach, and not near another kingdom's town.
        /// </summary>
        public static bool TakesCleared(World world, int country, int cell)
        {
            Cell c = world.Cells[cell];
            if (c.Owner >= 0 || c.IsWater || NearOtherTown(world, country, cell))
                return false;
            for (int d = 0; d < 6; d++)
            {
                int j = world.Neighbor(cell, d);
                if (j >= 0 && world.Cells[j].IsLand && world.Cells[j].Owner == country)
                    return true;
            }
            foreach (int index in world.Countries[country].Cities)
            {
                City city = world.Cities[index];
                int reach = Reach(city);
                Dictionary<int, float> walk = Walk(world, city, reach);
                for (int d = 0; d < 6; d++)
                {
                    int j = world.Neighbor(cell, d);
                    if (j >= 0 && walk.TryGetValue(j, out float cost) && cost + Step(world, j, (d + 3) % 6, cell) <= reach + 1e-4f)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The kingdom takes the cell: the wild shallows beside it are claimed again by the land
        /// round them, and the players see two steps round their new cell.
        /// </summary>
        public static void Take(GameState state, int country, int cell, TurnReport report = null, int town = -1)
        {
            World world = state.World;
            world.Cells[cell].Owner = country;
            report?.Changed.Add(cell);
            DropTakenStakes(world, report, town);
            for (int d = 0; d < 6; d++)
            {
                int j = world.Neighbor(cell, d);
                if (j < 0 || world.Cells[j].Biome != Biome.Shallows || world.Cells[j].Owner >= 0)
                    continue;
                world.Cells[j].Owner = ShoreOwner(world, j);
                if (world.Cells[j].Owner >= 0)
                    report?.Changed.Add(j);
            }
            if (country == world.Player)
                state.Reveal(cell, 2);
        }

        /// <summary>The kingdom with the most land round the shallows, or -1 (the first on a tie).</summary>
        public static int ShoreOwner(World world, int cell)
        {
            Span<int> votes = stackalloc int[16];
            int winner = -1, most = 0;
            for (int d = 0; d < 6; d++)
            {
                int j = world.Neighbor(cell, d);
                if (j < 0 || !world.Cells[j].IsLand || world.Cells[j].Owner < 0)
                    continue;
                int k = world.Cells[j].Owner;
                votes[k]++;
                if (votes[k] > most || (votes[k] == most && k < winner))
                {
                    most = votes[k];
                    winner = k;
                }
            }
            return winner;
        }

        /// <summary>
        /// Roughly how many turns until one of the kingdom's towns takes the wild cell, and which
        /// town (-1 and -1 when none reaches it): the cells the town would take before it are
        /// counted, as the walk's order goes.
        /// </summary>
        public static int TurnsTo(World world, int country, int cell, out int town)
        {
            town = -1;
            int best = -1;
            if (world.Cells[cell].Owner >= 0 || !Open(world, cell) || NearOtherTown(world, country, cell))
                return -1;
            foreach (int index in world.Countries[country].Cities)
            {
                City city = world.Cities[index];
                Dictionary<int, float> walk = Walk(world, city, Reach(city));
                if (!walk.TryGetValue(cell, out float cost))
                    continue;
                float score = Score(world, cell, cost);
                int before = 0;
                foreach (KeyValuePair<int, float> step in walk)
                {
                    if (step.Key != cell && world.Cells[step.Key].Owner < 0 && Open(world, step.Key) && !NearOtherTown(world, country, step.Key)
                        && Score(world, step.Key, step.Value) <= score)
                        before++;
                }
                int power = Math.Max(1, Power(city));
                int turns = Math.Max(1, (int)Math.Ceiling(((before + 1) * CellPrice - city.Influence) / (double)power));
                if (best < 0 || turns < best)
                {
                    best = turns;
                    town = index;
                }
            }
            return best;
        }

        /// <summary>Whether the cell lies within <see cref="HomeCircle"/> steps of another kingdom's town.</summary>
        public static bool NearOtherTown(World world, int country, int cell)
        {
            foreach (City town in world.Cities)
            {
                if (town.Country != country && Hex.Distance(world.Cells[cell].Hex, world.Cells[town.Cell].Hex) <= HomeCircle)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Cuts each kingdom's land at the start to what its towns reach for one step less than
        /// their <see cref="Reach"/>, by the walk over its own land, and the cheapest way from each
        /// town to the capital, so that every town has a ring to grow into from the first turn.
        /// The rest becomes wild. Returns the owners before the cut (the roads between the capitals
        /// are laid by them).
        /// </summary>
        internal static int[] Trim(World world)
        {
            Cell[] cells = world.Cells;
            var before = new int[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                before[i] = cells[i].Owner;
            var keep = new bool[cells.Length];
            foreach (City city in world.Cities)
            {
                foreach (int cell in Walk(world, city, Reach(city) - StartRing, ownOnly: true).Keys)
                    keep[cell] = true;
            }
            foreach (Country country in world.Countries)
            {
                if (country.Capital < 0)
                    continue;
                City capital = world.Cities[country.Capital];
                foreach (int index in country.Cities)
                {
                    if (index == country.Capital)
                        continue;
                    foreach (int cell in OwnPath(world, world.Cities[index].Cell, capital.Cell, country.Index))
                        keep[cell] = true;
                }
            }
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].IsLand && cells[i].Owner >= 0 && !keep[i])
                    cells[i].Owner = -1;
            }
            return before;
        }

        /// <summary>
        /// The bot's stake (the territory plan's bot): when the kingdom has none, on the wild
        /// resource cell nearest to one of its towns by the walk that may stand one. Free, so it
        /// is no purchase of the turn.
        /// </summary>
        public static void BotStake(GameState state, int country)
        {
            if (!state.BotsStake || state.World.Countries[country].Stake >= 0)
                return;
            foreach (int cell in StakeChoices(state.World, country))
            {
                if (state.SetStake(country, cell))
                    return;
            }
        }

        /// <summary>
        /// The wild resources the kingdom's towns reach, nearest first: where a stake serves best,
        /// for the neighbours' stakes and the players' guiding line (the interface plan's 3.3).
        /// </summary>
        public static List<int> StakeChoices(World world, int country)
        {
            var near = new Dictionary<int, float>();
            foreach (int index in world.Countries[country].Cities)
            {
                City city = world.Cities[index];
                foreach (KeyValuePair<int, float> step in Walk(world, city, Reach(city)))
                {
                    Cell c = world.Cells[step.Key];
                    if (c.Owner < 0 && c.Resource != Resource.None && (!near.TryGetValue(step.Key, out float old) || step.Value < old))
                        near[step.Key] = step.Value;
                }
            }
            var cells = new List<int>(near.Keys);
            cells.Sort((a, b) => near[a] != near[b] ? near[a].CompareTo(near[b]) : a.CompareTo(b));
            return cells;
        }

        /// <summary>The cheapest walk from one cell to another over the kingdom's own land (empty when there is none).</summary>
        static List<int> OwnPath(World world, int from, int to, int country)
        {
            var cost = new Dictionary<int, float> { [from] = 0f };
            var previous = new Dictionary<int, int>();
            var heap = new MinHeap();
            heap.Push(0f, from);
            while (heap.Count > 0)
            {
                heap.Pop(out float d, out int i);
                if (d > cost[i])
                    continue;
                if (i == to)
                    break;
                for (int dir = 0; dir < 6; dir++)
                {
                    int j = world.Neighbor(i, dir);
                    if (j < 0 || world.Cells[j].IsWater || world.Cells[j].Owner != country)
                        continue;
                    float next = d + Step(world, i, dir, j);
                    if (!cost.TryGetValue(j, out float old) || next < old)
                    {
                        cost[j] = next;
                        previous[j] = i;
                        heap.Push(next, j);
                    }
                }
            }
            var path = new List<int>();
            if (!cost.ContainsKey(to))
                return path;
            for (int i = to; ; i = previous[i])
            {
                path.Add(i);
                if (i == from)
                    break;
            }
            return path;
        }
    }

    /// <summary>The kingdoms' stakes (the territory plan's П3).</summary>
    public sealed partial class GameState
    {
        /// <summary>How many times the players set their stake (the stake's task asks for one).</summary>
        public int StakesSet;

        /// <summary>
        /// Whether the bots set stakes (<see cref="Influence.BotStake"/>): the neighbours in their
        /// turns and the measurer's players. The measurer turns it off to see what the stake gives.
        /// </summary>
        public bool BotsStake = true;

        /// <summary>
        /// Sets the kingdom's stake on the wild cell, moving it from where it stood, or takes it
        /// away with -1; false, and nothing changes, when it may not stand there
        /// (<see cref="Influence.CanStake"/>).
        /// </summary>
        public bool SetStake(int country, int cell)
        {
            if (cell >= 0 && !Influence.CanStake(World, country, cell))
                return false;
            World.Countries[country].Stake = cell;
            if (cell >= 0 && country == World.Player)
                StakesSet++;
            return true;
        }
    }
}

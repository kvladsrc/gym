using System;
using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>How fast the neighbouring kingdoms build: chosen with a new game (the economy plan's step 2).</summary>
    public enum Difficulty : byte
    {
        /// <summary>A neighbour buys one thing in three turns.</summary>
        Calm,

        /// <summary>Two things in three turns.</summary>
        Normal,

        /// <summary>Three things in two turns.</summary>
        Race,
    }

    /// <summary>
    /// A simple player of a kingdom's economy, for the neighbouring kingdoms (the economy plan of
    /// 2026-11-11, step 2) and for the measurer's players' kingdom; it was the economy tests' bot.
    /// Lumber camps first; a tavern, a quarter for a town with no free place, and walls and a
    /// castle for a town that has stopped at the castle's size; food buildings while the towns
    /// want more food than comes in; then, unless it saves for one of those, a granary, quarries
    /// and gold mines and a market when gold piles up. A neighbour buys at most what the
    /// <see cref="Difficulty"/> allows a turn and puts first what its temper's gem wants
    /// (<see cref="GameState.Aim"/>). It founds no towns; its land grows by itself
    /// (<see cref="Influence"/>). The players' heroes in the measurer and the captures
    /// (<see cref="HeroTurn"/>) hire with what the buildings leave and go after the groups they
    /// clearly beat, the places worth a visit and, with nothing else to do, the edge of the
    /// known land; the neighbours have no heroes.
    /// </summary>
    public static class Rival
    {
        /// <summary>The gems a neighbour goes after, without a hero; its troops clear the lairs for the hunter's (<see cref="Troops"/>).</summary>
        public static readonly Gem[] Pursued = { Gem.Capital, Gem.Towns, Gem.Lands, Gem.Hunter };

        /// <summary>
        /// How much a neighbour buys: <c>things</c> in <c>turns</c>. The measurer set the paces on
        /// 2026-11-11 so that its players' bot sets its crown first on about 95%, 78% and 53% of the
        /// maps; the economy plan wants 70-80% at <see cref="Difficulty.Normal"/>.
        /// </summary>
        public static (int things, int turns) Pace(Difficulty difficulty) =>
            difficulty == Difficulty.Race ? (3, 2) : difficulty == Difficulty.Normal ? (2, 3) : (1, 3);

        /// <summary>What a neighbour may buy on the turn at the <see cref="Pace"/>, spread evenly over the turns.</summary>
        public static int Purchases(Difficulty difficulty, int turn)
        {
            (int things, int turns) = Pace(difficulty);
            return turn * things / turns - (turn - 1) * things / turns;
        }

        /// <summary>What a neighbour may buy on the turn at <paramref name="rate"/> things a turn, spread evenly over the turns.</summary>
        public static int Purchases(float rate, int turn) =>
            (int)Math.Floor(turn * rate + 1e-4) - (int)Math.Floor((turn - 1) * rate + 1e-4);

        /// <summary>
        /// A kingdom's temper, the order in which it goes after the <see cref="Pursued"/> gems: one of
        /// their six orders, picked by the map's seed and the kingdom, so the same map has the same
        /// neighbours and the world itself is not changed.
        /// </summary>
        public static Gem[] AimsFor(int seed, int country)
        {
            uint h = (uint)seed * 0x9E3779B1u ^ (uint)(country + 1) * 0x85EBCA77u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            var left = new List<Gem>(Pursued);
            var aims = new Gem[left.Count];
            for (int i = 0; i < aims.Length; i++)
            {
                int pick = (int)(h % (uint)left.Count);
                h /= (uint)left.Count;
                aims[i] = left[pick];
                left.RemoveAt(pick);
            }
            return aims;
        }

        /// <summary>
        /// Plays the kingdom's turn; returns how many things it built or grew onto. A neighbour makes
        /// at most <paramref name="budget"/> of them and puts first what its <paramref name="aim"/>
        /// wants: for the capital, the capital's needs before the other towns' walls and castles;
        /// for the towns, the smallest towns first; for the lands, markets early, as a market makes
        /// a town's influence stronger. The cells whose look changed go into
        /// <paramref name="changed"/>, the new quarters, whose ground is laid again, into
        /// <paramref name="reshaped"/>.
        /// </summary>
        public static int PlayATurn(GameState state, int country, int budget = int.MaxValue, Gem? aim = null,
            List<int> changed = null, List<int> reshaped = null, TurnReport report = null)
        {
            World world = state.World;
            int done = 0;
            bool saving = false;
            Influence.BotStake(state, country);

            // Each builds or grows when the turn's budget allows; true when only money was missing.
            bool Field(Cell c)
            {
                if (done >= budget)
                    return false;
                Refusal refusal = state.Build(country, c.Index);
                if (refusal == Refusal.None)
                {
                    done++;
                    changed?.Add(c.Index);
                }
                return refusal == Refusal.NotEnough;
            }
            bool InTown(int index, TownBuilding building)
            {
                if (done >= budget)
                    return false;
                Refusal refusal = state.Build(country, index, building);
                if (refusal == Refusal.None)
                {
                    done++;
                    Dress(world.Cities[index]);
                }
                return refusal == Refusal.NotEnough;
            }
            bool Grow(City city)
            {
                if (done >= budget)
                    return false;
                int quarters = city.Districts.Count;
                Refusal refusal = Expand(state, country, city);
                if (refusal == Refusal.None)
                {
                    done++;
                    Dress(city);
                    if (city.Districts.Count > quarters)
                        reshaped?.Add(city.Districts[city.Districts.Count - 1]);
                }
                return refusal == Refusal.NotEnough;
            }
            void Dress(City city)
            {
                changed?.Add(city.Cell);
                changed?.AddRange(city.Districts);
            }
            // A neighbour's troop to the nearest lair; true when one was near but too dear.
            bool SendTroop()
            {
                if (done >= budget || country == world.Player)
                    return false;
                if (!Troops.Send(state, country, out bool tooDear, report))
                    return tooDear;
                done++;
                if (report == null)
                    changed?.Add(Troops.Of(state, country).Lair);
                return false;
            }

            // A neighbour after the hunter's gem sends its troop first and saves for it.
            if (aim == Gem.Hunter)
                saving |= SendTroop();

            var resources = new List<Cell>();
            foreach (Cell c in world.Cells)
            {
                if (c.Owner == country && c.City < 0 && c.Resource != Resource.None && c.Building == FieldBuilding.None)
                    resources.Add(c);
            }
            foreach (Cell c in resources)
            {
                if (c.Resource == Resource.Wood)
                    Field(c);
            }
            List<int> towns = TownOrder(world, country, aim);
            foreach (int index in towns)
            {
                City city = world.Cities[index];
                if (Towns.Limit(city) == GrowthStop.Tavern || city.Houses == Towns.TavernHouses - 1)
                    saving |= InTown(index, TownBuilding.Tavern);
                if (Towns.Free(city) == 0 && city.Houses < Towns.MaxHouses)
                    saving |= Grow(city);
                // A town of the castle's size grows on only behind walls and with a castle; a
                // neighbour after the capital's gem keeps its stone for the capital's.
                if (Towns.Limit(city) == GrowthStop.Castle && (aim != Gem.Capital || city.Capital))
                {
                    saving |= InTown(index, TownBuilding.Walls);
                    saving |= InTown(index, TownBuilding.Castle);
                }
            }
            foreach (Cell c in resources)
            {
                bool food = c.Resource == Resource.Wheat || c.Resource == Resource.Fish || c.Resource == Resource.Sheep;
                if (food && Economy.Income(world, country).Food < Towns.Demand(world, country) + 2)
                    Field(c);
            }
            if (saving)
                return done;
            if (aim == Gem.Lands && state.Treasury[country].Gold > 15)
            {
                foreach (int index in towns)
                    InTown(index, TownBuilding.Market);
            }
            foreach (int index in towns)
                InTown(index, TownBuilding.Granary);
            foreach (Cell c in resources)
            {
                if (c.Building == FieldBuilding.None && (c.Resource == Resource.Stone || c.Resource == Resource.Gold))
                    Field(c);
            }
            if (state.Treasury[country].Gold > 25)
            {
                foreach (int index in towns)
                    InTown(index, TownBuilding.Market);
            }
            // What is left goes to a troop: the neighbours piled up gold before they had one.
            SendTroop();
            return done;
        }

        /// <summary>The kingdom's towns in the order the bot sees to them: the list's own (the capital first), or the smallest first for the towns' gem.</summary>
        static List<int> TownOrder(World world, int country, Gem? aim)
        {
            var towns = new List<int>(world.Countries[country].Cities);
            if (aim == Gem.Towns)
            {
                var rank = new Dictionary<int, int>();
                for (int i = 0; i < towns.Count; i++)
                    rank[towns[i]] = i;
                towns.Sort((a, b) =>
                {
                    int byLevel = Towns.Level(world.Cities[a]).CompareTo(Towns.Level(world.Cities[b]));
                    return byLevel != 0 ? byLevel : rank[a].CompareTo(rank[b]);
                });
            }
            return towns;
        }

        /// <summary>Grows the town onto the first neighbour it may have: the refusal, NotEnough if only money is missing.</summary>
        public static Refusal Expand(GameState state, int country, City city)
        {
            Refusal last = Refusal.NotBesideTown;
            for (int d = 0; d < 6; d++)
            {
                int j = state.World.Neighbor(city.Cell, d);
                if (j < 0)
                    continue;
                Refusal refusal = Towns.CanExpand(state.World, state.Treasury[country], country, j, out _);
                if (refusal == Refusal.None)
                    return state.Expand(country, j);
                if (refusal == Refusal.NotEnough)
                    last = refusal;
            }
            return last;
        }

        // ---- Heroes (the economy plan's step 1b: a hero for the measurer, fights played by the auto battle) ----

        /// <summary>A group the hero attacks is this many times weaker than his army, by the units' worth.</summary>
        public const float Stronger = 1.5f;

        /// <summary>And the odds of beating it, over this many quick fights, are at least <see cref="SureWin"/>.</summary>
        public const int OddsRuns = 40;

        public const float SureWin = 0.6f;

        /// <summary>What the treasury keeps when the bot hires: the buildings come first.</summary>
        public static readonly Stock HireReserve = new Stock(30, 10, 10, 0);

        /// <summary>
        /// Builds the capital's barracks, archery range and stables, in that order, each when the
        /// treasury keeps <see cref="HireReserve"/> after it; for a kingdom with a hero. Returns
        /// how many it built.
        /// </summary>
        public static int Arm(GameState state, int country)
        {
            int capital = state.World.Countries[country].Capital;
            if (capital < 0 || state.HeroesOf(country).Count == 0)
                return 0;
            int built = 0;
            foreach (TownBuilding building in new[] { TownBuilding.Barracks, TownBuilding.ArcheryRange, TownBuilding.Stables })
            {
                if (state.World.Cities[capital].Buildings.Contains(building))
                    continue;
                if (!state.Treasury[country].Covers(Economy.Cost(building) + HireReserve))
                    break;
                if (state.Build(country, capital, building) == Refusal.None)
                    built++;
            }
            return built;
        }

        /// <summary>
        /// Hires in every town of the kingdom what it offers, the strongest units first, as far as
        /// the treasury keeps <see cref="HireReserve"/>. The units join a hero standing in the town,
        /// else its garrison, which a hero takes on when he comes. Returns the units hired.
        /// </summary>
        public static int Hire(GameState state, int country)
        {
            int hired = 0;
            foreach (int index in state.World.Countries[country].Cities)
            {
                for (int u = Units.Hired.Length - 1; u >= 0; u--)
                {
                    Unit unit = Units.Hired[u];
                    int count = Math.Min(state.Affordable(country, index, unit), Spare(state.Treasury[country], Units.Cost(unit)));
                    if (count > 0 && state.Recruit(country, index, unit, count) == Refusal.None)
                        hired += count;
                }
            }
            return hired;
        }

        /// <summary>How many of a cost the treasury pays while keeping the reserve.</summary>
        static int Spare(Stock treasury, Stock cost)
        {
            int count = int.MaxValue;
            void By(int have, int keep, int each)
            {
                if (each > 0)
                    count = Math.Min(count, Math.Max(0, have - keep) / each);
            }
            By(treasury.Gold, HireReserve.Gold, cost.Gold);
            By(treasury.Food, HireReserve.Food, cost.Food);
            By(treasury.Wood, HireReserve.Wood, cost.Wood);
            By(treasury.Stone, HireReserve.Stone, cost.Stone);
            return count == int.MaxValue ? 0 : count;
        }

        /// <summary>What a hero's turn came to.</summary>
        public struct HeroDeeds
        {
            public int Steps, Fights, Won, Visits;
        }

        /// <summary>
        /// The hero's turn: he walks this turn's part of the way to the nearest goal and fights the
        /// group at its end, takes a chest's experience and his level-ups (attack while it is not
        /// above defence). The goals: a group <see cref="Stronger"/> times weaker than his army with
        /// good odds, a pile, chest, shrine he has not prayed at or watchtower with unseen land
        /// round it, and a town of his kingdom whose garrison waits for him; with none in reach, the
        /// nearest seen cell beside unseen land. Cells whose map changed go into
        /// <paramref name="changed"/> when it is given.
        /// </summary>
        public static HeroDeeds HeroTurn(GameState state, Hero hero, List<int> changed = null)
        {
            var deeds = new HeroDeeds();
            HeroPath way = Goal(state, hero) ?? Frontier(state, hero);
            if (way == null)
                return deeds;
            foreach (int cell in way.Cells)
            {
                StepResult step = state.Step(hero, cell);
                if (step.Kind == StepKind.Fight)
                {
                    Battle battle = state.StartBattle(hero, cell, state.OddsOf(hero, cell, OddsRuns));
                    battle.AutoToEnd();
                    state.EndBattle(hero, cell, battle);
                    LevelUps(hero);
                    deeds.Fights++;
                    if (battle.Outcome == Outcome.Won)
                        deeds.Won++;
                    changed?.Add(cell);
                    break;
                }
                if (step.Kind != StepKind.Moved && step.Kind != StepKind.Visited)
                    break;
                deeds.Steps++;
                if (step.Kind == StepKind.Visited)
                {
                    deeds.Visits++;
                    changed?.Add(cell);
                }
                if (step.Chest)
                {
                    state.TakeChest(false);
                    LevelUps(hero);
                }
            }
            return deeds;
        }

        static void LevelUps(Hero hero)
        {
            while (hero.LevelUps > 0)
                GameState.ChooseLevelUp(hero, hero.Attack <= hero.Defence);
        }

        /// <summary>The way to the nearest goal the hero can reach, or null.</summary>
        static HeroPath Goal(GameState state, Hero hero)
        {
            World world = state.World;
            Hex at = world.Cells[hero.Cell].Hex;
            float army = Armies.Worth(hero.Army);
            var goals = new List<int>();
            foreach (Cell c in world.Cells)
            {
                if (c.Index == hero.Cell || !Known(state, hero, c.Index))
                    continue;
                if (c.Monster != Monster.None)
                {
                    if (army > 0 && army >= Stronger * c.Count * Units.Worth(Units.Of(c.Monster)))
                        goals.Add(c.Index);
                }
                else if (WorthAVisit(state, hero, c) || Waiting(state, hero, c, army))
                {
                    goals.Add(c.Index);
                }
            }
            goals.Sort((a, b) => Hex.Distance(at, world.Cells[a].Hex).CompareTo(Hex.Distance(at, world.Cells[b].Hex)));
            foreach (int goal in goals)
            {
                if (world.Cells[goal].Monster != Monster.None && state.OddsOf(hero, goal, OddsRuns).Win < SureWin)
                    continue;
                HeroPath way = state.Path(hero, goal);
                if (way != null)
                    return way;
            }
            return null;
        }

        /// <summary>The players' heroes know the land the players have seen; the other kingdoms' know it all.</summary>
        static bool Known(GameState state, Hero hero, int cell) => hero.Country != state.World.Player || state.Seen[cell];

        static bool WorthAVisit(GameState state, Hero hero, Cell c)
        {
            switch (c.Place)
            {
                case Place.None:
                    return false;
                case Place.ShrineAttack:
                case Place.ShrineDefence:
                    return !hero.Shrines.Contains(c.Index);
                case Place.Watchtower:
                {
                    if (hero.Country != state.World.Player)
                        return false;
                    foreach (Cell o in state.World.Cells)
                    {
                        if (!state.Seen[o.Index] && Hex.Distance(o.Hex, c.Hex) <= Wilds.TowerSight)
                            return true;
                    }
                    return false;
                }
                default:
                    return true;
            }
        }

        /// <summary>A town of the hero's kingdom whose garrison is worth fetching: half his army's worth, or anything when he has none.</summary>
        static bool Waiting(GameState state, Hero hero, Cell c, float army)
        {
            if (c.City < 0)
                return false;
            City town = state.World.Cities[c.City];
            if (town.Country != hero.Country || town.Garrison.Count == 0)
                return false;
            return Armies.Worth(town.Garrison) >= 0.5f * army;
        }

        /// <summary>The way to the nearest known land cell beside unknown land, for the players' heroes; null for the others.</summary>
        static HeroPath Frontier(GameState state, Hero hero)
        {
            if (hero.Country != state.World.Player)
                return null;
            World world = state.World;
            Hex at = world.Cells[hero.Cell].Hex;
            var edge = new List<int>();
            foreach (Cell c in world.Cells)
            {
                if (!state.Seen[c.Index] || !c.IsLand || c.Monster != Monster.None || c.Index == hero.Cell)
                    continue;
                for (int d = 0; d < 6; d++)
                {
                    int j = world.Neighbor(c.Index, d);
                    if (j >= 0 && !state.Seen[j])
                    {
                        edge.Add(c.Index);
                        break;
                    }
                }
            }
            edge.Sort((a, b) => Hex.Distance(at, world.Cells[a].Hex).CompareTo(Hex.Distance(at, world.Cells[b].Hex)));
            foreach (int cell in edge)
            {
                HeroPath way = state.Path(hero, cell);
                if (way != null)
                    return way;
            }
            return null;
        }
    }

    /// <summary>The neighbouring kingdoms' turns (the economy plan's step 2).</summary>
    public sealed partial class GameState
    {
        /// <summary>How fast the neighbours build.</summary>
        public Difficulty Difficulty = Difficulty.Normal;

        /// <summary>
        /// Whether the other kingdoms play their turns at the turn's end. The tests of one kingdom's
        /// rules, and the measurer's look at the game as it was, turn them off.
        /// </summary>
        public bool RivalsPlay = true;

        /// <summary>Each kingdom's temper: the order in which it goes after the gems (<see cref="Rival.AimsFor"/>).</summary>
        public Gem[][] Aims;

        void StartRivals()
        {
            Aims = new Gem[World.Countries.Count][];
            for (int k = 0; k < Aims.Length; k++)
                Aims[k] = Rival.AimsFor(World.Seed, k);
        }

        /// <summary>
        /// The gem the kingdom goes after now: the first of its temper's it has not won and can
        /// still go after (the hunter's while a lair is left for its troops), or null.
        /// </summary>
        public Gem? Aim(int country)
        {
            foreach (Gem gem in Aims[country])
            {
                if (!HasGem(country, gem) && (gem != Gem.Hunter || Core.Troops.Hunting(this, country)))
                    return gem;
            }
            return null;
        }

        /// <summary>Every other kingdom buys what the difficulty allows, before the turn's income, as the players did in their turn.</summary>
        void PlayRivals(TurnReport report)
        {
            if (!RivalsPlay)
                return;
            int budget = Rival.Purchases(Difficulty, Turn);
            for (int k = 0; k < World.Countries.Count; k++)
            {
                if (k != World.Player)
                    Rival.PlayATurn(this, k, budget, Aim(k), report.Changed, report.Reshaped, report);
            }
        }
    }
}

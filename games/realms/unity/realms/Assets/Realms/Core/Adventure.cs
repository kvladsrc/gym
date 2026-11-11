using System;
using System.Collections.Generic;

namespace Realms.Core
{
    public enum StepKind : byte
    {
        /// <summary>The hero walked onto the cell.</summary>
        Moved,

        /// <summary>Not enough moves left this turn.</summary>
        NoMoves,

        /// <summary>The hero cannot go there (water, fog, a foreign town, another hero, no army for a fight).</summary>
        Blocked,

        /// <summary>A monster group stands there: the fight must be played (see <see cref="GameState.StartBattle"/>).</summary>
        Fight,

        /// <summary>The hero walked onto a place and visited it.</summary>
        Visited,

        /// <summary>
        /// Another hero of the kingdom stands there: the two meet from neighbouring cells and may
        /// exchange their units (see <see cref="Armies.Move"/>).
        /// </summary>
        Meet,
    }

    /// <summary>What a hero's step brought.</summary>
    public sealed class StepResult
    {
        public StepKind Kind;
        public Place Place;

        /// <summary>Goods from a pile.</summary>
        public Stock Gained;

        /// <summary>A shrine the hero had already visited: nothing more.</summary>
        public bool Again;

        /// <summary>A chest waits for the players' choice (<see cref="GameState.Chest"/>).</summary>
        public bool Chest;

        /// <summary>The hero took the units waiting in the town.</summary>
        public bool Garrison;

        /// <summary>The hero took the settlers' cart waiting in the town.</summary>
        public bool Settlers;
    }

    /// <summary>An opened chest: gold or experience, the players choose.</summary>
    public sealed class Chest
    {
        public int Hero, Cell, Gold, Experience;
    }

    public sealed class FightReport
    {
        public Outcome Outcome;
        public Monster Monster;
        public bool Lair;
        public int Cell, Killed, Experience, LevelUps;
        public Stock Reward;

        /// <summary>How many times the group's own reward was paid (<see cref="Battle.RewardFactor"/>).</summary>
        public int RewardFactor = 1;

        /// <summary>The monsters gave up and were let go (<see cref="Battle.Surrendered"/>): a win with some left alive.</summary>
        public bool Surrendered;

        /// <summary>After a win: the place on the cell, visited.</summary>
        public StepResult Visit;

        /// <summary>After a loss: the cell of the town the hero went back to.</summary>
        public int Home = -1;

        /// <summary>The loss was a retreat: the hero led the living away (<see cref="Battle.Retreated"/>).</summary>
        public bool Retreated;

        /// <summary>After a loss that left the hero no army: the spearmen waiting for him in the town (<see cref="HeroRules.Militia"/>).</summary>
        public int Militia;

        /// <summary>The cleared cell went into the winners' land (<see cref="Influence.TakesCleared"/>).</summary>
        public bool Took;
    }

    /// <summary>Heroes, fog of war, recruits, places and fights.</summary>
    public sealed partial class GameState
    {
        public readonly List<Hero> Heroes = new List<Hero>();

        /// <summary>Cells the players have seen (the rest is under the clouds).</summary>
        public bool[] Seen;

        public Rng Rng;

        /// <summary>The players' won fights, cleared lairs and opened chests (for the tasks).</summary>
        public int Victories, LairsCleared, ChestsOpened;

        /// <summary>A chest waiting for the players' choice, or null.</summary>
        public Chest Chest;

        void StartAdventure()
        {
            Seen = new bool[World.Cells.Length];
            Rng = new Rng(World.Seed ^ 0x5EED);
            foreach (Cell c in World.Cells)
            {
                if (c.Owner == World.Player)
                    Reveal(c.Index, 2);
            }
            Country player = World.Countries[World.Player];
            if (player.Capital < 0)
                return;
            Hero hero = AddHero(World.Player, World.Cities[player.Capital].Cell);
            hero.Army.Add(new Stack(Unit.Spearmen, HeroRules.StartSpearmen));
            hero.Army.Add(new Stack(Unit.Archers, HeroRules.StartArchers));
            Reveal(hero.Cell, HeroRules.SightOf(World.Cells[hero.Cell]));
        }

        Hero AddHero(int country, int cell)
        {
            int named = 0;
            foreach (Hero h in Heroes)
                named += h.Country == country ? 1 : 0;
            var hero = new Hero
            {
                Index = Heroes.Count, Country = country, Cell = cell,
                Name = HeroRules.Names[((World.Seed + 5 * country + named) & int.MaxValue) % HeroRules.Names.Length],
            };
            Heroes.Add(hero);
            return hero;
        }

        public Hero HeroAt(int cell)
        {
            foreach (Hero h in Heroes)
            {
                if (h.Cell == cell)
                    return h;
            }
            return null;
        }

        public List<Hero> HeroesOf(int country) => Heroes.FindAll(h => h.Country == country);

        /// <summary>The heroes of the same kingdom standing next to the hero: they may exchange units.</summary>
        public List<Hero> Neighbours(Hero hero)
        {
            var near = new List<Hero>();
            for (int d = 0; d < 6; d++)
            {
                int n = World.Neighbor(hero.Cell, d);
                Hero other = n >= 0 ? HeroAt(n) : null;
                if (other != null && other.Country == hero.Country)
                    near.Add(other);
            }
            return near;
        }

        /// <summary>The kingdom's town the hero stands in, or null: he may leave units in its garrison.</summary>
        public City TownOf(Hero hero)
        {
            Cell c = World.Cells[hero.Cell];
            return c.City >= 0 && World.Cities[c.City].Country == hero.Country ? World.Cities[c.City] : null;
        }

        /// <summary>Opens the fog within <paramref name="radius"/> steps of the cell.</summary>
        public void Reveal(int cell, int radius)
        {
            Hex centre = World.Cells[cell].Hex;
            int row0 = centre.Row, col0 = centre.Col;
            for (int row = row0 - radius; row <= row0 + radius; row++)
            for (int col = col0 - radius - 1; col <= col0 + radius + 1; col++)
            {
                int i = World.IndexOf(col, row);
                if (i >= 0 && Hex.Distance(World.Cells[i].Hex, centre) <= radius)
                    Seen[i] = true;
            }
        }

        // ---- Recruits and heroes ----

        /// <summary>Whether the town can hire another hero now.</summary>
        public Refusal CanHireHero(int country, int city)
        {
            City town = World.Cities[city];
            if (town.Country != country)
                return Refusal.NotYours;
            if (!town.Buildings.Contains(TownBuilding.Tavern))
                return Refusal.NoTavern;
            if (HeroesOf(country).Count >= HeroRules.MaxHeroes)
                return Refusal.TooManyHeroes;
            if (HeroAt(town.Cell) != null)
                return Refusal.Occupied;
            return Treasury[country].Gold < HeroRules.HireGold ? Refusal.NotEnough : Refusal.None;
        }

        /// <summary>A town with a tavern hires another hero with a few spearmen.</summary>
        public Refusal HireHero(int country, int city)
        {
            Refusal refusal = CanHireHero(country, city);
            if (refusal != Refusal.None)
                return refusal;
            City town = World.Cities[city];
            Treasury[country].Gold -= HeroRules.HireGold;
            Hero hero = AddHero(country, town.Cell);
            hero.Army.Add(new Stack(Unit.Spearmen, HeroRules.HireSpearmen));
            Armies.Merge(hero.Army, town.Garrison);
            TakeSettlers(hero);
            if (country == World.Player)
                Reveal(hero.Cell, HeroRules.SightOf(World.Cells[hero.Cell]));
            return Refusal.None;
        }

        /// <summary>Whether the town can hire <paramref name="count"/> of the unit now.</summary>
        public Refusal CanRecruit(int country, int city, Unit unit, int count)
        {
            City town = World.Cities[city];
            if (town.Country != country)
                return Refusal.NotYours;
            if (!town.Buildings.Contains(Units.Building(unit)))
                return Refusal.NoBuilding;
            if (count <= 0 || count > town.Recruits[(int)unit])
                return Refusal.NoRecruits;
            if (!Armies.CanAdd(ArmyFor(town, unit), unit))
                return Refusal.NoRoom;
            return Treasury[country].Covers(Units.Cost(unit) * count) ? Refusal.None : Refusal.NotEnough;
        }

        /// <summary>The army hired units join: the hero's in the town when there is room, else the garrison.</summary>
        List<Stack> ArmyFor(City town, Unit unit)
        {
            Hero hero = HeroAt(town.Cell);
            return hero != null && hero.Country == town.Country && Armies.CanAdd(hero.Army, unit) ? hero.Army : town.Garrison;
        }

        public Refusal Recruit(int country, int city, Unit unit, int count)
        {
            Refusal refusal = CanRecruit(country, city, unit, count);
            if (refusal != Refusal.None)
                return refusal;
            City town = World.Cities[city];
            Treasury[country] -= Units.Cost(unit) * count;
            town.Recruits[(int)unit] -= count;
            Armies.Add(ArmyFor(town, unit), unit, count);
            return Refusal.None;
        }

        /// <summary>How many of the unit the town can hire now: its recruits, as far as the treasury pays.</summary>
        public int Affordable(int country, int city, Unit unit)
        {
            City town = World.Cities[city];
            if (town.Country != country || !town.Buildings.Contains(Units.Building(unit)))
                return 0;
            Stock cost = Units.Cost(unit), t = Treasury[country];
            int count = town.Recruits[(int)unit];
            if (cost.Gold > 0) count = Math.Min(count, t.Gold / cost.Gold);
            if (cost.Food > 0) count = Math.Min(count, t.Food / cost.Food);
            if (cost.Wood > 0) count = Math.Min(count, t.Wood / cost.Wood);
            if (cost.Stone > 0) count = Math.Min(count, t.Stone / cost.Stone);
            return Math.Max(0, count);
        }

        void GrowRecruits()
        {
            foreach (City town in World.Cities)
            {
                foreach (Unit unit in Units.Hired)
                {
                    if (town.Buildings.Contains(Units.Building(unit)))
                        town.Recruits[(int)unit] = Math.Min(town.Recruits[(int)unit] + Units.PerTurn(unit), Units.PoolLimit(unit));
                }
            }
        }

        // ---- Moving ----

        /// <summary>
        /// Whether the hero may enter the cell; a monster group's cell and a fellow hero's (to meet
        /// him) only as the end of a path.
        /// </summary>
        public bool CanEnter(Hero hero, int cell, bool end)
        {
            Cell c = World.Cells[cell];
            if (c.IsWater)
                return false;
            if (hero.Country == World.Player && !Seen[cell])
                return false;
            if (c.City >= 0 && World.Cities[c.City].Country != hero.Country)
                return false;
            Hero other = HeroAt(cell);
            if (other != null && other != hero)
                return end && other.Country == hero.Country;
            return c.Monster == Monster.None || end;
        }

        /// <summary>The cheapest way for the hero to the cell, or null.</summary>
        public HeroPath Path(Hero hero, int target)
        {
            if (target < 0 || target == hero.Cell || !CanEnter(hero, target, true))
                return null;
            int n = World.Cells.Length;
            var cost = new int[n];
            var previous = new int[n];
            for (int i = 0; i < n; i++)
            {
                cost[i] = int.MaxValue;
                previous[i] = -1;
            }
            var heap = new MinHeap();
            cost[hero.Cell] = 0;
            heap.Push(0, hero.Cell);
            while (heap.Count > 0)
            {
                heap.Pop(out float d, out int i);
                if (d > cost[i])
                    continue;
                if (i == target)
                    break;
                for (int dir = 0; dir < 6; dir++)
                {
                    int j = World.Neighbor(i, dir);
                    if (j < 0 || !CanEnter(hero, j, j == target))
                        continue;
                    int next = cost[i] + HeroRules.StepCost(World, i, dir);
                    if (next < cost[j])
                    {
                        cost[j] = next;
                        previous[j] = i;
                        heap.Push(next, j);
                    }
                }
            }
            if (cost[target] == int.MaxValue)
                return null;
            var path = new HeroPath();
            for (int i = target; i != hero.Cell; i = previous[i])
                path.Cells.Add(i);
            path.Cells.Reverse();
            int left = hero.Moves, turn = 0, from = hero.Cell;
            foreach (int cell in path.Cells)
            {
                int step = HeroRules.StepCost(World, from, Direction(from, cell));
                if (step > left)
                {
                    turn++;
                    left = HeroRules.MovesPerTurn;
                }
                left -= step;
                path.Turns.Add(turn);
                from = cell;
            }
            return path;
        }

        /// <summary>The edge from one cell to its neighbour, or -1.</summary>
        public int Direction(int from, int to)
        {
            for (int d = 0; d < 6; d++)
            {
                if (World.Neighbor(from, d) == to)
                    return d;
            }
            return -1;
        }

        /// <summary>The hero steps onto a neighbouring cell, or finds a fight there.</summary>
        public StepResult Step(Hero hero, int cell)
        {
            var result = new StepResult { Kind = StepKind.Blocked };
            int dir = Direction(hero.Cell, cell);
            if (dir < 0 || !CanEnter(hero, cell, true))
                return result;
            int cost = HeroRules.StepCost(World, hero.Cell, dir);
            if (cost > hero.Moves)
            {
                result.Kind = StepKind.NoMoves;
                return result;
            }
            if (World.Cells[cell].Monster != Monster.None)
            {
                if (hero.Army.Count > 0)
                    result.Kind = StepKind.Fight;
                return result;
            }
            if (HeroAt(cell) != null)
            {
                result.Kind = StepKind.Meet;
                return result;
            }
            hero.Moves -= cost;
            hero.Cell = cell;
            Arrive(hero, result);
            return result;
        }

        void Arrive(Hero hero, StepResult result)
        {
            Cell c = World.Cells[hero.Cell];
            if (hero.Country == World.Player)
                Reveal(hero.Cell, HeroRules.SightOf(c));
            if (c.City >= 0 && World.Cities[c.City].Garrison.Count > 0)
            {
                int before = World.Cities[c.City].Garrison.Count;
                Armies.Merge(hero.Army, World.Cities[c.City].Garrison);
                result.Garrison = World.Cities[c.City].Garrison.Count < before;
            }
            result.Settlers = TakeSettlers(hero);
            result.Kind = StepKind.Moved;
            if (c.Place != Place.None)
                Visit(hero, result);
        }

        void Visit(Hero hero, StepResult result)
        {
            Cell c = World.Cells[hero.Cell];
            result.Kind = StepKind.Visited;
            result.Place = c.Place;
            switch (c.Place)
            {
                case Place.Chest:
                    Chest = new Chest
                    {
                        Hero = hero.Index, Cell = c.Index, Gold = Rng.Range(8, 16), Experience = 10 * Rng.Range(8, 16),
                    };
                    result.Chest = true;
                    c.Place = Place.None;
                    break;
                case Place.WoodPile:
                    result.Gained = new Stock(0, 0, Rng.Range(5, 9), 0);
                    c.Place = Place.None;
                    break;
                case Place.StonePile:
                    result.Gained = new Stock(0, 0, 0, Rng.Range(4, 7));
                    c.Place = Place.None;
                    break;
                case Place.GoldPile:
                    result.Gained = new Stock(Rng.Range(8, 13), 0, 0, 0);
                    c.Place = Place.None;
                    break;
                case Place.ShrineAttack:
                case Place.ShrineDefence:
                    if (hero.Shrines.Contains(c.Index))
                    {
                        result.Again = true;
                        break;
                    }
                    hero.Shrines.Add(c.Index);
                    if (c.Place == Place.ShrineAttack)
                        hero.Attack++;
                    else
                        hero.Defence++;
                    break;
                case Place.Watchtower:
                    if (hero.Country == World.Player)
                        Reveal(c.Index, Wilds.TowerSight);
                    break;
            }
            Treasury[hero.Country] += result.Gained;
        }

        /// <summary>Takes the waiting chest's gold or its experience.</summary>
        public void TakeChest(bool gold)
        {
            if (Chest == null)
                return;
            Hero hero = Heroes[Chest.Hero];
            if (gold)
                Treasury[hero.Country].Gold += Chest.Gold;
            else
                Gain(hero, Chest.Experience);
            if (hero.Country == World.Player)
                ChestsOpened++;
            Chest = null;
        }

        /// <summary>Adds experience; returns the levels gained (each waits for its card).</summary>
        public static int Gain(Hero hero, int experience)
        {
            hero.Experience += experience;
            int levels = 0;
            while (hero.Experience >= HeroRules.NextLevel(hero.Level))
            {
                hero.Level++;
                hero.LevelUps++;
                levels++;
            }
            return levels;
        }

        /// <summary>Spends a waiting level-up on +1 attack or +1 defence.</summary>
        public static bool ChooseLevelUp(Hero hero, bool attack)
        {
            if (hero.LevelUps <= 0)
                return false;
            hero.LevelUps--;
            if (attack)
                hero.Attack++;
            else
                hero.Defence++;
            return true;
        }

        // ---- Fights ----

        /// <summary>
        /// The fight with the group on the cell. Its reward's factor comes from the odds shown
        /// before it (<see cref="Odds.RewardFactor"/>), worked out here when they are not given.
        /// </summary>
        public Battle StartBattle(Hero hero, int cell, Odds? odds = null)
        {
            Cell c = World.Cells[cell];
            float win = (odds ?? OddsOf(hero, cell)).Win;
            return new Battle(hero.Army, hero.Attack, hero.Defence, c.Monster, c.Count, c.Biome, Rng.NextInt())
            {
                RewardFactor = Odds.RewardFactor(win),
            };
        }

        /// <summary>What beating the group on the cell would pay, by the odds of the fight.</summary>
        public Stock RewardOf(int cell, Odds odds)
        {
            Cell c = World.Cells[cell];
            return Reward(c.Monster, c.Count, c.Lair) * Odds.RewardFactor(odds.Win);
        }

        /// <summary>The odds of the hero's fight with the group on the cell; the same for the same turn and armies.</summary>
        public Odds OddsOf(Hero hero, int cell, int runs = 200)
        {
            Cell c = World.Cells[cell];
            int seed = World.Seed * 31 + Turn * 7919 + cell * 104729 + c.Count;
            foreach (Stack s in hero.Army)
                seed = seed * 17 + s.Count;
            return Odds.Of(hero.Army, hero.Attack, hero.Defence, c.Monster, c.Count, c.Biome, seed, runs);
        }

        /// <summary>What beating the group gives: gold for its size, goods from a lair.</summary>
        public static Stock Reward(Monster monster, int count, bool lair)
        {
            var reward = new Stock(Math.Max(2, (int)MathF.Round(count * Units.Of(Units.Of(monster)).Health / 12f)), 0, 0, 0);
            if (lair)
            {
                switch (monster)
                {
                    case Monster.Boar: reward += new Stock(5, 10, 0, 0); break;
                    case Monster.Wolves: reward += new Stock(10, 5, 5, 0); break;
                    case Monster.Troll: reward += new Stock(20, 0, 5, 10); break;
                    default: reward += new Stock(10, 0, 0, 0); break;
                }
            }
            return reward;
        }

        /// <summary>
        /// Applies a finished fight: the hero's survivors and experience; after a win the group is
        /// gone, its reward paid (times the fight's factor) and the hero stands on its cell; after
        /// a loss the group keeps its survivors and the hero goes back to the nearest town, out of
        /// moves, with what a retreat saved, or with the militia when nothing was left.
        /// </summary>
        public FightReport EndBattle(Hero hero, int cell, Battle battle)
        {
            Cell c = World.Cells[cell];
            var report = new FightReport
            {
                Outcome = battle.Outcome, Monster = c.Monster, Lair = c.Lair, Cell = cell, Surrendered = battle.Surrendered,
            };
            int before = c.Count, left = battle.MonstersLeft();
            battle.Survivors(hero.Army);
            report.Killed = before - left;
            report.Experience = battle.Experience;
            report.LevelUps = Gain(hero, battle.Experience);
            if (battle.Outcome == Outcome.Won)
            {
                report.RewardFactor = battle.RewardFactor;
                report.Reward = Reward(c.Monster, before, c.Lair) * battle.RewardFactor;
                Treasury[hero.Country] += report.Reward;
                if (c.Lair)
                    Lairs[hero.Country]++;
                if (hero.Country == World.Player)
                {
                    Victories++;
                    if (c.Lair)
                        LairsCleared++;
                }
                c.Monster = Monster.None;
                c.Count = c.FirstCount = 0;
                c.Lair = c.Band = false;
                if (Influence.TakesCleared(World, hero.Country, cell))
                {
                    Influence.Take(this, hero.Country, cell);
                    report.Took = true;
                }
                int dir = Direction(hero.Cell, cell);
                hero.Moves = Math.Max(0, hero.Moves - (dir >= 0 ? HeroRules.StepCost(World, hero.Cell, dir) : 0));
                hero.Cell = cell;
                report.Visit = new StepResult();
                Arrive(hero, report.Visit);
            }
            else
            {
                c.Count = Math.Max(1, left);
                report.Retreated = battle.Retreated;
                hero.Moves = 0;
                hero.Cell = report.Home = HomeFor(hero);
                if (hero.Army.Count == 0)
                {
                    hero.Army.Add(new Stack(Unit.Spearmen, HeroRules.Militia));
                    report.Militia = HeroRules.Militia;
                }
                Arrive(hero, new StepResult());
            }
            return report;
        }

        /// <summary>The kingdom's town nearest to the hero with no other hero in it (the capital at worst).</summary>
        int HomeFor(Hero hero)
        {
            Country country = World.Countries[hero.Country];
            Hex at = World.Cells[hero.Cell].Hex;
            int best = World.Cities[country.Capital].Cell, distance = int.MaxValue;
            foreach (int index in country.Cities)
            {
                int cell = World.Cities[index].Cell;
                Hero other = HeroAt(cell);
                if (other != null && other != hero)
                    continue;
                int d = Hex.Distance(at, World.Cells[cell].Hex);
                if (d < distance)
                {
                    distance = d;
                    best = cell;
                }
            }
            return best;
        }

        /// <summary>
        /// Every fourth turn the groups grow by a sixth of their first size, up to twice that; the
        /// bands out on raids do not.
        /// </summary>
        bool GrowMonsters()
        {
            if (Turn < 2 || (Turn - 1) % Wilds.GrowthTurns != 0)
                return false;
            int year = (Turn - 1) / Wilds.GrowthTurns;
            foreach (Cell c in World.Cells)
            {
                if (c.Monster == Monster.None || c.Band)
                    continue;
                int grow = c.FirstCount * year / 6 - c.FirstCount * (year - 1) / 6;
                c.Count = Math.Min(2 * c.FirstCount, c.Count + grow);
            }
            return true;
        }
    }
}

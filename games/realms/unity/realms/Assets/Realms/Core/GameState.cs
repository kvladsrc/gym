using System;
using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>The small tasks that lead the players through the game, in the order they are offered.</summary>
    public enum Quest : byte
    {
        Farm,
        LumberCamp,
        Fight,
        Chest,
        Quarry,
        Barracks,
        Granary,
        Town,
        Market,
        Lair,
        Tavern,
        GoldMine,
        ArcheryRange,
        HeroLevel,
        City,
        Stables,
        Settle,
        Walls,
        Stake,
    }

    public static class Quests
    {
        public static readonly Quest[] Order =
        {
            Quest.Farm, Quest.LumberCamp, Quest.Fight, Quest.Chest, Quest.Stake, Quest.Quarry, Quest.Barracks,
            Quest.Granary, Quest.Town, Quest.Market, Quest.Settle, Quest.Lair, Quest.Tavern, Quest.GoldMine, Quest.ArcheryRange,
            Quest.HeroLevel, Quest.City, Quest.Stables, Quest.Walls,
        };

        /// <summary>The hero level the <see cref="Quest.HeroLevel"/> task asks for.</summary>
        public const int HeroLevelGoal = 3;

        /// <summary>How many tasks are offered at a time.</summary>
        public const int Shown = 3;

        public static Stock Reward(Quest quest)
        {
            switch (quest)
            {
                case Quest.Farm: return new Stock(5, 0, 0, 0);
                case Quest.LumberCamp: return new Stock(0, 5, 0, 0);
                case Quest.Fight: return new Stock(5, 0, 5, 0);
                case Quest.Chest: return new Stock(0, 0, 0, 5);
                case Quest.Quarry: return new Stock(5, 0, 0, 0);
                case Quest.Barracks: return new Stock(0, 5, 0, 0);
                case Quest.Granary: return new Stock(0, 5, 0, 0);
                case Quest.Town: return new Stock(10, 0, 0, 0);
                case Quest.Market: return new Stock(0, 0, 5, 0);
                case Quest.Lair: return new Stock(10, 0, 0, 5);
                case Quest.Tavern: return new Stock(0, 0, 0, 5);
                case Quest.GoldMine: return new Stock(0, 10, 0, 0);
                case Quest.ArcheryRange: return new Stock(0, 0, 5, 0);
                case Quest.HeroLevel: return new Stock(15, 0, 0, 0);
                case Quest.City: return new Stock(20, 0, 0, 0);
                case Quest.Stables: return new Stock(10, 5, 0, 0);
                case Quest.Settle: return new Stock(10, 10, 0, 0);
                case Quest.Walls: return new Stock(5, 0, 0, 5);
                case Quest.Stake: return new Stock(0, 0, 5, 0);
                default: return default;
            }
        }

        /// <summary>Whether the kingdom has done what the task asks.</summary>
        public static bool Reached(GameState state, int country, Quest quest)
        {
            World world = state.World;
            switch (quest)
            {
                case Quest.Farm: return HasField(world, country, FieldBuilding.Farm);
                case Quest.LumberCamp: return HasField(world, country, FieldBuilding.LumberCamp);
                case Quest.Quarry: return HasField(world, country, FieldBuilding.Quarry);
                case Quest.GoldMine: return HasField(world, country, FieldBuilding.GoldMine);
                case Quest.Granary: return HasTown(world, country, TownBuilding.Granary);
                case Quest.Market: return HasTown(world, country, TownBuilding.Market);
                case Quest.Tavern: return HasTown(world, country, TownBuilding.Tavern);
                case Quest.Barracks: return HasTown(world, country, TownBuilding.Barracks);
                case Quest.ArcheryRange: return HasTown(world, country, TownBuilding.ArcheryRange);
                case Quest.Stables: return HasTown(world, country, TownBuilding.Stables);
                case Quest.Walls: return HasTown(world, country, TownBuilding.Walls);
                case Quest.Fight: return state.Victories > 0;
                case Quest.Chest: return state.ChestsOpened > 0;
                case Quest.Lair: return state.LairsCleared > 0;
                case Quest.Settle: return state.TownsFounded > 0;
                case Quest.Stake: return state.StakesSet > 0;
                case Quest.HeroLevel:
                    foreach (Hero hero in state.Heroes)
                    {
                        if (hero.Country == country && hero.Level >= HeroLevelGoal)
                            return true;
                    }
                    return false;
                case Quest.Town:
                    foreach (int index in world.Countries[country].Cities)
                    {
                        if (!world.Cities[index].Capital && Towns.Level(world.Cities[index]) >= 2)
                            return true;
                    }
                    return false;
                case Quest.City:
                    foreach (int index in world.Countries[country].Cities)
                    {
                        if (Towns.Level(world.Cities[index]) >= 3)
                            return true;
                    }
                    return false;
                default: return false;
            }
        }

        static bool HasField(World world, int country, FieldBuilding building)
        {
            foreach (Cell cell in world.Cells)
            {
                if (cell.Owner == country && cell.Building == building)
                    return true;
            }
            return false;
        }

        static bool HasTown(World world, int country, TownBuilding building)
        {
            foreach (int index in world.Countries[country].Cities)
            {
                if (world.Cities[index].Buildings.Contains(building))
                    return true;
            }
            return false;
        }
    }

    public enum TurnEventKind : byte
    {
        /// <summary>A town got a house.</summary>
        Grew,

        /// <summary>A town got a house and reached a new level.</summary>
        LevelUp,

        /// <summary>A town stopped growing (see <see cref="TurnEvent.Stop"/>).</summary>
        Stopped,

        /// <summary>A task was done and its reward paid.</summary>
        QuestDone,

        /// <summary>The monster groups grew (every fourth turn).</summary>
        MonstersGrew,

        /// <summary>A kingdom set a gem in its crown (see <see cref="TurnEvent.Gem"/> and <see cref="TurnEvent.Country"/>).</summary>
        GemSet,

        /// <summary>A town took a wild cell into the kingdom's land (see <see cref="TurnEvent.Cell"/>).</summary>
        Border,

        /// <summary>A town found no wild cell left to take.</summary>
        LandFull,

        /// <summary>The players' town took the cell their stake stood on (see <see cref="TurnEvent.Cell"/>).</summary>
        StakeReached,

        /// <summary>
        /// Another kingdom took the cell the players' stake stood on first (see
        /// <see cref="TurnEvent.Country"/>); <see cref="TurnEvent.City"/> is -1.
        /// </summary>
        StakeLost,

        /// <summary>A lair sent a band against the players' town (<see cref="TurnEvent.Cell"/> is where it came out).</summary>
        RaidSent,

        /// <summary>A band stands at the players' town without walls: it does not grow for a while.</summary>
        RaidArrived,

        /// <summary>The town's walls beat a band off (<see cref="TurnEvent.Amount"/> gold of loot).</summary>
        RaidRepelled,

        /// <summary>A band went back to its lair.</summary>
        RaidLeft,

        /// <summary>
        /// A neighbour's troop came to a lair the players have seen (<see cref="TurnEvent.Country"/>,
        /// <see cref="TurnEvent.Cell"/>; <see cref="TurnEvent.Amount"/> turns until it clears it).
        /// </summary>
        TroopSent,

        /// <summary>A neighbour's troop cleared a lair the players have seen.</summary>
        TroopCleared,

        /// <summary>The players cleared the lair first: the neighbour's troop went home (<see cref="TurnEvent.Amount"/> gold back to it).</summary>
        TroopRecalled,
    }

    public readonly struct TurnEvent
    {
        public readonly TurnEventKind Kind;
        public readonly int City;
        public readonly GrowthStop Stop;
        public readonly Quest Quest;
        public readonly Gem Gem;
        public readonly int Country;
        public readonly int Cell;
        public readonly Monster Monster;
        public readonly int Amount;

        public TurnEvent(TurnEventKind kind, int city, GrowthStop stop = GrowthStop.None, Quest quest = default,
            Gem gem = default, int country = -1, int cell = -1, Monster monster = Monster.None, int amount = 0)
        {
            Monster = monster;
            Amount = amount;
            Kind = kind;
            City = city;
            Stop = stop;
            Quest = quest;
            Gem = gem;
            Country = country;
            Cell = cell;
        }
    }

    /// <summary>What happened in a turn, for the players' kingdom.</summary>
    public sealed class TurnReport
    {
        /// <summary>The kingdom's income this turn, before the towns took their food.</summary>
        public Stock Income;

        /// <summary>Food the towns took from the treasury for their growth.</summary>
        public int GrowthFood;

        public readonly List<TurnEvent> Events = new List<TurnEvent>();

        /// <summary>Cells of every kingdom whose town or owner changed and must be dressed again.</summary>
        public readonly List<int> Changed = new List<int>();

        /// <summary>The other kingdoms' new quarters: the ground round them is laid again.</summary>
        public readonly List<int> Reshaped = new List<int>();
    }

    /// <summary>A game in progress: the world, the turn, every kingdom's treasury and the players' tasks.</summary>
    public sealed partial class GameState
    {
        public readonly World World;
        public readonly Stock[] Treasury;

        /// <summary>Cells each kingdom's towns have taken by their influence (<see cref="Influence.Grow"/>).</summary>
        public readonly int[] Grown;

        /// <summary>Cells each kingdom's towns took by their influence at the end of the last turn.</summary>
        public readonly int[] GrownLastTurn;

        /// <summary>The players' finished tasks.</summary>
        public readonly List<Quest> Done = new List<Quest>();

        public int Turn = 1;

        public GameState(World world) : this(world, true)
        {
        }

        /// <summary>
        /// The game's start on the world, or with <paramref name="start"/> false a blank state for
        /// <see cref="Save.Read"/> to fill: no hero and nothing seen.
        /// </summary>
        GameState(World world, bool start)
        {
            World = world;
            Treasury = new Stock[world.Countries.Count];
            Grown = new int[world.Countries.Count];
            GrownLastTurn = new int[world.Countries.Count];
            for (int k = 0; k < Treasury.Length; k++)
                Treasury[k] = Economy.Start;
            if (start)
                StartAdventure();
            else
            {
                Seen = new bool[world.Cells.Length];
                Rng = new Rng(world.Seed ^ 0x5EED);
            }
            StartCrown();
            StartRivals();
            InitRaids();
            InitTroops();
        }

        /// <summary>A blank state on the world for <see cref="Save.Read"/> to fill.</summary>
        internal static GameState Blank(World world) => new GameState(world, false);

        /// <summary>Puts the field building the cell's resource needs on it and pays for it.</summary>
        public Refusal Build(int country, int cell)
        {
            Refusal refusal = Economy.CanBuild(World, Treasury[country], country, cell);
            if (refusal != Refusal.None)
                return refusal;
            Cell c = World.Cells[cell];
            c.Building = Economy.BuildingFor(c.Resource);
            Treasury[country] -= Economy.Cost(c.Building);
            return Refusal.None;
        }

        /// <summary>Builds <paramref name="building"/> in the kingdom's town and pays for it.</summary>
        public Refusal Build(int country, int city, TownBuilding building)
        {
            City town = World.Cities[city];
            if (town.Country != country)
                return Refusal.NotYours;
            Refusal refusal = Economy.CanBuild(town, Treasury[country], building);
            if (refusal != Refusal.None)
                return refusal;
            // The building takes the first free place, after the houses, so nothing already standing moves.
            if (Economy.TakesSlot(building))
                town.Places[building] = Array.IndexOf(Towns.Layout(town), Towns.FreeLot);
            town.Buildings.Add(building);
            Treasury[country] -= Economy.Cost(building);
            // A new army building has its first turn's recruits ready.
            if (Economy.Hires(building) is Unit unit)
                town.Recruits[(int)unit] = Units.PerTurn(unit);
            return Refusal.None;
        }

        /// <summary>The town beside the cell grows onto it as a new quarter (see <see cref="Towns.CanExpand"/>).</summary>
        public Refusal Expand(int country, int cell)
        {
            Refusal refusal = Towns.CanExpand(World, Treasury[country], country, cell, out int town);
            if (refusal != Refusal.None)
                return refusal;
            World.Cells[cell].District = town;
            World.Cities[town].Districts.Add(cell);
            Towns.Join(World, World.Cities[town], cell);
            Treasury[country] -= Towns.DistrictCost;
            return Refusal.None;
        }

        /// <summary>The tasks on offer: the first unfinished ones.</summary>
        public List<Quest> Offered()
        {
            var offered = new List<Quest>();
            foreach (Quest quest in Quests.Order)
            {
                if (offered.Count == Quests.Shown)
                    break;
                if (!Done.Contains(quest))
                    offered.Add(quest);
            }
            return offered;
        }

        /// <summary>Marks the tasks the players have just done and pays their rewards.</summary>
        public List<Quest> CheckQuests()
        {
            var finished = new List<Quest>();
            int player = World.Player;
            foreach (Quest quest in Quests.Order)
            {
                if (Done.Contains(quest) || !Quests.Reached(this, player, quest))
                    continue;
                Done.Add(quest);
                Treasury[player] += Quests.Reward(quest);
                finished.Add(quest);
            }
            return finished;
        }

        /// <summary>
        /// Ends the turn: the other kingdoms play theirs (<see cref="PlayRivals"/>), every kingdom
        /// gets its income, then its towns take food for their growth in the order of the kingdom's
        /// list (the capital first) while the treasury has food.
        /// </summary>
        public TurnReport EndTurn()
        {
            var report = new TurnReport();
            Core.Troops.Play(this, report);
            PlayRivals(report);
            for (int k = 0; k < Treasury.Length; k++)
            {
                Stock income = Economy.Income(World, k);
                Treasury[k] += income;
                int eaten = Grow(k, report, k == World.Player);
                if (k == World.Player)
                {
                    report.Income = income;
                    report.GrowthFood = eaten;
                }
            }
            Influence.Grow(this, report);
            foreach (Hero hero in Heroes)
                hero.Moves = HeroRules.MovesPerTurn;
            GrowRecruits();
            Turn++;
            if (GrowMonsters())
                report.Events.Add(new TurnEvent(TurnEventKind.MonstersGrew, -1));
            Core.Raids.Play(this, report);
            foreach (Quest quest in CheckQuests())
                report.Events.Add(new TurnEvent(TurnEventKind.QuestDone, -1, quest: quest));
            foreach ((int country, Gem gem) in CheckCrown())
                report.Events.Add(new TurnEvent(TurnEventKind.GemSet, -1, gem: gem, country: country));
            return report;
        }

        int Grow(int country, TurnReport report, bool tell)
        {
            int eaten = 0;
            foreach (int index in World.Countries[country].Cities)
            {
                City city = World.Cities[index];
                bool raided = Core.Raids.AtTown(this, index);
                GrowthStop stop = raided ? GrowthStop.Raid : Towns.Limit(city);
                if (raided)
                    RaidTurns[country]++;
                if (stop == GrowthStop.None)
                {
                    int want = Math.Min(Towns.IntakeOf(city), Towns.NextHouse(city) - city.Growth);
                    int take = Math.Min(want, Math.Max(0, Treasury[country].Food));
                    Treasury[country].Food -= take;
                    city.Growth += take;
                    eaten += take;
                    if (city.Growth >= Towns.NextHouse(city))
                    {
                        int level = Towns.Level(city);
                        city.Houses++;
                        city.Growth = 0;
                        report.Changed.Add(city.Cell);
                        report.Changed.AddRange(city.Districts);
                        if (tell)
                            report.Events.Add(new TurnEvent(Towns.Level(city) > level ? TurnEventKind.LevelUp : TurnEventKind.Grew, index));
                    }
                    else if (take < want)
                    {
                        stop = GrowthStop.Food;
                    }
                }
                if (tell && stop != GrowthStop.None && stop != city.Stop)
                    report.Events.Add(new TurnEvent(TurnEventKind.Stopped, index, stop));
                city.Stop = stop;
            }
            return eaten;
        }
    }
}

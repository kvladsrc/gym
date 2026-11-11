using System;
using System.Collections.Generic;

namespace Realms.Core
{
    /// <summary>Goods of a kingdom.</summary>
    public struct Stock
    {
        public int Gold, Food, Wood, Stone;

        public Stock(int gold, int food, int wood, int stone)
        {
            Gold = gold;
            Food = food;
            Wood = wood;
            Stone = stone;
        }

        public static Stock operator +(Stock a, Stock b) =>
            new Stock(a.Gold + b.Gold, a.Food + b.Food, a.Wood + b.Wood, a.Stone + b.Stone);

        public static Stock operator -(Stock a, Stock b) =>
            new Stock(a.Gold - b.Gold, a.Food - b.Food, a.Wood - b.Wood, a.Stone - b.Stone);

        public static Stock operator *(Stock a, int k) => new Stock(a.Gold * k, a.Food * k, a.Wood * k, a.Stone * k);

        public bool IsEmpty => Gold == 0 && Food == 0 && Wood == 0 && Stone == 0;

        /// <summary>Whether this stock pays for <paramref name="cost"/>.</summary>
        public bool Covers(Stock cost) => Gold >= cost.Gold && Food >= cost.Food && Wood >= cost.Wood && Stone >= cost.Stone;

        /// <summary>What is missing to pay for <paramref name="cost"/>.</summary>
        public Stock Missing(Stock cost) => new Stock(Math.Max(0, cost.Gold - Gold), Math.Max(0, cost.Food - Food),
            Math.Max(0, cost.Wood - Wood), Math.Max(0, cost.Stone - Stone));

        public override string ToString() => $"gold {Gold}, food {Food}, wood {Wood}, stone {Stone}";
    }

    /// <summary>A building on a resource cell: one per cell, the one its resource needs.</summary>
    public enum FieldBuilding : byte
    {
        None,
        Farm,
        Fishery,
        Pasture,
        LumberCamp,
        Quarry,
        GoldMine,
    }

    /// <summary>
    /// A building in a town: most take one of its places (see <see cref="Towns.Places"/>), the
    /// walls and the castle (the fortifications) stand round and in the middle of the town and take none.
    /// </summary>
    public enum TownBuilding : byte
    {
        Market,
        Granary,
        Tavern,
        Barracks,
        ArcheryRange,
        Stables,
        Walls,
        Castle,
    }

    /// <summary>Why something cannot be built or bought.</summary>
    public enum Refusal : byte
    {
        None,
        NotYours,
        NoResource,
        AlreadyBuilt,
        NotEnough,
        LevelTooLow,
        NoSlot,
        NoTavern,
        TooManyHeroes,
        Occupied,
        NoBuilding,
        NoRecruits,
        NoRoom,
        NoSettlers,
        SettlersWaiting,
        BadGround,
        TooClose,
        Taken,
        NoWalls,
        NotACity,
        HasRoom,
        NotBesideTown,
    }

    public static class Economy
    {
        public static readonly Stock Start = new Stock(20, 10, 10, 5);

        public static readonly FieldBuilding[] FieldBuildings =
        {
            FieldBuilding.Farm, FieldBuilding.Fishery, FieldBuilding.Pasture, FieldBuilding.LumberCamp,
            FieldBuilding.Quarry, FieldBuilding.GoldMine,
        };

        public static readonly TownBuilding[] TownBuildings =
        {
            TownBuilding.Market, TownBuilding.Granary, TownBuilding.Tavern, TownBuilding.Barracks,
            TownBuilding.ArcheryRange, TownBuilding.Stables,
        };

        /// <summary>The town buildings that take no place, in the order they can be built.</summary>
        public static readonly TownBuilding[] Fortifications = { TownBuilding.Walls, TownBuilding.Castle };

        /// <summary>The field building a resource needs.</summary>
        public static FieldBuilding BuildingFor(Resource resource)
        {
            switch (resource)
            {
                case Resource.Wheat: return FieldBuilding.Farm;
                case Resource.Fish: return FieldBuilding.Fishery;
                case Resource.Sheep: return FieldBuilding.Pasture;
                case Resource.Wood: return FieldBuilding.LumberCamp;
                case Resource.Stone: return FieldBuilding.Quarry;
                case Resource.Gold: return FieldBuilding.GoldMine;
                default: return FieldBuilding.None;
            }
        }

        public static Stock Cost(FieldBuilding building)
        {
            switch (building)
            {
                case FieldBuilding.Farm: return new Stock(0, 0, 5, 0);
                case FieldBuilding.Fishery: return new Stock(0, 0, 5, 0);
                case FieldBuilding.Pasture: return new Stock(0, 0, 4, 0);
                case FieldBuilding.LumberCamp: return new Stock(3, 0, 0, 0);
                case FieldBuilding.Quarry: return new Stock(0, 0, 5, 0);
                case FieldBuilding.GoldMine: return new Stock(0, 0, 8, 4);
                default: return default;
            }
        }

        public static Stock Cost(TownBuilding building)
        {
            switch (building)
            {
                case TownBuilding.Market: return new Stock(10, 0, 5, 0);
                case TownBuilding.Granary: return new Stock(0, 0, 8, 2);
                case TownBuilding.Tavern: return new Stock(10, 0, 8, 0);
                case TownBuilding.Barracks: return new Stock(0, 0, 6, 2);
                case TownBuilding.ArcheryRange: return new Stock(4, 0, 6, 0);
                case TownBuilding.Stables: return new Stock(8, 0, 6, 4);
                case TownBuilding.Walls: return new Stock(0, 0, 4, 12);
                case TownBuilding.Castle: return new Stock(10, 0, 6, 20);
                default: return default;
            }
        }

        /// <summary>The town level a town building needs.</summary>
        public static int Level(TownBuilding building)
        {
            switch (building)
            {
                case TownBuilding.Tavern:
                case TownBuilding.Stables:
                case TownBuilding.Walls:
                    return 2;
                case TownBuilding.Castle:
                    return 3;
                default:
                    return 1;
            }
        }

        /// <summary>Whether the building takes one of the town's places (the fortifications take none).</summary>
        public static bool TakesSlot(TownBuilding building) =>
            building != TownBuilding.Walls && building != TownBuilding.Castle;

        /// <summary>The unit a town building hires, or null.</summary>
        public static Unit? Hires(TownBuilding building)
        {
            switch (building)
            {
                case TownBuilding.Barracks: return Unit.Spearmen;
                case TownBuilding.ArcheryRange: return Unit.Archers;
                case TownBuilding.Stables: return Unit.Riders;
                default: return null;
            }
        }

        /// <summary>What a field building gives a turn; a farm beside a river gives one more food.</summary>
        public static Stock Yield(FieldBuilding building, bool river)
        {
            switch (building)
            {
                case FieldBuilding.Farm: return new Stock(0, river ? 3 : 2, 0, 0);
                case FieldBuilding.Fishery: return new Stock(0, 2, 0, 0);
                case FieldBuilding.Pasture: return new Stock(1, 1, 0, 0);
                case FieldBuilding.LumberCamp: return new Stock(0, 0, 2, 0);
                case FieldBuilding.Quarry: return new Stock(0, 0, 0, 2);
                case FieldBuilding.GoldMine: return new Stock(3, 0, 0, 0);
                default: return default;
            }
        }

        /// <summary>What a cell gives a turn: its field building's goods, nothing without one.</summary>
        public static Stock Yield(Cell cell) => Yield(cell.Building, cell.IsLand && cell.Rivers != 0);

        /// <summary>What the cell would give with the building its resource needs.</summary>
        public static Stock Potential(Cell cell) => Yield(BuildingFor(cell.Resource), cell.IsLand && cell.Rivers != 0);

        /// <summary>A town's taxes and food, and the capital's extra; a market adds 2 gold.</summary>
        public static Stock CityYield(City city)
        {
            int level = Towns.Level(city);
            var yield = city.Capital ? new Stock(2 + level, 2, 1, 0) : new Stock(1 + level, 1, 0, 0);
            if (city.Buildings.Contains(TownBuilding.Market))
                yield.Gold += 2;
            return yield;
        }

        /// <summary>A kingdom's income a turn: its towns and the field buildings on its land and waters.</summary>
        public static Stock Income(World world, int country)
        {
            var income = new Stock();
            foreach (int city in world.Countries[country].Cities)
                income += CityYield(world.Cities[city]);
            foreach (Cell cell in world.Cells)
            {
                // A field a band stands on gives nothing (Raids).
                if (cell.Owner == country && cell.Monster == Monster.None)
                    income += Yield(cell);
            }
            return income;
        }

        /// <summary>Whether <paramref name="country"/> may put the field building its resource needs on the cell.</summary>
        public static Refusal CanBuild(World world, Stock treasury, int country, int cell)
        {
            Cell c = world.Cells[cell];
            if (c.Owner != country)
                return Refusal.NotYours;
            FieldBuilding building = BuildingFor(c.Resource);
            if (building == FieldBuilding.None || c.City >= 0)
                return Refusal.NoResource;
            if (c.Building != FieldBuilding.None)
                return Refusal.AlreadyBuilt;
            return treasury.Covers(Cost(building)) ? Refusal.None : Refusal.NotEnough;
        }

        /// <summary>Whether a town may get <paramref name="building"/>; the castle stands inside walls.</summary>
        public static Refusal CanBuild(City city, Stock treasury, TownBuilding building)
        {
            if (city.Buildings.Contains(building))
                return Refusal.AlreadyBuilt;
            int level = Towns.Level(city);
            if (level < Level(building))
                return Level(building) >= 3 ? Refusal.NotACity : Refusal.LevelTooLow;
            if (building == TownBuilding.Castle && !city.Buildings.Contains(TownBuilding.Walls))
                return Refusal.NoWalls;
            if (TakesSlot(building) && Towns.Free(city) == 0)
                return Refusal.NoSlot;
            return treasury.Covers(Cost(building)) ? Refusal.None : Refusal.NotEnough;
        }
    }

    /// <summary>Why a town did not grow this turn.</summary>
    public enum GrowthStop : byte
    {
        None,
        Food,
        Tavern,
        Full,
        Castle,
        Room,

        /// <summary>A band out of a lair stands at the town (<see cref="Raids"/>).</summary>
        Raid,
    }

    /// <summary>Town levels and growth.</summary>
    public static class Towns
    {
        /// <summary>Houses a capital starts with (level 2); other towns start with 2 or 3.</summary>
        public const int CapitalHouses = 5;

        /// <summary>Food a town takes from the treasury a turn for its growth.</summary>
        public const int Intake = 2;

        /// <summary>Food a town with a granary takes a turn.</summary>
        public const int GranaryIntake = 4;

        /// <summary>The most houses a town has without a tavern (the top of level 2).</summary>
        public const int TavernHouses = 7;

        /// <summary>The most houses a town has without a castle (the top of level 3).</summary>
        public const int CastleHouses = 10;

        /// <summary>The most houses a town reaches (the top of level 4, a royal city with a castle).</summary>
        public const int MaxHouses = 13;

        /// <summary>1 village (2–4 houses), 2 town (5–7), 3 city (8–10), 4 royal city (11+).</summary>
        public static int Level(int houses) => houses <= 4 ? 1 : houses <= 7 ? 2 : houses <= 10 ? 3 : 4;

        public static int Level(City city) => Level(city.Houses);

        /// <summary>Places on a town's centre: five round the middle; the front one is the square.</summary>
        public const int CentrePlaces = 5;

        /// <summary>Places on a quarter: six round its square.</summary>
        public const int DistrictPlaces = 6;

        /// <summary>What a quarter costs.</summary>
        public static readonly Stock DistrictCost = new Stock(6, 0, 8, 0);

        /// <summary>A free place in <see cref="Layout"/>.</summary>
        public const int FreeLot = -1;

        /// <summary>A place with a house in <see cref="Layout"/>.</summary>
        public const int HouseLot = -2;

        /// <summary>
        /// The town's places: five on its centre, then six on each quarter in the order the
        /// quarters were added. Every house and every building but the fortifications takes one.
        /// </summary>
        public static int Places(City city) => CentrePlaces + DistrictPlaces * city.Districts.Count;

        /// <summary>The town's places taken by its houses and buildings.</summary>
        public static int Taken(City city) => city.Houses + Used(city);

        /// <summary>The town's free places.</summary>
        public static int Free(City city) => Math.Max(0, Places(city) - Taken(city));

        /// <summary>
        /// What stands on each of the town's places: a building (its <see cref="TownBuilding"/>
        /// value), <see cref="HouseLot"/> or <see cref="FreeLot"/>. A building keeps the place it was
        /// built on and the houses fill the others in order, so a new house or building never moves
        /// the old ones.
        /// </summary>
        public static int[] Layout(City city)
        {
            var lots = new int[Places(city)];
            for (int p = 0; p < lots.Length; p++)
                lots[p] = FreeLot;
            var unplaced = new List<TownBuilding>();
            foreach (TownBuilding building in city.Buildings)
            {
                if (!Economy.TakesSlot(building))
                    continue;
                if (city.Places.TryGetValue(building, out int p) && p >= 0 && p < lots.Length && lots[p] == FreeLot)
                    lots[p] = (int)building;
                else
                    unplaced.Add(building);
            }
            int houses = city.Houses;
            for (int p = 0; p < lots.Length && houses > 0; p++)
            {
                if (lots[p] != FreeLot)
                    continue;
                lots[p] = HouseLot;
                houses--;
            }
            foreach (TownBuilding building in unplaced)
            {
                int p = Array.IndexOf(lots, FreeLot);
                if (p < 0)
                    break;
                lots[p] = (int)building;
            }
            return lots;
        }

        /// <summary>The cell a place lies on and its number there.</summary>
        public static int CellOf(City city, int place, out int spot)
        {
            if (place < CentrePlaces)
            {
                spot = place;
                return city.Cell;
            }
            int k = place - CentrePlaces;
            spot = k % DistrictPlaces;
            return city.Districts[k / DistrictPlaces];
        }

        /// <summary>The first place of a town's cell: 0 for the centre, then those of each quarter.</summary>
        public static int FirstPlace(City city, int cell)
        {
            if (cell == city.Cell)
                return 0;
            int k = city.Districts.IndexOf(cell);
            return k < 0 ? -1 : CentrePlaces + DistrictPlaces * k;
        }

        /// <summary>The town whose centre is next to the cell, or -1 (town centres stand three steps apart or more).</summary>
        public static int Beside(World world, int cell)
        {
            for (int d = 0; d < 6; d++)
            {
                int j = world.Neighbor(cell, d);
                if (j >= 0 && world.Cells[j].City >= 0)
                    return world.Cells[j].City;
            }
            return -1;
        }

        /// <summary>
        /// Joins a quarter to its town's centre by a street, as any road: the centre's walls open a
        /// gate where it leaves and a bridge carries it over a river between them (the players
        /// asked for both, 2026-11-11).
        /// </summary>
        public static void Join(World world, City city, int cell)
        {
            for (int d = 0; d < 6; d++)
            {
                if (world.Neighbor(city.Cell, d) != cell)
                    continue;
                world.Cells[city.Cell].Roads |= (byte)(1 << d);
                world.Cells[cell].Roads |= (byte)(1 << ((d + 3) % 6));
            }
        }

        /// <summary>
        /// Whether the town beside the cell (see <see cref="Beside"/>) may grow onto it as a quarter: the
        /// kingdom's own empty land (no resource, place or monsters, not mountains), and only once the
        /// town has no free place.
        /// </summary>
        public static Refusal CanExpand(World world, Stock treasury, int country, int cell, out int town)
        {
            town = Beside(world, cell);
            if (town < 0)
                return Refusal.NotBesideTown;
            Cell c = world.Cells[cell];
            if (world.Cities[town].Country != country || c.Owner != country)
                return Refusal.NotYours;
            if (!c.IsLand || c.Biome == Biome.Mountains)
                return Refusal.BadGround;
            if (c.City >= 0 || c.District >= 0 || c.Resource != Resource.None || c.Place != Place.None || c.Monster != Monster.None)
                return Refusal.Taken;
            if (Free(world.Cities[town]) > 0)
                return Refusal.HasRoom;
            return treasury.Covers(DistrictCost) ? Refusal.None : Refusal.NotEnough;
        }

        /// <summary>
        /// The cells a full town can grow onto now, whatever the treasury (see <see cref="CanExpand"/>),
        /// best first: not across a river from the centre, beside more of the town's quarters, along a
        /// road from the centre. The town screen's button takes the first: the players found it odd
        /// that a town could not be expanded from its own screen (2026-11-11).
        /// </summary>
        public static List<int> ExpansionCells(World world, City city)
        {
            var cells = new List<int>();
            var score = new Dictionary<int, int>();
            Cell centre = world.Cells[city.Cell];
            for (int d = 0; d < 6; d++)
            {
                int j = world.Neighbor(city.Cell, d);
                if (j < 0 || CanExpand(world, DistrictCost, city.Country, j, out int town) != Refusal.None || town != city.Index)
                    continue;
                int s = (centre.Rivers & (1 << d)) == 0 ? 100 : 0;
                for (int e = 0; e < 6; e++)
                {
                    int k = world.Neighbor(j, e);
                    if (k >= 0 && world.Cells[k].District == city.Index)
                        s += 10;
                }
                if ((centre.Roads & (1 << d)) != 0)
                    s += 1;
                cells.Add(j);
                score[j] = s;
            }
            cells.Sort((a, b) => score[a] != score[b] ? score[b].CompareTo(score[a]) : a.CompareTo(b));
            return cells;
        }

        /// <summary>The town's places taken by its buildings (the fortifications take none).</summary>
        public static int Used(City city)
        {
            int used = 0;
            foreach (TownBuilding building in city.Buildings)
            {
                if (Economy.TakesSlot(building))
                    used++;
            }
            return used;
        }

        /// <summary>Food the next house needs: twice the town's houses.</summary>
        public static int NextHouse(City city) => 2 * city.Houses;

        /// <summary>Food the town takes a turn while it can grow.</summary>
        public static int IntakeOf(City city) => city.Buildings.Contains(TownBuilding.Granary) ? GranaryIntake : Intake;

        /// <summary>What keeps the town from growing whatever the food, or <see cref="GrowthStop.None"/>.</summary>
        public static GrowthStop Limit(City city)
        {
            if (city.Houses >= MaxHouses)
                return GrowthStop.Full;
            if (Free(city) == 0)
                return GrowthStop.Room;
            if (city.Houses >= TavernHouses && !city.Buildings.Contains(TownBuilding.Tavern))
                return GrowthStop.Tavern;
            if (city.Houses >= CastleHouses && !city.Buildings.Contains(TownBuilding.Castle))
                return GrowthStop.Castle;
            return GrowthStop.None;
        }

        /// <summary>Food the kingdom's towns want a turn for their growth.</summary>
        public static int Demand(World world, int country)
        {
            int demand = 0;
            foreach (int index in world.Countries[country].Cities)
            {
                City city = world.Cities[index];
                if (Limit(city) == GrowthStop.None)
                    demand += Math.Min(IntakeOf(city), NextHouse(city) - city.Growth);
            }
            return demand;
        }
    }
}

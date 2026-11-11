using System.Collections.Generic;

namespace Realms.Core
{
    public enum Biome : byte
    {
        Ocean,
        Shallows,
        Beach,
        Meadow,
        Plains,
        Forest,
        Hills,
        Mountains,
    }

    public enum Resource : byte
    {
        None,
        Wheat,
        Wood,
        Stone,
        Gold,
        Fish,
        Sheep,
    }

    /// <summary>The kind of a monster group (the first three also have lairs).</summary>
    public enum Monster : byte
    {
        None,
        Troll,
        Wolves,
        Boar,
        Goblins,
        Bandits,
        Skeletons,
        Spiders,
    }

    /// <summary>Something a hero visits on the map.</summary>
    public enum Place : byte
    {
        None,
        Chest,
        WoodPile,
        StonePile,
        GoldPile,
        ShrineAttack,
        ShrineDefence,
        Watchtower,
    }

    public sealed class Cell
    {
        public int Index;
        public Hex Hex;
        public Biome Biome;

        /// <summary>Raw elevation from the generator, above the sea level when positive.</summary>
        public float Elevation;

        /// <summary>Water not connected to the open sea.</summary>
        public bool Lake;

        /// <summary>Index of the owning country, or -1 for wild land and open sea.</summary>
        public int Owner = -1;

        /// <summary>Index into <see cref="World.Cities"/> of the town whose centre this is, or -1.</summary>
        public int City = -1;

        /// <summary>Index into <see cref="World.Cities"/> of the town this cell is a quarter of, or -1.</summary>
        public int District = -1;

        public Resource Resource;

        /// <summary>The monster group standing on the cell, or <see cref="Monster.None"/>.</summary>
        public Monster Monster;

        /// <summary>How many monsters the group has now.</summary>
        public int Count;

        /// <summary>The group's size when the game began; it grows up to twice that.</summary>
        public int FirstCount;

        /// <summary>Whether the group lives in a lair (a cave or a den, with goods inside).</summary>
        public bool Lair;

        /// <summary>Whether the group is a band out of a lair on a raid (<see cref="Raids"/>), not a group of the wild.</summary>
        public bool Band;

        public Place Place;

        /// <summary>The building on the cell's resource, which makes it give goods.</summary>
        public FieldBuilding Building;

        /// <summary>Roads leaving the cell: bit d set for a road across edge d (see <see cref="Hex.Directions"/>).</summary>
        public byte Roads;

        /// <summary>Rivers along the cell's edges: bit d set for a river along edge d.</summary>
        public byte Rivers;

        public int Col => Hex.Col;
        public int Row => Hex.Row;
        public bool IsWater => Biome == Biome.Ocean || Biome == Biome.Shallows;
        public bool IsLand => !IsWater;

        /// <summary>The town on the cell, its centre or one of its quarters, or -1.</summary>
        public int Town => City >= 0 ? City : District;
    }

    public sealed class City
    {
        public int Index;
        public string Name;

        /// <summary>The name as the players read it, in the game's language (<see cref="Names.Shown"/>).</summary>
        public string Label => Names.Shown(Name);
        public int Country;
        public int Cell;
        public bool Capital;

        /// <summary>Houses around the centre (the town hall of a capital is extra).</summary>
        public int Houses;

        /// <summary>Food gathered towards the next house.</summary>
        public int Growth;

        /// <summary>Why the town did not grow in the last turn.</summary>
        public GrowthStop Stop;

        /// <summary>The town's buildings in the order they were built.</summary>
        public readonly List<TownBuilding> Buildings = new List<TownBuilding>();

        /// <summary>The town's quarters: neighbouring cells it grew onto, in that order (see <see cref="Towns.Places"/>).</summary>
        public readonly List<int> Districts = new List<int>();

        /// <summary>The place each building that takes one stands on (see <see cref="Towns.Layout"/>).</summary>
        public readonly Dictionary<TownBuilding, int> Places = new Dictionary<TownBuilding, int>();

        /// <summary>Recruits waiting to be hired, per hired unit (see <see cref="Units.Hired"/>).</summary>
        public readonly int[] Recruits = new int[3];

        /// <summary>Hired units waiting in the town for the next hero who comes in.</summary>
        public readonly List<Stack> Garrison = new List<Stack>();

        /// <summary>A settlers' cart waiting in the town for a hero to lead it (see <see cref="Settling"/>).</summary>
        public bool Settlers;

        /// <summary>Influence gathered towards the next cell of land (see <see cref="Core.Influence"/>).</summary>
        public int Influence;

        /// <summary>Whether the town found no wild cell to take at the end of the last turn.</summary>
        public bool LandFull;
    }

    public sealed class Country
    {
        public int Index;
        public string Name;

        /// <summary>The name as the players read it, in the game's language (<see cref="Names.Shown"/>).</summary>
        public string Label => Names.Shown(Name);

        /// <summary>The kingdom's flag on its banners and shields (<see cref="Flags.Generate"/>, or the players' own).</summary>
        public Flag Flag;

        /// <summary>Index into <see cref="World.Cities"/>.</summary>
        public int Capital = -1;

        public readonly List<int> Cities = new List<int>();

        /// <summary>
        /// The wild cell the kingdom's stake (вешка) stands on, or -1: the town nearest to it grows
        /// towards it (<see cref="Influence.StakeWay"/>).
        /// </summary>
        public int Stake = -1;
    }

    /// <summary>A point of a river's course on the ground plane and the river's size there.</summary>
    public readonly struct RiverPoint
    {
        public readonly float X, Z;

        /// <summary>River edges upstream of the point, the river's own and its tributaries'.</summary>
        public readonly int Flow;

        public RiverPoint(float x, float z, int flow)
        {
            X = x;
            Z = z;
            Flow = flow;
        }
    }

    /// <summary>
    /// A river along hexagon edges, from a spring in the hills or mountains down to the sea, a lake
    /// or a bigger river. Its points are the hexagon corners it passes, from the spring on; the last
    /// one touches <see cref="Mouth"/> or lies on the river it <see cref="Joins"/>.
    /// </summary>
    public sealed class River
    {
        public readonly List<RiverPoint> Points = new List<RiverPoint>();

        /// <summary>The water cell the river flows into, or -1 when it joins another river.</summary>
        public int Mouth = -1;

        /// <summary>The river this one flows into, or -1.</summary>
        public int Joins = -1;
    }

    public sealed class World
    {
        public readonly int Width, Height, Seed;
        public readonly Cell[] Cells;
        public readonly List<Country> Countries = new List<Country>();
        public readonly List<City> Cities = new List<City>();
        public readonly List<River> Rivers = new List<River>();

        /// <summary>The country the players rule.</summary>
        public int Player;

        public World(int width, int height, int seed)
        {
            Width = width;
            Height = height;
            Seed = seed;
            Cells = new Cell[width * height];
            for (int row = 0; row < height; row++)
            for (int col = 0; col < width; col++)
            {
                int index = row * width + col;
                Cells[index] = new Cell { Index = index, Hex = Hex.FromOffset(col, row) };
            }
        }

        public bool Contains(int col, int row) => col >= 0 && row >= 0 && col < Width && row < Height;

        public int IndexOf(int col, int row) => Contains(col, row) ? row * Width + col : -1;

        public int IndexOf(Hex hex) => IndexOf(hex.Col, hex.Row);

        public Cell CellAt(Hex hex)
        {
            int index = IndexOf(hex);
            return index < 0 ? null : Cells[index];
        }

        /// <summary>The neighbour across edge <paramref name="direction"/>, or -1 off the map.</summary>
        public int Neighbor(int index, int direction) => IndexOf(Cells[index].Hex.Neighbor(direction));

        /// <summary>World bounds of the hexagons: x from <see cref="MinX"/> to <see cref="MaxX"/>.</summary>
        public float MinX => -HexMetrics.InnerRadius;

        public float MaxX => HexMetrics.Sqrt3 * Width;
        public float MinZ => -HexMetrics.Radius;
        public float MaxZ => 1.5f * (Height - 1) + HexMetrics.Radius;

        public float CenterX(int index) => HexMetrics.CenterX(Cells[index].Hex);
        public float CenterZ(int index) => HexMetrics.CenterZ(Cells[index].Hex);

        /// <summary>A new town of the country on the cell (its land is the caller's).</summary>
        public City AddCity(int country, int cell, bool capital, int houses, string name)
        {
            var city = new City
            {
                Index = Cities.Count, Name = name, Country = country, Cell = cell, Capital = capital, Houses = houses,
            };
            Cities.Add(city);
            Cells[cell].City = city.Index;
            Countries[country].Cities.Add(city.Index);
            return city;
        }
    }
}

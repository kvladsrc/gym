using System;

namespace Realms.Core
{
    /// <summary>How a flag's field is divided between its first and second colour.</summary>
    public enum FlagPattern : byte
    {
        Plain,

        /// <summary>Two halves side by side.</summary>
        Pale,

        /// <summary>Two halves one above the other.</summary>
        Fess,

        Quarters,

        /// <summary>Halved from the top of the pole's side to the bottom of the far side.</summary>
        Bend,

        Cross,
        Saltire,

        /// <summary>A chevron rising from the bottom edge.</summary>
        Chevron,

        /// <summary>A band across the middle.</summary>
        Band,

        /// <summary>A border round the field.</summary>
        Border,

        /// <summary>A triangle from the pole's side.</summary>
        Triangle,
    }

    /// <summary>The flag's emblem, a heraldic figure in one colour, or none.</summary>
    public enum Emblem : byte
    {
        None,
        Lion,
        Eagle,
        Horse,
        Stag,
        Bear,
        Dragon,
        Fish,
        Tree,
        Tower,
        Crown,
        Star,
        Sun,
        Moon,
        Sword,
        Key,
        Wheat,
        Lily,
        Anchor,
    }

    /// <summary>
    /// A kingdom's flag: a pattern of two colours and an emblem in a third. The colours are
    /// indices into the tinctures, whose shades the game's palette keeps.
    /// </summary>
    public struct Flag : IEquatable<Flag>
    {
        public FlagPattern Pattern;
        public byte Field, Second, EmblemColour;
        public Emblem Emblem;

        public Flag(FlagPattern pattern, int field, int second, Emblem emblem, int emblemColour)
        {
            Pattern = pattern;
            Field = (byte)field;
            Second = (byte)second;
            Emblem = emblem;
            EmblemColour = (byte)emblemColour;
        }

        /// <summary>The flag as a short line of numbers, to keep it between games.</summary>
        public string Code => $"{(int)Pattern}.{Field}.{Second}.{(int)Emblem}.{EmblemColour}";

        /// <summary>The flag a <see cref="Code"/> describes; false for anything else.</summary>
        public static bool TryParse(string code, out Flag flag)
        {
            flag = default;
            string[] parts = code?.Split('.');
            if (parts == null || parts.Length != 5)
                return false;
            var n = new int[5];
            for (int k = 0; k < 5; k++)
            {
                if (!int.TryParse(parts[k], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out n[k]))
                    return false;
            }
            if (n[0] >= Flags.Patterns || n[3] >= Flags.Emblems
                || n[1] >= Flags.Tinctures || n[2] >= Flags.Tinctures || n[4] >= Flags.Tinctures)
                return false;
            flag = new Flag((FlagPattern)n[0], n[1], n[2], (Emblem)n[3], n[4]);
            return true;
        }

        public bool Equals(Flag other) => Code == other.Code;
        public override bool Equals(object obj) => obj is Flag other && Equals(other);
        public override int GetHashCode() => Code.GetHashCode();
        public override string ToString() => Code;
    }

    public static class Flags
    {
        public static readonly int Patterns = Enum.GetValues(typeof(FlagPattern)).Length;
        public static readonly int Emblems = Enum.GetValues(typeof(Emblem)).Length;

        /// <summary>
        /// The colours a flag may take, in the order the editor shows them: white, yellow,
        /// orange, red, violet, blue, sky, green, brown, black.
        /// </summary>
        public const int Tinctures = 10;

        public const int White = 0, Yellow = 1, Orange = 2, Red = 3, Violet = 4, Blue = 5, Sky = 6, Green = 7, Brown = 8, Black = 9;

        /// <summary>The colour each kingdom of a new map starts with, by its index.</summary>
        public static readonly int[] KingdomTincture = { Red, Blue, Yellow, Green, Violet, Orange };

        /// <summary>White and yellow, heraldry's metals: they stand out on every other colour.</summary>
        public static bool Metal(int tincture) => tincture == White || tincture == Yellow;

        /// <summary>
        /// Which colour of the pattern is at (<paramref name="u"/>, <paramref name="v"/>): u runs
        /// from the pole's side to the far side, v from the top to the bottom, both 0–1. 0 is the
        /// field, 1 the second colour.
        /// </summary>
        public static int Part(FlagPattern pattern, float u, float v)
        {
            switch (pattern)
            {
                case FlagPattern.Pale: return u < 0.5f ? 0 : 1;
                case FlagPattern.Fess: return v < 0.5f ? 0 : 1;
                case FlagPattern.Quarters: return (u < 0.5f) == (v < 0.5f) ? 0 : 1;
                case FlagPattern.Bend: return u > v ? 0 : 1;
                case FlagPattern.Cross: return Math.Abs(u - 0.5f) < 0.09f || Math.Abs(v - 0.5f) < 0.14f ? 1 : 0;
                case FlagPattern.Saltire: return Math.Abs(u - v) < 0.12f || Math.Abs(u + v - 1f) < 0.12f ? 1 : 0;
                case FlagPattern.Chevron:
                    {
                        float ridge = 0.38f + 1.1f * Math.Abs(u - 0.5f);
                        return v > ridge && v < ridge + 0.3f ? 1 : 0;
                    }
                case FlagPattern.Band: return v > 0.33f && v < 0.67f ? 1 : 0;
                case FlagPattern.Border: return u < 0.1f || u > 0.9f || v < 0.14f || v > 0.86f ? 1 : 0;
                case FlagPattern.Triangle: return u < 0.5f * (1f - Math.Abs(2f * v - 1f)) ? 1 : 0;
                default: return 0;
            }
        }

        /// <summary>
        /// The flag a kingdom flies until the players draw their own, chosen from the map's seed
        /// so the same map always gets the same flags: the field in the kingdom's colour and the
        /// second colour and the emblem in a metal on a colour or a colour on a metal. Half the
        /// flags have an emblem on a plain field, in a border or on a band; the others a pattern
        /// whose parts would cut through an emblem, so they have none.
        /// </summary>
        public static Flag Generate(int seed, int country) => Generate(seed, country, KingdomTincture[country % KingdomTincture.Length]);

        /// <summary>
        /// The kingdom's generated flag on another <paramref name="field"/>: the same pattern and
        /// emblem, the other colours chosen again to read on it.
        /// </summary>
        public static Flag Generate(int seed, int country, int field)
        {
            var rng = new Rng(seed * 131 + country * 977 + 5);
            int[] colours = { Red, Blue, Green, Black, Violet };
            int[] metals = { White, Yellow };
            int Against(int tincture)
            {
                int[] from = Metal(tincture) ? colours : metals;
                int pick = from[rng.Range(0, from.Length)];
                return pick == field ? from[(Array.IndexOf(from, pick) + 1) % from.Length] : pick;
            }
            int second = Against(field);
            if (rng.Chance(0.5f))
            {
                FlagPattern[] framing = { FlagPattern.Plain, FlagPattern.Border, FlagPattern.Band };
                FlagPattern pattern = framing[rng.Range(0, framing.Length)];
                var emblem = (Emblem)rng.Range(1, Emblems);
                int under = pattern == FlagPattern.Band ? second : field;
                return new Flag(pattern, field, second, emblem, Against(under));
            }
            FlagPattern[] parted =
            {
                FlagPattern.Pale, FlagPattern.Fess, FlagPattern.Quarters, FlagPattern.Bend, FlagPattern.Cross,
                FlagPattern.Saltire, FlagPattern.Chevron, FlagPattern.Triangle,
            };
            return new Flag(parted[rng.Range(0, parted.Length)], field, second, Emblem.None, second);
        }

        /// <summary>
        /// The kingdom raises <paramref name="flag"/>. Its field is the kingdom's colour on the map
        /// and gives it its name, so no two kingdoms share a field: a kingdom that had this colour
        /// takes the claimer's old one (or the first colour nobody has), its flag drawn again on
        /// it. Returns whether any kingdom's colour changed.
        /// </summary>
        public static bool Claim(World world, int country, Flag flag)
        {
            Country claimer = world.Countries[country];
            int old = claimer.Flag.Field;
            claimer.Flag = flag;
            if (old == flag.Field)
                return false;
            claimer.Name = Names.Kingdoms[flag.Field];
            foreach (Country other in world.Countries)
            {
                if (other == claimer || other.Flag.Field != flag.Field)
                    continue;
                int field = Taken(world, old) ? Free(world) : old;
                other.Flag = Generate(world.Seed, other.Index, field);
                other.Name = Names.Kingdoms[field];
            }
            return true;
        }

        /// <summary>The order in which kingdoms get a colour nobody flies: the kingdoms' own first.</summary>
        static readonly int[] FreeOrder = { Red, Blue, Yellow, Green, Violet, Orange, Sky, Brown, Black, White };

        static bool Taken(World world, int tincture)
        {
            foreach (Country country in world.Countries)
            {
                if (country.Flag.Field == tincture)
                    return true;
            }
            return false;
        }

        static int Free(World world)
        {
            foreach (int tincture in FreeOrder)
            {
                if (!Taken(world, tincture))
                    return tincture;
            }
            return White;
        }

        /// <summary>Every kingdom's generated flag.</summary>
        public static void Assign(World world)
        {
            foreach (Country country in world.Countries)
                country.Flag = Generate(world.Seed, country.Index);
        }
    }
}

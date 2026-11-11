using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Realms.Core
{
    /// <summary>Whether the game wrote the save by itself or the players did.</summary>
    public enum SaveKind : byte
    {
        Auto,
        Manual,
    }

    /// <summary>Why a file does not open as a save.</summary>
    public enum SaveProblem : byte
    {
        /// <summary>Not a save of this game at all.</summary>
        Foreign,

        /// <summary>Cut short or damaged.</summary>
        Damaged,

        /// <summary>Written by an older game in a format this one no longer reads.</summary>
        Old,

        /// <summary>Written by a newer game.</summary>
        Newer,
    }

    /// <summary>A file that does not open as a save, and why (<see cref="SaveProblem"/>).</summary>
    public sealed class SaveException : Exception
    {
        public readonly SaveProblem Problem;

        public SaveException(SaveProblem problem, string message, Exception inner = null) : base(message, inner)
        {
            Problem = problem;
        }
    }

    /// <summary>What the list of saves shows: read without the world (<see cref="Save.ReadHeader"/>).</summary>
    public sealed class SaveHeader
    {
        /// <summary>The file's format (<see cref="Save.Format"/>); <see cref="Save.Write"/> sets it.</summary>
        public int Format;

        /// <summary>The game's version that wrote it, for the record.</summary>
        public string Version = "";

        /// <summary>The game the save belongs to: each new game draws its own, and its autosaves go round under it.</summary>
        public long GameId;

        public SaveKind Kind;

        /// <summary>When it was written, in UTC.</summary>
        public DateTime Written;

        /// <summary>Seconds the game has been played.</summary>
        public int Played;

        /// <summary>The turn, the players' kingdom and its flag (<see cref="Core.Flag.Code"/>) and the map: <see cref="Save.Write"/> takes them from the state.</summary>
        public int Turn;

        public string Kingdom = "";
        public string Flag = "";
        public int Seed;
    }

    /// <summary>Where the players looked: the chosen hero and the camera, so that a loaded game shows the map as they left it.</summary>
    public struct SaveView
    {
        /// <summary>The chosen hero's index in <see cref="GameState.Heroes"/>, or -1.</summary>
        public int Hero;

        /// <summary>The camera's point on the ground, its distance and its turn.</summary>
        public float X, Z, Distance, Yaw;
    }

    /// <summary>A save: its header, the game, the view and the last turn's report (null before the first turn's end).</summary>
    public sealed class SaveGame
    {
        public SaveHeader Header = new SaveHeader();
        public GameState State;
        public SaveView View = new SaveView { Hero = -1 };
        public TurnReport Report;
    }

    /// <summary>
    /// The saves (the interface plan's 3.1, 2026-11-11): the whole world and the game's state, not
    /// only the map's number, so that a save opens after the map maker changes, with the random
    /// numbers' state, so that the same chest and fight come out the same after loading. A file is
    /// the signature, the format, the header, then the body with its length and an FNV-1a hash: a
    /// file cut short or damaged says so instead of loading half a game. Raise <see cref="Format"/>
    /// whenever the body changes and read the older formats on with defaults; the census test
    /// checks <see cref="Fields"/> against the classes, so a new field without a place here fails it.
    /// </summary>
    public static class Save
    {
        /// <summary>The format this game writes.</summary>
        public const int Format = 1;

        /// <summary>The oldest format this game still reads.</summary>
        public const int OldestFormat = 1;

        static readonly byte[] Signature = Encoding.ASCII.GetBytes("REALMSAV");

        /// <summary>
        /// The fields of the saved classes, each with its place in the file (or restored from the
        /// order the file keeps). The census test compares it with the classes' own fields.
        /// </summary>
        public static readonly IReadOnlyDictionary<Type, string> Fields = new Dictionary<Type, string>
        {
            [typeof(World)] = "Width Height Seed Cells Countries Cities Rivers Player",
            // Index and Hex come back from the cell's place in the list.
            [typeof(Cell)] = "Index Hex Biome Elevation Lake Owner City District Resource Monster Count FirstCount Lair Band Place Building Roads Rivers",
            [typeof(Country)] = "Index Name Flag Capital Cities Stake",
            [typeof(Flag)] = "Pattern Field Second EmblemColour Emblem",
            [typeof(City)] = "Index Name Country Cell Capital Houses Growth Stop Buildings Districts Places Recruits Garrison Settlers Influence LandFull",
            [typeof(Stack)] = "Unit Count",
            [typeof(River)] = "Points Mouth Joins",
            [typeof(RiverPoint)] = "X Z Flow",
            [typeof(GameState)] =
                "World Treasury Grown GrownLastTurn Done Turn " +
                "Heroes Seen Rng Victories LairsCleared ChestsOpened Chest " +
                "Gems StartLand Lairs Crowned " +
                "Difficulty RivalsPlay Aims " +
                "TownsFounded " +
                "StakesSet BotsStake " +
                "Bands RaidsSent RaidsRepelled RaidsBeaten RaidTurns LastRaid LairSent " +
                "LairTroops TroopsSent TroopsWon TroopsRecalled",
            [typeof(Stock)] = "Gold Food Wood Stone",
            [typeof(Hero)] = "Index Name Country Cell Attack Defence Level Experience Moves LevelUps Army Shrines Settlers",
            [typeof(Rng)] = "state",
            [typeof(Chest)] = "Hero Cell Gold Experience",
            [typeof(GemWon)] = "Gem Turn",
            [typeof(Raid)] = "Lair Town Cell Stay Age Steps",
            [typeof(Troop)] = "Country Lair Turns Men Paid",
        };

        /// <summary>
        /// Writes the save: the header (its format, turn, kingdom, flag and map taken from the
        /// state), the game, the view and the report.
        /// </summary>
        public static void Write(Stream stream, SaveGame save)
        {
            GameState state = save.State;
            World world = state.World;
            Country player = world.Countries[world.Player];
            SaveHeader header = save.Header;
            header.Format = Format;
            header.Turn = state.Turn;
            header.Kingdom = player.Name ?? "";
            header.Flag = player.Flag.Code;
            header.Seed = world.Seed;

            byte[] body;
            using (var memory = new MemoryStream())
            {
                using (var w = new BinaryWriter(memory, Encoding.UTF8, true))
                {
                    WriteWorld(w, world);
                    WriteState(w, state);
                    WriteView(w, save.View);
                    WriteReport(w, save.Report);
                }
                body = memory.ToArray();
            }
            using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                w.Write(Signature);
                w.Write(Format);
                WriteHeader(w, header);
                w.Write(body.Length);
                w.Write(body);
                w.Write(Hash(body));
            }
        }

        /// <summary>The save as bytes.</summary>
        public static byte[] ToBytes(SaveGame save)
        {
            using (var memory = new MemoryStream())
            {
                Write(memory, save);
                return memory.ToArray();
            }
        }

        /// <summary>The header alone, for the list of saves; <see cref="SaveException"/> when the file is not a save this game reads.</summary>
        public static SaveHeader ReadHeader(Stream stream)
        {
            using (var r = new BinaryReader(stream, Encoding.UTF8, true))
                return Guard(() => Header(r));
        }

        /// <summary>The whole save; <see cref="SaveException"/> when the file is not a save, cut short or damaged.</summary>
        public static SaveGame Read(Stream stream)
        {
            using (var r = new BinaryReader(stream, Encoding.UTF8, true))
            {
                return Guard(() =>
                {
                    SaveHeader header = Header(r);
                    int length = r.ReadInt32();
                    if (length < 0)
                        throw Damaged("a negative length");
                    byte[] body = r.ReadBytes(length);
                    if (body.Length != length)
                        throw Damaged("the body is cut short");
                    if (r.ReadUInt32() != Hash(body))
                        throw Damaged("the body does not match its hash");
                    using (var memory = new MemoryStream(body, false))
                    using (var b = new BinaryReader(memory, Encoding.UTF8))
                    {
                        World world = ReadWorld(b);
                        var save = new SaveGame { Header = header, State = ReadState(b, world) };
                        save.View = ReadView(b);
                        save.Report = ReadReport(b);
                        if (memory.Position != memory.Length)
                            throw Damaged("bytes are left over after the game");
                        return save;
                    }
                });
            }
        }

        /// <summary>The save from bytes.</summary>
        public static SaveGame FromBytes(byte[] bytes)
        {
            using (var memory = new MemoryStream(bytes, false))
                return Read(memory);
        }

        /// <summary>Runs a read, turning a file that ends early or holds nonsense into <see cref="SaveProblem.Damaged"/>.</summary>
        static T Guard<T>(Func<T> read)
        {
            try
            {
                return read();
            }
            catch (SaveException)
            {
                throw;
            }
            catch (Exception e) when (e is EndOfStreamException || e is IOException || e is ArgumentException
                || e is IndexOutOfRangeException || e is InvalidOperationException || e is DecoderFallbackException
                || e is OverflowException || e is OutOfMemoryException)
            {
                throw new SaveException(SaveProblem.Damaged, "The save is cut short or damaged: " + e.Message, e);
            }
        }

        static SaveException Damaged(string why) => new SaveException(SaveProblem.Damaged, "The save is damaged: " + why);

        static SaveHeader Header(BinaryReader r)
        {
            byte[] signature = r.ReadBytes(Signature.Length);
            if (signature.Length != Signature.Length)
                throw new SaveException(SaveProblem.Foreign, "The file is too short to be a save");
            for (int i = 0; i < Signature.Length; i++)
            {
                if (signature[i] != Signature[i])
                    throw new SaveException(SaveProblem.Foreign, "The file is not a save of this game");
            }
            int format = r.ReadInt32();
            if (format < OldestFormat)
                throw new SaveException(SaveProblem.Old, $"The save's format {format} is older than {OldestFormat}, the oldest this game reads");
            if (format > Format)
                throw new SaveException(SaveProblem.Newer, $"The save's format {format} is newer than this game's {Format}");
            var header = new SaveHeader
            {
                Format = format,
                Version = r.ReadString(),
                GameId = r.ReadInt64(),
                Kind = (SaveKind)r.ReadByte(),
                Written = new DateTime(r.ReadInt64(), DateTimeKind.Utc),
                Played = r.ReadInt32(),
                Turn = r.ReadInt32(),
                Kingdom = r.ReadString(),
                Flag = r.ReadString(),
                Seed = r.ReadInt32(),
            };
            return header;
        }

        static void WriteHeader(BinaryWriter w, SaveHeader header)
        {
            w.Write(header.Version ?? "");
            w.Write(header.GameId);
            w.Write((byte)header.Kind);
            w.Write((header.Written.Kind == DateTimeKind.Local ? header.Written.ToUniversalTime() : header.Written).Ticks);
            w.Write(header.Played);
            w.Write(header.Turn);
            w.Write(header.Kingdom ?? "");
            w.Write(header.Flag ?? "");
            w.Write(header.Seed);
        }

        // The world.

        static void WriteWorld(BinaryWriter w, World world)
        {
            w.Write(world.Width);
            w.Write(world.Height);
            w.Write(world.Seed);
            w.Write(world.Player);
            foreach (Cell c in world.Cells)
            {
                w.Write((byte)c.Biome);
                w.Write(c.Elevation);
                w.Write(c.Lake);
                w.Write(c.Owner);
                w.Write(c.City);
                w.Write(c.District);
                w.Write((byte)c.Resource);
                w.Write((byte)c.Monster);
                w.Write(c.Count);
                w.Write(c.FirstCount);
                w.Write(c.Lair);
                w.Write(c.Band);
                w.Write((byte)c.Place);
                w.Write((byte)c.Building);
                w.Write(c.Roads);
                w.Write(c.Rivers);
            }
            w.Write(world.Countries.Count);
            foreach (Country country in world.Countries)
            {
                w.Write(country.Name ?? "");
                Flag flag = country.Flag;
                w.Write((byte)flag.Pattern);
                w.Write(flag.Field);
                w.Write(flag.Second);
                w.Write((byte)flag.Emblem);
                w.Write(flag.EmblemColour);
                w.Write(country.Capital);
                Ints(w, country.Cities);
                w.Write(country.Stake);
            }
            w.Write(world.Cities.Count);
            foreach (City city in world.Cities)
            {
                w.Write(city.Name ?? "");
                w.Write(city.Country);
                w.Write(city.Cell);
                w.Write(city.Capital);
                w.Write(city.Houses);
                w.Write(city.Growth);
                w.Write((byte)city.Stop);
                w.Write(city.Buildings.Count);
                foreach (TownBuilding b in city.Buildings)
                    w.Write((byte)b);
                Ints(w, city.Districts);
                w.Write(city.Places.Count);
                foreach (KeyValuePair<TownBuilding, int> place in city.Places)
                {
                    w.Write((byte)place.Key);
                    w.Write(place.Value);
                }
                Ints(w, city.Recruits);
                Stacks(w, city.Garrison);
                w.Write(city.Settlers);
                w.Write(city.Influence);
                w.Write(city.LandFull);
            }
            w.Write(world.Rivers.Count);
            foreach (River river in world.Rivers)
            {
                w.Write(river.Points.Count);
                foreach (RiverPoint p in river.Points)
                {
                    w.Write(p.X);
                    w.Write(p.Z);
                    w.Write(p.Flow);
                }
                w.Write(river.Mouth);
                w.Write(river.Joins);
            }
        }

        /// <summary>The largest map side and list a save may hold: more is a damaged file, not a world.</summary>
        const int MostSide = 1024, MostItems = 1 << 20;

        static World ReadWorld(BinaryReader r)
        {
            int width = r.ReadInt32(), height = r.ReadInt32(), seed = r.ReadInt32();
            if (width <= 0 || height <= 0 || width > MostSide || height > MostSide)
                throw Damaged($"a map of {width}×{height}");
            var world = new World(width, height, seed) { Player = r.ReadInt32() };
            foreach (Cell c in world.Cells)
            {
                c.Biome = (Biome)r.ReadByte();
                c.Elevation = r.ReadSingle();
                c.Lake = r.ReadBoolean();
                c.Owner = r.ReadInt32();
                c.City = r.ReadInt32();
                c.District = r.ReadInt32();
                c.Resource = (Resource)r.ReadByte();
                c.Monster = (Monster)r.ReadByte();
                c.Count = r.ReadInt32();
                c.FirstCount = r.ReadInt32();
                c.Lair = r.ReadBoolean();
                c.Band = r.ReadBoolean();
                c.Place = (Place)r.ReadByte();
                c.Building = (FieldBuilding)r.ReadByte();
                c.Roads = r.ReadByte();
                c.Rivers = r.ReadByte();
            }
            int countries = Count(r);
            for (int k = 0; k < countries; k++)
            {
                var country = new Country { Index = k, Name = r.ReadString() };
                country.Flag = new Flag
                {
                    Pattern = (FlagPattern)r.ReadByte(), Field = r.ReadByte(), Second = r.ReadByte(),
                    Emblem = (Emblem)r.ReadByte(), EmblemColour = r.ReadByte(),
                };
                country.Capital = r.ReadInt32();
                ReadInts(r, country.Cities);
                country.Stake = r.ReadInt32();
                world.Countries.Add(country);
            }
            if (world.Player < 0 || world.Player >= countries)
                throw Damaged($"the players' kingdom {world.Player} of {countries}");
            int cities = Count(r);
            for (int i = 0; i < cities; i++)
            {
                var city = new City
                {
                    Index = i, Name = r.ReadString(), Country = r.ReadInt32(), Cell = r.ReadInt32(), Capital = r.ReadBoolean(),
                    Houses = r.ReadInt32(), Growth = r.ReadInt32(), Stop = (GrowthStop)r.ReadByte(),
                };
                int buildings = Count(r);
                for (int b = 0; b < buildings; b++)
                    city.Buildings.Add((TownBuilding)r.ReadByte());
                ReadInts(r, city.Districts);
                int places = Count(r);
                for (int p = 0; p < places; p++)
                {
                    var building = (TownBuilding)r.ReadByte();
                    city.Places[building] = r.ReadInt32();
                }
                ReadInts(r, city.Recruits);
                ReadStacks(r, city.Garrison);
                city.Settlers = r.ReadBoolean();
                city.Influence = r.ReadInt32();
                city.LandFull = r.ReadBoolean();
                world.Cities.Add(city);
            }
            int rivers = Count(r);
            for (int i = 0; i < rivers; i++)
            {
                var river = new River();
                int points = Count(r);
                for (int p = 0; p < points; p++)
                    river.Points.Add(new RiverPoint(r.ReadSingle(), r.ReadSingle(), r.ReadInt32()));
                river.Mouth = r.ReadInt32();
                river.Joins = r.ReadInt32();
                world.Rivers.Add(river);
            }
            return world;
        }

        // The game's state, in the order of GameState's parts.

        static void WriteState(BinaryWriter w, GameState s)
        {
            int n = s.World.Countries.Count;
            w.Write(s.Turn);
            for (int k = 0; k < n; k++)
                WriteStock(w, s.Treasury[k]);
            Ints(w, s.Grown);
            Ints(w, s.GrownLastTurn);
            w.Write(s.Done.Count);
            foreach (Quest quest in s.Done)
                w.Write((byte)quest);

            // Adventure.
            w.Write(s.Heroes.Count);
            foreach (Hero hero in s.Heroes)
            {
                w.Write(hero.Name ?? "");
                w.Write(hero.Country);
                w.Write(hero.Cell);
                w.Write(hero.Attack);
                w.Write(hero.Defence);
                w.Write(hero.Level);
                w.Write(hero.Experience);
                w.Write(hero.Moves);
                w.Write(hero.LevelUps);
                Stacks(w, hero.Army);
                Ints(w, hero.Shrines);
                w.Write(hero.Settlers);
            }
            Bools(w, s.Seen);
            w.Write(s.Rng.State);
            w.Write(s.Victories);
            w.Write(s.LairsCleared);
            w.Write(s.ChestsOpened);
            w.Write(s.Chest != null);
            if (s.Chest != null)
            {
                w.Write(s.Chest.Hero);
                w.Write(s.Chest.Cell);
                w.Write(s.Chest.Gold);
                w.Write(s.Chest.Experience);
            }

            // Crown.
            for (int k = 0; k < n; k++)
            {
                w.Write(s.Gems[k].Count);
                foreach (GemWon won in s.Gems[k])
                {
                    w.Write((byte)won.Gem);
                    w.Write(won.Turn);
                }
            }
            Ints(w, s.StartLand);
            Ints(w, s.Lairs);
            Ints(w, s.Crowned);

            // Rivals.
            w.Write((byte)s.Difficulty);
            w.Write(s.RivalsPlay);
            for (int k = 0; k < n; k++)
            {
                w.Write(s.Aims[k].Length);
                foreach (Gem gem in s.Aims[k])
                    w.Write((byte)gem);
            }

            // Settlers and stakes.
            w.Write(s.TownsFounded);
            w.Write(s.StakesSet);
            w.Write(s.BotsStake);

            // Raids.
            w.Write(s.Bands.Count);
            foreach (Raid band in s.Bands)
            {
                w.Write(band.Lair);
                w.Write(band.Town);
                w.Write(band.Cell);
                w.Write(band.Stay);
                w.Write(band.Age);
                w.Write(band.Steps);
            }
            Ints(w, s.RaidsSent);
            Ints(w, s.RaidsRepelled);
            Ints(w, s.RaidsBeaten);
            Ints(w, s.RaidTurns);
            Ints(w, s.LastRaid);
            w.Write(s.LairSent.Count);
            foreach (KeyValuePair<int, int> sent in s.LairSent)
            {
                w.Write(sent.Key);
                w.Write(sent.Value);
            }

            // Troops.
            w.Write(s.LairTroops.Count);
            foreach (Troop troop in s.LairTroops)
            {
                w.Write(troop.Country);
                w.Write(troop.Lair);
                w.Write(troop.Turns);
                w.Write(troop.Men);
                WriteStock(w, troop.Paid);
            }
            Ints(w, s.TroopsSent);
            Ints(w, s.TroopsWon);
            Ints(w, s.TroopsRecalled);
        }

        static GameState ReadState(BinaryReader r, World world)
        {
            GameState s = GameState.Blank(world);
            int n = world.Countries.Count;
            s.Turn = r.ReadInt32();
            for (int k = 0; k < n; k++)
                s.Treasury[k] = ReadStock(r);
            ReadInts(r, s.Grown);
            ReadInts(r, s.GrownLastTurn);
            int done = Count(r);
            for (int i = 0; i < done; i++)
                s.Done.Add((Quest)r.ReadByte());

            int heroes = Count(r);
            for (int i = 0; i < heroes; i++)
            {
                var hero = new Hero
                {
                    Index = i, Name = r.ReadString(), Country = r.ReadInt32(), Cell = r.ReadInt32(),
                    Attack = r.ReadInt32(), Defence = r.ReadInt32(), Level = r.ReadInt32(), Experience = r.ReadInt32(),
                    Moves = r.ReadInt32(), LevelUps = r.ReadInt32(),
                };
                ReadStacks(r, hero.Army);
                ReadInts(r, hero.Shrines);
                hero.Settlers = r.ReadBoolean();
                s.Heroes.Add(hero);
            }
            ReadBools(r, s.Seen);
            s.Rng.State = r.ReadUInt64();
            s.Victories = r.ReadInt32();
            s.LairsCleared = r.ReadInt32();
            s.ChestsOpened = r.ReadInt32();
            s.Chest = r.ReadBoolean()
                ? new Chest { Hero = r.ReadInt32(), Cell = r.ReadInt32(), Gold = r.ReadInt32(), Experience = r.ReadInt32() }
                : null;

            for (int k = 0; k < n; k++)
            {
                s.Gems[k].Clear();
                int gems = Count(r);
                for (int i = 0; i < gems; i++)
                    s.Gems[k].Add(new GemWon((Gem)r.ReadByte(), r.ReadInt32()));
            }
            ReadInts(r, s.StartLand);
            ReadInts(r, s.Lairs);
            ReadInts(r, s.Crowned);

            s.Difficulty = (Difficulty)r.ReadByte();
            s.RivalsPlay = r.ReadBoolean();
            for (int k = 0; k < n; k++)
            {
                var aims = new Gem[Count(r)];
                for (int i = 0; i < aims.Length; i++)
                    aims[i] = (Gem)r.ReadByte();
                s.Aims[k] = aims;
            }

            s.TownsFounded = r.ReadInt32();
            s.StakesSet = r.ReadInt32();
            s.BotsStake = r.ReadBoolean();

            int bands = Count(r);
            for (int i = 0; i < bands; i++)
            {
                s.Bands.Add(new Raid
                {
                    Lair = r.ReadInt32(), Town = r.ReadInt32(), Cell = r.ReadInt32(), Stay = r.ReadInt32(), Age = r.ReadInt32(), Steps = r.ReadInt32(),
                });
            }
            ReadInts(r, s.RaidsSent);
            ReadInts(r, s.RaidsRepelled);
            ReadInts(r, s.RaidsBeaten);
            ReadInts(r, s.RaidTurns);
            ReadInts(r, s.LastRaid);
            int lairs = Count(r);
            for (int i = 0; i < lairs; i++)
            {
                int lair = r.ReadInt32();
                s.LairSent[lair] = r.ReadInt32();
            }

            int troops = Count(r);
            for (int i = 0; i < troops; i++)
            {
                s.LairTroops.Add(new Troop
                {
                    Country = r.ReadInt32(), Lair = r.ReadInt32(), Turns = r.ReadInt32(), Men = r.ReadInt32(), Paid = ReadStock(r),
                });
            }
            ReadInts(r, s.TroopsSent);
            ReadInts(r, s.TroopsWon);
            ReadInts(r, s.TroopsRecalled);
            return s;
        }

        // The view and the report.

        static void WriteView(BinaryWriter w, SaveView view)
        {
            w.Write(view.Hero);
            w.Write(view.X);
            w.Write(view.Z);
            w.Write(view.Distance);
            w.Write(view.Yaw);
        }

        static SaveView ReadView(BinaryReader r) =>
            new SaveView { Hero = r.ReadInt32(), X = r.ReadSingle(), Z = r.ReadSingle(), Distance = r.ReadSingle(), Yaw = r.ReadSingle() };

        static void WriteReport(BinaryWriter w, TurnReport report)
        {
            w.Write(report != null);
            if (report == null)
                return;
            WriteStock(w, report.Income);
            w.Write(report.GrowthFood);
            w.Write(report.Events.Count);
            foreach (TurnEvent e in report.Events)
            {
                w.Write((byte)e.Kind);
                w.Write(e.City);
                w.Write((byte)e.Stop);
                w.Write((byte)e.Quest);
                w.Write((byte)e.Gem);
                w.Write(e.Country);
                w.Write(e.Cell);
                w.Write((byte)e.Monster);
                w.Write(e.Amount);
            }
        }

        static TurnReport ReadReport(BinaryReader r)
        {
            if (!r.ReadBoolean())
                return null;
            var report = new TurnReport { Income = ReadStock(r), GrowthFood = r.ReadInt32() };
            int events = Count(r);
            for (int i = 0; i < events; i++)
            {
                var kind = (TurnEventKind)r.ReadByte();
                int city = r.ReadInt32();
                var stop = (GrowthStop)r.ReadByte();
                var quest = (Quest)r.ReadByte();
                var gem = (Gem)r.ReadByte();
                int country = r.ReadInt32(), cell = r.ReadInt32();
                var monster = (Monster)r.ReadByte();
                report.Events.Add(new TurnEvent(kind, city, stop, quest, gem, country, cell, monster, r.ReadInt32()));
            }
            return report;
        }

        // Small parts.

        static void WriteStock(BinaryWriter w, Stock s)
        {
            w.Write(s.Gold);
            w.Write(s.Food);
            w.Write(s.Wood);
            w.Write(s.Stone);
        }

        static Stock ReadStock(BinaryReader r) => new Stock(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());

        static void Stacks(BinaryWriter w, List<Stack> stacks)
        {
            w.Write(stacks.Count);
            foreach (Stack stack in stacks)
            {
                w.Write((byte)stack.Unit);
                w.Write(stack.Count);
            }
        }

        static void ReadStacks(BinaryReader r, List<Stack> stacks)
        {
            int count = Count(r);
            for (int i = 0; i < count; i++)
                stacks.Add(new Stack((Unit)r.ReadByte(), r.ReadInt32()));
        }

        static void Ints(BinaryWriter w, IReadOnlyList<int> values)
        {
            w.Write(values.Count);
            for (int i = 0; i < values.Count; i++)
                w.Write(values[i]);
        }

        static void ReadInts(BinaryReader r, List<int> values)
        {
            int count = Count(r);
            for (int i = 0; i < count; i++)
                values.Add(r.ReadInt32());
        }

        /// <summary>Reads into an array of a fixed length, such as one value per kingdom; another length is a damaged file.</summary>
        static void ReadInts(BinaryReader r, int[] values)
        {
            int count = Count(r);
            if (count != values.Length)
                throw Damaged($"{count} values where {values.Length} belong");
            for (int i = 0; i < count; i++)
                values[i] = r.ReadInt32();
        }

        /// <summary>Booleans eight to a byte, after their count.</summary>
        static void Bools(BinaryWriter w, bool[] values)
        {
            w.Write(values.Length);
            for (int i = 0; i < values.Length; i += 8)
            {
                int bits = 0;
                for (int b = 0; b < 8 && i + b < values.Length; b++)
                    bits |= values[i + b] ? 1 << b : 0;
                w.Write((byte)bits);
            }
        }

        static void ReadBools(BinaryReader r, bool[] values)
        {
            int count = Count(r);
            if (count != values.Length)
                throw Damaged($"{count} cells seen where the map has {values.Length}");
            for (int i = 0; i < count; i += 8)
            {
                int bits = r.ReadByte();
                for (int b = 0; b < 8 && i + b < count; b++)
                    values[i + b] = (bits & (1 << b)) != 0;
            }
        }

        static int Count(BinaryReader r)
        {
            int count = r.ReadInt32();
            if (count < 0 || count > MostItems)
                throw Damaged($"a list of {count}");
            return count;
        }

        /// <summary>FNV-1a over the body.</summary>
        static uint Hash(byte[] bytes)
        {
            uint hash = 2166136261;
            foreach (byte b in bytes)
                hash = (hash ^ b) * 16777619;
            return hash;
        }
    }
}

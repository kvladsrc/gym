using System.Collections.Generic;
using Realms.Core;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// What the map shows of the growing border (the territory plan of 2026-11-11): the cell each
    /// of the players' towns takes next, outlined with dashes in the kingdom's colour with the
    /// kingdom's small flag in it; the other kingdoms' next cells, dashes without a flag, while
    /// Alt is held; dots on the wild cells a chosen town of the players reaches; and a short flash
    /// over the cells the players' border has just taken; the players' stake (вешка) as a taller
    /// flag, and dots on the cells of the way their town takes to it; and a neighbour's flag on each lair its
    /// troop besieges (the economy plan's step 4). Without them the land grew unseen, as if
    /// the game played itself. The marks go to the overlay (RealmsLines in RealmsCommon.hlsl) in a
    /// texture laid out like the cells' one, _RealmsMarks: red the players' kingdom + 1 for their
    /// marks, green what is drawn (<see cref="Own"/>, <see cref="Other"/>, <see cref="InReach"/>,
    /// <see cref="Taken"/>, <see cref="Ring"/>), blue the other kingdom + 1 for its mark. The guiding
    /// lines' gold rings (the interface plan's 3.3) ride in the same texture.
    /// </summary>
    sealed class BorderMarks
    {
        public const int Own = 1, Other = 2, InReach = 4, Taken = 8;

        /// <summary>A gold ring where the guiding lines point (<see cref="SetGuide"/>).</summary>
        public const int Ring = 16;

        static readonly int MarksId = Shader.PropertyToID("_RealmsMarks");
        static readonly int ShowId = Shader.PropertyToID("_RealmsMarkShow");

        /// <summary>How tall the small flag's pole is: as the flag on a town's waiting cart (HeroView).</summary>
        const float FlagHeight = 0.3f;

        /// <summary>How tall the stake's flag is: taller than a next cell's, as it is the players' own.</summary>
        const float StakeHeight = 0.5f;

        /// <summary>How tall a besieging troop's flag is: as the stake's, so it shows over the lair's rocks.</summary>
        const float TroopHeight = 0.5f;

        readonly World world;
        readonly TerrainField field;
        readonly Material material;
        readonly Transform root;
        readonly Texture2D texture;
        readonly Color32[] pixels;

        /// <summary>The small flags on the players' towns' next cells, by cell.</summary>
        readonly Dictionary<int, GameObject> flags = new Dictionary<int, GameObject>();

        /// <summary>The flag on the players' stake, and its cell (-1: none).</summary>
        GameObject stakeFlag;
        int stakeCell = -1;

        /// <summary>
        /// The neighbours' troops at the lairs (GameState.LairTroops), or null; their flags by lair,
        /// with the kingdom they show. A flag stands only on a lair the players have seen: under the
        /// fog it would tell where the lair is.
        /// </summary>
        readonly IReadOnlyList<Troop> troops;
        readonly System.Func<int, bool> seen;
        readonly Dictionary<int, (GameObject flag, int country)> troopFlags = new Dictionary<int, (GameObject, int)>();

        readonly HashSet<int> taken = new HashSet<int>();
        readonly HashSet<int> guide = new HashSet<int>();
        int chosenTown = -1;
        float takenAt = -100f;
        bool others;

        public BorderMarks(World world, TerrainField field, Material material, Transform root, IReadOnlyList<Troop> troops,
            System.Func<int, bool> seen)
        {
            this.world = world;
            this.troops = troops;
            this.seen = seen;
            this.field = field;
            this.material = material;
            this.root = root;
            texture = new Texture2D(world.Width, world.Height, TextureFormat.RGBA32, false, true)
            {
                name = "Realms border marks", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            pixels = new Color32[world.Cells.Length];
        }

        /// <summary>Works the marks out again for the world as it is now and puts the flags where they go.</summary>
        public void Update()
        {
            System.Array.Clear(pixels, 0, pixels.Length);
            var wanted = new HashSet<int>();
            foreach (City city in world.Cities)
            {
                int next = Influence.Next(world, city);
                if (next < 0)
                    continue;
                bool own = city.Country == world.Player;
                Mark(next, city.Country, own ? Own : Other);
                if (own)
                    wanted.Add(next);
            }
            int stake = world.Countries[world.Player].Stake;
            var way = new List<int>();
            if (stake >= 0 && Influence.StakeWay(world, world.Player, stake, way) >= 0)
            {
                foreach (int cell in way)
                    Mark(cell, world.Player, InReach);
                // The stake's own flag stands there; no small one under it.
                wanted.Remove(stake);
            }
            if (chosenTown >= 0 && chosenTown < world.Cities.Count)
            {
                City city = world.Cities[chosenTown];
                foreach (int cell in Influence.Walk(world, city, Influence.Reach(city)).Keys)
                {
                    if (world.Cells[cell].Owner < 0 && !Influence.NearOtherTown(world, city.Country, cell))
                        Mark(cell, city.Country, InReach);
                }
            }
            foreach (int cell in taken)
            {
                if (world.Cells[cell].Owner >= 0)
                    Mark(cell, world.Cells[cell].Owner, Taken);
            }
            foreach (int cell in guide)
            {
                if (cell < 0 || cell >= pixels.Length)
                    continue;
                Color32 p = pixels[cell];
                p.g |= (byte)Ring;
                pixels[cell] = p;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            Shader.SetGlobalTexture(MarksId, texture);
            SetShow();
            PlaceFlags(wanted);
            PlaceStake(stake);
            PlaceTroops();
        }

        /// <summary>The troops' flags again, as the fog clears: a lair just seen may have one.</summary>
        public void UpdateTroops() => PlaceTroops();

        /// <summary>Red holds the players' kingdom for their marks, blue the other kingdom for its mark.</summary>
        void Mark(int cell, int country, int kind)
        {
            Color32 p = pixels[cell];
            if (kind == Other)
                p.b = (byte)(country + 1);
            else
                p.r = (byte)(country + 1);
            p.g |= (byte)kind;
            pixels[cell] = p;
        }

        /// <summary>The players' town whose reach shows as dots: the chosen cell's town, or none.</summary>
        public void Choose(int cell)
        {
            int town = cell >= 0 && world.Cells[cell].Owner == world.Player ? world.Cells[cell].Town : -1;
            if (town == chosenTown)
                return;
            chosenTown = town;
            Update();
        }

        /// <summary>The cells the guiding lines point at get gold rings; null or none: no rings.</summary>
        public void SetGuide(IEnumerable<int> cells)
        {
            var wanted = cells != null ? new HashSet<int>(cells) : new HashSet<int>();
            if (wanted.SetEquals(guide))
                return;
            guide.Clear();
            guide.UnionWith(wanted);
            Update();
        }

        /// <summary>The other kingdoms' next cells show while Alt is held.</summary>
        public void ShowOthers(bool show)
        {
            if (show == others)
                return;
            others = show;
            SetShow();
        }

        /// <summary>The cells the players' border has just taken flash in the kingdom's colour and settle.</summary>
        public void Flash(IEnumerable<int> cells)
        {
            taken.Clear();
            foreach (int cell in cells)
                taken.Add(cell);
            takenAt = Time.timeSinceLevelLoad;
            Update();
        }

        void SetShow() => Shader.SetGlobalVector(ShowId, new Vector4(others ? 1f : 0f, takenAt, 0f, 0f));

        /// <summary>
        /// A small flag of the kingdom on each next cell, where a hero would stand on it (HeroView.Stand,
        /// out of the way of a resource's building); flags of cells no longer next go.
        /// </summary>
        void PlaceFlags(HashSet<int> wanted)
        {
            var gone = new List<int>();
            foreach (int cell in flags.Keys)
            {
                if (!wanted.Contains(cell))
                    gone.Add(cell);
            }
            foreach (int cell in gone)
            {
                Figures.Destroy(flags[cell]);
                flags.Remove(cell);
            }
            foreach (int cell in wanted)
            {
                if (flags.ContainsKey(cell))
                    continue;
                flags[cell] = NewFlag("Next cell's flag", cell, FlagHeight);
            }
        }

        /// <summary>The stake's flag moves with the stake and goes with it.</summary>
        void PlaceStake(int cell)
        {
            if (cell == stakeCell)
                return;
            if (stakeFlag != null)
                Figures.Destroy(stakeFlag);
            stakeFlag = null;
            stakeCell = cell;
            if (cell < 0)
                return;
            stakeFlag = NewFlag("The players' stake", cell, StakeHeight);
        }

        /// <summary>A flag of the troop's kingdom on each besieged lair the players have seen; flags of troops gone or done go.</summary>
        void PlaceTroops()
        {
            var wanted = new Dictionary<int, int>();
            if (troops != null)
            {
                foreach (Troop troop in troops)
                {
                    if (seen == null || seen(troop.Lair))
                        wanted[troop.Lair] = troop.Country;
                }
            }
            var gone = new List<int>();
            foreach (KeyValuePair<int, (GameObject flag, int country)> pair in troopFlags)
            {
                if (!wanted.TryGetValue(pair.Key, out int country) || country != pair.Value.country)
                    gone.Add(pair.Key);
            }
            foreach (int cell in gone)
            {
                Figures.Destroy(troopFlags[cell].flag);
                troopFlags.Remove(cell);
            }
            foreach (KeyValuePair<int, int> pair in wanted)
            {
                if (!troopFlags.ContainsKey(pair.Key))
                    troopFlags[pair.Key] = (NewFlag("A neighbour's troop", pair.Key, TroopHeight, pair.Value), pair.Value);
            }
        }

        GameObject NewFlag(string name, int cell, float height) => NewFlag(name, cell, height, world.Player);

        GameObject NewFlag(string name, int cell, float height, int country)
        {
            var flag = new GameObject(name);
            flag.transform.SetParent(root, false);
            Vector2 stand = HeroView.Stand(world, field, cell);
            float x = world.CenterX(cell) + stand.x, z = world.CenterZ(cell) + stand.y;
            flag.transform.localPosition = new Vector3(x, Mathf.Max(field.Height(x, z), 0f), z);
            Figures.Banner(material, flag.transform, Vector3.zero, height / Figures.BannerHeight, country);
            return flag;
        }

        public void Dispose()
        {
            if (stakeFlag != null)
                Figures.Destroy(stakeFlag);
            stakeFlag = null;
            stakeCell = -1;
            foreach (GameObject flag in flags.Values)
                Figures.Destroy(flag);
            flags.Clear();
            foreach (var (flag, _) in troopFlags.Values)
                Figures.Destroy(flag);
            troopFlags.Clear();
            Object.Destroy(texture);
        }
    }
}

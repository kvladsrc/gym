using System.Collections.Generic;
using System.Linq;
using Realms.Core;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// Dresses the map: trees, the rocks of the mountains and the boulders (studio models, their
    /// copies merged per model, <see cref="ModelBatch"/>); low-poly bushes, grass tufts, flowers,
    /// pebbles, reeds, haystacks, fences, signposts, standing stones and banners (merged per block of
    /// cells), and low-poly trees and peaks without the art; boats, piers and windmills with
    /// their own small meshes so they can move; and the studio models for towns, their buildings,
    /// the field buildings, sheep, the monster groups and the places heroes visit. Nothing stands
    /// on a road. Towns, resources, groups and places change during the game, so each such cell
    /// has its own object that <see cref="RebuildCell"/> makes again; every cell draws from its
    /// own random sequence, so the rest of a rebuilt cell stays as it was. Mountains have their
    /// own objects too, so that, like the others, <see cref="ShowCell"/> can hide them while
    /// the cell is unseen: their peaks would stand out of the clouds.
    /// </summary>
    public sealed partial class PropBuilder
    {
        /// <summary>
        /// The props of the cells without objects of their own are merged per block of this many
        /// columns and rows, each block an object of its own, so that a town founded or grown
        /// dresses again only the blocks round it (<see cref="Redress"/>): the players found the
        /// game hanging for seconds when a town was founded, as the whole map was dressed again
        /// (2026-11-11). Blocks of 10 by 6 cells took about 100 ms each, and the neighbours'
        /// quarters and the monster groups spread over a dozen of them made a turn's end take up
        /// to 1.8 s by turn 12 (capture of 2026-11-11); a block a quarter that size dresses in a
        /// quarter of the time.
        /// </summary>
        const int BlockColumns = 5, BlockRows = 3;

        /// <summary>
        /// The studio trees (the style-D trees of the Studio project 'procedural') by where they
        /// grow: leafy and tall oaks in the woods, young round trees on open land, wind-bent ones
        /// on the hills, willows by the water; a wood has a dead tree now and then, and here and
        /// there a tree has turned ochre. Conifers stand where the land favours pines; while the
        /// art has none, broadleaf trees stand there too, so a wood keeps one style.
        /// </summary>
        internal static readonly string[] WoodTrees = { "oak_leafy_2", "oak_leafy_4", "oak_leafy_5", "oak_tall_1", "oak_tall_2", "broadleaf_1" };
        internal static readonly string[] OpenTrees = { "young_round_2", "young_round_3", "broadleaf_1", "oak_leafy_5" };
        internal static readonly string[] HillTrees = { "windswept_2", "young_round_3", "oak_leafy_4" };
        internal static readonly string[] DeadTrees = { "snag_1", "oak_bare_4", "oak_bare_5" };
        static readonly string[] WaterTrees = { "willow_1" };
        static readonly string[] AutumnTrees = { "ochre_2" };
        internal static readonly string[] Conifers = { "spruce_2", "spruce_3", "conifer_4" };

        /// <summary>The earlier trees, for art that has none of the above.</summary>
        static readonly string[] OldTrees = { "linden" };

        static readonly string[] TreeModels = new[] { WoodTrees, OpenTrees, HillTrees, DeadTrees, WaterTrees, AutumnTrees, Conifers, OldTrees }
            .SelectMany(kinds => kinds).Distinct().ToArray();

        /// <summary>How tall a studio tree stands, before its own spread (0.4 when not listed).</summary>
        static readonly Dictionary<string, float> TreeHeights = new Dictionary<string, float>
        {
            ["oak_tall_1"] = 0.48f, ["oak_tall_2"] = 0.48f, ["young_round_2"] = 0.32f, ["young_round_3"] = 0.32f,
            ["windswept_2"] = 0.36f, ["snag_1"] = 0.38f, ["oak_bare_4"] = 0.42f, ["oak_bare_5"] = 0.42f,
            ["oak_leafy_2"] = 0.42f, ["oak_leafy_4"] = 0.42f, ["oak_leafy_5"] = 0.42f, ["broadleaf_1"] = 0.42f,
            ["spruce_2"] = 0.46f, ["spruce_3"] = 0.46f, ["conifer_4"] = 0.44f,
        };

        /// <summary>How tall a studio tree stands on the map, a cell's radius being 1, before its own spread.</summary>
        internal static float TreeHeight(string name) => TreeHeights.TryGetValue(name, out float tall) ? tall : 0.4f;

        /// <summary>
        /// Buildings whose models came out big for what they are: a quarry as big as a castle and a
        /// granary bigger than the houses read as the cell's main building. Their size is
        /// multiplied by this.
        /// </summary>
        static readonly Dictionary<string, float> Smaller = new Dictionary<string, float> { ["quarry"] = 0.68f, ["granary"] = 0.68f };

        static float SizeFactor(GameObject prefab) => Smaller.TryGetValue(prefab.name, out float k) ? k : 1f;

        /// <summary>
        /// The upright monsters on the map (people, goblins, a troll) at this share of their battle
        /// size: goblins stood as tall as the trees beside them. The bandits now stand about 0.7 of
        /// a tree; the troll, the biggest, a little under a tree.
        /// </summary>
        const float UprightOnMap = 0.68f;

        /// <summary>The studio bushes, the wood's stumps and fallen logs, and its stones.</summary>
        static readonly string[] BushModels = { "bush_round", "bush_wide", "juniper" };
        static readonly string[] DeadwoodModels = { "stump", "log" };
        static readonly string[] RockModels = { "boulder", "boulder_mossy", "outcrop" };
        static readonly string[] BareRockModels = { "boulder", "outcrop" };

        /// <summary>
        /// The studio models scattered by the hundred, merged per block or cell instead of standing
        /// as objects of their own: Setup gives each one readable mesh. The towns' and roads' small
        /// things (lamps, signposts, fence spans, carts, casks, stalls) and the heaps of gold are
        /// merged the same way. The boat rocks on its own, but its plan is measured from the same
        /// readable mesh.
        /// </summary>
        public static readonly string[] ScatterModels = TreeModels.Concat(BushModels).Concat(DeadwoodModels).Concat(RockModels)
            .Concat(new[] { "peak", "peak_snow", "crag", "standing_stone", "mushrooms", "pebbles", "haystack", "logpile", "goldore", "stoneblocks", "wall", "tower" })
            .Concat(new[] { "lamp", "signpost", "fence", "cart", "barrels", "stall", "gold", "boat" })
            .Distinct().ToArray();

        /// <summary>Whether a model of the art is one of the map's trees.</summary>
        public static bool IsTree(string name) => System.Array.IndexOf(TreeModels, name) >= 0;
        /// <summary>
        /// A town's homes: the two cottages and the workshops, all drawn in the castles' style,
        /// the cottages between the workshops so that a town is not a row of crafts.
        /// </summary>
        static readonly string[] HouseModels = { "cottage_a", "tannery", "cottage_b", "weaver", "tailor", "smelter", "spinnery", "hunters_lodge" };

        /// <summary>The art key of house <paramref name="k"/> of a city.</summary>
        public static string HouseOf(City city, int k) => HouseModels[(city.Index * 2 + k) % HouseModels.Length];

        /// <summary>The art key of a town building.</summary>
        public static string ModelOf(TownBuilding building)
        {
            switch (building)
            {
                case TownBuilding.Market: return "market";
                case TownBuilding.Granary: return "granary";
                case TownBuilding.Barracks: return "barracks";
                case TownBuilding.ArcheryRange: return "archery_range";
                case TownBuilding.Stables: return "stables";
                default: return "tavern";
            }
        }

        /// <summary>The art key of a unit's model.</summary>
        public static string ModelOf(Unit unit)
        {
            switch (unit)
            {
                case Unit.Spearmen: return "spearman";
                case Unit.Archers: return "archer";
                case Unit.Riders: return "rider";
                case Unit.Goblins: return "goblin";
                case Unit.Boars: return "boar";
                case Unit.Wolves: return "wolf";
                case Unit.Bandits: return "bandit";
                case Unit.Skeletons: return "skeleton";
                case Unit.Spiders: return "spider";
                default: return "troll";
            }
        }

        /// <summary>
        /// How big one figure of the unit stands on the map, a cell's radius being 1: its largest
        /// dimension, so the upright ones (their spears and bows reach above them) are given more.
        /// </summary>
        public static float SizeOf(Unit unit)
        {
            switch (unit)
            {
                case Unit.Troll: return 0.6f;
                case Unit.Goblins: return 0.42f;
                case Unit.Wolves:
                case Unit.Boars: return 0.38f;
                case Unit.Spiders: return 0.45f;
                case Unit.Riders: return 0.6f;
                default: return 0.5f;
            }
        }

        /// <summary>A stand-in colour for a unit whose model is missing.</summary>
        public static Color ColourOf(Unit unit)
        {
            switch (unit)
            {
                case Unit.Troll: return Palette.Hex("#7A8E6D");
                case Unit.Wolves: return Palette.Hex("#8D8E9C");
                case Unit.Boars: return Palette.Hex("#8A5A3C");
                case Unit.Goblins: return Palette.Hex("#739F56");
                case Unit.Bandits: return Palette.Hex("#864D41");
                case Unit.Skeletons: return Palette.Hex("#E8DFC6");
                case Unit.Spiders: return Palette.Hex("#4A3B5C");
                case Unit.Archers: return Palette.Hex("#4D8055");
                case Unit.Riders: return Palette.Hex("#B39760");
                default: return Palette.Hex("#5171A4");
            }
        }

        /// <summary>The art key of a place's model, or null for the piles made here.</summary>
        public static string ModelOf(Place place)
        {
            switch (place)
            {
                case Core.Place.Chest: return "chest";
                case Core.Place.ShrineAttack: return "shrine_attack";
                case Core.Place.ShrineDefence: return "shrine_defence";
                case Core.Place.Watchtower: return "watchtower";
                default: return null;
            }
        }

        /// <summary>The art key of a field building.</summary>
        public static string ModelOf(FieldBuilding building)
        {
            switch (building)
            {
                case FieldBuilding.Farm: return "farm";
                case FieldBuilding.Fishery: return "fishery";
                case FieldBuilding.LumberCamp: return "lumber_camp";
                case FieldBuilding.Quarry: return "quarry";
                case FieldBuilding.GoldMine: return "gold_mine";
                default: return null;
            }
        }

        /// <summary>Half a road's width with its rim (Terrain.shader: 0.06 + 0.027) and the warp that bends it: keep props this far.</summary>
        const float RoadClear = 0.14f;

        /// <summary>The gravel bank and a little more: keep props this far from a river's water.</summary>
        const float RiverClear = RiverCourse.Bank + 0.03f;

        readonly World world;
        readonly TerrainField field;
        readonly ArtLibrary art;
        readonly Material propMaterial;
        readonly Transform parent;
        readonly int blocksX;
        readonly GameObject[] blocks;
        readonly Dictionary<string, ModelBatch.Source> sources = new Dictionary<string, ModelBatch.Source>();
        readonly Noise2 pineness;
        readonly List<Vector2> placed = new List<Vector2>();
        readonly Dictionary<int, GameObject> cellRoots = new Dictionary<int, GameObject>();
        readonly ContactShade contact;
        Rng rng;
        Cell here;

        /// <summary>Where props of the cell being dressed go: its own object and mesh, or its block's.</summary>
        Transform root;

        MeshKit cellKit;
        ModelBatch cellBatch;
        FlagCloth cellCloth;
        SmokePlumes cellSmoke;

        /// <param name="contact">Where the shade round the feet of houses, walls and trees goes, or null for none.</param>
        public PropBuilder(World world, TerrainField field, ArtLibrary art, Material propMaterial, Transform parent, ContactShade contact = null)
        {
            this.world = world;
            this.field = field;
            this.art = art;
            this.propMaterial = propMaterial;
            this.parent = parent;
            this.contact = contact;
            pineness = new Noise2(world.Seed * 13 + 5);
            root = parent;
            blocksX = (world.Width + BlockColumns - 1) / BlockColumns;
            blocks = new GameObject[blocksX * ((world.Height + BlockRows - 1) / BlockRows)];
        }

        /// <summary>The block whose object holds a cell's props, while the cell has no object of its own.</summary>
        int BlockOf(int index) => index / world.Width / BlockRows * blocksX + index % world.Width / BlockColumns;

        /// <summary>The mesh the cell being dressed merges its low-poly props into.</summary>
        MeshKit Kit() => cellKit;

        /// <summary>Where the cell being dressed puts its copies of studio models.</summary>
        ModelBatch Batch() => cellBatch;

        /// <summary>A scatter model of the art, or null without it.</summary>
        ModelBatch.Source Scatter(string name)
        {
            if (!sources.TryGetValue(name, out ModelBatch.Source source))
                sources[name] = source = art != null ? ModelBatch.Source.Of(art.ModelOf(name)) : null;
            return source;
        }

        Vector3 Ground(float x, float z) => new Vector3(x, field.Height(x, z), z);

        public void Build()
        {
            foreach (float _ in BuildSteps())
            {
            }
        }

        /// <summary>
        /// Dresses the whole map as <see cref="Build"/> does, telling after each cell of its own
        /// and each block how far it has come, 0 to 1, so that a loading screen can be drawn
        /// between them (<see cref="MapView.ShowSteps"/>).
        /// </summary>
        public System.Collections.Generic.IEnumerable<float> BuildSteps()
        {
            var own = new List<Cell>();
            foreach (Cell cell in world.Cells)
            {
                if (Changes(cell) || cell.Biome == Biome.Mountains)
                    own.Add(cell);
            }
            float total = own.Count + blocks.Length, done = 0f;
            foreach (Cell cell in own)
            {
                DressOwn(cell);
                yield return ++done / total;
            }
            for (int block = 0; block < blocks.Length; block++)
            {
                DressBlock(block);
                yield return ++done / total;
            }
            contact?.Draw();
        }

        /// <summary>Towns, resources, monster groups and places change during the game.</summary>
        static bool Changes(Cell cell) =>
            cell.Town >= 0 || cell.Resource != Resource.None || cell.Monster != Monster.None || cell.Place != Core.Place.None;

        readonly HashSet<int> dressedOwn = new HashSet<int>();

        /// <summary>Dresses a town or resource cell again after it changed.</summary>
        public void RebuildCell(int index)
        {
            if (cellRoots.TryGetValue(index, out GameObject old))
            {
                Remove(old);
                cellRoots.Remove(index);
            }
            if (Changes(world.Cells[index]) || dressedOwn.Contains(index))
                DressOwn(world.Cells[index]);
        }

        /// <summary>
        /// Dresses the cells again after they changed: a cell with an object of its own gets it
        /// anew (<see cref="RebuildCell"/>), and so does a cell that has just become a town's;
        /// the blocks of all the others, and of those that have just left their block, are
        /// dressed again whole. A cell draws from its own random sequence, so the rest of a
        /// block stays as it was.
        /// </summary>
        /// <returns>How many blocks were dressed again.</returns>
        /// <param name="now">
        /// Whether a block, known by the cell in its middle, is dressed at once; the others wait
        /// for <see cref="DressPending"/>, one a frame. Null dresses them all at once.
        /// </param>
        public int Redress(IEnumerable<int> cells, System.Func<int, bool> now = null)
        {
            var again = new HashSet<int>();
            foreach (int index in cells)
            {
                bool own = dressedOwn.Contains(index);
                RebuildCell(index);
                if (!own)
                    again.Add(BlockOf(index));
            }
            int dressed = 0;
            foreach (int block in again)
            {
                if (now != null && !now(MiddleOf(block)))
                {
                    if (pendingSet.Add(block))
                        pending.Add(block);
                    continue;
                }
                DressAgain(block);
                dressed++;
            }
            return dressed;
        }

        /// <summary>The blocks off the screen waiting to be dressed again, in the order they came.</summary>
        readonly List<int> pending = new List<int>();
        readonly HashSet<int> pendingSet = new HashSet<int>();

        /// <summary>How many blocks wait to be dressed again.</summary>
        public int Pending => pending.Count;

        /// <summary>Dresses one waiting block again; false when none waits.</summary>
        public bool DressPending()
        {
            if (pending.Count == 0)
                return false;
            DressAgain(pending[0]);
            return true;
        }

        void DressAgain(int block)
        {
            if (pendingSet.Remove(block))
                pending.Remove(block);
            if (blocks[block] != null)
                Remove(blocks[block]);
            DressBlock(block);
        }

        /// <summary>The cell in the middle of a block.</summary>
        int MiddleOf(int block)
        {
            int col = Mathf.Min(world.Width - 1, block % blocksX * BlockColumns + BlockColumns / 2);
            int row = Mathf.Min(world.Height - 1, block / blocksX * BlockRows + BlockRows / 2);
            return row * world.Width + col;
        }

        /// <summary>Removes a cell's or block's object with the meshes made for it; the studio models' own meshes stay.</summary>
        static void Remove(GameObject go)
        {
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh != null && mesh.name.StartsWith("Props "))
                    Object.Destroy(mesh);
            }
            Object.Destroy(go);
        }

        /// <summary>Dresses the cells of a block that have no objects of their own into the block's object.</summary>
        void DressBlock(int block)
        {
            var go = new GameObject($"Block {block}");
            go.transform.SetParent(parent, false);
            root = go.transform;
            cellKit = new MeshKit();
            cellBatch = new ModelBatch();
            int col0 = block % blocksX * BlockColumns, row0 = block / blocksX * BlockRows;
            for (int row = row0; row < Mathf.Min(world.Height, row0 + BlockRows); row++)
            for (int col = col0; col < Mathf.Min(world.Width, col0 + BlockColumns); col++)
            {
                int index = row * world.Width + col;
                if (!dressedOwn.Contains(index))
                    Dress(world.Cells[index]);
            }
            if (cellKit.VertexCount > 0)
            {
                go.AddComponent<MeshFilter>().sharedMesh = cellKit.ToMesh($"Props block {block}");
                go.AddComponent<MeshRenderer>().sharedMaterial = propMaterial;
            }
            cellBatch.Build(go.transform, $"block {block}");
            blocks[block] = go;
            root = parent;
            cellKit = null;
            cellBatch = null;
        }

        /// <summary>
        /// Shows or hides a cell's own object (a town, resource, monster group or place): hidden
        /// while the cell is unseen, so nothing of it stands out of the clouds.
        /// </summary>
        public void ShowCell(int index, bool shown)
        {
            if (cellRoots.TryGetValue(index, out GameObject go) && go.activeSelf != shown)
                go.SetActive(shown);
        }

        /// <summary>Dresses a cell into its own object with its own merged mesh.</summary>
        void DressOwn(Cell cell)
        {
            dressedOwn.Add(cell.Index);
            var go = new GameObject($"Cell {cell.Index}");
            go.transform.SetParent(parent, false);
            root = go.transform;
            cellKit = new MeshKit();
            cellBatch = new ModelBatch();
            cellCloth = new FlagCloth();
            cellSmoke = new SmokePlumes();
            Dress(cell);
            if (cellKit.VertexCount > 0)
            {
                go.AddComponent<MeshFilter>().sharedMesh = cellKit.ToMesh($"Props cell {cell.Index}");
                go.AddComponent<MeshRenderer>().sharedMaterial = propMaterial;
            }
            if (cellCloth.VertexCount > 0)
            {
                var flag = new GameObject("Flag");
                flag.transform.SetParent(go.transform, false);
                flag.AddComponent<MeshFilter>().sharedMesh = cellCloth.ToMesh($"Props flag {cell.Index}");
                flag.AddComponent<MeshRenderer>().sharedMaterial = FlagArt.ClothMaterial;
            }
            if (cellSmoke.VertexCount > 0 && SmokePlumes.Material != null)
            {
                var smoke = new GameObject("Smoke");
                smoke.transform.SetParent(go.transform, false);
                smoke.AddComponent<MeshFilter>().sharedMesh = cellSmoke.ToMesh($"Props smoke {cell.Index}");
                var renderer = smoke.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = SmokePlumes.Material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            cellBatch.Build(go.transform, $"cell {cell.Index}");
            cellRoots[cell.Index] = go;
            root = parent;
            cellKit = null;
            cellBatch = null;
            cellCloth = null;
            cellSmoke = null;
        }

        void Dress(Cell cell)
        {
            {
                float cx = world.CenterX(cell.Index), cz = world.CenterZ(cell.Index);
                contact?.Begin(cell.Index);
                placed.Clear();
                here = cell;
                // The town, the resource or the lair first, then (from a sequence of its own) the land's
                // plants and stones, which therefore stay put when the town or the resource changes.
                rng = new Rng(world.Seed * 13 + 7 + cell.Index * 7919);
                if (cell.IsWater)
                {
                    Water(cell, cx, cz);
                    return;
                }
                // Nothing grows inside a town's walls; elsewhere a town and its quarters keep their places clear.
                bool walled = cell.City >= 0 && world.Cities[cell.City].Buildings.Contains(TownBuilding.Walls);
                float clear = walled ? 1f : cell.Town >= 0 ? 0.82f : cell.Monster != Monster.None || cell.Place != Core.Place.None ? 0.6f
                    : cell.Resource == Resource.Sheep ? 0.52f : cell.Resource != Resource.None ? (cell.Building != FieldBuilding.None ? 0.55f : 0.36f) : 0f;

                if (cell.City >= 0)
                    City(world.Cities[cell.City], cx, cz);
                else if (cell.District >= 0)
                    Quarter(world.Cities[cell.District], cell.Index, cx, cz);
                bool place = cell.Place != Core.Place.None;
                if (place)
                    PlaceProps(cell, cx, place && cell.Monster != Monster.None ? cz + 0.2f : cz);
                bool guarded = Guarded(cell);
                if (cell.Monster != Monster.None)
                {
                    Vector2 offset = GroupOffset(cell);
                    Group(cell, cx + offset.x, cz + offset.y);
                }
                ResourceProps(cell, cx, cz, guarded);
                rng = new Rng(world.Seed * 31 + 3 + cell.Index * 104729);
                if (cell.Town < 0 && cell.Monster == Monster.None && !place && Bits(cell.Roads) >= 3)
                    Signpost(cell, cx, cz);
                // The chance is drawn whoever owns the cell, so that a cell coming into a kingdom's
                // land loses its stones but keeps its trees where they stood.
                bool wild = cell.Owner < 0 && cell.City < 0 && cell.Monster == Monster.None && !place && cell.Resource == Resource.None;
                if (cell.Roads == 0 && (cell.Biome == Biome.Meadow || cell.Biome == Biome.Plains || cell.Biome == Biome.Hills)
                    && rng.Chance(0.07f) && wild)
                {
                    StandingStones(cx, cz);
                    clear = Mathf.Max(clear, 0.3f);
                }
                switch (cell.Biome)
                {
                    case Biome.Forest:
                        Trees(cx, cz, clear > 0 ? rng.Range(5, 8) : rng.Range(10, 14), clear, 0.19f);
                        Bushes(cx, cz, rng.Range(1, 3), clear);
                        Mushrooms(cx, cz, rng.Range(0, 3), clear);
                        Deadwood(cx, cz, rng.Range(0, 3), clear);
                        if (cell.Town < 0 && rng.Chance(0.35f))
                            Rocks(cx, cz, 1);
                        break;
                    case Biome.Mountains: Peaks(cx, cz); break;
                    case Biome.Hills:
                        if (rng.Chance(0.45f))
                            Trees(cx, cz, rng.Range(1, 4), Mathf.Max(clear, 0.3f), 0.25f, 0.6f);
                        if (rng.Chance(0.5f) && cell.Town < 0)
                            Rocks(cx, cz, rng.Range(1, 3));
                        Bushes(cx, cz, rng.Range(0, 3), Mathf.Max(clear, 0.2f));
                        Tufts(cx, cz, rng.Range(2, 5), Mathf.Max(clear, 0.15f), false);
                        break;
                    case Biome.Meadow:
                        if (rng.Chance(0.4f))
                            Trees(cx, cz, rng.Range(1, 4), Mathf.Max(clear, 0.3f), 0.25f, -0.6f);
                        Bushes(cx, cz, rng.Range(1, 4), Mathf.Max(clear, 0.2f));
                        Flowers(cx, cz, rng.Range(1, 4), Mathf.Max(clear, 0.15f));
                        Tufts(cx, cz, rng.Range(3, 6), Mathf.Max(clear, 0.15f), false);
                        break;
                    case Biome.Plains:
                        if (rng.Chance(0.15f))
                            Trees(cx, cz, 1, Mathf.Max(clear, 0.3f), 0.25f, -1f);
                        Bushes(cx, cz, rng.Range(0, 3), Mathf.Max(clear, 0.2f), 0.5f);
                        Pebbles(cx, cz, rng.Range(0, 3), Mathf.Max(clear, 0.15f));
                        Tufts(cx, cz, rng.Range(4, 8), Mathf.Max(clear, 0.15f), true);
                        break;
                    case Biome.Beach:
                        if (rng.Chance(0.3f))
                            Rocks(cx, cz, 1);
                        break;
                }
                Reeds(cell, cx, cz);
                RiverReeds(cell, cx, cz);
                Bridges(cell, cx, cz);
            }
        }

        /// <summary>
        /// A resource as it lies in the wild (haystacks, boulders, logs, a few sheep) and, once its
        /// field building stands, the building beside it: a farm with a windmill, a lumber camp, a
        /// quarry, a gold mine, a fenced pasture. A <paramref name="guarded"/> resource's heap lies
        /// behind the monsters that guard it.
        /// </summary>
        void ResourceProps(Cell cell, float cx, float cz, bool guarded)
        {
            if (cell.Resource == Resource.None || cell.City >= 0)
                return;
            bool built = cell.Building != FieldBuilding.None;
            fieldPrint = null;
            Vector2 heap = new Vector2(cx, cz);
            if (guarded)
                // Behind the monsters that guard it (Dress), towards the corner farthest from the roads.
                heap = Around(cx, cz, FreeCorner(cell.Roads), 0.34f);
            else if (built && cell.Resource != Resource.Sheep)
            {
                Vector2 at = FieldModel(cell.Building, cx, cz);
                // The resource's own props move to the other side of the building.
                Vector2 away = new Vector2(cx, cz) - at;
                float angle = away.sqrMagnitude > 1e-4f ? Mathf.Atan2(away.y, away.x) : rng.Range(0f, Mathf.PI * 2f);
                heap = Around(cx, cz, angle, 0.46f, 0.08f);
            }
            switch (cell.Resource)
            {
                case Resource.Wheat:
                    if (built)
                        Windmill(cx, cz);
                    if (guarded)
                        Haystacks(heap.x, heap.y, 0.16f);
                    else
                        Haystacks(cx, cz, built ? 0.6f : 0.42f);
                    break;
                case Resource.Stone: Boulders(heap, built, false, guarded); break;
                case Resource.Gold: Boulders(heap, built, true, guarded); break;
                case Resource.Wood: Logs(heap.x, heap.y, built || guarded); break;
                case Resource.Sheep:
                    Sheep(heap.x, heap.y, built ? 5 : 3);
                    if (built)
                        Fence(cx, cz);
                    break;
            }
        }

        /// <summary>Monsters guarding a resource stand in front of its heap (ResourceProps), not in it.</summary>
        static bool Guarded(Cell cell) => cell.Monster != Monster.None && cell.Place == Core.Place.None && cell.City < 0
            && cell.Resource != Resource.None && cell.Building == FieldBuilding.None;

        /// <summary>
        /// Where a cell's monster group stands from the cell's middle (x, z): in front of a place,
        /// whose props take the middle; at the free corner of a guarded resource's heap; in front
        /// of a mountain's peaks (<see cref="Peaks"/>), between its knolls, rather than inside the
        /// rock; else in the middle. Its label follows it.
        /// </summary>
        internal static Vector2 GroupOffset(Cell cell)
        {
            if (cell.Place != Core.Place.None)
                return new Vector2(0f, -0.22f);
            if (Guarded(cell))
            {
                float corner = FreeCorner(cell.Roads);
                return new Vector2(-Mathf.Cos(corner) * 0.22f, -Mathf.Sin(corner) * 0.22f);
            }
            // A band of a lair standing on a town's field (raids): beside the field's building, not in it.
            if (cell.Band && cell.Building != FieldBuilding.None)
            {
                float corner = FreeCorner(cell.Roads);
                return new Vector2(-Mathf.Cos(corner) * 0.27f, -Mathf.Sin(corner) * 0.27f);
            }
            return cell.Biome == Biome.Mountains ? new Vector2(0f, -0.26f) : Vector2.zero;
        }

        /// <summary>
        /// The direction (radians) of the hexagon's corner farthest from its roads: the first corner,
        /// the back one (+z) first, between two edges without a road; else the back.
        /// </summary>
        static float FreeCorner(byte roads)
        {
            foreach (float degrees in QuarterAngles)
            {
                int right = Mathf.RoundToInt((degrees - 30f) / 60f), left = (right + 1) % 6;
                if ((roads & (1 << right | 1 << left)) == 0)
                    return degrees * Mathf.Deg2Rad;
            }
            return Mathf.PI / 2f;
        }

        /// <summary>The ground the cell's field building covers (<see cref="FieldModel"/>), kept clear by the windmill beside it.</summary>
        Footprint? fieldPrint;

        /// <summary>
        /// What was just built, which rises out of the ground (<see cref="Rise"/>) as its cell is
        /// dressed again: the field building on <see cref="RisingCell"/>, or <see cref="RisingBuilding"/>
        /// in the town <see cref="RisingTown"/>. Set only round the redraw after a build.
        /// </summary>
        public int RisingCell = -1, RisingTown = -1;
        public TownBuilding RisingBuilding;

        /// <summary>The studio model of a field building near the cell's centre, off the roads; where it stands.</summary>
        Vector2 FieldModel(FieldBuilding building, float cx, float cz)
        {
            Vector2 at = here.Roads != 0 ? Around(cx, cz, rng.Range(0f, Mathf.PI * 2f), 0.2f, 0.16f) : new Vector2(cx, cz + 0.04f);
            at = OffRiver(cx, cz, at.x, at.y, 0.16f);
            placed.Add(at);
            GameObject prefab = art != null ? art.ModelOf(ModelOf(building)) : null;
            if (prefab != null)
            {
                float size = 0.5f * SizeFactor(prefab), yaw = rng.Range(-25f, 25f);
                Transform model = Place(prefab, at.x, at.y, size, yaw, false);
                if (here.Index == RisingCell)
                    model.gameObject.AddComponent<Rise>();
                fieldPrint = new Footprint(at, SidesOf(prefab) * (size / 2f), yaw);
            }
            else
            {
                House(at.x, at.y, 1.3f);
                fieldPrint = new Footprint(at, new Vector2(0.12f, 0.12f), 0f);
            }
            return at;
        }

        /// <summary>A free spot in the hexagon at least <paramref name="gap"/> from what is placed.</summary>
        bool Spot(float cx, float cz, float clear, float gap, float reach, out Vector2 p)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                float x = rng.Range(-reach, reach), z = rng.Range(-reach, reach);
                if (!InsideHex(x, z, reach) || x * x + z * z < clear * clear)
                    continue;
                p = new Vector2(cx + x, cz + z);
                if (field.Height(p.x, p.y) < 0.06f || NearRoad(here, p.x, p.y, 0f) || NearRiver(p.x, p.y, 0f))
                    continue;
                bool free = true;
                foreach (Vector2 q in placed)
                {
                    if ((q - p).sqrMagnitude < gap * gap)
                    {
                        free = false;
                        break;
                    }
                }
                if (!free)
                    continue;
                placed.Add(p);
                return true;
            }
            p = default;
            return false;
        }

        static int Bits(int mask)
        {
            int n = 0;
            for (; mask != 0; mask >>= 1)
                n += mask & 1;
            return n;
        }

        /// <summary>True when a point of the cell lies on or beside one of its roads.</summary>
        bool NearRoad(Cell cell, float x, float z, float margin)
        {
            if (cell == null || cell.Roads == 0)
                return false;
            var local = new Vector2(x - world.CenterX(cell.Index), z - world.CenterZ(cell.Index));
            return RoadShape.Distance(cell.Roads, RoadShape.Hub(cell), local) < RoadClear + margin;
        }

        bool NearRiver(float x, float z, float margin) => field.RiverEdge(x, z) < RiverClear + margin;

        /// <summary>The point moved towards the cell's centre until it keeps <paramref name="margin"/> off the rivers.</summary>
        Vector2 OffRiver(float cx, float cz, float x, float z, float margin)
        {
            var centre = new Vector2(cx, cz);
            var p = new Vector2(x, z);
            for (int k = 0; k < 12 && NearRiver(p.x, p.y, margin) && (p - centre).magnitude > 0.15f; k++)
                p = Vector2.MoveTowards(p, centre, 0.04f);
            return p;
        }

        /// <summary>The way across edge d: at 60·d degrees from +x, as in Hex.Directions.</summary>
        static Vector2 Direction(int d) => new Vector2(Mathf.Cos(d * Mathf.PI / 3f), Mathf.Sin(d * Mathf.PI / 3f));

        /// <summary>
        /// A point at <paramref name="distance"/> from the centre, at the angle or the nearest one
        /// turned from it that keeps off the roads (the angle itself when none does).
        /// </summary>
        Vector2 Around(float cx, float cz, float angle, float distance, float margin = 0f)
        {
            for (int k = 0; k < 14; k++)
            {
                float a = angle + (k % 2 == 0 ? 1 : -1) * ((k + 1) / 2) * 0.45f;
                float x = cx + Mathf.Cos(a) * distance, z = cz + Mathf.Sin(a) * distance;
                if (!NearRoad(here, x, z, margin) && !NearRiver(x, z, margin))
                    return new Vector2(x, z);
            }
            return new Vector2(cx + Mathf.Cos(angle) * distance, cz + Mathf.Sin(angle) * distance);
        }

        static bool InsideHex(float x, float z, float inner)
        {
            for (int d = 0; d < 6; d++)
            {
                float a = d * Mathf.PI / 3f;
                if (x * Mathf.Cos(a) + z * Mathf.Sin(a) > inner)
                    return false;
            }
            return true;
        }

        /// <summary>Trees; <paramref name="bias"/> above 0 favours pines, below 0 broadleaf.</summary>
        void Trees(float cx, float cz, int count, float clear, float gap, float bias = 0f)
        {
            bool shore = ByWater();
            for (int t = 0; t < count; t++)
            {
                if (!Spot(cx, cz, clear, gap, 0.86f, out Vector2 p))
                    break;
                float pine = pineness.Fbm(p.x * 0.09f, p.y * 0.09f, 2) * 3f + bias + rng.Range(-0.6f, 0.6f);
                float s = rng.Range(0.8f, 1.2f);
                Vector3 at = Ground(p.x, p.y);
                if (StudioTree(TreeKind(p, pine > 0f, shore || NearRiver(p.x, p.y, 0.2f)), at, s))
                    continue;
                if (pine > 0f)
                    Pine(at, s);
                else
                    Broadleaf(at, s);
            }
        }

        /// <summary>The studio tree for a spot (see <see cref="WoodTrees"/>), or null without the art.</summary>
        string TreeKind(Vector2 p, bool pine, bool wet)
        {
            if (art == null)
                return null;
            string kind = pine ? Any(Conifers) : null;
            if (kind == null && wet && rng.Chance(0.5f))
                kind = Any(WaterTrees);
            if (kind == null && here.Biome == Biome.Forest && rng.Chance(0.07f))
                kind = Any(DeadTrees);
            if (kind == null && rng.Chance(0.03f))
                kind = Any(AutumnTrees);
            if (kind == null)
                kind = Any(here.Biome == Biome.Forest ? WoodTrees : here.Biome == Biome.Hills ? HillTrees : OpenTrees);
            return kind ?? Any(WoodTrees) ?? Any(OldTrees);
        }

        /// <summary>One of the kinds the art has, at random, or null when it has none.</summary>
        string Any(string[] kinds)
        {
            int count = 0;
            foreach (string kind in kinds)
                if (Scatter(kind) != null)
                    count++;
            if (count == 0)
                return null;
            int pick = rng.Range(0, count);
            foreach (string kind in kinds)
                if (Scatter(kind) != null && pick-- == 0)
                    return kind;
            return null;
        }

        /// <summary>Whether the cell being dressed borders a lake or the sea.</summary>
        bool ByWater()
        {
            for (int d = 0; d < 6; d++)
            {
                int n = world.Neighbor(here.Index, d);
                if (n >= 0 && world.Cells[n].IsWater)
                    return true;
            }
            return false;
        }

        /// <summary>How far a tree's top moves in the wind, as a share of its height; grass blades and reeds bend further.</summary>
        const float TreeSway = 0.07f, BushSway = 0.12f, GrassSway = 0.3f;

        /// <summary>
        /// A studio tree about as tall and wide as the low-poly ones (its kind a little taller or
        /// shorter, see <see cref="TreeHeights"/>), turned any way, in its own shade: a little
        /// lighter or darker, some towards a warmer olive. Its crown sways in the wind; a dead
        /// tree's bare boughs barely move.
        /// </summary>
        bool StudioTree(string name, Vector3 at, float s)
        {
            ModelBatch.Source model = name != null ? Scatter(name) : null;
            if (model == null)
                return false;
            Vector3 size = model.Bounds.size;
            float height = TreeHeight(name) * s;
            float k = Mathf.Min(height / Mathf.Max(size.y, 1e-3f), 0.42f * s / Mathf.Max(Mathf.Max(size.x, size.z), 1e-3f));
            Color tint = Color.Lerp(Color.white, new Color(1.05f, 1f, 0.84f), rng.Range(0f, 1f) * rng.Range(0f, 1f))
                * rng.Range(0.88f, 1.05f);
            float sway = System.Array.IndexOf(DeadTrees, name) >= 0 ? TreeSway * 0.4f : TreeSway;
            Batch().Add(model, at - new Vector3(0, 0.02f, 0), rng.Range(0f, 360f), Vector3.one * k, tint, sway);
            // The studio trees' trunks are about a twentieth of their height across at the foot.
            float tall = size.y * k;
            contact?.Trunk(new Vector2(at.x, at.z), 0.045f * tall, tall);
            return true;
        }

        /// <summary>
        /// A studio stone (or another prop that lies on the ground: a stump, a heap, a haystack)
        /// <paramref name="width"/> across at the ground, sunk a little, turned any way;
        /// <paramref name="height"/> stretches it (the model's own proportions when 0).
        /// </summary>
        bool StudioStone(string name, float x, float z, float width, float height = 0f, float sink = 0.015f)
        {
            ModelBatch.Source model = Scatter(name);
            if (model == null)
                return false;
            Vector3 size = model.Bounds.size;
            float k = width / Mathf.Max(Mathf.Max(size.x, size.z), 1e-3f);
            float ky = height > 0f ? height / Mathf.Max(size.y, 1e-3f) : k;
            Batch().Add(model, Ground(x, z) - new Vector3(0, sink, 0), rng.Range(0f, 360f), new Vector3(k, ky, k),
                Color.white * rng.Range(0.9f, 1.04f));
            return true;
        }

        /// <summary>How deep the towns' and roads' small studio things stand in the ground.</summary>
        const float DecorSink = 0.008f;

        /// <summary>
        /// A small studio thing standing upright at <paramref name="p"/> (a lamp, a signpost, a
        /// stall, casks): <paramref name="size"/> tall, or that long along its long side when
        /// <paramref name="wide"/>, turned by <paramref name="yaw"/>, where 0 shows it to the camera
        /// as its picture drew it; false when the art lacks it.
        /// </summary>
        bool StudioThing(string name, Vector2 p, float size, float yaw, bool wide = false)
        {
            ModelBatch.Source model = Scatter(name);
            if (model == null)
                return false;
            float k = size / Mathf.Max(wide ? LayOf(model).Length : model.Bounds.size.y, 1e-3f);
            Batch().Add(model, Ground(p.x, p.y) - new Vector3(0, DecorSink, 0), yaw, Vector3.one * k,
                Color.white * rng.Range(0.95f, 1.03f));
            return true;
        }

        /// <summary>
        /// A small studio thing <paramref name="length"/> long lying along <paramref name="degrees"/>
        /// (from x towards z, as the roads are counted) with the middle of its long side at
        /// <paramref name="p"/>, its front either way: a cart along its road. False when the art
        /// lacks it.
        /// </summary>
        bool StudioAlong(string name, Vector2 p, float degrees, float length)
        {
            ModelBatch.Source model = Scatter(name);
            if (model == null)
                return false;
            Lay lay = LayOf(model);
            float k = length / Mathf.Max(lay.Length, 1e-3f);
            // A yaw turns the model's lines by minus itself (see Footprint): its long side, at
            // lay.Angle, comes to lie along degrees.
            float yaw = lay.Angle - degrees + (rng.Chance(0.5f) ? 180f : 0f);
            Matrix4x4 place = Matrix4x4.TRS(Ground(p.x, p.y) - new Vector3(0, DecorSink, 0), Quaternion.Euler(0, yaw, 0), Vector3.one * k)
                * Matrix4x4.Translate(new Vector3(-lay.Middle.x, 0f, -lay.Middle.y));
            Batch().Add(model, place, Color.white * rng.Range(0.95f, 1.03f));
            return true;
        }

        /// <summary>
        /// How a scatter model lies on the ground: the narrowest box round its plan, the turn of
        /// the box's long side from x towards z in degrees, its length and width, and its middle.
        /// The image-to-3D chain builds a thing turned the way its three-quarter picture shows it,
        /// so a fence span or a cart does not lie along x in its model.
        /// </summary>
        readonly struct Lay
        {
            public readonly float Angle, Length, Width;
            public readonly Vector2 Middle;

            public Lay(float angle, float length, float width, Vector2 middle)
            {
                Angle = angle;
                Length = length;
                Width = width;
                Middle = middle;
            }
        }

        readonly Dictionary<ModelBatch.Source, Lay> lays = new Dictionary<ModelBatch.Source, Lay>();

        /// <summary>A scatter model's <see cref="Lay"/>, its box turned in steps of 2 degrees, read once.</summary>
        Lay LayOf(ModelBatch.Source model)
        {
            if (lays.TryGetValue(model, out Lay lay))
                return lay;
            float best = 0f, narrowest = float.MaxValue;
            for (int step = 0; step < 90; step++)
            {
                float a = step * 2f * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                float low = float.MaxValue, high = float.MinValue;
                foreach (Vector3 v in model.Positions)
                {
                    float across = v.z * cos - v.x * sin;
                    low = Mathf.Min(low, across);
                    high = Mathf.Max(high, across);
                }
                if (high - low < narrowest)
                {
                    narrowest = high - low;
                    best = step * 2f;
                }
            }
            float r = best * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            float alongLow = float.MaxValue, alongHigh = float.MinValue, acrossLow = float.MaxValue, acrossHigh = float.MinValue;
            foreach (Vector3 v in model.Positions)
            {
                float along = v.x * c + v.z * s, across = v.z * c - v.x * s;
                alongLow = Mathf.Min(alongLow, along);
                alongHigh = Mathf.Max(alongHigh, along);
                acrossLow = Mathf.Min(acrossLow, across);
                acrossHigh = Mathf.Max(acrossHigh, across);
            }
            float mAlong = (alongLow + alongHigh) * 0.5f, mAcross = (acrossLow + acrossHigh) * 0.5f;
            var middle = new Vector2(mAlong * c - mAcross * s, mAlong * s + mAcross * c);
            lay = new Lay(best, alongHigh - alongLow, acrossHigh - acrossLow, middle);
            lays[model] = lay;
            return lay;
        }

        /// <summary>
        /// Each tree's own shade of its greens: a little lighter or darker, some towards olive, so
        /// a wood reads as many trees rather than one colour.
        /// </summary>
        Color Tone(Color green)
        {
            Color c = Color.Lerp(green, Palette.LeavesOlive, rng.Range(0f, 0.45f) * rng.Range(0f, 1f));
            c *= rng.Range(0.88f, 1.06f);
            c.a = 1f;
            return c;
        }

        /// <summary>A pine: three cones on a short trunk, swaying in the wind from the ground up.</summary>
        void Pine(Vector3 at, float s)
        {
            MeshKit kit = Kit();
            kit.Sway(at.y - 0.02f, TreeSway, MeshKit.PhaseAt(at));
            kit.Lathe(at - new Vector3(0, 0.02f, 0), new[] { 0f, 0.11f * s }, new[] { 0.028f * s, 0.02f * s },
                new[] { Palette.Trunk }, 5, rng);
            Color low = Tone(Palette.Pine), high = Tone(Palette.PineLight);
            int tiers = 3;
            for (int k = 0; k < tiers; k++)
            {
                float y = (0.07f + 0.085f * k) * s, r = (0.17f - 0.04f * k) * s, h = (0.21f - 0.02f * k) * s;
                Color green = Color.Lerp(low, high, k / (float)(tiers - 1));
                kit.Lathe(at + new Vector3(0, y, 0), new[] { 0f, h }, new[] { r, 0f }, new[] { green }, 7, rng, 0.08f, 0.1f);
            }
            kit.Still();
        }

        /// <summary>
        /// A broadleaf tree: a crown of a big ball and a smaller one leaning out of it, swaying in
        /// the wind from the ground up.
        /// </summary>
        void Broadleaf(Vector3 at, float s)
        {
            MeshKit kit = Kit();
            kit.Sway(at.y - 0.02f, TreeSway, MeshKit.PhaseAt(at));
            kit.Lathe(at - new Vector3(0, 0.02f, 0), new[] { 0f, 0.16f * s }, new[] { 0.03f * s, 0.024f * s },
                new[] { Palette.Trunk }, 5, rng);
            Color dark = Tone(Palette.Leaves) * 0.84f, light = Tone(Palette.LeavesLight);
            float lean = rng.Range(-0.02f, 0.02f);
            kit.Blob(at + new Vector3(lean, 0.27f * s, -lean), new Vector3(0.16f, 0.14f, 0.16f) * s, 7, 4,
                dark, light, rng, 0.12f);
            float side = rng.Range(0f, Mathf.PI * 2f);
            Vector3 offset = new Vector3(Mathf.Cos(side), 0f, Mathf.Sin(side)) * (0.1f * s);
            kit.Blob(at + offset + new Vector3(0, 0.24f * s, 0), new Vector3(0.1f, 0.09f, 0.1f) * s, 6, 3,
                dark, light, rng, 0.12f);
            kit.Still();
        }

        /// <summary>
        /// A mountain cell's peaks round a pass: the high peak and one or two lower ones stand at
        /// the back and the sides, two low knolls by the front corners, so a hero or a monster group
        /// in the middle stays in sight of the camera (which looks from the south). Every mountain
        /// cell is laid out alike, so the peaks stay put when a group there is beaten.
        /// </summary>
        void Peaks(float cx, float cz)
        {
            int sides = rng.Range(1, 3);
            bool eastFirst = rng.Chance(0.5f);
            for (int k = 0; k < 3 + sides; k++)
            {
                // 0 the high peak behind, 1 and 2 the knolls in front, then the side peaks.
                float angle, offset, r, h;
                if (k == 0)
                {
                    angle = Mathf.PI * 0.5f + rng.Range(-0.45f, 0.45f);
                    offset = rng.Range(0.52f, 0.62f);
                    r = rng.Range(0.5f, 0.6f);
                    h = rng.Range(0.85f, 1.1f);
                }
                else if (k < 3)
                {
                    angle = (k == 1 ? 1.3f : 1.7f) * Mathf.PI + rng.Range(-0.12f, 0.12f);
                    offset = rng.Range(0.66f, 0.74f);
                    r = rng.Range(0.18f, 0.24f);
                    h = rng.Range(0.2f, 0.3f);
                }
                else
                {
                    bool east = (k == 3) == eastFirst;
                    angle = (east ? 0.12f : 0.88f) * Mathf.PI + rng.Range(-0.2f, 0.2f);
                    offset = rng.Range(0.56f, 0.66f);
                    r = rng.Range(0.32f, 0.42f);
                    h = rng.Range(0.45f, 0.65f);
                }
                // Rivers run between the mountains: turn a peak away from them along its side of
                // the cell, then make it narrower (and lower with it) where the water still comes close.
                float x = cx + Mathf.Cos(angle) * offset, z = cz + Mathf.Sin(angle) * offset;
                float room = field.RiverEdge(x, z) - 0.03f;
                for (int turn = 1; turn < 4 && room < r; turn++)
                {
                    float a = angle + (turn % 2 == 1 ? 1 : -1) * ((turn + 1) / 2) * 0.35f;
                    float tx = cx + Mathf.Cos(a) * offset, tz = cz + Mathf.Sin(a) * offset;
                    float tr = field.RiverEdge(tx, tz) - 0.03f;
                    if (tr > room)
                    {
                        room = tr;
                        x = tx;
                        z = tz;
                    }
                }
                if (room < r)
                {
                    if (k > 0 && room < 0.12f)
                        continue;
                    float shrink = Mathf.Max(room, 0.3f) / r;
                    r *= shrink;
                    h *= Mathf.Lerp(1f, shrink, 0.6f);
                }
                // A studio rock: the high peaks with snow, a spire or a crag at the sides, low
                // outcrops in front (a low crag while there is no outcrop); sunk the deeper the
                // higher it stands.
                bool snow = h > 0.6f;
                string rock = snow ? "peak_snow" : k == 0 ? "peak" : k < 3 ? "outcrop" : rng.Chance(0.5f) ? "crag" : "peak";
                if (StudioStone(rock, x, z, 2f * r, h, 0.04f + 0.08f * h)
                    || rock == "outcrop" && StudioStone("crag", x, z, 2f * r, h, 0.04f + 0.08f * h))
                    continue;
                // Without the art, a craggy cone of rock, narrowing faster towards the top, its
                // rings jittered so the faces break up; the shader lays the rock texture over it
                // and snow on the high ones.
                Vector3 at = Ground(x, z) - new Vector3(0, 0.12f, 0);
                Kit().Lathe(at, new[] { 0f, h * 0.28f, h * 0.55f, h * 0.8f, h }, new[] { r, r * 0.8f, r * 0.52f, r * 0.24f, 0f },
                    new[] { Palette.RockDark, Palette.Rock, Palette.Rock, Palette.Rock }, rng.Range(6, 9), rng, 0.16f, 0.1f,
                    stone: true, snowFrom: snow ? 0.5f : 2f);
            }
        }

        void Rocks(float cx, float cz, int count)
        {
            for (int k = 0; k < count; k++)
            {
                if (!Spot(cx, cz, 0.2f, 0.2f, 0.7f, out Vector2 p))
                    break;
                float s = rng.Range(0.06f, 0.1f);
                // Moss grows on the stones of the woods and hills, not on the beach.
                string kind = Any(here.Biome == Biome.Beach ? BareRockModels : RockModels);
                if (kind != null && StudioStone(kind, p.x, p.y, (kind == "outcrop" ? 2.6f : 2.2f) * s))
                    continue;
                Kit().Blob(Ground(p.x, p.y) + new Vector3(0, s * 0.3f, 0), new Vector3(s, s * 0.7f, s), 6, 3,
                    Palette.RockDark, Palette.Boulder, rng, 0.18f, stone: true);
            }
        }

        void Haystacks(float cx, float cz, float distance)
        {
            int count = rng.Range(2, 4);
            for (int k = 0; k < count; k++)
            {
                Vector2 at = Around(cx, cz, k * 2.1f + rng.Range(0f, 0.8f), distance, 0.04f);
                if (!Clear(at, 0.16f))
                    continue;
                float x = at.x, z = at.y;
                placed.Add(at);
                float s = rng.Range(0.9f, 1.15f);
                if (StudioStone("haystack", x, z, 0.19f * s, 0f, 0.012f))
                    continue;
                Kit().Lathe(Ground(x, z) - new Vector3(0, 0.01f, 0),
                    new[] { 0f, 0.07f * s, 0.13f * s, 0.17f * s }, new[] { 0.085f * s, 0.09f * s, 0.06f * s, 0f },
                    new[] { Palette.HayDark, Palette.Hay, Palette.Hay }, 8, rng, 0.04f, 0.05f);
            }
        }

        /// <summary>
        /// A heap of boulders (with gold nuggets), a small one beside a building; with the art,
        /// quarried blocks or boulders veined with gold and a boulder or two beside them. A
        /// <paramref name="settled"/> heap stays where it is put, close together.
        /// </summary>
        void Boulders(Vector2 centre, bool small, bool gold, bool settled = false)
        {
            float cx = centre.x, cz = centre.y;
            bool tight = small || settled || here.Roads != 0;
            Vector2 heap = !small && !settled && here.Roads != 0 ? Around(cx, cz, rng.Range(0f, Mathf.PI * 2f), 0.4f, 0.12f) : centre;
            bool studio = StudioStone(gold ? "goldore" : "stoneblocks", heap.x, heap.y, small ? 0.24f : 0.32f, 0f, 0.015f);
            int count = studio ? (small ? 0 : rng.Range(1, 3)) : small ? rng.Range(2, 4) : rng.Range(3, 6);
            for (int k = 0; k < count; k++)
            {
                float angle = rng.Range(0f, Mathf.PI * 2f), d = studio ? rng.Range(0.22f, 0.3f) : rng.Range(0.05f, tight ? 0.16f : 0.3f);
                float x = heap.x + Mathf.Cos(angle) * d, z = heap.y + Mathf.Sin(angle) * d;
                float s = rng.Range(0.09f, 0.16f);
                if (StudioStone("boulder", x, z, 2f * s, 0f, 0.02f))
                    continue;
                Kit().Blob(Ground(x, z) + new Vector3(0, s * 0.35f, 0), new Vector3(s, s * 0.8f, s), 6, 3,
                    Palette.RockDark, Palette.Boulder, rng, 0.2f, stone: true);
            }
            if (!gold || studio)
                return;
            int nuggets = small ? rng.Range(3, 5) : rng.Range(5, 8);
            for (int k = 0; k < nuggets; k++)
            {
                float angle = rng.Range(0f, Mathf.PI * 2f), d = rng.Range(0.12f, tight ? 0.24f : 0.38f);
                float x = heap.x + Mathf.Cos(angle) * d, z = heap.y + Mathf.Sin(angle) * d;
                if (NearRoad(here, x, z, -0.04f) || NearRiver(x, z, -0.02f))
                    continue;
                float s = rng.Range(0.035f, 0.06f);
                Kit().Blob(Ground(x, z) + new Vector3(0, s * 0.4f, 0), new Vector3(s, s * 0.8f, s), 5, 2,
                    Palette.HayDark, Palette.Gold, rng, 0.25f);
            }
        }

        void Logs(float cx, float cz, bool placedAlready)
        {
            if (here.Roads != 0 && !placedAlready)
            {
                Vector2 free = Around(cx, cz, rng.Range(0f, Mathf.PI * 2f), 0.4f, 0.1f);
                cx = free.x;
                cz = free.y;
            }
            placed.Add(new Vector2(cx, cz));
            if (StudioStone("logpile", cx, cz, 0.36f, 0f, 0.01f))
                return;
            Vector3 at = Ground(cx, cz);
            float yaw = rng.Range(-30f, 30f);
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            float r = 0.045f, half = 0.2f;
            Vector3[] rows = { new Vector3(-0.09f, r, 0), new Vector3(0, r, 0), new Vector3(0.09f, r, 0),
                new Vector3(-0.045f, r * 2.7f, 0), new Vector3(0.045f, r * 2.7f, 0), new Vector3(0, r * 4.4f, 0) };
            MeshKit kit = Kit();
            foreach (Vector3 row in rows)
            {
                Vector3 c = at + q * new Vector3(row.x, row.y, 0);
                Vector3 axis = q * new Vector3(0, 0, half + rng.Range(-0.03f, 0.03f));
                kit.Cylinder(c - axis, c + axis, r, 7, Palette.Trunk, Palette.Hay);
            }
        }

        void Sheep(float cx, float cz, int count)
        {
            GameObject prefab = art != null ? art.ModelOf("sheep") : null;
            for (int k = 0; k < count; k++)
            {
                Vector2 at = Around(cx, cz, k * Mathf.PI * 2f / count + rng.Range(0f, 0.9f), rng.Range(0.15f, 0.34f), 0.03f);
                float x = at.x, z = at.y;
                placed.Add(at);
                if (prefab != null)
                    // About a third of a house: at 0.2 they stood almost as big as one.
                    Place(prefab, x, z, 0.13f, rng.Range(-60f, 60f), true);
                else
                    Kit().Blob(Ground(x, z) + new Vector3(0, 0.07f, 0), new Vector3(0.08f, 0.06f, 0.1f), 7, 3,
                        Palette.Snow * 0.85f, Palette.Snow, rng, 0.1f);
            }
        }

        /// <summary>
        /// Angles of a town centre's places, in the order they fill: behind the middle first, the
        /// sides, then the front corners. The front (270°) is the square, where a hero stands.
        /// </summary>
        static readonly float[] CentreAngles = { 90f, 30f, 150f, 330f, 210f };

        /// <summary>Angles of a quarter's places round its square, in the order they fill.</summary>
        static readonly float[] QuarterAngles = { 90f, 30f, 150f, 330f, 210f, 270f };

        /// <summary>
        /// A town's centre: a castle, the capital's town hall, a fountain (from level 3) or a well in
        /// the middle, its places towards the hexagon's corners (between the roads) with a house,
        /// a building or a garden each (see <see cref="Towns.Layout"/>), the square in front with
        /// the banner and, until there is a market, a stall, and what a town always has between
        /// the places (<see cref="TownCore"/>). Walls run round it all.
        /// </summary>
        void City(City city, float cx, float cz)
        {
            bool capital = city.Capital;
            bool castle = city.Buildings.Contains(TownBuilding.Castle), walls = city.Buildings.Contains(TownBuilding.Walls);
            // Walls stand a little inside the hexagon, so the places draw in to keep off them.
            float ring = (capital || castle ? 0.64f : 0.54f) * (walls ? 0.94f : 1f);
            Color colour = Palette.Countries[city.Country % Palette.Countries.Length];
            // A castle or the capital's hall fills much of the centre: the places fit round it, and
            // inside the walls when the town has them, rather than stand on the ring regardless.
            List<Footprint> crowd = null;
            if (castle)
                crowd = new List<Footprint> { Castle(city, cx, cz, colour) };
            else if (capital)
            {
                const float HallSize = 0.72f;
                GameObject hall = art != null ? art.ModelOf("town_hall") : null;
                if (hall != null)
                {
                    float yaw = rng.Range(-12f, 12f);
                    Place(hall, cx, cz + 0.05f, HallSize, yaw, false);
                    crowd = new List<Footprint> { new Footprint(new Vector2(cx, cz + 0.05f), SidesOf(hall) * (HallSize / 2f), yaw) };
                }
                else
                    House(cx, cz + 0.05f, 1.7f);
            }
            else if (Towns.Level(city) >= 3)
                Fountain(cx, cz);
            else
                Well(cx, cz);
            if (crowd != null && walls)
                Walls(city, cx, cz, colour, crowd);
            Lots(city, city.Cell, cx, cz, ring, CentreAngles, crowd);
            float front = castle || capital ? 0.52f : 0.46f;
            // A hero stands at the front of the square (HeroView.Stand), or on a road when the
            // water comes up to the front; nothing else stands there. The banner goes to one side
            // (its cloth flies east, 0.27 long, so it keeps east of a hero when it can) and the
            // stall to the other, each off the roads.
            Vector2 stand = HeroView.Stand(world, field, city.Cell), hero = new Vector2(cx, cz) + stand;
            placed.Add(hero);
            placed.Add(hero + new Vector2(0.15f, 0f));
            placed.Add(hero - new Vector2(0.15f, 0f));
            placed.Add(hero + new Vector2(0f, 0.12f));
            float away = HeroView.BannerSide(stand);
            Vector2? Spot(float gap, params Vector2[] offsets)
            {
                foreach (Vector2 o in offsets)
                {
                    Vector2 p = new Vector2(cx, cz) + o;
                    if (!NearRoad(here, p.x, p.y, 0f) && Clear(p, gap) && field.Height(p.x, p.y) > 0.04f && !NearRiver(p.x, p.y, 0f))
                        return p;
                }
                return null;
            }
            // Beside the square rather than before the hall's door, and no taller than the roofs:
            // a long dark pole in front of the castle hid its facade behind the flag. (A village's
            // square is narrower: its front corners' houses stand nearer.)
            float side = capital || castle ? 0.3f : 0.24f, back = capital || castle ? 0.03f : 0f;
            Vector2? banner = Spot(0.12f, new Vector2(away * side, back - front), new Vector2(-away * (side + 0.04f), back - front),
                new Vector2(away * (side + 0.06f), back - front + 0.14f), new Vector2(-away * (side + 0.06f), back - front + 0.14f));
            Vector2 pole = banner ?? new Vector2(cx + away * side, cz - front + back);
            Banner(pole.x, pole.y, city.Country, capital ? 0.62f : 0.5f);
            placed.Add(pole);
            if (!capital && !city.Buildings.Contains(TownBuilding.Market))
            {
                float other = pole.x > cx ? -1f : 1f;
                Vector2? stall = Spot(0.14f, new Vector2(other * 0.2f, 0.1f - front), new Vector2(other * 0.12f, 0.16f - front),
                    new Vector2(-other * 0.12f, 0.16f - front));
                if (stall.HasValue)
                {
                    Stall(stall.Value.x, stall.Value.y, colour);
                    placed.Add(stall.Value);
                }
            }
            Pier(city, cx, cz);
            TownCore(city, cx, cz, true, ring);
            if (walls)
                Walls(city, cx, cz, colour);
        }

        /// <summary>A quarter of a town: six places round a trodden square where its roads meet.</summary>
        void Quarter(City city, int cell, float cx, float cz)
        {
            const float Ring = 0.56f;
            // A hero stands on the square (HeroView.Stand): the lamps and carts keep off.
            Vector2 stand = new Vector2(cx, cz) + HeroView.Stand(world, field, cell);
            placed.Add(stand + new Vector2(0.12f, 0f));
            placed.Add(stand - new Vector2(0.12f, 0f));
            Lots(city, cell, cx, cz, Ring, QuarterAngles);
            TownCore(city, cx, cz, false, Ring);
        }

        /// <summary>
        /// The houses and buildings on the town's places that lie on one of its cells. Round a
        /// castle or a hall they keep clear of it and of the walls (<paramref name="crowd"/>; see <see cref="Fit"/>).
        /// </summary>
        void Lots(City city, int cell, float cx, float cz, float ring, float[] angles, List<Footprint> crowd = null)
        {
            int[] lots = Towns.Layout(city);
            int first = Towns.FirstPlace(city, cell);
            if (first < 0)
                return;
            // Each house keeps its cottage wherever it stands: they are counted over the whole town.
            int house = 0;
            for (int p = 0; p < first && p < lots.Length; p++)
            {
                if (lots[p] == Towns.HouseLot)
                    house++;
            }
            for (int k = 0; k < angles.Length && first + k < lots.Length; k++)
            {
                int lot = lots[first + k];
                float degrees = angles[k] + rng.Range(-4f, 4f);
                Vector2 at = LotAt(cx, cz, degrees, ring);
                if (lot == Towns.FreeLot)
                {
                    if (crowd != null)
                    {
                        float size = 1f, yaw = 90f - angles[k];
                        at = Fit(crowd, new Vector2(cx, cz), degrees, GardenSides, at, ref size, ref yaw, false);
                    }
                    placed.Add(at);
                    Garden(at, angles[k]);
                    continue;
                }
                bool home = lot == Towns.HouseLot;
                GameObject prefab = home ? HouseModel(city, house++) : art != null ? art.ModelOf(ModelOf((TownBuilding)lot)) : null;
                if (prefab == null)
                {
                    placed.Add(at);
                    House(at.x, at.y, home ? 1f : 1.15f);
                    continue;
                }
                float scale = home ? 0.4f : 0.43f * SizeFactor(prefab), turn = home ? rng.Range(-25f, 25f) : rng.Range(-20f, 20f);
                if (crowd != null)
                    at = Fit(crowd, new Vector2(cx, cz), degrees, SidesOf(prefab), at, ref scale, ref turn, true);
                placed.Add(at);
                Transform model = Place(prefab, at.x, at.y, scale, turn, false);
                if (home)
                    Chimneys(prefab.name, model);
                else if (city.Index == RisingTown && (TownBuilding)lot == RisingBuilding)
                    model.gameObject.AddComponent<Rise>();
            }
        }

        /// <summary>A place's spot: at the angle and distance, drawn in towards the middle off the water and the rivers.</summary>
        Vector2 LotAt(float cx, float cz, float degrees, float ring)
        {
            float a = degrees * Mathf.Deg2Rad;
            float r = ring;
            for (int k = 0; k < 6 && r > 0.3f && field.Height(cx + Mathf.Cos(a) * r, cz + Mathf.Sin(a) * r) < 0.05f; k++)
                r -= 0.05f;
            return OffRiver(cx, cz, cx + Mathf.Cos(a) * r, cz + Mathf.Sin(a) * r, 0.1f);
        }

        /// <summary>Grey stone of the walls and the castle.</summary>
        static readonly Color WallStone = Palette.Hex("#B4AC9E");

        /// <summary>The studio castles, one town's after another's.</summary>
        static readonly string[] CastleModels = { "castle_a", "castle_d", "castle_b" };

        /// <summary>
        /// The town's castle in its middle, and its footprint: a studio model, or a keep between two
        /// round towers. It faces the square nearly straight: turned further, its corner came into
        /// the houses beside it.
        /// </summary>
        Footprint Castle(City city, float cx, float cz, Color colour)
        {
            const float Size = 0.72f;
            GameObject model = null;
            for (int k = 0; art != null && model == null && k < CastleModels.Length; k++)
                model = art.ModelOf(CastleModels[(city.Index + k) % CastleModels.Length]);
            float yaw = rng.Range(-5f, 5f);
            if (model != null)
            {
                Place(model, cx, cz - 0.03f, Size, yaw, false);
                return new Footprint(new Vector2(cx, cz - 0.03f), SidesOf(model) * (Size / 2f), yaw);
            }
            Vector3 at = Ground(cx, cz) - new Vector3(0, 0.02f, 0);
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            MeshKit kit = Kit();
            Color roof = Color.Lerp(colour, Palette.Roof, 0.35f);
            var keep = new Vector3(0.26f, 0.44f, 0.24f);
            const float RoofHeight = 0.17f;
            kit.Box(at, keep, yaw, WallStone);
            Battlements(kit, at + new Vector3(0, keep.y, 0), keep.x, keep.z, yaw, WallStone);
            kit.Roof(at + new Vector3(0, keep.y, 0), new Vector3(keep.x * 0.78f, RoofHeight, keep.z * 0.7f), yaw, roof);
            Vector3 hall = at + q * new Vector3(0, 0, -0.13f);
            kit.Box(hall, new Vector3(0.4f, 0.15f, 0.1f), yaw, WallStone * 0.96f);
            Battlements(kit, hall + new Vector3(0, 0.15f, 0), 0.4f, 0.1f, yaw, WallStone * 0.96f);
            kit.Box(hall + q * new Vector3(0, 0, -0.051f), new Vector3(0.065f, 0.09f, 0.004f), yaw, Palette.Wood);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 foot = at + q * new Vector3(side * 0.21f, 0, -0.13f);
                kit.Lathe(foot, new[] { 0f, 0.3f }, new[] { 0.072f, 0.066f }, new[] { WallStone }, 9, rng);
                kit.Lathe(foot + new Vector3(0, 0.3f, 0), new[] { 0f, 0.15f }, new[] { 0.088f, 0f }, new[] { roof }, 9, rng);
            }
            Vector3 pole = at + new Vector3(0, keep.y + RoofHeight, 0);
            kit.Cylinder(pole - new Vector3(0, 0.02f, 0), pole + new Vector3(0, 0.18f, 0), 0.007f, 4, Palette.Wood, Palette.Wood);
            Vector3 a = pole + new Vector3(0, 0.175f, 0), b = pole + new Vector3(0, 0.09f, 0);
            kit.TwoSided(a, pole + new Vector3(0.13f, 0.135f, 0.01f), b, colour);
            // The hall and the towers beside it reach 0.28 to the sides, the keep 0.12 back.
            return new Footprint(new Vector2(cx, cz - 0.03f), new Vector2(0.29f, 0.16f), yaw);
        }

        /// <summary>
        /// Stone walls round the town along its hexagon, a little inside the edges: a round tower
        /// on each corner, a gatehouse (or a gate between two taller towers) where a road leaves,
        /// and no wall over water or a river: a wall stops at the bank with a tower. The studio wall, tower and gatehouse when the art has
        /// them, else boxes and round towers with roofs in the kingdom's colour. Given a
        /// <paramref name="crowd"/>, nothing is built: the walls' footprints go into it instead, so
        /// the places inside can keep clear of them before the walls go up (see <see cref="City"/>).
        /// </summary>
        void Walls(City city, float cx, float cz, Color colour, List<Footprint> crowd = null)
        {
            // The wall runs just inside the edges: a capital's places round its castle stand 0.6 of
            // the way to the corners, and a cottage there must keep clear of the wall and its towers.
            const float Scale = 0.97f, Gap = RoadClear + 0.01f, GateTower = Gap + 0.045f;
            // The studio wall stands about half as tall as a cottage, its towers twice that: next to
            // the castle a lower wall read as a garden fence.
            const float WallHeight = 0.19f, CornerHeight = 0.34f, GateHeight = 0.42f;
            // The studio gatehouse is as wide as the two gate towers stood apart; its arch lets a
            // road through.
            const float GateWidth = 0.5f;
            int roads = world.Cells[city.Cell].Roads;
            var centre = new Vector2(cx, cz);
            Vector2 Corner(int i) => centre + new Vector2(HexMetrics.CornerX[i], HexMetrics.CornerZ[i]) * Scale;
            bool Dry(Vector2 p) => field.Height(p.x, p.y) > 0.03f && field.RiverEdge(p.x, p.y) > 0.02f;
            ModelBatch.Source wall = Scatter("wall"), tower = Scatter("tower");
            bool studio = wall != null && tower != null;
            GameObject gatehouse = studio ? art.ModelOf("gate") : null;
            Color roof = Color.Lerp(colour, Palette.Roof, 0.35f), stone = crowd != null ? Color.white : Color.white * rng.Range(0.92f, 1f);

            // A stretch of wall: the studio piece repeated, each copy stretched a little to fit
            // and leaning with the ground along it, or short boxes, each on its own foot. It runs
            // only over its dry part and ends at a bank with a tower: a river along the next side
            // wets this side's end by their corner, and the whole studio piece left out there
            // opened the wall to the middle of the side (the players saw it, 2026-11-11).
            void Curtain(Vector2 from, Vector2 to)
            {
                const float Height = 0.07f, Thick = 0.032f, Sink = 0.03f;
                if (!DryPart(ref from, ref to, out bool cutFrom, out bool cutTo))
                    return;
                if (cutFrom)
                    Tower(from, false);
                if (cutTo)
                    Tower(to, false);
                float length = (to - from).magnitude;
                float yaw = -Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
                if (crowd != null)
                {
                    float thick = studio ? StudioWallThickness(wall, WallHeight) : Thick;
                    crowd.Add(new Footprint((from + to) * 0.5f, new Vector2(length, thick) * 0.5f, yaw));
                    return;
                }
                if (studio)
                {
                    StudioWall(wall, from, to, WallHeight, stone, Dry);
                    return;
                }
                MeshKit kit = Kit();
                int pieces = Mathf.CeilToInt(length / 0.05f);
                float step = length / pieces;
                for (int k = 0; k < pieces; k++)
                {
                    Vector2 p = Vector2.Lerp(from, to, (k + 0.5f) / pieces);
                    if (!Dry(p))
                        continue;
                    Color shade = WallStone * rng.Range(0.94f, 1.04f);
                    Vector3 foot = Ground(p.x, p.y) - new Vector3(0, Sink, 0);
                    kit.Box(foot, new Vector3(step + 0.004f, Height + Sink, Thick), yaw, shade);
                    kit.Box(foot + new Vector3(0, Height + Sink, 0), new Vector3(step * 0.5f, 0.018f, Thick + 0.004f), yaw, shade);
                }
            }

            // The dry part of a stretch from one end to the other, its wet ends cut a tower's
            // width back from the water; false when too little is left.
            bool DryPart(ref Vector2 from, ref Vector2 to, out bool cutFrom, out bool cutTo)
            {
                const int Steps = 48;
                const float Back = 0.05f;
                cutFrom = cutTo = false;
                int first = 0, last = Steps;
                while (first <= Steps && !Dry(Vector2.Lerp(from, to, first / (float)Steps)))
                    first++;
                if (first > Steps)
                    return false;
                while (last > first && !Dry(Vector2.Lerp(from, to, last / (float)Steps)))
                    last--;
                Vector2 dir = (to - from).normalized;
                Vector2 f = Vector2.Lerp(from, to, first / (float)Steps), t = Vector2.Lerp(from, to, last / (float)Steps);
                if (first > 0)
                {
                    f += dir * Back;
                    cutFrom = true;
                }
                if (last < Steps)
                {
                    t -= dir * Back;
                    cutTo = true;
                }
                if (Vector2.Dot(t - f, dir) < 0.08f)
                    return false;
                from = f;
                to = t;
                return true;
            }

            void Tower(Vector2 p, bool gate)
            {
                if (!Dry(p))
                    return;
                float height = gate ? GateHeight : CornerHeight, radius = gate ? 0.045f : 0.042f;
                if (crowd != null)
                {
                    // A round tower, as the square round it.
                    if (studio)
                        radius = Mathf.Max(tower.Bounds.size.x, tower.Bounds.size.z) * TowerScale(tower, height) * 0.5f;
                    crowd.Add(new Footprint(p, new Vector2(radius, radius), 0f));
                }
                else if (studio)
                    StudioTower(tower, p, height, stone);
                else
                    PlainTower(Kit(), Ground(p.x, p.y) - new Vector3(0, 0.03f, 0), gate ? 0.17f : 0.13f, radius, roof);
            }

            for (int d = 0; d < 6; d++)
            {
                // Edge d (towards neighbour d, at 60·d degrees) runs from corner d to corner d + 1,
                // or to the gate's tower and on from the other one.
                Vector2 a = Corner(d), b = Corner((d + 1) % 6);
                if ((roads >> d & 1) == 0)
                {
                    Curtain(a, b);
                    continue;
                }
                Vector2 along = (b - a).normalized, middle = (a + b) * 0.5f;
                if (gatehouse != null)
                {
                    // The curtains run a little into the gatehouse's towers.
                    Curtain(a, middle - along * (GateWidth * 0.42f));
                    Curtain(middle + along * (GateWidth * 0.42f), b);
                    if (!Dry(middle))
                        continue;
                    if (crowd != null)
                        crowd.Add(new Footprint(middle, SidesOf(gatehouse) * (GateWidth / 2f), -Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg));
                    else
                        Gatehouse(gatehouse, middle, along, GateWidth);
                    continue;
                }
                Curtain(a, middle - along * GateTower);
                Curtain(middle + along * GateTower, b);
                Tower(middle - along * GateTower, true);
                Tower(middle + along * GateTower, true);
            }
            for (int i = 0; i < 6; i++)
                Tower(Corner(i), false);
        }

        /// <summary>
        /// The studio wall from <paramref name="from"/> to <paramref name="to"/>, its front
        /// outwards (the town's corners run counter-clockwise): as many copies as fit at the
        /// model's own proportions and <paramref name="height"/> (a little thinner), each
        /// stretched a little to fill its share, sheared to follow the ground along it and sunk
        /// where the ground dips under its middle. A copy over water or a river is left out.
        /// </summary>
        void StudioWall(ModelBatch.Source wall, Vector2 from, Vector2 to, float height, Color tint, System.Func<Vector2, bool> dry)
        {
            Vector3 size = wall.Bounds.size;
            float k = (height + WallSink) / Mathf.Max(size.y, 1e-3f);
            float length = (to - from).magnitude;
            int pieces = Mathf.Max(1, Mathf.RoundToInt(length / (size.x * k)));
            Vector2 dir = (to - from) / length;
            Quaternion turn = Quaternion.Euler(0, -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, 0);
            var scale = new Vector3(length / pieces / Mathf.Max(size.x, 1e-3f), k, k * WallThinner);
            for (int i = 0; i < pieces; i++)
            {
                Vector2 a = Vector2.Lerp(from, to, (float)i / pieces), b = Vector2.Lerp(from, to, (i + 1f) / pieces), m = (a + b) * 0.5f;
                if (!dry(a) || !dry(m) || !dry(b))
                    continue;
                float ha = field.Height(a.x, a.y), hb = field.Height(b.x, b.y), line = (ha + hb) * 0.5f;
                float slope = Mathf.Clamp((hb - ha) / (length / pieces), -0.5f, 0.5f);
                float sag = Mathf.Max(0f, line - field.Height(m.x, m.y));
                Matrix4x4 shear = Matrix4x4.identity;
                shear.m10 = slope * dir.x;
                shear.m12 = slope * dir.y;
                Matrix4x4 place = Matrix4x4.Translate(new Vector3(m.x, line - WallSink - sag, m.y)) * shear * Matrix4x4.TRS(Vector3.zero, turn, scale);
                Batch().Add(wall, place, tint * rng.Range(0.97f, 1.02f));
                contact?.Wall(a, b, size.z * scale.z, height);
            }
        }

        /// <summary>
        /// The studio gatehouse across a road at <paramref name="middle"/> of a wall,
        /// <paramref name="width"/> wide along the wall, its front outwards like the wall's, on
        /// the lowest ground under its towers.
        /// </summary>
        void Gatehouse(GameObject prefab, Vector2 middle, Vector2 along, float width)
        {
            const float Sink = 0.02f;
            float low = field.Height(middle.x, middle.y);
            foreach (float side in new[] { -0.4f, 0.4f })
            {
                Vector2 p = middle + along * (side * width);
                low = Mathf.Min(low, field.Height(p.x, p.y));
            }
            Place(prefab, middle.x, middle.y, width, -Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg, false, low - Sink);
        }

        /// <summary>How deep <see cref="StudioWall"/> sinks the wall, and how much thinner than the model's own proportions it stands.</summary>
        const float WallSink = 0.015f, WallThinner = 0.75f;

        /// <summary>How thick <see cref="StudioWall"/> stands the studio wall <paramref name="height"/> tall.</summary>
        static float StudioWallThickness(ModelBatch.Source wall, float height) =>
            wall.Bounds.size.z * (height + WallSink) / Mathf.Max(wall.Bounds.size.y, 1e-3f) * WallThinner;

        /// <summary>How deep <see cref="StudioTower"/> sinks a tower.</summary>
        const float TowerSink = 0.02f;

        /// <summary>The scale at which <see cref="StudioTower"/> stands the studio tower <paramref name="height"/> tall.</summary>
        static float TowerScale(ModelBatch.Source tower, float height) => (height + TowerSink) / Mathf.Max(tower.Bounds.size.y, 1e-3f);

        /// <summary>
        /// The studio tower <paramref name="height"/> tall over the ground, standing on the lowest
        /// ground under it, turned any way.
        /// </summary>
        void StudioTower(ModelBatch.Source tower, Vector2 p, float height, Color tint)
        {
            Vector3 size = tower.Bounds.size;
            float k = TowerScale(tower, height), r = Mathf.Max(size.x, size.z) * k * 0.45f;
            float low = field.Height(p.x, p.y);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                low = Mathf.Min(low, field.Height(p.x + Mathf.Cos(angle) * r, p.y + Mathf.Sin(angle) * r));
            }
            Batch().Add(tower, new Vector3(p.x, low - TowerSink, p.y), rng.Range(0f, 360f), Vector3.one * k, tint);
            contact?.Tower(p, Mathf.Max(size.x, size.z) * k * 0.48f, height);
        }

        /// <summary>A round wall tower of the map's own shapes, with a pointed roof.</summary>
        void PlainTower(MeshKit kit, Vector3 foot, float height, float radius, Color roof)
        {
            kit.Lathe(foot, new[] { 0f, height }, new[] { radius, radius * 0.9f }, new[] { WallStone }, 8, rng);
            kit.Lathe(foot + new Vector3(0, height, 0), new[] { 0f, radius * 1.7f }, new[] { radius * 1.24f, 0f }, new[] { roof }, 8, rng);
        }

        /// <summary>Merlons round the top of a box turned by <paramref name="yaw"/>: one on each corner and more along the sides.</summary>
        static void Battlements(MeshKit kit, Vector3 top, float sx, float sz, float yaw, Color stone)
        {
            const float M = 0.018f;
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            var cube = new Vector3(M, M, M);
            int nx = Mathf.Max(2, Mathf.RoundToInt(sx / 0.045f)), nz = Mathf.Max(2, Mathf.RoundToInt(sz / 0.045f));
            for (int i = 0; i < nx; i++)
            {
                float x = -sx / 2 + M / 2 + i * (sx - M) / (nx - 1);
                kit.Box(top + q * new Vector3(x, 0, sz / 2 - M / 2), cube, yaw, stone);
                kit.Box(top + q * new Vector3(x, 0, -sz / 2 + M / 2), cube, yaw, stone);
            }
            for (int j = 1; j < nz - 1; j++)
            {
                float z = -sz / 2 + M / 2 + j * (sz - M) / (nz - 1);
                kit.Box(top + q * new Vector3(sx / 2 - M / 2, 0, z), cube, yaw, stone);
                kit.Box(top + q * new Vector3(-sx / 2 + M / 2, 0, z), cube, yaw, stone);
            }
        }

        /// <summary>The model of house <paramref name="k"/> of a city, or null when the art lacks it.</summary>
        GameObject HouseModel(City city, int k) => art != null ? art.ModelOf(HouseOf(city, k)) : null;

        /// <summary>
        /// A small town's well in the middle of its square: the studio one, its front towards the
        /// camera, a little over the eaves of the cottages round it; without the art, one of lathes
        /// and boxes.
        /// </summary>
        void Well(float cx, float cz)
        {
            GameObject model = art != null ? art.ModelOf("well") : null;
            if (model != null)
            {
                Place(model, cx, cz, 0.18f, rng.Range(-30f, 30f), false);
                return;
            }
            Vector3 at = Ground(cx, cz);
            MeshKit kit = Kit();
            kit.Lathe(at - new Vector3(0, 0.01f, 0), new[] { 0f, 0.05f }, new[] { 0.045f, 0.045f }, new[] { Palette.Boulder }, 8, rng);
            kit.Lathe(at + new Vector3(0, 0.05f, 0), new[] { 0f, 0.002f }, new[] { 0.035f, 0.035f }, new[] { Palette.Hex("#43689B") }, 8, rng);
            kit.Cylinder(at + new Vector3(-0.04f, 0.04f, 0), at + new Vector3(-0.04f, 0.11f, 0), 0.006f, 4, Palette.Wood, Palette.Wood);
            kit.Cylinder(at + new Vector3(0.04f, 0.04f, 0), at + new Vector3(0.04f, 0.11f, 0), 0.006f, 4, Palette.Wood, Palette.Wood);
            kit.Roof(at + new Vector3(0, 0.105f, 0), new Vector3(0.1f, 0.04f, 0.06f), 0f, Palette.Roof);
        }

        /// <summary>
        /// A market stall: the studio one, its front to the camera, or a counter under a striped
        /// awning in the kingdom's colour.
        /// </summary>
        void Stall(float x, float z, Color colour)
        {
            if (StudioThing("stall", new Vector2(x, z), StallLength, rng.Range(-20f, 20f), true))
                return;
            Vector3 at = Ground(x, z) - new Vector3(0, 0.005f, 0);
            float yaw = rng.Range(-20f, 20f);
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            MeshKit kit = Kit();
            kit.Box(at, new Vector3(0.1f, 0.035f, 0.05f), yaw, Palette.Wood);
            kit.Box(at + q * new Vector3(0, 0.035f, 0.005f), new Vector3(0.09f, 0.008f, 0.035f), yaw,
                rng.Chance(0.5f) ? Palette.Hex("#D58A52") : Palette.Hex("#89B35E"));
            foreach (float side in new[] { -0.045f, 0.045f })
                kit.Cylinder(at + q * new Vector3(side, 0, 0.022f), at + q * new Vector3(side, 0.085f, 0.022f), 0.004f, 4, Palette.Wood, Palette.Wood);
            for (int k = 0; k < 4; k++)
            {
                float x0 = -0.06f + k * 0.03f, x1 = x0 + 0.03f;
                Color c = k % 2 == 0 ? colour : Palette.Wall;
                kit.TwoSided(at + q * new Vector3(x0, 0.095f, 0.035f), at + q * new Vector3(x1, 0.095f, 0.035f),
                    at + q * new Vector3(x1, 0.075f, -0.035f), c);
                kit.TwoSided(at + q * new Vector3(x0, 0.095f, 0.035f), at + q * new Vector3(x1, 0.075f, -0.035f),
                    at + q * new Vector3(x0, 0.075f, -0.035f), c);
            }
        }

        /// <summary>Whether nothing placed is nearer than <paramref name="gap"/>.</summary>
        bool Clear(Vector2 p, float gap)
        {
            foreach (Vector2 q in placed)
            {
                if ((q - p).sqrMagnitude < gap * gap)
                    return false;
            }
            return true;
        }

        /// <summary>A spot at <paramref name="distance"/> from the centre, off the roads and clear of what is placed.</summary>
        bool FreeAround(float cx, float cz, float distance, float margin, float gap, out Vector2 p, System.Func<Vector2, bool> fits = null)
        {
            float start = rng.Range(0f, Mathf.PI * 2f);
            for (int k = 0; k < 12; k++)
            {
                float a = start + k * Mathf.PI / 6f;
                p = new Vector2(cx + Mathf.Cos(a) * distance, cz + Mathf.Sin(a) * distance);
                if (NearRoad(here, p.x, p.y, margin) || NearRiver(p.x, p.y, margin) || field.Height(p.x, p.y) < 0.05f
                    || (fits != null && !fits(p)))
                    continue;
                bool free = true;
                foreach (Vector2 q in placed)
                {
                    if ((q - p).sqrMagnitude < gap * gap)
                    {
                        free = false;
                        break;
                    }
                }
                if (free)
                    return true;
            }
            p = default;
            return false;
        }

        /// <summary>A prop with its own small mesh, so that it can move: boats and windmill sails.</summary>
        Transform Mover(string name, MeshKit kit, Vector3 position, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.AddComponent<MeshFilter>().sharedMesh = kit.ToMesh("Props " + name);
            go.AddComponent<MeshRenderer>().sharedMaterial = propMaterial;
            return go.transform;
        }

        /// <summary>Bushes; a share <paramref name="dry"/> of them olive and straw, as on the steppe.</summary>
        void Bushes(float cx, float cz, int count, float clear, float dry = 0f)
        {
            for (int k = 0; k < count; k++)
            {
                if (!Spot(cx, cz, clear, 0.13f, 0.84f, out Vector2 p))
                    break;
                float s = rng.Range(0.045f, 0.07f);
                Vector3 at = Ground(p.x, p.y);
                bool parched = rng.Chance(dry);
                // A studio bush, straw-tinted on the steppe; it sways like the low-poly ones.
                string kind = Any(BushModels);
                if (kind != null)
                {
                    ModelBatch.Source model = Scatter(kind);
                    Vector3 size = model.Bounds.size;
                    float scale = 2.4f * s / Mathf.Max(Mathf.Max(size.x, size.z), 1e-3f);
                    Color tint = (parched ? new Color(1.12f, 1.02f, 0.64f) : Color.white) * rng.Range(0.9f, 1.04f);
                    Batch().Add(model, at - new Vector3(0, 0.01f, 0), rng.Range(0f, 360f), Vector3.one * scale, tint, BushSway);
                    continue;
                }
                MeshKit kit = Kit();
                kit.Sway(at.y, BushSway, MeshKit.PhaseAt(at));
                Color dark = parched ? Palette.Hex("#8C8A33") : Palette.Leaves * 0.72f;
                Color light = parched ? Palette.Hex("#D2C35C") : Palette.LeavesLight * 0.95f;
                kit.Blob(at + new Vector3(0, s * 0.5f, 0), new Vector3(s, s * 0.8f, s), 6, 3, dark, light, rng, 0.15f);
                if (rng.Chance(0.6f))
                    kit.Blob(at + new Vector3(s * 0.8f, s * 0.35f, rng.Range(-0.3f, 0.3f) * s), Vector3.one * (s * 0.7f), 6, 3,
                        dark, light, rng, 0.15f);
                if (!parched && rng.Chance(0.35f))
                {
                    // Berries or blossom.
                    Color dot = rng.Chance(0.5f) ? Palette.Hex("#E8434F") : Palette.Hex("#FFF4FA");
                    for (int b = 0; b < 3; b++)
                    {
                        float a = rng.Range(Mathf.PI, Mathf.PI * 2f);
                        Vector3 q = at + new Vector3(Mathf.Cos(a) * s * 0.75f, s * rng.Range(0.6f, 1.1f), Mathf.Sin(a) * s * 0.75f);
                        kit.Blob(q, Vector3.one * 0.011f, 4, 2, dot, dot, rng, 0f);
                    }
                }
                kit.Still();
            }
        }

        /// <summary>Tufts of grass blades: straw on the steppe, green on meadows and hills.</summary>
        void Tufts(float cx, float cz, int count, float clear, bool dry)
        {
            Color low = dry ? Palette.Hex("#8F7E2A") : Palette.Leaves * 0.7f;
            Color high = dry ? Palette.Hex("#EED67A") : Palette.LeavesLight * 1.05f;
            for (int k = 0; k < count; k++)
            {
                if (!Spot(cx, cz, clear, 0.07f, 0.84f, out Vector2 p))
                    break;
                Vector3 at = Ground(p.x, p.y);
                MeshKit kit = Kit();
                int blades = rng.Range(7, 11);
                float turn = rng.Range(0f, Mathf.PI * 2f), s = rng.Range(0.9f, 1.35f) * (dry ? 1.3f : 1f);
                kit.Sway(at.y, GrassSway, MeshKit.PhaseAt(at));
                for (int b = 0; b < blades; b++)
                {
                    float a = turn + b * Mathf.PI * 2f / blades + rng.Range(-0.3f, 0.3f);
                    var lean = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 side = new Vector3(-lean.z, 0f, lean.x) * (0.009f * s);
                    Vector3 root = at + lean * 0.008f - new Vector3(0f, 0.004f, 0f);
                    Vector3 tip = at + lean * (rng.Range(0.022f, 0.04f) * s) + new Vector3(0f, rng.Range(0.04f, 0.07f) * s, 0f);
                    kit.TwoSided(root - side, root + side, tip, Color.Lerp(low, high, rng.Range(0.3f, 1f)));
                }
                kit.Still();
            }
        }

        /// <summary>Small clusters of one colour of flower on meadows.</summary>
        void Flowers(float cx, float cz, int count, float clear)
        {
            Color[] colours = { Palette.Hex("#FFFDF2"), Palette.Hex("#FFC61F"), Palette.Hex("#FF6B9A"), Palette.Hex("#A274FF") };
            for (int k = 0; k < count; k++)
            {
                if (!Spot(cx, cz, clear, 0.1f, 0.82f, out Vector2 p))
                    break;
                Color colour = colours[rng.Range(0, colours.Length)];
                MeshKit kit = Kit();
                int n = rng.Range(4, 7);
                for (int f = 0; f < n; f++)
                {
                    float a = rng.Range(0f, Mathf.PI * 2f), r = rng.Range(0f, 0.045f);
                    Vector3 at = Ground(p.x + Mathf.Cos(a) * r, p.y + Mathf.Sin(a) * r);
                    float h = rng.Range(0.015f, 0.03f);
                    kit.TwoSided(at + new Vector3(-0.003f, -0.003f, 0f), at + new Vector3(0.003f, -0.003f, 0f), at + new Vector3(0f, h, 0f),
                        Palette.Leaves * 0.8f);
                    kit.Blob(at + new Vector3(0f, h, 0f), new Vector3(0.011f, 0.008f, 0.011f), 4, 2, colour * 0.85f, colour, rng, 0f);
                }
            }
        }

        /// <summary>A few small stones on the steppe.</summary>
        void Pebbles(float cx, float cz, int count, float clear)
        {
            for (int k = 0; k < count; k++)
            {
                if (!Spot(cx, cz, clear, 0.1f, 0.8f, out Vector2 p))
                    break;
                float s = rng.Range(0.022f, 0.038f);
                if (StudioStone("pebbles", p.x, p.y, 3f * s, 0f, 0.006f))
                    continue;
                Kit().Blob(Ground(p.x, p.y) + new Vector3(0f, s * 0.25f, 0f), new Vector3(s, s * 0.65f, s * 0.85f), 5, 2,
                    Palette.RockDark, Palette.Boulder * 1.08f, rng, 0.2f);
            }
        }

        void Mushrooms(float cx, float cz, int count, float clear)
        {
            for (int k = 0; k < count; k++)
            {
                if (!Spot(cx, cz, clear, 0.1f, 0.8f, out Vector2 p))
                    break;
                if (StudioStone("mushrooms", p.x, p.y, rng.Range(0.05f, 0.075f), 0f, 0.004f))
                    continue;
                int n = rng.Range(1, 4);
                for (int m = 0; m < n; m++)
                {
                    Vector3 at = Ground(p.x + rng.Range(-0.03f, 0.03f), p.y + rng.Range(-0.03f, 0.03f));
                    float s = rng.Range(0.8f, 1.25f);
                    MeshKit kit = Kit();
                    kit.Lathe(at - new Vector3(0, 0.005f, 0), new[] { 0f, 0.03f * s }, new[] { 0.007f * s, 0.006f * s },
                        new[] { Palette.Wall }, 5, rng);
                    kit.Lathe(at + new Vector3(0, 0.026f * s, 0), new[] { 0f, 0.008f * s, 0.016f * s }, new[] { 0.024f * s, 0.018f * s, 0f },
                        new[] { Palette.Hex("#E2453A"), Palette.Hex("#F05A48") }, 7, rng, 0.05f);
                }
            }
        }

        /// <summary>Stumps and fallen logs in a wood (woods have dead trees), when the art has them.</summary>
        void Deadwood(float cx, float cz, int count, float clear)
        {
            for (int k = 0; k < count; k++)
            {
                string kind = Any(DeadwoodModels);
                if (kind == null || !Spot(cx, cz, clear, 0.14f, 0.8f, out Vector2 p))
                    return;
                StudioStone(kind, p.x, p.y, kind == "log" ? rng.Range(0.16f, 0.22f) : rng.Range(0.07f, 0.1f), 0f, 0.01f);
            }
        }

        /// <summary>A ring of standing stones in the wilds.</summary>
        void StandingStones(float cx, float cz)
        {
            int count = rng.Range(5, 8);
            float radius = rng.Range(0.18f, 0.24f), turn = rng.Range(0f, Mathf.PI * 2f);
            for (int k = 0; k < count; k++)
            {
                float a = turn + k * Mathf.PI * 2f / count;
                float x = cx + Mathf.Cos(a) * radius, z = cz + Mathf.Sin(a) * radius;
                placed.Add(new Vector2(x, z));
                float h = rng.Range(0.09f, 0.15f), w = rng.Range(0.022f, 0.03f);
                if (StudioStone("standing_stone", x, z, 2.6f * w, h, 0.015f))
                    continue;
                Kit().Lathe(Ground(x, z) - new Vector3(0, 0.015f, 0), new[] { 0f, h * 0.8f, h }, new[] { w, w * 0.85f, w * 0.4f },
                    new[] { Palette.Boulder, Palette.Boulder * 1.06f }, 4, rng, 0.12f, 0.08f);
            }
            placed.Add(new Vector2(cx, cz));
        }

        /// <summary>
        /// A windmill by the wheat fields: the studio one, about as big as a house (at 0.44 it
        /// stood bigger than the houses inside a town's walls, review of 2026-11-10), its front
        /// with the sails towards the camera; without the art, one of lathes whose sails turn. It
        /// keeps clear of the whole farmhouse, sails and eaves included: kept only from the
        /// house's middle, it stood half inside it (the players saw it, 2026-11-11).
        /// </summary>
        void Windmill(float cx, float cz)
        {
            const float Size = 0.36f;
            GameObject mill = art != null ? art.ModelOf("windmill") : null;
            float yaw = rng.Range(-30f, 30f);
            Vector2 half = mill != null ? SidesOf(mill) * (Size / 2f) : new Vector2(0.09f, 0.08f);
            bool Clear(Vector2 at) => fieldPrint == null || new Footprint(at, half, yaw).Gap(fieldPrint.Value) >= 0.02f;
            Vector2 p = default;
            bool found = false;
            foreach (float distance in new[] { 0.22f, 0.32f, 0.42f, 0.5f })
                if (found = FreeAround(cx, cz, distance, 0.06f, 0.17f, out p, Clear))
                    break;
            if (!found)
                return;
            placed.Add(p);
            if (mill != null)
            {
                Place(mill, p.x, p.y, Size, yaw, false);
                return;
            }
            Vector3 at = Ground(p.x, p.y) - new Vector3(0, 0.01f, 0);
            MeshKit kit = Kit();
            kit.Lathe(at, new[] { 0f, 0.24f }, new[] { 0.075f, 0.055f }, new[] { Palette.Wall }, 8, rng, 0f, 0.05f);
            kit.Lathe(at + new Vector3(0, 0.235f, 0), new[] { 0f, 0.1f }, new[] { 0.074f, 0f }, new[] { Palette.Roof }, 8, rng, 0f, 0.06f);
            kit.Box(at + new Vector3(0, 0, -0.07f), new Vector3(0.035f, 0.065f, 0.012f), 0f, Palette.Wood);
            kit.Box(at + new Vector3(0, 0.13f, -0.062f), new Vector3(0.022f, 0.025f, 0.01f), 0f, Palette.Hex("#43689B"));

            var sails = new MeshKit();
            Color canvas = Palette.Hex("#FFF6E0"), arm = Palette.Hex("#8A6242");
            sails.Cylinder(new Vector3(0, 0, 0.01f), new Vector3(0, 0, -0.018f), 0.013f, 6, arm, Palette.Roof);
            for (int k = 0; k < 4; k++)
            {
                float a = Mathf.PI / 4f + k * Mathf.PI / 2f;
                var u = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                var v = new Vector3(-u.y, u.x, 0);
                Vector3 z = new Vector3(0, 0, -0.012f);
                sails.Cylinder(z, z + u * 0.18f, 0.0045f, 4, arm, arm);
                Vector3 a0 = z + u * 0.045f, a1 = z + u * 0.175f, b1 = a1 + v * 0.05f, b0 = a0 + v * 0.04f;
                sails.TwoSided(a0, a1, b1, canvas);
                sails.TwoSided(a0, b1, b0, canvas * 0.96f);
            }
            Transform spinner = Mover("sails", sails, at + new Vector3(0, 0.25f, -0.072f), rng.Range(-15f, 15f));
            spinner.gameObject.AddComponent<Spin>().speed = rng.Range(25f, 45f);
        }

        /// <summary>A pen round the sheep, open where the roads pass and on one side as a gate.</summary>
        void Fence(float cx, float cz)
        {
            const int Posts = 16;
            const float Radius = 0.45f;
            int gate = rng.Range(0, Posts);
            var posts = new Vector3[Posts];
            var open = new bool[Posts];
            for (int k = 0; k < Posts; k++)
            {
                float a = k * Mathf.PI * 2f / Posts;
                float x = cx + Mathf.Cos(a) * Radius, z = cz + Mathf.Sin(a) * Radius * 0.92f;
                posts[k] = Ground(x, z);
                open[k] = NearRoad(here, x, z, 0.02f) || NearRiver(x, z, 0f) || posts[k].y < 0.04f;
            }
            ModelBatch.Source span = Scatter("fence");
            if (span != null)
            {
                // The studio spans bring their posts: one from each post to the next, none
                // through the gate or past a gap.
                for (int k = 0; k < Posts; k++)
                {
                    int n = (k + 1) % Posts;
                    if (!open[k] && !open[n] && k != gate)
                        StudioFence(span, posts[k], posts[n]);
                }
                return;
            }
            MeshKit kit = Kit();
            Color wood = Palette.Hex("#A07448");
            for (int k = 0; k < Posts; k++)
            {
                if (open[k])
                    continue;
                kit.Cylinder(posts[k] - new Vector3(0, 0.01f, 0), posts[k] + new Vector3(0, 0.055f, 0), 0.007f, 4, wood, wood * 1.1f);
                int n = (k + 1) % Posts;
                if (open[n] || k == gate)
                    continue;
                foreach (float h in new[] { 0.022f, 0.044f })
                    kit.Cylinder(posts[k] + new Vector3(0, h, 0), posts[n] + new Vector3(0, h, 0), 0.0045f, 4, wood * 0.95f, wood);
            }
        }

        /// <summary>The most a studio fence span stands tall, the pen's of shapes being 0.055.</summary>
        const float FenceHeight = 0.065f;

        /// <summary>
        /// How much longer than from post to post a studio span stands: its length runs over its
        /// two posts, so its posts' middles come about where the pen's posts are.
        /// </summary>
        const float FenceOverlap = 1.06f;

        /// <summary>
        /// A span of the studio fence from the pen's post <paramref name="a"/> to the next one
        /// <paramref name="b"/>, sheared to follow the ground between them like a wall.
        /// </summary>
        void StudioFence(ModelBatch.Source span, Vector3 a, Vector3 b)
        {
            Lay lay = LayOf(span);
            Vector2 from = new Vector2(a.x, a.z), to = new Vector2(b.x, b.z);
            float length = (to - from).magnitude;
            if (length < 1e-4f)
                return;
            Vector2 dir = (to - from) / length, m = (from + to) * 0.5f;
            float kx = length * FenceOverlap / Mathf.Max(lay.Length, 1e-3f);
            float k = Mathf.Min(kx, FenceHeight / Mathf.Max(span.Bounds.size.y, 1e-3f));
            float line = (a.y + b.y) * 0.5f, sag = Mathf.Max(0f, line - field.Height(m.x, m.y));
            float slope = Mathf.Clamp((b.y - a.y) / length, -0.5f, 0.5f);
            Matrix4x4 shear = Matrix4x4.identity;
            shear.m10 = slope * dir.x;
            shear.m12 = slope * dir.y;
            // The model's long side turned onto x and stretched to the span, then along the span.
            float degrees = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Matrix4x4 place = Matrix4x4.Translate(new Vector3(m.x, line - DecorSink - sag, m.y)) * shear
                * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, -degrees, 0), new Vector3(kx, k, k))
                * Matrix4x4.Rotate(Quaternion.Euler(0, lay.Angle, 0))
                * Matrix4x4.Translate(new Vector3(-lay.Middle.x, 0f, -lay.Middle.y));
            Batch().Add(span, place, Color.white * rng.Range(0.95f, 1.03f));
        }

        /// <summary>
        /// A signpost at a crossroads: the studio one, its three boards pointing three ways as
        /// drawn, or one of shapes with an arrow down each road (three at most).
        /// </summary>
        void Signpost(Cell cell, float cx, float cz)
        {
            float corner = 0f;
            for (int d = 0; d < 6; d++)
            {
                if ((cell.Roads >> d & 1) == 0)
                {
                    corner = d * Mathf.PI / 3f;
                    break;
                }
            }
            Vector2 p = Around(cx, cz, corner, 0.2f);
            placed.Add(p);
            if (StudioThing("signpost", p, SignpostHeight, rng.Range(-25f, 25f)))
                return;
            Vector3 at = Ground(p.x, p.y);
            MeshKit kit = Kit();
            Color post = Palette.Hex("#8A6242"), board = Palette.Hex("#EBC47E");
            kit.Cylinder(at - new Vector3(0, 0.01f, 0), at + new Vector3(0, 0.17f, 0), 0.008f, 5, post, post);
            kit.Blob(at, new Vector3(0.03f, 0.02f, 0.03f), 5, 2, Palette.RockDark, Palette.Boulder, rng, 0.2f);
            int arrows = 0;
            for (int d = 0; d < 6 && arrows < 3; d++)
            {
                if ((cell.Roads >> d & 1) == 0)
                    continue;
                Vector2 dir = Direction(d);
                var dir3 = new Vector3(dir.x, 0, dir.y);
                Vector3 bottom = at + new Vector3(0, 0.145f - 0.03f * arrows, 0);
                kit.Box(bottom + dir3 * 0.045f, new Vector3(0.08f, 0.018f, 0.007f), -d * 60f, board * (1f - 0.06f * arrows));
                Vector3 end = bottom + dir3 * 0.085f;
                kit.TwoSided(end, end + new Vector3(0, 0.018f, 0), end + dir3 * 0.016f + new Vector3(0, 0.009f, 0), board);
                arrows++;
            }
        }

        /// <summary>Reeds where the land meets a lake, and here and there on green sea shores.</summary>
        void Reeds(Cell cell, float cx, float cz)
        {
            for (int d = 0; d < 6; d++)
            {
                int n = world.Neighbor(cell.Index, d);
                if (n < 0 || !world.Cells[n].IsWater)
                    continue;
                if (!rng.Chance(world.Cells[n].Lake ? 0.75f : cell.Biome == Biome.Beach ? 0f : 0.3f))
                    continue;
                Vector2 dir = Direction(d), side = new Vector2(-dir.y, dir.x);
                float lateral = rng.Range(-0.35f, 0.35f);
                for (float t = 0.4f; t < 1.3f; t += 0.03f)
                {
                    Vector2 q = new Vector2(cx, cz) + dir * t + side * lateral;
                    if (field.Height(q.x, q.y) > 0.015f)
                        continue;
                    if (!NearRoad(cell, q.x, q.y, 0f))
                        ReedClump(q.x, q.y);
                    break;
                }
            }
        }

        /// <summary>Reeds here and there on the banks of the rivers along the cell.</summary>
        void RiverReeds(Cell cell, float cx, float cz)
        {
            for (int d = 0; d < 6; d++)
            {
                if ((cell.Rivers >> d & 1) == 0 || !rng.Chance(0.55f))
                    continue;
                Vector2 dir = Direction(d), side = new Vector2(-dir.y, dir.x);
                int clumps = rng.Range(1, 3);
                for (int c = 0; c < clumps; c++)
                {
                    float lateral = rng.Range(-0.4f, 0.4f);
                    for (float t = 0.45f; t < 0.9f; t += 0.015f)
                    {
                        Vector2 q = new Vector2(cx, cz) + dir * t + side * lateral;
                        if (field.RiverEdge(q.x, q.y) > RiverCourse.Bank * 0.6f)
                            continue;
                        if (!NearRoad(cell, q.x, q.y, 0.05f))
                            ReedClump(q.x, q.y);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// A stone bridge where a road crosses a river: ramps from bank to bank over a low arch,
        /// with parapets, on piers standing in the bed. Built from the cell on the lower-numbered side.
        /// </summary>
        void Bridges(Cell cell, float cx, float cz)
        {
            for (int d = 0; d < 3; d++)
            {
                if ((cell.Roads >> d & 1) == 0 || (cell.Rivers >> d & 1) == 0)
                    continue;
                Vector2 dir = Direction(d), centre = new Vector2(cx, cz);
                // Where the road meets the middle of the river.
                Vector2 cross = centre + dir * HexMetrics.InnerRadius;
                float deepest = float.MaxValue;
                for (float t = 0.55f; t <= 1.2f; t += 0.01f)
                {
                    Vector2 q = centre + dir * t;
                    float e = field.RiverEdge(q.x, q.y);
                    if (e < deepest)
                    {
                        deepest = e;
                        cross = q;
                    }
                }
                if (!field.NearestRiver(cross, out RiverCourse river, out int at))
                    continue;
                float level = river.Levels[at];
                float half = river.Widths[at] * 0.5f + RiverCourse.Bank + 0.07f;
                Bridge(cross, dir, half, level);
            }
        }

        void Bridge(Vector2 cross, Vector2 dir, float half, float level)
        {
            Color stone = Palette.Hex("#D2C6AE"), dark = Palette.Hex("#A89C88"), path = Palette.Hex("#C2A57E");
            const float Width = 0.13f, Thick = 0.03f, Wall = 0.016f, WallHeight = 0.028f;
            var side = new Vector3(-dir.y, 0f, dir.x);
            MeshKit kit = Kit();
            // The deck's line: from the ground beyond each bank up over the middle.
            const int Segments = 8;
            var deck = new Vector3[Segments + 1];
            float reach = half + 0.08f, top = level + 0.07f;
            for (int k = 0; k <= Segments; k++)
            {
                float s = -reach + 2f * reach * k / Segments;
                Vector2 q = cross + dir * s;
                float arch = top + 0.03f * (1f - (s / reach) * (s / reach));
                float ground = field.Height(q.x, q.y) + 0.012f;
                // On the banks the deck lies on the ground; over the water it keeps its arch.
                float y = Mathf.Abs(s) > half ? Mathf.Lerp(arch, ground, (Mathf.Abs(s) - half) / (reach - half)) : Mathf.Max(arch, ground);
                deck[k] = new Vector3(q.x, y, q.y);
            }
            for (int k = 0; k < Segments; k++)
            {
                Vector3 a = deck[k], b = deck[k + 1];
                Vector3 w = side * (Width * 0.5f), down = Vector3.up * Thick;
                kit.Quad(a - w, b - w, b + w, a + w, path, Vector3.up);
                kit.Quad(a - w - down, b - w - down, b - w, a - w, dark, -side);
                kit.Quad(a + w - down, b + w - down, b + w, a + w, dark, side);
                kit.Quad(a - w - down, b - w - down, b + w - down, a + w - down, dark, Vector3.down);
                // Parapets along both edges.
                foreach (float edge in new[] { -1f, 1f })
                {
                    Vector3 o = side * (edge * (Width * 0.5f - Wall * 0.5f)), ww = side * (Wall * 0.5f), up = Vector3.up * WallHeight;
                    Vector3 pa = a + o, pb = b + o;
                    kit.Quad(pa - ww + up, pb - ww + up, pb + ww + up, pa + ww + up, stone, Vector3.up);
                    kit.Quad(pa - ww, pb - ww, pb - ww + up, pa - ww + up, stone * 0.9f, -side);
                    kit.Quad(pa + ww, pb + ww, pb + ww + up, pa + ww + up, stone * 0.9f, side);
                }
            }
            // Piers at the water's edges, down into the bed.
            float yaw = -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            foreach (float s in new[] { -half + 0.04f, half - 0.04f })
            {
                Vector2 q = cross + dir * s;
                float bottom = level - 0.06f;
                int k = Mathf.Clamp(Mathf.RoundToInt((s + reach) / (2f * reach) * Segments), 0, Segments);
                kit.Box(new Vector3(q.x, bottom, q.y), new Vector3(0.06f, deck[k].y - Thick - bottom, Width * 0.9f), yaw, dark);
            }
        }

        void ReedClump(float x, float z)
        {
            var at = new Vector3(x, Mathf.Max(field.Height(x, z), -0.03f), z);
            MeshKit kit = Kit();
            kit.Sway(at.y - 0.01f, GrassSway * 0.8f, MeshKit.PhaseAt(at));
            int blades = rng.Range(5, 9);
            for (int b = 0; b < blades; b++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f), r = rng.Range(0f, 0.035f);
                Vector3 root = at + new Vector3(Mathf.Cos(a) * r, -0.01f, Mathf.Sin(a) * r);
                float h = rng.Range(0.06f, 0.12f);
                Vector3 lean = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (h * 0.25f);
                Vector3 across = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.007f;
                Color green = Color.Lerp(Palette.Hex("#5E9A3A"), Palette.Hex("#A3C957"), rng.Value());
                kit.TwoSided(root - across, root + across, root + lean + Vector3.up * h, green);
                if (b < 2)
                {
                    Vector3 top = root + lean * 0.8f + Vector3.up * (h * 0.8f);
                    kit.Cylinder(top - Vector3.up * 0.022f, top, 0.0065f, 5, Palette.Hex("#7A4A2A"), Palette.Hex("#5C3820"));
                }
            }
            kit.Still();
        }

        /// <summary>Water cells: lily pads on lakes, a fishing boat with its net where the fish are.</summary>
        void Water(Cell cell, float cx, float cz)
        {
            if (cell.Lake)
                LilyPads(cx, cz);
            if (cell.Resource != Resource.Fish)
                return;
            if (cell.Building == FieldBuilding.Fishery)
                Fishery(cell, cx, cz);
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f), d = rng.Range(0.25f, 0.45f);
                float x = cx + Mathf.Cos(a) * d, z = cz + Mathf.Sin(a) * d;
                if (field.Height(x, z) > -0.08f)
                    continue;
                float yaw = rng.Range(-40f, 40f) + (rng.Chance(0.5f) ? 180f : 0f);
                Boat(x, z, yaw, cell.Owner, 1f);
                Quaternion q = Quaternion.Euler(0, yaw, 0);
                for (int k = 0; k < 6; k++)
                {
                    float f = Mathf.PI * (0.65f + 0.7f * k / 5f);
                    Vector3 at = new Vector3(x, 0.004f, z) + q * new Vector3(Mathf.Cos(f) * 0.17f - 0.08f, 0, Mathf.Sin(f) * 0.17f);
                    Color c = k % 2 == 0 ? Palette.Hex("#F26B3A") : Palette.Snow;
                    Kit().Blob(at, new Vector3(0.011f, 0.008f, 0.011f), 5, 2, c, c, rng, 0f);
                }
                return;
            }
        }

        /// <summary>The fishery on its stilts in the water, towards the nearest shore and the camera.</summary>
        void Fishery(Cell cell, float cx, float cz)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int d = 0; d < 6; d++)
            {
                int n = world.Neighbor(cell.Index, d);
                if (n < 0 || !world.Cells[n].IsLand)
                    continue;
                float score = -Mathf.Sin(d * Mathf.PI / 3f) * 0.5f + rng.Range(0f, 0.2f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = d;
                }
            }
            Vector2 at = new Vector2(cx, cz) + (best >= 0 ? Direction(best) * 0.42f : Vector2.zero);
            placed.Add(at);
            GameObject prefab = art != null ? art.ModelOf(ModelOf(FieldBuilding.Fishery)) : null;
            float yaw = best >= 0 ? 90f - best * 60f + rng.Range(-15f, 15f) : rng.Range(0f, 360f);
            if (prefab != null)
                Place(prefab, at.x, at.y, 0.48f, yaw, false, -0.05f);
            else
                House(at.x, at.y, 1.2f);
        }

        void LilyPads(float cx, float cz)
        {
            int count = rng.Range(3, 8);
            for (int attempt = 0, made = 0; attempt < 30 && made < count; attempt++)
            {
                float x = rng.Range(-0.8f, 0.8f), z = rng.Range(-0.8f, 0.8f);
                if (!InsideHex(x, z, 0.84f))
                    continue;
                x += cx;
                z += cz;
                float h = field.Height(x, z);
                if (h > -0.04f || h < -0.35f)
                    continue;
                made++;
                float r = rng.Range(0.025f, 0.042f);
                Color pad = Color.Lerp(Palette.Hex("#4E9F3D"), Palette.Hex("#7CC24C"), rng.Value());
                Kit().Lathe(new Vector3(x, 0.002f, z), new[] { 0f, 0.003f }, new[] { r, r }, new[] { pad }, 7, rng, 0.08f, 0.04f);
                if (rng.Chance(0.3f))
                    Kit().Blob(new Vector3(x, 0.01f, z), new Vector3(0.014f, 0.01f, 0.014f), 5, 2,
                        Palette.Hex("#F7A8C8"), Palette.Hex("#FFE3EE"), rng, 0.1f);
            }
        }

        /// <summary>How long the studio boat is, at scale 1, as the stand-in.</summary>
        const float BoatLength = 0.26f;

        /// <summary>How deep the studio boat sits, a share of its height (keel to masthead).</summary>
        const float BoatDraft = 0.1f;

        /// <summary>
        /// 0 when the studio boat's bow lies at the far end of its long side as measured, 180
        /// when at the near one: set by looking at the model.
        /// </summary>
        const float BoatBow = 0f;

        /// <summary>
        /// The studio boat, when the art has one: its long side along local +x, sunk to its
        /// waterline, rocking, with its sail (<see cref="Sail"/>).
        /// </summary>
        bool StudioBoat(float x, float z, float yaw, int country, float scale)
        {
            ModelBatch.Source model = Scatter("boat");
            GameObject prefab = art != null ? art.ModelOf("boat") : null;
            if (model == null || prefab == null)
                return false;
            Lay lay = LayOf(model);
            float k = BoatLength * scale / Mathf.Max(lay.Length, 1e-3f);
            var boat = new GameObject("boat").transform;
            boat.SetParent(root, false);
            boat.localPosition = new Vector3(x, 0f, z);
            boat.localRotation = Quaternion.Euler(0, yaw, 0);
            Transform hull = Object.Instantiate(prefab, boat).transform;
            // A yaw turns the model's lines by minus itself (see Footprint): its long side, at
            // lay.Angle, comes to lie along +x.
            hull.localRotation = Quaternion.Euler(0, lay.Angle + BoatBow, 0);
            hull.localScale = Vector3.one * k;
            Vector3 middle = hull.localRotation * new Vector3(lay.Middle.x, 0f, lay.Middle.y) * k;
            hull.localPosition = new Vector3(-middle.x, -BoatDraft * model.Bounds.size.y * k, -middle.z);
            Sail(boat, model, hull, k, country);
            boat.gameObject.AddComponent<Bob>();
            return true;
        }

        /// <summary>
        /// The studio boat's sail, which the model leaves out (only its mast stands): cloth like
        /// the banners', from just under the masthead towards the stern, with the owner's flag
        /// on it or undyed on a boat nobody owns. The players asked for the sail in the flag's
        /// colours (2026-11-10). It is the boat's child, so it rocks with it. The masthead is the
        /// model's highest point, the stern the end of the hull farther from the mast (the mast
        /// stands a little forward of the middle), the gunwale the highest point away from the
        /// mast. Without the cloth's material, three triangles of the kingdom's colour.
        /// </summary>
        void Sail(Transform boat, ModelBatch.Source model, Transform hull, float k, int country)
        {
            var points = new Vector3[model.Positions.Length];
            for (int i = 0; i < points.Length; i++)
                points[i] = hull.localPosition + hull.localRotation * (model.Positions[i] * k);
            Vector3 head = points[0];
            float bow = float.MaxValue, stern = float.MinValue;
            foreach (Vector3 p in points)
            {
                if (p.y > head.y) head = p;
                bow = Mathf.Min(bow, p.x);
                stern = Mathf.Max(stern, p.x);
            }
            float length = stern - bow, gunwale = float.MinValue;
            foreach (Vector3 p in points)
                if (Mathf.Abs(p.x - head.x) > 0.2f * length)
                    gunwale = Mathf.Max(gunwale, p.y);
            float mast = head.y - gunwale;
            if (!(mast > 0f))
                return;
            float aft = stern - head.x >= head.x - bow ? 1f : -1f;
            float reach = 0.85f * (aft > 0f ? stern - head.x : head.x - bow);
            Vector3 top = new Vector3(head.x, head.y - 0.06f * mast, head.z);
            float height = 0.8f * mast;
            var along = new Vector3(aft, 0f, 0f);
            if (FlagArt.ClothMaterial != null)
            {
                var cloth = new FlagCloth();
                cloth.Add(top, along, reach, height, country >= 0 ? country : FlagArt.Undyed, 0.45f);
                var go = new GameObject("sail");
                go.transform.SetParent(boat, false);
                go.AddComponent<MeshFilter>().sharedMesh = cloth.ToMesh("Props sail");
                go.AddComponent<MeshRenderer>().sharedMaterial = FlagArt.ClothMaterial;
                return;
            }
            Color colour = country >= 0 ? Palette.Countries[country % Palette.Countries.Length] : Palette.Wall;
            var kit = new MeshKit();
            Vector3 foot = top - new Vector3(0f, height, 0f), clew = foot + along * reach;
            Vector3 belly = (top + foot + clew) / 3f + new Vector3(0f, 0f, 0.08f * reach);
            kit.TwoSided(top, belly, foot, colour);
            kit.TwoSided(top, clew, belly, colour * 1.06f);
            kit.TwoSided(belly, clew, foot, colour * 0.94f);
            Mover("sail", kit, Vector3.zero, 0f).SetParent(boat, false);
        }

        /// <summary>
        /// A little sailing boat that rocks on the water: its bow points along local +x. The
        /// studio one when the art has it, else a stand-in; its sail shows whose it is
        /// (<paramref name="country"/>, -1 for nobody's).
        /// </summary>
        void Boat(float x, float z, float yaw, int country, float scale)
        {
            if (StudioBoat(x, z, yaw, country, scale))
                return;
            Color sail = country >= 0 ? Palette.Countries[country % Palette.Countries.Length] : Palette.Wall;
            var kit = new MeshKit();
            float length = 0.26f * scale, width = 0.1f * scale, height = 0.05f * scale, floor = 0.018f * scale;
            Vector3[] top =
            {
                new Vector3(length * 0.5f, height * 1.3f, 0),
                new Vector3(length * 0.22f, height, width * 0.5f),
                new Vector3(-length * 0.3f, height, width * 0.48f),
                new Vector3(-length * 0.47f, height * 1.08f, width * 0.3f),
                new Vector3(-length * 0.47f, height * 1.08f, -width * 0.3f),
                new Vector3(-length * 0.3f, height, -width * 0.48f),
                new Vector3(length * 0.22f, height, -width * 0.5f),
            };
            int n = top.Length;
            var keel = new Vector3[n];
            for (int k = 0; k < n; k++)
                keel[k] = new Vector3(top[k].x * 0.8f, -0.012f * scale, top[k].z * 0.45f);
            Color outside = Palette.Hex("#8C5A36"), rim = Palette.Hex("#C98F55"), inside = Palette.Hex("#6E4A2E");
            for (int k = 0; k < n; k++)
            {
                int j = (k + 1) % n;
                Vector3 middle = (top[k] + top[j] + keel[k] + keel[j]) * 0.25f;
                var outward = new Vector3(middle.x, 0, middle.z);
                // Planking below, a lighter strake under the gunwale; the inner side is darker.
                Vector3 lowK = Vector3.Lerp(keel[k], top[k], 0.7f), lowJ = Vector3.Lerp(keel[j], top[j], 0.7f);
                kit.Quad(keel[k], keel[j], lowJ, lowK, outside, outward);
                kit.Quad(lowK, lowJ, top[j], top[k], rim, outward);
                kit.Quad(keel[k], keel[j], top[j], top[k], inside, -outward);
                kit.Triangle(Vector3.zero - new Vector3(0, 0.012f * scale, 0), keel[k], keel[j], outside * 0.8f, Vector3.down);
                var deck = new Vector3(0, floor, 0);
                kit.Triangle(deck, new Vector3(top[k].x * 0.85f, floor, top[k].z * 0.7f),
                    new Vector3(top[j].x * 0.85f, floor, top[j].z * 0.7f), Palette.Hex("#A47A50"), Vector3.up);
            }
            kit.Box(new Vector3(-length * 0.12f, height * 0.6f, 0), new Vector3(0.025f * scale, 0.006f * scale, width * 0.88f), 0f, rim);
            float mastX = length * 0.08f, mastTop = 0.27f * scale;
            kit.Cylinder(new Vector3(mastX, floor, 0), new Vector3(mastX, mastTop, 0), 0.006f * scale, 5, Palette.Wood, Palette.Wood);
            Vector3 head = new Vector3(mastX, mastTop - 0.01f * scale, 0), foot = new Vector3(mastX, 0.075f * scale, 0);
            Vector3 clew = new Vector3(mastX - 0.14f * scale, 0.08f * scale, 0);
            Vector3 belly = (head + foot + clew) / 3f + new Vector3(0, 0, -0.02f * scale);
            kit.TwoSided(head, belly, foot, sail);
            kit.TwoSided(head, clew, belly, sail * 1.06f);
            kit.TwoSided(belly, clew, foot, sail * 0.94f);
            Transform boat = Mover("boat", kit, new Vector3(x, 0f, z), yaw);
            boat.gameObject.AddComponent<Bob>();
        }

        /// <summary>A wooden pier into the nearest water, with a boat moored alongside.</summary>
        void Pier(City city, float cx, float cz)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int d = 0; d < 6; d++)
            {
                int n = world.Neighbor(city.Cell, d);
                if (n < 0 || !world.Cells[n].IsWater)
                    continue;
                // Piers towards the camera (−z) stay in view in front of the houses.
                float score = -Mathf.Sin(d * Mathf.PI / 3f) + rng.Range(0f, 0.3f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = d;
                }
            }
            if (best < 0)
                return;
            Vector2 dir = Direction(best), centre = new Vector2(cx, cz);
            float start = -1f;
            for (float t = 0.45f; t < 1.4f; t += 0.02f)
            {
                Vector2 q = centre + dir * t;
                if (field.Height(q.x, q.y) < 0.03f)
                {
                    start = t - 0.03f;
                    break;
                }
            }
            if (start < 0f)
                return;
            // Long enough to reach past the foam: a shorter one lay on the sand at the water's edge.
            const float Length = 0.56f, Deck = 0.035f;
            var dir3 = new Vector3(dir.x, 0, dir.y);
            var side = new Vector3(-dir.y, 0, dir.x);
            Vector3 origin = new Vector3(cx, 0, cz) + dir3 * start;
            MeshKit kit = Kit();
            Color light = Palette.Hex("#B08050"), dark = Palette.Hex("#94683F");
            int planks = Mathf.RoundToInt(Length / 0.03f);
            for (int k = 0; k < planks; k++)
            {
                Vector3 c = origin + dir3 * (0.015f + k * 0.03f) + new Vector3(0, Deck, 0);
                kit.Box(c, new Vector3(0.026f, 0.01f, 0.09f), -best * 60f, k % 2 == 0 ? light : dark);
            }
            for (float t = 0.06f; t < Length; t += 0.12f)
            {
                foreach (float s in new[] { -0.05f, 0.05f })
                {
                    Vector3 c = origin + dir3 * t + side * s;
                    // Down to the bed, wherever it is.
                    float foot = Mathf.Min(field.Height(c.x, c.z), 0f) - 0.03f;
                    kit.Cylinder(new Vector3(c.x, foot, c.z), c + new Vector3(0, Deck + 0.025f, 0), 0.008f, 5, Palette.Trunk, Palette.Trunk);
                }
            }
            Vector3 berth = origin + dir3 * (Length * 0.75f) + side * (rng.Chance(0.5f) ? 0.11f : -0.11f);
            if (field.Height(berth.x, berth.z) < -0.03f)
                Boat(berth.x, berth.z, -best * 60f + (rng.Chance(0.5f) ? 180f : 0f), city.Country, 0.9f);
        }

        /// <summary>A simple house when the studio models are missing.</summary>
        void House(float x, float z, float s)
        {
            Vector3 at = Ground(x, z) - new Vector3(0, 0.02f, 0);
            float yaw = rng.Range(-20f, 20f);
            var size = new Vector3(0.2f, 0.14f, 0.15f) * s;
            MeshKit kit = Kit();
            kit.Box(at, size, yaw, Palette.Wall);
            kit.Roof(at + new Vector3(0, size.y, 0), new Vector3(size.x, size.y * 0.75f, size.z), yaw, Palette.Roof);
        }

        /// <summary>
        /// The kingdom's flag on a pole: cloth with the flag the players drew, or three triangles
        /// of the kingdom's colour without the flag's material.
        /// </summary>
        void Banner(float x, float z, int country, float height)
        {
            Vector3 at = Ground(x, z);
            MeshKit kit = Kit();
            Vector3 top = at + new Vector3(0, height, 0);
            kit.Cylinder(at - new Vector3(0, 0.02f, 0), top, 0.01f, 5, Palette.Pole, Palette.Pole);
            kit.Blob(top + new Vector3(0, 0.015f, 0), new Vector3(0.022f, 0.022f, 0.022f), 5, 2, Palette.HayDark, Palette.Gold, rng, 0f);
            if (cellCloth != null && FlagArt.ClothMaterial != null)
            {
                cellCloth.Add(top - new Vector3(0, 0.012f, 0), new Vector3(1f, 0f, 0.12f), 0.27f, 0.18f, country);
                return;
            }
            Color color = Palette.Countries[country % Palette.Countries.Length];
            Vector3 a = top - new Vector3(0, 0.01f, 0), b = top - new Vector3(0, 0.17f, 0);
            Vector3 tip = top + new Vector3(0.24f, -0.07f, 0.03f);
            Vector3 mid = (a + b) * 0.5f + new Vector3(0.12f, 0.01f, -0.02f);
            kit.TwoSided(a, mid, b, color);
            kit.TwoSided(a, tip, mid, color * 1.08f);
            kit.TwoSided(mid, tip, b, color * 0.92f);
        }

        /// <summary>
        /// A monster group: one to three figures by its size; a lair has a ring of dark rocks
        /// round the den.
        /// </summary>
        void Group(Cell cell, float cx, float cz)
        {
            if (cell.Lair)
            {
                int rocks = rng.Range(4, 6);
                for (int k = 0; k < rocks; k++)
                {
                    float angle = k * Mathf.PI * 2f / rocks + rng.Range(-0.3f, 0.3f);
                    if (Mathf.Sin(angle) < -0.55f)
                        continue; // keep the side facing the camera open
                    float d = rng.Range(0.3f, 0.38f);
                    float x = cx + Mathf.Cos(angle) * d, z = cz + Mathf.Sin(angle) * d;
                    float s = rng.Range(0.09f, 0.15f);
                    // The studio's boulders, like the land's other stones; the faceted stand-in without the art.
                    if (StudioStone("boulder", x, z, 2.1f * s))
                        continue;
                    Kit().Blob(Ground(x, z) + new Vector3(0, s * 0.4f, 0), new Vector3(s, s * 1.1f, s), 6, 3,
                        Palette.LairGround * 0.7f, Palette.LairGround * 1.15f, rng, 0.22f);
                }
            }
            Unit unit = Units.Of(cell.Monster);
            int figures = unit == Unit.Troll ? (cell.Count > 1 ? 2 : 1) : cell.Count <= 4 ? 1 : cell.Count <= 12 ? 2 : 3;
            // A little under their battle size (SizeOf), the upright ones well under it (UprightOnMap).
            bool upright = unit == Unit.Troll || unit == Unit.Goblins || unit == Unit.Bandits || unit == Unit.Skeletons;
            float size = SizeOf(unit) * 0.88f * (upright ? UprightOnMap : 1f) * (cell.Lair && unit == Unit.Troll ? 1.12f : 1f);
            Creatures(unit, cx, cz, figures, size);
        }

        void Creatures(Unit unit, float cx, float cz, int count, float size)
        {
            GameObject prefab = art != null ? art.ModelOf(ModelOf(unit)) : null;
            for (int k = 0; k < count; k++)
            {
                float angle = k * Mathf.PI * 2f / count + 0.6f, d = count == 1 ? 0f : 0.19f;
                float x = cx + Mathf.Cos(angle) * d, z = cz + Mathf.Sin(angle) * d - 0.05f;
                placed.Add(new Vector2(x, z));
                if (prefab != null)
                {
                    Place(prefab, x, z, size * rng.Range(0.92f, 1.08f), rng.Range(-35f, 35f), true);
                    continue;
                }
                Color body = ColourOf(unit);
                Kit().Blob(Ground(x, z) + new Vector3(0, size * 0.35f, 0), new Vector3(size * 0.3f, size * 0.35f, size * 0.4f),
                    7, 4, body * 0.8f, body, rng, 0.08f);
            }
        }

        /// <summary>A place heroes visit: a chest, a pile of goods, a shrine or a watchtower.</summary>
        void PlaceProps(Cell cell, float cx, float cz)
        {
            string key = ModelOf(cell.Place);
            GameObject prefab = art != null && key != null ? art.ModelOf(key) : null;
            placed.Add(new Vector2(cx, cz));
            switch (cell.Place)
            {
                case Core.Place.WoodPile:
                    Logs(cx, cz, true);
                    return;
                case Core.Place.StonePile:
                    Boulders(new Vector2(cx, cz), true, false);
                    return;
                case Core.Place.GoldPile:
                    Coins(cx, cz);
                    return;
            }
            float size = cell.Place == Core.Place.Watchtower ? 0.8f : cell.Place == Core.Place.Chest ? 0.3f : 0.5f;
            if (prefab != null)
            {
                Place(prefab, cx, cz, size, rng.Range(-20f, 20f), false);
                return;
            }
            // Stand-ins until the models come.
            Vector3 at = Ground(cx, cz);
            MeshKit kit = Kit();
            switch (cell.Place)
            {
                case Core.Place.Chest:
                    kit.Box(at, new Vector3(0.2f, 0.11f, 0.13f), 10f, Palette.Wood);
                    kit.Box(at + new Vector3(0, 0.11f, 0), new Vector3(0.21f, 0.04f, 0.14f), 10f, Palette.Gold);
                    break;
                case Core.Place.Watchtower:
                    kit.Box(at, new Vector3(0.22f, 0.6f, 0.22f), 0f, Palette.Wood);
                    kit.Roof(at + new Vector3(0, 0.6f, 0), new Vector3(0.3f, 0.2f, 0.3f), 0f, Palette.Roof);
                    break;
                default:
                    StandingStones(cx, cz);
                    Color glow = cell.Place == Core.Place.ShrineAttack ? Palette.Hex("#E8743B") : Palette.Hex("#4FA3E0");
                    kit.Blob(at + new Vector3(0, 0.12f, 0), new Vector3(0.07f, 0.12f, 0.07f), 6, 3, glow * 0.8f, glow, rng, 0.1f);
                    break;
            }
        }

        /// <summary>How wide the studio heap of gold is.</summary>
        const float GoldLength = 0.26f;

        /// <summary>A small heap of gold coins: the studio one, or a blob with coins round it.</summary>
        void Coins(float cx, float cz)
        {
            if (StudioThing("gold", new Vector2(cx, cz), GoldLength, rng.Range(-30f, 30f), true))
                return;
            MeshKit kit = Kit();
            kit.Blob(Ground(cx, cz) + new Vector3(0, 0.03f, 0), new Vector3(0.13f, 0.07f, 0.11f), 7, 3, Palette.HayDark, Palette.Gold, rng, 0.15f);
            for (int k = 0; k < 6; k++)
            {
                float angle = rng.Range(0f, Mathf.PI * 2f), d = rng.Range(0.12f, 0.2f);
                float x = cx + Mathf.Cos(angle) * d, z = cz + Mathf.Sin(angle) * d;
                Vector3 c = Ground(x, z);
                kit.Cylinder(c, c + new Vector3(0, 0.012f, 0), 0.03f, 8, Palette.Gold, Palette.Gold * 1.1f);
            }
        }

        /// <summary>
        /// A studio model standing on the ground, or at height <paramref name="y"/> when given (in
        /// the water). A building (anything not <paramref name="idle"/>) shades the ground round
        /// its foot.
        /// </summary>
        Transform Place(GameObject prefab, float x, float z, float size, float yaw, bool idle, float? y = null)
        {
            GameObject go = Object.Instantiate(prefab, root);
            go.transform.localPosition = y.HasValue ? new Vector3(x, y.Value, z) : Ground(x, z) - new Vector3(0, 0.015f, 0);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one * size;
            if (idle)
                go.AddComponent<Idle>();
            else
                Foot(prefab, x, z, size, yaw);
            return go.transform;
        }

        /// <summary>
        /// The shade round a placed model's foot (<see cref="ContactShade"/>), from where its prefab
        /// meets the ground (<see cref="ModelFooting"/>): round the walls, not under the eaves or a
        /// windmill's sails. None in the water.
        /// </summary>
        void Foot(GameObject prefab, float x, float z, float size, float yaw)
        {
            if (contact == null || !prefab.TryGetComponent(out ModelFooting footing) || field.Height(x, z) <= 0.03f)
                return;
            // Turned by yaw about +y, the prefab's x runs along (cos, -sin) and its z along (sin, cos).
            float a = yaw * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            Vector2 c = footing.centre * size;
            var at = new Vector2(x + c.x * cos + c.y * sin, z - c.x * sin + c.y * cos);
            contact.Building(at, footing.half * size, yaw, footing.height * size);
        }

        /// <summary>
        /// The tops of the homes' chimneys in their prefabs (the largest side 1 long, the bottom
        /// centre at 0), the middle of each chimney's rim on the model's height map: the thatched
        /// cottage smokes through the top of its thatch, the smelter out of its kiln.
        /// </summary>
        static readonly Dictionary<string, Vector3[]> ChimneyTops = new Dictionary<string, Vector3[]>
        {
            ["cottage_a"] = new[] { new Vector3(0.147f, 1.0f, 0.266f) },
            ["cottage_b"] = new[] { new Vector3(0.025f, 0.96f, 0.006f) },
            ["tannery"] = new[] { new Vector3(0.069f, 0.715f, 0.372f) },
            ["tailor"] = new[] { new Vector3(0.113f, 1.0f, 0.322f) },
            ["smelter"] = new[] { new Vector3(0.243f, 0.698f, 0.027f) },
            ["hunters_lodge"] = new[] { new Vector3(0.166f, 0.863f, 0.357f) },
        };

        /// <summary>Smoke over three houses in four, from their place rather than a random draw, so nothing else moves.</summary>
        void Chimneys(string model, Transform house)
        {
            if (cellSmoke == null || !ChimneyTops.TryGetValue(model, out Vector3[] tops))
                return;
            float seed = MeshKit.PhaseAt(house.localPosition);
            if (seed > 0.75f)
                return;
            for (int k = 0; k < tops.Length; k++)
                cellSmoke.Add(root.InverseTransformPoint(house.TransformPoint(tops[k])), k == 0 ? 1f : 0.75f, Mathf.Repeat(seed * 4.3f + k * 0.37f, 1f));
        }
    }
}

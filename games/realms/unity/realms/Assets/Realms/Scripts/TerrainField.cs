using System;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realms
{
    /// <summary>
    /// The ground textures, in the order of the texture array Setup builds and RealmsGround.hlsl
    /// reads: each is the name of a texture in Art/Textures. The terrain's vertices carry shares of
    /// the first <see cref="Count"/>; the cobbles and the road strip (a band along the road, as
    /// wide as half its length) follow them in the array for the towns' squares and the roads.
    /// </summary>
    public static class GroundLayer
    {
        public const int Grass = 0, DryGrass = 1, Forest = 2, Sand = 3, Scree = 4, Rock = 5, Snow = 6, Dirt = 7;
        public const int Cobble = 8, Road = 9;
        public const int Count = 8, ArrayCount = 10;
        public static readonly string[] Names = { "grass", "drygrass", "forest", "sand", "scree", "rock", "snow", "dirt", "cobble", "road" };
    }

    /// <summary>
    /// The ground as a height field over the map and a margin of sea floor: each hexagon pulls the
    /// ground towards its biome's height, blended with a Gaussian over its neighbours, so land forms
    /// soft plateaus and the coast runs near the hexagons' edges. The rivers cut their beds into it,
    /// level across and never rising downstream. Builds the terrain mesh with vertex colours and
    /// answers height and river queries for props and picking. Each vertex also carries the shares of
    /// the ground textures (<see cref="GroundLayer"/>) that the terrain shader blends.
    /// </summary>
    public sealed class TerrainField
    {
        public const float Step = 0.25f;
        public const float Margin = 8f;
        const float Sigma = 0.65f, ColorSigma = 0.5f, Warp = 0.22f;
        // Out in the open water the floor blends wider and its shelf winds: with the narrow blend
        // the drop from the shallows ran along the hexagons' edges, and the water's depth colour
        // showed straight edges and pale wedges at the corners (review 2026-11-10).
        const float SeaSigma = 0.95f, SeaWarp = 0.55f;

        /// <summary>
        /// How far a hexagon's height and town reach into the ground round it: a sample blends
        /// the hexagons two steps round the one its warped point falls in (√3 a step, and that
        /// one's radius), and the warps move it by up to Warp and SeaWarp (the noise keeps
        /// within ±1); a sample's width more for rounding.
        /// </summary>
        const float Reach = 2f * 2f * HexMetrics.InnerRadius + HexMetrics.Radius + Warp + SeaWarp + Step;

        readonly World world;
        public readonly float OriginX, OriginZ;
        public readonly int SizeX, SizeZ;
        readonly float[] heights;
        readonly Color[] colors;
        readonly float[] layers;

        /// <summary>The ground's heights and colours before the rivers cut their beds, so that they can be cut again.</summary>
        readonly float[] uncutHeights;
        readonly Color[] uncutColors;

        readonly Noise2 warpX, warpZ, detail, bumps, tone;
        readonly float[] hexHeight;

        /// <summary>Each hexagon's own height before its jitter, and the jitter, which a town's cells lose.</summary>
        readonly float[] hexBase, hexJitter;

        /// <summary>What the ground of each cell was worked out for (<see cref="StateOf"/>), to find the cells that changed since.</summary>
        readonly byte[] builtFor;

        /// <summary>A sample whose height changed by more than this has moved what stands on it.</summary>
        const float Moved = 0.002f;
        readonly Color[] hexColor;
        readonly float[] hexLayers;
        readonly float[] cityMask;

        /// <summary>Per sample: distance from the nearest river's water, negative in it.</summary>
        float[] riverEdge;

        public List<RiverCourse> Rivers { get; private set; }

        public TerrainField(World world)
        {
            this.world = world;
            OriginX = world.MinX - Margin;
            OriginZ = world.MinZ - Margin;
            SizeX = Mathf.CeilToInt((world.MaxX + Margin - OriginX) / Step) + 1;
            SizeZ = Mathf.CeilToInt((world.MaxZ + Margin - OriginZ) / Step) + 1;
            heights = new float[SizeX * SizeZ];
            colors = new Color[SizeX * SizeZ];
            layers = new float[SizeX * SizeZ * GroundLayer.Count];
            warpX = new Noise2(world.Seed * 31 + 1);
            warpZ = new Noise2(world.Seed * 31 + 2);
            detail = new Noise2(world.Seed * 31 + 3);
            bumps = new Noise2(world.Seed * 31 + 4);
            tone = new Noise2(world.Seed * 31 + 5);

            // Each hexagon's own height, jittered so plateaus are not all level.
            var rng = new Rng(world.Seed * 31 + 6);
            hexHeight = new float[world.Cells.Length];
            hexBase = new float[world.Cells.Length];
            hexJitter = new float[world.Cells.Length];
            foreach (Cell cell in world.Cells)
            {
                // The jitter is drawn for every cell, so a town founded later levels only its own cells.
                float h = BaseHeight(cell.Biome);
                hexJitter[cell.Index] = rng.Range(-0.03f, 0.03f);
                if (cell.Biome == Biome.Hills)
                    h += rng.Range(-0.06f, 0.08f);
                if (cell.Biome == Biome.Ocean)
                    h -= rng.Range(0f, 0.4f);
                hexBase[cell.Index] = h;
                hexHeight[cell.Index] = HexHeight(cell);
            }
            // Each hexagon's own shade of its biome: wide stretches of greener or drier steppe,
            // yellower or deeper meadows.
            var hue = new Noise2(world.Seed * 31 + 7);
            hexColor = new Color[world.Cells.Length];
            foreach (Cell cell in world.Cells)
            {
                float v = hue.Fbm(world.CenterX(cell.Index) * 0.22f, world.CenterZ(cell.Index) * 0.22f, 2) * 1.8f;
                hexColor[cell.Index] = cell.IsWater ? Palette.Seabed : Palette.Ground(cell.Biome, v);
            }
            hexLayers = new float[world.Cells.Length * GroundLayer.Count];
            foreach (Cell cell in world.Cells)
                BiomeLayers(cell.Biome, hexLayers, cell.Index * GroundLayer.Count);
            cityMask = new float[heights.Length];
            builtFor = new byte[world.Cells.Length];
            foreach (Cell cell in world.Cells)
                builtFor[cell.Index] = StateOf(cell);
            Build(0, SizeX - 1, 0, SizeZ - 1);
            uncutHeights = (float[])heights.Clone();
            uncutColors = (Color[])colors.Clone();
            CarveRivers();
        }

        /// <summary>A hexagon's height: a town's cells keep level, without the jitter.</summary>
        float HexHeight(Cell cell) => hexBase[cell.Index] + (cell.IsLand && cell.Town < 0 ? hexJitter[cell.Index] : 0f);

        /// <summary>What of a cell shapes the ground round it: a town (level, bare earth), a field building's or place's site (level), a monster group (trodden dirt).</summary>
        static byte StateOf(Cell cell) =>
            (byte)((cell.Town >= 0 ? 1 : 0)
                   | (cell.Town < 0 && (cell.Resource != Resource.None || cell.Place != Core.Place.None) ? 2 : 0)
                   | (cell.Monster != Monster.None ? 4 : 0));

        /// <summary>Whether a cell changed since the field was worked out in a way that shapes the ground (<see cref="StateOf"/>).</summary>
        public bool Stale()
        {
            foreach (Cell cell in world.Cells)
            {
                if (StateOf(cell) != builtFor[cell.Index])
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The ground again after the cells changed: a town founded or grown, a group beaten.
        /// A cell reaches only the samples within <see cref="Reach"/> of it, which are worked out
        /// again; the rivers then cut their beds anew over the whole map, as a river's water keeps
        /// level downstream. The field is then the same as a new one for the world as it is now,
        /// at a small part of the cost: the players found the game hanging for seconds when a
        /// town was founded, as the whole map was drawn again (2026-11-11).
        /// </summary>
        /// <returns>The cells where the ground moved, so what stands there must be set on it again.</returns>
        public HashSet<int> Refresh()
        {
            var changed = new List<int>();
            foreach (Cell cell in world.Cells)
            {
                if (StateOf(cell) != builtFor[cell.Index])
                    changed.Add(cell.Index);
            }
            var moved = new HashSet<int>();
            if (changed.Count == 0)
                return moved;
            var before = (float[])heights.Clone();
            System.Array.Copy(uncutHeights, heights, heights.Length);
            System.Array.Copy(uncutColors, colors, colors.Length);
            foreach (int index in changed)
            {
                builtFor[index] = StateOf(world.Cells[index]);
                hexHeight[index] = HexHeight(world.Cells[index]);
            }
            foreach (int index in changed)
            {
                float cx = world.CenterX(index), cz = world.CenterZ(index);
                Build(Mathf.Max(0, Mathf.FloorToInt((cx - Reach - OriginX) / Step)), Mathf.Min(SizeX - 1, Mathf.CeilToInt((cx + Reach - OriginX) / Step)),
                    Mathf.Max(0, Mathf.FloorToInt((cz - Reach - OriginZ) / Step)), Mathf.Min(SizeZ - 1, Mathf.CeilToInt((cz + Reach - OriginZ) / Step)));
            }
            System.Array.Copy(heights, uncutHeights, heights.Length);
            System.Array.Copy(colors, uncutColors, colors.Length);
            CarveRivers();
            for (int s = 0; s < heights.Length; s++)
            {
                if (Mathf.Abs(heights[s] - before[s]) <= Moved)
                    continue;
                Cell cell = world.CellAt(HexMetrics.FromWorld(OriginX + s % SizeX * Step, OriginZ + s / SizeX * Step));
                if (cell != null)
                    moved.Add(cell.Index);
            }
            return moved;
        }

        /// <summary>
        /// The largest difference between this field and <paramref name="other"/> over the heights,
        /// the colours, the texture shares and the rivers' reach: 0 when a refreshed field matches
        /// a new one.
        /// </summary>
        public float Difference(TerrainField other)
        {
            float most = 0f;
            for (int k = 0; k < heights.Length; k++)
            {
                most = Mathf.Max(most, Mathf.Abs(heights[k] - other.heights[k]));
                Color d = colors[k] - other.colors[k];
                most = Mathf.Max(most, Mathf.Max(Mathf.Abs(d.r), Mathf.Max(Mathf.Abs(d.g), Mathf.Abs(d.b))));
                most = Mathf.Max(most, Mathf.Abs(Mathf.Min(riverEdge[k], 9f) - Mathf.Min(other.riverEdge[k], 9f)));
            }
            for (int k = 0; k < layers.Length; k++)
                most = Mathf.Max(most, Mathf.Abs(layers[k] - other.layers[k]));
            return most;
        }

        public static float BaseHeight(Biome biome)
        {
            switch (biome)
            {
                case Biome.Ocean: return -1.4f;
                case Biome.Shallows: return -0.26f;
                case Biome.Beach: return 0.13f;
                case Biome.Meadow: return 0.21f;
                case Biome.Plains: return 0.19f;
                case Biome.Forest: return 0.24f;
                case Biome.Hills: return 0.46f;
                case Biome.Mountains: return 0.7f;
                default: return 0f;
            }
        }

        /// <summary>The ground textures of a biome, as shares written at <paramref name="at"/>.</summary>
        internal static void BiomeLayers(Biome biome, float[] shares, int at)
        {
            switch (biome)
            {
                case Biome.Meadow:
                    shares[at + GroundLayer.Grass] = 1f;
                    break;
                case Biome.Plains:
                    shares[at + GroundLayer.DryGrass] = 1f;
                    break;
                case Biome.Forest:
                    shares[at + GroundLayer.Forest] = 1f;
                    break;
                case Biome.Hills:
                    shares[at + GroundLayer.Grass] = 0.6f;
                    shares[at + GroundLayer.DryGrass] = 0.4f;
                    break;
                case Biome.Mountains:
                    shares[at + GroundLayer.Scree] = 0.7f;
                    shares[at + GroundLayer.Rock] = 0.3f;
                    break;
                default:
                    shares[at + GroundLayer.Sand] = 1f;
                    break;
            }
        }

        /// <summary>Moves a share <paramref name="t"/> of every layer at <paramref name="at"/> over to <paramref name="layer"/>.</summary>
        static void Toward(float[] shares, int at, int layer, float t)
        {
            for (int k = 0; k < GroundLayer.Count; k++)
                shares[at + k] *= 1f - t;
            shares[at + layer] += t;
        }

        float Target(int col, int row) => world.Contains(col, row) ? hexHeight[row * world.Width + col] : -1.6f;

        Color GroundColor(int col, int row, float h)
        {
            if (!world.Contains(col, row))
                return Palette.Seabed;
            return hexColor[row * world.Width + col];
        }

        /// <summary>Works out the ground's samples from column <paramref name="i0"/> to <paramref name="i1"/> and row <paramref name="j0"/> to <paramref name="j1"/>.</summary>
        void Build(int i0, int i1, int j0, int j1)
        {
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                float x = OriginX + i * Step, z = OriginZ + j * Step;
                float open = OpenWater(x, z);
                float wx = x + Warp * warpX.Fbm(x * 0.55f, z * 0.55f, 2) + open * SeaWarp * warpX.Fbm(x * 0.3f + 31.7f, z * 0.3f, 2);
                float wz = z + Warp * warpZ.Fbm(x * 0.55f, z * 0.55f, 2) + open * SeaWarp * warpZ.Fbm(x * 0.3f + 31.7f, z * 0.3f, 2);
                float sigma = Mathf.Lerp(Sigma, SeaSigma, open);
                Hex home = HexMetrics.FromWorld(wx, wz);

                float sumH = 0, sumW = 0, sumCW = 0, sumRiver = 0;
                Color color = Color.black;
                int s = (j * SizeX + i) * GroundLayer.Count;
                System.Array.Clear(layers, s, GroundLayer.Count);
                float near = float.MaxValue;
                int nearest = -1;
                for (int dq = -2; dq <= 2; dq++)
                for (int dr = Math.Max(-2, -dq - 2); dr <= Math.Min(2, -dq + 2); dr++)
                {
                    var hex = new Hex(home.Q + dq, home.R + dr);
                    float cx = HexMetrics.CenterX(hex) - wx, cz = HexMetrics.CenterZ(hex) - wz;
                    float d2 = cx * cx + cz * cz;
                    float w = Mathf.Exp(-d2 / (2 * sigma * sigma));
                    float cw = Mathf.Exp(-d2 / (2 * ColorSigma * ColorSigma));
                    int col = hex.Col, row = hex.Row;
                    sumH += w * Target(col, row);
                    sumW += w;
                    color += cw * GroundColor(col, row, 0);
                    sumCW += cw;
                    if (world.Contains(col, row))
                    {
                        int at = (row * world.Width + col) * GroundLayer.Count;
                        for (int k = 0; k < GroundLayer.Count; k++)
                            layers[s + k] += cw * hexLayers[at + k];
                        if (world.Cells[row * world.Width + col].Rivers != 0)
                            sumRiver += cw;
                    }
                    else
                        layers[s + GroundLayer.Sand] += cw;
                    if (d2 < near && world.Contains(col, row))
                    {
                        near = d2;
                        nearest = row * world.Width + col;
                    }
                }
                float h = sumH / sumW;
                color /= sumCW;
                for (int k = 0; k < GroundLayer.Count; k++)
                    layers[s + k] /= sumCW;

                // Detail: low rolls and gentle swells everywhere on land, bumps on hills, rough rock
                // in mountains, all calmer around cities so the houses stand on level ground. The
                // rolls, a couple of cells across, came in on 2026-11-10, when the players felt
                // bare, empty spaces on the level meadows between the woods and towns: now the land
                // rises and falls a little, and its slopes catch the light. They die away towards
                // the shore, so the coast keeps its line, and near rivers, whose water keeps level
                // downstream and would otherwise cut deep trenches through the rises.
                Cell cell = nearest >= 0 ? world.Cells[nearest] : null;
                float city = 0f;
                if (cell != null && cell.Town >= 0)
                    city = 1f - Mathf.Clamp01((Mathf.Sqrt(near) - 0.45f) / 0.5f);
                cityMask[j * SizeX + i] = city;
                // A building out in the country, a mine, a mill or a shrine, keeps the swells and
                // rolls off its site, so it stands level rather than tilted over a slope.
                float site = 0f;
                if (cell != null && cell.Town < 0 && (cell.Resource != Resource.None || cell.Place != Core.Place.None))
                    site = 1f - Mathf.Clamp01((Mathf.Sqrt(near) - 0.35f) / 0.45f);
                float land = Mathf.Clamp01(h * 8f);
                float inland = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.2f, h));
                float calm = 1f - 0.65f * Mathf.Clamp01(sumRiver / sumCW * 1.5f);
                // The rolls rise much higher than they sink, round rises over broad flat lows, so the
                // low ground between them never comes down to the sand of the shore or loses its
                // grass. (Rolls of a few hundredths either way read as flat from the map's height.)
                float roll = detail.Fbm(x * 0.3f + 51.3f, z * 0.3f - 23.9f, 3);
                roll = (roll + 0.8f * roll * roll) * 0.2f * inland * calm;
                float swell = detail.Fbm(x * 0.8f, z * 0.8f, 3) * Mathf.Lerp(0.05f, 0.075f, inland * calm) + roll;
                float bump = bumps.Fbm(x * 1.4f, z * 1.4f, 3), hillWeight = HillWeight(wx, wz, home);
                float hill = hillWeight * Mathf.Max(0f, bump + 0.15f) * 0.32f;
                // Scree on the tops of the hills' bumps.
                Toward(layers, s, GroundLayer.Scree, hillWeight * (1f - city) * 0.8f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.5f, bump)));
                float rock = MountainWeight(wx, wz, home) * Mathf.Abs(bumps.Fbm(x * 1.1f + 7.3f, z * 1.1f, 3)) * 0.5f;
                h += land * (1f - city) * ((1f - site) * swell + hill + rock);
                if (city > 0f && cell != null)
                    h = Mathf.Lerp(h, Mathf.Max(hexHeight[cell.Index], 0.12f), city * 0.7f);
                heights[j * SizeX + i] = h;

                colors[j * SizeX + i] = Shade(color, h, x, z, cell, Mathf.Sqrt(near), city);
                ShadeLayers(s, h, cell, Mathf.Sqrt(near), city);
            }
        }

        /// <summary>
        /// How far out in the open water a point is: 0 wherever land is near (so the coast keeps
        /// its place), 1 among water cells only. Over the 19 hexagons round the point, so it does
        /// not jump where the point crosses into the next one.
        /// </summary>
        float OpenWater(float x, float z)
        {
            const float Spread = 0.8f;
            Hex home = HexMetrics.FromWorld(x, z);
            float sum = 0, total = 0;
            for (int dq = -2; dq <= 2; dq++)
            for (int dr = Math.Max(-2, -dq - 2); dr <= Math.Min(2, -dq + 2); dr++)
            {
                var hex = new Hex(home.Q + dq, home.R + dr);
                float cx = HexMetrics.CenterX(hex) - x, cz = HexMetrics.CenterZ(hex) - z;
                float w = Mathf.Exp(-(cx * cx + cz * cz) / (2 * Spread * Spread));
                Cell cell = world.CellAt(hex);
                if (cell == null || cell.IsWater)
                    sum += w;
                total += w;
            }
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.97f, sum / total));
        }

        float HillWeight(float x, float z, Hex home) => BiomeWeight(x, z, home, Biome.Hills, 0.8f)
                                                       + 0.5f * BiomeWeight(x, z, home, Biome.Mountains, 0.8f);

        float MountainWeight(float x, float z, Hex home) => BiomeWeight(x, z, home, Biome.Mountains, 0.75f);

        /// <summary>A smooth 0..1 weight of how much the point is inside hexagons of a biome.</summary>
        float BiomeWeight(float x, float z, Hex home, Biome biome, float sigma)
        {
            float sum = 0, total = 0;
            for (int d = -1; d < 6; d++)
            {
                Hex hex = d < 0 ? home : home.Neighbor(d);
                float cx = HexMetrics.CenterX(hex) - x, cz = HexMetrics.CenterZ(hex) - z;
                float w = Mathf.Exp(-(cx * cx + cz * cz) / (2 * sigma * sigma));
                Cell cell = world.CellAt(hex);
                if (cell != null && cell.Biome == biome)
                    sum += w;
                total += w;
            }
            return sum / total;
        }

        Color Shade(Color ground, float h, float x, float z, Cell cell, float distance, float city)
        {
            Color c = ground;
            if (h < 0.02f)
            {
                // Sea floor: pale sand under the waterline, blue further down; the water tints it.
                c = Color.Lerp(Palette.Sand, Palette.Seabed, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, -0.7f, h)));
            }
            else
            {
                // A thin sand rim along every shore, a little wider on beaches.
                float rim = cell != null && cell.Biome == Biome.Beach ? 0.11f : 0.06f;
                c = Color.Lerp(Palette.Sand, c, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, rim, h)));
                // Pale rock showing on high ground.
                if (h > 0.62f)
                    c = Color.Lerp(c, Palette.Rock, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.9f, h)));
            }
            if (cell != null && cell.IsLand)
            {
                if (city > 0f)
                    c = Color.Lerp(c, Palette.Earth, 0.55f * Mathf.Clamp01(city * 1.6f));
                if (cell.Monster != Monster.None)
                    c = Color.Lerp(c, Palette.LairGround, 0.6f * (1f - Mathf.Clamp01((distance - 0.25f) / 0.45f)));
            }
            float n = tone.Fbm(x * 0.45f, z * 0.45f, 3);
            c *= 1f + 0.07f * n;
            c.a = 1f;
            return c;
        }

        /// <summary>The ground textures' side of <see cref="Shade"/>: sand by the shore and under the sea, rock up high, dirt round cities and lairs.</summary>
        void ShadeLayers(int s, float h, Cell cell, float distance, float city)
        {
            float rim = cell != null && cell.Biome == Biome.Beach ? 0.11f : 0.06f;
            Toward(layers, s, GroundLayer.Sand, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, rim, h)));
            Toward(layers, s, GroundLayer.Rock, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.9f, h)));
            if (cell != null && cell.IsLand)
            {
                if (city > 0f)
                    Toward(layers, s, GroundLayer.Dirt, 0.8f * Mathf.Clamp01(city * 1.6f));
                if (cell.Monster != Monster.None)
                    Toward(layers, s, GroundLayer.Dirt, 0.7f * (1f - Mathf.Clamp01((distance - 0.25f) / 0.45f)));
            }
        }

        /// <summary>Depth of the bed below the water, and how far each side the bank slopes back up.</summary>
        const float BedDepth = 0.035f, BedFlat = 0.17f, BankSlope = 0.38f;

        /// <summary>
        /// The water level along every river: the ground under the course, never rising downstream,
        /// just above the sea at a mouth and meeting the bigger river at a confluence. Then the bed
        /// is cut level across, a little below the water, and the banks slope back to the ground.
        /// </summary>
        void CarveRivers()
        {
            Rivers = RiverCourse.FromWorld(world);
            riverEdge = new float[heights.Length];
            var nearest = new float[heights.Length];
            var bed = new float[heights.Length];
            var half = new float[heights.Length];
            for (int k = 0; k < heights.Length; k++)
            {
                nearest[k] = float.MaxValue;
                riverEdge[k] = 99f;
            }
            foreach (RiverCourse river in Rivers)
            {
                int n = river.Points.Count;
                river.Levels = new float[n];
                river.Banks = new float[n];
                float level = float.MaxValue;
                for (int k = 0; k < n; k++)
                {
                    float ground = Height(river.Points[k].x, river.Points[k].y);
                    level = Mathf.Min(level, ground);
                    river.Levels[k] = river.HasMouth ? Mathf.Max(level, 0.012f) : level;
                    // No gravel banks out at sea.
                    river.Banks[k] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.01f, 0.06f, ground));
                }
                if (!river.HasMouth)
                {
                    RiverCourse main = Rivers[river.Joins];
                    Vector2 end = river.Points[n - 1];
                    float joinLevel = main.Levels[main.Nearest(end, out _)];
                    river.Levels[n - 1] = joinLevel;
                    for (int k = n - 2; k >= 0; k--)
                        river.Levels[k] = Mathf.Max(river.Levels[k], river.Levels[k + 1]);
                    // The banks stop where the water meets the bigger river's.
                    for (int k = 0; k < n; k++)
                    {
                        int m = main.Nearest(river.Points[k], out float d);
                        river.Banks[k] *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(main.Widths[m] * 0.5f, main.Widths[m] * 0.5f + 0.25f, d));
                    }
                }

                // The nearest river point of every sample around the course.
                float reach = BedFlat + BankSlope + 0.25f;
                for (int k = 0; k < n; k++)
                {
                    Vector2 p = river.Points[k];
                    float w = river.Widths[k] * 0.5f;
                    float r = w + reach;
                    int i0 = Mathf.Max(0, Mathf.FloorToInt((p.x - r - OriginX) / Step));
                    int i1 = Mathf.Min(SizeX - 1, Mathf.CeilToInt((p.x + r - OriginX) / Step));
                    int j0 = Mathf.Max(0, Mathf.FloorToInt((p.y - r - OriginZ) / Step));
                    int j1 = Mathf.Min(SizeZ - 1, Mathf.CeilToInt((p.y + r - OriginZ) / Step));
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        float dx = OriginX + i * Step - p.x, dz = OriginZ + j * Step - p.y;
                        float d = Mathf.Sqrt(dx * dx + dz * dz);
                        int s = j * SizeX + i;
                        if (d - w >= nearest[s] - half[s])
                            continue;
                        nearest[s] = d;
                        half[s] = w;
                        bed[s] = river.Levels[k] - BedDepth;
                    }
                }
            }

            for (int s = 0; s < heights.Length; s++)
            {
                if (nearest[s] == float.MaxValue)
                    continue;
                float edge = nearest[s] - half[s];
                riverEdge[s] = edge;
                float h = heights[s];
                float carved = Mathf.Lerp(bed[s], h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BedFlat, BedFlat + BankSlope, edge)));
                // Cut into the land, raised only on it: never a ridge across the sea floor.
                heights[s] = h < 0f ? Mathf.Min(h, carved) : carved;
                // Lusher grass along the water.
                float lush = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.75f, edge));
                if (h > 0.05f)
                    colors[s] = Color.Lerp(colors[s], colors[s] * Palette.RiverGrass, lush * 0.6f);
            }
        }

        /// <summary>The river whose course passes nearest to p, and the index of its nearest point.</summary>
        public bool NearestRiver(Vector2 p, out RiverCourse river, out int index)
        {
            river = null;
            index = -1;
            float best = float.MaxValue;
            foreach (RiverCourse r in Rivers)
            {
                int k = r.Nearest(p, out float d);
                if (d < best)
                {
                    best = d;
                    river = r;
                    index = k;
                }
            }
            return river != null;
        }

        /// <summary>How far a point is from the nearest river's water (negative in it; large far away).</summary>
        public float RiverEdge(float x, float z)
        {
            float fx = Mathf.Clamp((x - OriginX) / Step, 0, SizeX - 1.001f);
            float fz = Mathf.Clamp((z - OriginZ) / Step, 0, SizeZ - 1.001f);
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            float a = riverEdge[j * SizeX + i], b = riverEdge[j * SizeX + i + 1];
            float c = riverEdge[(j + 1) * SizeX + i], d = riverEdge[(j + 1) * SizeX + i + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        /// <summary>The ground height at a point, bilinear between samples.</summary>
        public float Height(float x, float z)
        {
            float fx = Mathf.Clamp((x - OriginX) / Step, 0, SizeX - 1.001f);
            float fz = Mathf.Clamp((z - OriginZ) / Step, 0, SizeZ - 1.001f);
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            float a = heights[j * SizeX + i], b = heights[j * SizeX + i + 1];
            float c = heights[(j + 1) * SizeX + i], d = heights[(j + 1) * SizeX + i + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        /// <summary>The ground's normal at a point, from neighbouring samples.</summary>
        public Vector3 Normal(float x, float z)
        {
            float hx = Height(x + Step, z) - Height(x - Step, z);
            float hz = Height(x, z + Step) - Height(x, z - Step);
            return new Vector3(-hx, 2 * Step, -hz).normalized;
        }

        public Mesh BuildMesh()
        {
            int count = SizeX * SizeZ;
            var positions = new Vector3[count];
            var normals = new Vector3[count];
            var linear = new Color[count];
            var sharesA = new Vector4[count];
            var sharesB = new Vector4[count];
            for (int j = 0; j < SizeZ; j++)
            for (int i = 0; i < SizeX; i++)
            {
                int k = j * SizeX + i;
                float x = OriginX + i * Step, z = OriginZ + j * Step;
                positions[k] = new Vector3(x, heights[k], z);
                float hl = heights[j * SizeX + Math.Max(i - 1, 0)], hr = heights[j * SizeX + Math.Min(i + 1, SizeX - 1)];
                float hd = heights[Math.Max(j - 1, 0) * SizeX + i], hu = heights[Math.Min(j + 1, SizeZ - 1) * SizeX + i];
                normals[k] = new Vector3(hl - hr, 2 * Step, hd - hu).normalized;
                linear[k] = colors[k].linear;
                int s = k * GroundLayer.Count;
                sharesA[k] = new Vector4(layers[s], layers[s + 1], layers[s + 2], layers[s + 3]);
                sharesB[k] = new Vector4(layers[s + 4], layers[s + 5], layers[s + 6], layers[s + 7]);
            }
            var indices = new int[(SizeX - 1) * (SizeZ - 1) * 6];
            int t = 0;
            for (int j = 0; j < SizeZ - 1; j++)
            for (int i = 0; i < SizeX - 1; i++)
            {
                int a = j * SizeX + i, b = a + 1, c = a + SizeX, d = c + 1;
                // Split each quad along the diagonal that follows the ground better.
                if (Mathf.Abs(heights[a] - heights[d]) < Mathf.Abs(heights[b] - heights[c]))
                {
                    indices[t++] = a; indices[t++] = c; indices[t++] = d;
                    indices[t++] = a; indices[t++] = d; indices[t++] = b;
                }
                else
                {
                    indices[t++] = a; indices[t++] = c; indices[t++] = b;
                    indices[t++] = b; indices[t++] = c; indices[t++] = d;
                }
            }
            var mesh = new Mesh { name = "Terrain", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetColors(linear);
            mesh.SetUVs(1, sharesA);
            mesh.SetUVs(2, sharesB);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Where a ray first meets the ground or the sea surface (y = 0).</summary>
        public bool Raycast(Ray ray, out Vector3 point)
        {
            point = default;
            if (ray.direction.y > -0.01f)
                return false;
            float y = 0.3f;
            for (int k = 0; k < 6; k++)
            {
                float t = (y - ray.origin.y) / ray.direction.y;
                point = ray.origin + ray.direction * t;
                y = Mathf.Max(Height(point.x, point.z), 0f);
            }
            point.y = y;
            return true;
        }
    }
}

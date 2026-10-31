using System;
using System.Collections.Generic;
using System.Threading;

namespace Procedural.Generation
{
    /// <summary>Inputs of the vegetation stage, all at heightmap resolution.</summary>
    internal sealed class VegetationInput
    {
        internal Grid Height, Moisture;
        internal Grid[] Surface;
        /// <summary>Sea, and open water: lakes and river channels.</summary>
        internal bool[] Ocean, Water;
        /// <summary>Metres to river channels and lakes; 0 inside them.</summary>
        internal float[] WaterDistance;
        internal int Seed;
        internal float Peak, ForestCover;
    }

    internal sealed class VegetationMetrics
    {
        internal float ForestCoverTarget, ForestCover, CanopyFactor;
        internal int CalibrationPasses;
        internal int[] Counts;
        internal double[] SuitableHectares, TargetPerHectare;
        internal int[] Kept, Rejected;
        internal double MoistureUnderCanopy, MoistureOpen;
    }

    internal sealed class VegetationResult
    {
        internal SpeciesSpec[] Species;
        internal readonly List<PlantInstance> Plants = new List<PlantInstance>();
        internal Grid[] DetailCoverage;
        internal readonly VegetationMetrics Metrics = new VegetationMetrics();
        internal readonly List<Invariant> Invariants = new List<Invariant>();
        internal readonly List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// Suitability maps, density and layered spaced placement (DESIGN.md "9. Vegetation"):
    /// canopy calibrated to the recipe's forest cover, snags and logs from the
    /// forest, understory, rocks, then grass and reed coverage maps.
    /// </summary>
    internal static class VegetationStage
    {
        private const float CanopySpacing = 2, UnderstorySpacing = 2, RockSpacing = 4, StoneSpacing = 2;
        private const float CoverCell = 2, BucketSize = 4;
        private const float CoverTolerance = 0.03f;
        private const int MaxPasses = 6;
        private const float SnagShare = 0.06f, LogsPerTree = 0.05f;
        private const float MaxAcceptance = 0.6f;
        // The log placeholder and Studio logs: 6 m long, 0.3 m radius at scale 1.
        private const float LogHalfLength = 3, LogRadius = 0.3f;

        internal static VegetationResult Place(VegetationInput input, SpeciesSpec[] species, CancellationToken cancel)
        {
            var result = new VegetationResult { Species = species };
            var fields = new Fields(input, cancel);
            var hash = new PlantHash(fields.WorldSize, species, result.Plants);
            var metrics = result.Metrics;
            metrics.Counts = new int[species.Length];
            metrics.Kept = new int[species.Length];
            metrics.Rejected = new int[species.Length];
            metrics.ForestCoverTarget = input.ForestCover;
            ulong seed = MathCore.Stage(input.Seed, "Vegetation");

            int[] canopy = Indices(species, s => s.Layer == PlantLayer.Canopy && s.MaxDensity > 0 && !s.Detail);
            var cover = new CoverRaster(fields, cancel);
            float factor = 1;
            for (int pass = 1; pass <= MaxPasses; pass++)
            {
                cancel.ThrowIfCancellationRequested();
                result.Plants.Clear();
                hash.Clear();
                Array.Clear(metrics.Kept, 0, metrics.Kept.Length);
                Array.Clear(metrics.Rejected, 0, metrics.Rejected.Length);
                Sample(fields, species, canopy, CanopySpacing, factor, seed ^ 0x1001, hash, result, cancel);
                // Snags have no crowns: convert them within the pass so cover is measured on the final canopy.
                cover.Rasterize(result.Plants, species, cancel);
                ConvertSnags(fields, species, seed ^ 0x2002, cover.Smoothed(7, cancel), cover, result, cancel);
                metrics.ForestCover = cover.Rasterize(result.Plants, species, cancel);
                metrics.CalibrationPasses = pass;
                metrics.CanopyFactor = factor;
                if (Math.Abs(metrics.ForestCover - input.ForestCover) <= CoverTolerance || pass == MaxPasses)
                    break;
                factor *= MathCore.Clamp(input.ForestCover / Math.Max(metrics.ForestCover, 0.01f), 0.5f, 2.5f);
            }

            float[] forest = cover.Smoothed(7, cancel);   // about 15 m: inside a forest or not
            PlaceLogs(fields, species, seed ^ 0x5005, forest, cover, hash, result, cancel);
            Sample(fields, species, Indices(species, s => s.Layer == PlantLayer.Understory && s.MaxDensity > 0), UnderstorySpacing, 1, seed ^ 0x3003, hash, result, cancel);
            Sample(fields, species, Indices(species, s => s.Layer == PlantLayer.Prop && s.MaxDensity > 0 && s.Footprint >= 1), RockSpacing, 1, seed ^ 0x4004, hash, result, cancel);
            Sample(fields, species, Indices(species, s => s.Layer == PlantLayer.Prop && s.MaxDensity > 0 && s.Footprint < 1), StoneSpacing, 1, seed ^ 0x6006, hash, result, cancel);
            result.DetailCoverage = Details(fields, species, cover.Smoothed(2, cancel), cover, seed, cancel);

            foreach (PlantInstance p in result.Plants)
                metrics.Counts[p.Species]++;
            Measure(fields, species, cover, seed, result, cancel);
            Validate(fields, species, hash, result, cancel);
            return result;
        }

        private static int[] Indices(SpeciesSpec[] species, Func<SpeciesSpec, bool> keep)
        {
            var list = new List<int>();
            for (int k = 0; k < species.Length; k++)
                if (keep(species[k]))
                    list.Add(k);
            return list.ToArray();
        }

        /// <summary>One jittered candidate per spacing cell; kept with probability Σ density × cell area.</summary>
        private static void Sample(Fields f, SpeciesSpec[] species, int[] layer, float spacing, float factor, ulong seed, PlantHash hash, VegetationResult result, CancellationToken cancel)
        {
            if (layer.Length == 0)
                return;
            int cells = (int)(f.WorldSize / spacing);
            var density = new float[layer.Length];
            var fit = new float[layer.Length];
            for (int cz = 0; cz < cells; cz++)
                for (int cx = 0; cx < cells; cx++)
                {
                    int cell = cz * cells + cx;
                    MathCore.Check(cancel, cell);
                    ulong state = MathCore.Mix(seed ^ unchecked((ulong)cell * 0x9E3779B97F4A7C15UL));
                    float x = (cx + MathCore.Random(ref state)) * spacing;
                    float z = (cz + MathCore.Random(ref state)) * spacing;
                    float keep = MathCore.Random(ref state), pick = MathCore.Random(ref state);
                    float grow = MathCore.Random(ref state), yaw = MathCore.Random(ref state);
                    if (!f.OnLand(x, z))
                        continue;
                    float total = 0;
                    for (int k = 0; k < layer.Length; k++)
                    {
                        SpeciesSpec s = species[layer[k]];
                        fit[k] = f.Suitability(s, x, z);
                        density[k] = fit[k] <= 0 ? 0 : fit[k] * s.MaxDensity * f.Cluster(s, layer[k], x, z);
                        total += density[k];
                    }

                    // Calibration scales acceptance only; species are picked from unscaled densities.
                    // Capped acceptance keeps even a grove's heart from packing into a wall of trees.
                    if (total <= 0 || keep >= Math.Min(MaxAcceptance, total * spacing * spacing * factor))
                        continue;
                    int chosen = 0;
                    float acc = density[0];
                    while (chosen < layer.Length - 1 && pick * total >= acc)
                        acc += density[++chosen];

                    int index = layer[chosen];
                    SpeciesSpec spec = species[index];
                    var plant = new PlantInstance
                    {
                        Species = index,
                        X = x,
                        Z = z,
                        Scale = spec.ScaleMin + (spec.ScaleMax - spec.ScaleMin) * MathCore.Clamp(fit[chosen] * 0.7f + grow * 0.3f, 0, 1),
                        Yaw = yaw * 2 * (float)Math.PI
                    };
                    plant.Y = f.BaseHeight(spec, x, z, plant.Scale);
                    if (spec.AlignToSlope)
                        f.AlignToSlope(ref plant, x, z, (grow - 0.5f) * 0.35f, (keep - 0.5f) * 0.35f);
                    if (hash.Fits(plant))
                    {
                        hash.Add(plant);
                        result.Metrics.Kept[index]++;
                    }
                    else
                        result.Metrics.Rejected[index]++;
                }
        }

        /// <summary>Snags replace some pines and broadleaves inside forest; they are smaller, so spacing still holds.</summary>
        private static void ConvertSnags(Fields f, SpeciesSpec[] species, ulong seed, float[] forest, CoverRaster cover, VegetationResult result, CancellationToken cancel)
        {
            int snag = Array.FindIndex(species, s => s.Id == SpeciesTable.Snag);
            if (snag < 0)
                return;
            for (int i = 0; i < result.Plants.Count; i++)
            {
                MathCore.Check(cancel, i);
                PlantInstance tree = result.Plants[i];
                string id = species[tree.Species].Id;
                if (id != SpeciesTable.Pine && id != SpeciesTable.Broadleaf)
                    continue;
                ulong state = MathCore.Mix(seed ^ unchecked((ulong)i * 0xD1B54A32D192ED03UL));
                if (cover.SampleField(forest, tree.X, tree.Z) > 0.5f && MathCore.Random(ref state) < SnagShare)
                {
                    tree.Species = snag;
                    tree.Y = f.BaseHeight(species[snag], tree.X, tree.Z, tree.Scale);
                    result.Plants[i] = tree;
                }
            }
        }

        /// <summary>Logs lie near forest trees, along the contour, pitched with the slope.</summary>
        private static void PlaceLogs(Fields f, SpeciesSpec[] species, ulong seed, float[] forest, CoverRaster cover, PlantHash hash, VegetationResult result, CancellationToken cancel)
        {
            int log = Array.FindIndex(species, s => s.Id == SpeciesTable.Log);
            if (log < 0)
                return;
            SpeciesSpec logSpec = species[log];
            int trees = result.Plants.Count;
            for (int i = 0; i < trees; i++)
            {
                MathCore.Check(cancel, i);
                PlantInstance tree = result.Plants[i];
                SpeciesSpec spec = species[tree.Species];
                if (spec.Id != SpeciesTable.Pine && spec.Id != SpeciesTable.Broadleaf && spec.Id != SpeciesTable.Alder)
                    continue;
                ulong state = MathCore.Mix(seed ^ unchecked((ulong)i * 0xD1B54A32D192ED03UL));
                if (cover.SampleField(forest, tree.X, tree.Z) <= 0.4f || MathCore.Random(ref state) >= LogsPerTree)
                    continue;
                float angle = MathCore.Random(ref state) * 2 * (float)Math.PI;
                float distance = (0.6f + 0.8f * MathCore.Random(ref state)) * Math.Max(spec.CrownRadius, 1) * tree.Scale;
                float x = tree.X + distance * (float)Math.Cos(angle), z = tree.Z + distance * (float)Math.Sin(angle);
                if (!f.OnLand(x, z) || f.Suitability(logSpec, x, z) <= 0)
                    continue;
                f.Gradient(x, z, out float gx, out float gz);
                // Along the contour, turned a little; Unity yaw maps local +x to (cos, 0, -sin).
                double contour = Math.Atan2(gx, -gz) + (MathCore.Random(ref state) - 0.5f) * 0.6f;
                float ax = (float)Math.Cos(contour), az = (float)Math.Sin(contour);
                var plant = new PlantInstance
                {
                    Species = log,
                    X = x,
                    Z = z,
                    Y = f.HeightAt(x, z),
                    Scale = logSpec.ScaleMin + (logSpec.ScaleMax - logSpec.ScaleMin) * MathCore.Random(ref state),
                    Yaw = (float)Math.Atan2(-az, ax),
                    Tilt = (float)Math.Atan(gx * ax + gz * az)
                };
                if (hash.Fits(plant))
                {
                    hash.Add(plant);
                    result.Metrics.Kept[log]++;
                }
                else
                    result.Metrics.Rejected[log]++;
            }
        }

        private static Grid[] Details(Fields f, SpeciesSpec[] species, float[] canopy, CoverRaster cover, ulong seed, CancellationToken cancel)
        {
            int[] detail = Indices(species, s => s.Detail);
            var maps = new Grid[detail.Length];
            Grid h = f.Input.Height;
            for (int k = 0; k < detail.Length; k++)
            {
                SpeciesSpec s = species[detail[k]];
                var map = maps[k] = new Grid(h.Width, h.Height, h.CellSize);
                bool shaded = s.Layer == PlantLayer.Ground && s.Id == SpeciesTable.Grass;
                for (int i = 0; i < map.Data.Length; i++)
                {
                    MathCore.Check(cancel, i);
                    if (f.Input.Ocean[i] || f.Input.Water[i])
                        continue;
                    float x = i % h.Width * h.CellSize, z = i / h.Width * h.CellSize;
                    float v = f.Suitability(s, x, z);
                    if (v <= 0)
                        continue;
                    v *= s.MaxDensity * f.Cluster(s, detail[k], x, z);
                    // Grass thins under canopy; reeds stand in the open by water anyway.
                    if (shaded)
                        v *= 1 - 0.85f * cover.SampleField(canopy, x, z);
                    map.Data[i] = MathCore.Clamp(v, 0, 1);
                }
            }

            return maps;
        }

        private static void Measure(Fields f, SpeciesSpec[] species, CoverRaster cover, ulong seed, VegetationResult result, CancellationToken cancel)
        {
            VegetationMetrics m = result.Metrics;
            m.SuitableHectares = new double[species.Length];
            m.TargetPerHectare = new double[species.Length];
            var densitySum = new double[species.Length];
            Grid h = f.Input.Height;
            const int stride = 2;
            double sampleArea = stride * stride * (double)h.CellSize * h.CellSize;
            double wetUnder = 0, under = 0, wetOpen = 0, open = 0;
            float[] canopy = cover.Smoothed(1, cancel);
            for (int z = 0; z < h.Height; z += stride)
                for (int x = 0; x < h.Width; x += stride)
                {
                    int i = z * h.Width + x;
                    MathCore.Check(cancel, i);
                    float wx = x * h.CellSize, wz = z * h.CellSize;
                    if (!f.OnLand(wx, wz))
                        continue;
                    float c = cover.SampleField(canopy, wx, wz), wet = f.Input.Moisture.Data[i];
                    wetUnder += c * wet;
                    under += c;
                    wetOpen += (1 - c) * wet;
                    open += 1 - c;
                    for (int k = 0; k < species.Length; k++)
                    {
                        SpeciesSpec s = species[k];
                        if (s.Detail || s.MaxDensity <= 0)
                            continue;
                        float v = f.Suitability(s, wx, wz);
                        if (v <= 0.25f)
                            continue;
                        m.SuitableHectares[k] += sampleArea / 10000;
                        densitySum[k] += v * s.MaxDensity * f.Cluster(s, k, wx, wz) * sampleArea;
                    }
                }

            for (int k = 0; k < species.Length; k++)
                m.TargetPerHectare[k] = m.SuitableHectares[k] > 0 ? densitySum[k] / m.SuitableHectares[k] : 0;
            m.MoistureUnderCanopy = under > 0 ? wetUnder / under : double.NaN;
            m.MoistureOpen = open > 0 ? wetOpen / open : double.NaN;
            if (Math.Abs(m.ForestCover - m.ForestCoverTarget) > CoverTolerance)
                result.Warnings.Add("Forest cover " + m.ForestCover.ToString("P1") + " misses the recipe's " + m.ForestCoverTarget.ToString("P1") + " by more than 3 points; suitable land or spacing limits it.");
            for (int k = 0; k < species.Length; k++)
                if (!species[k].Detail && species[k].MaxDensity > 0 && m.SuitableHectares[k] >= 0.5 && m.Counts[k] == 0)
                    result.Warnings.Add("Species " + species[k].Id + " has " + m.SuitableHectares[k].ToString("F1") + " ha of suitable land but no instances.");
        }

        private static void Validate(Fields f, SpeciesSpec[] species, PlantHash hash, VegetationResult result, CancellationToken cancel)
        {
            int wet = 0, steep = 0, spacing = 0, scale = 0, height = 0, floating = 0;
            for (int i = 0; i < result.Plants.Count; i++)
            {
                MathCore.Check(cancel, i);
                PlantInstance p = result.Plants[i];
                SpeciesSpec s = species[p.Species];
                if (!f.OnLand(p.X, p.Z))
                    wet++;
                // Bilinear slope between samples can exceed the sampled limit slightly.
                if (s.Slope.Max < 90 && f.TriangleSlopeAt(p.X, p.Z) > s.Slope.Max + 0.01f)
                    steep++;
                if (p.Scale < s.ScaleMin - 1e-4f || p.Scale > s.ScaleMax + 1e-4f)
                    scale++;
                if (s.RootDepth > 0)
                {
                    // Checked on a finer circle than BaseHeight samples; the root depth covers the gaps.
                    float r = 2 * s.TrunkRadius * p.Scale, centre = f.HeightAt(p.X, p.Z);
                    for (int k = 0; k < 16; k++)
                    {
                        double a = k * Math.PI / 8;
                        if (p.Y > f.HeightAt(p.X + r * (float)Math.Cos(a), p.Z + r * (float)Math.Sin(a)))
                        {
                            floating++;
                            break;
                        }
                    }
                    if (centre - p.Y > s.RootDepth * p.Scale + 2 * r)
                        height++;
                }
                else if (Math.Abs(p.Y + s.Bury * s.Footprint * p.Scale - f.HeightAt(p.X, p.Z)) > 1e-3f)
                    height++;
                spacing += hash.Violations(i);
            }

            bool coverage = true;
            foreach (Grid map in result.DetailCoverage)
                foreach (float v in map.Data)
                    coverage &= v >= 0 && v <= 1;
            Add(result, "plants out of water", wet == 0, wet + " instances in the sea, a lake, a river channel or within 0.5 m of one.");
            Add(result, "plant slopes within species limits", steep == 0, steep + " instances on a terrain triangle steeper than their species' slope limit.");
            Add(result, "plant spacing", spacing == 0, spacing + " pairs closer than their spacing rule (0.9 × summed footprints; trunk clearance under canopy).");
            Add(result, "plant scales in range", scale == 0, scale + " instances outside their species' scale range.");
            Add(result, "plants on the terrain", height == 0, height + " instances off the terrain: props by more than 1 mm from their burial depth, rooted plants sunk deeper than their roots and flare allow.");
            Add(result, "rooted plants do not float", floating == 0, floating + " upright plants with ground below their base somewhere on the root flare.");
            Add(result, "detail coverage in 0..1", coverage, "Every detail map value is in 0..1.");
        }

        private static void Add(VegetationResult result, string name, bool passed, string detail)
        {
            result.Invariants.Add(new Invariant { Name = name, Passed = passed, Detail = detail });
        }

        internal static void Serialize(TinyJson j, VegetationResult v)
        {
            VegetationMetrics m = v.Metrics;
            j.Name("vegetation");
            j.Obj();
            j.P("forestCoverRequested", m.ForestCoverTarget);
            j.P("forestCoverAchieved", m.ForestCover);
            j.P("calibrationPasses", m.CalibrationPasses);
            j.P("canopyDensityFactor", m.CanopyFactor);
            j.P("moistureUnderCanopy", m.MoistureUnderCanopy);
            j.P("moistureOpen", m.MoistureOpen);
            j.Name("species");
            j.Arr();
            for (int k = 0; k < v.Species.Length; k++)
            {
                SpeciesSpec s = v.Species[k];
                j.Obj();
                j.P("id", s.Id);
                j.P("count", m.Counts[k]);
                j.P("suitableHectares", m.SuitableHectares[k]);
                j.P("targetPerHectare", m.TargetPerHectare[k]);
                j.P("achievedPerSuitableHectare", m.SuitableHectares[k] > 0 ? m.Counts[k] / m.SuitableHectares[k] : 0);
                int tried = m.Kept[k] + m.Rejected[k];
                j.P("rejectedBySpacing", tried > 0 ? m.Rejected[k] / (double)tried : 0);
                j.EndObj();
            }

            j.EndArr();
            j.EndObj();
        }

        /// <summary>Terrain fields sampled at world positions (metres).</summary>
        private sealed class Fields
        {
            internal readonly VegetationInput Input;
            internal readonly float WorldSize;
            private readonly Grid slope, seaDistance, waterDistance, rockNearby;
            private readonly ulong clusterSeed;

            internal Fields(VegetationInput input, CancellationToken cancel)
            {
                Input = input;
                Grid h = input.Height;
                WorldSize = (h.Width - 1) * h.CellSize;
                slope = new Grid(h.Width, h.Height, h.CellSize);
                seaDistance = new Grid(h.Width, h.Height, h.CellSize);
                waterDistance = new Grid(h.Width, h.Height, h.CellSize);
                float[] sea = EuclideanDistance.Transform(input.Ocean, h.Width, h.Height, cancel);
                for (int i = 0; i < h.Data.Length; i++)
                {
                    MathCore.Check(cancel, i);
                    slope.Data[i] = (float)(Math.Atan(SteepestSlope(h, i)) * 180 / Math.PI);
                    seaDistance.Data[i] = float.IsInfinity(sea[i]) ? 1e6f : sea[i] * h.CellSize;
                    waterDistance.Data[i] = float.IsInfinity(input.WaterDistance[i]) ? 1e6f : input.WaterDistance[i];
                }

                clusterSeed = MathCore.Stage(input.Seed, "VegetationClusters");
                rockNearby = new Grid(h.Width, h.Height, h.CellSize);
                float[] near = TerrainStages.Blur(input.Surface[SurfaceLayer.Rock].Data, h.Width, h.Height, Math.Max(1, (int)Math.Round(4 / h.CellSize)), 2, cancel);
                Array.Copy(near, rockNearby.Data, near.Length);
            }

            /// <summary>
            /// Tilts a prop onto the terrain normal plus jitter (radians), for its yaw:
            /// Euler(0, Yaw, 0) · Euler(Roll, 0, Tilt) maps local up onto the normal.
            /// </summary>
            internal void AlignToSlope(ref PlantInstance p, float x, float z, float jitterTilt, float jitterRoll)
            {
                Gradient(x, z, out float gx, out float gz);
                float nx = -gx, ny = 1, nz = -gz, length = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                nx /= length;
                ny /= length;
                nz /= length;
                float c = (float)Math.Cos(p.Yaw), s = (float)Math.Sin(p.Yaw);
                // The normal in the yawed frame: local x = (c, 0, -s), local z = (s, 0, c).
                float lx = nx * c - nz * s, lz = nx * s + nz * c;
                // Euler(Roll, 0, Tilt) turns up into (-sin T, cos T cos R, cos T sin R): Unity applies z, then x.
                p.Tilt = (float)Math.Asin(MathCore.Clamp(-lx, -1, 1)) + jitterTilt;
                p.Roll = (float)Math.Atan2(lz, ny) + jitterRoll;
            }

            internal float HeightAt(float x, float z) => MathCore.Sample(Input.Height, x / Input.Height.CellSize, z / Input.Height.CellSize);

            // Unit directions around the root flare (no trigonometry: identical on every runtime).
            private static readonly float[] FlareX = { 1, 0.70710678f, 0, -0.70710678f, -1, -0.70710678f, 0, 0.70710678f };
            private static readonly float[] FlareZ = { 0, 0.70710678f, 1, 0.70710678f, 0, -0.70710678f, -1, -0.70710678f };

            /// <summary>
            /// Pivot height: props sink Bury of their footprint below the ground at their centre;
            /// rooted plants stand RootDepth below the lowest ground on their root flare.
            /// </summary>
            internal float BaseHeight(SpeciesSpec s, float x, float z, float scale)
            {
                float h = HeightAt(x, z);
                if (s.RootDepth <= 0)
                    return h - s.Bury * s.Footprint * scale;
                float r = 2 * s.TrunkRadius * scale;
                for (int k = 0; k < FlareX.Length; k++)
                    h = Math.Min(h, HeightAt(x + r * FlareX[k], z + r * FlareZ[k]));
                return h - s.RootDepth * scale;
            }

            /// <summary>
            /// Slope in degrees used for suitability: the steepest corner of the containing
            /// cell, so no terrain triangle under the point is steeper.
            /// </summary>
            internal float SlopeAt(float x, float z)
            {
                int x0 = Math.Max(0, Math.Min(slope.Width - 2, (int)Math.Floor(x / slope.CellSize)));
                int z0 = Math.Max(0, Math.Min(slope.Height - 2, (int)Math.Floor(z / slope.CellSize)));
                return Math.Max(Math.Max(slope[x0, z0], slope[x0 + 1, z0]), Math.Max(slope[x0, z0 + 1], slope[x0 + 1, z0 + 1]));
            }

            /// <summary>
            /// Gradient from the larger one-sided difference on each axis: central differences
            /// cancel on alternating heights and would call a 60° comb flat.
            /// </summary>
            private static double SteepestSlope(Grid h, int i)
            {
                int x = i % h.Width, z = i / h.Width;
                float c = h.Data[i];
                double gx = Math.Max(x + 1 < h.Width ? Math.Abs(h[x + 1, z] - c) : 0, x > 0 ? Math.Abs(c - h[x - 1, z]) : 0) / h.CellSize;
                double gz = Math.Max(z + 1 < h.Height ? Math.Abs(h[x, z + 1] - c) : 0, z > 0 ? Math.Abs(c - h[x, z - 1]) : 0) / h.CellSize;
                return Math.Sqrt(gx * gx + gz * gz);
            }

            /// <summary>
            /// Dry land: above sea level, no corner of the cell in the sea, a lake or a channel,
            /// and at least 0.5 m from water. Distance to water is 1-Lipschitz, so each corner's
            /// distance minus its distance to the point bounds it from below.
            /// </summary>
            internal bool OnLand(float x, float z)
            {
                Grid h = Input.Height;
                float gx = x / h.CellSize, gz = z / h.CellSize;
                int x0 = (int)Math.Floor(gx), z0 = (int)Math.Floor(gz);
                if (x0 < 0 || z0 < 0 || x0 + 1 >= h.Width || z0 + 1 >= h.Height || MathCore.Sample(h, gx, gz) <= 0.05f)
                    return false;
                float lower = float.MinValue;
                for (int cz = z0; cz <= z0 + 1; cz++)
                    for (int cx = x0; cx <= x0 + 1; cx++)
                    {
                        int i = cz * h.Width + cx;
                        if (Input.Ocean[i] || Input.Water[i])
                            return false;
                        float away = h.CellSize * (float)Math.Sqrt((gx - cx) * (gx - cx) + (gz - cz) * (gz - cz));
                        lower = Math.Max(lower, waterDistance.Data[i] - away);
                    }
                return lower >= 0.5f;
            }

            /// <summary>Steepest of the four triangles a cell can be split into, in degrees; an independent check.</summary>
            internal float TriangleSlopeAt(float x, float z)
            {
                Grid h = Input.Height;
                int x0 = Math.Max(0, Math.Min(h.Width - 2, (int)Math.Floor(x / h.CellSize)));
                int z0 = Math.Max(0, Math.Min(h.Height - 2, (int)Math.Floor(z / h.CellSize)));
                float a = h[x0, z0], b = h[x0 + 1, z0], c = h[x0, z0 + 1], d = h[x0 + 1, z0 + 1];
                float steepest = Math.Max(Math.Max(Leg(b - a, c - a), Leg(d - c, d - b)), Math.Max(Leg(b - a, d - b), Leg(d - c, c - a)));
                return (float)(Math.Atan(steepest / h.CellSize) * 180 / Math.PI);
            }

            private static float Leg(float dx, float dz) => (float)Math.Sqrt(dx * dx + dz * dz);

            internal void Gradient(float x, float z, out float gx, out float gz)
            {
                const float d = 1;
                gx = (HeightAt(x + d, z) - HeightAt(x - d, z)) / (2 * d);
                gz = (HeightAt(x, z + d) - HeightAt(x, z - d)) / (2 * d);
            }

            internal float Suitability(SpeciesSpec s, float x, float z)
            {
                Grid h = Input.Height;
                float gx = x / h.CellSize, gz = z / h.CellSize;
                float v = s.Elevation.Evaluate(MathCore.Sample(h, gx, gz));
                if (v <= 0)
                    return 0;
                v *= s.Slope.Evaluate(SlopeAt(x, z));
                if (v <= 0)
                    return 0;
                v *= s.Moisture.Evaluate(MathCore.Sample(Input.Moisture, gx, gz));
                if (v <= 0)
                    return 0;
                v *= s.WaterDistance.Evaluate(MathCore.Sample(waterDistance, gx, gz));
                if (v <= 0)
                    return 0;
                v *= s.SeaDistance.Evaluate(MathCore.Sample(seaDistance, gx, gz));
                if (v <= 0)
                    return 0;
                v *= s.RockNearby.Evaluate(MathCore.Sample(rockNearby, gx, gz));
                if (v <= 0)
                    return 0;
                float gate = 0;
                for (int l = 0; l < SurfaceLayer.Count; l++)
                    gate += s.Surface[l] * MathCore.Sample(Input.Surface[l], gx, gz);
                return v * Math.Min(1, gate);
            }

            /// <summary>
            /// Groves and clearings: low-frequency noise remapped to 0..1 for trees, so clearings
            /// are really open, and to 0.2..1 for everything else, so ground cover has no bald patches.
            /// </summary>
            internal float Cluster(SpeciesSpec s, int index, float x, float z)
            {
                float n = MathCore.Fbm(x / s.ClusterScale, z / s.ClusterScale, clusterSeed + (ulong)index * 7919UL, 3);
                float floor = s.Layer == PlantLayer.Canopy ? 0f : 0.2f;
                return floor + (1 - floor) * MathCore.Smooth(0.3f, 0.65f, n);
            }
        }

        /// <summary>Canopy cover on a 2 m raster of dry land.</summary>
        private sealed class CoverRaster
        {
            private readonly int size;
            private readonly bool[] land;
            private readonly float[] covered;
            private readonly int landCount;

            internal CoverRaster(Fields f, CancellationToken cancel)
            {
                size = Math.Max(2, (int)Math.Ceiling(f.WorldSize / CoverCell));
                land = new bool[size * size];
                covered = new float[size * size];
                for (int i = 0; i < land.Length; i++)
                {
                    MathCore.Check(cancel, i);
                    land[i] = f.OnLand((i % size + 0.5f) * CoverCell, (i / size + 0.5f) * CoverCell);
                    if (land[i])
                        landCount++;
                }
            }

            internal float Rasterize(List<PlantInstance> plants, SpeciesSpec[] species, CancellationToken cancel)
            {
                Array.Clear(covered, 0, covered.Length);
                for (int k = 0; k < plants.Count; k++)
                {
                    MathCore.Check(cancel, k);
                    PlantInstance p = plants[k];
                    float r = species[p.Species].CrownRadius * p.Scale;
                    if (r <= 0)
                        continue;
                    int x0 = Math.Max(0, (int)((p.X - r) / CoverCell)), x1 = Math.Min(size - 1, (int)((p.X + r) / CoverCell));
                    int z0 = Math.Max(0, (int)((p.Z - r) / CoverCell)), z1 = Math.Min(size - 1, (int)((p.Z + r) / CoverCell));
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                        {
                            float dx = (x + 0.5f) * CoverCell - p.X, dz = (z + 0.5f) * CoverCell - p.Z;
                            if (dx * dx + dz * dz <= r * r)
                                covered[z * size + x] = 1;
                        }
                }

                int count = 0;
                for (int i = 0; i < covered.Length; i++)
                    if (land[i] && covered[i] > 0)
                        count++;
                return landCount > 0 ? count / (float)landCount : 0;
            }

            /// <summary>Cover blurred over a radius in raster cells (0..1).</summary>
            internal float[] Smoothed(int radius, CancellationToken cancel)
            {
                return TerrainStages.Blur(covered, size, size, radius, 2, cancel);
            }

            internal float SampleField(float[] field, float x, float z)
            {
                float gx = MathCore.Clamp(x / CoverCell - 0.5f, 0, size - 1), gz = MathCore.Clamp(z / CoverCell - 0.5f, 0, size - 1);
                int ix = (int)gx, iz = (int)gz, jx = Math.Min(ix + 1, size - 1), jz = Math.Min(iz + 1, size - 1);
                float u = gx - ix, v = gz - iz;
                return (field[iz * size + ix] * (1 - u) + field[iz * size + jx] * u) * (1 - v) + (field[jz * size + ix] * (1 - u) + field[jz * size + jx] * u) * v;
            }
        }

        /// <summary>Uniform buckets over the map; spacing rules are symmetric for every pair.</summary>
        private sealed class PlantHash
        {
            private readonly int size, reach;
            private readonly int[] head;
            private readonly List<int> next = new List<int>();
            private readonly List<PlantInstance> plants;
            private readonly SpeciesSpec[] species;

            internal PlantHash(float worldSize, SpeciesSpec[] species, List<PlantInstance> plants)
            {
                this.species = species;
                this.plants = plants;
                size = Math.Max(1, (int)Math.Ceiling(worldSize / BucketSize) + 1);
                head = new int[size * size];
                float widest = 0;
                foreach (SpeciesSpec s in species)
                    widest = Math.Max(widest, Math.Max(s.Footprint, s.TrunkRadius) * s.ScaleMax);
                reach = (int)Math.Ceiling(2 * widest / BucketSize);
                Clear();
            }

            internal void Clear()
            {
                for (int i = 0; i < head.Length; i++)
                    head[i] = -1;
                next.Clear();
            }

            /// <summary>Adds the plant to the shared list and the buckets.</summary>
            internal void Add(PlantInstance p)
            {
                int bucket = Bucket(p.X, p.Z);
                next.Add(head[bucket]);
                head[bucket] = plants.Count;
                plants.Add(p);
            }

            internal bool Fits(PlantInstance p)
            {
                return Count(p, -1, true) == 0;
            }

            /// <summary>Plants closer to plants[index] than the rule allows (each pair counted once).</summary>
            internal int Violations(int index)
            {
                return Count(plants[index], index, false);
            }

            private int Count(PlantInstance p, int self, bool stopAtFirst)
            {
                int bx = Clamp((int)(p.X / BucketSize)), bz = Clamp((int)(p.Z / BucketSize));
                int found = 0;
                for (int z = Math.Max(0, bz - reach); z <= Math.Min(size - 1, bz + reach); z++)
                    for (int x = Math.Max(0, bx - reach); x <= Math.Min(size - 1, bx + reach); x++)
                        for (int j = head[z * size + x]; j >= 0; j = next[j])
                        {
                            if (j == self || (self >= 0 && j < self))
                                continue;
                            if (TooClose(p, plants[j]))
                            {
                                found++;
                                if (stopAtFirst)
                                    return found;
                            }
                        }

                return found;
            }

            private int Bucket(float x, float z)
            {
                return Clamp((int)(z / BucketSize)) * size + Clamp((int)(x / BucketSize));
            }

            private int Clamp(int v)
            {
                return Math.Max(0, Math.Min(size - 1, v));
            }

            private bool TooClose(PlantInstance p, PlantInstance q)
            {
                SpeciesSpec a = species[p.Species], b = species[q.Species];
                bool logA = a.Id == SpeciesTable.Log, logB = b.Id == SpeciesTable.Log;
                if (logA != logB)
                    return logA ? LogTooClose(p, b, q) : LogTooClose(q, a, p);
                float need = Required(a, p.Scale, b, q.Scale);
                float dx = p.X - q.X, dz = p.Z - q.Z;
                return dx * dx + dz * dz < need * need - 1e-4f;
            }

            /// <summary>
            /// A log is a 6 m × 0.3 m capsule along its local x; it may lie between trunks, so it
            /// is tested as a segment against the other plant's trunk, crown or rock radius.
            /// </summary>
            private static bool LogTooClose(PlantInstance log, SpeciesSpec other, PlantInstance q)
            {
                float ax = (float)Math.Cos(log.Yaw), az = -(float)Math.Sin(log.Yaw), half = LogHalfLength * log.Scale;
                float dx = q.X - log.X, dz = q.Z - log.Z;
                float along = Math.Max(-half, Math.Min(half, dx * ax + dz * az));
                float ex = dx - along * ax, ez = dz - along * az;
                float radius = other.Layer == PlantLayer.Canopy ? other.TrunkRadius
                    : other.Layer == PlantLayer.Understory ? 0.5f * other.Footprint
                    : other.Footprint;
                float need = radius * q.Scale + LogRadius * log.Scale;
                return ex * ex + ez * ez < need * need - 1e-4f;
            }

            /// <summary>Shade-tolerant plants and props under a canopy tree keep only trunk clearance.</summary>
            internal static float Required(SpeciesSpec a, float sa, SpeciesSpec b, float sb)
            {
                if (a.Layer == PlantLayer.Canopy && b.Layer != PlantLayer.Canopy && b.ShadeTolerant)
                    return a.TrunkRadius * sa + UnderCanopy(b, sb);
                if (b.Layer == PlantLayer.Canopy && a.Layer != PlantLayer.Canopy && a.ShadeTolerant)
                    return b.TrunkRadius * sb + UnderCanopy(a, sa);
                return Math.Min(a.Spacing, b.Spacing) * (a.Footprint * sa + b.Footprint * sb);
            }

            /// <summary>Solid props keep their whole radius from a trunk; soft understory may brush it.</summary>
            private static float UnderCanopy(SpeciesSpec s, float scale)
            {
                return (s.Layer == PlantLayer.Prop ? 1f : 0.5f) * s.Footprint * scale;
            }
        }
    }
}

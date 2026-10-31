using System;

namespace Procedural.Generation
{
    /// <summary>Placement order: canopy, then understory, then props. Ground cover is a detail map.</summary>
    public enum PlantLayer
    {
        Canopy,
        Understory,
        Ground,
        Prop
    }

    /// <summary>Suitability over one factor: 0 at or below Min, rising to 1 at OptMin, 1 up to OptMax, 0 again at Max.</summary>
    [Serializable]
    public struct Trapezoid
    {
        public float Min, OptMin, OptMax, Max;

        public Trapezoid(float min, float optMin, float optMax, float max)
        {
            Min = min;
            OptMin = optMin;
            OptMax = optMax;
            Max = max;
        }

        /// <summary>No limit on either side.</summary>
        public static Trapezoid Any => new Trapezoid(-float.MaxValue, -float.MaxValue, float.MaxValue, float.MaxValue);

        /// <summary>Only a lower ramp: 0 at min, 1 from optMin upwards.</summary>
        public static Trapezoid AtLeast(float min, float optMin) => new Trapezoid(min, optMin, float.MaxValue, float.MaxValue);

        /// <summary>Only an upper ramp: 1 up to optMax, 0 at max.</summary>
        public static Trapezoid AtMost(float optMax, float max) => new Trapezoid(-float.MaxValue, -float.MaxValue, optMax, max);

        public float Evaluate(float value)
        {
            if (value <= Min || value >= Max)
                return 0;
            if (value < OptMin)
                return (value - Min) / (OptMin - Min);
            if (value > OptMax)
                return (Max - value) / (Max - OptMax);
            return 1;
        }
    }

    /// <summary>A species' needs, size and spacing. Ids match the Unity species catalog.</summary>
    public sealed class SpeciesSpec
    {
        public string Id;
        public PlantLayer Layer;
        /// <summary>A Terrain detail coverage map instead of instances (grass, reeds).</summary>
        public bool Detail;
        /// <summary>Spacing radius in metres at scale 1.</summary>
        public float Footprint;
        /// <summary>Radius that counts as canopy cover at scale 1; 0 for none.</summary>
        public float CrownRadius;
        /// <summary>Clearance shade-tolerant plants and props keep from this plant's trunk.</summary>
        public float TrunkRadius;
        /// <summary>Instances per m² at full suitability; for details the maximum coverage 0..1.</summary>
        public float MaxDensity;
        public float ScaleMin = 1, ScaleMax = 1;
        /// <summary>May stand under canopy, keeping only the trunk clearance.</summary>
        public bool ShadeTolerant;
        /// <summary>Metres above sea level.</summary>
        public Trapezoid Elevation = Trapezoid.Any;
        /// <summary>Degrees.</summary>
        public Trapezoid Slope = Trapezoid.Any;
        /// <summary>IslandResult.Moisture, 0..1.</summary>
        public Trapezoid Moisture = Trapezoid.Any;
        /// <summary>Metres to river channels and lakes.</summary>
        public Trapezoid WaterDistance = Trapezoid.Any;
        /// <summary>Metres to the coast.</summary>
        public Trapezoid SeaDistance = Trapezoid.Any;
        /// <summary>Affinity per SurfaceLayer (sand, grass, rock, gravel); suitability is scaled by the weighted sum.</summary>
        public float[] Surface = { 1, 1, 1, 1 };
        /// <summary>Metres; low-frequency noise of this size makes groves and clearings.</summary>
        public float ClusterScale = 100;
        /// <summary>Weighted share of rock surface within about 8 m: draws boulders and stones to cliffs.</summary>
        public Trapezoid RockNearby = Trapezoid.Any;
        /// <summary>Share of the footprint radius sunk below the ground (props half buried in it).</summary>
        public float Bury;
        /// <summary>
        /// Metres at scale 1 an upright plant stands below the lowest ground under its root flare
        /// (twice the trunk radius), so its base never floats on a slope. Its model goes on below the
        /// pivot (deep roots) by at least this plus the flare's drop on the steepest allowed slope.
        /// </summary>
        public float RootDepth;
        /// <summary>Tilted onto the terrain's normal (with a little jitter) instead of standing upright.</summary>
        public bool AlignToSlope;
        /// <summary>Pairs keep at least Spacing × (summed footprints); below 0.9 they may interpenetrate.</summary>
        public float Spacing = 0.9f;
    }

    /// <summary>One placed plant or prop.</summary>
    public struct PlantInstance
    {
        /// <summary>Index into IslandResult.Species.</summary>
        public int Species;
        /// <summary>World metres; Y is the terrain height at (X, Z).</summary>
        public float X, Z, Y;
        public float Scale;
        /// <summary>Radians about the vertical axis.</summary>
        public float Yaw;
        /// <summary>Radians about the instance's local z axis: pitch along a log lying on local x. 0 for upright plants.</summary>
        public float Tilt;
        /// <summary>
        /// Radians about the local x axis. Unity rotation: Euler(0, Yaw, 0) · Euler(Roll, 0, Tilt),
        /// which puts a slope-aligned prop's local up on the terrain normal.
        /// </summary>
        public float Roll;
    }

    /// <summary>Species sets the generator knows.</summary>
    public static class SpeciesTable
    {
        public const string Pine = "pine", Broadleaf = "broadleaf", Alder = "alder", Snag = "snag", Shrub = "shrub", Log = "log", Rock = "rock", Slab = "slab", Stone = "stone", Mound = "mound", Grass = "grass", Reed = "reed";

        /// <summary>Temperate wet island: conifers on drier high ground, broadleaf in moist lowlands, alder and reeds by water.</summary>
        public static SpeciesSpec[] Temperate(float peakHeight)
        {
            float peak = Math.Max(1, peakHeight);
            return new[]
            {
                new SpeciesSpec
                {
                    Id = Pine, Layer = PlantLayer.Canopy, Footprint = 1.8f, CrownRadius = 2.2f, TrunkRadius = 0.25f, RootDepth = 0.25f,
                    MaxDensity = 0.045f, ScaleMin = 0.7f, ScaleMax = 1.3f,
                    Elevation = Trapezoid.AtLeast(1, 8), Slope = Trapezoid.AtMost(28, 38),
                    Moisture = new Trapezoid(0.05f, 0.2f, 0.55f, 0.8f),
                    WaterDistance = Trapezoid.AtLeast(2, 6), SeaDistance = Trapezoid.AtLeast(5, 20),
                    Surface = new[] { 0f, 1f, 0.35f, 0.1f }, ClusterScale = 120
                },
                new SpeciesSpec
                {
                    Id = Broadleaf, Layer = PlantLayer.Canopy, Footprint = 2.6f, CrownRadius = 3.5f, TrunkRadius = 0.3f, RootDepth = 0.3f,
                    MaxDensity = 0.03f, ScaleMin = 0.7f, ScaleMax = 1.2f,
                    Elevation = new Trapezoid(0.5f, 3, 0.55f * peak, 0.8f * peak), Slope = Trapezoid.AtMost(20, 30),
                    Moisture = new Trapezoid(0.3f, 0.45f, 0.85f, 1.01f),
                    WaterDistance = Trapezoid.AtLeast(1.5f, 5), SeaDistance = Trapezoid.AtLeast(10, 30),
                    Surface = new[] { 0f, 1f, 0.15f, 0.1f }, ClusterScale = 150
                },
                new SpeciesSpec
                {
                    Id = Alder, Layer = PlantLayer.Canopy, Footprint = 2, CrownRadius = 2.5f, TrunkRadius = 0.22f, RootDepth = 0.25f,
                    MaxDensity = 0.035f, ScaleMin = 0.7f, ScaleMax = 1.1f,
                    Elevation = Trapezoid.AtMost(0.3f * peak, 0.45f * peak), Slope = Trapezoid.AtMost(10, 18),
                    Moisture = Trapezoid.AtLeast(0.55f, 0.7f),
                    WaterDistance = new Trapezoid(0.8f, 2, 15, 30), SeaDistance = Trapezoid.AtLeast(5, 15),
                    Surface = new[] { 0f, 1f, 0.1f, 0.5f }, ClusterScale = 80
                },
                // Snags are dead canopy trees: converted from pines and broadleaves inside forest, never sampled.
                new SpeciesSpec
                {
                    Id = Snag, Layer = PlantLayer.Canopy, Footprint = 1.5f, TrunkRadius = 0.25f, RootDepth = 0.25f,
                    ScaleMin = 0.7f, ScaleMax = 1.3f, Slope = Trapezoid.AtMost(28, 38)
                },
                new SpeciesSpec
                {
                    Id = Shrub, Layer = PlantLayer.Understory, ShadeTolerant = true, Footprint = 1, TrunkRadius = 0.1f, RootDepth = 0.1f,
                    MaxDensity = 0.06f, ScaleMin = 0.6f, ScaleMax = 1.4f,
                    Elevation = Trapezoid.AtLeast(0.3f, 2), Slope = Trapezoid.AtMost(30, 40),
                    Moisture = new Trapezoid(0.15f, 0.3f, 0.9f, 1.01f),
                    WaterDistance = Trapezoid.AtLeast(0.8f, 2), SeaDistance = Trapezoid.AtLeast(3, 8),
                    Surface = new[] { 0.1f, 1f, 0.4f, 0.3f }, ClusterScale = 60
                },
                // Logs lie near canopy trees in forest; placed from the trees, never sampled.
                new SpeciesSpec
                {
                    // Footprint: half the 6 m length plus the 0.3 m radius, so a log clears trunks.
                    Id = Log, Layer = PlantLayer.Prop, ShadeTolerant = true, Footprint = 3.3f, TrunkRadius = 0.3f,
                    ScaleMin = 0.6f, ScaleMax = 1.2f, Slope = Trapezoid.AtMost(18, 25),
                    WaterDistance = Trapezoid.AtLeast(1, 3)
                },
                new SpeciesSpec
                {
                    // Boulders: half buried, gathered below and on rock, allowed to interpenetrate.
                    Id = Rock, Layer = PlantLayer.Prop, ShadeTolerant = true, Footprint = 1.5f, TrunkRadius = 0.6f,
                    MaxDensity = 0.02f, ScaleMin = 0.5f, ScaleMax = 2.5f, Bury = 0.4f, AlignToSlope = true, Spacing = 0.5f,
                    Slope = Trapezoid.AtLeast(2, 15), WaterDistance = Trapezoid.AtLeast(0.3f, 1),
                    RockNearby = Trapezoid.AtLeast(0.02f, 0.25f),
                    Surface = new[] { 0.2f, 0.25f, 1f, 0.8f }, ClusterScale = 50
                },
                new SpeciesSpec
                {
                    // Flat slabs on rock and scree, sunk deep, lying with the slope.
                    Id = Slab, Layer = PlantLayer.Prop, ShadeTolerant = true, Footprint = 1.5f, TrunkRadius = 0.6f,
                    MaxDensity = 0.01f, ScaleMin = 0.6f, ScaleMax = 2.2f, Bury = 0.25f, AlignToSlope = true, Spacing = 0.6f,
                    Slope = Trapezoid.AtLeast(8, 20), WaterDistance = Trapezoid.AtLeast(0.3f, 1),
                    RockNearby = Trapezoid.AtLeast(0.05f, 0.3f),
                    Surface = new[] { 0.1f, 0.1f, 1f, 0.6f }, ClusterScale = 40
                },
                new SpeciesSpec
                {
                    // Loose stones: dense on gravel, scree and by channels, scattered elsewhere.
                    Id = Stone, Layer = PlantLayer.Prop, ShadeTolerant = true, Footprint = 0.3f, TrunkRadius = 0.15f,
                    MaxDensity = 0.08f, ScaleMin = 0.5f, ScaleMax = 1.6f, Bury = 0.3f, AlignToSlope = true, Spacing = 0.8f,
                    WaterDistance = Trapezoid.AtLeast(0.2f, 0.5f),
                    Surface = new[] { 0.3f, 0.08f, 0.8f, 1f }, ClusterScale = 25
                },
                new SpeciesSpec
                {
                    // Grassy hummocks on meadows and wet ground.
                    Id = Mound, Layer = PlantLayer.Prop, ShadeTolerant = true, Footprint = 1.2f, TrunkRadius = 0.6f,
                    MaxDensity = 0.012f, ScaleMin = 0.6f, ScaleMax = 1.6f, Bury = 0.6f, AlignToSlope = true, Spacing = 0.7f,
                    Slope = Trapezoid.AtMost(15, 25), Moisture = Trapezoid.AtLeast(0.3f, 0.5f),
                    WaterDistance = Trapezoid.AtLeast(0.5f, 2), SeaDistance = Trapezoid.AtLeast(5, 15),
                    Surface = new[] { 0f, 1f, 0f, 0.1f }, ClusterScale = 35
                },
                new SpeciesSpec
                {
                    Id = Grass, Layer = PlantLayer.Ground, Detail = true, MaxDensity = 1,
                    Elevation = Trapezoid.AtLeast(0.2f, 1.5f), Slope = Trapezoid.AtMost(30, 40),
                    Moisture = Trapezoid.AtLeast(0.1f, 0.25f),
                    WaterDistance = Trapezoid.AtLeast(0.3f, 1), SeaDistance = Trapezoid.AtLeast(3, 10),
                    Surface = new[] { 0.15f, 1f, 0.1f, 0.2f }, ClusterScale = 40
                },
                new SpeciesSpec
                {
                    Id = Reed, Layer = PlantLayer.Ground, Detail = true, MaxDensity = 0.9f,
                    Elevation = Trapezoid.AtMost(3, 6), Slope = Trapezoid.AtMost(6, 12),
                    Moisture = Trapezoid.AtLeast(0.65f, 0.8f),
                    WaterDistance = Trapezoid.AtMost(2, 5), SeaDistance = Trapezoid.AtLeast(4, 10),
                    Surface = new[] { 0.2f, 0.8f, 0f, 1f }, ClusterScale = 30
                }
            };
        }
    }
}

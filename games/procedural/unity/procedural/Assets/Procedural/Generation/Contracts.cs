using System;
using System.Collections.Generic;

namespace Procedural.Generation
{
    /// <summary>World scale defaults.</summary>
    public enum Preset
    {
        Small,
        Medium
    }

    /// <summary>Macro terrain solver.</summary>
    public enum ReliefMode
    {
        StreamPower,
        Noise,
        ImplicitStreamPower
    }

    /// <summary>Resolution and iteration budget.</summary>
    public enum BakeMode
    {
        Preview,
        Bake
    }

    /// <summary>Indices of normalized terrain materials.</summary>
    public static class SurfaceLayer
    {
        public const int Sand = 0, Grass = 1, Rock = 2, Gravel = 3, Count = 4;
    }

    /// <summary>Serializable, complete generator input. Zero dimensional settings use preset defaults.</summary>
    [Serializable]
    public class IslandRecipe
    {
        public int seed = 1;
        public Preset preset = Preset.Small;
        public ReliefMode relief = ReliefMode.StreamPower;
        public float landFraction = 0.45f, coastRoughness = 0.5f, elongation = 1f, orientationDegrees = -1f;
        public int massifs = 2;
        public float ruggedness = 0.5f, riverDensity = 6f;
        public int lakes = 2;
        public float beachShare = 0.4f;
        // Zero uses a physical preset default, including old serialized recipes.
        public float beachWidth;
        public float channelHeadArea;
        public bool particleErosion;
        public float erosionDropletsPerSquareMetre = 0.15f;
        public float implicitTimeStep = 1f;
        /// <summary>Canopy cover share of the land, met within ±3 % when the land allows it.</summary>
        public float forestCover = 0.30f;
        /// <summary>Metre-scale relief: rock ledges, scree, soil hummocks (0 = smooth, 1 = default).</summary>
        public float microRelief = 1f;
        /// <summary>False skips plant placement (fast previews and tests).</summary>
        public bool vegetation = true;
        public float peakHeight, seaDepth, maxRiverWidth, maxRiverDepth, worldSize;
        public int simResolution, heightmapResolution;
        public IslandRecipe Clone()
        {
            return (IslandRecipe)MemberwiseClone();
        }
    }

    /// <summary>Row-major samples at (x * CellSize, z * CellSize), in metres.</summary>
    public sealed class Grid
    {
        public readonly int Width, Height;
        public readonly float CellSize;
        public readonly float[] Data;
        public Grid(int width, int height, float cellSize)
        {
            if (width < 2 || height < 2 || !(cellSize > 0) || float.IsInfinity(cellSize))
                throw new ArgumentOutOfRangeException(nameof(width));
            Width = width;
            Height = height;
            CellSize = cellSize;
            Data = new float[checked(width * height)];
        }

        public float this[int x, int z]
        {
            get
            {
                return Data[z * Width + x];
            }

            set
            {
                Data[z * Width + x] = value;
            }
        }
    }

    /// <summary>A reach between network junctions, oriented downstream. All point arrays have equal lengths.</summary>
    public sealed class RiverSegment
    {
        public int Id, Order;
        public int Downstream = -1, FromLake = -1, ToLake = -1;
        public bool ToSea;
        public float[] X, Z, Surface, Bed, Width, Depth, Discharge;
        public bool[] Rapid;
    }

    /// <summary>A connected flooded basin and its unique routed spill cell.</summary>
    public sealed class Lake
    {
        public int Id;
        public float Level;
        public int Cells;
        public float Area;
        public float InflowCatchmentArea, InflowChannelLength, CoastDistance;
        public int OutletX, OutletZ;
        public int OutletCount;
    }

    /// <summary>Final terrain, water network, materials and validation report.</summary>
    public sealed class IslandResult
    {
        public IslandRecipe Recipe;
        public BakeMode Mode;
        public float WorldSize, SeaDepth, PeakHeight;
        public Grid Height;
        public Grid[] Surface;
        public Grid Discharge;
        /// <summary>Land-percentile normalized TWI and moisture, both 0..1 at Height resolution. Open water and sea are 1.</summary>
        public Grid Wetness;
        public Grid Moisture;
        public int[] LakeId;
        public List<Lake> Lakes;
        public List<RiverSegment> Rivers;
        /// <summary>Species of this island; PlantInstance.Species indexes it.</summary>
        public SpeciesSpec[] Species;
        /// <summary>Instances of every non-detail species (empty when the recipe disables vegetation).</summary>
        public List<PlantInstance> Plants;
        /// <summary>One coverage map (0..1, Height's size) per detail species, in Species order.</summary>
        public Grid[] DetailCoverage;
        public string ReportJson;
        public bool Passed;
    }

    /// <summary>Progress within a zero-based pipeline stage.</summary>
    public readonly struct PipelineProgress
    {
        public PipelineProgress(string stage, int index, int count, float fraction)
        {
            Stage = stage;
            Index = index;
            Count = count;
            Fraction = fraction;
        }

        public readonly string Stage;
        public readonly int Index, Count;
        public readonly float Fraction;
    }
}

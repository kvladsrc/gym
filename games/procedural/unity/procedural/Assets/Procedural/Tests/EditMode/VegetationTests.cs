using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Procedural.Generation;

namespace Procedural.Tests
{
    /// <summary>Vegetation placement on synthetic maps and end to end; checks are independent of the stage's own fields.</summary>
    public class VegetationTests
    {
        private const int Size = 257;
        private const float Cell = 2;
        private const int RiverX = 128;

        [Test]
        public void TrapezoidRampsAndPlateau()
        {
            var t = new Trapezoid(0, 10, 20, 40);
            Assert.AreEqual(0f, t.Evaluate(-5));
            Assert.AreEqual(0f, t.Evaluate(0));
            Assert.AreEqual(0.5f, t.Evaluate(5), 1e-6f);
            Assert.AreEqual(1f, t.Evaluate(15));
            Assert.AreEqual(0.5f, t.Evaluate(30), 1e-6f);
            Assert.AreEqual(0f, t.Evaluate(40));
            Assert.AreEqual(1f, Trapezoid.Any.Evaluate(1e9f));
            Assert.AreEqual(1f, Trapezoid.AtLeast(1, 2).Evaluate(1e9f));
            Assert.AreEqual(1f, Trapezoid.AtMost(1, 2).Evaluate(-1e9f));
        }

        /// <summary>
        /// A moist 512 m island at about 20 m. Water is wherever <paramref name="waterDistance"/>
        /// (metres, by sample) is ≤ 0; by default a 4 m river runs down the middle.
        /// </summary>
        private static VegetationInput Map(float forestCover, Func<int, int, float> height = null, Func<int, int, float> waterDistance = null, int seed = 3)
        {
            height = height ?? ((x, z) => 20);
            waterDistance = waterDistance ?? ((x, z) => Math.Abs(x - RiverX) * Cell - 2);
            var h = new Grid(Size, Size, Cell);
            var moisture = new Grid(Size, Size, Cell);
            var surface = new Grid[SurfaceLayer.Count];
            for (int l = 0; l < surface.Length; l++)
                surface[l] = new Grid(Size, Size, Cell);
            var ocean = new bool[Size * Size];
            var water = new bool[Size * Size];
            var distance = new float[Size * Size];
            for (int z = 0; z < Size; z++)
                for (int x = 0; x < Size; x++)
                {
                    int i = z * Size + x;
                    ocean[i] = x < 4 || z < 4 || x >= Size - 4 || z >= Size - 4;
                    h.Data[i] = ocean[i] ? -2 : height(x, z);
                    moisture.Data[i] = 0.6f;
                    surface[SurfaceLayer.Grass].Data[i] = 1;
                    distance[i] = Math.Max(0, waterDistance(x, z));
                    water[i] = distance[i] <= 0;
                }

            return new VegetationInput
            {
                Height = h,
                Moisture = moisture,
                Surface = surface,
                Ocean = ocean,
                Water = water,
                WaterDistance = distance,
                Seed = seed,
                Peak = 100,
                ForestCover = forestCover
            };
        }

        private static VegetationResult Place(VegetationInput input)
        {
            return VegetationStage.Place(input, SpeciesTable.Temperate(input.Peak), CancellationToken.None);
        }

        /// <summary>Share of dry samples under a scaled crown of the final plants (snags have none).</summary>
        private static float CrownCover(VegetationResult result, VegetationInput input)
        {
            var covered = new bool[Size * Size];
            foreach (PlantInstance p in result.Plants)
            {
                float r = result.Species[p.Species].CrownRadius * p.Scale;
                if (r <= 0)
                    continue;
                for (int z = (int)((p.Z - r) / Cell); z <= (int)((p.Z + r) / Cell) + 1; z++)
                    for (int x = (int)((p.X - r) / Cell); x <= (int)((p.X + r) / Cell) + 1; x++)
                        if (x >= 0 && z >= 0 && x < Size && z < Size && (x * Cell - p.X) * (x * Cell - p.X) + (z * Cell - p.Z) * (z * Cell - p.Z) <= r * r)
                            covered[z * Size + x] = true;
            }

            int dry = 0, under = 0;
            for (int i = 0; i < covered.Length; i++)
                if (!input.Ocean[i] && !input.Water[i])
                {
                    dry++;
                    if (covered[i])
                        under++;
                }

            return under / (float)dry;
        }

        [Test]
        public void FlatIslandHoldsEveryInvariant()
        {
            VegetationResult result = Place(Map(0.45f));
            foreach (var v in result.Invariants)
                Assert.IsTrue(v.Passed, v.Name + ": " + v.Detail);
            Assert.Greater(result.Plants.Count, 100);
        }

        [Test]
        public void NoPlantStandsInTheRiver()
        {
            VegetationResult result = Place(Map(0.45f));
            foreach (PlantInstance p in result.Plants)
                Assert.GreaterOrEqual(Math.Abs(p.X - RiverX * Cell) - 2, 0.5f - 1e-3f, result.Species[p.Species].Id + " at x=" + p.X);
        }

        [Test]
        public void NoPlantStandsInANarrowDiagonalChannel()
        {
            // A 0.4 m wide channel along x = z, narrower than the 2 m sample spacing.
            const float halfWidth = 0.2f;
            VegetationResult result = Place(Map(0.45f, waterDistance: (x, z) => Math.Abs(x - z) * Cell / (float)Math.Sqrt(2) - halfWidth));
            foreach (PlantInstance p in result.Plants)
            {
                float toWater = Math.Abs(p.X - p.Z) / (float)Math.Sqrt(2) - halfWidth;
                Assert.GreaterOrEqual(toWater, 0.5f - 1e-3f, result.Species[p.Species].Id + " at " + p.X + ", " + p.Z);
            }
        }

        [Test]
        public void NoUprightPlantOnASteepComb()
        {
            // Alternating 20 m and 24 m columns, 2 m apart: 63° everywhere, yet central differences read 0.
            VegetationResult result = Place(Map(0.45f, height: (x, z) => x % 2 == 0 ? 20 : 24));
            foreach (PlantInstance p in result.Plants)
            {
                SpeciesSpec s = result.Species[p.Species];
                Assert.IsTrue(s.Slope.Max >= 90, s.Id + " placed on a 63° comb at " + p.X + ", " + p.Z);
            }
        }

        [TestCase(0.2f)]
        [TestCase(0.3f)]
        [TestCase(0.45f)]
        public void ForestCoverIsCalibratedOnTheFinalCrowns(float target)
        {
            VegetationInput input = Map(target);
            VegetationResult result = Place(input);
            float cover = CrownCover(result, input);
            Assert.AreEqual(target, cover, 0.03f, "independent crown cover; passes " + result.Metrics.CalibrationPasses);
            Assert.AreEqual(result.Metrics.ForestCover, cover, 0.01f, "reported cover differs from the final crowns");
        }

        [Test]
        public void LogsClearEveryTrunk()
        {
            VegetationResult result = Place(Map(0.6f));
            var trunks = new List<PlantInstance>();
            var logs = new List<PlantInstance>();
            foreach (PlantInstance p in result.Plants)
            {
                SpeciesSpec s = result.Species[p.Species];
                if (s.Layer == PlantLayer.Canopy)
                    trunks.Add(p);
                else if (s.Id == SpeciesTable.Log)
                    logs.Add(p);
            }

            Assert.Greater(logs.Count, 0);
            foreach (PlantInstance log in logs)
            {
                // The log lies along its local +x: (cos yaw, -sin yaw) in x/z, 6 m long, 0.3 m thick at scale 1.
                float ax = (float)Math.Cos(log.Yaw), az = -(float)Math.Sin(log.Yaw), half = 3 * log.Scale;
                foreach (PlantInstance tree in trunks)
                {
                    float dx = tree.X - log.X, dz = tree.Z - log.Z;
                    float along = Math.Max(-half, Math.Min(half, dx * ax + dz * az));
                    float ex = dx - along * ax, ez = dz - along * az;
                    float clear = result.Species[tree.Species].TrunkRadius * tree.Scale + 0.3f * log.Scale;
                    Assert.GreaterOrEqual((float)Math.Sqrt(ex * ex + ez * ez), clear - 1e-3f, "log through a trunk at " + tree.X + ", " + tree.Z);
                }
            }
        }

        [Test]
        public void SlopeAlignedPropsStandOnTheNormal()
        {
            // A plane rising 0.3 m per metre in x and 0.2 in z; its normal is (-0.3, 1, -0.2) normalised.
            VegetationResult result = Place(Map(0.3f, height: (x, z) => 20 + 0.3f * x * Cell + 0.2f * z * Cell));
            double nl = Math.Sqrt(0.09 + 1 + 0.04);
            int checkedProps = 0;
            foreach (PlantInstance p in result.Plants)
            {
                SpeciesSpec s = result.Species[p.Species];
                // The map's edge is a cliff down to the sea: only the plane's interior has its normal.
                if (!s.AlignToSlope || p.X < 12 || p.Z < 12 || p.X > (Size - 1) * Cell - 12 || p.Z > (Size - 1) * Cell - 12)
                    continue;
                // Unity: Euler(0, Yaw, 0) · Euler(Roll, 0, Tilt) applied to up = (0, 1, 0).
                double ux = -Math.Sin(p.Tilt), uy = Math.Cos(p.Tilt) * Math.Cos(p.Roll), uz = Math.Cos(p.Tilt) * Math.Sin(p.Roll);
                double c = Math.Cos(p.Yaw), sn = Math.Sin(p.Yaw);
                double wx = c * ux + sn * uz, wy = uy, wz = -sn * ux + c * uz;
                double dot = (wx * -0.3 + wy + wz * -0.2) / nl;
                Assert.Greater(dot, Math.Cos(0.5), s.Id + " leans more than the jitter allows from the normal");
                checkedProps++;
            }

            Assert.Greater(checkedProps, 0);
        }

        [Test]
        public void RootedPlantsNeverFloatOnASlope()
        {
            // A plane rising 0.65 m per metre in x and 0.1 in z (33°): a flare edge drops a third of
            // its radius, more than the root depth, so standing at the centre's height would float.
            const float gx = 0.65f, gz = 0.1f;
            Func<float, float, float> plane = (x, z) => 20 + gx * x + gz * z;
            VegetationResult result = Place(Map(0.3f, height: (x, z) => plane(x * Cell, z * Cell)));
            double drop = Math.Sqrt(gx * gx + gz * gz);
            int rooted = 0;
            foreach (PlantInstance p in result.Plants)
            {
                SpeciesSpec s = result.Species[p.Species];
                if (s.RootDepth <= 0 || p.X < 12 || p.Z < 12 || p.X > (Size - 1) * Cell - 12 || p.Z > (Size - 1) * Cell - 12)
                    continue;
                float r = 2 * s.TrunkRadius * p.Scale, lowest = plane(p.X, p.Z) - (float)(drop * r);
                Assert.LessOrEqual(p.Y, lowest + 1e-3f, s.Id + " floats above the downhill edge of its flare");
                Assert.GreaterOrEqual(p.Y, lowest - s.RootDepth * p.Scale - 1e-3f, s.Id + " sunk deeper than its roots");
                rooted++;
            }

            Assert.Greater(rooted, 0);
        }

        [Test]
        public void SameInputPlacesTheSamePlants()
        {
            List<PlantInstance> a = Place(Map(0.45f)).Plants, b = Place(Map(0.45f)).Plants;
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
                Assert.IsTrue(a[i].Equals(b[i]), "plant " + i);
        }

        [Test]
        public void DeadwoodAndUnderstoryAppearInForest()
        {
            VegetationResult result = Place(Map(0.6f));
            var counts = new Dictionary<string, int>();
            foreach (PlantInstance p in result.Plants)
            {
                string id = result.Species[p.Species].Id;
                counts[id] = counts.TryGetValue(id, out int c) ? c + 1 : 1;
            }

            foreach (string id in new[] { SpeciesTable.Broadleaf, SpeciesTable.Snag, SpeciesTable.Log, SpeciesTable.Shrub })
                Assert.IsTrue(counts.ContainsKey(id), "no " + id);
        }

        [Test]
        public void GrassAvoidsWaterAndThinsUnderCanopy()
        {
            float Mean(VegetationResult r)
            {
                double sum = 0;
                foreach (float v in r.DetailCoverage[0].Data)
                    sum += v;
                return (float)(sum / r.DetailCoverage[0].Data.Length);
            }

            VegetationResult open = Place(Map(0.0f)), forest = Place(Map(0.6f));
            Assert.AreEqual(SpeciesTable.Grass, open.Species[Array.FindIndex(open.Species, s => s.Detail)].Id);
            Assert.AreEqual(0f, forest.DetailCoverage[0][RiverX, Size / 2]);
            // Same suitability and clusters in both; only the canopy differs.
            Assert.Less(Mean(forest), 0.75f * Mean(open), "grass is not thinner under 60 % canopy");
        }

        [Test]
        public void TinyIslandWithVegetationPasses()
        {
            var recipe = new IslandRecipe
            {
                seed = 7, worldSize = 256, simResolution = 64, heightmapResolution = 129,
                peakHeight = 45, seaDepth = 12, maxRiverWidth = 6, maxRiverDepth = 1, riverDensity = 2, lakes = 2
            };
            IslandResult result = IslandPipeline.Run(recipe, BakeMode.Bake, null, null, CancellationToken.None);
            Assert.IsTrue(result.Passed, result.ReportJson);
            Assert.Greater(result.Plants.Count, 0);
            Assert.AreEqual(2, result.DetailCoverage.Length);
            StringAssert.Contains("\"vegetation\"", result.ReportJson);

            recipe.vegetation = false;
            IslandResult bare = IslandPipeline.Run(recipe, BakeMode.Bake, null, null, CancellationToken.None);
            Assert.AreEqual(0, bare.Plants.Count);
            Assert.AreEqual(2, bare.DetailCoverage.Length);
        }
    }
}

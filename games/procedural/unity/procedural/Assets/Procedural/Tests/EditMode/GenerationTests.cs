using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Procedural.Generation;

namespace Procedural.Tests
{
    /// <summary>Shared NUnit 3.5-compatible core tests; no Unity runtime is needed.</summary>
    public class GenerationTests
    {
        private static IslandRecipe Tiny(int seed = 7)
        {
            return new IslandRecipe
            {
                seed = seed,
                worldSize = 256,
                simResolution = 64,
                heightmapResolution = 129,
                peakHeight = 45,
                seaDepth = 12,
                maxRiverWidth = 6,
                maxRiverDepth = 1,
                riverDensity = 2,
                lakes = 2
            };
        }

        private static bool[] Ocean(Grid g)
        {
            var ocean = new bool[g.Data.Length];
            for (int z = 0; z < g.Height; z++)
                for (int x = 0; x < g.Width; x++)
                    ocean[z * g.Width + x] = x == 0 || z == 0 || x == g.Width - 1 || z == g.Height - 1;
            return ocean;
        }

        private static Grid Plateau(int size, float level)
        {
            var g = new Grid(size, size, 2);
            for (int i = 0; i < g.Data.Length; i++)
                g.Data[i] = level;
            return g;
        }

        private static void VerifyRouting(Grid g, Drainage d)
        {
            var position = new int[g.Data.Length];
            for (int i = 0; i < position.Length; i++)
                position[i] = -1;
            for (int k = 0; k < d.Stack.Length; k++)
                position[d.Stack[k]] = k;
            int land = 0;
            double discharge = 0;
            for (int i = 0; i < position.Length; i++)
            {
                if (d.Ocean[i])
                    continue;
                land++;
                Assert.IsTrue(position[i] >= 0);
                int r = d.Receiver[i];
                Assert.IsTrue(r >= 0 && r < position.Length);
                Assert.IsTrue(d.Ocean[r] || position[r] < position[i]);
                int c = i, length = 0;
                while (!d.Ocean[c] && length <= position.Length)
                {
                    c = d.Receiver[c];
                    length++;
                }

                Assert.IsTrue(length <= position.Length, "cycle");
                Assert.IsTrue(d.Ocean[c], "must reach sea");
                Assert.IsTrue(d.Fill[i] >= g.Data[i]);
                if (d.Ocean[r])
                    discharge += d.Discharge[i];
            }

            Assert.AreEqual(land, d.Stack.Length);
            Assert.That(discharge, Is.EqualTo(land * g.CellSize * g.CellSize).Within(0.001));
        }

        [Test]
        public void BroadFlatDrainsWithoutEpsilon()
        {
            Grid g = Plateau(13, 5);
            Drainage d = Hydrology.Route(g, Ocean(g), CancellationToken.None);
            VerifyRouting(g, d);
            for (int z = 1; z < 12; z++)
                for (int x = 1; x < 12; x++)
                    Assert.AreEqual(5f, d.Fill[z * 13 + x]);
        }

        [Test]
        public void EqualSpillLakesKeepTheirPhysicalLevel()
        {
            Grid g = Plateau(13, 8);
            for (int z = 4; z <= 8; z++)
                for (int x = 2; x <= 4; x++)
                    g[x, z] = 1;
            for (int z = 4; z <= 8; z++)
                for (int x = 8; x <= 10; x++)
                    g[x, z] = 1;
            g[1, 6] = g[11, 6] = 3;
            Drainage d = Hydrology.Route(g, Ocean(g), CancellationToken.None);
            VerifyRouting(g, d);
            for (int z = 4; z <= 8; z++)
            {
                Assert.AreEqual(3f, d.Fill[z * 13 + 3]);
                Assert.AreEqual(3f, d.Fill[z * 13 + 9]);
            }
        }

        [Test]
        public void NestedBasinsFillToOuterSpill()
        {
            Grid g = Plateau(13, 9);
            for (int z = 2; z <= 10; z++)
                for (int x = 2; x <= 10; x++)
                    g[x, z] = 4;
            for (int z = 4; z <= 8; z++)
                for (int x = 4; x <= 8; x++)
                    g[x, z] = 1;
            g[1, 6] = 7;
            Drainage d = Hydrology.Route(g, Ocean(g), CancellationToken.None);
            VerifyRouting(g, d);
            Assert.AreEqual(7f, d.Fill[6 * 13 + 6]);
            Assert.AreEqual(7f, d.Fill[3 * 13 + 3]);
        }

        [Test]
        public void PitInsidePlateauHasFlatFillAndConservesRain()
        {
            Grid g = Plateau(15, 6);
            g[7, 7] = -3;
            Drainage d = Hydrology.Route(g, Ocean(g), CancellationToken.None);
            VerifyRouting(g, d);
            Assert.AreEqual(6f, d.Fill[7 * 15 + 7]);
            Assert.IsTrue(d.Discharge[7 * 15 + 7] >= 4);
        }

        [Test]
        public void AsymmetricConfluenceAccumulatesCatchmentAndStrahler()
        {
            Grid g = Plateau(17, 0);
            for (int z = 1; z < 16; z++)
                for (int x = 1; x < 16; x++)
                    g[x, z] = 30 - z + Math.Abs(x - 8) * 2;
            Drainage d = Hydrology.Route(g, Ocean(g), CancellationToken.None);
            VerifyRouting(g, d);
            var channel = new bool[g.Data.Length];
            for (int i = 0; i < channel.Length; i++)
                channel[i] = !d.Ocean[i];
            int[] order = Hydrology.Strahler(d, channel, CancellationToken.None);
            int max = 0;
            for (int i = 0; i < order.Length; i++)
                max = Math.Max(max, order[i]);
            Assert.IsTrue(max >= 2);
        }

        [Test]
        public void StrahlerCountsEqualDonorsOnSelectedNetwork()
        {
            // Four levels of a binary tree. The sea sink is outside the land stack.
            var d = new Drainage
            {
                Receiver = new int[16],
                Stack = new int[15]
            };
            var selected = new bool[16];
            d.Receiver[0] = 15;
            for (int i = 0; i < 15; i++)
            {
                d.Stack[i] = i;
                selected[i] = true;
                if (i > 0)
                    d.Receiver[i] = (i - 1) / 2;
            }

            int[] order = Hydrology.Strahler(d, selected, CancellationToken.None);
            Assert.AreEqual(4, order[0]);
            Assert.AreEqual(3, order[1]);
            Assert.AreEqual(3, order[2]);
            // Removing a selected leaf lowers its whole branch. Unequal-order
            // confluences preserve the larger order instead of incrementing it.
            selected[14] = false;
            order = Hydrology.Strahler(d, selected, CancellationToken.None);
            Assert.AreEqual(0, order[14]);
            Assert.AreEqual(1, order[6]);
            Assert.AreEqual(2, order[2]);
            Assert.AreEqual(3, order[0]);
        }

        [TestCase(ReliefMode.StreamPower, 1)]
        [TestCase(ReliefMode.StreamPower, 7)]
        [TestCase(ReliefMode.Noise, 1)]
        [TestCase(ReliefMode.Noise, 7)]
        [TestCase(ReliefMode.ImplicitStreamPower, 1)]
        [TestCase(ReliefMode.ImplicitStreamPower, 7)]
        public void FinalPeakMeetsRequestedRelief(ReliefMode relief, int seed)
        {
            IslandRecipe recipe = Tiny(seed);
            recipe.relief = relief;
            IslandResult result = IslandPipeline.Run(recipe, BakeMode.Bake, null, null, CancellationToken.None);
            Valid(result);
            Assert.That(result.PeakHeight, Is.EqualTo(recipe.peakHeight).Within(recipe.peakHeight * 0.05));
            Assert.IsTrue(result.ReportJson.Contains("\"peakHeightWithinTolerance\":true"));
            Assert.IsTrue(result.ReportJson.Contains("\"concavityThetaBasinsSkipped\":"));
            Assert.IsFalse(result.ReportJson.Contains("Achieved peak height is outside"));
        }

        [Test]
        public void InfeasiblePeakIsReportedWithoutBreakingSlopeLimit()
        {
            IslandRecipe recipe = Tiny();
            recipe.peakHeight = 1000;
            recipe.lakes = 0;
            recipe.riverDensity = 0;
            IslandResult result = IslandPipeline.Run(recipe, BakeMode.Bake, null, null, CancellationToken.None);
            Assert.IsTrue(result.Passed, result.ReportJson);
            Assert.IsTrue(result.ReportJson.Contains("\"peakHeightWithinTolerance\":false"));
            Assert.IsTrue(result.ReportJson.Contains("Achieved peak height is outside"));
            Grid g = result.Height;
            for (int z = 1; z < g.Height - 1; z++)
                for (int x = 1; x < g.Width - 1; x++)
                {
                    if (g[x, z] <= 0)
                        continue;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            float drop = g[x, z] - Math.Max(0, g[x + dx, z + dz]);
                            double distance = g.CellSize * Math.Sqrt(dx * dx + dz * dz);
                            Assert.IsTrue(drop <= distance + 0.001, "Infeasible target must retain the 45 degree talus bound.");
                        }
                }
        }

        private static ulong Hash(float[] data)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                for (int i = 0; i < data.Length; i++)
                {
                    h ^= (uint)BitConverter.SingleToInt32Bits(data[i]);
                    h *= 1099511628211UL;
                }

                return h;
            }
        }

        private static void Valid(IslandResult result)
        {
            Assert.IsTrue(result.Passed, result.ReportJson);
            int land = 0;
            for (int i = 0; i < result.Height.Data.Length; i++)
                if (result.Height.Data[i] > 0)
                    land++;
            Assert.That(land / (double)result.Height.Data.Length, Is.EqualTo(result.Recipe.landFraction).Within(0.01));
            Assert.AreEqual(SurfaceLayer.Count, result.Surface.Length);
            foreach (Grid field in new[]
            {
                result.Wetness,
                result.Moisture
            }

            )
            {
                Assert.AreEqual(result.Height.Width, field.Width);
                Assert.AreEqual(result.Height.Height, field.Height);
                Assert.AreEqual(result.Height.CellSize, field.CellSize);
                foreach (float value in field.Data)
                    Assert.That(value, Is.InRange(0f, 1f));
            }

            Assert.IsTrue(result.ReportJson.Contains("\"invariants\":["));
            if (result.Recipe.riverDensity > 0)
                Assert.IsTrue(result.Rivers.Count > 0);
            else
                Assert.AreEqual(0, result.Rivers.Count);
        }

        // Contour-following dams can have fewer feasible sites on the steep,
        // coarse custom fixture. Keep their physical/inflow obligations strict;
        // count shortfalls must be reported. QualitySweep enforces default counts.
        private static void ValidLakeSites(IslandResult result)
        {
            Assert.That(result.Lakes.Count, Is.LessThanOrEqualTo(result.Recipe.lakes));
            if (result.Lakes.Count != result.Recipe.lakes)
                Assert.IsTrue(result.ReportJson.Contains("Lake sites/valid single-outlet basins achieved"));
            foreach (Lake lake in result.Lakes)
            {
                Assert.AreEqual(1, lake.OutletCount);
                Assert.That(lake.InflowCatchmentArea, Is.GreaterThanOrEqualTo(result.Recipe.channelHeadArea), "seed=" + result.Recipe.seed + " mode=" + result.Mode + " " + result.ReportJson);
                Assert.That(lake.InflowChannelLength, Is.GreaterThanOrEqualTo(result.WorldSize * 0.1f), "seed=" + result.Recipe.seed + " mode=" + result.Mode + " " + result.ReportJson);
            }
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(11)]
        [TestCase(31)]
        [TestCase(99)]
        [TestCase(26)]
        public void TinyIslandPassesAllInvariants(int seed)
        {
            IslandResult result = IslandPipeline.Run(Tiny(seed), BakeMode.Bake, null, null, CancellationToken.None);
            Valid(result);
            ValidLakeSites(result);
        }

        [TestCase(ReliefMode.StreamPower, false)]
        [TestCase(ReliefMode.ImplicitStreamPower, false)]
        [TestCase(ReliefMode.StreamPower, true)]
        public void SameRecipeIsBitwiseDeterministic(ReliefMode mode, bool erosion)
        {
            IslandRecipe r = Tiny();
            r.relief = mode;
            r.particleErosion = erosion;
            IslandResult a = IslandPipeline.Run(r, BakeMode.Bake, null, null, CancellationToken.None), b = IslandPipeline.Run(r, BakeMode.Bake, null, null, CancellationToken.None);
            Valid(a);
            Valid(b);
            Assert.AreEqual(Hash(a.Height.Data), Hash(b.Height.Data));
            Assert.AreEqual(Hash(a.Discharge.Data), Hash(b.Discharge.Data));
            Assert.AreEqual(Hash(a.Wetness.Data), Hash(b.Wetness.Data));
            Assert.AreEqual(Hash(a.Moisture.Data), Hash(b.Moisture.Data));
            for (int j = 0; j < SurfaceLayer.Count; j++)
                Assert.AreEqual(Hash(a.Surface[j].Data), Hash(b.Surface[j].Data));
            Assert.AreEqual(129, r.heightmapResolution);
            Assert.AreEqual(129, a.Height.Width);
        }

        [Test]
        public void LegacySteepRecipeRetainsPhysicalInvariantsAndReportsLakeShortfall()
        {
            IslandRecipe recipe = Tiny(99);
            recipe.peakHeight = 45;
            IslandResult result = IslandPipeline.Run(recipe, BakeMode.Bake, null, null, CancellationToken.None);
            Valid(result);
            Assert.That(result.PeakHeight, Is.EqualTo(45).Within(45 * 0.05));
            if (result.Lakes.Count != recipe.lakes)
                Assert.IsTrue(result.ReportJson.Contains("Lake sites/valid single-outlet basins achieved"));
        }

        [Test]
        public void NoiseReliefAlsoPasses()
        {
            IslandRecipe r = Tiny(13);
            r.relief = ReliefMode.Noise;
            Valid(IslandPipeline.Run(r, BakeMode.Bake, null, null, CancellationToken.None));
        }

        [Test]
        public void LakeLabelsMatchIndependentFinalFlood()
        {
            IslandResult result = IslandPipeline.Run(Tiny(99), BakeMode.Bake, null, null, CancellationToken.None);
            var ocean = new bool[result.Height.Data.Length];
            for (int i = 0; i < ocean.Length; i++)
                ocean[i] = result.Height.Data[i] <= 0;
            Drainage final = Hydrology.Route(result.Height, ocean, CancellationToken.None);
            for (int id = 0; id < result.Lakes.Count; id++)
            {
                Lake lake = result.Lakes[id];
                int cells = 0, exits = 0;
                for (int i = 0; i < result.LakeId.Length; i++)
                    if (result.LakeId[i] == id)
                    {
                        cells++;
                        Assert.That(final.Fill[i], Is.EqualTo(lake.Level).Within(0.001));
                        Assert.IsTrue(result.Height.Data[i] <= lake.Level + 0.001f);
                        if (result.LakeId[final.Receiver[i]] != id)
                            exits++;
                    }

                Assert.AreEqual(lake.Cells, cells);
                Assert.AreEqual(1, exits);
                Assert.That(result.Height[lake.OutletX, lake.OutletZ], Is.EqualTo(lake.Level).Within(0.001));
            }
        }

        [TestCase(0.35f, 1f, 0f)]
        [TestCase(0.6f, 2f, 1f)]
        public void ShapeTargetsSurviveRecipeVariations(float fraction, float elongation, float roughness)
        {
            IslandRecipe r = Tiny(17);
            r.landFraction = fraction;
            r.elongation = elongation;
            r.coastRoughness = roughness;
            r.orientationDegrees = 43;
            r.lakes = 0;
            IslandResult result = IslandPipeline.Run(r, BakeMode.Bake, null, null, CancellationToken.None);
            Valid(result);
            string marker = "\"simLandFractionAchieved\":";
            int start = result.ReportJson.IndexOf(marker, StringComparison.Ordinal) + marker.Length, end = result.ReportJson.IndexOf(',', start);
            double actual = double.Parse(result.ReportJson.Substring(start, end - start), System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(actual, Is.EqualTo(fraction).Within(0.5 / (r.simResolution * r.simResolution)));
        }

        [Test]
        public void WaterCanBeDisabled()
        {
            IslandRecipe r = Tiny();
            r.lakes = 0;
            r.riverDensity = 0;
            IslandResult result = IslandPipeline.Run(r, BakeMode.Preview, null, null, CancellationToken.None);
            Assert.IsTrue(result.Passed, result.ReportJson);
            Assert.AreEqual(0, result.Rivers.Count);
            Assert.AreEqual(0, result.Lakes.Count);
        }

        [Test]
        public void BeachShareControlsSandAndLegacyZeroSettingsResolve()
        {
            IslandRecipe recipe = Tiny(1);
            recipe.lakes = 0;
            recipe.riverDensity = 0;
            recipe.beachShare = 0;
            recipe.implicitTimeStep = 0;
            IslandResult rocky = IslandPipeline.Run(recipe, BakeMode.Bake, null, null, CancellationToken.None);
            Assert.That(rocky.Recipe.beachWidth, Is.GreaterThan(0));
            Assert.That(rocky.Recipe.channelHeadArea, Is.GreaterThan(0));
            Assert.That(rocky.Recipe.implicitTimeStep, Is.GreaterThan(0));
            recipe.beachShare = 1;
            IslandResult sandy = IslandPipeline.Run(recipe, BakeMode.Bake, null, null, CancellationToken.None);
            double rockySand = 0;
            double beachSand = 0;
            for (int i = 0; i < sandy.Height.Data.Length; i++)
            {
                if (sandy.Height.Data[i] <= 0)
                    continue;
                rockySand += rocky.Surface[SurfaceLayer.Sand].Data[i];
                beachSand += sandy.Surface[SurfaceLayer.Sand].Data[i];
            }

            Assert.AreEqual(0, rockySand);
            Assert.That(beachSand, Is.GreaterThan(10));
            Valid(rocky);
            Valid(sandy);
        }

        [Test]
        public void ParticleErosionIsAnOptionalBakePass()
        {
            IslandRecipe recipe = Tiny(1);
            recipe.lakes = 0;
            recipe.riverDensity = 0;
            IslandResult reference = IslandPipeline.Run(recipe, BakeMode.Bake, null, null, CancellationToken.None);
            recipe.particleErosion = true;
            recipe.erosionDropletsPerSquareMetre = 0;
            IslandResult zero = IslandPipeline.Run(recipe, BakeMode.Bake, null, null, CancellationToken.None);
            Assert.AreEqual(Hash(reference.Height.Data), Hash(zero.Height.Data));
            recipe.erosionDropletsPerSquareMetre = 0.15f;
            IslandResult eroded = IslandPipeline.Run(recipe, BakeMode.Bake, null, null, CancellationToken.None);
            Assert.AreNotEqual(Hash(reference.Height.Data), Hash(eroded.Height.Data));
            Valid(eroded);
            IslandResult preview = IslandPipeline.Run(recipe, BakeMode.Preview, null, null, CancellationToken.None);
            recipe.particleErosion = false;
            IslandResult plainPreview = IslandPipeline.Run(recipe, BakeMode.Preview, null, null, CancellationToken.None);
            Assert.AreEqual(Hash(preview.Height.Data), Hash(plainPreview.Height.Data));
        }

        [TestCase(-1f, 32f, 1f)]
        [TestCase(3000f, -1f, 1f)]
        [TestCase(3000f, 32f, -1f)]
        [TestCase(float.NaN, 32f, 1f)]
        public void NewPhysicalSettingsRejectInvalidValues(float area, float width, float timeStep)
        {
            IslandRecipe recipe = Tiny();
            recipe.channelHeadArea = area;
            recipe.beachWidth = width;
            recipe.implicitTimeStep = timeStep;
            Assert.Throws<ArgumentException>(() => IslandPipeline.Run(recipe, BakeMode.Bake, null, null, CancellationToken.None));
        }

        private static string Scratch()
        {
            return Path.Combine(Path.GetTempPath(), "procedural-checkpoints-" + Guid.NewGuid().ToString("N"));
        }

        private static bool Loaded(string report, string stage)
        {
            int start = report.IndexOf("\"name\":\"" + stage + "\"", StringComparison.Ordinal), end = report.IndexOf('}', start);
            return report.Substring(start, end - start).Contains("\"loadedFromCheckpoint\":true");
        }

        [Test]
        public void CheckpointsRoundTripAndInvalidateOnlyAffectedStages()
        {
            string dir = Scratch();
            try
            {
                IslandRecipe r = Tiny();
                IslandResult a = IslandPipeline.Run(r, BakeMode.Bake, dir, null, CancellationToken.None), b = IslandPipeline.Run(r, BakeMode.Bake, dir, null, CancellationToken.None);
                Valid(a);
                Valid(b);
                Assert.AreEqual(Hash(a.Height.Data), Hash(b.Height.Data));
                Assert.AreEqual(Hash(a.Discharge.Data), Hash(b.Discharge.Data));
                Assert.AreEqual(Hash(a.Wetness.Data), Hash(b.Wetness.Data));
                Assert.AreEqual(Hash(a.Moisture.Data), Hash(b.Moisture.Data));
                Assert.IsTrue(Loaded(b.ReportJson, "Shape"));
                Assert.IsTrue(Loaded(b.ReportJson, "Relief"));
                Assert.IsTrue(Loaded(b.ReportJson, "Upsample"));
                r.riverDensity *= 1.2f;
                IslandResult c = IslandPipeline.Run(r, BakeMode.Bake, dir, null, CancellationToken.None);
                Assert.IsTrue(Loaded(c.ReportJson, "Upsample"));
                r.beachShare = 0.8f;
                c = IslandPipeline.Run(r, BakeMode.Bake, dir, null, CancellationToken.None);
                Assert.IsTrue(Loaded(c.ReportJson, "Relief"));
                Assert.IsTrue(!Loaded(c.ReportJson, "Upsample"));
                r.channelHeadArea = c.Recipe.channelHeadArea * 1.5f;
                c = IslandPipeline.Run(r, BakeMode.Bake, dir, null, CancellationToken.None);
                Assert.IsTrue(Loaded(c.ReportJson, "Shape"));
                Assert.IsFalse(Loaded(c.ReportJson, "Relief"));
                r.particleErosion = true;
                c = IslandPipeline.Run(r, BakeMode.Bake, dir, null, CancellationToken.None);
                Assert.IsTrue(Loaded(c.ReportJson, "Upsample"));
                string[] files = Directory.GetFiles(dir, "1-*.bin");
                File.WriteAllBytes(files[0], new byte[] { 1, 2, 3 });
                c = IslandPipeline.Run(r, BakeMode.Bake, dir, null, CancellationToken.None);
                Assert.IsTrue(!Loaded(c.ReportJson, "Shape"));
                Valid(c);
                r.beachWidth = c.Recipe.beachWidth * 1.2f;
                c = IslandPipeline.Run(r, BakeMode.Bake, dir, null, CancellationToken.None);
                Assert.IsFalse(Loaded(c.ReportJson, "Shape"));
                r.peakHeight *= 0.95f;
                c = IslandPipeline.Run(r, BakeMode.Bake, dir, null, CancellationToken.None);
                Assert.IsFalse(Loaded(c.ReportJson, "Shape"));
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }

        private sealed class CancelProgress : IProgress<PipelineProgress>
        {
            internal CancellationTokenSource Source;
            public void Report(PipelineProgress p)
            {
                if (p.Stage == "Relief" && p.Fraction > 0)
                    Source.Cancel();
            }
        }

        [Test]
        public void CancellationInterruptsRelief()
        {
            using (var source = new CancellationTokenSource())
            {
                bool canceled = false;
                try
                {
                    IslandPipeline.Run(Tiny(), BakeMode.Bake, null, new CancelProgress { Source = source }, source.Token);
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                }

                Assert.IsTrue(canceled);
            }
        }

        [Test]
        public void ConcurrentRunsHaveIndependentState()
        {
            IslandResult a = null, b = null;
            Exception error = null;
            var thread = new Thread(() =>
            {
                try
                {
                    a = IslandPipeline.Run(Tiny(), BakeMode.Preview, null, null, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            thread.Start();
            b = IslandPipeline.Run(Tiny(), BakeMode.Preview, null, null, CancellationToken.None);
            thread.Join();
            Assert.IsTrue(error == null, error == null ? "" : error.ToString());
            Assert.AreEqual(Hash(a.Height.Data), Hash(b.Height.Data));
            Assert.AreEqual(Hash(a.Wetness.Data), Hash(b.Wetness.Data));
            Assert.AreEqual(Hash(a.Moisture.Data), Hash(b.Moisture.Data));
        }

        [Test]
        [Explicit]
        public void SeedSweep()
        {
            for (int seed = 1; seed <= 32; seed++)
            {
                foreach (BakeMode mode in new[]
                {
                    BakeMode.Preview,
                    BakeMode.Bake
                }

                )
                {
                    IslandRecipe r = Tiny(seed);
                    IslandResult result = IslandPipeline.Run(r, mode, null, null, CancellationToken.None);
                    Valid(result);
                    ValidLakeSites(result);
                }
            }
        }

        [Test]
        [Explicit]
        public void TimingSmallPreviewAndBake()
        {
            foreach (BakeMode mode in new[]
            {
                BakeMode.Preview,
                BakeMode.Bake
            }

            )
            {
                var clock = Stopwatch.StartNew();
                IslandResult result = IslandPipeline.Run(new IslandRecipe { seed = 7 }, mode, null, null, CancellationToken.None);
                clock.Stop();
                Assert.That(clock.Elapsed.TotalSeconds, Is.LessThanOrEqualTo(mode == BakeMode.Preview ? 3 : 20));
                Console.WriteLine(mode + " total ms=" + clock.Elapsed.TotalMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
                Console.WriteLine(result.ReportJson);
                Valid(result);
            }
        }
    }
}

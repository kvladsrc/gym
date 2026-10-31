using System;
using System.Threading;
using NUnit.Framework;
using Procedural.Generation;

namespace Procedural.Tests
{
    public class LandformTests
    {
        [Test]
        public void DescendingRouteMatchesFullFloodAndFallsBackForPits()
        {
            const int width = 17;
            var grid = new Grid(width, width, 2);
            var ocean = new bool[width * width];
            for (int z = 0; z < width; z++)
                for (int x = 0; x < width; x++)
                {
                    ocean[z * width + x] = x == 0 || z == 0 || x == width - 1 || z == width - 1;
                    grid[x, z] = ocean[z * width + x] ? 0 : 10 + z * 0.37f + x * 0.19f;
                }

            for (int pit = 0; pit < 2; pit++)
            {
                if (pit == 1)
                    grid[8, 8] = 1;
                Drainage full = Hydrology.Route(grid, ocean, CancellationToken.None, null, 0.05f);
                Drainage direct = Hydrology.Route(grid, ocean, CancellationToken.None, null, 0.05f, true);
                CollectionAssert.AreEqual(full.Fill, direct.Fill);
                CollectionAssert.AreEqual(full.Receiver, direct.Receiver);
                CollectionAssert.AreEqual(full.Distance, direct.Distance);
                CollectionAssert.AreEqual(full.Discharge, direct.Discharge);
                CollectionAssert.AreEqual(full.Stack, direct.Stack);
            }
        }

        [TestCase(1)]
        [TestCase(7)]
        public void StreamPowerGrowthSettlesWithoutUpdateBound(int seed)
        {
            var recipe = TerrainStages.Resolve(new IslandRecipe { seed = seed, worldSize = 256, simResolution = 64, heightmapResolution = 129, peakHeight = 45 });
            var state = new TerrainState();
            TerrainStages.Shape(recipe, state, CancellationToken.None);
            TerrainStages.Relief(recipe, BakeMode.Bake, state, CancellationToken.None, _ =>
            {
            });
            Assert.That(state.Iterations, Is.LessThan(250), "Must settle before exhausting the budget.");
            Assert.That(state.Changed, Is.LessThan(0.01f));
            Assert.That(state.ChangedAll, Is.LessThan(0.01f));
            Assert.That(state.FixedPointResidual, Is.LessThan(0.001f));
        }

        [Test]
        public void NearEqualSpillHeightsKeepOnePhysicalReservoir()
        {
            // A real dam on a valley whose cross-sections differ by several float
            // ulps. Check the labelled basin against an independent final flood.
            const int width = 129;
            var grid = new Grid(width, width, 1);
            var ocean = new bool[width * width];
            for (int z = 0; z < width; z++)
                for (int x = 0; x < width; x++)
                {
                    int i = z * width + x;
                    ocean[i] = x == 0 || z == 0 || x == width - 1 || z == width - 1;
                    double lateral = x - width / 2;
                    grid.Data[i] = ocean[i] ? 0 : (float)(5 + z * 0.08 + lateral * lateral * 0.08 + (i % 3 - 1) * 0.000003);
                }

            var recipe = TerrainStages.Resolve(new IslandRecipe { worldSize = 128, simResolution = 64, heightmapResolution = 129, channelHeadArea = 100, lakes = 1, peakHeight = 45 });
            var state = new TerrainState
            {
                Height = grid,
                Ocean = ocean
            };
            var water = new WaterState();
            WaterStages.Lakes(recipe, state, water, CancellationToken.None);
            Assert.That(water.Lakes.Count, Is.EqualTo(1));
            Drainage final = Hydrology.Route(grid, ocean, CancellationToken.None);
            foreach (Lake lake in water.Lakes)
            {
                int exits = 0;
                for (int i = 0; i < water.LakeId.Length; i++)
                    if (water.LakeId[i] == lake.Id)
                    {
                        Assert.That(final.Fill[i], Is.EqualTo(lake.Level).Within(0.001));
                        if (water.LakeId[final.Receiver[i]] != lake.Id)
                            exits++;
                    }

                Assert.That(exits, Is.EqualTo(1));
                Assert.That(grid[lake.OutletX, lake.OutletZ], Is.EqualTo(lake.Level).Within(0.001));
            }
        }

        [Test]
        public void ExactEdtMatchesBruteForceOnRectangularField()
        {
            const int width = 23, height = 17;
            var source = new bool[width * height];
            source[2 * width + 3] = true;
            source[13 * width + 20] = true;
            source[5 * width + 14] = true;
            float[] distances = EuclideanDistance.Transform(source, width, height);
            for (int i = 0; i < source.Length; i++)
            {
                double expected = double.PositiveInfinity;
                for (int j = 0; j < source.Length; j++)
                {
                    if (!source[j])
                        continue;
                    int dx = i % width - j % width, dz = i / width - j / width;
                    expected = Math.Min(expected, Math.Sqrt(dx * dx + dz * dz));
                }

                Assert.That(distances[i], Is.EqualTo(expected).Within(1e-5));
            }
        }

        [Test]
        public void EdtHasCircularContoursAndHandlesEmptySource()
        {
            var source = new bool[81];
            foreach (float value in EuclideanDistance.Transform(source, 9, 9))
                Assert.IsTrue(float.IsPositiveInfinity(value));
            source[40] = true;
            float[] distances = EuclideanDistance.Transform(source, 9, 9);
            Assert.That(distances[4 * 9 + 8], Is.EqualTo(4));
            Assert.That(distances[7 * 9 + 8], Is.EqualTo(5));
            Assert.That(distances[8 * 9 + 7], Is.EqualTo(5));
            Assert.Throws<ArgumentException>(() => EuclideanDistance.Transform(source, 8, 9));
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                Assert.Throws<OperationCanceledException>(() => EuclideanDistance.Transform(source, 9, 9, cancel.Token));
            }
        }

        [Test]
        public void DamRaisesRidgeAndFloodsExistingUpstreamContoursWithoutDigging()
        {
            const int width = 129;
            var grid = new Grid(width, width, 1);
            var ocean = new bool[width * width];
            for (int z = 0; z < width; z++)
                for (int x = 0; x < width; x++)
                {
                    ocean[z * width + x] = x == 0 || z == 0 || x == width - 1 || z == width - 1;
                    float lateral = x - width / 2;
                    grid[x, z] = ocean[z * width + x] ? 0 : 5 + z * 0.08f + lateral * lateral * 0.08f;
                }

            float[] original = (float[])grid.Data.Clone();
            Drainage initial = Hydrology.Route(grid, ocean, CancellationToken.None);
            var recipe = new IslandRecipe
            {
                worldSize = 128,
                maxRiverDepth = 1.5f
            };
            int site = 64 * width + 64;
            Assert.IsTrue(WaterStages.BuildDam(recipe, grid, ocean, initial, site, 17, CancellationToken.None));
            Assert.That(grid.Data[site] - original[site], Is.GreaterThanOrEqualTo(2));
            Assert.That(grid[64, 60], Is.EqualTo(grid[64, 68]).Within(0.001), "Both longitudinal shoulders must use the original site height, even after the crest cell is raised.");
            int raised = 0;
            for (int i = 0; i < original.Length; i++)
            {
                Assert.That(grid.Data[i], Is.GreaterThanOrEqualTo(original[i]));
                if (grid.Data[i] > original[i])
                    raised++;
            }

            Assert.That(raised, Is.GreaterThan(20));
            Drainage flooded = Hydrology.Route(grid, ocean, CancellationToken.None);
            int upstreamWet = 0;
            for (int z = 65; z < width - 1; z++)
                for (int x = 1; x < width - 1; x++)
                {
                    int i = z * width + x;
                    if (flooded.Fill[i] - grid.Data[i] > 0.1f)
                        upstreamWet++;
                }

            Assert.That(upstreamWet, Is.GreaterThan(20));
        }

        [Test]
        public void CurveCarvingUsesEuclideanSegmentDistanceAndParabolicSection()
        {
            var grid = new Grid(65, 65, 1);
            var ocean = new bool[grid.Data.Length];
            var water = new WaterState
            {
                LakeId = new int[grid.Data.Length],
                RimFloor = new float[grid.Data.Length],
                BankTarget = new float[grid.Data.Length],
                Corridor = new bool[grid.Data.Length],
                Floodplain = new bool[grid.Data.Length]
            };
            for (int i = 0; i < grid.Data.Length; i++)
            {
                grid.Data[i] = 10;
                water.LakeId[i] = -1;
                water.RimFloor[i] = water.BankTarget[i] = float.MinValue;
            }

            var river = new RiverSegment
            {
                X = new[]
                {
                    10.25f,
                    40.75f
                },
                Z = new[]
                {
                    18.5f,
                    27.25f
                },
                Surface = new[]
                {
                    5f,
                    4f
                },
                Width = new[]
                {
                    6f,
                    6f
                },
                Depth = new[]
                {
                    1f,
                    1f
                },
                Discharge = new[]
                {
                    1f,
                    1f
                }
            };
            var recipe = new IslandRecipe
            {
                channelHeadArea = 100
            };
            WaterStages.CarveCurve(recipe, river, 0, grid, ocean, water, CancellationToken.None);
            const float x = 25, z = 24, dx = 30.5f, dz = 8.75f;
            float t = ((x - 10.25f) * dx + (z - 18.5f) * dz) / (dx * dx + dz * dz);
            float ex = x - 10.25f - t * dx, ez = z - 18.5f - t * dz;
            float expected = 5 - t - 1 + 1.15f * (ex * ex + ez * ez) / 9;
            Assert.That(grid[25, 24], Is.EqualTo(expected).Within(1e-5));
            Assert.IsTrue(water.Corridor[24 * 65 + 25]);
            Assert.AreEqual(10, grid[25, 40]);
        }

        [Test]
        public void CurveCarvingDoesNotAccumulateOverlappingSpanBlends()
        {
            const int width = 65;
            var grid = new Grid(width, width, 1);
            var water = new WaterState
            {
                LakeId = new int[width * width],
                RimFloor = new float[width * width],
                BankTarget = new float[width * width],
                CarveBase = new float[width * width],
                Corridor = new bool[width * width],
                Floodplain = new bool[width * width]
            };
            for (int i = 0; i < grid.Data.Length; i++)
            {
                grid.Data[i] = water.CarveBase[i] = 10;
                water.LakeId[i] = -1;
                water.RimFloor[i] = water.BankTarget[i] = float.MinValue;
            }

            var river = new RiverSegment
            {
                X = new[]
                {
                    10f,
                    50f
                },
                Z = new[]
                {
                    30f,
                    30f
                },
                Surface = new[]
                {
                    5f,
                    5f
                },
                Width = new[]
                {
                    6f,
                    6f
                },
                Depth = new[]
                {
                    1f,
                    1f
                },
                Discharge = new[]
                {
                    200f,
                    200f
                }
            };
            var recipe = new IslandRecipe
            {
                channelHeadArea = 100
            };
            var ocean = new bool[width * width];
            WaterStages.CarveCurve(recipe, river, 0, grid, ocean, water, CancellationToken.None);
            float[] once = (float[])grid.Data.Clone();
            for (int repeat = 0; repeat < 8; repeat++)
                WaterStages.CarveCurve(recipe, river, 0, grid, ocean, water, CancellationToken.None);
            CollectionAssert.AreEqual(once, grid.Data, "Sub-cell span overlap must not deepen or widen the bank blend.");
            Assert.That(grid[30, 58], Is.GreaterThan(5.41f).And.LessThan(10), "Outer floodplain retains its smooth blend.");
        }

        [Test]
        public void DiffusionPreservesInteriorPlanesAndRemovesGridCorrugation()
        {
            const int width = 65;
            var plane = new float[width * width];
            for (int z = 0; z < width; z++)
                for (int x = 0; x < width; x++)
                    plane[z * width + x] = 20 + x * 0.4f + z * 0.2f + (x % 2 == 0 ? 0.25f : -0.25f);
            float[] smoothed = TerrainStages.Blur(plane, width, width, 2, 3, CancellationToken.None);
            for (int z = 10; z < width - 10; z++)
                for (int x = 10; x < width - 10; x++)
                    Assert.That(smoothed[z * width + x], Is.EqualTo(20 + x * 0.4f + z * 0.2f).Within(0.003));
            Assert.Throws<System.OperationCanceledException>(() => TerrainStages.Blur(plane, width, width, 2, 3, new CancellationToken(true)));
        }

        [Test]
        public void RoutingRetainsItsOceanSnapshotAfterCoastalCarving()
        {
            var grid = new Grid(9, 9, 1);
            var ocean = new bool[81];
            for (int z = 0; z < 9; z++)
                for (int x = 0; x < 9; x++)
                {
                    ocean[z * 9 + x] = x == 0 || x == 8 || z == 0 || z == 8;
                    grid[x, z] = ocean[z * 9 + x] ? 0 : 1;
                }

            Drainage drainage = Hydrology.Route(grid, ocean, CancellationToken.None);
            ocean[10] = true;
            Assert.IsFalse(drainage.Ocean[10], "Extraction catchments must retain their pre-carving coastline.");
        }

        [Test]
        public void MainStemFitUsesLargestAreaDonorAndReportsUnclippedExponent()
        {
            // An intentionally linear chain has L proportional to A, hence h=1.
            // It must not be coerced into the acceptance interval.
            const int count = 100;
            var grid = new Grid(11, 10, 1);
            var drainage = new Drainage
            {
                Stack = new int[count],
                Receiver = new int[110],
                Ocean = new bool[110],
                Discharge = new float[110],
                Length = new float[110],
                DonorHead = new int[110],
                DonorNext = new int[110]
            };
            for (int i = 0; i < 110; i++)
                drainage.DonorHead[i] = drainage.DonorNext[i] = -1;
            drainage.Ocean[100] = true;
            for (int i = 0; i < count; i++)
            {
                drainage.Stack[i] = i;
                drainage.Receiver[i] = i == 0 ? 100 : i - 1;
                drainage.Discharge[i] = drainage.Length[i] = count - i;
                if (i + 1 < count)
                    drainage.DonorHead[i] = i + 1;
            }

            var report = new ReportData();
            MainStemMetrics.Measure(drainage, grid, report, CancellationToken.None);
            Assert.AreEqual(1, report.MainStems.Count);
            // 100/16 is less than one decade: this basin is retained with a null fit.
            Assert.IsTrue(double.IsNaN(report.MainStemHack));
            for (int i = 0; i < count; i++)
                drainage.Discharge[i] = drainage.Length[i] = (count - i) * 10;
            report = new ReportData();
            MainStemMetrics.Measure(drainage, grid, report, CancellationToken.None);
            Assert.That(report.MainStemHack, Is.EqualTo(1).Within(1e-8));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Procedural.Generation;

namespace Procedural.Tests
{
    public class MoistureTests
    {
        [Test]
        public void MfdSplitsToEveryLowerNeighbourWithFreemanWeights()
        {
            var g = new Grid(3, 3, 2);
            g[1, 1] = 2;
            var ocean = new bool[9];
            Array.Fill(ocean, true);
            ocean[4] = false;
            Drainage d = Hydrology.Route(g, ocean, CancellationToken.None);
            double[] area = WetnessStages.Catchment(g, d, CancellationToken.None);
            double cardinal = Math.Pow(1, 1.1), diagonal = Math.Pow(1 / Math.Sqrt(2), 1.1);
            double total = 4 * (cardinal + diagonal), outlets = 0;
            for (int i = 0; i < 9; i++)
                if (i != 4)
                {
                    double expected = 4 * (i % 2 == 1 ? cardinal : diagonal) / total;
                    Assert.That(area[i], Is.EqualTo(expected).Within(1e-12));
                    outlets += area[i];
                }

            Assert.That(outlets, Is.EqualTo(4).Within(1e-12));
            // MFD does not mutate the channel receiver or its single-flow Q.
            Assert.That(d.Discharge[d.Receiver[4]], Is.EqualTo(4));
        }

        [Test]
        public void PlanarTwiIsUniformAlongContoursAndGrowsLogarithmicallyDownslope()
        {
            var g = new Grid(129, 33, 2);
            var ocean = new bool[g.Data.Length];
            for (int z = 0; z < g.Height; z++)
                for (int x = 0; x < g.Width; x++)
                {
                    g[x, z] = z * 0.5f;
                    ocean[z * g.Width + x] = z == 0;
                }

            double[] area = WetnessStages.Catchment(g, Hydrology.Route(g, ocean, CancellationToken.None), CancellationToken.None);
            float[] twi = WetnessStages.Twi(g, area, CancellationToken.None);
            for (int z = 2; z < 31; z++)
                for (int x = 40; x < 89; x++)
                {
                    double expected = Math.Log((g.Height - z) * g.CellSize / 0.25);
                    Assert.That(twi[z * g.Width + x], Is.EqualTo(expected).Within(1e-6));
                }

            Assert.That(twi[2 * g.Width + 64], Is.GreaterThan(twi[20 * g.Width + 64]));
        }

        [Test]
        public void ValleyIsWetterThanItsRidges()
        {
            var g = new Grid(65, 65, 2);
            var ocean = new bool[g.Data.Length];
            for (int z = 0; z < g.Height; z++)
                for (int x = 0; x < g.Width; x++)
                {
                    ocean[z * g.Width + x] = z == 0;
                    g[x, z] = z == 0 ? 0 : (float)(5 + 0.1 * z + 0.02 * (x - 32) * (x - 32));
                }

            double[] area = WetnessStages.Catchment(g, Hydrology.Route(g, ocean, CancellationToken.None), CancellationToken.None);
            float[] twi = WetnessStages.Twi(g, area, CancellationToken.None);
            Assert.That(twi[20 * 65 + 32], Is.GreaterThan(twi[20 * 65 + 4] + 2));
            Assert.That(twi[20 * 65 + 32], Is.GreaterThan(twi[20 * 65 + 60] + 2));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void MfdConservesRainThroughRoughTerrainFlatsAndFilledPits(int shape)
        {
            var g = new Grid(25, 19, 1.7f);
            var ocean = new bool[g.Data.Length];
            int land = 0;
            for (int z = 0; z < g.Height; z++)
                for (int x = 0; x < g.Width; x++)
                {
                    int i = z * g.Width + x;
                    ocean[i] = x == 0 || z == 0 || x == g.Width - 1 || z == g.Height - 1;
                    g.Data[i] = ocean[i] ? 0 : shape == 0 ? 1 + 5 * MathCore.Noise(x * 0.2f, z * 0.2f, 123) : shape == 1 ? 5 : 1 + (x - 12) * (x - 12) * 0.1f + (z - 9) * (z - 9) * 0.1f;
                    if (!ocean[i])
                        land++;
                }

            Drainage d = Hydrology.Route(g, ocean, CancellationToken.None);
            double[] area = WetnessStages.Catchment(g, d, CancellationToken.None);
            double outlets = 0;
            for (int i = 0; i < area.Length; i++)
                if (ocean[i])
                    outlets += area[i];
            Assert.That(outlets, Is.EqualTo(land * (double)g.CellSize * g.CellSize).Within(1e-8));
        }

        [Test]
        public void TwiUsesPhysicalSlopeAndHalfDegreeFloor()
        {
            var g = new Grid(7, 7, 2);
            var area = new double[49];
            Array.Fill(area, 8);
            float[] flat = WetnessStages.Twi(g, area, CancellationToken.None);
            Assert.That(flat[24], Is.EqualTo(Math.Log(4 / Math.Tan(Math.PI / 360))).Within(1e-6));
            for (int z = 0; z < 7; z++)
                for (int x = 0; x < 7; x++)
                    g[x, z] = 2 * x;
            Assert.That(WetnessStages.Twi(g, area, CancellationToken.None)[24], Is.EqualTo(Math.Log(4)).Within(1e-6));
        }

        [Test]
        public void WaterDistanceUsesBanksTapersLakeBoundariesAndUnboundedSearch()
        {
            var bank = new WaterProximity.Bank(0, 0, 10, 0, 1, 3);
            // At the interior tangent of a linear radius ramp, the closest disc
            // centre differs from the nearest centreline projection.
            Assert.That(bank.Distance(5, 10), Is.EqualTo(10 * Math.Sqrt(0.96) - 2).Within(1e-12));
            var tree = new WaterProximity(new[] { bank });
            Assert.That(tree.Distance(5, 10), Is.EqualTo(bank.Distance(5, 10)));
            Assert.That(new WaterProximity.Bank(0, 0, 10, 0, 2, 2).Distance(5, 7), Is.EqualTo(5).Within(1e-12));
            Assert.That(new WaterProximity.Bank(0, 0, 0, 0, 2, 3).Distance(0, 8), Is.EqualTo(5).Within(1e-12));
            Assert.That(tree.Distance(5000, 5000), Is.GreaterThan(7000));
            var g = new Grid(5, 5, 2);
            var lakes = new int[25];
            Array.Fill(lakes, -1);
            lakes[12] = 0;
            float[] distance = WaterProximity.Distances(g, new List<RiverSegment>(), lakes, CancellationToken.None);
            Assert.That(distance[12], Is.EqualTo(0));
            Assert.That(distance[13], Is.EqualTo(1));
            Assert.That(distance[18], Is.EqualTo(Math.Sqrt(2)).Within(1e-6));
            lakes[12] = -1;
            Assert.That(WaterProximity.Distances(g, new List<RiverSegment>(), lakes, CancellationToken.None)[12], Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void WaterBvhMatchesExhaustivePrimitiveSearch()
        {
            var banks = new WaterProximity.Bank[51];
            for (int k = 0; k < banks.Length; k++)
                banks[k] = new WaterProximity.Bank(k * 3, k % 7 * 4, k * 3 + 5, k % 11 * 3, k % 3, k % 5);
            var tree = new WaterProximity(banks);
            for (int z = -7; z <= 57; z += 4)
                for (int x = -11; x <= 177; x += 7)
                {
                    double expected = double.PositiveInfinity;
                    foreach (var bank in banks)
                        expected = Math.Min(expected, bank.Distance(x, z));
                    Assert.That(tree.Distance(x, z), Is.EqualTo(expected).Within(1e-12));
                }
        }

        [Test]
        public void MoistureStageHonoursCancellation()
        {
            var g = new Grid(3, 3, 1);
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                Assert.Throws<OperationCanceledException>(() => WetnessStages.Twi(g, new double[9], source.Token));
                Assert.Throws<OperationCanceledException>(() => WaterProximity.Distances(g, new List<RiverSegment>(), new int[9], source.Token));
            }
        }
    }
}

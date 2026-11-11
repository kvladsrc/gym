using System;
using NUnit.Framework;
using Realms.Core;

namespace Realms.Tests
{
    public class HexTests
    {
        [Test]
        public void OffsetCoordinatesRoundTrip()
        {
            for (int row = -3; row < 12; row++)
            for (int col = -3; col < 12; col++)
            {
                Hex hex = Hex.FromOffset(col, row);
                Assert.AreEqual(col, hex.Col, $"col of ({col}, {row})");
                Assert.AreEqual(row, hex.Row, $"row of ({col}, {row})");
            }
        }

        [Test]
        public void NeighboursLieAcrossTheirEdge()
        {
            var hex = Hex.FromOffset(5, 3);
            float x = HexMetrics.CenterX(hex), z = HexMetrics.CenterZ(hex);
            for (int d = 0; d < 6; d++)
            {
                Hex next = hex.Neighbor(d);
                Assert.AreEqual(1, Hex.Distance(hex, next));
                // The neighbour's centre is at 60·d degrees, twice the distance to the edge's middle,
                // and the middle of edge d (corners d and d + 1) is on that line.
                float angle = 60f * d * MathF.PI / 180f;
                float dx = HexMetrics.CenterX(next) - x, dz = HexMetrics.CenterZ(next) - z;
                Assert.AreEqual(2 * HexMetrics.InnerRadius * MathF.Cos(angle), dx, 1e-4);
                Assert.AreEqual(2 * HexMetrics.InnerRadius * MathF.Sin(angle), dz, 1e-4);
                float mx = (HexMetrics.CornerX[d] + HexMetrics.CornerX[(d + 1) % 6]) / 2;
                float mz = (HexMetrics.CornerZ[d] + HexMetrics.CornerZ[(d + 1) % 6]) / 2;
                Assert.AreEqual(dx / 2, mx, 1e-4);
                Assert.AreEqual(dz / 2, mz, 1e-4);
            }
        }

        [Test]
        public void WorldPointsFindTheirHexagon()
        {
            for (int row = 0; row < 6; row++)
            for (int col = 0; col < 6; col++)
            {
                var hex = Hex.FromOffset(col, row);
                float x = HexMetrics.CenterX(hex), z = HexMetrics.CenterZ(hex);
                Assert.AreEqual(hex, HexMetrics.FromWorld(x, z));
                for (int d = 0; d < 6; d++)
                {
                    float angle = 60f * d * MathF.PI / 180f;
                    float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
                    float inside = 0.95f * HexMetrics.InnerRadius, outside = 1.05f * HexMetrics.InnerRadius;
                    Assert.AreEqual(hex, HexMetrics.FromWorld(x + inside * cos, z + inside * sin));
                    Assert.AreEqual(hex.Neighbor(d), HexMetrics.FromWorld(x + outside * cos, z + outside * sin));
                }
            }
        }
    }
}

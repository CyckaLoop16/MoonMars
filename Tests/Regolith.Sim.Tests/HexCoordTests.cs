using System.Linq;
using NUnit.Framework;
using Regolith.Sim;

namespace Regolith.Sim.Tests
{
    public class HexCoordTests
    {
        [Test]
        public void NeighborsAreAllDistanceOne()
        {
            var origin = new HexCoord(3, -2);
            var neighbors = origin.Neighbors().ToList();

            Assert.That(neighbors, Has.Count.EqualTo(6));
            Assert.That(neighbors.Distinct().Count(), Is.EqualTo(6));
            Assert.That(neighbors.All(n => origin.DistanceTo(n) == 1));
        }

        [TestCase(0, 0, 0, 0, 0)]
        [TestCase(0, 0, 3, 0, 3)]
        [TestCase(0, 0, 2, -5, 5)]
        [TestCase(-1, 4, 2, 1, 3)]
        public void Distance(int q1, int r1, int q2, int r2, int expected)
        {
            Assert.That(new HexCoord(q1, r1).DistanceTo(new HexCoord(q2, r2)), Is.EqualTo(expected));
        }

        [Test]
        public void OffsetRoundTrips()
        {
            for (int row = 0; row < 12; row++)
            for (int col = 0; col < 12; col++)
            {
                var (c, r) = HexCoord.FromOffset(col, row).ToOffset();
                Assert.That((c, r), Is.EqualTo((col, row)));
            }
        }

        [Test]
        public void PlaneRoundTrips()
        {
            for (int q = -6; q <= 6; q++)
            for (int r = -6; r <= 6; r++)
            {
                var hex = new HexCoord(q, r);
                var (x, y) = HexLayout.ToPlane(hex, 1.0);
                Assert.That(HexLayout.FromPlane(x, y, 1.0), Is.EqualTo(hex));

                // Points well inside the hex resolve to it too.
                var (cx, cy) = HexLayout.CornerOffset(2, 1.0);
                Assert.That(HexLayout.FromPlane(x + cx * 0.8, y + cy * 0.8, 1.0), Is.EqualTo(hex));
            }
        }
    }
}

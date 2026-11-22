using Game.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Game.Terrain.Tests
{
    public sealed class WorldZoneLayoutTests
    {
        [TestCase(40, 216, 9)]
        [TestCase(128, 216, 2)]
        [TestCase(216, 216, 6)]
        [TestCase(40, 128, 5)]
        [TestCase(128, 128, 1)]
        [TestCase(216, 128, 3)]
        [TestCase(40, 40, 8)]
        [TestCase(128, 40, 4)]
        [TestCase(216, 40, 7)]
        public void NorthPositiveZUsesApprovedMatrix(float x, float z, int expected)
        {
            var layout = new WorldZoneLayout(256, 256, 1f, Vector3.zero);
            Assert.That(layout.ZoneAt(new Vector3(x, 0, z)), Is.EqualTo(expected));
        }

        [Test]
        public void RegionOneIs96SquaredAndBoundsAreHalfOpen()
        {
            var layout = new WorldZoneLayout(256, 256, 1f, Vector3.zero);
            Assert.That(layout.Bounds(1), Is.EqualTo(new Rect(80, 80, 96, 96)));
            Assert.That(layout.ZoneAt(new Vector3(79.999f, 0, 128)), Is.EqualTo(5));
            Assert.That(layout.ZoneAt(new Vector3(80, 0, 128)), Is.EqualTo(1));
            Assert.That(layout.ZoneAt(new Vector3(175.999f, 0, 128)), Is.EqualTo(1));
            Assert.That(layout.ZoneAt(new Vector3(176, 0, 128)), Is.EqualTo(3));
            Assert.That(layout.ZoneAt(new Vector3(256, 0, 128)), Is.Zero);
            Assert.That(layout.ZoneAt(new Vector3(-.001f, 0, 128)), Is.Zero);
            Assert.That(layout.ZoneAt(new Vector3(float.NaN, 0, 128)), Is.Zero);
        }

        [Test]
        public void SmallTranslatedRectangularWorldScalesTheLayout()
        {
            var layout = new WorldZoneLayout(32, 64, .5f, new Vector3(-10, 3, 20));
            Assert.That(layout.Bounds(1), Is.EqualTo(new Rect(-5, 30, 6, 12)));
            Assert.That(layout.ZoneAt(new Vector3(-2, 100, 36)), Is.EqualTo(1));
            Assert.That(layout.IsMountain(new Vector3(-9.9f, 0, 36)), Is.True);
            Assert.That(layout.IsMountain(new Vector3(-2, 0, 36)), Is.False);
        }

        [Test]
        public void CardinalAdjacencyHasTwelveEdgesAndNoDiagonalCornerCut()
        {
            int edges = 0;
            for (int a = 1; a <= 9; a++)
            for (int b = a + 1; b <= 9; b++)
                if (WorldZoneLayout.AreAdjacent(a, b)) edges++;
            Assert.That(edges, Is.EqualTo(12));
            Assert.That(WorldZoneLayout.AreAdjacent(1, 6), Is.False);
            Assert.That(WorldZoneLayout.AreAdjacent(2, 3), Is.False);
            Assert.That(WorldZoneLayout.AreAdjacent(2, 6), Is.True);
            Assert.That(WorldZoneLayout.AreAdjacent(3, 6), Is.True);
        }

        [Test]
        public void BiomeIdsRemainIndependentOfPurchaseRegions()
        {
            var map = new HeightMap(256, 256);
            map.SetZone(128, 200, 3);
            Assert.That(map.GetZone(128, 200), Is.EqualTo(3));
            Assert.That(new WorldZoneLayout(256, 256, 1, Vector3.zero)
                .ZoneAt(new Vector3(128.5f, 0, 200.5f)), Is.EqualTo(2));
        }
    }
}

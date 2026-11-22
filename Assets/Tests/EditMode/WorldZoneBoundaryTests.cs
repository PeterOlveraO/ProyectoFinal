using Game.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Game.Terrain.Tests
{
    public sealed class WorldZoneBoundaryTests
    {
        sealed class Open : IWorldZoneAccess
        {
            public int Revision => 0;
            public int UnlockedMask => WorldZoneLayout.AllUnlockedMask;
            public bool IsUnlocked(int id) => id >= 1 && id <= 9;
        }

        [TestCase(12345)]
        [TestCase(777)]
        [TestCase(778)]
        public void ExpandedMapHasPermanentSixteenUnitBorderAndReachableNineGateRegions(int seed)
        {
            var settings = ScriptableObject.CreateInstance<TerrainSettings>();
            try
            {
                settings.enableWorldZones = true;
                settings.chunksX = settings.chunksZ = 16;
                settings.chunkSize = 16;
                settings.seed = seed;
                settings.maxLevel = 8;
                settings.lakeCount = 1;
                var map = HeightMapGenerator.Generate(settings);
                var nav = new WorldTraversal(map, settings, Vector3.zero, new Open());
                Assert.That(nav.Layout.MountainWidth, Is.EqualTo(16));
                Assert.That(map.IsMountainCell(15, 128), Is.True);
                Assert.That(map.IsMountainCell(16, 128), Is.False);
                Assert.That(map.Get(15, 128) * settings.stepHeight, Is.InRange(8f, 14f));
                nav.UpdateTarget(new Vector3(128.5f, .5f, 128.5f));
                for (int id = 1; id <= 9; id++)
                {
                    Rect bounds = nav.Layout.Bounds(id);
                    Vector3 centre = new Vector3(Mathf.Floor(bounds.center.x) + .5f, 0,
                        Mathf.Floor(bounds.center.y) + .5f);
                    Assert.That(nav.IsReachable(centre), Is.True, "Region " + id + " has no route.");
                    for (int slot = 0; slot < WorldZoneLayout.VendorCount(id); slot++)
                    {
                        Vector2Int offset = WorldZoneLayout.VendorOffset(id, slot);
                        int x = Mathf.FloorToInt(centre.x) + offset.x;
                        int z = Mathf.FloorToInt(centre.z) + offset.y;
                        int level = map.Get(x, z);
                        for (int nz = z - 1; nz <= z + 1; nz++)
                        for (int nx = x - 1; nx <= x + 1; nx++)
                            Assert.That(map.Get(nx, nz), Is.EqualTo(level), "Vendor needs a flat 3x3 footprint.");
                        Assert.That(nav.CanTraverse(centre, new Vector3(x + .5f, 0, z + .5f), .5f),
                            Is.True, "Vendor must have a straight, dry approach from its regional centre.");
                    }
                }
                for (int a = 1; a <= 9; a++)
                for (int b = a + 1; b <= 9; b++)
                {
                    if (!nav.Layout.TryGetGate(a, b, out Vector3 gate, out Vector3 direction)) continue;
                    Assert.That(nav.CanTraverse(gate - direction * 3f, gate + direction * 3f, .5f),
                        Is.True, "Gate " + a + "-" + b + " is blocked.");
                }
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void ExistingFixturesDoNotOptIntoMountainsOrExpandedTerrain()
        {
            var settings = ScriptableObject.CreateInstance<TerrainSettings>();
            try
            {
                Assert.That(settings.enableWorldZones, Is.False);
                settings.chunksX = settings.chunksZ = 2;
                settings.lakeCount = 0;
                var map = HeightMapGenerator.Generate(settings);
                for (int z = 0; z < map.Depth; z++)
                for (int x = 0; x < map.Width; x++)
                    Assert.That(map.IsMountainCell(x, z), Is.False);
            }
            finally { Object.DestroyImmediate(settings); }
        }
    }
}

using Game.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Game.Terrain.Tests
{
    public sealed class WorldTraversalTests
    {
        sealed class Access : IWorldZoneAccess
        {
            public int Revision { get; private set; }
            public int UnlockedMask { get; private set; } = WorldZoneLayout.AllUnlockedMask;
            public bool IsUnlocked(int id) => id >= 1 && id <= 9 && (UnlockedMask & (1 << (id - 1))) != 0;
            public void Set(int mask) { UnlockedMask = mask; Revision++; }
        }
        TerrainSettings _settings;
        HeightMap _map;
        Access _access;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<TerrainSettings>();
            _settings.blockSize = 1;
            _settings.stepHeight = .5f;
            _map = new HeightMap(32, 32);
            _access = new Access();
        }
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_settings);
        WorldTraversal Create() => new WorldTraversal(_map, _settings, Vector3.zero, _access);
        static Vector3 At(float x, float z) => new Vector3(x, 0, z);

        [Test]
        public void ClosedZonesRespectBodyRadiusAndSweptSegments()
        {
            _access.Set(1);
            var nav = Create();
            Assert.That(nav.CanOccupy(At(10.6f, 16), .5f), Is.True);
            Assert.That(nav.CanOccupy(At(10.4f, 16), .5f), Is.False);
            Assert.That(nav.CanTraverse(At(16, 16), At(23, 16), .5f), Is.False);
            Assert.That(nav.CanOccupyGeography(At(23, 16), .5f), Is.True);
        }

        [Test]
        public void MountainMaskCannotBeJumpedOrCrossedByFastMovement()
        {
            var nav = Create();
            Assert.That(nav.CanOccupy(new Vector3(1, 1000, 16)), Is.False);
            Assert.That(nav.CanTraverse(At(16, 16), At(40, 16)), Is.False);
            Assert.That(nav.CanProjectileTraverse(new Vector3(16, 200, 16), new Vector3(0, 200, 16)), Is.False);
            Assert.That(nav.CanOccupy(At(2.1f, 16), .5f), Is.False);
            Assert.That(nav.CanOccupy(At(2.6f, 16), .5f), Is.True);
        }

        [Test]
        public void LakeAndPropsBlockSweptMovementButDoNotChangeProjectileHeightRules()
        {
            _map.Lakes.Add(new LakeInfo { Center = new Vector2Int(16, 16), Radius = 2 });
            _map.Set(21, 16, 12);
            var nav = Create();
            nav.RebuildObstacles(new[] { new Bounds(At(25, 16), new Vector3(1, 4, 1)) });
            Assert.That(nav.CanTraverse(At(10, 16), At(20, 16), .4f), Is.False);
            Assert.That(nav.CanTraverse(At(23, 16), At(27, 16), .4f), Is.False);
            Assert.That(nav.CanProjectileTraverse(At(10, 16), At(27, 16)), Is.True);
            Assert.That(nav.CanOccupyGeography(At(25, 16), .4f), Is.True);
        }

        [Test]
        public void DiagonalCliffCornerIsNotAPath()
        {
            _map.Set(16, 15, 8);
            _map.Set(15, 16, 8);
            var nav = Create();
            Assert.That(nav.CanTraverse(At(15.5f, 15.5f), At(16.5f, 16.5f)), Is.False);
            Assert.That(nav.CanProjectileTraverse(At(15.5f, 15.5f), At(16.5f, 16.5f)), Is.True);
        }

        [Test]
        public void SharedFieldRoutesAroundTreeAndNeverReturnsOriginOnSpawnFailure()
        {
            var nav = Create();
            nav.RebuildObstacles(new[] { new Bounds(At(20.5f, 16.5f), new Vector3(1, 4, 3)) });
            Vector3 target = At(25.5f, 16.5f), position = At(16.5f, 16.5f);
            nav.UpdateTarget(target);
            Assert.That(nav.TryGetReachableSpawn(position, out Vector3 ground, .4f), Is.True);
            Assert.That(ground.x, Is.EqualTo(position.x));
            for (int steps = 0; steps < 80 && Vector3.Distance(position, target) > .01f; steps++)
            {
                Assert.That(nav.TryGetNextWaypoint(position, out Vector3 next), Is.True);
                Assert.That(nav.CanTraverse(position, next, .4f), Is.True);
                position = next;
            }
            Assert.That(Vector3.Distance(position, target), Is.LessThan(.01f));
            Assert.That(nav.TryGetReachableSpawn(At(20.5f, 16.5f), out _, .4f), Is.False);
            Assert.That(nav.TryGetReachableSpawn(At(-1, 16), out _, .4f), Is.False);
        }

        [Test]
        public void FieldRebuildsOnlyWhenTargetCellAccessOrObstaclesChange()
        {
            var nav = Create();
            nav.UpdateTarget(At(16.5f, 16.5f));
            int builds = nav.FieldBuildCount;
            for (int i = 0; i < 100; i++)
            {
                nav.UpdateTarget(At(16.6f, 16.6f));
                nav.DirectionToTarget(At(18.5f, 16.5f));
                nav.IsReachable(At(18.5f, 16.5f));
            }
            Assert.That(nav.FieldBuildCount, Is.EqualTo(builds));
            _access.Set(1);
            Assert.That(nav.IsReachable(At(18.5f, 16.5f)), Is.False, "Stale field cannot authorize a spawn.");
            nav.UpdateTarget(At(16.5f, 16.5f));
            Assert.That(nav.FieldBuildCount, Is.EqualTo(builds + 1));
            nav.RebuildObstacles(new[] { new Bounds(At(18.5f, 16.5f), Vector3.one) });
            Assert.That(nav.IsReachable(At(18.5f, 16.5f)), Is.False);
            nav.UpdateTarget(At(16.5f, 16.5f));
            Assert.That(nav.FieldBuildCount, Is.EqualTo(builds + 2));
        }

        [Test]
        public void DisconnectedPlateauRejectsSpawnEvenWithAllRegionsBought()
        {
            for (int z = 2; z < 30; z++) _map.Set(20, z, 12);
            var nav = Create();
            nav.UpdateTarget(At(16.5f, 16.5f));
            Assert.That(nav.IsReachable(At(24.5f, 16.5f)), Is.False);
            Assert.That(nav.TryGetReachableSpawn(At(24.5f, 16.5f), out _), Is.False);
        }
    }
}

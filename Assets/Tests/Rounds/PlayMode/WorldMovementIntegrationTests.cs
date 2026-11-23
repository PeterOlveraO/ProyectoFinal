using System.Collections;
using System.Reflection;
using Game.Core;
using Game.Combat;
using Game.Enemies;
using Game.Player;
using Game.Rounds;
using Game.Terrain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Rounds
{
    public sealed class WorldMovementIntegrationTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject _world, _playerRoot, _enemyRoot;
        TerrainSettings _settings;
        Material _terrainMaterial;
        WorldTerrain _terrain;
        PlayerSphereController _player;
        RunZoneUnlocks _access;
        bool _blocked, _over;

        static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Private).SetValue(target, value);
        static object Call(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Private).Invoke(target, args);

        [SetUp]
        public void Setup()
        {
            Assert.IsNull(WorldTerrain.Instance);
            Assert.IsNull(PlayerSphereController.Instance);
            _blocked = GameFlow.Blocked;
            _over = GameFlow.Over;
            GameFlow.Blocked = false;
            GameFlow.Over = false;
            _settings = ScriptableObject.CreateInstance<TerrainSettings>();
            _terrainMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _settings.terrainMaterial = _terrainMaterial;
            _settings.chunksX = _settings.chunksZ = 4;
            _settings.chunkSize = 16;
            _settings.flatRadius = 1000;
            _settings.flatLevel = 1;
            _settings.lakeCount = 0;
            _settings.enableWorldZones = true;
            _settings.loadRadius = 0;
            _world = new GameObject("MovementWorld");
            _world.SetActive(false);
            _terrain = _world.AddComponent<WorldTerrain>();
            Set(_terrain, "settings", _settings);
            _world.SetActive(true);
            _access = new RunZoneUnlocks();
            _terrain.Traversal.SetZoneAccess(_access);
            _playerRoot = new GameObject("MovementPlayer");
            var controller = _playerRoot.AddComponent<CharacterController>();
            controller.height = 1;
            controller.radius = .5f;
            _player = _playerRoot.AddComponent<PlayerSphereController>();
            _player.enabled = false;
            PlacePlayer(new Vector3(43, 0, 32.5f));
        }

        void PlacePlayer(Vector3 point)
        {
            Assert.IsTrue(_terrain.TryGetWalkablePosition(point, out Vector3 ground));
            Call(_player, "Teleport", ground + Vector3.up * .58f);
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            AttackProjectile.ClearAll();
            foreach (var burst in Object.FindObjectsByType<HitBurst>(FindObjectsInactive.Include))
                Object.Destroy(burst.gameObject);
            if (_enemyRoot != null) Object.Destroy(_enemyRoot);
            if (_playerRoot != null) Object.Destroy(_playerRoot);
            if (_world != null) Object.Destroy(_world);
            yield return null;
            if (_settings != null) Object.Destroy(_settings);
            if (_terrainMaterial != null) Object.Destroy(_terrainMaterial);
            GameFlow.Blocked = _blocked;
            GameFlow.Over = _over;
        }

        [UnityTest]
        public IEnumerator PlayerSweptMovementRespectsLockedFootprintAndPermanentMountain()
        {
            yield return null;
            Call(_player, "MoveOnTerrain", Vector3.right * 4);
            Assert.Less(_player.transform.position.x, 44f - .49f);
            Assert.IsTrue(_access.TryUnlock(3, _ => true));
            Call(_player, "MoveOnTerrain", Vector3.right * 4);
            Assert.Greater(_player.transform.position.x, 44f);
            float before = _player.transform.position.x;
            Call(_player, "MoveOnTerrain", Vector3.right * 30);
            Assert.AreEqual(before, _player.transform.position.x, .01f,
                "A long frame cannot tunnel through the permanent mountain after buying its region.");
        }

        [UnityTest]
        public IEnumerator EnemyDirectTransformMovementUsesTheSameZoneAndObstacleRules()
        {
            _enemyRoot = new GameObject("MovementEnemy");
            _enemyRoot.transform.position = new Vector3(43, .5f, 32.5f);
            var enemy = _enemyRoot.AddComponent<EnemyTriangle>();
            enemy.enabled = false;
            Set(enemy, "_terrain", _terrain);
            yield return null;
            var from = enemy.transform.position;
            Assert.IsFalse((bool)Call(enemy, "TryMove", from, Vector3.right * 3));
            Assert.AreEqual(from, enemy.transform.position);
            Assert.IsTrue(_access.TryUnlock(3, _ => true));
            Assert.IsTrue((bool)Call(enemy, "TryMove", from, Vector3.right * 3));
            _terrain.Traversal.RebuildObstacles(new[]
            {
                new Bounds(new Vector3(48, 1, 32.5f), new Vector3(.8f, 2, 3))
            });
            Assert.IsFalse((bool)Call(enemy, "TryMove", enemy.transform.position, Vector3.right * 4),
                "Writing transform.position does not permit enemies to pass through trees.");
        }

        [UnityTest]
        public IEnumerator ExplosionClearsFlatGroundButCannotReachAcrossLockedTerritory()
        {
            _enemyRoot = new GameObject("ExplosionTargets");
            var targets = new EnemyTriangle[3];
            float[] positions = { 42.4f, 43.1f, 44.1f };
            for (int i = 0; i < targets.Length; i++)
            {
                var root = new GameObject("Target" + i);
                root.transform.SetParent(_enemyRoot.transform);
                Assert.IsTrue(_terrain.TryGetWalkablePosition(new Vector3(positions[i], 0, 32.5f), out var ground));
                root.transform.position = ground;
                targets[i] = root.AddComponent<EnemyTriangle>();
                targets[i].Initialize(1, 1);
            }
            Physics.SyncTransforms();
            var projectile = AttackProjectile.SpawnPattern(new Vector3(41, 1.12f, 32.5f), Vector3.right,
                20, 14, 4, Color.white, ShotType.Explosive, PokemonElement.Fire);
            Call(projectile, "TickProjectile", .1f);
            Assert.AreEqual(6, targets[0].Health);
            Assert.AreEqual(7, targets[1].Health, "The ground beneath an enemy must not occlude body-height splash.");
            Assert.AreEqual(10, targets[2].Health, "Explosion radius never bypasses a locked frontier.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RespawnKeepsPurchasedTerritoriesAndNeverSweepsAcrossPickups()
        {
            Assert.IsTrue(_access.TryUnlock(3, _ => true));
            PlacePlayer(new Vector3(48, 0, 32.5f));
            int revision = _player.TeleportRevision;
            _player.Respawn();
            Assert.AreEqual(revision + 1, _player.TeleportRevision);
            Assert.IsTrue(_access.IsUnlocked(3));
            Assert.AreEqual(1, _terrain.Traversal.ZoneAt(_player.transform.position));
            yield return null;
        }

        [TestCase(ShotType.Basic)]
        [TestCase(ShotType.Precision)]
        [TestCase(ShotType.Piercing)]
        [TestCase(ShotType.Boomerang)]
        [TestCase(ShotType.Explosive)]
        public void LongFrameHitsAccessibleEnemyBeforeRetiringAtClosedFrontier(ShotType type)
        {
            _enemyRoot = new GameObject("FrontierTargets");
            var targets = new EnemyTriangle[2];
            for (int i = 0; i < targets.Length; i++)
            {
                var root = new GameObject("Target" + i);
                root.transform.SetParent(_enemyRoot.transform);
                Assert.IsTrue(_terrain.TryGetWalkablePosition(
                    new Vector3(i == 0 ? 43.5f : 44.1f, 0, 32.5f), out var ground));
                root.transform.position = ground;
                targets[i] = root.AddComponent<EnemyTriangle>();
                targets[i].Initialize(1, 1);
            }
            Physics.SyncTransforms();
            var projectile = AttackProjectile.SpawnPattern(new Vector3(42.8f, 1.12f, 32.5f),
                Vector3.right, 22, 18, 4, Color.white, type, PokemonElement.Normal);
            Call(projectile, "TickProjectile", .06f);
            Assert.AreEqual(6, targets[0].Health, "A frontier must not erase the earlier legal part of the sweep.");
            Assert.AreEqual(10, targets[1].Health, "Hit radius and splash cannot cross the frontier.");
            Assert.Less(projectile.transform.position.x, 44f);
            Assert.IsFalse(projectile.gameObject.activeSelf);
        }

        [Test]
        public void AutoAimCannotAcquireAnEnemyAcrossALockedTerritory()
        {
            _enemyRoot = new GameObject("LockedAimTarget");
            Assert.IsTrue(_terrain.TryGetWalkablePosition(new Vector3(45.5f, 0, 32.5f), out var ground));
            _enemyRoot.transform.position = ground;
            _enemyRoot.AddComponent<EnemyTriangle>().Initialize(1, 1);
            Physics.SyncTransforms();
            Vector3 origin = new Vector3(43, ground.y + .62f, 32.5f);
            Assert.IsFalse(AutoAimTargeting.TryDirection(origin, 18, out _));
            Assert.IsTrue(_access.TryUnlock(3, _ => true));
            Assert.IsTrue(AutoAimTargeting.TryDirection(origin, 18, out var direction));
            Assert.Less(Vector3.Distance(direction, Vector3.right), .001f);
        }
    }
}

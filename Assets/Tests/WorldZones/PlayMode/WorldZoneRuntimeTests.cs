using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Player;
using Game.Rounds;
using Game.Terrain;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.WorldZones.Tests
{
    /// <summary>Synchronous PlayMode contracts. No progression bootstrap, StartRun save capture or PlayerPrefs.</summary>
    public sealed class WorldZoneRuntimeTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> _objects = new List<GameObject>();
        RoundManager _round;
        PlayerSphereController _player;
        WorldTerrain _terrain;
        TerrainSettings _settings;
        bool _blocked, _over;
        float _timeScale;

        [SetUp]
        public void SetUp()
        {
            Assert.That(RoundManager.Instance, Is.Null, "Requires an empty test scene.");
            Assert.That(PlayerSphereController.Instance, Is.Null);
            Assert.That(WorldTerrain.Instance, Is.Null);
            _blocked = GameFlow.Blocked;
            _over = GameFlow.Over;
            _timeScale = Time.timeScale;
            Time.timeScale = 1f;
            GameFlow.Blocked = GameFlow.Over = false;
            _settings = ScriptableObject.CreateInstance<TerrainSettings>();
            _settings.enableWorldZones = true;
            _settings.chunksX = _settings.chunksZ = 4;
            _settings.chunkSize = 16;
            _settings.lakeCount = 0;
            // Inject only geographic data, without activating mesh generation or a scene bootstrap.
            var world = Create("ZoneTestTerrain");
            world.SetActive(false);
            _terrain = world.AddComponent<WorldTerrain>();
            var map = new HeightMap(64, 64);
            for (int z = 0; z < 64; z++)
            for (int x = 0; x < 64; x++) map.Set(x, z, 1);
            Set(_terrain, "settings", _settings);
            Set(_terrain, "_map", map);
            Set(_terrain, "<Traversal>k__BackingField", new WorldTraversal(map, _settings, Vector3.zero));
            typeof(WorldTerrain).GetProperty("Instance").SetValue(null, _terrain);
            _round = Create("ZoneTestRound").AddComponent<RoundManager>();
            _round.enabled = false;
            _round.Drinks.enabled = false;
            _round.ShotStations.enabled = false;
            Assert.That(_round.Zones, Is.SameAs(_round.GetComponent<WorldZoneController>()));
            _round.Zones.enabled = false;
            _player = Create("ZoneTestPlayer").AddComponent<PlayerSphereController>();
            _player.enabled = false;
            Set(_round, "_phase", RoundManager.RoundPhase.Preparing);
            Set(_round, "<Points>k__BackingField", 5000);
            _player.transform.position = _terrain.SpawnPosition;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            typeof(WorldTerrain).GetProperty("Instance").SetValue(null, null);
            if (_settings != null) Object.DestroyImmediate(_settings);
            GameFlow.Blocked = _blocked;
            GameFlow.Over = _over;
            Time.timeScale = _timeScale;
        }

        [Test]
        public void RealRoundCharges250Then600WithAdjacencyAndNotifiesHud()
        {
            int notifications = 0;
            _round.Changed += () => notifications++;
            Assert.That(_round.Zones.Buy(6), Is.False);
            GoTo(2);
            Assert.That(_round.Zones.NearbyOffer(_player.transform.position), Is.EqualTo(2));
            Assert.That(_round.Zones.Buy(2), Is.True);
            Assert.That(_round.Points, Is.EqualTo(4750));
            Assert.That(_round.Zones.Buy(2), Is.False);
            GoTo(6);
            Assert.That(_round.Zones.Buy(6), Is.True);
            Assert.That(_round.Points, Is.EqualTo(4150));
            Assert.That(_round.CoinsEarned, Is.Zero);
            Assert.That(_round.TotalPointsEarned, Is.Zero);
            Assert.That(_round.Zones.UnlockedCount, Is.EqualTo(3));
            Assert.That(notifications, Is.GreaterThanOrEqualTo(2));
        }

        [TestCase("paused")]
        [TestCase("timescale")]
        [TestCase("dead")]
        [TestCase("idle")]
        [TestCase("over")]
        [TestCase("remote")]
        [TestCase("height")]
        [TestCase("poor")]
        public void RealRoundRejectsUnauthorizedPurchaseWithoutCharging(string reason)
        {
            GoTo(2);
            switch (reason)
            {
                case "paused": GameFlow.Blocked = true; break;
                case "timescale": Time.timeScale = 0f; break;
                case "dead": Set(_player, "_alive", false); break;
                case "idle": Set(_round, "_phase", RoundManager.RoundPhase.Idle); break;
                case "over": GameFlow.Over = true; break;
                case "remote": _player.transform.position = _terrain.SpawnPosition; break;
                case "height": _player.transform.position += Vector3.up * 5; break;
                case "poor": Set(_round, "<Points>k__BackingField", 249); break;
            }
            int before = _round.Points;
            Assert.That(_round.Zones.CanBuy(2), Is.False);
            Assert.That(_round.Zones.Buy(2), Is.False);
            Assert.That(_round.Points, Is.EqualTo(before));
            Assert.That(_round.Zones.IsUnlocked(2), Is.False);
        }

        [Test]
        public void SpendPointsChangedListenerCannotChargeReentrantBuy()
        {
            GoTo(2);
            int nested = 0;
            _round.Changed += () => { if (_round.Zones.Buy(2)) nested++; };
            Assert.That(_round.Zones.Buy(2), Is.True);
            Assert.That(nested, Is.Zero);
            Assert.That(_round.Points, Is.EqualTo(4750));
            Assert.That(_round.Zones.UnlockedCount, Is.EqualTo(2));
        }

        [Test]
        public void WaveAndPausePreservePurchasesButRunResetCallbackClosesThem()
        {
            GoTo(2);
            Assert.That(_round.Zones.Buy(2), Is.True);
            Call(_round, "StartRound", 2);
            Assert.That(_round.Zones.IsUnlocked(2), Is.True);
            GameFlow.Blocked = true;
            Call(_round, "Update");
            GameFlow.Blocked = false;
            Assert.That(_round.Zones.IsUnlocked(2), Is.True);
            // Same reset callback used by FinishRun and StopRun; avoids their persistence side effects.
            Call(_round, "ResetTemporaryStats");
            Assert.That(_round.Zones.UnlockedCount, Is.EqualTo(1));
            _round.Zones.StartRun();
            Assert.That(_round.Zones.IsUnlocked(2), Is.False);
        }

        [Test]
        public void FogForceOffSurvivesCullerAndUnlockDoesNotOverrideFrustum()
        {
            var fog = Create("TestFog").AddComponent<WorldZoneFog>();
            fog.Initialize(_terrain);
            Set(_terrain, "<ZoneFog>k__BackingField", fog);
            var prop = Create("ElevatedLockedProp").AddComponent<SpriteRenderer>();
            prop.transform.position = new Vector3(32, 6, 54);
            fog.RegisterProp(prop, new Vector3(32, .5f, 54));
            Assert.That(prop.forceRenderingOff, Is.True);
            VisibilityCuller.RegisterTerrain(prop);
            var culler = Create("TestCuller").AddComponent<VisibilityCuller>();
            // A zero-normal frustum accepts every bound, just as a visible prop in the real culler.
            Call(culler, "ApplyVisibility", new List<Renderer> { prop });
            Assert.That(prop.enabled, Is.True);
            Assert.That(prop.forceRenderingOff, Is.True, "Frustum visibility must not reveal closed props.");
            prop.enabled = false; // off-screen result of the independent culler
            GoTo(2);
            Assert.That(_round.Zones.Buy(2), Is.True);
            Assert.That(prop.forceRenderingOff, Is.False);
            Assert.That(prop.enabled, Is.False, "Unlock cannot override a culling decision.");
            _round.Zones.ResetRun();
            Assert.That(prop.forceRenderingOff, Is.True);
            VisibilityCuller.UnregisterTerrain(prop);
        }

        [Test]
        public void SurfaceOverlayReusesMeshAndPersistentShaderResource()
        {
            var fog = Create("TestSurfaceFog").AddComponent<WorldZoneFog>();
            fog.Initialize(_terrain);
            var surface = Create("TestGround");
            var mesh = new Mesh();
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.forward, Vector3.right };
                mesh.triangles = new[] { 0, 1, 2 };
                surface.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = surface.AddComponent<MeshRenderer>();
                fog.RegisterSurface(renderer);
                var overlay = surface.transform.Find("ZoneFogSurface");
                Assert.That(overlay, Is.Not.Null);
                Assert.That(overlay.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                Assert.That(overlay.GetComponent<MeshRenderer>().sharedMaterial.shader.name,
                    Is.EqualTo("Game/WorldZoneFog"));
                Assert.That(overlay.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(fog.SurfaceCount, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        void GoTo(int zone)
        {
            Assert.That(_round.Zones.TryGetOfferPosition(zone, out Vector3 position), Is.True);
            _player.transform.position = position;
        }
        GameObject Create(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }
        static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Private).SetValue(target, value);
        static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Private).Invoke(target, args);
    }
}

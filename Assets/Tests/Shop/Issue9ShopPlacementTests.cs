using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Player;
using Game.Rounds;
using Game.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Game.Rounds.Shop.Tests
{
    public sealed class Issue9ShopPlacementTests
    {
        [Test]
        public void Placement_RejectsMissingTerrainWithVisibleFailure()
        {
            Assert.Throws<System.ArgumentException>(() =>
                WorldShopPlacement.FindPositions(null, 5, 123));
        }
    }

    /// <summary>Prueba Buy sin despertar managers ni tocar el mundo abierto del Editor.</summary>
    public sealed class Issue9DrinkPurchaseTests
    {
        const int StartingPoints = 1000;

        readonly List<Object> _created = new List<Object>();
        RoundManager _round;
        PlayerSphereController _player;
        DrinkStations _drinks;
        Transform[] _stands;
        GameObject _standObject;
        bool _previousBlocked;
        bool _previousOver;
        object _previousRoundInstance;
        object _previousPlayerInstance;

        [SetUp]
        public void SetUp()
        {
            _previousBlocked = GameFlow.Blocked;
            _previousOver = GameFlow.Over;
            _previousRoundInstance = GetStaticBackingField(typeof(RoundManager), "Instance");
            _previousPlayerInstance = GetStaticBackingField(typeof(PlayerSphereController), "Instance");
            GameFlow.Blocked = false;
            GameFlow.Over = false;

            _round = AddInactive<RoundManager>("Issue9_TestRound");
            _player = AddInactive<PlayerSphereController>("Issue9_TestPlayer");
            _drinks = AddInactive<DrinkStations>("Issue9_TestDrinks");
            _standObject = new GameObject("Issue9_TestStand");
            _standObject.SetActive(false);
            _created.Add(_standObject);

            SetStaticBackingField(typeof(RoundManager), "Instance", _round);
            SetStaticBackingField(typeof(PlayerSphereController), "Instance", _player);
            SetField(_round, "_phase", RoundManager.RoundPhase.Fighting);
            SetAutoProperty(_round, "Points", StartingPoints);

            _stands = (Transform[])GetField(_drinks, "_stands");
            _stands[0] = _standObject.transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created)
                if (item != null) Object.DestroyImmediate(item);
            _created.Clear();

            SetStaticBackingField(typeof(RoundManager), "Instance", _previousRoundInstance);
            SetStaticBackingField(typeof(PlayerSphereController), "Instance", _previousPlayerInstance);
            GameFlow.Blocked = _previousBlocked;
            GameFlow.Over = _previousOver;
        }

        [Test]
        public void Buy_DeductsExactlyEachAdvertisedPrice()
        {
            for (int i = 0; i < DrinkStations.Prices.Length; i++)
            {
                _drinks.ResetRun();
                SetAutoProperty(_round, "Points", StartingPoints);
                _stands[i] = _standObject.transform;

                int price = DrinkStations.Prices[i];
                Assert.IsTrue(_drinks.Buy(i), "Debe poder comprar " + DrinkStations.Names[i]);
                Assert.AreEqual(StartingPoints - price, _round.Points,
                    "El cobro debe coincidir con el precio anunciado de " + DrinkStations.Names[i]);
                Assert.IsTrue(_drinks.Has((DrinkType)i));
                _stands[i] = null;
            }
        }

        [Test]
        public void Buy_InsufficientBalanceDoesNotChargeOrMarkPurchased()
        {
            int before = DrinkStations.Prices[0] - 1;
            SetAutoProperty(_round, "Points", before);

            Assert.IsFalse(_drinks.Buy(0));
            Assert.AreEqual(before, _round.Points);
            Assert.IsFalse(_drinks.Has(DrinkType.Power));
        }

        [Test]
        public void Buy_RepurchaseDoesNotChargeAgain()
        {
            Assert.IsTrue(_drinks.Buy(0));
            int afterFirstPurchase = _round.Points;

            Assert.IsFalse(_drinks.Buy(0));
            Assert.AreEqual(afterFirstPurchase, _round.Points);
        }

        [Test]
        public void Buy_OutOfRangeDoesNotChargeOrMarkPurchased()
        {
            _player.transform.position = Vector3.right * 4f;

            Assert.IsFalse(_drinks.Buy(0));
            Assert.AreEqual(StartingPoints, _round.Points);
            Assert.IsFalse(_drinks.Has(DrinkType.Power));
        }

        T AddInactive<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.SetActive(false);
            _created.Add(go);
            return go.AddComponent<T>();
        }

        static object GetStaticBackingField(System.Type type, string property)
        {
            FieldInfo field = type.GetField("<" + property + ">k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return field.GetValue(null);
        }

        static void SetStaticBackingField(System.Type type, string property, object value)
        {
            FieldInfo field = type.GetField("<" + property + ">k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(null, value);
        }

        static void SetAutoProperty(object target, string property, object value)
        {
            FieldInfo field = target.GetType().GetField("<" + property + ">k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }

        static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return field.GetValue(target);
        }
    }

    public sealed class Issue9StaticObstacleConnectivityTests
    {
        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created)
                if (item != null) Object.DestroyImmediate(item);
            _created.Clear();
        }

        [Test]
        public void Reachability_StopsAtOneBlockedStaticStep()
        {
            TerrainSettings settings = ScriptableObject.CreateInstance<TerrainSettings>();
            settings.blockSize = 1f;
            settings.stepHeight = 0.5f;
            _created.Add(settings);

            var terrainObject = new GameObject("Issue9_TestTerrain");
            terrainObject.SetActive(false);
            _created.Add(terrainObject);
            WorldTerrain terrain = terrainObject.AddComponent<WorldTerrain>();
            SetField(terrain, "settings", settings);

            var map = new HeightMap(7, 7);
            for (int z = 0; z < map.Depth; z++)
            for (int x = 0; x < map.Width; x++)
                map.Set(x, z, 5);
            map.Set(2, 3, 0);
            map.Set(3, 3, 0);
            map.Set(4, 3, 0);
            SetField(terrain, "_map", map);

            bool[,] openRoute = FindReachable(terrain, 2, 3);
            Assert.IsTrue(openRoute[4, 3], "La ruta de control debería llegar al otro lado.");

            var obstacle = new GameObject("Issue9_StaticPathBlocker");
            _created.Add(obstacle);
            obstacle.transform.position = terrain.BlockTopCenter(3, 3) + Vector3.right * 4f;
            obstacle.AddComponent<BoxCollider>().size = new Vector3(0.8f, 1.2f, 0.8f);
            // Cambiarlo sin esperar al siguiente frame prueba que el helper sincroniza transforms.
            obstacle.transform.position = terrain.BlockTopCenter(3, 3) + Vector3.up * 0.72f;

            bool[,] blockedRoute = FindReachable(terrain, 2, 3);
            Assert.IsFalse(blockedRoute[3, 3], "El paso ocupado debe ser inaccesible.");
            Assert.IsFalse(blockedRoute[4, 3], "No se puede declarar accesible el corredor tras el obstáculo.");
        }

        static bool[,] FindReachable(WorldTerrain terrain, int x, int z)
        {
            MethodInfo method = typeof(WorldShopPlacement).GetMethod("FindReachable",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (bool[,])method.Invoke(null, new object[] { terrain, x, z });
        }

        static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(target, value);
        }
    }
}

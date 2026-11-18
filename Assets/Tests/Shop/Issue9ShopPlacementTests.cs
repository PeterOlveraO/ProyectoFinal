using System.Collections.Generic;
using System.Reflection;
using Game.Rounds;
using Game.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Game.Rounds.Shop.Tests
{
    public sealed class Issue9ShopPlacementTests
    {
        [Test]
        public void DrinkPrices_MatchThePointsShownAndCharged()
        {
            CollectionAssert.AreEqual(new[] { 30, 45, 35, 45, 60 }, DrinkStations.Prices);
            Assert.AreEqual(DrinkStations.Names.Length, DrinkStations.Prices.Length);
        }

        [Test]
        public void Placement_RejectsMissingTerrainWithVisibleFailure()
        {
            Assert.Throws<System.ArgumentException>(() =>
                WorldShopPlacement.FindPositions(null, 5, 123));
        }

        [Test]
        public void Placement_RejectsNonPositiveCountWithVisibleFailure()
        {
            Assert.Throws<System.ArgumentException>(() =>
                WorldShopPlacement.FindPositions(null, 0, 123));
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

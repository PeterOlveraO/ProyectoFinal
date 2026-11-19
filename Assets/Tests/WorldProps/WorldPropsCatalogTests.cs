using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.WorldProps.Tests
{
    public class WorldPropsCatalogTests
    {
        static Type FindType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(name);
                if (type != null) return type;
            }
            Assert.Fail(name + " no está cargado");
            return null;
        }

        static Type Decorator() => FindType("Game.Pokemon.WorldDecorator");

        static object Invoke(string method, params object[] args) =>
            Decorator().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);

        static IEnumerable Catalog(string method) => (IEnumerable)Invoke(method);

        static float Number(object profile, string field) =>
            (float)profile.GetType().GetField(field).GetValue(profile);

        static float Clearance(object profile) =>
            (float)profile.GetType().GetProperty("Clearance").GetValue(profile);

        [TestCase("CatalogTrees")]
        [TestCase("CatalogShrubs")]
        [TestCase("CatalogPlants")]
        public void CatalogContainsOnlyVisuallyReviewedFlora(string method)
        {
            // Allowlist de la inspección de PNG, no deducida del nombre de la carpeta.
            int[] reviewed = method == "CatalogShrubs" ? new[] { 40, 41, 42, 43 } :
                method == "CatalogPlants" ? new[] { 12, 13, 14, 15 } :
                new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 16, 17, 18, 19,
                    24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39 };
            var expected = new HashSet<string>();
            foreach (int id in reviewed) expected.Add("Decor/Trees/tree_" + id.ToString("00"));
            int count = 0;
            foreach (var profile in Catalog(method))
            {
                Type type = profile.GetType();
                string path = (string)type.GetField("Path").GetValue(profile);
                var sprite = Resources.Load<Sprite>(path);
                Assert.That(sprite, Is.Not.Null, path);
                Assert.That(expected.Remove(path), Is.True, "Sprite no revisado o duplicado: " + path);
                Assert.That(Number(profile, "Footprint"), Is.GreaterThan(0), path);
                Assert.That(Clearance(profile), Is.GreaterThanOrEqualTo(Number(profile, "Width") * .5f));
                float scale = Mathf.Min(Number(profile, "Height") * sprite.pixelsPerUnit / sprite.rect.height,
                    Number(profile, "Width") * sprite.pixelsPerUnit / sprite.rect.width);
                Assert.That(Number(profile, "ColliderHeight"),
                    Is.LessThanOrEqualTo(sprite.rect.height / sprite.pixelsPerUnit * scale), path);
                count++;
            }
            Assert.That(count, Is.EqualTo(reviewed.Length));
            Assert.That(expected, Is.Empty);
        }

        [TestCase("CatalogTrees")]
        [TestCase("CatalogShrubs")]
        [TestCase("CatalogPlants")]
        public void SolidFloraHasRoundedGroundedColliderAndLowPlantsAreWalkable(string method)
        {
            foreach (var profile in Catalog(method))
            {
                var root = new GameObject("Flora collider test");
                try
                {
                    Invoke("AddFloraCollider", root, profile);
                    if (method == "CatalogPlants")
                    {
                        Assert.That(root.GetComponents<Collider>(), Is.Empty);
                        continue;
                    }
                    var collider = root.GetComponent<CapsuleCollider>();
                    Assert.That(root.GetComponents<Collider>().Length, Is.EqualTo(1));
                    Assert.That(collider, Is.Not.Null);
                    Assert.That(collider.isTrigger, Is.False);
                    Assert.That(collider.direction, Is.EqualTo(1));
                    Assert.That(collider.radius, Is.EqualTo(Number(profile, "Footprint")));
                    Assert.That(collider.height, Is.EqualTo(Number(profile, "ColliderHeight")));
                    Assert.That(collider.height, Is.GreaterThanOrEqualTo(collider.radius * 2f));
                    Assert.That(collider.center.y - collider.height * .5f, Is.EqualTo(0f).Within(.0001f));
                    Assert.That(collider.radius * 2f, Is.LessThan(Number(profile, "Width")));
                    Assert.That(collider.transform, Is.SameAs(root.transform));
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }

        static bool Reserved(Vector3 offset, float clearance = 1.4f, float spawnRadius = 0f) =>
            (bool)Invoke("IsReserved", offset, clearance, spawnRadius, 10f, 9f);

        [Test]
        public void TycoonDiskIncludesVisualFootprintAndCannotBeReducedByOldSceneValue()
        {
            foreach (float angle in new[] { 30f, 45f, 60f, 135f, 225f, 315f })
            {
                Vector3 direction = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f,
                    Mathf.Sin(angle * Mathf.Deg2Rad));
                Assert.That(Reserved(direction * 16.3f), Is.True);
                Assert.That(Reserved(direction * 16.3f, 1.4f, 6f), Is.True);
            }
            Assert.That(Reserved(new Vector3(20f, 0f, 20f)), Is.False);
            Assert.That(Reserved(new Vector3(20f, 0f, 20f), 1.4f, 30f), Is.True);
        }

        [Test]
        public void CardinalCorridorsProtectRampsAndWidenTowardMapEdge()
        {
            foreach (float distance in new[] { 20f, 40f, 70f })
                foreach (float sign in new[] { -1f, 1f })
                {
                    float width = Mathf.Max(5f, distance * Mathf.Tan(9f * Mathf.Deg2Rad)) + 1.4f + 2f;
                    Assert.That(Reserved(new Vector3(sign * distance, 0f, width - .01f)), Is.True);
                    Assert.That(Reserved(new Vector3(width - .01f, 0f, sign * distance)), Is.True);
                    Assert.That(Reserved(new Vector3(sign * distance, 0f, width + .01f)), Is.False);
                }
        }

        [Test]
        public void PlacementIsDeterministicSeparatedAndMoreAbundantThanOldFloraBudget()
        {
            // Terreno plano sintético, sin abrir escenas ni ejecutar WorldTerrain.Awake.
            // No es una afirmación sobre el conteo del mapa de gameplay: requiere QA runtime.
            var terrainRoot = new GameObject("Inactive terrain fixture");
            terrainRoot.SetActive(false);
            var decorRoot = new GameObject("Inactive decorator fixture");
            decorRoot.SetActive(false);
            UnityEngine.Object settings = null;
            try
            {
                var terrainType = FindType("Game.Terrain.WorldTerrain");
                var terrain = terrainRoot.AddComponent(terrainType);
                settings = ScriptableObject.CreateInstance(FindType("Game.Terrain.TerrainSettings"));
                var map = Activator.CreateInstance(FindType("Game.Terrain.HeightMap"), 160, 160);
                terrainType.GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(terrain, settings);
                terrainType.GetField("_map", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(terrain, map);
                terrainRoot.transform.position = new Vector3(123f, 7f, -211f);
                var decorator = decorRoot.AddComponent(Decorator());
                // World.unity aún puede contener estas claves antiguas: no deben restaurar
                // ni edificios ni el límite de 90 árboles al deserializar la nueva clase.
                foreach (string legacy in new[] { "buildings", "extras", "trees", "props" })
                    Assert.That(Decorator().GetField(legacy, BindingFlags.NonPublic | BindingFlags.Instance), Is.Null);
                var find = Decorator().GetMethod("TryFindSpot", BindingFlags.NonPublic | BindingFlags.Instance);
                var random = Decorator().GetField("_random", BindingFlags.NonPublic | BindingFlags.Instance);
                var placed = (IList)Decorator().GetField("_placed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(decorator);
                var firstRun = new List<Vector3>();
                for (int run = 0; run < 2; run++)
                {
                    placed.Clear();
                    random.SetValue(decorator, new System.Random(12345));
                    int index = 0;
                    foreach (string method in new[] { "CatalogTrees", "CatalogShrubs", "CatalogPlants" })
                    {
                        string field = method == "CatalogTrees" ? "canopyTrees" :
                            method == "CatalogShrubs" ? "shrubs" : "groundPlants";
                        int budget = (int)Decorator().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(decorator);
                        Assert.That(budget, Is.EqualTo(method == "CatalogTrees" ? 150 :
                            method == "CatalogShrubs" ? 80 : 60));
                        var profiles = new List<object>();
                        foreach (var profile in Catalog(method)) profiles.Add(profile);
                        for (int i = 0; i < budget; i++)
                        {
                            object profile = profiles[i % profiles.Count];
                            object[] args = { terrain, profile, 12f, 76f, Vector3.zero };
                            if (!(bool)find.Invoke(decorator, args)) continue;
                            var position = (Vector3)args[4];
                            Vector3 spawn = (Vector3)terrainType.GetProperty("SpawnPosition").GetValue(terrain);
                            Assert.That(Reserved(position - spawn, Clearance(profile)), Is.False);
                            foreach (var other in placed)
                            {
                                var otherPosition = (Vector3)other.GetType().GetField("Position").GetValue(other);
                                if (otherPosition == position) continue;
                                float radius = (float)other.GetType().GetField("Radius").GetValue(other);
                                Assert.That(Vector3.Distance(position, otherPosition),
                                    Is.GreaterThanOrEqualTo(Clearance(profile) + radius + 2.5f - .001f));
                            }
                            if (run == 0) firstRun.Add(position);
                            else Assert.That(position, Is.EqualTo(firstRun[index]));
                            index++;
                        }
                    }
                    Assert.That(index, Is.GreaterThan(124), "Antes: <=90 árboles + <=34 props (muchos ni eran flora)");
                    if (run == 1) Assert.That(index, Is.EqualTo(firstRun.Count));
                }

                // La misma consulta usada al colocar rechaza bordes, desniveles y orillas.
                object tree = null;
                foreach (var profile in Catalog("CatalogTrees")) { tree = profile; break; }
                Assert.That((bool)Invoke("SuitableGround", terrain, 40, 40, tree), Is.True);
                Assert.That((bool)Invoke("SuitableGround", terrain, 0, 40, tree), Is.False);
                var setLevel = map.GetType().GetMethod("Set");
                setLevel.Invoke(map, new object[] { 41, 40, 1 });
                Assert.That((bool)Invoke("SuitableGround", terrain, 40, 40, tree), Is.False);
                setLevel.Invoke(map, new object[] { 41, 40, 0 });
                var lake = Activator.CreateInstance(FindType("Game.Terrain.LakeInfo"));
                lake.GetType().GetField("Center").SetValue(lake, new Vector2Int(43, 40));
                lake.GetType().GetField("Radius").SetValue(lake, 1);
                ((IList)map.GetType().GetField("Lakes").GetValue(map)).Add(lake);
                Assert.That((bool)Invoke("SuitableGround", terrain, 40, 40, tree), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decorRoot);
                UnityEngine.Object.DestroyImmediate(terrainRoot);
                if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}

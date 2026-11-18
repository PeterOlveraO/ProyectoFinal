using System.Collections;
using System.Reflection;
using Game.Terrain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Terrain.Tests
{
    public sealed class LakeBorderTests
    {
        GameObject _root;
        TerrainSettings _settings;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            if (_settings != null) Object.Destroy(_settings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CharacterController_StopsAtShoreFromCardinalAndDiagonalDirections()
        {
            _root = new GameObject("LakeBorderTest");
            var lake = new LakeInfo { Center = new Vector2Int(0, 0), Radius = 11, RimLevel = 4, BottomLevel = 2 };
            LakeGeometry.AddBlockers(_root, lake, 1f, 0.5f, 8f);
            var ground = new GameObject("Shore");
            ground.transform.SetParent(_root.transform);
            var floor = ground.AddComponent<BoxCollider>();
            floor.center = new Vector3(0f, 1.75f, 0f);
            floor.size = new Vector3(100f, 0.5f, 100f);
            var actor = new GameObject("Character");
            actor.transform.SetParent(_root.transform);
            var controller = actor.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = Vector3.up * 0.9f;
            controller.radius = 0.35f;
            controller.skinWidth = 0.03f;
            controller.stepOffset = 0.55f; // El mismo escalón de un bloque del juego.
            yield return null;

            for (int direction = 0; direction < 8; direction++)
            {
                float angle = direction * Mathf.PI / 4f;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                controller.enabled = false;
                actor.transform.position = new Vector3(0.5f, 2.05f, 0.5f) + outward * 15f;
                controller.enabled = true;
                Physics.SyncTransforms();
                bool hitShore = false;
                for (int step = 0; step < 120; step++)
                {
                    var flags = controller.Move(-outward * 0.15f + Vector3.down * 0.04f);
                    hitShore |= (flags & CollisionFlags.Sides) != 0;
                    Vector3 p = actor.transform.position;
                    Assert.IsFalse(lake.ContainsCell(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z)),
                        $"Entró al agua por dirección {direction}, paso {step}.");
                    Assert.GreaterOrEqual(p.y, 1.9f, "Debe detenerse antes de caer, sin teletransporte.");
                }
                Assert.IsTrue(hitShore, $"No se encontró colisión lateral desde {direction}.");
            }
            // La antigua caja cuadrada bloqueaba esquinas que no pertenecen al disco.
            Assert.IsFalse(Physics.CheckSphere(new Vector3(11.4f, 3f, 11.4f), 0.2f,
                ~0, QueryTriggerInteraction.Ignore));
            foreach (var box in _root.GetComponents<BoxCollider>()) Assert.IsFalse(box.isTrigger);
        }

        [UnityTest]
        public IEnumerator WorldQueries_MatchPhysicalFootprint_WithOffsetAndBlockScale()
        {
            _settings = ScriptableObject.CreateInstance<TerrainSettings>();
            // Evita depender de assets o Shader.Find en el test de consultas.
            _root = new GameObject("WorldQueries");
            _root.SetActive(false);
            _root.transform.position = new Vector3(100f, 7f, -50f);
            var world = _root.AddComponent<WorldTerrain>();
            var map = new HeightMap(48, 48);
            var lake = new LakeInfo { Center = new Vector2Int(20, 20), Radius = 11 };
            map.Lakes.Add(lake);
            _settings.blockSize = 2f;
            _settings.stepHeight = 0.5f;
            typeof(WorldTerrain).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(world, _settings);
            typeof(WorldTerrain).GetField("_map", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(world, map);
            LakeGeometry.AddBlockers(_root, lake, 2f, -1f, 4f);
            // Inactivo: no Awake/Generate ni dependencia del renderer. Se validan
            // bounds locales directamente; la prueba anterior ejercita PhysX real.
            foreach (var box in _root.GetComponents<BoxCollider>())
                Assert.IsFalse(box.isTrigger);
            for (int z = 7; z <= 33; z++)
            {
                for (int x = 7; x <= 33; x++)
                {
                    var p = _root.transform.position + new Vector3((x + 0.9f) * 2f, 999f, (z + 0.1f) * 2f);
                    bool expected = lake.ContainsCell(x, z);
                    Assert.AreEqual(expected, world.IsLakeAtWorld(p));
                    Assert.AreEqual(!expected, world.TryGetWalkablePosition(p, out var surface));
                    if (!expected)
                    {
                        Assert.AreEqual(p.x, surface.x);
                        Assert.AreEqual(p.z, surface.z);
                        Assert.AreEqual(_root.transform.position.y, surface.y);
                    }
                    bool inBox = false;
                    Vector3 local = p - _root.transform.position;
                    foreach (var box in _root.GetComponents<BoxCollider>())
                    {
                        var bounds = new Bounds(box.center, box.size);
                        inBox |= bounds.Contains(new Vector3(local.x, 1f, local.z));
                    }
                    Assert.AreEqual(expected, inBox);
                }
            }
            var edge = _root.transform.position + new Vector3(32.25f * 2f, 0f, 20.5f * 2f);
            Assert.IsTrue(world.TryGetWalkablePosition(edge, out _, 0.1f));
            Assert.IsFalse(world.TryGetWalkablePosition(edge, out _, 3f));
            Assert.IsFalse(world.TryGetWalkablePosition(_root.transform.position, out _, 0.1f));
            yield return null;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Player;
using Game.Pooling;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    // Exercise real MonoBehaviour lifecycles across frames, without loading World,
    // touching progression saves or depending on keyboard/mobile hardware.
    public sealed class RuntimeIntegrationTests
    {
        readonly List<GameObject> _created = new List<GameObject>();
        bool _blocked, _over;
        float _timeScale;
        Random.State _random;

        [SetUp]
        public void SetUp()
        {
            _blocked = GameFlow.Blocked;
            _over = GameFlow.Over;
            _timeScale = Time.timeScale;
            _random = Random.state;
            Time.timeScale = 1f;
            GameFlow.Blocked = true;
            GameFlow.Over = false;
            Assert.IsNull(PlayerSphereController.Instance, "These tests require an empty test scene.");
            Assert.AreEqual(0, EnemyTriangle.All.Count);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Hit effects are independent roots created by combat, not children of fixtures.
            foreach (var burst in Object.FindObjectsByType<HitBurst>(FindObjectsInactive.Include))
                Object.Destroy(burst.gameObject);
            foreach (var go in _created)
                if (go != null) Object.Destroy(go);
            _created.Clear();
            yield return null; // OnDisable/OnDestroy must run before the next test.
            GameFlow.Blocked = _blocked;
            GameFlow.Over = _over;
            Time.timeScale = _timeScale;
            Random.state = _random;
        }

        GameObject Create(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        EnemyTriangle Enemy(Vector3 position)
        {
            var go = Create("TestEnemy");
            go.transform.position = position;
            var enemy = go.AddComponent<EnemyTriangle>();
            enemy.Initialize(1, 1f);
            return enemy;
        }

        AttackProjectile Projectile(Vector3 position, int damage = 4, float speed = 1f, float range = 100f)
        {
            var projectile = AttackProjectile.Spawn(position, Vector3.forward, speed, range, damage, Color.white);
            _created.Add(projectile.gameObject);
            return projectile;
        }

        PlayerSphereController Player()
        {
            var player = Create("TestPlayer").AddComponent<PlayerSphereController>();
            // Awake still initializes health/controller; no gravity or physical input in fixtures.
            player.enabled = false;
            return player;
        }

        [TestCase("jolteon", PokemonElement.Electric)]
        [TestCase("charizard", PokemonElement.Fire)]
        [TestCase("garchomp", PokemonElement.Dragon)]
        [TestCase("vaporeon", PokemonElement.Water)]
        [TestCase("flareon", PokemonElement.Fire)]
        [TestCase("snorlax", PokemonElement.Normal)]
        [TestCase("blastoise", PokemonElement.Water)]
        [TestCase("dragonite", PokemonElement.Dragon)]
        [TestCase("lucario", PokemonElement.Fighting)]
        [TestCase("venusaur", PokemonElement.Grass)]
        [TestCase("gengar", PokemonElement.Ghost)]
        [TestCase("gyarados", PokemonElement.Water)]
        [TestCase("lapras", PokemonElement.Ice)]
        [TestCase("ditto", PokemonElement.Normal)]
        [TestCase("aerodactyl", PokemonElement.Rock)]
        [TestCase("kangaskhan", PokemonElement.Normal)]
        [TestCase("tauros", PokemonElement.Normal)]
        [TestCase("mew", PokemonElement.Psychic)]
        [TestCase("unknown", PokemonElement.Normal)]
        public void SpeciesElementIsDeterministic(string species, PokemonElement expected)
        {
            Assert.AreEqual(expected, PokemonElementResolver.Resolve(species));
        }

        [UnityTest]
        public IEnumerator ElementShotsHaveDifferentMasksAndLegacySpawnStillWorks()
        {
            var fire = AttackProjectile.Spawn(Vector3.zero, Vector3.forward, 4f, 10f, 2, Color.white, 1, PokemonElement.Fire);
            var electric = AttackProjectile.Spawn(Vector3.zero, Vector3.right, 4f, 10f, 2, Color.white, 1, PokemonElement.Electric);
            var legacy = Projectile(Vector3.zero);
            _created.Add(fire.gameObject);
            _created.Add(electric.gameObject);
            var fireSprite = fire.transform.Find("VisualAnchor/ElementVisual").GetComponent<SpriteRenderer>().sprite;
            var electricSprite = electric.transform.Find("VisualAnchor/ElementVisual").GetComponent<SpriteRenderer>().sprite;
            Assert.AreNotSame(fireSprite, electricSprite);
            Assert.AreEqual(FilterMode.Point, fireSprite.texture.filterMode);
            Assert.AreEqual(Color.white, fire.transform.Find("VisualAnchor/ElementVisual").GetComponent<SpriteRenderer>().color,
                "Elemental textures carry their own palette and must not be tinted twice.");
            Assert.IsNotNull(legacy.transform.Find("VisualAnchor/ElementVisual").GetComponent<SpriteRenderer>().sprite);
            GameFlow.Blocked = false;
            yield return null;
            Assert.Greater(fire.transform.position.z, 0f);
            Assert.Greater(electric.transform.position.x, 0f);
        }

        [UnityTest]
        public IEnumerator ImpactPoolIsBoundedReusesExpiredEffectsAndSurvivesDestroyedInstances()
        {
            GameFlow.Blocked = false;
            for (int i = 0; i < 100; i++)
                HitBurst.Spawn(new Vector3(i, 0, 0), Color.yellow);
            var allocated = Object.FindObjectsByType<HitBurst>(FindObjectsInactive.Include);
            Assert.LessOrEqual(allocated.Length, 32, "Crowded combat must not allocate unbounded effects.");
            yield return new WaitForSeconds(.4f);
            Assert.AreEqual(0, Object.FindObjectsByType<HitBurst>().Length);
            for (int i = 0; i < 10; i++) HitBurst.Spawn(Vector3.zero, Color.cyan);
            Assert.AreEqual(allocated.Length, Object.FindObjectsByType<HitBurst>(FindObjectsInactive.Include).Length,
                "Expired impacts should be reused without allocating new roots.");
            foreach (var burst in allocated) Object.Destroy(burst.gameObject);
            yield return null;
            HitBurst.Spawn(Vector3.one, Color.yellow);
            Assert.AreEqual(1, Object.FindObjectsByType<HitBurst>().Length,
                "Reloaded scenes must not retain stale pool references.");
        }

        [UnityTest]
        public IEnumerator ImpactFreezesInPauseAndEndsOnDefeat()
        {
            HitBurst.Spawn(Vector3.zero, Color.yellow);
            var burst = Object.FindAnyObjectByType<HitBurst>();
            yield return new WaitForSeconds(.4f);
            Assert.IsTrue(burst.gameObject.activeSelf, "An open menu must pause impact lifetime.");
            GameFlow.Over = true;
            yield return null;
            Assert.IsFalse(burst.gameObject.activeSelf, "Defeat must end the visual immediately.");
        }

        [UnityTest]
        public IEnumerator ProjectilePausesWithMenuAndResumesWithoutTeleporting()
        {
            var projectile = Projectile(Vector3.zero);
            yield return null;
            yield return null;
            Assert.AreEqual(Vector3.zero, projectile.transform.position);

            GameFlow.Blocked = false;
            yield return null;
            yield return null;
            Assert.Greater(projectile.transform.position.z, 0f);
            Assert.Less(projectile.transform.position.z, 1f);
        }

        [UnityTest]
        public IEnumerator ProjectileDamagesOnlyOneEnemyAndIsDestroyedOnImpact()
        {
            var first = Enemy(Vector3.zero);
            var second = Enemy(Vector3.zero);
            var projectile = Projectile(Vector3.zero);
            GameFlow.Blocked = false;
            yield return null;
            yield return null;

            Assert.IsTrue(projectile == null, "Impact must dispose of the projectile.");
            Assert.AreEqual(first.MaxHealth + second.MaxHealth - 4, first.Health + second.Health);
            Assert.IsTrue(first.Health == first.MaxHealth || second.Health == second.MaxHealth,
                "A single projectile must not hit both overlapping enemies.");
        }

        [UnityTest]
        public IEnumerator LethalProjectileRemovesEnemyFromActiveRegistry()
        {
            var enemy = Enemy(Vector3.zero);
            Assert.Contains(enemy, new List<EnemyTriangle>(EnemyTriangle.All));
            var projectile = Projectile(Vector3.zero, enemy.MaxHealth);
            GameFlow.Blocked = false;
            yield return null;
            yield return null;

            Assert.IsTrue(enemy == null);
            Assert.IsTrue(projectile == null);
            Assert.AreEqual(0, EnemyTriangle.All.Count, "Destroyed enemies must not remain targetable.");
        }

        [UnityTest]
        public IEnumerator MissedProjectileExpiresAtItsRange()
        {
            var projectile = Projectile(Vector3.zero, speed: 5f, range: 1f);
            GameFlow.Blocked = false;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (projectile != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(projectile == null, "A missed shot must not leak a permanent GameObject.");
        }

        [UnityTest]
        public IEnumerator DamageHealDeathAndRevivePreserveHealthInvariants()
        {
            var player = Player();
            int max = player.MaxHealth;
            player.TakeDamage(35f);
            Assert.AreEqual(max - 35, player.Health);
            player.Heal(10);
            Assert.AreEqual(max - 25, player.Health);
            player.RecalculateStats();
            Assert.AreEqual(max - 25, player.Health, "Recalculating stats must not heal.");
            player.Heal(max);
            Assert.AreEqual(max, player.Health);
            player.TakeDamage(max * 2);
            Assert.IsFalse(player.IsAlive);
            Assert.AreEqual(0, player.Health);
            player.Heal(max);
            Assert.AreEqual(0, player.Health, "Healing must not revive a defeated player.");

            yield return null;
            player.Revive();
            Assert.IsTrue(player.IsAlive);
            Assert.AreEqual(max, player.Health);
            Assert.AreEqual(1f, player.Health01);
        }

        [UnityTest]
        public IEnumerator SpawnProtectionBlocksDamageUntilExpiryWithoutClearingEnemies()
        {
            var player = Player();
            var first = Enemy(Vector3.zero);
            var second = Enemy(Vector3.forward * 5f);
            GameFlow.Blocked = false;
            // No terrain fixture: set the same timestamp Respawn assigns, without a timed wait.
            var protection = typeof(PlayerSphereController).GetField("_protectedUntil",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(protection);
            protection.SetValue(player, Time.time + 10f);
            Assert.IsTrue(player.IsProtected);
            player.TakeDamage(player.MaxHealth * 2);
            Assert.AreEqual(player.MaxHealth, player.Health);
            yield return null;
            Assert.AreEqual(2, EnemyTriangle.All.Count);
            Assert.AreEqual(first.MaxHealth, first.Health);
            Assert.AreEqual(second.MaxHealth, second.Health);

            protection.SetValue(player, Time.time - 1f);
            Assert.IsFalse(player.IsProtected);
            player.TakeDamage(20f);
            Assert.AreEqual(player.MaxHealth - 20, player.Health, "Damage resumes after spawn protection.");
            player.TakeDamage(player.MaxHealth * 2);
            Assert.IsFalse(player.IsAlive);
            protection.SetValue(player, Time.time + 10f);
            player.Revive();
            Assert.IsTrue(player.IsAlive);
            Assert.AreEqual(player.MaxHealth, player.Health);
            player.TakeDamage(10f);
            Assert.AreEqual(player.MaxHealth - 10, player.Health,
                "Revive without terrain must not retain an old protection timestamp.");
            Assert.AreEqual(2, EnemyTriangle.All.Count);
        }

        [UnityTest]
        public IEnumerator JoystickOwnsOnePointerAndReleasesInputWhenMenuOpens()
        {
            var canvasRect = UIFactory.CreateCanvas("TestTouchCanvas", 0);
            _created.Add(canvasRect.gameObject);
            var area = UIFactory.CreateRect("TestTouchArea", canvasRect);
            area.gameObject.SetActive(false);
            UIFactory.Stretch(area);
            var joystick = area.gameObject.AddComponent<VirtualJoystick>();
            joystick.SetShowInEditor(true);
            joystick.SetRadius(100f);
            area.gameObject.SetActive(true);
            var events = Create("TestEventSystem").AddComponent<EventSystem>();
            GameFlow.Blocked = false;
            yield return null;
            Canvas.ForceUpdateCanvases();

            var origin = RectTransformUtility.WorldToScreenPoint(null, area.position);
            var pointer = new PointerEventData(events) { pointerId = 1, position = origin };
            joystick.OnPointerDown(pointer);
            pointer.position += Vector2.right * (75f * canvasRect.GetComponent<Canvas>().scaleFactor);
            joystick.OnDrag(pointer);
            Assert.Greater(joystick.Value.x, .5f);
            Assert.LessOrEqual(joystick.Value.magnitude, 1f);
            var held = joystick.Value;
            var other = new PointerEventData(events) { pointerId = 2, position = origin - Vector2.right * 200f };
            joystick.OnDrag(other);
            joystick.OnPointerUp(other);
            Assert.AreEqual(held, joystick.Value, "Another finger must not steer/release this joystick.");

            GameFlow.Blocked = true;
            yield return null;
            yield return null;
            Assert.AreEqual(Vector2.zero, joystick.Value, "Opening a menu must clear held touch input.");
            GameFlow.Blocked = false;
            joystick.OnPointerDown(pointer);
            pointer.position += Vector2.right * 100f;
            joystick.OnDrag(pointer);
            Assert.Greater(joystick.Value.x, 0f);
            area.gameObject.SetActive(false);
            Assert.AreEqual(Vector2.zero, joystick.Value, "Disabling a touch zone must also release input.");
        }

        [UnityTest]
        public IEnumerator PoolRegistryReusesObjectAndReparentsAcrossLifecycles()
        {
            var prefab = Create("TestPrefab");
            var registry = Create("TestRegistry").AddComponent<PoolRegistry>();
            var parent = Create("SpawnParent").transform;
            registry.Reserve(prefab, 1);
            var pool = registry.GetPool(prefab);
            Assert.AreEqual(1, pool.Free);
            var first = registry.Spawn(prefab, Vector3.one, Quaternion.identity, parent);
            Assert.IsTrue(first.activeSelf);
            Assert.AreEqual(parent, first.transform.parent);
            Assert.AreEqual(Vector3.one, first.transform.position);
            registry.Despawn(first);
            yield return null;
            Assert.IsFalse(first.activeSelf);
            Assert.AreEqual(0, pool.Active);
            Assert.AreEqual(1, pool.Free);
            var second = registry.Spawn(prefab, Vector3.right * 3f, Quaternion.Euler(0, 90, 0), parent);
            Assert.AreSame(first, second);
            Assert.AreEqual(1, pool.Total, "Reusing a reserved instance must not allocate another object.");
            Assert.AreEqual(Vector3.right * 3f, second.transform.position);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0, 90, 0), second.transform.rotation), .01f);
        }
    }
}

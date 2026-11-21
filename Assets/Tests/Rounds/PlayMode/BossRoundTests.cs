using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Player;
using Game.Pokemon;
using Game.Rounds;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.Rounds
{
    public sealed class BossRoundTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> _objects = new List<GameObject>();
        readonly Dictionary<string, int?> _prefs = new Dictionary<string, int?>();
        RoundManager _round;
        PlayerSphereController _player;
        bool _blocked, _over;
        Random.State _random;

        static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Private).SetValue(target, value);
        static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Private).Invoke(target, args);

        [SetUp]
        public void Setup()
        {
            Assert.IsNull(RoundManager.Instance, "Requires empty scene.");
            Assert.IsNull(PlayerSphereController.Instance);
            Assert.AreEqual(0, EnemyTriangle.All.Count);
            _blocked = GameFlow.Blocked;
            _over = GameFlow.Over;
            _random = Random.state;
            foreach (string key in new[] { "pketer.enemigos", "pketer.rondas" })
                _prefs[key] = PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null;
            GameFlow.Blocked = false;
            GameFlow.Over = false;
            _round = Create("RoundTest").AddComponent<RoundManager>();
            _round.enabled = false; // manually drive lifecycle, no input or progression saves
            _round.Drinks.enabled = false;
            _round.ShotStations.enabled = false;
            _player = Create("PlayerTest").AddComponent<PlayerSphereController>();
            _player.enabled = false;
        }

        GameObject Create(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var enemy in new List<EnemyTriangle>(EnemyTriangle.All))
                if (enemy != null) Object.Destroy(enemy.gameObject);
            foreach (var burst in Object.FindObjectsByType<HitBurst>()) Object.Destroy(burst.gameObject);
            foreach (var go in _objects) if (go != null) Object.Destroy(go);
            yield return null;
            foreach (var pref in _prefs)
                if (pref.Value.HasValue) PlayerPrefs.SetInt(pref.Key, pref.Value.Value);
                else PlayerPrefs.DeleteKey(pref.Key);
            _prefs.Clear();
            _objects.Clear();
            GameFlow.Blocked = _blocked;
            GameFlow.Over = _over;
            Random.state = _random;
        }

        EnemyTriangle BeginBoss()
        {
            Call(_round, "StartRound", 5);
            Assert.IsTrue(_round.IsBossRound);
            // The expanded catalog's alphabetical legendary/mythical rotation starts at Celebi.
            Assert.AreEqual("Celebi", _round.BossName);
            Assert.AreEqual(1, _round.EnemiesThisRound);
            Call(_round, "BeginFighting");
            Call(_round, "Update"); // real CountAlive + spawn lifecycle
            Assert.AreEqual(1, EnemyTriangle.All.Count);
            return EnemyTriangle.All[0];
        }

        [UnityTest]
        public IEnumerator BossIsExactlyOneWithoutReinforcementsAndDeathAdvancesToNormalRound()
        {
            var boss = BeginBoss();
            Assert.IsTrue(boss.IsBoss);
            Assert.AreEqual(216, boss.MaxHealth);
            Assert.AreEqual(39f, boss.ContactDamage);
            Assert.AreEqual(3.12f, boss.Speed, .0001f);
            Assert.AreEqual("species_celebi", boss.GetComponentInChildren<SpriteRenderer>().sprite.name);
            for (int i = 0; i < 10; i++)
            {
                Set(_round, "_spawnTimer", 100f);
                Call(_round, "Update");
                Assert.AreEqual(1, EnemyTriangle.All.Count);
            }
            Assert.IsTrue(boss.TakeDamage(boss.MaxHealth));
            int earned = _round.Points;
            _round.NotifyEnemyKilled(boss);
            Assert.AreEqual(earned, _round.Points, "duplicate death must not pay twice");
            Assert.AreEqual(1, _round.EnemiesDefeatedThisRound);
            Assert.That(earned, Is.EqualTo(280).Or.EqualTo(560), "28 replaced kills, optional equivalent bonus");
            Assert.AreEqual(28, _round.CoinsEarned);
            Call(_round, "Update");
            Assert.AreEqual(RoundManager.RoundPhase.Clearing, _round.Phase);
            Assert.AreEqual(1, _round.RoundsPassed);
            yield return null;
            Assert.AreEqual(0, EnemyTriangle.All.Count);
            Set(_round, "_clearRemaining", 0f);
            Call(_round, "Update");
            Assert.AreEqual(6, _round.Round);
            Assert.IsFalse(_round.IsBossRound);
            Assert.IsNull(_round.BossName);
            Assert.AreEqual(32, _round.EnemiesThisRound);
            Call(_round, "BeginFighting");
            Call(_round, "Update");
            Assert.AreEqual(1, EnemyTriangle.All.Count);
            Assert.IsFalse(EnemyTriangle.All[0].IsBoss);
            Assert.AreEqual(24, EnemyTriangle.All[0].Health);
            Assert.AreEqual(17.75f, EnemyTriangle.All[0].ContactDamage);
            Assert.AreEqual(4.32f, EnemyTriangle.All[0].Speed, .0001f);
        }

        [UnityTest]
        public IEnumerator StopClearsBossAndNewRoundDoesNotRetainBossBudgetOrMetadata()
        {
            BeginBoss();
            // Do not finish/pay a run in this fixture; test the real stop cleanup in idle.
            Set(_round, "_phase", RoundManager.RoundPhase.Idle);
            _round.StopRun();
            yield return null;
            Assert.AreEqual(0, EnemyTriangle.All.Count);
            Assert.IsFalse(_round.IsBossRound);
            Assert.IsNull(_round.BossName);
            Call(_round, "StartRound", 1);
            Assert.AreEqual(12, _round.EnemiesThisRound);
            GameFlow.Blocked = false;
            Call(_round, "BeginFighting");
            Call(_round, "Update");
            Assert.AreEqual(1, EnemyTriangle.All.Count);
            Assert.AreEqual(14, EnemyTriangle.All[0].Health);
            Assert.AreEqual(3.92f, EnemyTriangle.All[0].Speed, .0001f);
            Assert.IsFalse(EnemyTriangle.All[0].IsBoss);
        }

        [UnityTest]
        public IEnumerator BossAndNormalContactDamagePlayerWithoutHurtingEnemiesAndRespectSpawnProtection()
        {
            var boss = BeginBoss();
            var normal = Create("ContactNormal").AddComponent<EnemyTriangle>();
            normal.InitializeRound(1, RoundDifficulty.Stats(1), false);
            yield return null;
            // No terrain in this fixture; drive contact directly instead of Update's movement.
            Assert.Greater(Time.deltaTime, 0f);
            float health = _player.Health01 * _player.MaxHealth;
            Call(boss, "ApplyContact", _player);
            Assert.AreEqual(health - boss.ContactDamage * Time.deltaTime,
                _player.Health01 * _player.MaxHealth, .001f);
            health = _player.Health01 * _player.MaxHealth;
            Call(normal, "ApplyContact", _player);
            Assert.AreEqual(health - normal.ContactDamage * Time.deltaTime,
                _player.Health01 * _player.MaxHealth, .001f);

            // Spawn protection prevents damage but never kills enemies on contact.
            Set(_player, "_protectedUntil", Time.time + 10f);
            health = _player.Health01;
            Call(boss, "ApplyContact", _player);
            Call(normal, "ApplyContact", _player);
            Assert.AreEqual(health, _player.Health01);
            Set(_player, "_protectedUntil", Time.time - 1f);
            Call(boss, "ApplyContact", _player);
            Assert.Less(_player.Health01, health, "boss contact resumes after protection expires");
            Assert.AreEqual(boss.MaxHealth, boss.Health);
            Assert.AreEqual(normal.MaxHealth, normal.Health);
            Assert.IsFalse(boss.IsDead);
            Assert.IsFalse(normal.IsDead);
            Assert.AreEqual(0, _round.EnemiesDefeatedThisRound, "contact must not grant kill rewards");
        }

        [UnityTest]
        public IEnumerator ReinitializedEnemyResetsBossDamageHealthFlashAndReplacesVisual()
        {
            var go = Create("ReusableEnemy");
            var enemy = go.AddComponent<EnemyTriangle>();
            PokemonEnemyVisual.Attach(go, Resources.Load<Sprite>("PokemonSprites/species_mew"));
            enemy.InitializeRound(2, RoundDifficulty.Stats(5, true), true);
            enemy.TakeDamage(1);
            go.SetActive(false);
            enemy.InitializeRound(1, RoundDifficulty.Stats(1), false);
            PokemonEnemyVisual.Attach(go, Resources.Load<Sprite>("PokemonSprites/species_charizard"));
            go.SetActive(true);
            Assert.IsFalse(enemy.IsBoss);
            Assert.AreEqual(14, enemy.Health);
            Assert.AreEqual(14f, enemy.ContactDamage);
            Assert.AreEqual(Color.white, ((SpriteRenderer)enemy.VisualRenderer).color);
            yield return null;
            Assert.AreEqual(1, go.GetComponentsInChildren<PokemonEnemyVisual>().Length);
            Assert.AreEqual("Pokemon species_charizard", enemy.VisualRenderer.gameObject.name);
            // Two healthbar renderers, one replacement billboard and one reused ground shadow.
            Assert.AreEqual(4, go.GetComponentsInChildren<SpriteRenderer>(true).Length);
            Assert.AreEqual(1, go.GetComponents<Game.Rendering.GroundContactShadow>().Length,
                "Reinitializing a pooled actor must not stack contact shadows.");
            enemy.Initialize(1, 2.5f);
            Assert.IsFalse(enemy.IsBoss);
            Assert.AreEqual(EnemyTriangle.ContactDamagePerSecond, enemy.ContactDamage);
        }

        [TestCase(1, 12, 8)]
        [TestCase(2, 16, 8)]
        [TestCase(3, 20, 10)]
        [TestCase(5, 1, 1)]
        [TestCase(6, 32, 12)]
        [TestCase(10, 1, 1)]
        [TestCase(21, 92, 28)]
        [TestCase(22, 96, 28)]
        [TestCase(int.MaxValue, 96, 28)]
        public void RoundBudgetsDoubleNormalsAndKeepOneBoss(int round, int total, int alive)
        {
            Call(_round, "StartRound", round);
            Assert.AreEqual(total, _round.EnemiesThisRound);
            Assert.AreEqual(alive, Call(_round, "MaxAliveForRound"));
        }

        [UnityTest]
        public IEnumerator NormalWaveReplenishesLiveSlotsWithoutExceedingTotalBudget()
        {
            Call(_round, "StartRound", 1);
            Call(_round, "BeginFighting");
            for (int i = 0; i < 20; i++)
            {
                Set(_round, "_spawnTimer", 100f);
                Call(_round, "Update");
            }
            Assert.AreEqual(8, EnemyTriangle.All.Count);
            var firstBatch = new List<EnemyTriangle>(EnemyTriangle.All);
            for (int i = 0; i < 4; i++) firstBatch[i].TakeDamage(firstBatch[i].MaxHealth);
            yield return null;
            for (int i = 0; i < 20; i++)
            {
                Set(_round, "_spawnTimer", 100f);
                Call(_round, "Update");
            }
            Assert.AreEqual(8, EnemyTriangle.All.Count);
            Assert.AreEqual(4, _round.EnemiesDefeatedThisRound);
            foreach (var enemy in new List<EnemyTriangle>(EnemyTriangle.All)) enemy.TakeDamage(enemy.MaxHealth);
            yield return null;
            Call(_round, "Update");
            Assert.AreEqual(12, _round.EnemiesDefeatedThisRound);
            Assert.AreEqual(0, EnemyTriangle.All.Count);
            Assert.AreEqual(RoundManager.RoundPhase.Clearing, _round.Phase);
        }
    }
}

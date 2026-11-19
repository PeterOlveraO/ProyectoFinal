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
            Assert.AreEqual("Mew", _round.BossName);
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
            Assert.AreEqual("species_mew", boss.GetComponentInChildren<SpriteRenderer>().sprite.name);
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
            Assert.That(earned, Is.EqualTo(140).Or.EqualTo(280), "14 replaced kills, optional equivalent bonus");
            Assert.AreEqual(14, _round.CoinsEarned);
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
            Call(_round, "BeginFighting");
            Call(_round, "Update");
            Assert.AreEqual(1, EnemyTriangle.All.Count);
            Assert.IsFalse(EnemyTriangle.All[0].IsBoss);
            Assert.AreEqual(20, EnemyTriangle.All[0].Health);
            Assert.AreEqual(13.75f, EnemyTriangle.All[0].ContactDamage);
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
            Assert.AreEqual(6, _round.EnemiesThisRound);
            GameFlow.Blocked = false;
            Call(_round, "BeginFighting");
            Call(_round, "Update");
            Assert.AreEqual(1, EnemyTriangle.All.Count);
            Assert.AreEqual(10, EnemyTriangle.All[0].Health);
            Assert.AreEqual(2.8f, EnemyTriangle.All[0].Speed);
            Assert.IsFalse(EnemyTriangle.All[0].IsBoss);
        }

        [UnityTest]
        public IEnumerator SpecialDamagesBossOncePerActivationButKeepsNormalKillsAndResetsCleanly()
        {
            var boss = BeginBoss();
            var normal = Create("SpecialNormal").AddComponent<EnemyTriangle>();
            normal.InitializeRound(1, RoundDifficulty.Stats(1), false);
            int quarter = boss.MaxHealth / 4;
            int playerHealth = _player.Health;
            Assert.IsTrue(_player.ActivateInvincibility());
            Assert.AreEqual(quarter * 3, boss.Health);
            Assert.IsTrue(normal.IsDead);
            Assert.IsTrue(_player.IsInvincible);
            Assert.IsFalse(_player.ActivateInvincibility(), "cooldown rejects duplicate activation");
            Assert.AreEqual(quarter * 3, boss.Health);
            for (int frame = 0; frame < 10; frame++)
                Call(boss, "ApplyContact", _player);
            Assert.AreEqual(quarter * 3, boss.Health, "invincible contact must not damage/kill the boss");
            Assert.AreEqual(playerHealth, _player.Health);
            var contactNormal = Create("ContactNormal").AddComponent<EnemyTriangle>();
            contactNormal.Initialize(1, 2.8f);
            Call(contactNormal, "ApplyContact", _player);
            Assert.IsTrue(contactNormal.IsDead, "normal enemies still die on invincible contact");
            yield return null;
            Assert.IsTrue(normal == null);
            Assert.IsTrue(contactNormal == null);
            Assert.IsFalse(boss.IsDead);

            // Advance only the cooldown timestamp, without a 30-second wall-clock test.
            // Each call still goes through the real Player activation guard and damage API.
            for (int remainingQuarters = 2; remainingQuarters >= 0; remainingQuarters--)
            {
                Set(_player, "_powerReadyAt", Time.time - 1f);
                Assert.IsTrue(_player.ActivateInvincibility());
                Assert.AreEqual(quarter * remainingQuarters, boss.Health);
            }
            Assert.IsTrue(boss.IsDead, "four separated activations can finish a full-health boss");
            Assert.AreEqual(1, _round.EnemiesDefeatedThisRound);
            yield return null;
            Assert.AreEqual(0, EnemyTriangle.All.Count);
            _player.Revive();
            Assert.IsTrue(_player.IsPowerReady);
            Assert.IsFalse(_player.IsInvincible);
            boss = Create("ReusedBoss").AddComponent<EnemyTriangle>();
            boss.InitializeRound(2, RoundDifficulty.Stats(5, true), true);
            Assert.AreEqual(216, boss.Health, "new initialization has no retained special damage");
            Assert.IsTrue(_player.ActivateInvincibility());
            Assert.AreEqual(162, boss.Health);
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
            Assert.AreEqual(10, enemy.Health);
            Assert.AreEqual(10f, enemy.ContactDamage);
            Assert.AreEqual(Color.white, ((SpriteRenderer)enemy.VisualRenderer).color);
            yield return null;
            Assert.AreEqual(1, go.GetComponentsInChildren<PokemonEnemyVisual>().Length);
            Assert.AreEqual("Pokemon species_charizard", enemy.VisualRenderer.gameObject.name);
            // Two healthbar renderers plus exactly one replacement billboard.
            Assert.AreEqual(3, go.GetComponentsInChildren<SpriteRenderer>(true).Length);
            enemy.Initialize(1, 2.5f);
            Assert.IsFalse(enemy.IsBoss);
            Assert.AreEqual(EnemyTriangle.ContactDamagePerSecond, enemy.ContactDamage);
        }
    }
}

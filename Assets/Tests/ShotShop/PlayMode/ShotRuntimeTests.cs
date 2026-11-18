using System.Collections.Generic;
using System.Reflection;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Player;
using Game.Player.ProgressionCombat;
using Game.Pokemon.Progression;
using Game.Rounds;
using NUnit.Framework;
using UnityEngine;

namespace Game.ShotShop.Tests
{
    /// <summary>Synchronous PlayMode tests: no scene bootstrap, saves, coroutines or input devices.</summary>
    public sealed class ShotRuntimeTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> _objects = new List<GameObject>();
        readonly HashSet<HitBurst> _existingEffects = new HashSet<HitBurst>();
        RoundManager _round;
        PlayerSphereController _player;
        PlayerAttack _attack;
        bool _blocked, _over;

        [SetUp]
        public void Setup()
        {
            Assert.IsNull(RoundManager.Instance, "Run in an empty test scene, not the live gameplay scene.");
            Assert.IsNull(PlayerSphereController.Instance);
            _blocked = GameFlow.Blocked;
            _over = GameFlow.Over;
            foreach (var effect in Object.FindObjectsByType<HitBurst>()) _existingEffects.Add(effect);
            _round = Create("TestRound").AddComponent<RoundManager>();
            _round.enabled = false;
            _round.Drinks.enabled = false;
            _round.ShotStations.enabled = false;
            _player = Create("TestPlayer").AddComponent<PlayerSphereController>();
            _player.enabled = false;
            _attack = _player.gameObject.AddComponent<PlayerAttack>();
            _attack.enabled = false;
            Set(_round, "_phase", RoundManager.RoundPhase.Preparing);
            Set(_round, "<Points>k__BackingField", 300);
            Set(_round.ShotStations, "_positions", System.Array.AsReadOnly(new[] { Vector3.zero, Vector3.right * 10, Vector3.right * 20 }));
            GameFlow.Blocked = false;
            GameFlow.Over = false;
            _attack.ResetRun();
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var projectile in Projectiles()) Object.DestroyImmediate(projectile.gameObject);
            foreach (var effect in Object.FindObjectsByType<HitBurst>())
                if (!_existingEffects.Contains(effect)) Object.DestroyImmediate(effect.gameObject);
            foreach (var go in _objects) if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
            _existingEffects.Clear();
            GameFlow.Blocked = _blocked;
            GameFlow.Over = _over;
        }

        GameObject Create(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        static void Set(object instance, string field, object value) =>
            instance.GetType().GetField(field, Fields).SetValue(instance, value);
        static T Get<T>(object instance, string field) => (T)instance.GetType().GetField(field, Fields).GetValue(instance);
        static object Call(object instance, string method, params object[] args) =>
            instance.GetType().GetMethod(method, Fields).Invoke(instance, args);
        static AttackProjectile[] Projectiles() =>
            Object.FindObjectsByType<AttackProjectile>(FindObjectsInactive.Include);
        void Tick(float delta, bool aiming, Vector3 axis) => Call(_attack, "TickAttack", delta, aiming, axis);

        [Test]
        public void ShopUsesRunPointsOnceAndRejectsRemotePausedDeadAndFinishedPurchases()
        {
            int points = _round.Points;
            Assert.IsFalse(_round.BuyShot(1), "cannot buy a remote offer");
            GameFlow.Blocked = true;
            Assert.IsFalse(_round.BuyShot(0));
            GameFlow.Blocked = false;
            Set(_player, "_alive", false);
            Assert.IsFalse(_round.BuyShot(0));
            Set(_player, "_alive", true);
            Set(_round, "<Points>k__BackingField", 59);
            Assert.IsFalse(_round.BuyShot(0));
            Set(_round, "<Points>k__BackingField", points);
            Assert.IsTrue(_round.BuyShot(0));
            Assert.AreEqual(points - 60, _round.Points);
            Assert.AreEqual(0, _round.CoinsEarned);
            Assert.AreEqual(0, _round.TotalPointsEarned, "spending is not earning");
            Assert.AreEqual(ShotType.Burst, _round.Shots.Equipped);
            Assert.IsTrue(_round.EquipShot(ShotType.Basic));
            Assert.IsTrue(_round.BuyShot(0));
            Assert.AreEqual(points - 60, _round.Points);
            _player.transform.position = Vector3.one * 100;
            Assert.IsTrue(_round.EquipShot(ShotType.Basic));
            Assert.IsTrue(_round.EquipShot(ShotType.Burst), "owned shots can be equipped anywhere");
            Set(_round, "_phase", RoundManager.RoundPhase.GameOver);
            Assert.IsFalse(_round.BuyShot(0));
            Assert.IsFalse(_round.EquipShot(ShotType.Basic));
            Assert.AreEqual(points - 60, _round.Points);
        }

        [Test]
        public void BurstCapturesManualAxisAndDamageForExactlyThreePulses()
        {
            Assert.IsTrue(_round.BuyShot(0));
            Tick(0f, true, Vector3.forward);
            Assert.AreEqual(1, Projectiles().Length);
            int capturedDamage = _attack.Damage;
            Get<bool[]>(_round.Drinks, "_purchased")[(int)DrinkType.Power] = true;
            Tick(.12f, true, Vector3.right);
            Assert.AreEqual(2, Projectiles().Length);
            Tick(.12f, true, Vector3.left);
            Assert.AreEqual(3, Projectiles().Length);
            foreach (var projectile in Projectiles())
            {
                Assert.That(Vector3.Distance(Get<Vector3>(projectile, "_direction"), Vector3.forward), Is.LessThan(.0001f));
                Assert.AreEqual(capturedDamage, Get<int>(projectile, "_damage"));
            }
            Tick(.12f, true, Vector3.back);
            Assert.AreEqual(3, Projectiles().Length);
        }

        [TestCase("release")]
        [TestCase("pause")]
        [TestCase("over")]
        [TestCase("switch")]
        [TestCase("reset")]
        [TestCase("disable")]
        [TestCase("dead")]
        [TestCase("idle")]
        public void PendingBurstNeverFiresAfterCancellation(string reason)
        {
            Assert.IsTrue(_round.BuyShot(0));
            Tick(0f, true, Vector3.forward);
            Assert.AreEqual(2, Get<int>(_attack, "_burstRemaining"));
            switch (reason)
            {
                case "release": Tick(.1f, false, Vector3.zero); break;
                case "pause": GameFlow.Blocked = true; Tick(.2f, true, Vector3.right); break;
                case "over": GameFlow.Over = true; Tick(.2f, true, Vector3.right); break;
                case "switch": Assert.IsTrue(_round.EquipShot(ShotType.Basic)); break;
                case "reset": _attack.ResetRun(); break;
                case "disable": Call(_attack, "OnDisable"); break;
                case "dead": Set(_player, "_alive", false); Tick(.2f, true, Vector3.right); break;
                case "idle": Set(_round, "_phase", RoundManager.RoundPhase.Idle); Tick(.2f, true, Vector3.right); break;
            }
            Assert.AreEqual(0, Get<int>(_attack, "_burstRemaining"));
            Assert.AreEqual(1, Projectiles().Length);
        }

        [Test]
        public void PowerAndRapidFireStillAffectEveryShotPattern()
        {
            Set(_round, "_runPokemon", new PokemonInstance());
            Set(_round, "_combatStats", new CombatStatBlock { damage = 7, maxHealth = 100 });
            var purchased = Get<bool[]>(_round.Drinks, "_purchased");
            purchased[(int)DrinkType.Power] = true;
            purchased[(int)DrinkType.RapidFire] = true;
            for (int i = 0; i < ShotDefinitions.Count; i++)
            {
                var type = (ShotType)i;
                _round.Shots.TryBuyAndEquip(type, _ => true);
                _attack.ResetRun();
                Tick(0f, true, Vector3.forward);
                foreach (var projectile in Projectiles())
                    Assert.AreEqual(_attack.Damage, Get<int>(projectile, "_damage"));
                Assert.AreEqual(10, _attack.Damage, "combat stats + Power, not serialized base damage");
                if (type == ShotType.Burst) { Tick(.12f, true, Vector3.forward); Tick(.12f, true, Vector3.forward); }
                int before = Projectiles().Length;
                Tick(.45f, true, Vector3.forward);
                Assert.AreEqual(before, Projectiles().Length);
                Tick(.26f, true, Vector3.forward);
                Assert.Greater(Projectiles().Length, before);
                foreach (var projectile in Projectiles()) Object.DestroyImmediate(projectile.gameObject);
            }
        }

        EnemyTriangle Enemy(float z)
        {
            var enemy = Create("TestEnemy").AddComponent<EnemyTriangle>();
            enemy.Initialize(1, 1f);
            enemy.transform.position = Vector3.forward * z;
            return enemy;
        }

        [Test]
        public void FanSpawnsExactlyThreeFixedDirectionsWithNoSteeringTowardAnEnemy()
        {
            _player.transform.position = Vector3.right * 10;
            Assert.IsTrue(_round.BuyShot(1));
            Enemy(-3f);
            Tick(0f, true, Vector3.right);
            var projectiles = Projectiles();
            Assert.AreEqual(3, projectiles.Length);
            for (int pellet = 0; pellet < 3; pellet++)
            {
                Vector3 expected = ShotDefinitions.FanDirection(Vector3.right, pellet);
                Assert.IsTrue(System.Array.Exists(projectiles, projectile =>
                    Vector3.Distance(Get<Vector3>(projectile, "_direction"), expected) < .0001f));
            }
        }

        [Test]
        public void NearbyOfferRequiresHorizontalAndVerticalProximity()
        {
            Assert.AreEqual(0, _round.ShotStations.NearbyOffer(Vector3.zero));
            Assert.AreEqual(-1, _round.ShotStations.NearbyOffer(Vector3.up * 3f));
            Assert.AreEqual(-1, _round.ShotStations.NearbyOffer(Vector3.forward * 4f));
            Assert.IsFalse(_round.BuyShot(-1));
            Assert.IsFalse(_round.BuyShot(3));
        }

        [Test]
        public void PiercingSweepsNearestFirstAndDamagesAtMostThreeDistinctTargets()
        {
            var farthest = Enemy(4f);
            var third = Enemy(3f);
            var second = Enemy(2f);
            var first = Enemy(1f); // deliberately opposite to registration order
            var projectile = AttackProjectile.Spawn(Vector3.zero, Vector3.forward, 15, 18, 2, Color.white, 3);
            projectile.transform.position = Vector3.forward;
            Assert.IsFalse((bool)Call(projectile, "CheckHit"));
            Assert.IsFalse((bool)Call(projectile, "CheckHit"));
            Assert.AreEqual(8, first.Health, "same enemy must not be hit twice");
            projectile.transform.position = Vector3.forward * 5;
            Assert.IsTrue((bool)Call(projectile, "CheckHit"));
            Assert.AreEqual(8, first.Health);
            Assert.AreEqual(8, second.Health);
            Assert.AreEqual(8, third.Health);
            Assert.AreEqual(10, farthest.Health);
            Assert.IsFalse(projectile.gameObject.activeSelf);
        }

        [Test]
        public void BasicStopsAtFirstTargetAndPauseFreezesExistingProjectiles()
        {
            var second = Enemy(2f);
            var first = Enemy(1f);
            var projectile = AttackProjectile.Spawn(Vector3.zero, Vector3.forward, 15, 18, 2, Color.white);
            GameFlow.Blocked = true;
            Call(projectile, "Update");
            Assert.AreEqual(Vector3.zero, projectile.transform.position);
            Assert.AreEqual(10, first.Health);
            GameFlow.Blocked = false;
            projectile.transform.position = Vector3.forward * 4;
            Assert.IsTrue((bool)Call(projectile, "CheckHit"));
            Assert.AreEqual(8, first.Health);
            Assert.AreEqual(10, second.Health);
        }

        [Test]
        public void ExistingProjectileCannotDamageAfterGameOver()
        {
            var enemy = Enemy(0f);
            var projectile = AttackProjectile.Spawn(Vector3.zero, Vector3.forward, 15, 18, 2, Color.white);
            GameFlow.Over = true;
            Call(projectile, "Update");
            Assert.AreEqual(10, enemy.Health);
            Assert.IsFalse(projectile.gameObject.activeSelf);
        }

        [Test]
        public void TemporaryResetClearsOwnershipPendingPulsesAndInFlightProjectiles()
        {
            Assert.IsTrue(_round.BuyShot(0));
            Tick(0f, true, Vector3.forward);
            Call(_round, "ResetTemporaryStats");
            Assert.AreEqual(ShotType.Basic, _round.Shots.Equipped);
            Assert.IsFalse(_round.Shots.Has(ShotType.Burst));
            Assert.AreEqual(0, Get<int>(_attack, "_burstRemaining"));
            Assert.AreEqual(0, Object.FindObjectsByType<AttackProjectile>().Length);
            Tick(.5f, false, Vector3.zero);
            Assert.AreEqual(0, Object.FindObjectsByType<AttackProjectile>().Length);
        }
    }
}

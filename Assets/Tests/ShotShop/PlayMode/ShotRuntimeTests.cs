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
        System.Func<int, bool> _zoneAccess;
        System.Func<Vector3, Vector3, bool> _projectileAccess;

        [SetUp]
        public void Setup()
        {
            Assert.IsNull(RoundManager.Instance, "Run in an empty test scene, not the live gameplay scene.");
            Assert.IsNull(PlayerSphereController.Instance);
            _blocked = GameFlow.Blocked;
            _over = GameFlow.Over;
            _zoneAccess = WorldShopPlacement.IsZoneAccessible;
            _projectileAccess = AttackProjectile.CanTravelSegment;
            WorldShopPlacement.IsZoneAccessible = null;
            AttackProjectile.CanTravelSegment = null;
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
            Set(_round, "<Points>k__BackingField", 5000);
            GameFlow.Blocked = false;
            GameFlow.Over = false;
            var positions = new Vector3[ShotStations.OfferCount];
            for (int i = 0; i < positions.Length; i++) positions[i] = Vector3.right * (10 * i);
            Set(_round.ShotStations, "_positions", System.Array.AsReadOnly(positions));
            // Other tests exercise construction separately; combat fixtures start with vendors built.
            for (int i = 0; i < ShotStations.OfferCount; i++)
            {
                _player.transform.position = Vector3.right * (10 * i);
                Assert.IsTrue(_round.ShotStations.Build(i));
            }
            _player.transform.position = Vector3.zero;
            Set(_round, "<Points>k__BackingField", 1000);
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
            WorldShopPlacement.IsZoneAccessible = _zoneAccess;
            AttackProjectile.CanTravelSegment = _projectileAccess;
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
        public void ShopChargesEveryPurchaseAndRejectsRemotePausedDeadAndFinishedPurchases()
        {
            int points = _round.Points;
            Assert.IsFalse(_round.BuyShot(1), "cannot buy a remote offer");
            GameFlow.Blocked = true;
            Assert.IsFalse(_round.BuyShot(0));
            GameFlow.Blocked = false;
            Set(_player, "_alive", false);
            Assert.IsFalse(_round.BuyShot(0));
            Set(_player, "_alive", true);
            Set(_round, "<Points>k__BackingField", ShotDefinitions.Price(ShotType.Burst) - 1);
            Assert.IsFalse(_round.BuyShot(0));
            Set(_round, "<Points>k__BackingField", points);
            Assert.IsTrue(_round.BuyShot(0));
            Assert.AreEqual(points - ShotDefinitions.Price(ShotType.Burst), _round.Points);
            Assert.AreEqual(0, _round.CoinsEarned);
            Assert.AreEqual(0, _round.TotalPointsEarned, "spending is not earning");
            Assert.AreEqual(ShotType.Burst, _round.Shots.Equipped);
            Assert.IsFalse(_round.EquipShot(ShotType.Basic));
            Assert.IsTrue(_round.BuyShot(0));
            Assert.AreEqual(points - 2 * ShotDefinitions.Price(ShotType.Burst), _round.Points);
            _player.transform.position = Vector3.one * 100;
            Assert.IsFalse(_round.EquipShot(ShotType.Basic));
            Assert.IsFalse(_round.EquipShot(ShotType.Burst), "there is no free equip API");
            Set(_round, "_phase", RoundManager.RoundPhase.GameOver);
            Assert.IsFalse(_round.BuyShot(0));
            Assert.IsFalse(_round.EquipShot(ShotType.Basic));
            Assert.AreEqual(points - 2 * ShotDefinitions.Price(ShotType.Burst), _round.Points);
        }

        [Test]
        public void ReturningToBasicRequiresItsBuiltArsenalAndFullProductPayment()
        {
            int before = _round.Points;
            Assert.IsTrue(_round.BuyShot(0));
            Assert.IsFalse(_round.EquipShot(ShotType.Basic));
            Assert.IsFalse(_round.BuyShot(3), "Basic is not remotely purchasable");
            _player.transform.position = Vector3.right * 30;
            Assert.IsTrue(_round.BuyShot(3));
            Assert.AreEqual(ShotType.Basic, _round.Shots.Equipped);
            Assert.AreEqual(before - ShotDefinitions.Price(ShotType.Burst) - ShotDefinitions.Price(ShotType.Basic), _round.Points);
        }

        [TestCase("remote")]
        [TestCase("paused")]
        [TestCase("dead")]
        [TestCase("over")]
        [TestCase("poor")]
        public void ShotConstructionRejectsUnauthorizedOrUnaffordableInteraction(string reason)
        {
            _round.ShotStations.ResetRun();
            switch (reason)
            {
                case "remote": _player.transform.position = Vector3.one * 100; break;
                case "paused": GameFlow.Blocked = true; break;
                case "dead": Set(_player, "_alive", false); break;
                case "over": Set(_round, "_phase", RoundManager.RoundPhase.GameOver); break;
                case "poor": Set(_round, "<Points>k__BackingField", ShotStations.BuildPrice(0) - 1); break;
            }
            int before = _round.Points;
            Assert.IsFalse(_round.ShotStations.Build(0));
            Assert.IsFalse(_round.ShotStations.IsBuilt(0));
            Assert.AreEqual(before, _round.Points);
            Assert.AreEqual(ShotType.Basic, _round.Shots.Equipped);
        }

        [Test]
        public void ShotConstructionAndPurchaseAreSeparateAndNeverUnlockAcrossRuns()
        {
            _round.ShotStations.ResetRun();
            int points = _round.Points;
            Assert.IsFalse(_round.BuyShot(0));
            Assert.AreEqual(points, _round.Points);
            Assert.IsTrue(_round.ShotStations.Build(0));
            Assert.AreEqual(ShotType.Basic, _round.Shots.Equipped);
            Assert.IsFalse(_round.ShotStations.Build(0));
            Assert.IsTrue(_round.BuyShot(0));
            Assert.AreEqual(points - ShotStations.BuildPrice(0) - ShotDefinitions.Price(ShotType.Burst), _round.Points);
            _round.ShotStations.ResetRun();
            Assert.IsFalse(_round.BuyShot(0));
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
                case "switch": Assert.IsTrue(_round.BuyShot(0)); break;
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
                    Assert.AreEqual(_attack.Damage * (type == ShotType.Precision ? 2 : 1), Get<int>(projectile, "_damage"));
                Assert.AreEqual(10, _attack.Damage, "combat stats + Power, not serialized base damage");
                if (type == ShotType.Burst) { Tick(.12f, true, Vector3.forward); Tick(.12f, true, Vector3.forward); }
                int before = Projectiles().Length;
                float interval = ShotDefinitions.Interval(type, 1f) * .7f;
                Tick(interval - (type == ShotType.Burst ? .24f : 0f) - .01f, true, Vector3.forward);
                Assert.AreEqual(before, Projectiles().Length);
                Tick(.02f, true, Vector3.forward);
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
            Assert.IsFalse(_round.BuyShot(ShotStations.OfferCount));
        }

        [TestCase(4, ShotType.Precision)]
        [TestCase(5, ShotType.Boomerang)]
        [TestCase(6, ShotType.Explosive)]
        public void NewShotsRequireBothTransactionsAndExactAdvertisedPoints(int offer, ShotType type)
        {
            _round.ShotStations.ResetRun();
            _player.transform.position = Vector3.right * (10 * offer);
            int before = _round.Points;
            Assert.IsFalse(_round.BuyShot(offer));
            Assert.IsTrue(_round.ShotStations.Build(offer));
            Assert.AreEqual(ShotType.Basic, _round.Shots.Equipped);
            Assert.IsTrue(_round.BuyShot(offer));
            Assert.AreEqual(type, _round.Shots.Equipped);
            Assert.AreEqual(before - ShotStations.BuildPrice(offer) - ShotDefinitions.Price(type), _round.Points);
            Assert.IsTrue(_round.BuyShot(offer));
            Assert.AreEqual(before - ShotStations.BuildPrice(offer) - 2 * ShotDefinitions.Price(type), _round.Points);
        }

        [Test]
        public void LockedZoneHidesOfferAndRejectsConstructionAndPurchase()
        {
            int before = _round.Points;
            WorldShopPlacement.IsZoneAccessible = zone => zone == 1;
            Assert.AreEqual(-1, _round.ShotStations.NearbyOffer(_player.transform.position));
            Assert.IsFalse(_round.BuyShot(0), "even an already-built vendor must respect zone access");
            _round.ShotStations.ResetRun();
            Assert.IsFalse(_round.ShotStations.Build(0));
            Assert.AreEqual(before, _round.Points);
            WorldShopPlacement.IsZoneAccessible = _ => true;
            Assert.IsTrue(_round.ShotStations.Build(0));
            Assert.IsTrue(_round.BuyShot(0));
        }

        [TestCase(ShotType.Basic)]
        [TestCase(ShotType.Burst)]
        [TestCase(ShotType.Fan)]
        [TestCase(ShotType.Piercing)]
        [TestCase(ShotType.Precision)]
        [TestCase(ShotType.Boomerang)]
        [TestCase(ShotType.Explosive)]
        public void BallisticsExtendsEveryPatternWithoutChangingItsManualAxis(ShotType type)
        {
            Get<bool[]>(_round.Drinks, "_purchased")[(int)DrinkType.Ballistics] = true;
            _round.Shots.TryBuyAndEquip(type, _ => true);
            _attack.ResetRun();
            Tick(0f, true, Vector3.right);
            foreach (var projectile in Projectiles())
            {
                Assert.That(Get<float>(projectile, "_speed"),
                    Is.EqualTo(ShotDefinitions.Speed(type, 15f) * 1.25f).Within(.001f));
                Assert.That(Get<float>(projectile, "_range"),
                    Is.EqualTo(ShotDefinitions.Range(type, 18f) * 1.2f).Within(.001f));
                if (type != ShotType.Fan)
                    Assert.That(Vector3.Distance(Get<Vector3>(projectile, "_direction"), Vector3.right), Is.LessThan(.001f));
            }
        }

        [Test]
        public void DamageEffectIsCapturedOnceForTheEntireBurst()
        {
            Assert.IsTrue(_round.BuyShot(0));
            Assert.IsTrue(_player.RunEffects.Apply(TemporaryEffect.Overcharge));
            int modified = _attack.Damage;
            Tick(0f, true, Vector3.forward);
            _player.RunEffects.ResetRun();
            Assert.Less(_attack.Damage, modified);
            Tick(.12f, true, Vector3.right);
            Tick(.12f, true, Vector3.left);
            foreach (var projectile in Projectiles())
                Assert.AreEqual(modified, Get<int>(projectile, "_damage"));
        }

        [Test]
        public void PrecisionDoublesDamageStopsAtOneTargetAndUsesItsOwnInterval()
        {
            _round.Shots.TryBuyAndEquip(ShotType.Precision, _ => true);
            _attack.ResetRun();
            var first = Enemy(2);
            var second = Enemy(3);
            Tick(0f, true, Vector3.forward);
            var projectile = Projectiles()[0];
            Assert.AreEqual(22f, Get<float>(projectile, "_speed"));
            Assert.AreEqual(26f, Get<float>(projectile, "_range"));
            projectile.transform.position = Vector3.forward * 4 + Vector3.up * .62f;
            Assert.IsTrue((bool)Call(projectile, "CheckHit"));
            Assert.AreEqual(10 - _attack.Damage * 2, first.Health);
            Assert.AreEqual(10, second.Health);
            Tick(1.39f, true, Vector3.forward);
            Assert.AreEqual(1, Projectiles().Length);
            Tick(.02f, true, Vector3.forward);
            Assert.AreEqual(2, Projectiles().Length);
        }

        [Test]
        public void BoomerangReturnsOnOriginalAxisAndHitsEachEnemyOncePerLeg()
        {
            var first = Enemy(2);
            var second = Enemy(5);
            var projectile = AttackProjectile.SpawnPattern(Vector3.zero, Vector3.forward, 15, 9, 2,
                Color.white, ShotType.Boomerang, PokemonElement.Normal);
            Call(projectile, "TickProjectile", .6f);
            Assert.AreEqual(8, first.Health);
            Assert.AreEqual(8, second.Health);
            Assert.IsTrue(projectile.gameObject.activeSelf);
            Assert.That(projectile.transform.position.z, Is.EqualTo(9f).Within(.001f));
            _player.transform.position = Vector3.right * 30;
            Call(projectile, "TickProjectile", .3f);
            Assert.AreEqual(6, second.Health);
            Assert.IsFalse((bool)Call(projectile, "CheckHit"));
            Assert.AreEqual(6, second.Health, "a repeated collision on the return leg cannot hit twice");
            Call(projectile, "TickProjectile", .3f);
            Assert.AreEqual(6, first.Health);
            Assert.That(projectile.transform.position.x, Is.EqualTo(0f).Within(.001f));
            Assert.That(projectile.transform.position.z, Is.EqualTo(0f).Within(.001f));
            Assert.IsFalse(projectile.gameObject.activeSelf);
        }

        [Test]
        public void BoomerangCapsEachLegAtTwoDistinctTargetsEvenAcrossALongFrame()
        {
            var first = Enemy(1);
            var middle = Enemy(2);
            var last = Enemy(3);
            var projectile = AttackProjectile.SpawnPattern(Vector3.zero, Vector3.forward, 15, 9, 2,
                Color.white, ShotType.Boomerang, PokemonElement.Normal);
            Call(projectile, "TickProjectile", 2f);
            Assert.AreEqual(8, first.Health);
            Assert.AreEqual(6, middle.Health);
            Assert.AreEqual(8, last.Health);
            Assert.IsFalse(projectile.gameObject.activeSelf);
        }

        [Test]
        public void ExplosionDamagesDirectTargetAndOnlyThreeDistinctNearbyTargets()
        {
            var direct = Enemy(2);
            var splash = new EnemyTriangle[4];
            for (int i = 0; i < splash.Length; i++)
            {
                splash[i] = Enemy(2);
                splash[i].transform.position += Vector3.right * (.8f + i * .3f);
                splash[i].gameObject.AddComponent<SphereCollider>().radius = .1f;
                splash[i].gameObject.AddComponent<BoxCollider>().size = Vector3.one * .1f;
            }
            var projectile = AttackProjectile.SpawnPattern(Vector3.zero, Vector3.forward, 15, 14, 4,
                Color.white, ShotType.Explosive, PokemonElement.Normal);
            Physics.SyncTransforms();
            projectile.transform.position = Vector3.forward * 3;
            Assert.IsTrue((bool)Call(projectile, "CheckHit"));
            Assert.AreEqual(6, direct.Health);
            for (int i = 0; i < 3; i++) Assert.AreEqual(7, splash[i].Health);
            Assert.AreEqual(10, splash[3].Health);
        }

        [Test]
        public void ExplosionRejectsHeightObstaclesClosedZonesAndTargetsBeyondItsRadius()
        {
            var direct = Enemy(2);
            var high = Enemy(2);
            high.transform.position += Vector3.up * 3;
            var walled = Enemy(2);
            walled.transform.position += Vector3.right * 1.5f;
            var locked = Enemy(2);
            locked.transform.position += Vector3.left;
            var outside = Enemy(2);
            outside.transform.position += Vector3.left * 2.1f;
            var wall = Create("BlastWall");
            wall.transform.position = new Vector3(.75f, 0, 2);
            wall.AddComponent<BoxCollider>().size = new Vector3(.2f, 3, 1);
            AttackProjectile.CanTravelSegment = (from, to) => to.x >= 0;
            Physics.SyncTransforms();
            var projectile = AttackProjectile.SpawnPattern(Vector3.zero, Vector3.forward, 15, 14, 4,
                Color.white, ShotType.Explosive, PokemonElement.Normal);
            projectile.transform.position = Vector3.forward * 3;
            Assert.IsTrue((bool)Call(projectile, "CheckHit"));
            Assert.AreEqual(6, direct.Health);
            foreach (var enemy in new[] { high, walled, locked, outside }) Assert.AreEqual(10, enemy.Health);
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
            Assert.IsFalse(_round.ShotStations.IsBuilt(0));
            Assert.IsFalse(_round.Shots.Has(ShotType.Burst));
            Assert.AreEqual(0, Get<int>(_attack, "_burstRemaining"));
            Assert.AreEqual(0, Object.FindObjectsByType<AttackProjectile>().Length);
            Tick(.5f, false, Vector3.zero);
            Assert.AreEqual(0, Object.FindObjectsByType<AttackProjectile>().Length);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.Rounds;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Real pickup/effect components with a manually advanced gameplay clock and injected
    /// loot rolls. No RoundManager/PlayerSphereController lifecycle, PlayerPrefs or saves.
    /// </summary>
    public sealed class DropRuntimeTests
    {
        readonly List<GameObject> _owned = new List<GameObject>();
        EnemyDropSystem _drops;
        PlayerRunEffects _effects;
        Transform _feet;
        bool _runActive, _reserve, _recovery, _acceptPoints;
        bool _blockedBefore, _overBefore;
        float _timeScaleBefore;
        float _healed;
        int _points, _collections;

        [SetUp]
        public void SetUp()
        {
            _blockedBefore = GameFlow.Blocked;
            _overBefore = GameFlow.Over;
            _timeScaleBefore = Time.timeScale;
            Assert.IsNull(RoundManager.Instance, "Use an empty test scene; these tests never instantiate a save-owning manager.");
            Assert.IsNull(PlayerSphereController.Instance);
            GameFlow.Blocked = false;
            GameFlow.Over = false;
            Time.timeScale = 1f;
            _runActive = true;
            _reserve = false;
            _recovery = false;
            _acceptPoints = true;
            _healed = 0f;
            _points = 0;
            _collections = 0;

            _feet = Create("DropRuntime_ContactBody").transform;
            _feet.position = Vector3.right * 20f;
            _effects = _feet.gameObject.AddComponent<PlayerRunEffects>();
            _effects.enabled = false;
            _effects.Configure(() => _runActive, amount => _healed += amount,
                () => _reserve, () => _recovery);
            _drops = Create("DropRuntime_Owner").AddComponent<EnemyDropSystem>();
            _drops.enabled = false;
            ConfigureDrops();
        }

        void ConfigureDrops() =>
            _drops.Configure(_feet, _effects, () => _runActive, CollectPoints, FlatGround);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameFlow.Blocked = true;
            foreach (var go in _owned)
                if (go != null) Object.Destroy(go);
            _owned.Clear();
            yield return null;
            yield return null; // Owner destruction also disposes its independent pool root/art.
            GameFlow.Blocked = _blockedBefore;
            GameFlow.Over = _overBefore;
            Time.timeScale = _timeScaleBefore;
        }

        GameObject Create(string name)
        {
            var go = new GameObject(name);
            _owned.Add(go);
            return go;
        }

        bool CollectPoints(int amount)
        {
            if (!_acceptPoints) return false;
            _points += amount;
            _collections++;
            return true;
        }

        [UnityTest]
        public IEnumerator CollectedEffectStartsItsClockAfterTheCollectionFrame()
        {
            yield return null;
            Assert.IsTrue(_effects.Apply(TemporaryEffect.Regeneration));
            var update = typeof(PlayerRunEffects).GetMethod("Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            update.Invoke(_effects, null);
            Assert.AreEqual(6f, _effects.Remaining(TemporaryEffect.Regeneration));
            Assert.AreEqual(0f, _healed, "The current frame's delta elapsed before collection.");
            yield return null;
            update.Invoke(_effects, null);
            Assert.Less(_effects.Remaining(TemporaryEffect.Regeneration), 6f);
            Assert.Greater(_healed, 0f);
        }

        static bool FlatGround(Vector3 death, out Vector3 ground)
        {
            ground = new Vector3(death.x, 0f, death.z);
            return true;
        }

        static Func<float> Rolls(params float[] values)
        {
            var sequence = new Queue<float>(values);
            return () => sequence.Dequeue();
        }

        void SpawnPoints(Vector3 position, int value = 10) =>
            _drops.NotifyDeath(position, false, value, Rolls(.99f, .1f));

        void SpawnEffect(TemporaryEffect type, Vector3 position) =>
            _drops.NotifyDeath(position, false, 10, Rolls(.1f, .99f, ((int)type + .5f) / 4f));

        GroundPickup Available(GroundPickupKind kind)
        {
            foreach (var pickup in _drops.Pickups)
                if (pickup != null && pickup.IsAvailable && pickup.Kind == kind) return pickup;
            Assert.Fail("Missing live pickup of kind " + kind);
            return null;
        }

        [TestCase(.4999f, .2999f, 2)]
        [TestCase(.5f, .2999f, 1)]
        [TestCase(.4999f, .3f, 1)]
        [TestCase(.5f, .3f, 0)]
        public void NormalRollsAreIndependentAndThresholdsAreExclusive(float temporary, float points, int expected)
        {
            _drops.NotifyDeath(Vector3.zero, false, 10, Rolls(temporary, points, .1f));
            Assert.AreEqual(expected, _drops.ActiveCount);
            Assert.AreEqual(0, _points, "A death must not automatically collect its bonus.");
            Assert.AreEqual(0f, _effects.Remaining(TemporaryEffect.Overcharge));
        }

        [TestCase(0f, TemporaryEffect.Overcharge)]
        [TestCase(.24999f, TemporaryEffect.Overcharge)]
        [TestCase(.25f, TemporaryEffect.Haste)]
        [TestCase(.49999f, TemporaryEffect.Haste)]
        [TestCase(.5f, TemporaryEffect.Barrier)]
        [TestCase(.74999f, TemporaryEffect.Barrier)]
        [TestCase(.75f, TemporaryEffect.Regeneration)]
        [TestCase(1f, TemporaryEffect.Regeneration)]
        public void BossAlwaysDropsOneTemporaryAndEachEffectOwnsOneQuarter(float selection, TemporaryEffect expected)
        {
            _drops.NotifyDeath(Vector3.zero, true, 470, Rolls(.99f, .99f, selection));
            Assert.AreEqual(1, _drops.ActiveCount);
            Assert.AreEqual(expected, Available(GroundPickupKind.TemporaryEffect).Effect);
            Assert.AreEqual(0, _points);
        }

        [Test]
        public void LifetimeIsStillAliveAt499AndExpiresAtExactlyFiveActiveSeconds()
        {
            SpawnPoints(Vector3.zero);
            var pickup = Available(GroundPickupKind.BonusPoints);
            _drops.Tick(4.99d);
            Assert.IsTrue(pickup.IsAvailable);
            Assert.That(pickup.RemainingLifetime, Is.EqualTo(.01f).Within(.00001f));
            _feet.position = Vector3.zero;
            _drops.Tick(.01d);
            Assert.IsFalse(pickup.IsAvailable);
            Assert.AreEqual(0, _points, "Expiration at 5.00 wins over contact in that tick.");
        }

        [Test]
        public void OnlyCurrentFeetContactCollectsAndThereIsNoTeleportSweep()
        {
            _feet.position = Vector3.left * 4f;
            SpawnPoints(Vector3.zero);
            _feet.position = Vector3.right * 4f; // R-style discontinuous relocation over the pickup.
            _drops.Tick(.1d);
            Assert.AreEqual(0, _points);
            _feet.position = Vector3.right * .71f;
            _drops.Tick(.1d);
            Assert.AreEqual(0, _points);
            _feet.position = Vector3.up * .46f;
            _drops.Tick(.1d);
            Assert.AreEqual(0, _points, "A body on the next ledge must not collect below its feet.");
            _feet.position = Vector3.down * .16f;
            _drops.Tick(.1d);
            Assert.AreEqual(0, _points);
            _feet.position = new Vector3(.69f, .08f, 0f);
            _drops.Tick(.1d);
            Assert.AreEqual(10, _points);
            Assert.AreEqual(1, _collections);
            _drops.Tick(.1d);
            Assert.AreEqual(1, _collections);
        }

        [Test]
        public void CharacterControllerFootIsUsedInsteadOfItsTransformCentre()
        {
            var controller = _feet.gameObject.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.center = Vector3.zero;
            controller.enabled = false;
            ConfigureDrops(); // Cache the new controller once.
            _feet.position = Vector3.up;
            SpawnPoints(Vector3.zero);
            Assert.AreEqual(10, _points, "The body centre is 1m high, but its feet already touch the pickup.");
            Assert.AreEqual(0, _drops.ActiveCount);
        }

        [Test]
        public void BirthUnderPlayerCollectsBothDropsOnceAndPreservesCapturedBossPointValue()
        {
            _feet.position = Vector3.zero;
            int capturedAtSpawn = 470;
            _drops.NotifyDeath(Vector3.zero, true, capturedAtSpawn, Rolls(.99f, .1f, .6f));
            capturedAtSpawn = 999;
            Assert.AreEqual(470, _points);
            Assert.AreEqual(1, _collections);
            Assert.AreEqual(8f, _effects.Remaining(TemporaryEffect.Barrier));
            Assert.AreEqual(0, _drops.ActiveCount);
            _drops.Tick(1d);
            Assert.AreEqual(470, _points);
        }

        [Test]
        public void DelayedPointCollectionUsesStoredValueAndRejectedCollectionCanRetry()
        {
            int reward = 350;
            SpawnPoints(Vector3.zero, reward);
            reward = 10; // Current round values are deliberately unrelated.
            _feet.position = Vector3.zero;
            _acceptPoints = false;
            _drops.Tick(.1d);
            Assert.AreEqual(1, _drops.ActiveCount);
            Assert.AreEqual(0, _collections);
            _acceptPoints = true;
            _drops.Tick(.1d);
            Assert.AreEqual(350, _points);
            Assert.AreEqual(0, _drops.ActiveCount);
        }

        [Test]
        public void BlockedAndInactiveRunFreezeLifetimeEffectsHealingAndCollection()
        {
            SpawnPoints(Vector3.zero);
            _effects.Apply(TemporaryEffect.Regeneration);
            GameFlow.Blocked = true;
            _feet.position = Vector3.zero;
            _drops.Tick(100d);
            _effects.Tick(100d);
            Assert.AreEqual(5f, Available(GroundPickupKind.BonusPoints).RemainingLifetime);
            Assert.AreEqual(6f, _effects.Remaining(TemporaryEffect.Regeneration));
            Assert.AreEqual(0, _points);
            Assert.AreEqual(0f, _healed);
            Assert.IsFalse(_effects.Apply(TemporaryEffect.Haste));

            GameFlow.Blocked = false;
            _runActive = false;
            _drops.Tick(100d);
            _effects.Tick(100d);
            Assert.AreEqual(5f, Available(GroundPickupKind.BonusPoints).RemainingLifetime);
            Assert.AreEqual(6f, _effects.Remaining(TemporaryEffect.Regeneration));
            Assert.AreEqual(0f, _healed);

            _runActive = true;
            _drops.Tick(.25d);
            _effects.Tick(.25d);
            Assert.AreEqual(10, _points);
            Assert.AreEqual(.5f, _healed);
        }

        [UnityTest]
        public IEnumerator ActualUpdateAlsoFreezesWhenBlockedEvenAtTimeScaleOne()
        {
            SpawnPoints(Vector3.zero);
            _effects.Apply(TemporaryEffect.Haste);
            GameFlow.Blocked = true;
            _drops.enabled = true;
            _effects.enabled = true;
            yield return null;
            yield return null;
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(5f, Available(GroundPickupKind.BonusPoints).RemainingLifetime);
            Assert.AreEqual(6f, _effects.Remaining(TemporaryEffect.Haste));
        }

        [Test]
        public void ClearingAndPreparingKeepTheLastEnemyDropUntilItsOwnDeadline()
        {
            var phase = RoundManager.RoundPhase.Fighting;
            _drops.Configure(_feet, _effects, () => _runActive &&
                phase != RoundManager.RoundPhase.Idle && phase != RoundManager.RoundPhase.GameOver,
                CollectPoints, FlatGround);
            SpawnPoints(Vector3.zero);
            phase = RoundManager.RoundPhase.Clearing;
            _drops.Tick(2.5d);
            phase = RoundManager.RoundPhase.Preparing;
            _drops.Tick(2.49d);
            Assert.AreEqual(1, _drops.ActiveCount);
            _feet.position = Vector3.zero;
            _drops.Tick(.005d);
            Assert.AreEqual(10, _points);
        }

        [Test]
        public void OffscreenAndDisabledRenderersDoNotPreventExpiration()
        {
            var chunk = Create("DropRuntime_UnloadedChunk");
            SpawnPoints(Vector3.zero);
            var pickup = Available(GroundPickupKind.BonusPoints);
            Assert.AreNotEqual(chunk.transform, pickup.transform.parent);
            foreach (var renderer in pickup.GetComponentsInChildren<SpriteRenderer>())
                renderer.enabled = false;
            chunk.SetActive(false);
            _feet.position = Vector3.one * 10000f;
            _drops.Tick(5d); // No camera exists in this fixture.
            Assert.IsFalse(pickup.IsAvailable);
            Assert.AreEqual(0, _points);
        }

        [Test]
        public void LastSecondBlinkUsesTheSamePausableGameplayClock()
        {
            SpawnPoints(Vector3.zero);
            var pickup = Available(GroundPickupKind.BonusPoints);
            var icon = pickup.transform.Find("DropSymbol").GetComponent<SpriteRenderer>();
            _drops.Tick(3.99d);
            Assert.IsTrue(icon.enabled);
            _drops.Tick(.16d);
            Assert.IsFalse(icon.enabled);
            GameFlow.Blocked = true;
            _drops.Tick(10d);
            Assert.IsFalse(icon.enabled);
            Assert.IsTrue(pickup.IsAvailable);
            GameFlow.Blocked = false;
            _drops.Tick(.1d);
            Assert.IsTrue(icon.enabled);
        }

        [Test]
        public void PlacementIsAuthoritativeAndReceivesTheOriginalDeath()
        {
            Vector3 death = new Vector3(7f, 1f, 4f);
            Vector3 observed = default;
            _drops.TryPlacement = (Vector3 source, out Vector3 ground) =>
            {
                observed = source;
                ground = new Vector3(7.2f, .5f, 4f);
                return true;
            };
            SpawnPoints(death);
            Assert.AreEqual(death, observed);
            Assert.AreEqual(new Vector3(7.2f, .5f, 4f), Available(GroundPickupKind.BonusPoints).GroundPosition);
            _drops.ResetRun();
            _drops.TryPlacement = (Vector3 source, out Vector3 ground) =>
            {
                ground = default;
                return false;
            };
            SpawnPoints(death);
            Assert.AreEqual(0, _drops.ActiveCount);
            Assert.AreEqual(0, _points);
        }

        [Test]
        public void RefreshDoesNotStackIntensityWhileDifferentEffectsCoexist()
        {
            int changes = 0;
            _effects.Changed += () => changes++;
            foreach (TemporaryEffect effect in Enum.GetValues(typeof(TemporaryEffect)))
                Assert.IsTrue(_effects.Apply(effect));
            Assert.AreEqual(4, changes);
            Assert.AreEqual(5, _effects.ModifyDamage(4));
            Assert.AreEqual(2, _effects.ModifyDamage(1), "Low base damage still gains at least one.");
            Assert.AreEqual(0, _effects.ModifyDamage(0));
            Assert.AreEqual(int.MaxValue, _effects.ModifyDamage(int.MaxValue));
            Assert.AreEqual(1.2f, _effects.MovementMultiplier);
            Assert.AreEqual(.1f, _effects.DamageReductionBonus);
            _effects.Tick(2d);
            _effects.Apply(TemporaryEffect.Overcharge);
            Assert.AreEqual(8f, _effects.Remaining(TemporaryEffect.Overcharge));
            Assert.AreEqual(4f, _effects.Remaining(TemporaryEffect.Haste));
            Assert.AreEqual(5, _effects.ModifyDamage(4));
            Assert.AreEqual(5, changes);
            _effects.Tick(4d);
            Assert.AreEqual(1f, _effects.MovementMultiplier);
            Assert.AreEqual(.1f, _effects.DamageReductionBonus);
            Assert.AreEqual(6, changes, "One state event covers simultaneous expirations.");
            _effects.Tick(4d);
            Assert.AreEqual(4, _effects.ModifyDamage(4));
            Assert.AreEqual(0f, _effects.DamageReductionBonus);
        }

        [Test]
        public void ReserveIsSampledOnPickupAndNeverExtendsExistingTimersRetroactively()
        {
            _feet.position = Vector3.zero;
            SpawnEffect(TemporaryEffect.Overcharge, Vector3.zero);
            _effects.Tick(2d);
            Assert.AreEqual(6f, _effects.Remaining(TemporaryEffect.Overcharge));
            _reserve = true;
            Assert.AreEqual(6f, _effects.Remaining(TemporaryEffect.Overcharge));
            SpawnEffect(TemporaryEffect.Haste, Vector3.zero);
            Assert.AreEqual(7.5f, _effects.Remaining(TemporaryEffect.Haste));
            SpawnEffect(TemporaryEffect.Overcharge, Vector3.zero);
            Assert.AreEqual(10f, _effects.Remaining(TemporaryEffect.Overcharge));
            Assert.AreEqual(5, _effects.ModifyDamage(4));
            _reserve = false;
            Assert.AreEqual(10f, _effects.Remaining(TemporaryEffect.Overcharge));
        }

        [Test]
        public void RegenerationIntegratesOnlyItsLiveIntervalEvenOnAnOvershootingFrame()
        {
            _effects.Apply(TemporaryEffect.Regeneration);
            _effects.Tick(.25d);
            Assert.AreEqual(.5f, _healed);
            _effects.Tick(20d);
            Assert.AreEqual(12f, _healed);
            Assert.AreEqual(0f, _effects.Remaining(TemporaryEffect.Regeneration));
            _reserve = true;
            _effects.Apply(TemporaryEffect.Regeneration);
            _effects.Tick(20d);
            Assert.AreEqual(27f, _healed, "Reserve grants 7.5s * 2 HP/s, not extra intensity.");
        }

        [Test]
        public void RecoveryWaitsFiveUndamagedActiveSecondsAndStacksWithRegeneration()
        {
            _recovery = true;
            _effects.Tick(4.99d);
            Assert.AreEqual(0f, _healed);
            _effects.Tick(.01d);
            Assert.AreEqual(0f, _healed);
            _effects.Tick(.25d);
            Assert.AreEqual(.25f, _healed);
            _effects.Apply(TemporaryEffect.Regeneration);
            _effects.Tick(1d);
            Assert.AreEqual(3.25f, _healed);
            _effects.NotifyDamaged();
            Assert.AreEqual(5f, _effects.RecoveryDelayRemaining);
            _effects.Tick(5d);
            Assert.AreEqual(13.25f, _healed, "Only the remaining 5s of regeneration may heal during Recovery's delay.");
            _effects.Tick(.5d);
            Assert.AreEqual(13.75f, _healed);
        }

        [Test]
        public void ResetClearsGroundRewardsEffectsAndRecoveryDelayAndReusesThePool()
        {
            SpawnPoints(Vector3.zero);
            var first = Available(GroundPickupKind.BonusPoints);
            _effects.Apply(TemporaryEffect.Barrier);
            _effects.Apply(TemporaryEffect.Haste);
            _effects.Tick(2d);
            _drops.ResetRun();
            _effects.ResetRun();
            Assert.AreEqual(0, _drops.ActiveCount);
            Assert.AreEqual(1f, _effects.MovementMultiplier);
            Assert.AreEqual(0f, _effects.DamageReductionBonus);
            Assert.AreEqual(5f, _effects.RecoveryDelayRemaining);
            _feet.position = Vector3.zero;
            _drops.Tick(1d);
            Assert.AreEqual(0, _points);
            _feet.position = Vector3.right * 20f;
            SpawnPoints(Vector3.zero);
            Assert.AreSame(first, Available(GroundPickupKind.BonusPoints));
            Assert.AreEqual(5f, first.RemainingLifetime);
        }

        [Test]
        public void BurstAllocationIsBoundedAndSharedPixelArtIsReused()
        {
            for (int i = 0; i < DropDefinitions.PoolCapacity + 10; i++) SpawnPoints(Vector3.zero);
            Assert.AreEqual(DropDefinitions.PoolCapacity, _drops.Pickups.Count);
            Assert.AreEqual(DropDefinitions.PoolCapacity, _drops.ActiveCount);
            var a = _drops.Pickups[0].transform.Find("DropSymbol").GetComponent<SpriteRenderer>();
            var b = _drops.Pickups[1].transform.Find("DropSymbol").GetComponent<SpriteRenderer>();
            Assert.AreSame(a.sprite, b.sprite);
            Assert.AreSame(a.sharedMaterial, b.sharedMaterial);
            Assert.AreEqual(FilterMode.Point, a.sprite.texture.filterMode);
            Assert.AreEqual(1, a.sprite.texture.mipmapCount);
            Assert.AreEqual(16, a.sprite.texture.width);
            _drops.Tick(5d);
            Assert.AreEqual(0, _drops.ActiveCount);
        }

        [UnityTest]
        public IEnumerator DestroyingOwnerDisposesIndependentPoolAndOwnedArt()
        {
            SpawnPoints(Vector3.zero);
            var pickup = Available(GroundPickupKind.BonusPoints);
            var sprite = pickup.transform.Find("DropSymbol").GetComponent<SpriteRenderer>().sprite;
            var texture = sprite.texture;
            var root = pickup.transform.parent.gameObject;
            Object.Destroy(_drops.gameObject);
            yield return null;
            yield return null;
            Assert.IsTrue(root == null);
            Assert.IsTrue(pickup == null);
            Assert.IsTrue(sprite == null);
            Assert.IsTrue(texture == null);
        }
    }
}

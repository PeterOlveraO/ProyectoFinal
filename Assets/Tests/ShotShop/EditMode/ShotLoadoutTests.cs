using Game.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Game.ShotShop.Tests
{
    public sealed class ShotLoadoutTests
    {
        [Test]
        public void BasicIsFreeAndTheOnlyInitialOwnedShot()
        {
            var shots = new ShotLoadout();
            Assert.AreEqual(ShotType.Basic, shots.Equipped);
            Assert.IsTrue(shots.Has(ShotType.Basic));
            Assert.IsFalse(shots.Has(ShotType.Burst));
            Assert.IsFalse(shots.Has(ShotType.Fan));
            Assert.IsFalse(shots.Has(ShotType.Piercing));
            Assert.IsFalse(shots.TryBuyAndEquip(ShotType.Basic, _ => false));
        }

        [TestCase(ShotType.Burst, 60)]
        [TestCase(ShotType.Fan, 90)]
        [TestCase(ShotType.Piercing, 120)]
        public void PurchaseSpendsExactRunPointsAndEquips(ShotType type, int expectedPrice)
        {
            var shots = new ShotLoadout();
            int points = expectedPrice;
            Assert.IsTrue(shots.TryBuyAndEquip(type, price => { points -= price; return true; }));
            Assert.AreEqual(0, points);
            Assert.IsTrue(shots.Has(type));
            Assert.AreEqual(type, shots.Equipped);
        }

        [Test]
        public void FailedPurchaseAndLockedEquipDoNotMutateLoadout()
        {
            var shots = new ShotLoadout();
            Assert.IsFalse(shots.TryEquip(ShotType.Fan));
            Assert.IsFalse(shots.TryBuyAndEquip(ShotType.Fan, _ => false));
            Assert.IsFalse(shots.Has(ShotType.Fan));
            Assert.AreEqual(ShotType.Basic, shots.Equipped);
            Assert.IsFalse(shots.TryBuyAndEquip((ShotType)(-1), _ => true));
            Assert.IsFalse(shots.TryBuyAndEquip((ShotType)99, _ => true));
        }

        [Test]
        public void EveryReplacementChargesAndNoInventorySurvives()
        {
            var shots = new ShotLoadout();
            int charges = 0;
            Assert.IsTrue(shots.TryBuyAndEquip(ShotType.Burst, _ => { charges++; return true; }));
            Assert.IsFalse(shots.TryEquip(ShotType.Basic));
            Assert.IsTrue(shots.TryBuyAndEquip(ShotType.Fan, _ => { charges++; return true; }));
            Assert.IsFalse(shots.Has(ShotType.Burst));
            Assert.IsTrue(shots.TryBuyAndEquip(ShotType.Burst, _ => { charges++; return true; }));
            Assert.AreEqual(3, charges);
            shots.ResetRun();
            Assert.AreEqual(ShotType.Basic, shots.Equipped);
            Assert.IsFalse(shots.TryEquip(ShotType.Burst));
            Assert.IsTrue(shots.TryBuyAndEquip(ShotType.Burst, _ => { charges++; return true; }));
            Assert.AreEqual(4, charges);
        }

        [TestCase(0f)]
        [TestCase(37f)]
        [TestCase(90f)]
        [TestCase(180f)]
        public void FanHasFixedSignedAnglesRelativeToManualAxis(float yaw)
        {
            var axis = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            for (int i = 0; i < 3; i++)
            {
                var direction = ShotDefinitions.FanDirection(axis * 3f, i);
                Assert.That(direction.magnitude, Is.EqualTo(1f).Within(.0001f));
                Assert.That(direction.y, Is.EqualTo(0f).Within(.0001f));
                Assert.That(Vector3.SignedAngle(axis, direction, Vector3.up),
                    Is.EqualTo((i - 1) * 16f).Within(.0001f));
            }
            Assert.AreEqual(Vector3.zero, ShotDefinitions.FanDirection(Vector3.zero, 0));
        }
    }
}

using Game.Rounds;
using NUnit.Framework;

namespace Game.Rounds.Shop.Tests
{
    public sealed class RunZoneUnlocksTests
    {
        [Test]
        public void StartsWithFreeCentreAndRequiresAnOpenCardinalNeighbour()
        {
            var zones = new RunZoneUnlocks();
            Assert.That(zones.UnlockedCount, Is.EqualTo(1));
            Assert.That(zones.IsUnlocked(1), Is.True);
            Assert.That(zones.Cost(1), Is.Zero);
            Assert.That(zones.TryUnlock(6, _ => true), Is.False);
            Assert.That(zones.TryUnlock(2, cost => cost == 250), Is.True);
            Assert.That(zones.TryUnlock(6, cost => cost == 600), Is.True);
            Assert.That(zones.IsUnlocked(6), Is.True);
            Assert.That(zones.CanUnlock(7), Is.False);
        }

        [Test]
        public void FailedAndRepeatedPurchasesNeverChargeTwice()
        {
            var zones = new RunZoneUnlocks();
            int attempts = 0, changed = 0;
            zones.Changed += () => changed++;
            Assert.That(zones.TryUnlock(2, cost => { attempts++; return false; }), Is.False);
            Assert.That(zones.IsUnlocked(2), Is.False);
            Assert.That(changed, Is.Zero);
            Assert.That(zones.TryUnlock(2, cost => { attempts++; return true; }), Is.True);
            Assert.That(zones.TryUnlock(2, cost => { attempts++; return true; }), Is.False);
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void SpendingCallbacksCannotReenterPurchaseOrReset()
        {
            var zones = new RunZoneUnlocks();
            int charges = 0;
            Assert.That(zones.TryUnlock(2, cost =>
            {
                charges++;
                Assert.That(zones.TryUnlock(3, _ => { charges++; return true; }), Is.False);
                zones.ResetRun();
                return true;
            }), Is.True);
            Assert.That(charges, Is.EqualTo(1));
            Assert.That(zones.UnlockedCount, Is.EqualTo(2));
        }

        [Test]
        public void AllRegionsCost3400AndOnlyExplicitRunResetClosesThem()
        {
            var zones = new RunZoneUnlocks();
            int cost = 0;
            for (int id = 2; id <= 9; id++)
                Assert.That(zones.TryUnlock(id, price => { cost += price; return true; }), Is.True);
            Assert.That(cost, Is.EqualTo(3400));
            Assert.That(zones.UnlockedCount, Is.EqualTo(9));
            int revision = zones.Revision;
            zones.ResetRun();
            Assert.That(zones.UnlockedCount, Is.EqualTo(1));
            Assert.That(zones.Revision, Is.GreaterThan(revision));
            Assert.That(zones.TryUnlock(6, _ => true), Is.False);
        }
    }
}

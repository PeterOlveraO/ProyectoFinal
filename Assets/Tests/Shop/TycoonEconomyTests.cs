using Game.Combat;
using Game.Economy;
using Game.Rounds;
using NUnit.Framework;

namespace Game.Rounds.Shop.Tests
{
    public sealed class TycoonEconomyTests
    {
        [TestCase(1, 170, 170)]
        [TestCase(2, 220, 390)]
        [TestCase(3, 270, 660)]
        [TestCase(4, 320, 980)]
        [TestCase(5, 370, 1350)]
        [TestCase(10, 620, 3950)]
        [TestCase(15, 870, 7800)]
        [TestCase(20, 1120, 12900)]
        [TestCase(25, 1250, 19010)]
        public void IncomeIncludesRoundClearAndBossEquivalentKills(int round, int income, int cumulative)
        {
            Assert.AreEqual(income, RunEconomy.DefaultRoundIncome(round));
            Assert.AreEqual(cumulative, RunEconomy.DefaultCumulativeIncome(round));
        }

        [TestCase(0, 200, 2)]
        [TestCase(1, 260, 2)]
        [TestCase(2, 190, 2)]
        [TestCase(3, 300, 2)]
        [TestCase(4, 340, 2)]
        public void PerksBudgetBothStructureAndProduct(int index, int combined, int round)
        {
            Assert.AreEqual(combined, DrinkStations.BuildPrices[index] + DrinkStations.Prices[index]);
            Assert.AreEqual(round, RunEconomy.RoundsToAfford(combined));
            int[] previous = { 30, 45, 35, 45, 60 };
            Assert.Greater(DrinkStations.Prices[index], previous[index]);
        }

        [TestCase(0, 250, 2)]
        [TestCase(1, 350, 2)]
        [TestCase(2, 480, 3)]
        [TestCase(3, 160, 1)]
        public void ShotsBudgetBothStructureAndProduct(int offer, int combined, int round)
        {
            Assert.AreEqual(combined, ShotStations.BuildPrice(offer) + ShotDefinitions.Price(ShotStations.OfferType(offer)));
            Assert.AreEqual(round, RunEconomy.RoundsToAfford(combined));
        }

        [Test]
        public void EarlyProgressIsReachableWithoutDropsAndCannotBuyEverythingImmediately()
        {
            int points = RunEconomy.DefaultRoundIncome(1);
            points -= ShotStations.BuildPrice(0);
            Assert.AreEqual(60, points, "First round builds Burst but does not silently grant it");
            Assert.Less(points, ShotDefinitions.Price(ShotType.Burst));
            points += RunEconomy.DefaultRoundIncome(2);
            points -= ShotDefinitions.Price(ShotType.Burst);
            Assert.AreEqual(140, points, "Burst available after two normal-equivalent rounds");
        }

        [Test]
        public void AdvancedBudgetStillRequiresChoicesAndRepeatedShotsStillCost()
        {
            int total = 0;
            for (int i = 0; i < 5; i++) total += DrinkStations.BuildPrices[i] + DrinkStations.Prices[i];
            for (int i = 0; i < 4; i++) total += ShotStations.BuildPrice(i) + ShotDefinitions.Price(ShotStations.OfferType(i));
            Assert.AreEqual(2530, total);
            Assert.AreEqual(8, RunEconomy.RoundsToAfford(total));
            int evTotal = 0;
            for (int level = 0; level < 10; level++)
            {
                Assert.Greater(RunEconomy.EVPrice(level), 20 + level * 10);
                evTotal += RunEconomy.EVPrice(level);
            }
            Assert.AreEqual(2600, evTotal);
            Assert.AreEqual(12, RunEconomy.RoundsToAfford(total + evTotal));
            int points = RunEconomy.DefaultCumulativeIncome(10);
            var shots = new ShotLoadout();
            System.Func<int, bool> spend = price =>
            {
                if (points < price) return false;
                points -= price;
                return true;
            };
            Assert.IsTrue(shots.TryBuyAndEquip(ShotType.Piercing, spend));
            Assert.IsTrue(shots.TryBuyAndEquip(ShotType.Burst, spend));
            Assert.IsTrue(shots.TryBuyAndEquip(ShotType.Piercing, spend));
            Assert.AreEqual(3950 - 280 - 140 - 280, points);
            int before = points;
            Assert.IsFalse(shots.TryBuyAndEquip(ShotType.Fan, _ => false));
            Assert.AreEqual(before, points);
            Assert.AreEqual(ShotType.Piercing, shots.Equipped);
        }
    }
}

using Game.Rounds;
using NUnit.Framework;

namespace Game.Rounds.Shop.Tests
{
    public sealed class Issue9ShopPlacementTests
    {
        [Test]
        public void DrinkPrices_MatchThePointsShownAndCharged()
        {
            CollectionAssert.AreEqual(new[] { 30, 45, 35, 45, 60 }, DrinkStations.Prices);
            Assert.AreEqual(DrinkStations.Names.Length, DrinkStations.Prices.Length);
        }

        [Test]
        public void Placement_RejectsMissingTerrainWithVisibleFailure()
        {
            Assert.Throws<System.ArgumentException>(() =>
                WorldShopPlacement.FindPositions(null, 5, 123));
        }

        [Test]
        public void Placement_RejectsNonPositiveCountWithVisibleFailure()
        {
            Assert.Throws<System.ArgumentException>(() =>
                WorldShopPlacement.FindPositions(null, 0, 123));
        }
    }
}

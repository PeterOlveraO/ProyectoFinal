using System;

namespace Game.Economy
{
    /// <summary>
    /// Prices and conservative balance model. Income is an analysis of default round settings,
    /// not a second reward source: RoundManager alone credits actual kills/rounds.
    /// Bosses substitute normal enemies and must preserve their combined kill-point reward.
    /// </summary>
    public static class RunEconomy
    {
        public static int EVPrice(int level) => 80 + Math.Max(0, level) * 40;

        public static int DefaultRoundIncome(int round)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            int normalEquivalent = Math.Min(48, 6 + 2 * (round - 1));
            return normalEquivalent * 10 + 50 + 10 * (round - 1);
        }

        public static int DefaultCumulativeIncome(int rounds)
        {
            if (rounds < 0) throw new ArgumentOutOfRangeException(nameof(rounds));
            int total = 0;
            for (int round = 1; round <= rounds; round++) total += DefaultRoundIncome(round);
            return total;
        }

        /// <summary>Earliest completed round, no other spending and no random bonus drops.</summary>
        public static int RoundsToAfford(int cost)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            int round = 0, earned = 0;
            while (earned < cost) earned += DefaultRoundIncome(++round);
            return round;
        }
    }
}

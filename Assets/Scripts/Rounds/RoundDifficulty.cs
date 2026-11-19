using System;
using System.Collections.Generic;
using Game.Pokemon.Progression;

namespace Game.Rounds
{
    /// <summary>Single source of truth for bounded round scaling, independent of scene overrides.</summary>
    public static class RoundDifficulty
    {
        public const int BossEvery = 5;
        public const int BossHealthMultiplier = 12;
        public const float BossDamageMultiplier = 3f;

        public static bool IsBoss(int round) => round > 0 && round % BossEvery == 0;

        public static EnemyRoundStats Stats(int round, bool boss = false)
        {
            double step = Math.Max(0d, (double)round - 1d);
            // +40% to normal starting stats only; bosses retain their original curve.
            return new EnemyRoundStats
            {
                health = (int)Math.Min(200d, (boss ? 10d : 14d) + 2d * step) * (boss ? BossHealthMultiplier : 1),
                speed = (float)Math.Min(4.6d, (boss ? 2.8d : 3.92d) + .08d * step),
                contactDamage = (float)Math.Min(35d, (boss ? 10d : 14d) + .75d * step) * (boss ? BossDamageMultiplier : 1f)
            };
        }

        // Stable rotation, not a new species list. PseudoLegendary is deliberately excluded.
        public static SpeciesDefinition BossSpecies(int round, IReadOnlyList<SpeciesDefinition> catalog)
        {
            var eligible = new List<SpeciesDefinition>();
            foreach (var species in catalog)
                if (species.category == "Legendary" || species.category == "Mythical")
                    eligible.Add(species);
            eligible.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            if (eligible.Count == 0) throw new InvalidOperationException("No legendary/mythical boss in catalog.");
            return eligible[Math.Max(0, round / BossEvery - 1) % eligible.Count];
        }
    }

    public struct EnemyRoundStats
    {
        public int health;
        public float speed, contactDamage;
    }
}

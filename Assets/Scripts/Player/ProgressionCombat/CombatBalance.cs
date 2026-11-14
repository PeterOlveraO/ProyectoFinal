using System;
using Game.Pokemon.Progression;

namespace Game.Player.ProgressionCombat
{
    /// <summary>Raw Pokémon stats are calculated first; this configuration converts them to game units.</summary>
    [Serializable]
    public sealed class CombatBalance
    {
        public StatBlock evPerUpgrade = new StatBlock(15, 40, 20, 10);
        public float healthOffset = 60f, healthPerPoint = .6f;
        public int minHealth = 50, maxHealth = 400;
        public float damageOffset = .5f, damagePerPoint = .025f;
        public int minDamage = 1, maxDamage = 25;
        public float defenseHalfReductionStat = 400f;
        public float maxBaseDamageReduction = .4f, maxTotalDamageReduction = .65f;
        public float movementOffset = 3f, movementPerPoint = .02f;
        public float minMovementSpeed = 3.5f, maxMovementSpeed = 8f;

        public CombatBalance Copy() => (CombatBalance)MemberwiseClone();
        public void Validate()
        {
            float[] finite = { healthOffset, healthPerPoint, damageOffset, damagePerPoint,
                defenseHalfReductionStat, maxBaseDamageReduction, maxTotalDamageReduction,
                movementOffset, movementPerPoint, minMovementSpeed, maxMovementSpeed };
            foreach (float value in finite)
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Non-finite combat balance.");
            if (evPerUpgrade.health <= 0 || evPerUpgrade.attack <= 0 || evPerUpgrade.defense <= 0 || evPerUpgrade.speed <= 0 ||
                evPerUpgrade.health > 1000 || evPerUpgrade.attack > 1000 || evPerUpgrade.defense > 1000 || evPerUpgrade.speed > 1000 ||
                healthOffset < 0 || damageOffset < 0 || movementOffset < 0 ||
                healthPerPoint <= 0 || damagePerPoint <= 0 || movementPerPoint <= 0 ||
                minHealth < 1 || maxHealth < minHealth || maxHealth > 10000 ||
                minDamage < 1 || maxDamage < minDamage || maxDamage > 1000 ||
                defenseHalfReductionStat <= 0 || maxBaseDamageReduction < 0 ||
                maxTotalDamageReduction < maxBaseDamageReduction || maxTotalDamageReduction > .85f ||
                minMovementSpeed <= 0 || maxMovementSpeed < minMovementSpeed || maxMovementSpeed > 20)
                throw new ArgumentException("Invalid combat balance.");
        }
    }

    public struct CombatStatBlock
    {
        public int maxHealth, damage;
        public float damageReduction, movementSpeed;
    }

    public static class PokemonCombatStats
    {
        public static StatBlock RunEV(CombatBalance balance, int healthLevel, int attackLevel, int defenseLevel, int speedLevel)
        {
            return new StatBlock(checked(balance.evPerUpgrade.health * Level(healthLevel)),
                checked(balance.evPerUpgrade.attack * Level(attackLevel)),
                checked(balance.evPerUpgrade.defense * Level(defenseLevel)),
                checked(balance.evPerUpgrade.speed * Level(speedLevel)));
        }
        static int Level(int value) => Math.Max(0, Math.Min(10, value));
        public static CombatStatBlock Convert(StatBlock stats, CombatBalance balance)
        {
            return new CombatStatBlock
            {
                maxHealth = RoundClamp(balance.healthOffset + stats.health * balance.healthPerPoint, balance.minHealth, balance.maxHealth),
                damage = RoundClamp(balance.damageOffset + stats.attack * balance.damagePerPoint, balance.minDamage, balance.maxDamage),
                damageReduction = (float)Math.Max(0, Math.Min(balance.maxBaseDamageReduction,
                    Math.Max(0, stats.defense) / ((double)Math.Max(0, stats.defense) + balance.defenseHalfReductionStat))),
                movementSpeed = (float)Math.Max(balance.minMovementSpeed, Math.Min(balance.maxMovementSpeed,
                    balance.movementOffset + stats.speed * balance.movementPerPoint))
            };
        }
        static int RoundClamp(double value, int min, int max) =>
            (int)Math.Round(Math.Max(min, Math.Min(max, value)), MidpointRounding.AwayFromZero);

        /// <summary>Recalculating maximum HP never grants healing; shrinking the maximum clamps current HP.</summary>
        public static float ClampHealth(float currentHealth, int maxHealth) =>
            (float)Math.Max(0, Math.Min(maxHealth, currentHealth));
    }
}

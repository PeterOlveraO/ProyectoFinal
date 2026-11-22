using System;
using UnityEngine;

namespace Game.Rounds
{
    public enum TemporaryEffect { Overcharge, Haste, Barrier, Regeneration }
    public enum GroundPickupKind { TemporaryEffect, BonusPoints }

    /// <summary>Return an accessible ground anchor near this death, or false to suppress the drop.</summary>
    public delegate bool TryDropPlacement(Vector3 deathPosition, out Vector3 groundPosition);

    public readonly struct DeathDropRoll
    {
        public readonly bool HasTemporary;
        public readonly bool HasPoints;
        public readonly TemporaryEffect Effect;

        public DeathDropRoll(bool hasTemporary, bool hasPoints, TemporaryEffect effect)
        {
            HasTemporary = hasTemporary;
            HasPoints = hasPoints;
            Effect = effect;
        }
    }

    /// <summary>Run-only pickups; the caller captures the point value when the enemy spawns.</summary>
    public static class DropDefinitions
    {
        public const int EffectCount = 4;
        public const float TemporaryChance = .5f;
        public const float BonusPointsChance = .3f;
        public const int NormalBonusPoints = 10;
        public const float Lifetime = 5f;
        public const float ContactRadius = .7f;
        public const float MaxFeetAboveGround = .45f;
        public const float MaxFeetBelowGround = .15f;
        public const float ReserveDurationMultiplier = 1.25f;
        public const float RecoveryDelay = 5f;
        public const float RecoveryPerSecond = 1f;
        public const float RegenerationPerSecond = 2f;
        public const int PoolCapacity = 128;

        public static bool IsValid(TemporaryEffect type) => (int)type >= 0 && (int)type < EffectCount;

        public static float Duration(TemporaryEffect type)
        {
            switch (type)
            {
                case TemporaryEffect.Overcharge:
                case TemporaryEffect.Barrier: return 8f;
                case TemporaryEffect.Haste:
                case TemporaryEffect.Regeneration: return 6f;
                default: return 0f;
            }
        }

        public static string Name(TemporaryEffect type)
        {
            switch (type)
            {
                case TemporaryEffect.Overcharge: return "SOBRECARGA";
                case TemporaryEffect.Haste: return "PRISA";
                case TemporaryEffect.Barrier: return "BARRERA";
                case TemporaryEffect.Regeneration: return "REGENERACIÓN";
                default: return "";
            }
        }

        public static Color Color(TemporaryEffect type)
        {
            switch (type)
            {
                case TemporaryEffect.Overcharge: return new Color32(255, 133, 72, 255);
                case TemporaryEffect.Haste: return new Color32(93, 226, 244, 255);
                case TemporaryEffect.Barrier: return new Color32(188, 154, 255, 255);
                case TemporaryEffect.Regeneration: return new Color32(132, 239, 135, 255);
                default: return UnityEngine.Color.white;
            }
        }

        /// <summary>
        /// Independent draws: temporary chance, point chance, then effect (only if awarded).
        /// Bosses still consume the first draw, so injected sequences have a stable ordering.
        /// A draw exactly at a probability threshold does not award that drop.
        /// </summary>
        public static DeathDropRoll Roll(bool boss, Func<float> random01 = null)
        {
            float temporaryRoll = Next(random01);
            float pointRoll = Next(random01);
            bool temporary = boss || temporaryRoll < TemporaryChance;
            var effect = temporary
                ? (TemporaryEffect)Mathf.Min(EffectCount - 1, Mathf.FloorToInt(Next(random01) * EffectCount))
                : TemporaryEffect.Overcharge;
            return new DeathDropRoll(temporary, pointRoll < BonusPointsChance, effect);
        }

        static float Next(Func<float> random01)
        {
            float value = random01 != null ? random01() : UnityEngine.Random.value;
            return float.IsNaN(value) ? 1f : Mathf.Clamp01(value);
        }
    }
}

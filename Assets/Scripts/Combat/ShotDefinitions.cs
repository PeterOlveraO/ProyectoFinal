using UnityEngine;

namespace Game.Combat
{
    public enum ShotType { Basic, Burst, Fan, Piercing, Precision, Boomerang, Explosive }

    /// <summary>Small, fixed patterns: no target queries or homing when choosing an axis.</summary>
    public static class ShotDefinitions
    {
        public const int Count = 7;
        public const int MaxPiercingTargets = 3;
        public const int MaxBoomerangTargetsPerLeg = 2;
        public const int MaxExplosionAdditionalTargets = 3;
        public const float ExplosionRadius = 2f;
        public const float ExplosionDamageMultiplier = .75f;
        public const float AdvancedFireInterval = 1.4f;
        public const float PrecisionRange = 26f;
        public const float PrecisionSpeed = 22f;
        public const float BoomerangLegRange = 9f;
        public const float ExplosiveRange = 14f;
        public const float BurstPulseInterval = .12f;
        public const float FanAngle = 16f;

        public static bool IsValid(ShotType type) => (int)type >= 0 && (int)type < Count;

        public static string Name(ShotType type)
        {
            switch (type)
            {
                case ShotType.Basic: return "BÁSICO";
                case ShotType.Burst: return "RÁFAGA";
                case ShotType.Fan: return "ABANICO";
                case ShotType.Piercing: return "PERFORANTE";
                case ShotType.Precision: return "PRECISIÓN";
                case ShotType.Boomerang: return "BUMERÁN";
                case ShotType.Explosive: return "EXPLOSIVO";
                default: return "";
            }
        }

        public static int Price(ShotType type)
        {
            switch (type)
            {
                case ShotType.Basic: return 80;
                case ShotType.Burst: return 140;
                case ShotType.Fan: return 200;
                case ShotType.Piercing: return 280;
                case ShotType.Precision: return 260;
                case ShotType.Boomerang: return 280;
                case ShotType.Explosive: return 320;
                default: return 0;
            }
        }

        public static Color Color(ShotType type)
        {
            switch (type)
            {
                case ShotType.Burst: return new Color(1f, .48f, .2f);
                case ShotType.Fan: return new Color(.35f, .9f, 1f);
                case ShotType.Piercing: return new Color(.8f, .5f, 1f);
                case ShotType.Precision: return new Color(.4f, 1f, .75f);
                case ShotType.Boomerang: return new Color(1f, .75f, .3f);
                case ShotType.Explosive: return new Color(1f, .35f, .3f);
                default: return new Color(1f, .8f, .16f);
            }
        }

        public static float Interval(ShotType type, float basicInterval) =>
            type == ShotType.Precision || type == ShotType.Boomerang || type == ShotType.Explosive
                ? AdvancedFireInterval : basicInterval;

        public static float Range(ShotType type, float basicRange)
        {
            switch (type)
            {
                case ShotType.Precision: return PrecisionRange;
                case ShotType.Boomerang: return BoomerangLegRange;
                case ShotType.Explosive: return ExplosiveRange;
                default: return basicRange;
            }
        }

        public static float Speed(ShotType type, float basicSpeed) =>
            type == ShotType.Precision ? PrecisionSpeed : basicSpeed;

        public static Vector3 FanDirection(Vector3 axis, int pellet)
        {
            axis.y = 0f;
            if (axis.sqrMagnitude < .0001f) return Vector3.zero;
            return Quaternion.Euler(0f, (Mathf.Clamp(pellet, 0, 2) - 1) * FanAngle, 0f) * axis.normalized;
        }
    }
}

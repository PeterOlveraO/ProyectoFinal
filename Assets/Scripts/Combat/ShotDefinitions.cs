using UnityEngine;

namespace Game.Combat
{
    public enum ShotType { Basic, Burst, Fan, Piercing }

    /// <summary>Small, fixed patterns: no target queries or homing when choosing an axis.</summary>
    public static class ShotDefinitions
    {
        public const int Count = 4;
        public const int MaxPiercingTargets = 3;
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
                default: return "";
            }
        }

        public static int Price(ShotType type)
        {
            switch (type)
            {
                case ShotType.Burst: return 60;
                case ShotType.Fan: return 90;
                case ShotType.Piercing: return 120;
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
                default: return new Color(1f, .8f, .16f);
            }
        }

        public static Vector3 FanDirection(Vector3 axis, int pellet)
        {
            axis.y = 0f;
            if (axis.sqrMagnitude < .0001f) return Vector3.zero;
            return Quaternion.Euler(0f, (Mathf.Clamp(pellet, 0, 2) - 1) * FanAngle, 0f) * axis.normalized;
        }
    }
}

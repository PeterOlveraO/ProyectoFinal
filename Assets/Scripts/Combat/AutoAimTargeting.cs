using Game.Enemies;
using Game.Player;
using Game.Terrain;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>Acquires an axis before firing. Projectiles remain non-homing.</summary>
    public static class AutoAimTargeting
    {
        static readonly RaycastHit[] Hits = new RaycastHit[32];

        public static bool TryDirection(Vector3 origin, float range, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (range <= 0 || float.IsNaN(range) || float.IsInfinity(range)) return false;
            float best = range * range;
            var terrain = WorldTerrain.Instance;
            foreach (var enemy in EnemyTriangle.All)
            {
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDead) continue;
                Vector3 target = enemy.transform.position + Vector3.up * .62f;
                Vector3 axis = target - origin;
                if (Mathf.Abs(axis.y) > 1.9f) continue;
                axis.y = 0;
                float distance = axis.sqrMagnitude;
                if (distance < .0001f || distance > best) continue;
                if (terrain != null && terrain.Traversal != null &&
                    !terrain.Traversal.CanProjectileTraverse(origin, target, .1f)) continue;
                if (!HasLineOfSight(origin, target)) continue;
                best = distance;
                direction = axis.normalized;
            }
            return direction.sqrMagnitude > 0;
        }

        static bool HasLineOfSight(Vector3 origin, Vector3 target)
        {
            Vector3 delta = target - origin;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, Hits, delta.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == Hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var collider = Hits[i].collider;
                if (collider == null || collider.GetComponentInParent<EnemyTriangle>() != null ||
                    collider.GetComponentInParent<PlayerSphereController>() != null) continue;
                return false;
            }
            return true;
        }
    }
}

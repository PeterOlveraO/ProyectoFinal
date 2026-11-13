using Game.Core;
using Game.Enemies;
using Game.Rendering;
using Game.Terrain;
using UnityEngine;
using System.Collections.Generic;

namespace Game.Combat
{
    /// <summary>
    /// Bala del ataque. Viaja en línea recta (siguiendo la altura del terreno), impacta al
    /// primer enemigo que toca y le resta vida. El daño lo decide <see cref="PlayerAttack"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackProjectile : MonoBehaviour
    {
        const float HitRadius = 0.60f;
        const float MaxHitHeight = 1.9f;
        const float GroundClearance = 0.62f;

        static Material _trailMaterial;
        static readonly List<AttackProjectile> Active = new List<AttackProjectile>();

        struct Impact : System.IComparable<Impact>
        {
            public EnemyTriangle Enemy;
            public float Time;
            public int Order;

            public int CompareTo(Impact other)
            {
                int result = Time.CompareTo(other.Time);
                return result != 0 ? result : Order.CompareTo(other.Order);
            }
        }

        Vector3 _direction;
        float _speed;
        float _range;
        float _travelled;
        int _damage;
        Color _color;
        WorldTerrain _terrain;

        SpriteRenderer _glow;
        float _pulse;
        int _pierce;
        bool _area;
        readonly HashSet<EnemyTriangle> _hitEnemies = new HashSet<EnemyTriangle>();
        readonly List<Impact> _impacts = new List<Impact>();
        readonly List<EnemyTriangle> _splashTargets = new List<EnemyTriangle>();

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        /// <summary>Desactiva y destruye todos los proyectiles activos, sin efectos de impacto.</summary>
        public static void CleanupAll()
        {
            while (Active.Count > 0)
            {
                int last = Active.Count - 1;
                var projectile = Active[last];
                Active.RemoveAt(last);
                if (projectile == null) continue;
                projectile.gameObject.SetActive(false);
                Destroy(projectile.gameObject);
            }
        }

        public static AttackProjectile Spawn(Vector3 origin, Vector3 direction, float speed, float range,
                                             int damage, Color color, int pierce = 0, bool area = false)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;

            var go = new GameObject("AttackProjectile");
            go.transform.position = origin;

            var projectile = go.AddComponent<AttackProjectile>();
            projectile._pierce = Mathf.Max(0, pierce);
            projectile._area = area;
            projectile.Initialize(direction, speed, range, damage, color);
            return projectile;
        }

        void Initialize(Vector3 direction, float speed, float range, int damage, Color color)
        {
            _direction = direction.normalized;
            _speed = Mathf.Max(1f, speed);
            _range = Mathf.Max(1f, range);
            _damage = Mathf.Max(1, damage);
            _color = color;

            BuildVisual();
        }

        void BuildVisual()
        {
            var billboard = gameObject.AddComponent<BillboardSprite>();
            billboard.SetContinuous(false);

            _glow = gameObject.AddComponent<SpriteRenderer>();
            _glow.sprite = ProceduralSprites.Glow;
            _glow.color = new Color(_color.r, _color.g, _color.b, 0.85f);
            _glow.sortingOrder = 18;
            transform.localScale = Vector3.one * 1.05f;

            var core = new GameObject("Core");
            core.transform.SetParent(transform, false);
            core.transform.localScale = Vector3.one * 0.34f;

            var coreRenderer = core.AddComponent<SpriteRenderer>();
            coreRenderer.sprite = ProceduralSprites.Circle;
            coreRenderer.color = new Color(1f, 0.98f, 0.85f, 1f);
            coreRenderer.sortingOrder = 19;

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.20f;
            trail.startWidth = 0.40f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.04f;
            trail.numCapVertices = 3;
            trail.alignment = LineAlignment.View;
            trail.sortingOrder = 17;
            trail.startColor = _color;
            trail.endColor = new Color(_color.r, _color.g, _color.b, 0f);

            if (_trailMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null) _trailMaterial = new Material(shader) { name = "ProjectileTrail" };
            }
            if (_trailMaterial != null) trail.material = _trailMaterial;

            _pulse = Random.Range(0f, Mathf.PI * 2f);
        }

        void Update()
        {
            if (GameFlow.Blocked) return;

            float step = Mathf.Min(_speed * Time.deltaTime, Mathf.Max(0f, _range - _travelled));
            Vector3 start = transform.position;
            transform.position += _direction * step;
            _travelled += step;

            FollowGround();
            Pulse();

            if (CheckHit(start, transform.position)) return;

            if (_travelled >= _range)
            {
                HitBurst.Spawn(transform.position, _color, 5, 0.5f);
                Destroy(gameObject);
            }
        }

        /// <summary>Mantiene la bala a una altura constante sobre el terreno para no cruzar colinas.</summary>
        void FollowGround()
        {
            if (_terrain == null) _terrain = WorldTerrain.Instance;
            if (_terrain == null || _terrain.Map == null) return;

            Vector3 position = transform.position;
            Vector3 local = position - _terrain.transform.position;
            int x = Mathf.FloorToInt(local.x / _terrain.BlockSize);
            int z = Mathf.FloorToInt(local.z / _terrain.BlockSize);

            if (!_terrain.Map.Contains(x, z)) return;
            if (_terrain.IsLakeAtWorld(position)) return;

            float targetY = _terrain.BlockTopCenter(x, z).y + GroundClearance;
            position.y = Mathf.Lerp(position.y, targetY, 18f * Time.deltaTime);
            transform.position = position;
        }

        void Pulse()
        {
            if (_glow == null) return;

            _pulse += Time.deltaTime * 14f;
            float scale = 1.05f + Mathf.Sin(_pulse) * 0.08f;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        bool CheckHit(Vector3 start, Vector3 end)
        {
            var enemies = EnemyTriangle.All;
            if (enemies == null) return false;
            _impacts.Clear();

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy.IsDead || _hitEnemies.Contains(enemy)) continue;

                if (SegmentHit(start, end, enemy.transform.position, out float time))
                    _impacts.Add(new Impact { Enemy = enemy, Time = time, Order = i });
            }

            _impacts.Sort();
            for (int i = 0; i < _impacts.Count; i++)
            {
                var impact = _impacts[i];
                var enemy = impact.Enemy;
                if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled || _hitEnemies.Contains(enemy)) continue;
                Vector3 position = Vector3.Lerp(start, end, impact.Time);
                Vector3 hitPoint = position + _direction * 0.1f;
                _hitEnemies.Add(enemy);
                enemy.TakeDamage(_damage);
                HitBurst.Spawn(hitPoint, _color, 8, 0.8f);
                if (_area) DamageNearby(position, enemy);
                if (_pierce > 0) { _pierce--; continue; }
                Destroy(gameObject);
                return true;
            }

            return false;
        }

        // Intersección del segmento con el cilindro horizontal y la franja vertical del enemigo.
        static bool SegmentHit(Vector3 start, Vector3 end, Vector3 center, out float time)
        {
            Vector3 delta = end - start;
            float x = start.x - center.x;
            float z = start.z - center.z;
            float a = delta.x * delta.x + delta.z * delta.z;
            float c = x * x + z * z - HitRadius * HitRadius;
            float enter = 0f;
            float exit = 1f;
            if (a < 0.00000001f)
            {
                if (c > 0f) { time = 0f; return false; }
            }
            else
            {
                float b = x * delta.x + z * delta.z;
                float discriminant = b * b - a * c;
                if (discriminant < 0f) { time = 0f; return false; }
                float root = Mathf.Sqrt(discriminant);
                enter = Mathf.Max(enter, (-b - root) / a);
                exit = Mathf.Min(exit, (-b + root) / a);
            }

            if (Mathf.Abs(delta.y) < 0.00000001f)
            {
                if (Mathf.Abs(start.y - center.y) > MaxHitHeight) { time = 0f; return false; }
            }
            else
            {
                float first = (center.y - MaxHitHeight - start.y) / delta.y;
                float second = (center.y + MaxHitHeight - start.y) / delta.y;
                enter = Mathf.Max(enter, Mathf.Min(first, second));
                exit = Mathf.Min(exit, Mathf.Max(first, second));
            }
            time = enter;
            return enter <= exit;
        }

        void DamageNearby(Vector3 position, EnemyTriangle primary)
        {
            const float radius = 2.5f;
            var enemies = EnemyTriangle.All;
            _splashTargets.Clear();
            int splashDamage = Mathf.Max(1, Mathf.CeilToInt(_damage * .5f));
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy == primary || enemy.IsDead) continue;
                Vector3 delta = enemy.transform.position - position;
                if (Mathf.Abs(delta.y) > MaxHitHeight) continue;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radius * radius) _splashTargets.Add(enemy);
            }
            for (int i = 0; i < _splashTargets.Count; i++)
            {
                var enemy = _splashTargets[i];
                if (enemy != null && !enemy.IsDead && enemy.isActiveAndEnabled) enemy.TakeDamage(splashDamage);
            }
            HitBurst.Spawn(position, _color, 14, 1.8f);
        }
    }
}

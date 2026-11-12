using Game.Core;
using Game.Enemies;
using Game.Rendering;
using Game.Terrain;
using UnityEngine;

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

        Vector3 _direction;
        float _speed;
        float _range;
        float _travelled;
        int _damage;
        Color _color;
        WorldTerrain _terrain;

        SpriteRenderer _glow;
        float _pulse;

        public static AttackProjectile Spawn(Vector3 origin, Vector3 direction, float speed, float range,
                                             int damage, Color color)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;

            var go = new GameObject("AttackProjectile");
            go.transform.position = origin;

            var projectile = go.AddComponent<AttackProjectile>();
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

            float step = _speed * Time.deltaTime;
            transform.position += _direction * step;
            _travelled += step;

            FollowGround();
            Pulse();

            if (CheckHit()) return;

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

        bool CheckHit()
        {
            var enemies = EnemyTriangle.All;
            if (enemies == null) return false;

            Vector3 position = transform.position;

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null) continue;

                Vector3 delta = enemy.transform.position - position;
                if (Mathf.Abs(delta.y) > MaxHitHeight) continue;

                delta.y = 0f;
                if (delta.sqrMagnitude > HitRadius * HitRadius) continue;

                Vector3 hitPoint = position + _direction * 0.1f;
                enemy.TakeDamage(_damage);
                HitBurst.Spawn(hitPoint, _color, 8, 0.8f);
                Destroy(gameObject);
                return true;
            }

            return false;
        }
    }
}

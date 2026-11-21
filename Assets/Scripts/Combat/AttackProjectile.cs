using System.Collections.Generic;
using Game.Core;
using Game.Enemies;
using Game.Rendering;
using Game.Terrain;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Straight, terrain-following projectile. Sweeps each frame; piercing is bounded
    /// to three unique enemies with no per-frame allocations or target steering.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackProjectile : MonoBehaviour
    {
        const float HitRadius = 0.60f;
        const float MaxHitHeight = 1.9f;
        const float GroundClearance = 0.62f;

        static Material _trailMaterial;
        static readonly List<AttackProjectile> Active = new List<AttackProjectile>(64);

        Vector3 _direction;
        Vector3 _previousPosition;
        float _speed;
        float _range;
        float _travelled;
        int _damage;
        int _maxTargets = 1;
        int _hitCount;
        readonly EnemyTriangle[] _hitTargets = new EnemyTriangle[ShotDefinitions.MaxPiercingTargets];
        bool _retired;
        Color _color;
        WorldTerrain _terrain;

        SpriteRenderer _glow;
        float _pulse;

        public static AttackProjectile Spawn(Vector3 origin, Vector3 direction, float speed, float range,
                                             int damage, Color color, int maxTargets = 1)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;

            var go = new GameObject("AttackProjectile");
            go.transform.position = origin;

            var projectile = go.AddComponent<AttackProjectile>();
            projectile.Initialize(direction, speed, range, damage, color, maxTargets);
            return projectile;
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        public static void ClearAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
                if (Active[i] != null) Active[i].Retire();
        }

        void Retire()
        {
            if (_retired) return;
            _retired = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        void Initialize(Vector3 direction, float speed, float range, int damage, Color color, int maxTargets)
        {
            _direction = direction.normalized;
            _previousPosition = transform.position;
            _speed = Mathf.Max(1f, speed);
            _range = Mathf.Max(1f, range);
            _damage = Mathf.Max(1, damage);
            _maxTargets = Mathf.Clamp(maxTargets, 1, ShotDefinitions.MaxPiercingTargets);
            _color = color;

            BuildVisual();
        }

        void BuildVisual()
        {
            var billboard = gameObject.AddComponent<BillboardSprite>();
            billboard.SetContinuous(false);

            _glow = gameObject.AddComponent<SpriteRenderer>();
            _glow.sprite = PixelCombatSprites.Shot;
            _glow.color = new Color(_color.r, _color.g, _color.b, 1f);
            _glow.sortingOrder = 18;
            transform.localScale = Vector3.one * .48f;

            var core = new GameObject("Core");
            core.transform.SetParent(transform, false);
            core.transform.localScale = Vector3.one * 0.44f;

            var coreRenderer = core.AddComponent<SpriteRenderer>();
            coreRenderer.sprite = PixelCombatSprites.Shot;
            coreRenderer.color = new Color(1f, 0.98f, 0.85f, 1f);
            coreRenderer.sortingOrder = 19;

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.10f;
            trail.startWidth = 0.12f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.04f;
            trail.numCapVertices = 0;
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
            if (_retired) return;
            if (GameFlow.Over)
            {
                Retire();
                return;
            }
            if (GameFlow.Blocked) return;

            _previousPosition = transform.position;
            float step = Mathf.Min(_speed * Time.deltaTime, _range - _travelled);
            transform.position += _direction * step;
            _travelled += step;

            FollowGround();
            Pulse();

            if (CheckHit()) return;

            if (_travelled >= _range)
            {
                HitBurst.Spawn(transform.position, _color, 5, 0.5f);
                Retire();
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
            float scale = .48f + Mathf.Sin(_pulse) * .025f;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        bool CheckHit()
        {
            if (_retired) return true;
            var enemies = EnemyTriangle.All;
            if (enemies == null) return false;

            Vector3 segment = transform.position - _previousPosition;
            Vector3 horizontal = new Vector3(segment.x, 0f, segment.z);
            float lengthSquared = horizontal.sqrMagnitude;
            while (_hitCount < _maxTargets)
            {
                EnemyTriangle nearest = null;
                float nearestT = float.PositiveInfinity;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (enemy == null || enemy.IsDead || AlreadyHit(enemy)) continue;
                    Vector3 offset = enemy.transform.position - _previousPosition;
                    offset.y = 0f;
                    float t = lengthSquared > .000001f ? Mathf.Clamp01(Vector3.Dot(offset, horizontal) / lengthSquared) : 0f;
                    Vector3 closest = _previousPosition + segment * t;
                    Vector3 delta = enemy.transform.position - closest;
                    if (Mathf.Abs(delta.y) > MaxHitHeight) continue;
                    delta.y = 0f;
                    if (delta.sqrMagnitude > HitRadius * HitRadius || t >= nearestT) continue;
                    nearestT = t;
                    nearest = enemy;
                }
                if (nearest == null) break;
                _hitTargets[_hitCount++] = nearest;
                nearest.TakeDamage(_damage);
                HitBurst.Spawn(_previousPosition + segment * nearestT, _color, 8, .8f);
                if (_hitCount >= _maxTargets)
                {
                    Retire();
                    return true;
                }
            }
            return false;
        }

        bool AlreadyHit(EnemyTriangle enemy)
        {
            for (int i = 0; i < _hitCount; i++)
                if (_hitTargets[i] == enemy) return true;
            return false;
        }
    }
}

using System.Collections.Generic;
using Game.Core;
using Game.Enemies;
using Game.Rendering;
using Game.Terrain;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Terrain-following fixed-axis shots. Swept hits, bounded piercing, a two-leg
    /// boomerang and a deduplicated explosion never choose or steer the aim axis.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackProjectile : MonoBehaviour
    {
        const float HitRadius = 0.60f;
        const float MaxHitHeight = 1.9f;
        const float GroundClearance = 0.62f;

        static readonly List<AttackProjectile> Active = new List<AttackProjectile>(64);
        static readonly RaycastHit[] BlastHits = new RaycastHit[32];

        /// <summary>Optional world-zone guard, also applied to explosion line-of-sight segments.</summary>
        public static System.Func<Vector3, Vector3, bool> CanTravelSegment { get; set; }

        Vector3 _direction;
        Vector3 _previousPosition;
        float _speed;
        float _range;
        float _travelled;
        int _damage;
        int _maxTargets = 1;
        int _hitCount;
        readonly EnemyTriangle[] _hitTargets = new EnemyTriangle[1 + ShotDefinitions.MaxExplosionAdditionalTargets];
        ShotType _shotType;
        bool _returning;
        bool _retired;
        Color _color;
        PokemonElement _element;
        bool _elemental;
        WorldTerrain _terrain;

        SpriteRenderer _glow;
        Transform _visual;
        Transform _visualAnchor;
        Camera _camera;
        Sprite _frame0;
        Sprite _frame1;
        float _pulse;
        float _visualTime;

        public static AttackProjectile Spawn(Vector3 origin, Vector3 direction, float speed, float range,
                                             int damage, Color color, int maxTargets = 1)
            => SpawnInternal(origin, direction, speed, range, damage, color, maxTargets, PokemonElement.Normal, false);

        public static AttackProjectile Spawn(Vector3 origin, Vector3 direction, float speed, float range,
                                             int damage, Color color, int maxTargets, PokemonElement element)
            => SpawnInternal(origin, direction, speed, range, damage, color, maxTargets, element, true);

        /// <summary>Range is the one-way distance for a boomerang; damage is already modified by the firing pattern.</summary>
        public static AttackProjectile SpawnPattern(Vector3 origin, Vector3 direction, float speed, float range,
            int damage, Color color, ShotType type, PokemonElement element)
        {
            int targets = type == ShotType.Piercing ? ShotDefinitions.MaxPiercingTargets :
                type == ShotType.Boomerang ? ShotDefinitions.MaxBoomerangTargetsPerLeg : 1;
            var projectile = SpawnInternal(origin, direction, speed, range, damage, color, targets, element, true);
            projectile._shotType = type;
            return projectile;
        }

        static AttackProjectile SpawnInternal(Vector3 origin, Vector3 direction, float speed, float range,
                                               int damage, Color color, int maxTargets, PokemonElement element, bool elemental)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;

            var go = new GameObject("AttackProjectile");
            go.transform.position = origin;

            var projectile = go.AddComponent<AttackProjectile>();
            projectile._element = element;
            projectile._elemental = elemental;
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
            var anchor = new GameObject("VisualAnchor");
            anchor.transform.SetParent(transform, false);
            _visualAnchor = anchor.transform;
            var billboard = anchor.AddComponent<BillboardSprite>();
            billboard.SetContinuous(false);

            var visual = new GameObject("ElementVisual");
            visual.transform.SetParent(anchor.transform, false);
            _visual = visual.transform;
            _glow = visual.AddComponent<SpriteRenderer>();
            if (_elemental)
            {
                _frame0 = PixelCombatSprites.ElementShot(_element, 0);
                _frame1 = PixelCombatSprites.ElementShot(_element, 1);
                _camera = Camera.main;
            }
            _glow.sprite = _elemental ? _frame0 : PixelCombatSprites.Shot;
            Color visualColor = _elemental ? PixelCombatSprites.ElementColor(_element) : _color;
            _glow.color = _elemental ? Color.white : new Color(visualColor.r, visualColor.g, visualColor.b, 1f);
            _glow.sortingOrder = 18;
            _visual.localScale = Vector3.one * (_elemental ? .78f : .48f);

            var core = new GameObject("Core");
            core.transform.SetParent(_visual, false);
            core.transform.localScale = Vector3.one * 0.44f;

            var coreRenderer = core.AddComponent<SpriteRenderer>();
            coreRenderer.sprite = PixelCombatSprites.Shot;
            coreRenderer.color = new Color(1f, 0.98f, 0.85f, 1f);
            coreRenderer.sortingOrder = 19;
            coreRenderer.enabled = !_elemental;

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.10f;
            trail.startWidth = 0.12f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.04f;
            trail.numCapVertices = 0;
            trail.alignment = LineAlignment.View;
            trail.sortingOrder = 17;
            trail.startColor = visualColor;
            trail.endColor = new Color(visualColor.r, visualColor.g, visualColor.b, 0f);

            trail.sharedMaterial = _glow.sharedMaterial;

            _pulse = Random.Range(0f, Mathf.PI * 2f);
        }

        void Update() => TickProjectile(Time.deltaTime);

        void TickProjectile(float deltaTime)
        {
            if (_retired) return;
            if (GameFlow.Over)
            {
                Retire();
                return;
            }
            if (GameFlow.Blocked) return;

            float remaining = Mathf.Max(0f, deltaTime) * _speed;
            // At most two segments: one outward leg and one return on the original axis.
            // Splitting at the endpoint prevents a long frame from skipping the turn or its hit reset.
            for (int leg = 0; leg < 2 && !_retired; leg++)
            {
                _previousPosition = transform.position;
                float step = Mathf.Min(remaining, _range - _travelled);
                Vector3 next = transform.position + _direction * step;
                bool blocked = !CanTravel(_previousPosition, next);
                if (blocked)
                {
                    if (!CanTravel(_previousPosition, _previousPosition))
                    {
                        Retire();
                        return;
                    }
                    // A long frame may reach a frontier after passing a valid enemy.
                    // Sweep up to the last legal point before retiring at the boundary.
                    float low = 0f, high = 1f;
                    for (int i = 0; i < 14; i++)
                    {
                        float middle = (low + high) * .5f;
                        if (CanTravel(_previousPosition, Vector3.Lerp(_previousPosition, next, middle)))
                            low = middle;
                        else high = middle;
                    }
                    next = Vector3.Lerp(_previousPosition, next, low);
                    step *= low;
                }
                transform.position = next;
                _travelled += step;
                remaining -= step;
                FollowGround();
                if (CheckHit()) return;
                if (blocked)
                {
                    Retire();
                    return;
                }
                if (_travelled < _range) break;
                if (_shotType == ShotType.Boomerang && !_returning)
                {
                    _returning = true;
                    _direction = -_direction;
                    _travelled = 0f;
                    _hitCount = 0;
                    System.Array.Clear(_hitTargets, 0, _hitTargets.Length);
                    if (remaining <= 0f) break;
                }
                else
                {
                    if (_elemental) HitBurst.SpawnElement(transform.position, _element, 4, .5f);
                    else HitBurst.Spawn(transform.position, _color, 5, .5f);
                    Retire();
                }
            }
            Pulse();
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
            float scale = (_elemental ? .78f : .48f) + Mathf.Sin(_pulse) * .025f;
            _visual.localScale = new Vector3(scale, scale, 1f);
            if (_elemental)
            {
                _visualTime += Time.deltaTime;
                _glow.sprite = ((int)(_visualTime * 12f) & 1) == 0 ? _frame0 : _frame1;
                if (_camera == null) _camera = Camera.main;
                if (_camera != null)
                {
                    Vector3 view = _camera.transform.InverseTransformDirection(_direction);
                    Vector3 right = _camera.transform.InverseTransformDirection(_visualAnchor.right);
                    Vector3 up = _camera.transform.InverseTransformDirection(_visualAnchor.up);
                    float determinant = right.x * up.y - right.y * up.x;
                    if (Mathf.Abs(determinant) > .0001f)
                    {
                        float x = (view.x * up.y - view.y * up.x) / determinant;
                        float y = (view.y * right.x - view.x * right.y) / determinant;
                        if (x * x + y * y > .00001f)
                            _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(y, x) * Mathf.Rad2Deg);
                    }
                }
            }
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
                    if (!CanTravel(closest, enemy.transform.position)) continue;
                    nearestT = t;
                    nearest = enemy;
                }
                if (nearest == null) break;
                _hitTargets[_hitCount++] = nearest;
                nearest.TakeDamage(_damage);
                Vector3 impact = _previousPosition + segment * nearestT;
                if (_elemental)
                {
                    HitBurst.SpawnElement(impact, _element, 6, .8f);
                    Game.Audio.GameAudio.PlayImpact(_element);
                }
                else HitBurst.Spawn(impact, _color, 8, .8f);
                if (_shotType == ShotType.Explosive) Explode(impact);
                if (_hitCount >= _maxTargets)
                {
                    if (_shotType == ShotType.Boomerang) return false;
                    Retire();
                    return true;
                }
            }
            return false;
        }

        void Explode(Vector3 center)
        {
            if (_elemental) HitBurst.SpawnElement(center, _element, 14, ShotDefinitions.ExplosionRadius);
            else HitBurst.Spawn(center, _color, 14, ShotDefinitions.ExplosionRadius);
            int splashDamage = Mathf.Max(1, Mathf.RoundToInt(_damage * ShotDefinitions.ExplosionDamageMultiplier));
            int additional = 0;
            var enemies = EnemyTriangle.All;
            while (additional < ShotDefinitions.MaxExplosionAdditionalTargets)
            {
                EnemyTriangle nearest = null;
                float best = float.PositiveInfinity;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (enemy == null || enemy.IsDead || AlreadyHit(enemy)) continue;
                    Vector3 delta = enemy.transform.position - center;
                    if (Mathf.Abs(delta.y) > MaxHitHeight) continue;
                    float distance = delta.sqrMagnitude;
                    if (distance > ShotDefinitions.ExplosionRadius * ShotDefinitions.ExplosionRadius ||
                        distance >= best || !HasClearBlastPath(center, enemy)) continue;
                    nearest = enemy;
                    best = distance;
                }
                if (nearest == null) break;
                _hitTargets[_hitCount++] = nearest;
                nearest.TakeDamage(splashDamage);
                additional++;
            }
        }

        static bool HasClearBlastPath(Vector3 center, EnemyTriangle target)
        {
            // Aim the occlusion ray at the body, not the feet on the terrain collider.
            Vector3 destination = target.transform.position + Vector3.up * GroundClearance;
            if (!CanTravel(center, destination)) return false;
            Vector3 delta = destination - center;
            float distance = delta.magnitude;
            if (distance < .001f) return true;
            int count = Physics.RaycastNonAlloc(center, delta / distance, BlastHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == BlastHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var collider = BlastHits[i].collider;
                if (collider == null || collider.GetComponentInParent<EnemyTriangle>() != null ||
                    collider.GetComponentInParent<Game.Player.PlayerSphereController>() != null) continue;
                return false;
            }
            return true;
        }

        bool AlreadyHit(EnemyTriangle enemy)
        {
            for (int i = 0; i < _hitCount; i++)
                if (_hitTargets[i] == enemy) return true;
            return false;
        }

        static bool CanTravel(Vector3 from, Vector3 to)
        {
            var terrain = WorldTerrain.Instance;
            return (terrain == null || terrain.Traversal == null ||
                terrain.Traversal.CanProjectileTraverse(from, to, .1f)) &&
                (CanTravelSegment == null || CanTravelSegment(from, to));
        }
    }
}

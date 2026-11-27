using System.Collections.Generic;
using Game.Combat;
using Game.Core;
using Game.Player;
using Game.Pokemon;
using Game.Rendering;
using Game.Rounds;
using Game.Terrain;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Enemigo Pokémon. Persigue al jugador por el terreno respetando las reglas del mundo
    /// (no escala desniveles de más de un bloque) y, mientras lo toque, le resta
    /// <see cref="ContactDamage"/> de vida por segundo.
    /// Vida/daño/velocidad se configuran por ronda (o por zona en el spawner antiguo).
    /// La barra de vida aparece al recibir daño.
    /// El visual es un sprite 2D: no tiene geometría 3D.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyTriangle : MonoBehaviour
    {
        /// <summary>Distancia horizontal a la que se considera que toca al jugador.</summary>
        public const float TouchRadius = 0.85f;

        /// <summary>Diferencia vertical máxima para que el toque cuente (evita tocar desde un acantilado).</summary>
        const float MaxTouchHeight = 1.5f;

        /// <summary>Velocidad con la que el visual se ajusta a la altura del bloque.</summary>
        const float GroundSnapSpeed = 30f;

        /// <summary>Vida del enemigo de la zona 1. Cada zona suma un 20 %.</summary>
        public const int BaseHealth = 10;

        /// <summary>Incremento de vida por zona (0.20 = +20 %).</summary>
        public const float HealthPerZone = 0.20f;

        /// <summary>Vida que resta al jugador por cada segundo que lo toca.</summary>
        public const float ContactDamagePerSecond = 10f;

        static readonly List<EnemyTriangle> Active = new List<EnemyTriangle>(128);

        /// <summary>Todos los enemigos activos, para selección de objetivos y gestión de rondas.</summary>
        public static IReadOnlyList<EnemyTriangle> All => Active;

        [Tooltip("Vida máxima. La zona 1 usa 10 y cada zona siguiente suma un 20 %.")]
        [SerializeField] int maxHealth = BaseHealth;

        ZoneSpawnManager _manager;
        WorldTerrain _terrain;
        PlayerSphereController _player;
        Renderer _visualRenderer;
        SpriteRenderer _spriteVisual;
        Color _baseVisualColor = Color.white;

        Transform _healthBar;
        SpriteRenderer _healthBarFill;

        int _health;
        float _flashUntil;
        bool _dead;

        float _speed = 2.5f;
        float _contactDamage = ContactDamagePerSecond;
        int _wallSide;

        public int Zone { get; private set; }
        public int MaxHealth => maxHealth;
        public int Health => _health;
        public float Speed => _speed * GameplayPace.MovementMultiplier;
        public float ContactDamage => _contactDamage;
        public bool IsBoss { get; private set; }
        internal int BonusDropPoints { get; set; }
        public bool IsDead => _dead || _health <= 0;
        public Renderer VisualRenderer => _visualRenderer;

        /// <summary>Registra el sprite (único visual) para que el culler lo pueda apagar.</summary>
        public void SetVisualRenderer(Renderer renderer)
        {
            if (_visualRenderer == renderer) return;

            if (isActiveAndEnabled && _visualRenderer != null)
                VisibilityCuller.UnregisterEnemy(_visualRenderer);

            _visualRenderer = renderer;
            _spriteVisual = renderer as SpriteRenderer;
            if (_spriteVisual != null) _baseVisualColor = _spriteVisual.color;
            UpdateHealthBarPosition();

            if (isActiveAndEnabled && _visualRenderer != null)
                VisibilityCuller.RegisterEnemy(_visualRenderer);
        }

        public void Initialize(ZoneSpawnManager manager, int zone, float speed)
        {
            Initialize(zone, speed);
            _manager = manager;
        }

        /// <summary>Configuración normal del spawner antiguo de zonas.</summary>
        public void Initialize(int zone, float speed)
        {
            _manager = null;
            Zone = zone;
            _speed = Mathf.Max(0.05f, speed);

            // Zona 1: 10 de vida. Zona 2: 12. Zona 3: 14 (redondeado).
            maxHealth = Mathf.Max(1, Mathf.RoundToInt(BaseHealth * Mathf.Pow(1f + HealthPerZone, zone - 1)));
            ResetCombatState(false, ContactDamagePerSecond);
        }

        /// <summary>Aplica todos los stats de la ronda, sin heredar estado del enemigo anterior.</summary>
        public void InitializeRound(int zone, EnemyRoundStats stats, bool boss)
        {
            _manager = null;
            Zone = zone;
            _speed = Mathf.Max(.05f, stats.speed);
            maxHealth = Mathf.Max(1, stats.health);
            ResetCombatState(boss, stats.contactDamage);
        }

        void ResetCombatState(bool boss, float damage)
        {
            IsBoss = boss;
            BonusDropPoints = 0;
            var pokemonVisual = GetComponent<PokemonEnemyVisual>();
            if (pokemonVisual != null)
                pokemonVisual.ApplySpeciesScale();
            _contactDamage = Mathf.Max(0f, damage);
            _flashUntil = 0f;
            if (_spriteVisual != null) _spriteVisual.color = _baseVisualColor;
            _health = maxHealth;
            _dead = false;
            UpdateHealthBar();
        }

        void Awake()
        {
            _visualRenderer = GetComponent<Renderer>();
            _health = maxHealth;

            // Cada enemigo rodea los obstáculos siempre hacia el mismo lado.
            _wallSide = Random.value < 0.5f ? -1 : 1;

            CreateHealthBar();
        }

        void OnEnable()
        {
            Active.Add(this);
            if (_visualRenderer != null) VisibilityCuller.RegisterEnemy(_visualRenderer);
        }

        void OnDisable()
        {
            Active.Remove(this);
            if (_visualRenderer != null) VisibilityCuller.UnregisterEnemy(_visualRenderer);
        }

        void OnDestroy()
        {
            if (_manager != null) _manager.NotifyDestroyed();
        }

        void Start()
        {
            if (_terrain == null) _terrain = WorldTerrain.Instance;
            SnapToGround();
        }

        void Update()
        {
            if (IsDead || !GameFlow.Playing) return;

            UpdateFlash();

            if (_player == null)
            {
                _player = PlayerSphereController.Instance;
                if (_player == null) return;
            }

            if (!_player.IsAlive) return;

            if (_terrain == null)
            {
                _terrain = WorldTerrain.Instance;
                if (_terrain == null) return;
            }

            Vector3 position = transform.position;
            Vector3 toPlayer = _player.transform.position - position;
            float heightDifference = Mathf.Abs(toPlayer.y);
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            if (distance <= TouchRadius && heightDifference <= MaxTouchHeight &&
                (_terrain.Traversal == null || _terrain.Traversal.CanTraverse(position, _player.transform.position, .35f)))
            {
                ApplyContact(_player);
                return;
            }

            if (distance <= 0.0001f) return;

            Vector3 direction = toPlayer / distance;
            if (_terrain.Traversal != null)
            {
                _terrain.Traversal.UpdateTarget(_player.transform.position);
                if (!_terrain.Traversal.CanTraverse(position, _player.transform.position, .35f))
                    direction = _terrain.Traversal.DirectionToTarget(position);
                if (direction.sqrMagnitude < .0001f) return;
            }
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            Move(direction, Speed * Time.deltaTime);
        }

        // Called only after Update's distance/height checks.
        void ApplyContact(PlayerSphereController player)
        {
            player.TakeDamage(_contactDamage * Time.deltaTime);
        }

        /// <summary>Mata a todos los enemigos vivos sin distinguir jefes.</summary>
        public static void KillAll()
        {
            if (Active.Count == 0) return;

            // Copia para no modificar la lista mientras se destruyen.
            var snapshot = Active.ToArray();
            foreach (var enemy in snapshot)
                if (enemy != null) enemy.Kill();
        }

        // ---------------------------------------------------------------- Vida

        /// <summary>Resta vida al enemigo. Devuelve true si este golpe lo ha matado.</summary>
        public bool TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead) return false;

            _health = Mathf.Max(0, _health - amount);
            _flashUntil = Time.time + 0.12f;

            if (_spriteVisual != null)
                _spriteVisual.color = new Color(1f, 0.32f, 0.28f, 1f);

            UpdateHealthBar();

            if (_health > 0) return false;

            Die();
            return true;
        }

        /// <summary>Mata al enemigo al instante.</summary>
        public void Kill()
        {
            if (IsDead) return;

            _health = 0;
            Die();
        }

        void Die()
        {
            if (_dead) return;
            _dead = true;

            // Suma puntos y monedas a la partida.
            if (RoundManager.Instance != null) RoundManager.Instance.NotifyEnemyKilled(this);

            HitBurst.Spawn(transform.position + Vector3.up * 0.7f,
                           new Color(1f, 0.85f, 0.25f, 1f), 16, 1.3f);
            Destroy(gameObject);
        }

        void UpdateFlash()
        {
            if (_flashUntil <= 0f || Time.time < _flashUntil) return;

            _flashUntil = 0f;
            if (_spriteVisual != null) _spriteVisual.color = _baseVisualColor;
        }

        void CreateHealthBar()
        {
            var root = new GameObject("HealthBar");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            _healthBar = root.transform;

            var billboard = root.AddComponent<BillboardSprite>();
            billboard.SetContinuous(true);

            var backgroundGo = new GameObject("BarBackground");
            backgroundGo.transform.SetParent(_healthBar, false);
            var background = backgroundGo.AddComponent<SpriteRenderer>();
            background.sprite = ProceduralSprites.RoundedRect;
            background.drawMode = SpriteDrawMode.Sliced;
            background.size = new Vector2(0.98f, 0.18f);
            background.color = new Color(0.04f, 0.02f, 0.08f, 0.9f);
            background.sortingOrder = 23;

            var fillGo = new GameObject("BarFill");
            fillGo.transform.SetParent(_healthBar, false);
            _healthBarFill = fillGo.AddComponent<SpriteRenderer>();
            _healthBarFill.sprite = ProceduralSprites.RoundedRect;
            _healthBarFill.drawMode = SpriteDrawMode.Sliced;
            _healthBarFill.size = new Vector2(0.86f, 0.11f);
            _healthBarFill.color = new Color32(72, 220, 120, 255);
            _healthBarFill.sortingOrder = 24;

            _healthBar.gameObject.SetActive(false);
        }

        void UpdateHealthBar()
        {
            if (_healthBar == null || _healthBarFill == null) return;
            UpdateHealthBarPosition();

            float ratio = maxHealth > 0 ? Mathf.Clamp01(_health / (float)maxHealth) : 0f;
            bool visible = _health > 0 && _health < maxHealth;

            _healthBar.gameObject.SetActive(visible);
            _healthBarFill.size = new Vector2(0.86f * ratio, 0.11f);
            _healthBarFill.color = Color.Lerp(new Color32(255, 90, 95, 255),
                                              new Color32(72, 220, 120, 255), ratio);
        }

        void UpdateHealthBarPosition()
        {
            if (_healthBar == null) return;
            var visual = GetComponent<PokemonEnemyVisual>();
            float height = visual != null ? visual.VisibleHeight : PokemonEnemyVisual.NormalHeight;
            _healthBar.localPosition = Vector3.up * (height + .27f);
        }

        // ------------------------------------------------------------ Movimiento

        void Move(Vector3 direction, float distance)
        {
            Vector3 from = transform.position;
            Vector3 step = direction * distance;

            if (TryMove(from, step)) return;

            // Deslizamiento: si el paso diagonal choca con un acantilado, se prueba cada eje.
            var slideX = new Vector3(step.x, 0f, 0f);
            if (slideX.sqrMagnitude > 1e-8f && TryMove(from, slideX)) return;

            var slideZ = new Vector3(0f, 0f, step.z);
            if (slideZ.sqrMagnitude > 1e-8f && TryMove(from, slideZ)) return;

            // Choque frontal: rodear el obstáculo en sentido tangencial para buscar una rampa.
            var tangent = new Vector3(-direction.z, 0f, direction.x) * _wallSide;
            if (TryMove(from, tangent * distance)) return;

            TryMove(from, -tangent * distance);
        }

        bool TryMove(Vector3 from, Vector3 delta)
        {
            if (delta.sqrMagnitude < 1e-8f) return false;

            Vector3 candidate = from + delta;
            if (_terrain.Traversal != null && !_terrain.Traversal.CanTraverse(from, candidate, .35f)) return false;
            if (!TryGround(candidate, out Vector3 ground)) return false;

            float y = Mathf.MoveTowards(from.y, ground.y, GroundSnapSpeed * Time.deltaTime);
            transform.position = new Vector3(candidate.x, y, candidate.z);
            return true;
        }

        /// <summary>Valida un bloque destino: dentro del mundo, sin agua y sin saltar acantilados.</summary>
        bool TryGround(Vector3 worldPosition, out Vector3 ground)
        {
            ground = worldPosition;

            var map = _terrain.Map;
            if (map == null) return false;

            if (!BlockAt(worldPosition, out int x, out int z)) return false;
            if (_terrain.IsLakeAtWorld(worldPosition)) return false;

            int currentLevel = LevelAt(transform.position);
            int nextLevel = map.Get(x, z);

            // La regla del mundo: 2 bloques ya no se escalan. Aquí, más de un nivel tampoco.
            if (Mathf.Abs(nextLevel - currentLevel) > 1) return false;

            ground = _terrain.BlockTopCenter(x, z);
            return true;
        }

        int LevelAt(Vector3 worldPosition)
        {
            if (!BlockAt(worldPosition, out int x, out int z)) return 0;
            return _terrain.Map.Get(x, z);
        }

        bool BlockAt(Vector3 worldPosition, out int x, out int z)
        {
            Vector3 local = worldPosition - _terrain.transform.position;
            x = Mathf.FloorToInt(local.x / _terrain.BlockSize);
            z = Mathf.FloorToInt(local.z / _terrain.BlockSize);
            return _terrain.Map.Contains(x, z);
        }

        void SnapToGround()
        {
            if (_terrain == null || _terrain.Map == null) return;
            if (TryGround(transform.position, out Vector3 ground)) transform.position = ground;
        }
    }
}

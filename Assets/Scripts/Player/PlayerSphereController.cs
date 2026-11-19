using Game.Combat;
using Game.Core;
using Game.Terrain;
using Game.Rounds;
using Game.Player.ProgressionCombat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Controlador del jugador: CharacterController invisible + sprite Pokémon.
    ///   - WASD / flechas (o el joystick táctil en móvil), relativo al ángulo Y de la cámara activa.
    ///   - Gravedad. stepOffset 0.55 sube un nivel (0.5) pero no dos (1.0).
    ///   - R, o caer por debajo de respawnHeight, lo devuelve al bloque central.
    ///   - Vida: 100. Cada enemigo que lo toca le resta 10 de vida por segundo.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class PlayerSphereController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float gravity = 20f;
        [SerializeField] float respawnHeight = -10f;

        [Header("Vida")]
        [Tooltip("Vida máxima del jugador.")]
        [SerializeField, Min(1)] int maxHealth = 100;

        [Tooltip("Tiempo sin recibir daño tras reaparecer, para no quedar atrapado sobre un enemigo.")]
        [SerializeField, Min(0f)] float respawnProtection = 1.5f;

        [Tooltip("Joystick táctil (móvil). Opcional: sin él solo se usa el teclado.")]
        [SerializeField] VirtualJoystick joystick;

        CharacterController _controller;
        float _verticalSpeed;
        Vector3 _lastSafePosition;
        bool _hasSafePosition;
        float _protectedUntil;

        float _health;
        bool _alive = true;

        public static PlayerSphereController Instance { get; private set; }

        public int MaxHealth => (RoundManager.Instance != null && RoundManager.Instance.HasRunPokemon ?
            RoundManager.Instance.CombatStats.maxHealth : maxHealth) + (Drinks != null && Drinks.Has(DrinkType.Vitality) ? 50 : 0);
        DrinkStations Drinks => RoundManager.Instance != null ? RoundManager.Instance.Drinks : null;
        public float MovementSpeed => (RoundManager.Instance != null && RoundManager.Instance.HasRunPokemon ?
            RoundManager.Instance.CombatStats.movementSpeed : moveSpeed) * (Drinks != null && Drinks.Has(DrinkType.Sprint) ? 1.3f : 1f);
        public float DamageReduction => Mathf.Min(RoundManager.Instance != null ? RoundManager.Instance.MaxDamageReduction : .65f,
            (RoundManager.Instance != null && RoundManager.Instance.HasRunPokemon ? RoundManager.Instance.CombatStats.damageReduction : 0f) +
            (Drinks != null && Drinks.Has(DrinkType.Armor) ? .25f : 0f));

        /// <summary>Vida actual redondeada hacia arriba (0 solo cuando está derrotado).</summary>
        public int Health => Mathf.Clamp(Mathf.CeilToInt(_health), 0, MaxHealth);

        /// <summary>Vida normalizada 0..1, para la barra del HUD.</summary>
        public float Health01 => MaxHealth > 0 ? Mathf.Clamp01(_health / MaxHealth) : 0f;

        public bool IsAlive => _alive;
        public bool IsDamaged => _alive && _health < MaxHealth;

        /// <summary>Inmune al daño tras reaparecer, pero no mata enemigos al tocarlos.</summary>
        public bool IsProtected => !_alive || Time.time < _protectedUntil;

        void Awake()
        {
            Instance = this;
            _controller = GetComponent<CharacterController>();
            _health = MaxHealth;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Asigna el joystick de movimiento desde el bootstrap de la escena.</summary>
        public void SetJoystick(VirtualJoystick value) => joystick = value;

        void Start()
        {
            var terrain = WorldTerrain.Instance;
            if (terrain != null && terrain.Player == null) terrain.Player = transform;
            Respawn();
        }

        void Update()
        {
            // Con un menú abierto o tras la derrota no se lee entrada.
            if (GameFlow.Blocked || GameFlow.Over || !_alive) return;

            var keyboard = Keyboard.current;

            if ((keyboard != null && keyboard.rKey.wasPressedThisFrame) || transform.position.y < respawnHeight)
            {
                Respawn();
                return;
            }

            Vector3 input = ReadInput(keyboard);
            if (joystick != null)
            {
                Vector2 stick = joystick.Value;
                input += new Vector3(stick.x, 0f, stick.y);
            }

            Vector3 move = Quaternion.Euler(0f, CameraYaw(), 0f) * input;
            if (move.sqrMagnitude > 1f) move.Normalize();

            if (_controller.isGrounded && _verticalSpeed < 0f) _verticalSpeed = -1f;
            _verticalSpeed -= gravity * Time.deltaTime;

            Vector3 velocity = move * MovementSpeed + Vector3.up * _verticalSpeed;
            _controller.Move(velocity * Time.deltaTime);

            // Guarda el último punto pisado fuera del agua: ahí vuelve si cae a un lago.
            var terrain = WorldTerrain.Instance;
            if (_controller.isGrounded && (terrain == null || !terrain.IsLakeAtWorld(transform.position)))
            {
                _lastSafePosition = transform.position;
                _hasSafePosition = true;
            }
        }

        // ---------------------------------------------------------------- Vida

        /// <summary>
        /// Resta vida de forma continua. Los enemigos llaman a esto cada frame con
        /// <c>dañoPorSegundo * Time.deltaTime</c> mientras siguen tocando al jugador.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (!_alive || amount <= 0f) return;
            if (IsProtected) return;

            _health -= amount * (1f - DamageReduction);
            if (_health <= 0f)
            {
                _health = 0f;
                Die();
            }
        }

        /// <summary>Cura al jugador (se usará al empezar una ronda nueva).</summary>
        public void Heal(int amount)
        {
            if (!_alive || amount <= 0) return;
            _health = Mathf.Min(MaxHealth, _health + amount);
        }

        /// <summary>Stat changes do not heal. Explicit Heal/Revive remain the only healing paths.</summary>
        public void RecalculateStats() => _health = PokemonCombatStats.ClampHealth(_health, MaxHealth);

        void Die()
        {
            if (!_alive) return;

            _alive = false;
            _verticalSpeed = 0f;

            HitBurst.Spawn(transform.position + Vector3.up * 0.6f,
                           new Color(1f, 0.25f, 0.3f, 1f), 18, 1.2f);
            RoundManager.Instance?.NotifyPlayerDefeated();
        }

        /// <summary>Deja al jugador con la vida al máximo y lo devuelve al spawn.</summary>
        public void Revive()
        {
            _health = MaxHealth;
            _alive = true;
            _protectedUntil = 0f;
            _verticalSpeed = 0f;
            _hasSafePosition = false;
            Respawn();
        }

        // ------------------------------------------------------------ Respawn

        /// <summary>Coloca la esfera sobre el bloque central del mundo.</summary>
        public void Respawn()
        {
            var terrain = WorldTerrain.Instance;
            if (terrain == null || terrain.Map == null) return;

            float halfHeight = _controller.height * 0.5f;
            Teleport(terrain.SpawnPosition +
                     Vector3.up * (halfHeight - _controller.center.y + _controller.skinWidth));

            // El punto seguro anterior pertenece a otra vida.
            _hasSafePosition = false;
            _protectedUntil = Time.time + respawnProtection;
        }

        /// <summary>Devuelve al jugador al último punto seguro (usado al caer a un lago).</summary>
        public void RespawnToSafePoint()
        {
            if (!_hasSafePosition)
            {
                Respawn();
                return;
            }

            Teleport(_lastSafePosition + Vector3.up * 0.1f);
        }

        void Teleport(Vector3 position)
        {
            // El CharacterController ignora cambios de posición mientras está activo.
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
            _verticalSpeed = 0f;
        }

        // -------------------------------------------------------------- Entrada

        static Vector3 ReadInput(Keyboard keyboard)
        {
            if (keyboard == null) return Vector3.zero;

            float x = 0f, z = 0f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) z += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) z -= 1f;
            return new Vector3(x, 0f, z);
        }

        static float CameraYaw()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.eulerAngles.y : 0f;
        }
    }
}

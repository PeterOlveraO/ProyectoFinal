using Game.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Jugador TEMPORAL: esfera de 1 unidad con CharacterController.
    ///   - WASD / flechas (o el joystick táctil en móvil), relativo al ángulo Y de la cámara activa.
    ///   - Gravedad. stepOffset 0.55 sube un nivel (0.5) pero no dos (1.0).
    ///   - R, o caer por debajo de respawnHeight, lo devuelve al bloque central.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class PlayerSphereController : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float gravity = 20f;
        [SerializeField] float respawnHeight = -10f;

        [Tooltip("Joystick táctil (móvil). Opcional: sin él solo se usa el teclado.")]
        [SerializeField] VirtualJoystick joystick;

        CharacterController _controller;
        float _verticalSpeed;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        void Start()
        {
            var terrain = WorldTerrain.Instance;
            if (terrain != null && terrain.Player == null) terrain.Player = transform;
            Respawn();
        }

        void Update()
        {
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

            Vector3 velocity = move * moveSpeed + Vector3.up * _verticalSpeed;
            _controller.Move(velocity * Time.deltaTime);
        }

        /// <summary>Coloca la esfera apoyada sobre el bloque central del mundo.</summary>
        public void Respawn()
        {
            var terrain = WorldTerrain.Instance;
            if (terrain == null || terrain.Map == null) return;

            // El CharacterController ignora cambios de posición mientras está activo.
            _controller.enabled = false;
            float halfHeight = _controller.height * 0.5f;
            transform.position = terrain.SpawnPosition +
                                 Vector3.up * (halfHeight - _controller.center.y + _controller.skinWidth);
            _controller.enabled = true;
            _verticalSpeed = 0f;
        }

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

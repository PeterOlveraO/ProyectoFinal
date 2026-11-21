using Game.Combat;
using Game.Core;
using Game.Rounds;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Ataque del jugador: el joystick derecho apunta y, mientras se mantiene pulsado,
    /// inicia el patrón equipado cada <see cref="fireInterval"/> segundos en esa dirección.
    ///
    /// En el editor también se puede probar con el ratón: mantener el botón izquierdo
    /// dispara hacia el cursor (útil para verificar el sistema sin móvil).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAttack : MonoBehaviour
    {
        [Tooltip("Joystick derecho de ataque (móvil).")]
        [SerializeField] VirtualJoystick joystick;

        [Tooltip("Segundos entre disparos mientras se mantiene apuntando.")]
        [SerializeField, Min(0.05f)] float fireInterval = 1f;

        [SerializeField, Min(1f)] float projectileSpeed = 15f;

        [SerializeField, Min(1f)] float range = 18f;

        [Tooltip("Vida que quita cada bala.")]
        [SerializeField, Min(1)] int damage = 2;

        [Range(0.05f, 0.9f)] [SerializeField] float aimDeadZone = 0.28f;

        [SerializeField] float muzzleHeight = 0.62f;

        [SerializeField] Color projectileColor = new Color(1f, 0.80f, 0.16f, 1f);

        float _timer;
        Vector3 _lastAim = Vector3.forward;
        int _burstRemaining;
        float _burstTimer;
        Vector3 _burstAim;
        int _burstDamage;

        public Vector3 LastAimDirection => _lastAim;
        public int Damage => (RoundManager.Instance != null && RoundManager.Instance.HasRunPokemon ?
            RoundManager.Instance.CombatStats.damage : damage) +
            (RoundManager.Instance != null && RoundManager.Instance.Drinks != null &&
             RoundManager.Instance.Drinks.Has(DrinkType.Power) ? 3 : 0);

        public void ResetRun()
        {
            CancelPendingShots();
            _timer = fireInterval;
            _lastAim = Vector3.forward;
        }

        public void CancelPendingShots()
        {
            _burstRemaining = 0;
            _burstTimer = 0f;
        }

        void OnDisable() => CancelPendingShots();

        /// <summary>Asigna el joystick de ataque desde el bootstrap de la escena.</summary>
        public void SetJoystick(VirtualJoystick value) => joystick = value;

        void Update()
        {
            Vector3 worldDirection = Vector3.zero;
            bool hasAim = GameFlow.Playing && TryReadAim(out worldDirection);
            TickAttack(Time.deltaTime, hasAim, worldDirection);
        }

        // Explicit delta keeps burst timing testable without coroutines or delayed callbacks.
        void TickAttack(float deltaTime, bool hasAim, Vector3 worldDirection)
        {
            var round = RoundManager.Instance;
            if (!GameFlow.Playing || (round != null && !round.IsRunActive))
            {
                // Al volver a jugar, el primer toque dispara de inmediato.
                CancelPendingShots();
                _timer = fireInterval;
                return;
            }

            var player = PlayerSphereController.Instance;
            if (player != null && !player.IsAlive)
            {
                CancelPendingShots();
                _timer = fireInterval;
                return;
            }

            if (!hasAim)
            {
                CancelPendingShots();
                _timer = fireInterval;
                return;
            }

            _lastAim = worldDirection;

            _timer += deltaTime;
            if (_burstRemaining > 0)
            {
                _burstTimer -= deltaTime;
                if (_burstTimer <= 0f)
                {
                    // At most one pulse per frame: never catch up with a large mobile-frame burst.
                    FireProjectile(_burstAim, _burstDamage, ShotType.Burst);
                    _burstRemaining--;
                    _burstTimer = ShotDefinitions.BurstPulseInterval;
                }
                return;
            }
            var drinks = round != null ? round.Drinks : null;
            float interval = drinks != null && drinks.Has(DrinkType.RapidFire) ? fireInterval * .7f : fireInterval;
            if (_timer < interval) return;

            _timer = 0f;
            Fire();
        }

        void Fire()
        {
            var round = RoundManager.Instance;
            var type = round != null ? round.Shots.Equipped : ShotType.Basic;
            int shotDamage = Damage;
            if (type == ShotType.Fan)
            {
                for (int i = 0; i < 3; i++)
                    FireProjectile(ShotDefinitions.FanDirection(_lastAim, i), shotDamage, type);
                return;
            }
            FireProjectile(_lastAim, shotDamage, type);
            if (type != ShotType.Burst) return;
            _burstAim = _lastAim;
            _burstDamage = shotDamage;
            _burstRemaining = 2;
            _burstTimer = ShotDefinitions.BurstPulseInterval;
        }

        void FireProjectile(Vector3 direction, int shotDamage, ShotType type)
        {
            Vector3 origin = transform.position + Vector3.up * muzzleHeight + direction * .55f;
            var round = RoundManager.Instance;
            var species = round != null ? round.RunSpecies : null;
            var element = PokemonElementResolver.Resolve(species != null ? species.id : null);
            AttackProjectile.Spawn(origin, direction, projectileSpeed, range, shotDamage,
                type == ShotType.Basic ? projectileColor : ShotDefinitions.Color(type),
                type == ShotType.Piercing ? ShotDefinitions.MaxPiercingTargets : 1, element);
            HitBurst.SpawnMuzzle(origin, element);
            Game.Audio.GameAudio.PlayShot(element);
        }

        bool TryReadAim(out Vector3 worldDirection)
        {
            worldDirection = Vector3.zero;

            if (joystick != null)
            {
                Vector2 stick = joystick.Value;
                if (stick.magnitude >= aimDeadZone)
                {
                    worldDirection = Quaternion.Euler(0f, CameraYaw(), 0f) *
                                     new Vector3(stick.x, 0f, stick.y);
                    if (worldDirection.sqrMagnitude > 0.0001f)
                    {
                        worldDirection.Normalize();
                        return true;
                    }
                }
            }

            // Nunca interpretar un toque o un ratón emulado como apuntado en móvil.
            // En equipos táctiles de escritorio tampoco se convierte un dedo en cursor.
            if (!CanUseMouseAim(Application.isMobilePlatform, HasActiveTouch())) return false;

            // Alternativa de escritorio: disparar hacia el cursor.
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed) return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;

            var camera = Camera.main;
            if (camera == null) return false;

            var plane = new Plane(Vector3.up, transform.position + Vector3.up * 0.5f);
            var ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!plane.Raycast(ray, out float distance)) return false;

            Vector3 toCursor = ray.GetPoint(distance) - transform.position;
            toCursor.y = 0f;
            if (toCursor.sqrMagnitude < 0.04f) return false;

            worldDirection = toCursor.normalized;
            return true;
        }

        static bool CanUseMouseAim(bool isMobile, bool hasActiveTouch) => !isMobile && !hasActiveTouch;

        static bool HasActiveTouch()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null) return false;
            foreach (var touch in touchscreen.touches)
                if (touch.press.isPressed) return true;
            return false;
        }

        static float CameraYaw()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.eulerAngles.y : 0f;
        }
    }
}

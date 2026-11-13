using Game.Combat;
using Game.Core;
using Game.Rounds;
using Game.Upgrades;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Ataque del jugador: el joystick derecho apunta y, mientras se mantiene pulsado,
    /// lanza una bala cada <see cref="fireInterval"/> segundos en esa dirección.
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
        ShotUpgradeStations _upgrades;
        Vector3 _lastAim = Vector3.forward;

        public Vector3 LastAimDirection => _lastAim;
        public int Damage => damage + (RoundManager.Instance != null ? RoundManager.Instance.EVLevel(1) : 0) +
            (RoundManager.Instance != null && RoundManager.Instance.GetComponent<DrinkStations>() != null &&
             RoundManager.Instance.GetComponent<DrinkStations>().Has(DrinkType.Power) ? 3 : 0);

        /// <summary>Asigna el joystick de ataque desde el bootstrap de la escena.</summary>
        public void SetJoystick(VirtualJoystick value) => joystick = value;

        void Awake()
        {
            _upgrades = GetComponent<ShotUpgradeStations>();
            if (_upgrades == null) _upgrades = gameObject.AddComponent<ShotUpgradeStations>();
        }

        void Update()
        {
            if (GameFlow.Blocked || GameFlow.Over)
            {
                // Al volver a jugar, el primer toque dispara de inmediato.
                _timer = fireInterval;
                return;
            }

            var player = PlayerSphereController.Instance;
            if (player != null && !player.IsAlive)
            {
                _timer = fireInterval;
                return;
            }

            if (!TryReadAim(out Vector3 worldDirection))
            {
                _timer = fireInterval;
                return;
            }

            _lastAim = worldDirection;

            _timer += Time.deltaTime;
            var drinks = RoundManager.Instance != null ? RoundManager.Instance.GetComponent<DrinkStations>() : null;
            float interval = drinks != null && drinks.Has(DrinkType.RapidFire) ? fireInterval * .7f : fireInterval;
            if (_upgrades.Has(ShotUpgrade.Rapid)) interval *= .7f;
            if (_timer < interval) return;

            _timer = 0f;
            Fire();
        }

        void Fire()
        {
            Vector3 origin = transform.position + Vector3.up * muzzleHeight + _lastAim * 0.55f;
            float speed = projectileSpeed;
            float distance = range;
            int power = Damage + (_upgrades.Has(ShotUpgrade.Power) ? 3 : 0);
            int pierce = _upgrades.Has(ShotUpgrade.Pierce) ? 2 : 0;
            bool area = _upgrades.Has(ShotUpgrade.Area);
            if (_upgrades.Has(ShotUpgrade.Triple))
            {
                int sideDamage = Mathf.Max(1, Mathf.CeilToInt(power * .6f));
                AttackProjectile.Spawn(origin, _lastAim, speed, distance, power, projectileColor, pierce, area);
                AttackProjectile.Spawn(origin, Quaternion.Euler(0, -15, 0) * _lastAim, speed, distance, sideDamage, projectileColor, pierce, area);
                AttackProjectile.Spawn(origin, Quaternion.Euler(0, 15, 0) * _lastAim, speed, distance, sideDamage, projectileColor, pierce, area);
            }
            else if (_upgrades.Has(ShotUpgrade.Double))
            {
                AttackProjectile.Spawn(origin, Quaternion.Euler(0, -7, 0) * _lastAim, speed, distance, power, projectileColor, pierce, area);
                AttackProjectile.Spawn(origin, Quaternion.Euler(0, 7, 0) * _lastAim, speed, distance, power, projectileColor, pierce, area);
            }
            else AttackProjectile.Spawn(origin, _lastAim, speed, distance, power, projectileColor, pierce, area);
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

        static float CameraYaw()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.eulerAngles.y : 0f;
        }
    }
}

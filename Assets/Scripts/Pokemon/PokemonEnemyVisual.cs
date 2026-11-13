using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>
    /// Pokémon que sustituye por completo al antiguo prisma triangular. Es el único visual
    /// del enemigo: un sprite billboard que se apoya en el suelo y conserva siempre el mismo
    /// Pokémon asignado.
    ///
    /// Elige la vista del atlas según la dirección de movimiento relativa a la cámara.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PokemonEnemyVisual : MonoBehaviour
    {
        [Tooltip("Altura del Pokémon en unidades de mundo.")]
        [SerializeField, Min(0.2f)] float height = 1.35f;

        [Tooltip("Altura del centro del sprite sobre la base (los pies quedan en el suelo).")]
        [SerializeField] float centerHeight = 0.675f;

        [Tooltip("Desplazamiento hacia la cámara para separar el sprite del terreno.")]
        [SerializeField] float cameraOffset = 0.2f;

        [Tooltip("Umbral de movimiento lateral para espejar el sprite.")]
        [SerializeField, Min(0f)] float facingThreshold = 0.05f;

        SpriteRenderer _renderer;
        Transform _visual;
        Sprite _identity;
        Vector3 _lastPosition;
        string _direction = "front";

        /// <summary>Añade el Pokémon a un enemigo recién creado.</summary>
        public static PokemonEnemyVisual Attach(GameObject enemy, Sprite sprite)
        {
            if (sprite == null) return null;

            var visual = enemy.GetComponent<PokemonEnemyVisual>();
            if (visual == null) visual = enemy.AddComponent<PokemonEnemyVisual>();
            visual.Build(sprite);
            return visual;
        }

        void Build(Sprite sprite)
        {
            _identity = sprite;
            _lastPosition = transform.position;
            _renderer = PokemonVisuals.CreateBillboard(transform, "Pokemon " + sprite.name,
                                                       sprite, height, true);
            _visual = _renderer.transform;
            UpdatePosition();

            var enemy = GetComponent<Game.Enemies.EnemyTriangle>();
            if (enemy != null) enemy.SetVisualRenderer(_renderer);
        }

        void LateUpdate()
        {
            UpdatePosition();
            UpdateFacing();
        }

        void UpdatePosition()
        {
            if (_visual == null) return;

            var camera = Camera.main;
            Vector3 offset = Vector3.up * centerHeight;
            if (camera != null)
            {
                Vector3 forward = camera.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                    offset += forward.normalized * cameraOffset;
            }

            _visual.position = transform.position + offset;
        }

        /// <summary>Voltea el sprite a izquierda/derecha según hacia dónde camina el enemigo.</summary>
        void UpdateFacing()
        {
            if (_renderer == null) return;

            var camera = Camera.main;
            if (camera == null) return;

            Vector3 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.000001f)
            {
                _renderer.sprite = PokemonSpriteLibrary.Frame(_identity, _direction, 0);
                return;
            }
            float lateral = Vector3.Dot(delta, camera.transform.right);
            Vector3 forward = camera.transform.forward; forward.y = 0f; forward.Normalize();
            float depth = Vector3.Dot(delta, forward);
            if (Mathf.Abs(lateral) > Mathf.Abs(depth))
            {
                _direction = "side";
                if (Mathf.Abs(lateral) > facingThreshold * Time.deltaTime) _renderer.flipX = lateral < 0f;
            }
            else { _direction = depth > 0f ? "back" : "front"; _renderer.flipX = false; }
            _renderer.sprite = PokemonSpriteLibrary.Frame(_identity, _direction, (int)(Time.time * 6f) % 2);
        }
    }
}

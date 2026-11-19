using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>
    /// Pokémon que sustituye por completo al antiguo prisma triangular. Es el único visual
    /// del enemigo: un sprite billboard que se apoya en el suelo y conserva siempre el mismo
    /// Pokémon asignado.
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

        SpriteRenderer _renderer;
        Transform _visual;
        PokemonVisuals.DirectionalAnimation _animation;
        Vector3 _lastPosition;

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
            // Reattachment/reuse must replace the old billboard, not stack boss and normal art.
            if (_visual != null)
            {
                _visual.gameObject.SetActive(false);
                Destroy(_visual.gameObject);
            }
            _renderer = PokemonVisuals.CreateBillboard(transform, "Pokemon " + sprite.name,
                                                       sprite, height, true);
            _visual = _renderer.transform;
            _animation = new PokemonVisuals.DirectionalAnimation(_renderer, sprite);
            _lastPosition = transform.position;
            UpdatePosition();

            var enemy = GetComponent<Game.Enemies.EnemyTriangle>();
            if (enemy != null) enemy.SetVisualRenderer(_renderer);
        }

        void LateUpdate()
        {
            UpdatePosition();
            Vector3 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;
            if (_animation != null) _animation.Update(delta, Camera.main, Time.deltaTime);
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

    }
}

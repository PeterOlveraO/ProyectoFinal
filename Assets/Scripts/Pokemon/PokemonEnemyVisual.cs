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
        public const float NormalHeight = 1.35f;
        public const float NormalMaximumWidth = 2.25f;

        [Tooltip("Altura visible máxima del Pokémon en unidades de mundo.")]
        [SerializeField, Min(0.2f)] float height = 1.35f;

        [Tooltip("Anchura visible máxima; se conserva la relación de aspecto.")]
        [SerializeField, Min(0.2f)] float maximumWidth = 2.25f;

        [Tooltip("Desplazamiento hacia la cámara para separar el sprite del terreno.")]
        [SerializeField] float cameraOffset = 0.2f;

        SpriteRenderer _renderer;
        Transform _visual;
        PokemonBillboardLayout _layout;
        PokemonVisuals.DirectionalAnimation _animation;
        Sprite _source;
        Vector3 _lastPosition;

        public float VisibleHeight => height;

        /// <summary>Añade el Pokémon a un enemigo recién creado.</summary>
        public static PokemonEnemyVisual Attach(GameObject enemy, Sprite sprite)
        {
            if (sprite == null) return null;

            var visual = enemy.GetComponent<PokemonEnemyVisual>();
            if (visual == null) visual = enemy.AddComponent<PokemonEnemyVisual>();
            visual._source = sprite;
            visual.height = PokemonPhysicalSize.HeightFor(sprite);
            visual.maximumWidth = PokemonPhysicalSize.WidthFor(sprite, visual.height);
            visual.Build(sprite);
            return visual;
        }

        /// <summary>
        /// Optional visual-only profile, applied immediately.
        /// Call after Attach/InitializeRound; actor transforms and combat values are untouched.
        /// </summary>
        public void SetVisualProfile(float targetHeight, float maxWidth)
        {
            height = Mathf.Max(0.2f, targetHeight);
            maximumWidth = Mathf.Max(0.2f, maxWidth);
            if (_layout == null) return;
            _layout.SetProfile(height, maximumWidth);
            Game.Rendering.GroundContactShadow.Attach(transform, height * .65f);
            UpdatePosition();
        }

        // Bosses use the same species measurements; combat rank does not change physical size.
        public void ApplySpeciesScale()
        {
            float speciesHeight = PokemonPhysicalSize.HeightFor(_source);
            SetVisualProfile(speciesHeight, PokemonPhysicalSize.WidthFor(_source, speciesHeight));
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
                                                       sprite, height, true, maximumWidth, 0f);
            _visual = _renderer.transform;
            _layout = _renderer.GetComponent<PokemonBillboardLayout>();
            _animation = new PokemonVisuals.DirectionalAnimation(_renderer, sprite);
            _lastPosition = transform.position;
            UpdatePosition();

            var enemy = GetComponent<Game.Enemies.EnemyTriangle>();
            if (enemy != null) enemy.SetVisualRenderer(_renderer);
        }

        void LateUpdate()
        {
            Vector3 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;
            if (_animation != null) _animation.Update(delta, Camera.main, Time.deltaTime);
            UpdatePosition();
        }

        void UpdatePosition()
        {
            if (_visual == null) return;

            var camera = Camera.main;
            Vector3 offset = Vector3.zero;
            if (camera != null)
            {
                Vector3 forward = camera.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                    offset += forward.normalized * cameraOffset;
            }

            _visual.position = transform.TransformPoint(Vector3.up * _layout.SupportHeight) + offset;
        }

    }
}

using Game.Player;
using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>
    /// Visual del jugador: únicamente un Pokémon (sprite 2D). La esfera 3D ya no se usa.
    /// El CharacterController y toda la lógica siguen intactos: solo existe el sprite.
    ///
    /// El Pokémon asignado es fijo durante toda la partida. La única variante es el espejado
    /// horizontal según la dirección en la que camina.
    /// </summary>
    [DefaultExecutionOrder(5)]
    [DisallowMultipleComponent]
    public sealed class PlayerPokemonVisual : MonoBehaviour
    {
        [Tooltip("Altura del Pokémon en unidades de mundo.")]
        [SerializeField, Min(0.2f)] float height = 1.65f;

        [Tooltip("Umbral de movimiento lateral para espejar el sprite.")]
        [SerializeField, Min(0f)] float facingThreshold = 0.01f;

        SpriteRenderer _renderer;
        PlayerSphereController _controller;
        Color _baseColor = Color.white;
        Vector3 _lastPosition;
        bool _hasLastPosition;

        void Start()
        {
            var sprite = PokemonCollection.GetSprite(PokemonCollection.Selected);
            PokemonSpriteLibrary.SetPlayerIndex(PokemonCollection.Selected);
            if (sprite == null) return;

            _renderer = PokemonVisuals.CreateBillboard(transform, "Pokemon Visual", sprite, height, false);
            _baseColor = _renderer.color;

            // El pivote del sprite está centrado: los pies quedan en -0.5 (base del CharacterController).
            var visual = _renderer.transform;
            visual.localPosition = new Vector3(0f, -0.5f + height * 0.5f, 0f);

            _controller = GetComponent<PlayerSphereController>();
            _lastPosition = transform.position;
            _hasLastPosition = true;
        }

        void Update()
        {
            if (_renderer == null) return;

            var selected = PokemonCollection.Selected;
            if (_renderer.sprite != PokemonCollection.GetSprite(selected))
            {
                _renderer.sprite = PokemonCollection.GetSprite(selected);
                PokemonSpriteLibrary.SetPlayerIndex(selected);
            }

            UpdateFacing();

            bool invincible = _controller != null && _controller.IsInvincible;
            _renderer.color = invincible ? new Color(1f, 0.85f, 0.2f, 1f) : _baseColor;
        }

        /// <summary>Voltea el sprite a izquierda/derecha según la dirección de caminar.</summary>
        void UpdateFacing()
        {
            Vector3 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;

            if (!_hasLastPosition || delta.sqrMagnitude < 0.000001f) return;

            var camera = Camera.main;
            if (camera == null) return;

            float lateral = Vector3.Dot(new Vector3(delta.x, 0f, delta.z), camera.transform.right);
            if (Mathf.Abs(lateral) > facingThreshold) _renderer.flipX = lateral < 0f;
        }
    }
}

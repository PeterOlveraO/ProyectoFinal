using Game.Player;
using Game.Rounds;
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
        RoundManager _rounds;
        Color _baseColor = Color.white;
        Vector3 _lastPosition;
        bool _hasLastPosition;

        void Start()
        {
            _controller = GetComponent<PlayerSphereController>();
            _lastPosition = transform.position;
            _hasLastPosition = true;
            SubscribeRounds();
            RefreshPokemon();
        }

        void OnEnable()
        {
            PokemonCollection.Changed += RefreshPokemon;
            SubscribeRounds();
        }
        void SubscribeRounds()
        {
            if (_rounds != null) return;
            _rounds = RoundManager.Instance;
            if (_rounds != null) _rounds.Changed += RefreshPokemon;
        }
        void OnDisable()
        {
            PokemonCollection.Changed -= RefreshPokemon;
            if (_rounds != null) _rounds.Changed -= RefreshPokemon;
            _rounds = null;
        }

        void RefreshPokemon()
        {
            // Combat and visuals must share the immutable instance captured at run start.
            var rounds = RoundManager.Instance;
            var equipped = rounds != null && rounds.IsRunActive ? rounds.RunPokemon : PokemonCollection.Equipped;
            var sprite = PokemonCollection.GetSprite(equipped);
            if (sprite == null)
            {
                if (_renderer != null) _renderer.enabled = false;
                return;
            }
            if (_renderer == null)
            {
                _renderer = PokemonVisuals.CreateBillboard(transform, "Pokemon Visual", sprite, height, false);
                _baseColor = _renderer.color;
                _renderer.transform.localPosition = new Vector3(0f, -0.5f + height * 0.5f, 0f);
            }
            _renderer.sprite = sprite;
            _renderer.enabled = true;
            PokemonSpriteLibrary.SetPlayerSprite(sprite);
        }

        void Update()
        {
            if (_renderer == null) return;

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

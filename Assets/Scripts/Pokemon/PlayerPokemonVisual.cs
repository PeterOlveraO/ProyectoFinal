using Game.Player;
using Game.Rounds;
using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>
    /// Visual del jugador: únicamente un Pokémon (sprite 2D). La esfera 3D ya no se usa.
    /// El CharacterController y toda la lógica siguen intactos: solo existe el sprite.
    ///
    /// El Pokémon asignado es fijo durante toda la partida.
    /// </summary>
    [DefaultExecutionOrder(5)]
    [DisallowMultipleComponent]
    public sealed class PlayerPokemonVisual : MonoBehaviour
    {
        [Tooltip("Altura del Pokémon en unidades de mundo.")]
        [SerializeField, Min(0.2f)] float height = 1.65f;

        SpriteRenderer _renderer;
        PokemonVisuals.DirectionalAnimation _animation;
        Sprite _source;
        PlayerSphereController _controller;
        RoundManager _rounds;
        Color _baseColor = Color.white;
        Vector3 _lastPosition;

        void Start()
        {
            _controller = GetComponent<PlayerSphereController>();
            _lastPosition = transform.position;
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
            if (_source != sprite)
            {
                _source = sprite;
                float spriteHeight = sprite.rect.height / sprite.pixelsPerUnit;
                float scale = spriteHeight > 0.0001f ? height / spriteHeight : 1f;
                _renderer.transform.localScale = new Vector3(scale, scale, 1f);
                _animation = new PokemonVisuals.DirectionalAnimation(_renderer, sprite);
            }
            _renderer.enabled = true;
            PokemonSpriteLibrary.SetPlayerSprite(sprite);
        }

        void Update()
        {
            if (_renderer == null) return;

            bool invincible = _controller != null && _controller.IsInvincible;
            _renderer.color = invincible ? new Color(1f, 0.85f, 0.2f, 1f) : _baseColor;
        }

        void LateUpdate()
        {
            Vector3 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;
            if (_animation != null) _animation.Update(delta, Camera.main, Time.deltaTime);
        }
    }
}

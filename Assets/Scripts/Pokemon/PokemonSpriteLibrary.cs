using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>
    /// Reparte los sprites de Pokémon extraídos de <c>Pokemones.png</c>
    /// (Assets/Resources/PokemonSprites).
    ///
    /// - El jugador recibe uno al azar.
    /// - Cada triángulo enemigo recibe uno <b>distinto</b>: se avanza un cursor por la lista
    ///   barajada y solo se repite cuando se acaban los sprites disponibles.
    /// </summary>
    public static class PokemonSpriteLibrary
    {
        const string ResourcePath = "PokemonSprites";

        static Sprite[] _pool;
        static int _cursor;
        static int _playerIndex = -1;

        public static int Count
        {
            get { EnsureLoaded(); return _pool.Length; }
        }

        public static Sprite At(int index)
        {
            EnsureLoaded();
            return index >= 0 && index < _pool.Length ? _pool[index] : null;
        }

        public static void SetPlayerIndex(int index) => _playerIndex = index;

        /// <summary>Sprite aleatorio para el personaje principal.</summary>
        public static Sprite RandomForPlayer()
        {
            EnsureLoaded();
            if (_pool.Length == 0) return null;

            _playerIndex = Random.Range(0, _pool.Length);
            return _pool[_playerIndex];
        }

        /// <summary>Siguiente sprite distinto para un triángulo enemigo.</summary>
        public static Sprite NextForEnemy()
        {
            EnsureLoaded();
            if (_pool.Length == 0) return null;

            if (_cursor >= _pool.Length) _cursor = 0;

            int index = _cursor++;

            // El jugador conserva su Pokémon: nunca se le asigna a un enemigo.
            if (_pool.Length > 1 && index == _playerIndex)
            {
                if (_cursor >= _pool.Length) _cursor = 0;
                index = _cursor++;
            }

            return _pool[index];
        }

        static void EnsureLoaded()
        {
            if (_pool != null && _pool.Length > 0) return;

            var sprites = Resources.LoadAll<Sprite>(ResourcePath);

            // Red de seguridad: si el importador todavía no marcó las texturas como Sprite,
            // se construyen los sprites en memoria a partir de las texturas.
            if (sprites == null || sprites.Length == 0)
            {
                var textures = Resources.LoadAll<Texture2D>(ResourcePath);
                sprites = new Sprite[textures.Length];
                for (int i = 0; i < textures.Length; i++)
                {
                    var texture = textures[i];
                    texture.filterMode = FilterMode.Point;
                    sprites[i] = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                                               new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
                }
            }

            if (sprites == null || sprites.Length == 0)
            {
                Debug.LogError("[PokemonSpriteLibrary] No hay sprites en Resources/" + ResourcePath);
                _pool = new Sprite[0];
                return;
            }

            _pool = sprites;

            // Orden estable y barajado determinista: misma partida, mismos Pokémon.
            System.Array.Sort(_pool, (a, b) => string.CompareOrdinal(a.name, b.name));
            var rng = new System.Random(20251109);
            for (int i = _pool.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_pool[i], _pool[j]) = (_pool[j], _pool[i]);
            }

            foreach (var sprite in _pool)
                if (sprite != null) sprite.texture.filterMode = FilterMode.Point;

            _cursor = 0;
            _playerIndex = -1;
        }
    }
}

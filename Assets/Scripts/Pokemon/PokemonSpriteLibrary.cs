using System.Collections.Generic;
using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>Catálogos estables; los PNG se generan en el editor desde Pokemones.png.</summary>
    public static class PokemonSpriteLibrary
    {
        public static readonly string[] PlayerNames = { "Pikachu", "Charizard", "Garchomp", "Vaporeon", "Eevee", "Snorlax", "Blastoise", "Dragonite", "Lucario", "Bulbasaur" };
        public static readonly string[] EnemyNames = { "Caterpie", "Metapod", "Butterfree", "Weedle", "Kakuna", "Beedrill", "Pidgey", "Pidgeotto", "Pidgeot", "Rattata", "Raticate", "Spearow", "Fearow", "Ekans", "Arbok", "Raichu", "Sandshrew", "Sandslash", "NidoranF", "Nidorina" };
        public static readonly string[] BossNames = { "Articuno", "Zapdos", "Moltres" };
        const string Path = "PokemonAtlas/";
        static int _cursor;
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        public static int Count => PlayerNames.Length;
        public static int EnemyCount => EnemyNames.Length;
        public static int BossCount => BossNames.Length;
        public static Sprite At(int index) => Load(PlayerNames, index);
        public static Sprite EnemyAt(int index) => Load(EnemyNames, index);
        public static Sprite BossAt(int index) => Load(BossNames, index);
        public static void SetPlayerIndex(int index) { }
        public static Sprite RandomForPlayer() => At(UnityEngine.Random.Range(0, Count));
        public static Sprite NextForEnemy() => EnemyAt(_cursor++ % EnemyCount);

        static Sprite Load(string[] names, int index)
        {
            return index >= 0 && index < names.Length ? LoadFrame(names[index] + "_front_0") : null;
        }

        static Sprite LoadFrame(string key)
        {
            if (!Cache.TryGetValue(key, out var sprite) || sprite == null)
            {
                sprite = Resources.Load<Sprite>(Path + key);
                if (sprite != null) Cache[key] = sprite;
            }
            return sprite;
        }

        /// <summary>Selecciona una vista y fotograma usando la identidad del sprite inicial.</summary>
        public static Sprite Frame(Sprite identity, string direction, int frame)
        {
            if (identity == null) return null;
            int separator = identity.name.IndexOf('_');
            string name = separator < 0 ? identity.name : identity.name.Substring(0, separator);
            return LoadFrame(name + "_" + direction + "_" + Mathf.Clamp(frame, 0, 1)) ?? identity;
        }
    }
}

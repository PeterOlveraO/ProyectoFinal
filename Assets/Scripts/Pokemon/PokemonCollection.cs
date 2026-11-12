using Game.Economy;
using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>Pokémon comprados con monedas globales. El primero es gratuito.</summary>
    public static class PokemonCollection
    {
        const string SelectedKey = "pketer.pokemon.selected";
        const string OwnedPrefix = "pketer.pokemon.owned.";
        public const int Price = 50;

        public static int Count => PokemonSpriteLibrary.Count;
        public static int Selected
        {
            get
            {
                int value = Mathf.Clamp(PlayerPrefs.GetInt(SelectedKey, 0), 0, Mathf.Max(0, Count - 1));
                return IsOwned(value) ? value : 0;
            }
        }

        public static Sprite GetSprite(int index) => PokemonSpriteLibrary.At(index);
        public static string GetName(int index)
        {
            var sprite = GetSprite(index);
            return sprite != null ? sprite.name : "Pokémon";
        }

        public static bool IsOwned(int index) => index >= 0 && index < Count &&
            (index == 0 || PlayerPrefs.GetInt(OwnedPrefix + index, 0) == 1);

        public static bool Select(int index)
        {
            if (!IsOwned(index)) return false;
            PlayerPrefs.SetInt(SelectedKey, index);
            PlayerPrefs.Save();
            return true;
        }

        public static bool Buy(int index)
        {
            if (index <= 0 || index >= Count || IsOwned(index) || !PlayerEconomy.SpendCoins(Price)) return false;
            PlayerPrefs.SetInt(OwnedPrefix + index, 1);
            Select(index);
            return true;
        }
    }
}

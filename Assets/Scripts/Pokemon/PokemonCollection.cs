using Game.Economy;
using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>Pokémon comprados con monedas globales. El primero es gratuito.</summary>
    public static class PokemonCollection
    {
        // La colección anterior tenía otro orden: no aplicar sus índices a especies distintas.
        const string SelectedKey = "pketer.pokemon.v2.selected";
        const string OwnedPrefix = "pketer.pokemon.v2.owned.";
        const string MigrationKey = "pketer.pokemon.v2.migrated";
        const string RefundMigrationKey = "pketer.pokemon.v2.legacy_refunded";
        const int LegacyCount = 163;
        const int LegacyPrice = 50;
        // R1 ~21 monedas; diez rondas ~615. Un Pokémon cuesta varias rondas.
        public const int Price = 180;

        public static int Count => PokemonSpriteLibrary.Count;
        public static int Selected
        {
            get
            {
                MigrateLegacyCollection();
                int value = Mathf.Clamp(PlayerPrefs.GetInt(SelectedKey, 0), 0, Mathf.Max(0, Count - 1));
                return IsOwned(value) ? value : 0;
            }
        }

        public static Sprite GetSprite(int index) => PokemonSpriteLibrary.At(index);
        public static string GetName(int index)
        {
            return index >= 0 && index < Count ? PokemonSpriteLibrary.PlayerNames[index] : "Pokémon";
        }

        public static bool IsOwned(int index)
        {
            MigrateLegacyCollection();
            return index >= 0 && index < Count &&
                   (index == 0 || PlayerPrefs.GetInt(OwnedPrefix + index, 0) == 1);
        }

        static bool MigrateLegacyCollection(bool save = true)
        {
            bool migrate = PlayerPrefs.GetInt(MigrationKey, 0) == 0;
            bool refund = PlayerPrefs.GetInt(RefundMigrationKey, 0) == 0;
            if (!migrate && !refund) return false;
            // Solo se conservan compras cuyo Pokémon sigue en el catálogo.
            if (migrate)
            {
                if (PlayerPrefs.GetInt("pketer.pokemon.owned.5", 0) == 1)
                    PlayerPrefs.SetInt(OwnedPrefix + 1, 1); // Charizard
                if (PlayerPrefs.GetInt("pketer.pokemon.owned.8", 0) == 1)
                    PlayerPrefs.SetInt(OwnedPrefix + 6, 1); // Blastoise
                int previous = PlayerPrefs.GetInt("pketer.pokemon.selected", 0);
                int selected = previous == 5 ? 1 : previous == 8 ? 6 : 0;
                if (selected == 0 || PlayerPrefs.GetInt(OwnedPrefix + selected, 0) == 1)
                    PlayerPrefs.SetInt(SelectedKey, selected);
                PlayerPrefs.SetInt(MigrationKey, 1);
            }
            if (refund)
            {
                int count = 0;
                for (int i = 1; i < LegacyCount; i++)
                    if (i != 5 && i != 8 && PlayerPrefs.GetInt("pketer.pokemon.owned." + i, 0) == 1) count++;
                PlayerEconomy.AddCoinsUncommitted(count * LegacyPrice);
                PlayerPrefs.SetInt(RefundMigrationKey, 1);
            }
            if (save)
            {
                PlayerPrefs.Save();
                if (refund) PlayerEconomy.NotifyChanged();
            }
            return true;
        }

        public static bool Select(int index)
        {
            if (!IsOwned(index)) return false;
            PlayerPrefs.SetInt(SelectedKey, index);
            PlayerPrefs.Save();
            return true;
        }

        public static bool Buy(int index)
        {
            bool migrated = MigrateLegacyCollection(false);
            if (index <= 0 || index >= Count || IsOwned(index) || !PlayerEconomy.SpendCoinsUncommitted(Price))
            {
                if (migrated) { PlayerPrefs.Save(); PlayerEconomy.NotifyChanged(); }
                return false;
            }
            PlayerPrefs.SetInt(OwnedPrefix + index, 1);
            PlayerPrefs.SetInt(SelectedKey, index);
            PlayerPrefs.Save();
            PlayerEconomy.NotifyChanged();
            return true;
        }
    }
}

using System;
using UnityEngine;
using Game.Pokemon;

namespace Game.Economy
{
    /// <summary>
    /// Cartera persistente para comprar Pokémon y futuras mejoras permanentes.
    /// Las monedas se ingresan al terminar una partida; los puntos y EVs viven solo en RoundManager.
    /// </summary>
    public static class PlayerEconomy
    {
        static PlayerEconomy() { PokemonCollection.Changed += NotifyChanged; }
        static void NotifyChanged() => Changed?.Invoke();
        const string CoinsKey = "pketer.monedas";
        const string EnemiesKey = "pketer.enemigos";
        const string RoundsKey = "pketer.rondas";
        const string LossesKey = "pketer.derrotas";

        /// <summary>Monedas globales disponibles para el gacha.</summary>
        public static int Coins => PokemonCollection.Progression.Coins;
        public static int TotalLosses => PlayerPrefs.GetInt(LossesKey, 0);

        public static void RegisterLoss()
        {
            PlayerPrefs.SetInt(LossesKey, TotalLosses + 1);
            Flush();
            Changed?.Invoke();
        }
        public static bool SpendCoins(int amount)
        {
            if (amount < 0 || Coins < amount) return false;
            if (!PokemonCollection.Progression.ChangeCoins(-amount)) return false;
            Flush();
            return true;
        }

        /// <summary>Enemigos derrotados en toda la cuenta.</summary>
        public static int TotalEnemiesDefeated => PlayerPrefs.GetInt(EnemiesKey, 0);

        /// <summary>Rondas superadas en toda la cuenta.</summary>
        public static int TotalRoundsWon => PlayerPrefs.GetInt(RoundsKey, 0);

        /// <summary>Se dispara cada vez que cambia la economía global.</summary>
        public static event Action Changed;

        public static bool AddCoins(int amount)
        {
            if (amount == 0) return PokemonCollection.Progression.IsReady;

            return PokemonCollection.Progression.AddCoinsClamped(amount);
        }

        public static void RegisterEnemyDefeated(int count = 1)
        {
            if (count <= 0) return;

            PlayerPrefs.SetInt(EnemiesKey, TotalEnemiesDefeated + count);
            Changed?.Invoke();
        }

        public static void RegisterRoundWon(int count = 1)
        {
            if (count <= 0) return;

            PlayerPrefs.SetInt(RoundsKey, TotalRoundsWon + count);
            Changed?.Invoke();
        }

        /// <summary>Escribe los cambios en disco (al cerrar, pausar o terminar una ronda).</summary>
        public static void Flush() => PlayerPrefs.Save();

        /// <summary>Borra la economía global (útil para probar el gacha desde cero).</summary>
        public static void ResetAll()
        {
            if (!PokemonCollection.Progression.SetCoins(0)) return;
            PlayerPrefs.DeleteKey(CoinsKey);
            PlayerPrefs.DeleteKey(EnemiesKey);
            PlayerPrefs.DeleteKey(RoundsKey);
            PlayerPrefs.DeleteKey(LossesKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}

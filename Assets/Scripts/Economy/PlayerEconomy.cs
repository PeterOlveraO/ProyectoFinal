using System;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>
    /// Cartera persistente para comprar Pokémon y futuras mejoras permanentes.
    /// Las monedas se ingresan al terminar una partida; los puntos y EVs viven solo en RoundManager.
    /// </summary>
    public static class PlayerEconomy
    {
        const string CoinsKey = "pketer.monedas";
        const string EnemiesKey = "pketer.enemigos";
        const string RoundsKey = "pketer.rondas";
        const string LossesKey = "pketer.derrotas";
        const string PendingRunCoinsKey = "pketer.monedas.pendientes";

        /// <summary>Monedas globales disponibles para el gacha.</summary>
        public static int Coins => PlayerPrefs.GetInt(CoinsKey, 0);
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
            PlayerPrefs.SetInt(CoinsKey, Coins - amount);
            Flush();
            Changed?.Invoke();
            return true;
        }

        // El comprador guarda propiedad, selección y saldo juntos antes de notificar.
        internal static bool SpendCoinsUncommitted(int amount)
        {
            if (amount < 0 || Coins < amount) return false;
            PlayerPrefs.SetInt(CoinsKey, Coins - amount);
            return true;
        }

        internal static void NotifyChanged() => Changed?.Invoke();

        internal static void AddCoinsUncommitted(int amount)
        {
            if (amount != 0) PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, Coins + amount));
        }

        public static void CheckpointRunCoins(int amount)
        {
            PlayerPrefs.SetInt(PendingRunCoinsKey, Mathf.Max(0, amount));
            Flush();
        }

        public static void SettlePendingRunCoins()
        {
            int pending = Mathf.Max(0, PlayerPrefs.GetInt(PendingRunCoinsKey, 0));
            AddCoinsUncommitted(pending);
            PlayerPrefs.DeleteKey(PendingRunCoinsKey);
            Flush();
            if (pending > 0) Changed?.Invoke();
        }

        /// <summary>Enemigos derrotados en toda la cuenta.</summary>
        public static int TotalEnemiesDefeated => PlayerPrefs.GetInt(EnemiesKey, 0);

        /// <summary>Rondas superadas en toda la cuenta.</summary>
        public static int TotalRoundsWon => PlayerPrefs.GetInt(RoundsKey, 0);

        /// <summary>Se dispara cada vez que cambia la economía global.</summary>
        public static event Action Changed;

        public static void AddCoins(int amount)
        {
            if (amount == 0) return;

            PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, Coins + amount));
            Changed?.Invoke();
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
            PlayerPrefs.DeleteKey(CoinsKey);
            PlayerPrefs.DeleteKey(EnemiesKey);
            PlayerPrefs.DeleteKey(RoundsKey);
            PlayerPrefs.DeleteKey(LossesKey);
            PlayerPrefs.DeleteKey(PendingRunCoinsKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}

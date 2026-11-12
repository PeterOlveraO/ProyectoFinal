using System;
using Game.Pokemon;
using UnityEngine;

namespace Game.Economy
{
    /// <summary>
    /// Economía GLOBAL y persistente (sobrevive a la partida). Se usará para comprar
    /// personajes en el gacha: cada enemigo derrotado y cada ronda superada suman
    /// monedas que quedan guardadas con PlayerPrefs.
    ///
    /// La economía interna (los <c>Puntos</c> de la partida) vive en <c>RoundManager</c>
    /// y se reinicia al empezar cada run.
    /// </summary>
    public static class PlayerEconomy
    {
        const string CoinsKey = "pketer.monedas";
        const string EnemiesKey = "pketer.enemigos";
        const string RoundsKey = "pketer.rondas";
        const string SkillsKey = "pketer.habilidad";
        const string LossesKey = "pketer.derrotas";
        const string EVPrefix = "pketer.ev.";

        /// <summary>Monedas globales disponibles para el gacha.</summary>
        public static int Coins => PlayerPrefs.GetInt(CoinsKey, 0);
        public static int SkillPoints => PlayerPrefs.GetInt(SkillsKey, 0);
        public static int TotalLosses => PlayerPrefs.GetInt(LossesKey, 0);

        public static void RegisterLoss()
        {
            PlayerPrefs.SetInt(LossesKey, TotalLosses + 1);
            Flush();
            Changed?.Invoke();
        }
        static string EVKey(int pokemon, int stat) => EVPrefix + pokemon + "." + stat;
        public static int EVLevel(int stat) => stat >= 0 && stat < 4 ?
            PlayerPrefs.GetInt(EVKey(PokemonCollection.Selected, stat), 0) : 0;
        public static int EVCost(int stat) => 1 + EVLevel(stat) / 2;
        public static bool CanBuyEV(int stat) => stat >= 0 && stat < 4 && EVLevel(stat) < 10 && SkillPoints >= EVCost(stat);

        public static bool BuyEV(int stat)
        {
            if (!CanBuyEV(stat)) return false;
            PlayerPrefs.SetInt(SkillsKey, SkillPoints - EVCost(stat));
            PlayerPrefs.SetInt(EVKey(PokemonCollection.Selected, stat), EVLevel(stat) + 1);
            Flush();
            Changed?.Invoke();
            return true;
        }

        public static void AddSkillPoint()
        {
            PlayerPrefs.SetInt(SkillsKey, SkillPoints + 1);
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
            PlayerPrefs.DeleteKey(SkillsKey);
            PlayerPrefs.DeleteKey(LossesKey);
            for (int pokemon = 0; pokemon < PokemonCollection.Count; pokemon++)
                for (int stat = 0; stat < 4; stat++) PlayerPrefs.DeleteKey(EVKey(pokemon, stat));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}

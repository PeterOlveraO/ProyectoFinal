using System;
using System.Collections.Generic;

namespace Game.Pokemon.Progression
{
    public enum Rarity { F, A, S }
    // Row = raised classic stat; column = lowered classic stat.
    public enum Nature
    {
        Hardy, Lonely, Brave, Adamant, Naughty,
        Bold, Docile, Relaxed, Impish, Lax,
        Timid, Hasty, Serious, Jolly, Naive,
        Modest, Mild, Quiet, Bashful, Rash,
        Calm, Gentle, Sassy, Careful, Quirky
    }
    [Serializable]
    public struct StatBlock
    {
        public int health, attack, defense, speed;
        public StatBlock(int health, int attack, int defense, int speed)
        { this.health = health; this.attack = attack; this.defense = defense; this.speed = speed; }
    }
    [Serializable]
    public sealed class SpeciesDefinition
    {
        public string id, name, category, spriteName;
        public StatBlock baseStats;
    }
    [Serializable]
    public sealed class PokemonInstance
    {
        public string instanceId, speciesId;
        public Rarity rarity;
        public StatBlock ivs;
        public Nature nature;
        public int duplicateUpgradeLevel;
        public PokemonInstance Copy() => (PokemonInstance)MemberwiseClone();
    }
    public static class PokemonStats
    {
        // Special attack maps to attack; special defense maps to defense.
        static readonly int[] Mapping = { 0, 1, 2, 0, 1 };
        public static StatBlock Calculate(StatBlock adaptedBase, StatBlock ivs, StatBlock runEV, Nature nature)
        {
            if ((int)nature < 0 || (int)nature >= 25) throw new ArgumentOutOfRangeException(nameof(nature));
            int[] values = { checked(adaptedBase.attack + ivs.attack + runEV.attack),
                checked(adaptedBase.defense + ivs.defense + runEV.defense),
                checked(adaptedBase.speed + ivs.speed + runEV.speed) };
            int up = Mapping[(int)nature / 5], down = Mapping[(int)nature % 5];
            if (up != down)
            {
                values[up] = checked((int)((long)values[up] * 110 / 100));
                values[down] = checked((int)((long)values[down] * 90 / 100));
            }
            return new StatBlock(checked(adaptedBase.health + ivs.health + runEV.health), values[0], values[1], values[2]);
        }
    }
    public sealed class OperationResult
    {
        public bool success;
        public string error;
        public PokemonInstance instance;
        public static OperationResult Fail(string error) => new OperationResult { error = error };
        public static OperationResult Ok(PokemonInstance instance = null) =>
            new OperationResult { success = true, instance = instance?.Copy() };
    }
    [Serializable] public sealed class SpeciesCatalog { public List<SpeciesDefinition> species; }
    [Serializable] public sealed class GachaConfig
    {
        public int capacity = 10;
        public List<GachaPool> pools;
        public int starterF = 95, starterA = 4, starterS = 1;
        public string[] starters;
    }
    [Serializable] public sealed class GachaPool
    {
        public string id;
        public int price, weightF, weightA, weightS;
        public string[] species;
    }
}

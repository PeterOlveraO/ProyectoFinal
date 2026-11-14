using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Pokemon.Progression;
using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>Compatibility bridge; ownership is exclusively instance-based in the versioned save.</summary>
    public static class PokemonCollection
    {
        public const int Price = 50;
        static ProgressionService service;
        static Sprite[] sprites;
        public static ProgressionService Progression
        {
            get
            {
                if (service == null)
                {
                    var catalog = Resources.Load<TextAsset>("PokemonProgression/catalog");
                    var config = Resources.Load<TextAsset>("PokemonProgression/gacha");
                    service = new ProgressionService(
                        new AtomicProgressionStore(Path.Combine(Application.persistentDataPath, "pokemon-progression-v1.json")),
                        Parse<SpeciesCatalog>(catalog),
                        Parse<GachaConfig>(config),
                        PlayerPrefs.GetInt("pketer.monedas", 0));
                    service.Changed += () => Changed?.Invoke();
                }
                return service;
            }
        }
        static T Parse<T>(TextAsset asset) where T : class
        {
            if (asset == null) return null;
            try { return JsonUtility.FromJson<T>(asset.text); }
            catch (Exception ex) { Debug.LogException(ex); return null; }
        }
        public static event Action Changed;
        public static IReadOnlyList<PokemonInstance> Inventory => Progression.Inventory;
        public static IReadOnlyList<SpeciesDefinition> Catalog => Progression.Catalog;
        public static PokemonInstance Equipped => Progression.Equipped;
        public static bool NeedsStarter => Progression.NeedsStarter;
        public static int InventoryCapacity => Progression.InventoryCapacity;
        public static IReadOnlyList<string> StarterIds => Progression.StarterIds;
        public static GachaConfig Configuration => Progression.Configuration;
        public static IReadOnlyList<GachaPool> GetPools() => Progression.GetPools();
        public static GachaPool GetPool(string id) => Progression.GetPool(id);
        public static OperationResult ChooseStarter(string speciesId) => Progression.ChooseStarter(speciesId);
        public static OperationResult Pull(string poolId, string requestId) => Progression.Pull(poolId, requestId);
        public static OperationResult Equip(string instanceId) => Progression.Equip(instanceId);
        public static OperationResult Release(string instanceId) => Progression.Release(instanceId);
        public static int Count => Catalog.Count;
        public static int Selected
        {
            get
            {
                string id = Equipped?.speciesId;
                for (int i = 0; i < Count; i++) if (Catalog[i].id == id) return i;
                return 0;
            }
        }
        public static Sprite GetSprite(int index)
        {
            if (index < 0 || index >= Count) return null;
            if (sprites == null) sprites = Resources.LoadAll<Sprite>("PokemonSprites");
            string name = Catalog[index].spriteName;
            return sprites.FirstOrDefault(s => s.name == name);
        }
        public static Sprite GetSprite(PokemonInstance instance)
        {
            if (instance == null) return null;
            for (int i = 0; i < Count; i++) if (Catalog[i].id == instance.speciesId) return GetSprite(i);
            return null;
        }
        public static string GetName(int index) => index >= 0 && index < Count ? Catalog[index].name : "Pokémon";
        public static bool IsOwned(int index) => index >= 0 && index < Count && Inventory.Any(p => p.speciesId == Catalog[index].id);
        public static bool Select(int index)
        {
            if (index < 0 || index >= Count) return false;
            var instance = Inventory.FirstOrDefault(p => p.speciesId == Catalog[index].id);
            return instance != null && Equip(instance.instanceId).success;
        }
        // Direct species purchases are intentionally disabled; use Pull with an intent ID.
        public static bool Buy(int index) => false;
    }
}

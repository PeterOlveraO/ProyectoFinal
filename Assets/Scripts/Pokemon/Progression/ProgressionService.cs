using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game.Pokemon.Progression
{
    public interface IProgressionStore
    {
        string Read();
        // Compare-and-swap; implementations must not expose partial writes.
        void Write(string expected, string next);
    }
    public sealed class AtomicProgressionStore : IProgressionStore
    {
        readonly string path;
        public AtomicProgressionStore(string path) { this.path = path; }
        public string Read() => File.Exists(path) ? File.ReadAllText(path) : null;
        public void Write(string expected, string next)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var gate = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                if (Read() != expected) throw new IOException("Save changed in another session; reload required.");
                string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(next);
                    using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                    if (expected == null) File.Move(temp, path);
                    else File.Replace(temp, path, null);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
        }
    }
    [Serializable] public sealed class PullReceipt
    {
        public string requestId, poolId;
        public PokemonInstance instance;
    }
    [Serializable] public sealed class ProgressionSave
    {
        public string format;
        public int version = 1;
        public int coins;
        public bool starterChosen;
        public string equippedId;
        public List<PokemonInstance> inventory = new List<PokemonInstance>();
        public List<PullReceipt> receipts = new List<PullReceipt>();
    }
    /// <summary>One transactional owner for inventory and wallet. Never overwrite a corrupt/unknown save.</summary>
    public sealed class ProgressionService
    {
        public const int Capacity = 10;
        readonly object sync = new object();
        readonly IProgressionStore store;
        readonly SpeciesCatalog catalog;
        readonly GachaConfig config;
        readonly System.Random random;
        ProgressionSave state;
        string serialized;
        public string LastError { get; private set; }
        public event Action Changed;
        public ProgressionService(IProgressionStore store, SpeciesCatalog catalog, GachaConfig config,
            int legacyCoins = 0, System.Random random = null)
        {
            this.store = store;
            this.catalog = catalog == null ? null : JsonUtility.FromJson<SpeciesCatalog>(JsonUtility.ToJson(catalog));
            this.config = config == null ? null : JsonUtility.FromJson<GachaConfig>(JsonUtility.ToJson(config));
            this.random = random ?? new System.Random();
            try
            {
                ValidateConfig();
                serialized = store.Read();
                if (serialized == null)
                {
                    var initial = new ProgressionSave { format = "pokemon-progression", coins = Math.Max(0, legacyCoins) };
                    string json = JsonUtility.ToJson(initial);
                    store.Write(null, json);
                    serialized = json;
                }
                state = JsonUtility.FromJson<ProgressionSave>(serialized);
                ValidateSave(state);
            }
            catch (Exception ex) { state = null; LastError = ex.Message; }
        }
        public bool IsReady => state != null;
        public int InventoryCapacity => config?.capacity ?? Capacity;
        public GachaConfig Configuration => config == null ? null : JsonUtility.FromJson<GachaConfig>(JsonUtility.ToJson(config));
        public IReadOnlyList<GachaPool> GetPools() => (Configuration?.pools ?? new List<GachaPool>()).AsReadOnly();
        public GachaPool GetPool(string id) => GetPools().FirstOrDefault(p => p.id == id);
        public IReadOnlyList<string> StarterIds => Array.AsReadOnly(config?.starters == null ? Array.Empty<string>() : (string[])config.starters.Clone());
        public bool NeedsStarter { get { lock (sync) return state != null && !state.starterChosen; } }
        public int Coins { get { lock (sync) return state?.coins ?? 0; } }
        public IReadOnlyList<SpeciesDefinition> Catalog =>
            (catalog?.species ?? new List<SpeciesDefinition>()).Select(s => new SpeciesDefinition { id = s.id, name = s.name, category = s.category,
                spriteName = s.spriteName, baseStats = s.baseStats }).ToList().AsReadOnly();
        public IReadOnlyList<PokemonInstance> Inventory
        { get { lock (sync) return (state?.inventory.Select(p => p.Copy()).ToList() ?? new List<PokemonInstance>()).AsReadOnly(); } }
        public PokemonInstance Equipped
        { get { lock (sync) return state?.inventory.Find(p => p.instanceId == state.equippedId)?.Copy(); } }
        public OperationResult ChooseStarter(string speciesId)
        {
            lock (sync)
            {
                if (state == null) return OperationResult.Fail("SaveUnavailable");
                if (state.starterChosen) return OperationResult.Fail("StarterAlreadyChosen");
                if (!config.starters.Contains(speciesId)) return OperationResult.Fail("InvalidStarter");
                var next = Clone();
                var instance = Create(speciesId, Roll(config.starterF, config.starterA, config.starterS));
                next.inventory.Add(instance); next.equippedId = instance.instanceId; next.starterChosen = true;
                return Commit(next, instance);
            }
        }
        /// <summary>Reuse requestId for retries/double clicks of ONE user intent; new ID means a new paid pull.</summary>
        public OperationResult Pull(string poolId, string requestId)
        {
            lock (sync)
            {
                if (state == null) return OperationResult.Fail("SaveUnavailable");
                if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128) return OperationResult.Fail("InvalidRequest");
                var receipt = state.receipts.Find(r => r.requestId == requestId);
                if (receipt != null) return receipt.poolId == poolId ? OperationResult.Ok(receipt.instance) : OperationResult.Fail("RequestConflict");
                if (!state.starterChosen) return OperationResult.Fail("StarterRequired");
                var pool = config.pools.Find(p => p.id == poolId);
                if (pool == null) return OperationResult.Fail("InvalidPool");
                if (state.inventory.Count >= InventoryCapacity) return OperationResult.Fail("InventoryFull");
                if (state.coins < pool.price) return OperationResult.Fail("InsufficientCoins");
                var next = Clone();
                var instance = Create(pool.species[random.Next(pool.species.Length)], Roll(pool.weightF, pool.weightA, pool.weightS));
                next.coins -= pool.price; next.inventory.Add(instance);
                next.receipts.Add(new PullReceipt { requestId = requestId, poolId = poolId, instance = instance.Copy() });
                return Commit(next, instance);
            }
        }
        public OperationResult Equip(string instanceId)
        {
            lock (sync)
            {
                if (state == null) return OperationResult.Fail("SaveUnavailable");
                if (!state.inventory.Exists(p => p.instanceId == instanceId)) return OperationResult.Fail("InstanceNotFound");
                var next = Clone(); next.equippedId = instanceId; return Commit(next);
            }
        }
        public OperationResult Release(string instanceId)
        {
            lock (sync)
            {
                if (state == null) return OperationResult.Fail("SaveUnavailable");
                if (!state.inventory.Exists(p => p.instanceId == instanceId)) return OperationResult.Fail("InstanceNotFound");
                if (state.inventory.Count <= 1) return OperationResult.Fail("LastInstance");
                var next = Clone(); next.inventory.RemoveAll(p => p.instanceId == instanceId);
                if (next.equippedId == instanceId) next.equippedId = next.inventory[0].instanceId;
                return Commit(next);
            }
        }
        public bool ChangeCoins(int delta)
        {
            lock (sync)
            {
                if (state == null || (long)state.coins + delta < 0 || (long)state.coins + delta > int.MaxValue) return false;
                var next = Clone(); next.coins += delta; return Commit(next).success;
            }
        }
        public bool SetCoins(int value)
        {
            lock (sync)
            {
                if (state == null || value < 0) return false;
                var next = Clone(); next.coins = value; return Commit(next).success;
            }
        }
        public bool AddCoinsClamped(int delta)
        {
            lock (sync)
            {
                if (state == null) return false;
                var next = Clone();
                next.coins = (int)Math.Max(0L, Math.Min(int.MaxValue, (long)next.coins + delta));
                return Commit(next).success;
            }
        }
        ProgressionSave Clone() => JsonUtility.FromJson<ProgressionSave>(serialized);
        OperationResult Commit(ProgressionSave next, PokemonInstance instance = null)
        {
            try
            {
                ValidateSave(next);
                string json = JsonUtility.ToJson(next);
                store.Write(serialized, json);
                serialized = json; state = next; LastError = null;
            }
            catch (Exception ex)
            {
                // Fail closed: an ambiguous disk failure must not permit a new roll in this session.
                LastError = ex.Message; state = null;
                return OperationResult.Fail("PersistenceFailure");
            }
            // Subscriber exceptions cannot make a committed transaction appear to have failed.
            if (Changed != null)
                foreach (Action listener in Changed.GetInvocationList())
                    try { listener(); } catch (Exception ex) { Debug.LogException(ex); }
            return OperationResult.Ok(instance);
        }
        PokemonInstance Create(string speciesId, Rarity rarity)
        {
            int iv = rarity == Rarity.F ? 0 : rarity == Rarity.A ? 15 : 31;
            return new PokemonInstance { instanceId = Guid.NewGuid().ToString("N"), speciesId = speciesId,
                rarity = rarity, ivs = new StatBlock(iv, iv, iv, iv), nature = (Nature)random.Next(25), duplicateUpgradeLevel = 0 };
        }
        Rarity Roll(int f, int a, int s)
        {
            int value = random.Next(checked(f + a + s));
            return value < f ? Rarity.F : value < f + a ? Rarity.A : Rarity.S;
        }
        void ValidateConfig()
        {
            if (catalog?.species == null || catalog.species.Count == 0 ||
                catalog.species.Any(s => s == null || string.IsNullOrEmpty(s.id)) ||
                catalog.species.Select(s => s.id).Distinct().Count() != catalog.species.Count) throw new InvalidDataException("Invalid catalog.");
            if (config?.pools == null || config.capacity < 1 || config.pools.Count == 0 || config.starters == null || config.starters.Length == 0)
                throw new InvalidDataException("Invalid gacha config.");
            Action<int, int, int> weights = (f, a, s) =>
            { if (f < 0 || a < 0 || s < 0 || (long)f + a + s <= 0 || (long)f + a + s > int.MaxValue) throw new InvalidDataException("Invalid weights."); };
            weights(config.starterF, config.starterA, config.starterS);
            if (config.starters.Distinct().Count() != config.starters.Length || config.starters.Any(id => !catalog.species.Exists(s => s.id == id)))
                throw new InvalidDataException("Invalid starters.");
            var ids = new HashSet<string>();
            foreach (var pool in config.pools)
            {
                if (pool == null || string.IsNullOrEmpty(pool.id) || !ids.Add(pool.id) || pool.price <= 0 ||
                    pool.species == null || pool.species.Length == 0 || pool.species.Distinct().Count() != pool.species.Length ||
                    pool.species.Any(id => !catalog.species.Exists(s => s.id == id))) throw new InvalidDataException("Invalid pool.");
                weights(pool.weightF, pool.weightA, pool.weightS);
            }
        }
        void ValidateSave(ProgressionSave save)
        {
            if (save == null || save.format != "pokemon-progression" || save.version != 1 || save.coins < 0 || save.inventory == null || save.receipts == null ||
                save.inventory.Count > InventoryCapacity || (save.starterChosen ? save.inventory.Count == 0 : save.inventory.Count != 0))
                throw new InvalidDataException("Invalid or unsupported save.");
            var ids = new HashSet<string>();
            foreach (var p in save.inventory)
            {
                ValidateInstance(p);
                if (!ids.Add(p.instanceId)) throw new InvalidDataException("Duplicate instance ID.");
            }
            if (save.inventory.Count > 0 && !ids.Contains(save.equippedId)) throw new InvalidDataException("Invalid equipped ID.");
            var requests = new HashSet<string>();
            foreach (var r in save.receipts)
            {
                if (r == null || string.IsNullOrWhiteSpace(r.requestId) || !requests.Add(r.requestId) || string.IsNullOrEmpty(r.poolId))
                    throw new InvalidDataException("Invalid receipt.");
                ValidateInstance(r.instance);
            }
        }
        void ValidateInstance(PokemonInstance p)
        {
            if (p == null || string.IsNullOrEmpty(p.instanceId) || !catalog.species.Exists(s => s.id == p.speciesId) ||
                (int)p.nature < 0 || (int)p.nature >= 25 || (int)p.rarity < 0 || (int)p.rarity > 2 ||
                p.duplicateUpgradeLevel != 0 || p.ivs.health < 0 || p.ivs.health > 31 || p.ivs.attack < 0 || p.ivs.attack > 31 ||
                p.ivs.defense < 0 || p.ivs.defense > 31 || p.ivs.speed < 0 || p.ivs.speed > 31)
                throw new InvalidDataException("Invalid instance.");
        }
    }
}

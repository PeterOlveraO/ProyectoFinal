using System;
using System.IO;
using System.Linq;
using Game.Pokemon.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Game.Pokemon.Tests
{
    public sealed class ProgressionTests
    {
        sealed class MemoryStore : IProgressionStore
        {
            public string data;
            public bool fail;
            public string Read() => data;
            public void Write(string expected, string next)
            {
                if (fail || expected != data) throw new IOException("Injected failure");
                data = next;
            }
        }
        SpeciesCatalog catalog;
        GachaConfig config;
        [SetUp] public void Setup()
        {
            catalog = JsonUtility.FromJson<SpeciesCatalog>(Resources.Load<TextAsset>("PokemonProgression/catalog").text);
            config = JsonUtility.FromJson<GachaConfig>(Resources.Load<TextAsset>("PokemonProgression/gacha").text);
        }
        ProgressionService Open(MemoryStore store, int coins = 1000) =>
            new ProgressionService(store, catalog, config, coins, new System.Random(123));
        [Test] public void CatalogAndPoolsHaveExpectedMembership()
        {
            Assert.AreEqual(18, catalog.species.Count);
            var basic = config.pools.Single(p => p.id == "basic");
            var premium = config.pools.Single(p => p.id == "premium");
            Assert.AreEqual(15, basic.species.Length);
            CollectionAssert.AreEquivalent(new[] { "dragonite", "garchomp", "mew" }, premium.species);
            CollectionAssert.AreEquivalent(catalog.species.Select(s => s.id), basic.species.Concat(premium.species));
            Assert.IsTrue(Open(new MemoryStore()).IsReady);
        }
        [Test] public void StarterIsOnceAndLegacyCoinsAreImportedOnlyOnce()
        {
            var store = new MemoryStore(); var service = Open(store, 789);
            Assert.IsTrue(service.NeedsStarter);
            Assert.IsFalse(service.ChooseStarter("mew").success);
            Assert.IsTrue(service.ChooseStarter("jolteon").success);
            string id = service.Equipped.instanceId;
            var reload = Open(store, 9999);
            Assert.AreEqual(789, reload.Coins);
            Assert.AreEqual(id, reload.Equipped.instanceId);
            Assert.IsFalse(reload.ChooseStarter("charizard").success);
            Assert.IsFalse(reload.Release(id).success);
        }
        [Test] public void DuplicatesAreSeparateAndPullRetriesSurviveReload()
        {
            config.pools[0].species = new[] { "jolteon" };
            var store = new MemoryStore(); var service = Open(store);
            service.ChooseStarter("jolteon");
            var first = service.Pull("basic", "intent-1");
            Assert.IsTrue(first.success);
            Assert.AreEqual(950, service.Coins);
            Assert.AreNotEqual(service.Equipped.instanceId, first.instance.instanceId);
            var reload = Open(store);
            Assert.AreEqual(first.instance.instanceId, reload.Pull("basic", "intent-1").instance.instanceId);
            Assert.AreEqual(950, reload.Coins);
            Assert.AreEqual(2, reload.Inventory.Count);
            Assert.IsFalse(reload.Pull("premium", "intent-1").success);
            Assert.IsTrue(reload.Release(first.instance.instanceId).success);
            Assert.IsTrue(Open(store).Pull("basic", "intent-1").success);
            Assert.AreEqual(1, Open(store).Inventory.Count);
        }
        [Test] public void FullInvalidAndInsufficientNeverCharge()
        {
            var service = Open(new MemoryStore());
            service.ChooseStarter("blastoise");
            for (int i = 0; i < 9; i++) Assert.IsTrue(service.Pull("basic", i.ToString()).success);
            Assert.IsFalse(service.Pull("basic", "full").success);
            Assert.AreEqual(550, service.Coins);
            Assert.IsFalse(service.Pull("missing", "invalid").success);
            Assert.AreEqual(550, service.Coins);
            var poor = Open(new MemoryStore(), 49);
            poor.ChooseStarter("charizard");
            Assert.IsFalse(poor.Pull("basic", "poor").success);
            Assert.AreEqual(49, poor.Coins);
        }
        [Test] public void ConcurrentSameIntentChargesExactlyOnce()
        {
            var service = Open(new MemoryStore());
            service.ChooseStarter("jolteon");
            var a = System.Threading.Tasks.Task.Run(() => service.Pull("basic", "same-intent"));
            var b = System.Threading.Tasks.Task.Run(() => service.Pull("basic", "same-intent"));
            System.Threading.Tasks.Task.WaitAll(a, b);
            Assert.IsTrue(a.Result.success);
            Assert.IsTrue(b.Result.success);
            Assert.AreEqual(a.Result.instance.instanceId, b.Result.instance.instanceId);
            Assert.AreEqual(950, service.Coins);
            Assert.AreEqual(2, service.Inventory.Count);
        }
        [Test] public void ConfigurationCopiesCannotChangePricesOrPoolMembership()
        {
            var service = Open(new MemoryStore());
            service.GetPool("basic").price = 1;
            service.Configuration.pools[0].species[0] = "mew";
            Assert.AreEqual(50, service.GetPool("basic").price);
            Assert.AreEqual("jolteon", service.GetPool("basic").species[0]);
        }
        [Test] public void ReleaseEquippedSelectsOldestRemaining()
        {
            var service = Open(new MemoryStore());
            service.ChooseStarter("jolteon");
            var second = service.Pull("basic", "1").instance;
            var third = service.Pull("basic", "2").instance;
            Assert.IsTrue(service.Release(service.Equipped.instanceId).success);
            Assert.AreEqual(second.instanceId, service.Equipped.instanceId);
            service.Equip(third.instanceId);
            Assert.IsTrue(service.Release(third.instanceId).success);
            Assert.AreEqual(second.instanceId, service.Equipped.instanceId);
        }
        [Test] public void SaveFailureDoesNotChargeAndFailsClosed()
        {
            var store = new MemoryStore(); var service = Open(store);
            service.ChooseStarter("jolteon");
            string before = store.data;
            store.fail = true;
            Assert.IsFalse(service.Pull("basic", "failure").success);
            Assert.AreEqual(before, store.data);
            Assert.IsFalse(service.IsReady);
            store.fail = false;
            Assert.AreEqual(1000, Open(store).Coins);
            Assert.AreEqual(1, Open(store).Inventory.Count);
        }
        [Test] public void CorruptAndFutureSavesAreNotOverwritten()
        {
            foreach (string value in new[] { "{broken", "{\"version\":999}", "{}" })
            {
                var store = new MemoryStore { data = value };
                var service = Open(store);
                Assert.IsFalse(service.IsReady);
                Assert.IsFalse(service.ChooseStarter("jolteon").success);
                Assert.AreEqual(value, store.data);
            }
        }
        [Test] public void StaleSessionCannotOverwriteCommittedPull()
        {
            var store = new MemoryStore(); var service = Open(store);
            service.ChooseStarter("jolteon");
            var stale = Open(store);
            Assert.IsTrue(service.Pull("premium", "one").success);
            Assert.IsFalse(stale.Pull("premium", "two").success);
            Assert.AreEqual(875, Open(store).Coins);
            Assert.AreEqual(2, Open(store).Inventory.Count);
        }
        [Test] public void ReturnedInstancesCannotMutateSave()
        {
            var service = Open(new MemoryStore());
            service.ChooseStarter("jolteon");
            service.Inventory[0].speciesId = "mew";
            Assert.AreEqual("jolteon", service.Equipped.speciesId);
        }
        [Test] public void RarityInitializesAllFourIndependentIVFields()
        {
            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
            {
                config.starterF = rarity == Rarity.F ? 1 : 0;
                config.starterA = rarity == Rarity.A ? 1 : 0;
                config.starterS = rarity == Rarity.S ? 1 : 0;
                var p = Open(new MemoryStore()).ChooseStarter("jolteon").instance;
                int iv = rarity == Rarity.F ? 0 : rarity == Rarity.A ? 15 : 31;
                Assert.AreEqual(rarity, p.rarity);
                Assert.AreEqual(new StatBlock(iv, iv, iv, iv), p.ivs);
                Assert.AreEqual(0, p.duplicateUpgradeLevel);
            }
        }
        [Test] public void All25NaturesMapAndCancelExactly()
        {
            int[] mapping = { 0, 1, 2, 0, 1 };
            for (int n = 0; n < 25; n++)
            {
                var result = PokemonStats.Calculate(new StatBlock(100, 100, 100, 100),
                    new StatBlock(7, 7, 7, 7), new StatBlock(3, 3, 3, 3), (Nature)n);
                int[] expected = { 110, 110, 110 };
                int up = mapping[n / 5], down = mapping[n % 5];
                if (up != down) { expected[up] = 121; expected[down] = 99; }
                Assert.AreEqual(new StatBlock(110, expected[0], expected[1], expected[2]), result, ((Nature)n).ToString());
            }
        }
        [Test] public void AtomicStoreCompareAndSwapAndReplacement()
        {
            string folder = Path.Combine(Path.GetTempPath(), "pokemon-tests-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "save.json");
            try
            {
                var store = new AtomicProgressionStore(path);
                store.Write(null, "first"); store.Write("first", "second");
                Assert.AreEqual("second", store.Read());
                Assert.Throws<IOException>(() => store.Write("first", "stale"));
                Assert.AreEqual("second", store.Read());
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
    }
}

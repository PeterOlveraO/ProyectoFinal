using System;
using System.Linq;
using Game.Pokemon.Progression;
using Game.Rounds;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Rounds
{
    public sealed class RoundDifficultyTests
    {
        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(4, false)]
        [TestCase(5, true)]
        [TestCase(6, false)]
        [TestCase(10, true)]
        public void BossCadence(int round, bool boss) => Assert.AreEqual(boss, RoundDifficulty.IsBoss(round));

        [Test]
        public void FirstRoundIsPerceptiblyFasterAndEveryEarlyRoundScalesAllStats()
        {
            Assert.GreaterOrEqual(RoundDifficulty.Stats(1).speed, 2.2f * 1.25f);
            for (int round = 2; round <= 20; round++)
            {
                var before = RoundDifficulty.Stats(round - 1);
                var after = RoundDifficulty.Stats(round);
                Assert.Greater(after.health, before.health);
                Assert.Greater(after.speed, before.speed);
                Assert.Greater(after.contactDamage, before.contactDamage);
            }
        }

        [TestCase(100)]
        [TestCase(10000)]
        [TestCase(int.MaxValue)]
        public void LateGameHasSensibleFiniteCaps(int round)
        {
            var stats = RoundDifficulty.Stats(round);
            Assert.AreEqual(200, stats.health);
            Assert.AreEqual(4.6f, stats.speed);
            Assert.AreEqual(35f, stats.contactDamage);
        }

        [TestCase(5)]
        [TestCase(25)]
        [TestCase(1000)]
        public void BossIsSubstantiallyStrongerThanComparableNormal(int round)
        {
            var normal = RoundDifficulty.Stats(round);
            var boss = RoundDifficulty.Stats(round, true);
            Assert.AreEqual(normal.health * 12, boss.health);
            Assert.AreEqual(normal.contactDamage * 3, boss.contactDamage);
            Assert.AreEqual(normal.speed, boss.speed, "boss must remain kiteable");
        }

        [Test]
        public void ExistingCatalogBossesHaveSpritesAndNoNewOrPseudoLegendarySpecies()
        {
            var catalog = JsonUtility.FromJson<SpeciesCatalog>(Resources.Load<TextAsset>("PokemonProgression/catalog").text).species;
            var eligible = catalog.Where(s => s.category == "Legendary" || s.category == "Mythical").ToArray();
            Assert.IsNotEmpty(eligible);
            for (int i = 0; i < eligible.Length; i++)
            {
                var boss = RoundDifficulty.BossSpecies((i + 1) * 5, catalog);
                Assert.Contains(boss, eligible);
                Assert.IsNotNull(Resources.Load<Sprite>("PokemonPortraits/" + boss.spriteName) ??
                    Resources.Load<Sprite>("PokemonSprites/" + boss.spriteName));
            }
            var onlyPseudo = new[] { new SpeciesDefinition { id = "not-a-boss", category = "PseudoLegendary" } };
            Assert.Throws<InvalidOperationException>(() => RoundDifficulty.BossSpecies(5, onlyPseudo));
        }
    }
}

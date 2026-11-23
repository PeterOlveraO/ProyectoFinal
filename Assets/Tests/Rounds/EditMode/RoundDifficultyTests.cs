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
        public void EarlyRoundsScaleStatsUntilTheirCaps()
        {
            Assert.GreaterOrEqual(RoundDifficulty.Stats(1).speed, 2.2f * 1.25f);
            for (int round = 2; round <= 20; round++)
            {
                var before = RoundDifficulty.Stats(round - 1);
                var after = RoundDifficulty.Stats(round);
                Assert.Greater(after.health, before.health);
                if (before.speed < RoundDifficulty.MaxSpeed) Assert.Greater(after.speed, before.speed);
                else Assert.AreEqual(RoundDifficulty.MaxSpeed, after.speed);
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
            Assert.AreEqual(10f, stats.speed);
            Assert.AreEqual(35f, stats.contactDamage);
        }

        [TestCase(1, 14, 14f, 3.92f)]
        [TestCase(6, 24, 17.75f, 5.92f)]
        [TestCase(9, 30, 20f, 7.12f)]
        [TestCase(10, 32, 20.75f, 7.52f)]
        public void NormalStatsKeepHealthAndDamageWhileSpeedClosesTheGap(int round, int health, float damage, float speed)
        {
            var normal = RoundDifficulty.Stats(round);
            Assert.AreEqual(health, normal.health);
            Assert.AreEqual(damage, normal.contactDamage, .0001f);
            Assert.AreEqual(speed, normal.speed, .0001f);
        }

        [TestCase(5, 216, 39f, 4.4f)]
        [TestCase(10, 336, 50.25f, 6.4f)]
        [TestCase(25, 696, 84f, 10f)]
        [TestCase(1000, 2400, 105f, 10f)]
        public void BossStatsKeepHealthAndDamageAndGainSpeedEveryRound(int round, int health, float damage, float speed)
        {
            var boss = RoundDifficulty.Stats(round, true);
            Assert.AreEqual(health, boss.health);
            Assert.AreEqual(damage, boss.contactDamage, .0001f);
            Assert.AreEqual(speed, boss.speed, .0001f);
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

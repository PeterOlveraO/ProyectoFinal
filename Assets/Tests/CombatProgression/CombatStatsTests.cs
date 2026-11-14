using System;
using Game.Player.ProgressionCombat;
using Game.Pokemon.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Game.Pokemon.Tests
{
    public sealed class CombatStatsTests
    {
        CombatBalance balance;
        [SetUp] public void Setup()
        {
            balance = JsonUtility.FromJson<CombatBalance>(Resources.Load<TextAsset>("PokemonProgression/combat").text);
            balance.Validate();
        }
        [Test] public void InitialStartersAreBalancedAgainstTenHPEnemies()
        {
            var jolteon = PokemonCombatStats.Convert(new StatBlock(65, 65, 60, 130), balance);
            var charizard = PokemonCombatStats.Convert(new StatBlock(78, 84, 78, 100), balance);
            var blastoise = PokemonCombatStats.Convert(new StatBlock(79, 83, 100, 78), balance);
            Assert.AreEqual(99, jolteon.maxHealth);
            Assert.AreEqual(2, jolteon.damage);
            Assert.AreEqual(107, charizard.maxHealth);
            Assert.AreEqual(3, charizard.damage);
            Assert.IsTrue(jolteon.movementSpeed > charizard.movementSpeed);
            Assert.IsTrue(charizard.movementSpeed > blastoise.movementSpeed);
            Assert.IsTrue(blastoise.damageReduction > charizard.damageReduction);
            Assert.IsTrue(charizard.damageReduction > jolteon.damageReduction);
        }
        [Test] public void IVsAffectOnlyTheirStoredStatBeforeNature()
        {
            var original = new StatBlock(65, 65, 60, 130);
            var basic = PokemonStats.Calculate(original, default, default, Nature.Hardy);
            var rare = PokemonStats.Calculate(original, new StatBlock(31, 31, 31, 31), default, Nature.Hardy);
            var low = PokemonCombatStats.Convert(basic, balance);
            var high = PokemonCombatStats.Convert(rare, balance);
            Assert.IsTrue(high.maxHealth > low.maxHealth);
            Assert.IsTrue(high.damage > low.damage);
            Assert.IsTrue(high.damageReduction > low.damageReduction);
            Assert.IsTrue(high.movementSpeed > low.movementSpeed);
            var one = PokemonStats.Calculate(original, new StatBlock(0, 15, 0, 0), default, Nature.Hardy);
            Assert.AreEqual(new StatBlock(65, 80, 60, 130), one);
        }
        [Test] public void EveryEVPurchaseImprovesItsCombatStatWithoutChangingOthers()
        {
            var original = new StatBlock(65, 65, 60, 130);
            var before = PokemonCombatStats.Convert(original, balance);
            for (int stat = 0; stat < 4; stat++)
            {
                var ev = PokemonCombatStats.RunEV(balance, stat == 0 ? 1 : 0, stat == 1 ? 1 : 0, stat == 2 ? 1 : 0, stat == 3 ? 1 : 0);
                var after = PokemonCombatStats.Convert(PokemonStats.Calculate(original, default, ev, Nature.Hardy), balance);
                Assert.IsTrue(stat == 0 ? after.maxHealth > before.maxHealth : after.maxHealth == before.maxHealth);
                Assert.IsTrue(stat == 1 ? after.damage > before.damage : after.damage == before.damage);
                Assert.IsTrue(stat == 2 ? after.damageReduction > before.damageReduction : after.damageReduction == before.damageReduction);
                Assert.IsTrue(stat == 3 ? after.movementSpeed > before.movementSpeed : after.movementSpeed == before.movementSpeed);
            }
        }
        [Test] public void NatureAppliesAfterIVAndEVBeforeCombatConversion()
        {
            var raw = PokemonStats.Calculate(new StatBlock(65, 65, 60, 130), new StatBlock(15, 15, 15, 15),
                PokemonCombatStats.RunEV(balance, 1, 1, 1, 1), Nature.Brave);
            Assert.AreEqual(new StatBlock(95, 132, 95, 139), raw);
            var neutral = PokemonStats.Calculate(new StatBlock(65, 65, 60, 130), new StatBlock(15, 15, 15, 15),
                PokemonCombatStats.RunEV(balance, 1, 1, 1, 1), Nature.Adamant);
            Assert.AreEqual(new StatBlock(95, 120, 95, 155), neutral);
            Assert.IsTrue(PokemonCombatStats.Convert(raw, balance).movementSpeed <
                PokemonCombatStats.Convert(neutral, balance).movementSpeed);
        }
        [Test] public void HPRecalculationNeverHealsOrOverfills()
        {
            Assert.AreEqual(25f, PokemonCombatStats.ClampHealth(25f, 200));
            Assert.AreEqual(90f, PokemonCombatStats.ClampHealth(150f, 90));
            Assert.AreEqual(0f, PokemonCombatStats.ClampHealth(-10f, 90));
        }
        [Test] public void BoundsKeepDefenseAndMovementReasonable()
        {
            var max = PokemonCombatStats.Convert(new StatBlock(10000, 10000, 10000, 10000), balance);
            Assert.AreEqual(balance.maxHealth, max.maxHealth);
            Assert.AreEqual(balance.maxDamage, max.damage);
            Assert.AreEqual(balance.maxMovementSpeed, max.movementSpeed);
            Assert.IsTrue(max.damageReduction <= balance.maxBaseDamageReduction);
            Assert.IsTrue(max.damageReduction + .25f <= balance.maxTotalDamageReduction + .0001f);
        }
        [Test] public void EVLevelsAreBoundedAndResetIsZero()
        {
            Assert.AreEqual(default(StatBlock), PokemonCombatStats.RunEV(balance, 0, 0, 0, 0));
            Assert.AreEqual(PokemonCombatStats.RunEV(balance, 10, 10, 10, 10), PokemonCombatStats.RunEV(balance, 100, 100, 100, 100));
            Assert.AreEqual(default(StatBlock), PokemonCombatStats.RunEV(balance, -1, -1, -1, -1));
        }
        [Test] public void InvalidBalanceIsRejectedAndCopiesAreIndependent()
        {
            var copy = balance.Copy();
            copy.evPerUpgrade.attack = 100;
            Assert.AreEqual(40, balance.evPerUpgrade.attack);
            copy.maxTotalDamageReduction = 1f;
            Assert.Throws<ArgumentException>(() => copy.Validate());
            copy = balance.Copy();
            copy.movementPerPoint = float.NaN;
            Assert.Throws<ArgumentException>(() => copy.Validate());
        }
    }
}

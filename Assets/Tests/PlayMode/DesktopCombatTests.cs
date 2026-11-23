using System.Collections.Generic;
using System.Reflection;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Player;
using Game.Rounds;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class DesktopCombatTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> _owned = new List<GameObject>();
        Keyboard _keyboard, _previousKeyboard;
        Mouse _mouse, _previousMouse;
        PlayerAttack _attack;
        bool _blocked, _over;

        GameObject Create(string name)
        {
            var go = new GameObject(name);
            _owned.Add(go);
            return go;
        }
        static object Call(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Private).Invoke(target, args);
        EnemyTriangle Enemy(Vector3 position)
        {
            var go = Create("AimEnemy");
            go.transform.position = position;
            var enemy = go.AddComponent<EnemyTriangle>();
            enemy.Initialize(1, 2);
            return enemy;
        }
        bool Aim(out Vector3 direction)
        {
            object[] args = { Vector3.zero };
            bool result = (bool)Call(_attack, "TryReadAim", args);
            direction = (Vector3)args[0];
            return result;
        }
        void Space(bool pressed)
        {
            InputSystem.QueueStateEvent(_keyboard, pressed ? new KeyboardState(Key.Space) : new KeyboardState());
            InputSystem.Update();
            _keyboard.MakeCurrent();
        }
        void Tick(float seconds)
        {
            bool aiming = Aim(out var direction);
            Call(_attack, "TickAttack", seconds, aiming, direction);
        }
        static int Projectiles() => Object.FindObjectsByType<AttackProjectile>(FindObjectsInactive.Include).Length;

        [SetUp]
        public void Setup()
        {
            Assert.IsNull(RoundManager.Instance);
            Assert.IsNull(PlayerSphereController.Instance);
            _blocked = GameFlow.Blocked; _over = GameFlow.Over;
            GameFlow.Blocked = GameFlow.Over = false;
            _previousKeyboard = Keyboard.current; _previousMouse = Mouse.current;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            _attack = Create("DesktopPlayer").AddComponent<PlayerAttack>();
            _attack.enabled = false;
            _attack.ResetRun();
        }
        [TearDown]
        public void Cleanup()
        {
            foreach (var projectile in Object.FindObjectsByType<AttackProjectile>(FindObjectsInactive.Include))
                Object.DestroyImmediate(projectile.gameObject);
            foreach (var burst in Object.FindObjectsByType<HitBurst>(FindObjectsInactive.Include))
                Object.DestroyImmediate(burst.gameObject);
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            InputSystem.RemoveDevice(_keyboard); InputSystem.RemoveDevice(_mouse);
            if (_previousKeyboard != null && _previousKeyboard.added) _previousKeyboard.MakeCurrent();
            if (_previousMouse != null && _previousMouse.added) _previousMouse.MakeCurrent();
            GameFlow.Blocked = _blocked; GameFlow.Over = _over;
        }

        [Test]
        public void HeldSpaceChoosesNearestLiveTargetAndReleaseStopsAiming()
        {
            Enemy(Vector3.forward * 8);
            var near = Enemy(Vector3.right * 3);
            Space(true);
            Assert.IsTrue(Aim(out var axis));
            Assert.Less(Vector3.Distance(axis, Vector3.right), .0001f);
            near.enabled = false;
            Assert.IsTrue(Aim(out axis));
            Assert.Less(Vector3.Distance(axis, Vector3.forward), .0001f);
            Space(false);
            Assert.IsFalse(Aim(out _));
        }

        [Test]
        public void AutoAimRejectsOccludedTooHighAndOutOfRangeTargets()
        {
            var hidden = Enemy(Vector3.right * 3);
            var wall = Create("AimWall");
            wall.transform.position = new Vector3(1.5f, 1, 0);
            wall.AddComponent<BoxCollider>().size = new Vector3(.4f, 3, 2);
            Enemy(new Vector3(0, 5, 2));
            Enemy(Vector3.back * 25);
            var clear = Enemy(Vector3.forward * 7);
            Physics.SyncTransforms();
            Space(true);
            Assert.IsTrue(Aim(out var axis));
            Assert.Less(Vector3.Distance(axis, Vector3.forward), .0001f);
            clear.enabled = false;
            Assert.IsFalse(Aim(out _));
            hidden.enabled = false;
            Assert.IsFalse(Aim(out _));
        }

        [Test]
        public void SpaceWithoutAnEnemyDoesNotEmitProjectiles()
        {
            Space(true);
            Tick(5);
            Assert.AreEqual(0, Projectiles());
        }

        [Test]
        public void ContinuousFireUsesFasterCadenceAndStopsOnReleasePauseOrFocusLoss()
        {
            Enemy(Vector3.forward * 7);
            Space(true);
            Tick(0);
            Assert.AreEqual(1, Projectiles());
            Tick(.65f);
            Assert.AreEqual(1, Projectiles());
            Tick(.02f);
            Assert.AreEqual(2, Projectiles(), "1.5 shots per second instead of 1.");
            Space(false); Tick(2);
            Assert.AreEqual(2, Projectiles());
            Space(true); GameFlow.Blocked = true; Tick(2);
            Assert.AreEqual(2, Projectiles());
            GameFlow.Blocked = false;
            Call(_attack, "OnApplicationFocus", false); Tick(2);
            Assert.AreEqual(2, Projectiles());
            Call(_attack, "OnApplicationFocus", true); Tick(0);
            Assert.AreEqual(3, Projectiles());
        }

        [Test]
        public void SelectedTextInputDoesNotStartAutoFire()
        {
            Enemy(Vector3.forward * 3);
            var events = Create("DesktopEvents").AddComponent<EventSystem>();
            var input = new GameObject("Amount", typeof(RectTransform), typeof(TMPro.TMP_InputField));
            _owned.Add(input);
            events.SetSelectedGameObject(input);
            Space(true);
            Assert.IsFalse(Aim(out _));
            events.SetSelectedGameObject(null);
            Assert.IsTrue(Aim(out _));
        }

        [Test]
        public void MovementMultiplierComposesWithHasteAndDoesNotAccumulateOnEnemyReuse()
        {
            var player = Create("PlayerSpeed").AddComponent<PlayerSphereController>();
            player.enabled = false;
            Assert.AreEqual(7.5f, player.MovementSpeed, .001f);
            player.RunEffects.Configure(() => true, _ => { }, () => false, () => false);
            Assert.IsTrue(player.RunEffects.Apply(TemporaryEffect.Haste));
            Assert.AreEqual(9f, player.MovementSpeed, .001f);
            var enemy = Enemy(Vector3.forward * 3);
            Assert.AreEqual(3f, enemy.Speed, .001f);
            enemy.Initialize(1, 2);
            Assert.AreEqual(3f, enemy.Speed, .001f);
            enemy.InitializeRound(1, new EnemyRoundStats { speed = 4, health = 100, contactDamage = 10 }, true);
            Assert.AreEqual(6f, enemy.Speed, .001f);
        }
    }
}

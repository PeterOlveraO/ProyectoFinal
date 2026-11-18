using System.Collections.Generic;
using System.Reflection;
using Game.Combat;
using Game.Enemies;
using Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Game.PlayerAim.Tests
{
    public sealed class PlayerAimTests
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        readonly List<GameObject> _objects = new List<GameObject>();
        readonly List<Camera> _disabledCameras = new List<Camera>();
        readonly List<EventSystem> _disabledEventSystems = new List<EventSystem>();
        PlayerAttack _attack;
        VirtualJoystick _joystick;
        Camera _camera;
        Mouse _mouse;
        Touchscreen _touchscreen;
        Mouse _previousMouse;
        Touchscreen _previousTouchscreen;
        UnityEngine.InputSystem.Pointer _previousPointer;
        EventSystem _previousEventSystem;

        [SetUp]
        public void SetUp()
        {
            _previousMouse = Mouse.current;
            _previousTouchscreen = Touchscreen.current;
            _previousPointer = UnityEngine.InputSystem.Pointer.current;
            _previousEventSystem = EventSystem.current;

            // EditMode can run with the user's scene open. Keep its cameras and UI
            // from affecting aim and restore only the components we disabled.
            foreach (var camera in Object.FindObjectsOfType<Camera>())
            {
                if (!camera.enabled || !camera.CompareTag("MainCamera")) continue;
                _disabledCameras.Add(camera);
                camera.enabled = false;
            }
            foreach (var eventSystem in Object.FindObjectsOfType<EventSystem>())
            {
                if (!eventSystem.enabled) continue;
                _disabledEventSystems.Add(eventSystem);
                eventSystem.enabled = false;
            }

            _attack = Create("Player").AddComponent<PlayerAttack>();
            _joystick = Create("AttackJoystick", typeof(RectTransform)).AddComponent<VirtualJoystick>();
            _attack.SetJoystick(_joystick);
            _camera = Create("AimCamera").AddComponent<Camera>();
            _camera.tag = "MainCamera";
            _mouse = InputSystem.AddDevice<Mouse>();
            _touchscreen = InputSystem.AddDevice<Touchscreen>();
            Assert.That(Camera.main, Is.EqualTo(_camera));
            Assert.That(EventSystem.current, Is.Null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_mouse != null) InputSystem.RemoveDevice(_mouse);
            if (_touchscreen != null) InputSystem.RemoveDevice(_touchscreen);
            if (_previousMouse != null && _previousMouse.added) _previousMouse.MakeCurrent();
            if (_previousTouchscreen != null && _previousTouchscreen.added) _previousTouchscreen.MakeCurrent();
            if (_previousPointer != null && _previousPointer.added) _previousPointer.MakeCurrent();
            foreach (var go in _objects)
                if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
            foreach (var camera in _disabledCameras)
                if (camera != null) camera.enabled = true;
            _disabledCameras.Clear();
            foreach (var eventSystem in _disabledEventSystems)
                if (eventSystem != null) eventSystem.enabled = true;
            _disabledEventSystems.Clear();
            if (_previousEventSystem != null && _previousEventSystem.isActiveAndEnabled)
                EventSystem.current = _previousEventSystem;
            _mouse = null;
            _touchscreen = null;
        }

        GameObject Create(string name, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            _objects.Add(go);
            return go;
        }

        void SetStick(Vector2 value)
        {
            typeof(VirtualJoystick).GetField("_value", PrivateInstance).SetValue(_joystick, value);
        }

        bool ReadAim(out Vector3 direction)
        {
            object[] args = { Vector3.zero };
            bool result = (bool)typeof(PlayerAttack).GetMethod("TryReadAim", PrivateInstance).Invoke(_attack, args);
            direction = (Vector3)args[0];
            return result;
        }

        static void AssertDirection(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
        }

        [TestCase(0f, 1f, 0f, 0f, 1f)]
        [TestCase(1f, 0f, 0f, 1f, 0f)]
        [TestCase(-1f, 0f, 0f, -1f, 0f)]
        [TestCase(0f, -1f, 0f, 0f, -1f)]
        [TestCase(0f, 1f, 90f, 1f, 0f)]
        [TestCase(1f, 0f, 90f, 0f, -1f)]
        public void StickUsesCameraRelativeHorizontalDirection(float x, float y, float yaw, float expectedX, float expectedZ)
        {
            _camera.transform.rotation = Quaternion.Euler(35f, yaw, 0f);
            SetStick(new Vector2(x, y));
            Assert.That(ReadAim(out var direction), Is.True);
            AssertDirection(direction, new Vector3(expectedX, 0f, expectedZ));
        }

        [Test]
        public void DiagonalIsNormalizedAndRotatedWithCamera()
        {
            _camera.transform.rotation = Quaternion.Euler(30f, 90f, 0f);
            SetStick(new Vector2(0.5f, 0.5f));
            Assert.That(ReadAim(out var direction), Is.True);
            AssertDirection(direction, new Vector3(1f, 0f, -1f).normalized);
        }

        [Test]
        public void DeadZoneAndReleaseDoNotKeepThePreviousStickDirection()
        {
            // No mouse is needed to exercise the joystick path.
            SetStick(new Vector2(0.28f, 0f));
            Assert.That(ReadAim(out _), Is.True, "the threshold itself is inclusive");
            SetStick(new Vector2(0.27f, 0f));
            Assert.That(ReadAim(out _), Is.False);
            SetStick(Vector2.right);
            Assert.That(ReadAim(out _), Is.True);
            typeof(VirtualJoystick).GetMethod("Release", PrivateInstance).Invoke(_joystick, null);
            Assert.That(_joystick.Value, Is.EqualTo(Vector2.zero));
            Assert.That(ReadAim(out _), Is.False);
        }

        [TestCase(true, false, false)]
        [TestCase(true, true, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public void MouseFallbackIsDesktopOnlyAndNeverUsesActiveTouch(bool mobile, bool touch, bool expected)
        {
            var method = typeof(PlayerAttack).GetMethod("CanUseMouseAim", PrivateStatic);
            Assert.That(method.Invoke(null, new object[] { mobile, touch }), Is.EqualTo(expected));
        }

        [Test]
        public void DesktopMouseAimsAtCursorButActiveStickTakesPriority()
        {
            _camera.transform.position = new Vector3(0f, 10f, -10f);
            _camera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            Vector2 cursor = new Vector2(Screen.width * 0.75f, Screen.height * 0.5f);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = cursor }.WithButton(MouseButton.Left));
            InputSystem.Update();
            _mouse.MakeCurrent();
            _touchscreen.MakeCurrent();

            Assert.That(ReadAim(out var cursorAim), Is.True);
            var ray = _camera.ScreenPointToRay(cursor);
            Assert.That(new Plane(Vector3.up, Vector3.up * 0.5f).Raycast(ray, out float distance), Is.True);
            Vector3 expected = ray.GetPoint(distance);
            expected.y = 0f;
            AssertDirection(cursorAim, expected.normalized);

            SetStick(Vector2.left);
            Assert.That(ReadAim(out var stickAim), Is.True);
            AssertDirection(stickAim, Vector3.left);
        }

        [Test]
        public void ActiveTouchSuppressesMouseFallback()
        {
            _camera.transform.position = new Vector3(0f, 10f, -10f);
            _camera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            var cursor = new Vector2(Screen.width * 0.75f, Screen.height * 0.5f);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = cursor }.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(_touchscreen, new TouchState
            {
                touchId = 1,
                phase = UnityEngine.InputSystem.TouchPhase.Began,
                position = cursor
            });
            InputSystem.Update();
            _mouse.MakeCurrent();
            _touchscreen.MakeCurrent();
            Assert.That(ReadAim(out _), Is.False);
        }

        [Test]
        public void NearbyEnemyDoesNotChangeAimOrProjectileDirection()
        {
            SetStick(Vector2.up);
            Assert.That(ReadAim(out var before), Is.True);

            var enemyObject = Create("NearbyEnemy");
            enemyObject.SetActive(false);
            enemyObject.transform.position = new Vector3(1f, 0f, 0f);
            var enemy = enemyObject.AddComponent<EnemyTriangle>();
            var enemies = (List<EnemyTriangle>)typeof(EnemyTriangle).GetField("Active", PrivateStatic).GetValue(null);
            enemies.Add(enemy);
            try
            {
                Assert.That(ReadAim(out var withEnemy), Is.True);
                AssertDirection(withEnemy, before);
                var projectile = AttackProjectile.Spawn(Vector3.zero, withEnemy, 10f, 20f, 1, Color.white);
                _objects.Add(projectile.gameObject);
                var directionField = typeof(AttackProjectile).GetField("_direction", PrivateInstance);
                AssertDirection((Vector3)directionField.GetValue(projectile), before);
                Assert.That(typeof(AttackProjectile).GetMethod("CheckHit", PrivateInstance).Invoke(projectile, null), Is.False);
                AssertDirection((Vector3)directionField.GetValue(projectile), before);
            }
            finally
            {
                enemies.Remove(enemy);
            }
        }
    }
}

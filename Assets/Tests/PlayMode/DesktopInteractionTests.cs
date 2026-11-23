using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Combat;
using Game.Core;
using Game.Player;
using Game.Rounds;
using Game.Terrain;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class DesktopInteractionTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> _objects = new List<GameObject>();
        RoundManager _round;
        PlayerSphereController _player;
        RunHud _hud;
        Keyboard _keyboard, _previousKeyboard;
        TerrainSettings _settings;
        WorldTerrain _terrain;
        bool _blocked, _over;
        float _timeScale;
        System.Func<int, bool> _zoneAccess;

        [SetUp]
        public void SetUp()
        {
            Assert.IsNull(RoundManager.Instance);
            Assert.IsNull(GameMenuController.Instance);
            Assert.IsNull(WorldTerrain.Instance);
            _blocked = GameFlow.Blocked; _over = GameFlow.Over; _timeScale = Time.timeScale;
            _zoneAccess = WorldShopPlacement.IsZoneAccessible;
            GameFlow.Blocked = GameFlow.Over = false;
            Time.timeScale = 1;
            WorldShopPlacement.IsZoneAccessible = _ => true;
            _previousKeyboard = Keyboard.current;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _keyboard.MakeCurrent();
            _ = _keyboard.eKey.wasPressedThisFrame;
            Key(false);
            _round = Create("InteractionRound").AddComponent<RoundManager>();
            foreach (var component in _round.GetComponents<MonoBehaviour>()) component.enabled = false;
            _player = Create("InteractionPlayer").AddComponent<PlayerSphereController>();
            _player.enabled = false;
            Set(_round, "_phase", RoundManager.RoundPhase.Preparing);
            Set(_round, "<Points>k__BackingField", 1000);
            var drinks = new Vector3[DrinkStations.OfferCount];
            var stands = Get<Transform[]>(_round.Drinks, "_stands");
            for (int i = 0; i < drinks.Length; i++)
            {
                drinks[i] = new Vector3(i * 10, 0, 0);
                stands[i] = Create("DrinkStand").transform;
                stands[i].position = drinks[i];
            }
            Set(_round.Drinks, "_positions", System.Array.AsReadOnly(drinks));
            var shots = new Vector3[ShotStations.OfferCount];
            for (int i = 0; i < shots.Length; i++) shots[i] = new Vector3(i * 10, 0, 20);
            Set(_round.ShotStations, "_positions", System.Array.AsReadOnly(shots));
            var canvas = Create("InteractionCanvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _hud = canvas.gameObject.AddComponent<RunHud>();
            _hud.enabled = false;
            Call(_hud, "Start");
            _objects.Add(Get<RectTransform>(_hud, "_overlay").gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrain != null) typeof(WorldTerrain).GetProperty("Instance").SetValue(null, null);
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            if (_settings != null) Object.DestroyImmediate(_settings);
            InputSystem.RemoveDevice(_keyboard);
            if (_previousKeyboard != null && _previousKeyboard.added) _previousKeyboard.MakeCurrent();
            WorldShopPlacement.IsZoneAccessible = _zoneAccess;
            GameFlow.Blocked = _blocked; GameFlow.Over = _over; Time.timeScale = _timeScale;
        }

        [UnityTest]
        public IEnumerator PressBuildsThenReleaseAndPressBuysButHoldingNeverSpendsAgain()
        {
            Key(true); Tick();
            Assert.IsTrue(_round.Drinks.IsBuilt(0));
            Assert.IsFalse(_round.Drinks.Has(DrinkType.Power));
            Assert.AreEqual(900, _round.Points);
            yield return null;
            InputSystem.Update();
            Tick();
            Assert.AreEqual(900, _round.Points, "holding E must not buy the newly built perk");
            Key(false); Tick();
            yield return null;
            Key(true); Tick();
            Assert.IsTrue(_round.Drinks.Has(DrinkType.Power));
            Assert.AreEqual(800, _round.Points);
            Assert.Zero(_round.CoinsEarned);
        }

        [UnityTest]
        public IEnumerator ArsenalUsesSeparateBuildAndBuyPressesAndKeepsTouchButtonWorking()
        {
            _player.transform.position = new Vector3(0, 0, 20);
            Key(true); Tick();
            Assert.IsTrue(_round.ShotStations.IsBuilt(0));
            Assert.AreEqual(ShotType.Basic, _round.Shots.Equipped);
            int afterBuild = _round.Points;
            // A pointer click delivered on the E frame cannot charge for a second operation.
            Get<Button>(_hud, "_shotButton").onClick.Invoke();
            Assert.AreEqual(afterBuild, _round.Points);
            Key(false);
            yield return null;
            Tick();
            var button = Get<Button>(_hud, "_shotButton");
            Assert.IsTrue(button.IsInteractable());
            button.onClick.Invoke();
            Assert.AreEqual(ShotType.Burst, _round.Shots.Equipped);
            Assert.AreEqual(afterBuild - ShotDefinitions.Price(ShotType.Burst), _round.Points);
        }

        [TestCase("paused")]
        [TestCase("timeScale")]
        [TestCase("dead")]
        [TestCase("idle")]
        [TestCase("over")]
        [TestCase("remote")]
        [TestCase("height")]
        [TestCase("poor")]
        [TestCase("locked")]
        [TestCase("typing")]
        public void ERejectsUnavailableInteractionsWithoutSpending(string reason)
        {
            switch (reason)
            {
                case "paused": GameFlow.Blocked = true; break;
                case "timeScale": Time.timeScale = 0; break;
                case "dead": Set(_player, "_alive", false); break;
                case "idle": Set(_round, "_phase", RoundManager.RoundPhase.Idle); break;
                case "over": GameFlow.Over = true; break;
                case "remote": _player.transform.position = Vector3.one * 100; break;
                case "height": _player.transform.position = Vector3.up * 5; break;
                case "poor": Set(_round, "<Points>k__BackingField", 99); break;
                case "locked": WorldShopPlacement.IsZoneAccessible = _ => false; break;
                case "typing":
                    var events = Create("InteractionEvents").AddComponent<EventSystem>();
                    var input = Create("InteractionText").AddComponent<TMPro.TMP_InputField>();
                    events.SetSelectedGameObject(input.gameObject);
                    break;
            }
            int before = _round.Points;
            Key(true); Tick();
            Assert.AreEqual(before, _round.Points);
            Assert.IsFalse(_round.Drinks.IsBuilt(0));
            Assert.IsFalse(_round.ShotStations.IsBuilt(0));
        }

        [Test]
        public void OverlappingOffersGiveEOnlyToNearestAndChargeOnce()
        {
            var positions = new Vector3[ShotStations.OfferCount];
            for (int i = 0; i < positions.Length; i++) positions[i] = new Vector3(i * 10, 0, 2);
            Set(_round.ShotStations, "_positions", System.Array.AsReadOnly(positions));
            _player.transform.position = new Vector3(0, 0, 1.8f);
            Key(true); Tick();
            Assert.IsTrue(_round.ShotStations.IsBuilt(0));
            Assert.IsFalse(_round.Drinks.IsBuilt(0));
            Assert.AreEqual(1000 - ShotStations.BuildPrice(0), _round.Points);
            StringAssert.StartsWith("[E]", Get<TMPro.TMP_Text>(_hud, "_shotLabel").text);
            Assert.IsFalse(Get<TMPro.TMP_Text>(_hud, "_drinkLabel").text.Contains("[E]"));
        }

        [Test]
        public void EOpensNearbyZoneAndChargesItsRealPrice()
        {
            _settings = ScriptableObject.CreateInstance<TerrainSettings>();
            _settings.enableWorldZones = true;
            _settings.chunksX = _settings.chunksZ = 4;
            _settings.chunkSize = 16;
            _settings.lakeCount = 0;
            var world = Create("InteractionWorld");
            world.SetActive(false);
            _terrain = world.AddComponent<WorldTerrain>();
            var map = new HeightMap(64, 64);
            for (int z = 0; z < 64; z++)
            for (int x = 0; x < 64; x++) map.Set(x, z, 1);
            Set(_terrain, "settings", _settings);
            Set(_terrain, "_map", map);
            Set(_terrain, "<Traversal>k__BackingField", new WorldTraversal(map, _settings, Vector3.zero));
            typeof(WorldTerrain).GetProperty("Instance").SetValue(null, _terrain);
            _round.Zones.Bind(_round);
            Assert.IsTrue(_round.Zones.TryGetOfferPosition(2, out var position));
            _player.transform.position = position;
            Key(true); Tick();
            Assert.IsTrue(_round.Zones.IsUnlocked(2));
            Assert.AreEqual(750, _round.Points);
            Assert.Zero(_round.CoinsEarned);
        }

        [Test]
        public void GameplayHudHasNoWeatherOrEquippedShotPanels()
        {
            Tick();
            var root = Get<RectTransform>(_hud, "_root");
            Assert.IsNull(root.Find("WeatherStatus"));
            Assert.IsNull(root.Find("ShotLoadout"));
            Assert.IsNotNull(root.Find("RunStatus"));
        }

        void Key(bool pressed)
        {
            InputSystem.QueueStateEvent(_keyboard, pressed ? new KeyboardState(UnityEngine.InputSystem.Key.E) : new KeyboardState());
            InputSystem.Update();
            _keyboard.MakeCurrent();
        }
        void Tick() => Call(_hud, "Update");
        GameObject Create(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }
        static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Private).SetValue(target, value);
        static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
        static object Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    }
}

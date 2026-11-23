using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Pokemon;
using Game.Pokemon.Progression;
using Game.Rounds;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.CompactMenus
{
    /// <summary>Real controller buttons, run and disk-backed progression; no duplicate pause model.</summary>
    public sealed class CompactMenuNavigationTests
    {
        GameObject _menuObject, _roundObject;
        GameMenuController _menu;
        object _previousService;
        string _directory;
        bool _blocked, _over, _audioPause;
        float _timeScale, _volume;
        int _quality;
        UnityEngine.Random.State _random;
        static readonly FieldInfo Service = typeof(PokemonCollection).GetField("service",
            BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            Assert.IsNull(GameMenuController.Instance, "Use an empty test scene.");
            Assert.IsNull(RoundManager.Instance);
            _blocked = GameFlow.Blocked;
            _over = GameFlow.Over;
            _audioPause = AudioListener.pause;
            _timeScale = Time.timeScale;
            _volume = AudioListener.volume;
            _quality = QualitySettings.GetQualityLevel();
            _random = UnityEngine.Random.state;
            _previousService = Service.GetValue(null);
            _directory = Path.Combine(Application.temporaryCachePath, "CompactMenus-" + Guid.NewGuid().ToString("N"));
            var progression = new ProgressionService(
                new AtomicProgressionStore(Path.Combine(_directory, "progression.json")),
                JsonUtility.FromJson<SpeciesCatalog>(Resources.Load<TextAsset>("PokemonProgression/catalog").text),
                JsonUtility.FromJson<GachaConfig>(Resources.Load<TextAsset>("PokemonProgression/gacha").text));
            Assert.IsTrue(progression.IsReady, progression.LastError);
            Assert.IsTrue(progression.ChooseStarter(progression.StarterIds[0]).success);
            Service.SetValue(null, progression);
            _roundObject = new GameObject("MenuTestRound");
            var round = _roundObject.AddComponent<RoundManager>();
            // Synchronous button tests: no spawning or unrelated frame updates.
            round.enabled = false;
            round.Drinks.enabled = false;
            round.ShotStations.enabled = false;
            _menuObject = new GameObject("MenuTestController");
            _menu = _menuObject.AddComponent<GameMenuController>();
            _menu.enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (_menuObject != null) Object.DestroyImmediate(_menuObject);
            if (_roundObject != null) Object.DestroyImmediate(_roundObject);
            // Controller disposes its canvas using delayed Destroy in Play Mode.
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                if (canvas.name == "UI_Menus") Object.DestroyImmediate(canvas.gameObject);
            Service.SetValue(null, _previousService);
            if (_directory != null && Directory.Exists(_directory)) Directory.Delete(_directory, true);
            GameFlow.Blocked = _blocked;
            GameFlow.Over = _over;
            Time.timeScale = _timeScale;
            AudioListener.pause = _audioPause;
            AudioListener.volume = _volume;
            QualitySettings.SetQualityLevel(_quality, true);
            UnityEngine.Random.state = _random;
        }

        [Test]
        public void EVReturnsToItsOriginWithoutUnpausingThePauseMenu()
        {
            Click("MainMenu", "PlayButton");
            Assert.IsTrue(RoundManager.Instance.IsRunActive);
            AssertRunning();
            Click("RunControls", "RunSkillsButton");
            AssertPaused("SkillsMenu");
            Click("SkillsMenu", "Back");
            AssertRunning();
            _menu.Pause();
            AssertPaused("PauseMenu");
            Click("PauseMenu", "Skills");
            AssertPaused("SkillsMenu");
            Click("SkillsMenu", "Back");
            AssertPaused("PauseMenu");
            Click("PauseMenu", "Resume");
            AssertRunning();
        }

        [Test]
        public void CollectionGachaAndInstitutionalCreditsRemainReachable()
        {
            Click("MainMenu", "Collection");
            AssertActive("CollectionMenu");
            Click("CollectionMenu", "Back");
            Click("MainMenu", "Gacha");
            AssertActive("GachaMenu");
            Click("GachaMenu", "Back");
            AssertActive("CollectionMenu");
            Click("CollectionMenu", "Back");
            Click("MainMenu", "Credits");
            AssertActive("CreditsMenu");
            var texts = CanvasRoot().Find("CreditsMenu").GetComponentsInChildren<TMPro.TMP_Text>();
            Assert.IsTrue(texts.Any(t => t.text == "Universidad Autónoma de Tamaulipas"));
            Assert.IsTrue(texts.Any(t => t.text == "Facultad de Ingeniería Tampico"));
            Assert.IsTrue(texts.Any(t => t.text == "Pedro Olvera"));
            Click("CreditsMenu", "Back");
            AssertActive("MainMenu");
        }

        [UnityTest]
        public IEnumerator TestInputFocusAndBackRouteKeepTheRunPaused()
        {
            var events = UnityEngine.EventSystems.EventSystem.current == null
                ? new GameObject("TestRewardsEventSystem", typeof(UnityEngine.EventSystems.EventSystem)) : null;
            try
            {
                Click("MainMenu", "PlayButton");
                string run = JsonUtility.ToJson(RoundManager.Instance.RunPokemon);
                Game.Weather.WeatherController.Instance.SetWeather(Game.Weather.WeatherKind.Rain);
                _menu.Pause();
                Click("PauseMenu", "Tests");
                AssertPaused("TestMenu");
                int coins = PokemonCollection.Progression.Coins;
                var input = CanvasRoot().Find("TestMenu").GetComponentInChildren<TMPro.TMP_InputField>();
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(input.gameObject);
                input.ActivateInputField();
                yield return null;
                Assert.IsTrue(input.isFocused);
                input.text = "777";
                Assert.IsTrue(GameFlow.Blocked, "Gameplay M/Space must stay gated while editing.");
                // Update routes Escape and gamepad East through this exact navigation method.
                Back();
                AssertPaused("PauseMenu");
                Assert.IsFalse(input.isFocused);
                Assert.AreEqual(run, JsonUtility.ToJson(RoundManager.Instance.RunPokemon));
                Click("PauseMenu", "Resume");
                AssertRunning();
                Assert.AreEqual(Game.Weather.WeatherKind.Rain, Game.Weather.WeatherController.Instance.Current);
                var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                Assert.IsNull(eventSystem.currentSelectedGameObject, "Resume must release UI Submit focus.");
                foreach (var button in CanvasRoot().Find("RunControls").GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {
                    Assert.AreEqual(UnityEngine.UI.Navigation.Mode.None, button.navigation.mode);
                    button.OnPointerDown(new UnityEngine.EventSystems.PointerEventData(eventSystem)
                        { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left });
                    Assert.IsNull(eventSystem.currentSelectedGameObject, "HUD pointer-down must not capture Submit.");
                    button.OnPointerUp(new UnityEngine.EventSystems.PointerEventData(eventSystem));
                }
                // The UI module dispatches Submit only to the currently selected object.
                UnityEngine.EventSystems.ExecuteEvents.Execute(eventSystem.currentSelectedGameObject,
                    new UnityEngine.EventSystems.BaseEventData(eventSystem), UnityEngine.EventSystems.ExecuteEvents.submitHandler);
                AssertRunning();
                Assert.AreEqual(coins, PokemonCollection.Progression.Coins, "Back/resume/Submit must not award coins.");
                Assert.AreEqual(run, JsonUtility.ToJson(RoundManager.Instance.RunPokemon));
                _menu.AbandonRun();
                Assert.AreEqual(Game.Weather.WeatherKind.Sunny, Game.Weather.WeatherController.Instance.Current);
            }
            finally { if (events != null) Object.DestroyImmediate(events); }
        }

        [TestCase(Rarity.F)]
        [TestCase(Rarity.A)]
        [TestCase(Rarity.S)]
        public void TestMenuAwardsChosenSpeciesToNormalCollectionWithoutChangingActiveRun(Rarity rarity)
        {
            Click("MainMenu", "PlayButton");
            string run = JsonUtility.ToJson(RoundManager.Instance.RunPokemon);
            string equipped = PokemonCollection.Equipped.instanceId;
            _menu.Pause();
            Click("PauseMenu", "Tests");
            SelectTestSpecies("mew");
            Click("TestMenu", "Rarity" + rarity);
            Assert.AreEqual(1, PokemonCollection.Inventory.Count, "Selection alone must not award.");
            Click("TestMenu", "GrantPokemon");
            var granted = PokemonCollection.Inventory.Single(p => p.speciesId == "mew");
            Assert.AreEqual(rarity, granted.rarity);
            Assert.AreEqual(equipped, PokemonCollection.Equipped.instanceId);
            Assert.AreEqual(run, JsonUtility.ToJson(RoundManager.Instance.RunPokemon));
            AssertPaused("TestMenu");
            Assert.AreEqual(granted.instanceId, ReloadProgression().Inventory.Single(p => p.speciesId == "mew").instanceId);
            Back();
            AssertPaused("PauseMenu");
            Click("PauseMenu", "Resume");
            AssertRunning();
            Assert.AreEqual(run, JsonUtility.ToJson(RoundManager.Instance.RunPokemon));
            _menu.AbandonRun();
            Assert.IsTrue(PokemonCollection.Equip(granted.instanceId).success);
            Click("MainMenu", "PlayButton");
            Assert.AreEqual(granted.instanceId, RoundManager.Instance.RunPokemon.instanceId);
        }

        [Test]
        public void MainTestMenuPersistsPresetsAndCustomCoinsAndReturnsToMain()
        {
            Click("MainMenu", "Tests");
            AssertActive("TestMenu");
            Assert.IsFalse(_menu.IsPlaying);
            Assert.IsTrue(GameFlow.Blocked);
            foreach (int amount in new[] { 100, 1000, 10000 }) Click("TestMenu", "Coins" + amount);
            var input = CanvasRoot().Find("TestMenu").GetComponentInChildren<TMPro.TMP_InputField>();
            input.text = "123";
            Click("TestMenu", "GrantCoins");
            Assert.AreEqual(11223, ReloadProgression().Coins);
            foreach (string invalid in new[] { "", "0", "-2", "1.5", "2147483648" })
            {
                input.text = invalid;
                Click("TestMenu", "GrantCoins");
                Assert.AreEqual(11223, PokemonCollection.Progression.Coins, invalid);
                StringAssert.Contains("positiva", TestFeedback());
            }
            Back();
            AssertActive("MainMenu");
            Assert.IsFalse(_menu.IsPlaying);
            Click("MainMenu", "Tests");
            Click("TestMenu", "Back");
            AssertActive("MainMenu");
        }

        [Test]
        public void TestMenuShowsFullCollectionAndStorageFailure()
        {
            Click("MainMenu", "Tests");
            for (int i = 1; i < 10; i++) Click("TestMenu", "GrantPokemon");
            Click("TestMenu", "GrantPokemon");
            Assert.AreEqual(10, PokemonCollection.Inventory.Count);
            StringAssert.Contains("Colección llena", TestFeedback());
            Click("TestMenu", "Coins100");
            Assert.AreEqual(100, ReloadProgression().Coins);
            // Another session commits first: the live menu must report the CAS failure.
            var otherSession = ReloadProgression();
            Assert.IsTrue(otherSession.GrantForTesting(1000).success);
            Click("TestMenu", "Coins100");
            StringAssert.Contains("No se pudo guardar", TestFeedback());
            Assert.IsFalse(PokemonCollection.Progression.IsReady);
            Assert.AreEqual(1100, ReloadProgression().Coins);
            Assert.AreEqual(10, ReloadProgression().Inventory.Count);
            Assert.IsFalse(CanvasRoot().Find("TestMenu").GetComponentsInChildren<UnityEngine.UI.Button>()
                .Single(b => b.name == "GrantPokemon").interactable);
            Click("TestMenu", "Back");
            AssertActive("MainMenu");
        }

        [Test]
        public void TestMenuCannotBypassStarterChoice()
        {
            var progression = new ProgressionService(
                new AtomicProgressionStore(Path.Combine(_directory, "new-save.json")),
                JsonUtility.FromJson<SpeciesCatalog>(Resources.Load<TextAsset>("PokemonProgression/catalog").text),
                JsonUtility.FromJson<GachaConfig>(Resources.Load<TextAsset>("PokemonProgression/gacha").text));
            Service.SetValue(null, progression);
            _menu.ShowMainMenu();
            AssertActive("StarterMenu");
            Assert.IsFalse(CanvasRoot().Find("StarterMenu").GetComponentsInChildren<UnityEngine.UI.Button>()
                .Any(b => b.name == "Tests"));
            typeof(GameMenuController).GetMethod("OpenTests", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_menu, null);
            AssertActive("StarterMenu");
            Assert.IsTrue(progression.NeedsStarter);
            Assert.IsEmpty(progression.Inventory);
            Click("StarterMenu", "ConfirmStarter");
            Click("ResultMenu", "Continue");
            Click("MainMenu", "Tests");
            AssertActive("TestMenu");
        }

        void SelectTestSpecies(string id)
        {
            int index = PokemonCollection.Catalog.ToList().FindIndex(s => s.id == id);
            Assert.GreaterOrEqual(index, 0);
            for (int i = 0; i < index; i++) Click("TestMenu", "NextSpecies");
        }

        void Back() => typeof(GameMenuController).GetMethod("NavigateBack",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_menu, null);

        static string TestFeedback() => CanvasRoot().Find("TestMenu").GetComponentsInChildren<TMPro.TMP_Text>()
            .Single(t => t.name == "Subtitle").text;

        ProgressionService ReloadProgression() => new ProgressionService(
            new AtomicProgressionStore(Path.Combine(_directory, "progression.json")),
            JsonUtility.FromJson<SpeciesCatalog>(Resources.Load<TextAsset>("PokemonProgression/catalog").text),
            JsonUtility.FromJson<GachaConfig>(Resources.Load<TextAsset>("PokemonProgression/gacha").text));

        [UnityTest]
        public IEnumerator GachaPreviewsCycleWhilePausedWithoutSpendingOrDuplicatingSlots()
        {
            Click("MainMenu", "Gacha");
            Assert.AreEqual(0, Time.timeScale);
            var page = CanvasRoot().Find("GachaMenu");
            var roulette = page.GetComponentsInChildren<GachaRoulette>()
                .Single(r => r.transform.IsChildOf(page.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Pool1")));
            Assert.IsFalse(page.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.name == "Species"));
            int coins = PokemonCollection.Progression.Coins;
            int count = PokemonCollection.Inventory.Count;
            yield return null;
            int initial = roulette.FeaturedIndex;
            yield return new WaitForSecondsRealtime(1.9f);
            Assert.AreNotEqual(initial, roulette.FeaturedIndex, "Preview must keep moving at timeScale zero.");
            Assert.AreEqual(coins, PokemonCollection.Progression.Coins);
            Assert.AreEqual(count, PokemonCollection.Inventory.Count);
            var portraits = roulette.GetComponentsInChildren<UnityEngine.UI.Image>().Where(i => i.name == "Portrait").ToArray();
            Assert.AreEqual(7, portraits.Length);
            Assert.IsTrue(portraits.All(i => i.sprite != null));
            Click("GachaMenu", "Back");
            Click("CollectionMenu", "Back");
            Click("MainMenu", "Gacha");
            Assert.AreEqual(7, roulette.GetComponentsInChildren<UnityEngine.UI.Image>().Count(i => i.name == "Portrait"));
        }

        void Click(string root, string name)
        {
            var button = CanvasRoot().Find(root).GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Single(b => b.name == name);
            Assert.IsTrue(button.gameObject.activeInHierarchy, name);
            Assert.IsTrue(button.interactable, name);
            button.onClick.Invoke();
        }

        static Transform CanvasRoot() => Object.FindObjectsByType<Canvas>()
            .Single(c => c.name == "UI_Menus").transform;
        static void AssertActive(string name) => Assert.IsTrue(CanvasRoot().Find(name).gameObject.activeSelf, name);
        void AssertPaused(string page)
        {
            AssertActive(page);
            Assert.IsTrue(_menu.IsPaused);
            Assert.IsTrue(GameFlow.Blocked);
            Assert.AreEqual(0, Time.timeScale);
            Assert.IsFalse(AudioListener.pause, "The DSP clock must keep running for the adaptive score.");
            Assert.IsTrue(_menu.GetComponent<Game.Audio.GameAudio>().CombatPaused);
            Assert.IsTrue(RoundManager.Instance.IsRunActive);
        }
        void AssertRunning()
        {
            Assert.IsFalse(_menu.IsPaused);
            Assert.IsFalse(GameFlow.Blocked);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
            Assert.IsFalse(_menu.GetComponent<Game.Audio.GameAudio>().CombatPaused);
        }
    }
}

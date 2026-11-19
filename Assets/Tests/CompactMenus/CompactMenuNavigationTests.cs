using System;
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
            Assert.IsTrue(AudioListener.pause);
            Assert.IsTrue(RoundManager.Instance.IsRunActive);
        }
        void AssertRunning()
        {
            Assert.IsFalse(_menu.IsPaused);
            Assert.IsFalse(GameFlow.Blocked);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
        }
    }
}

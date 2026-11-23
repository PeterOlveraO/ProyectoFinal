using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Economy;
using Game.Pokemon;
using Game.Pokemon.Progression;
using Game.Rounds;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>Owns navigation and user actions. Views only render progression snapshots.</summary>
    [DefaultExecutionOrder(-200), DisallowMultipleComponent]
    public sealed class GameMenuController : MonoBehaviour
    {
        const string VolumeKey = "pketer.volumen";
        const string QualityKey = "pketer.calidad";
        const string FullscreenKey = "pketer.linux.fullscreen";
        const string WindowWidthKey = "pketer.linux.windowWidth";
        const string WindowHeightKey = "pketer.linux.windowHeight";
        enum ScreenId { None, Main, Pause, Settings, Collection, Skills, Starter, Gacha, Result, Release, Credits, Tests }
        public static GameMenuController Instance { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsPaused => IsPlaying && _screen != ScreenId.None;

        readonly Dictionary<ScreenId, MenuPage> _pages = new Dictionary<ScreenId, MenuPage>();
        RectTransform _canvas;
        ScreenId _screen, _settingsOrigin, _skillsOrigin, _testsOrigin;
        CollectionView _collection;
        GachaView _gacha;
        TestMenuView _tests;
        PokemonDetailsView _resultDetails;
        UnityEngine.UI.Image _resultPortrait;
        TMPro.TMP_Text _resultBadge;
        UnityEngine.UI.Image[] _starterCards;
        IReadOnlyList<SpeciesDefinition> _catalog;
        IReadOnlyList<PokemonInstance> _inventory;
        IReadOnlyList<GachaPool> _pools;
        PokemonInstance _equipped;
        int _collectionIndex, _starterIndex;
        bool _busy, _starterResult;
        string _message = "", _pullRequest, _releaseTarget;
        int _quality;
        bool _fullscreen, _fullscreenPending;
        int _windowWidth = 1280, _windowHeight = 720;
        TMPro.TMP_Text _fullscreenValue;
        UnityEngine.UI.Slider _volumeSlider, _musicSlider, _effectsSlider;
        TMPro.TMP_Text _volumeValue, _musicValue, _effectsValue, _qualityValue, _starterInfo, _releaseInfo;
        UnityEngine.UI.Button _starterConfirm;
        UnityEngine.UI.Button[] _starterButtons, _skillButtons;
        UnityEngine.UI.Image[] _skillProgress;
        GameObject _pauseButton, _skillsButton;

        void Awake()
        {
            Instance = this;
            Game.Audio.GameAudio.EnsureExists(gameObject);
            Game.Weather.WeatherController.EnsureExists(gameObject);
            _quality = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()),
                0, Mathf.Max(0, QualitySettings.names.Length - 1));
            if (QualitySettings.names.Length > 0) QualitySettings.SetQualityLevel(_quality, true);
            InitializeLinuxDisplay();
            RefreshSnapshots();
            Build();
            PokemonCollection.Changed += OnCollectionChanged;
            ShowMainMenu();
        }

        void OnDestroy()
        {
            PokemonCollection.Changed -= OnCollectionChanged;
            if (_canvas != null) Destroy(_canvas.gameObject);
            if (Instance != this) return;
            Instance = null;
            GameFlow.Blocked = GameFlow.Over = false;
            Time.timeScale = 1;
        }

        void Update()
        {
            UpdateLinuxDisplay();
            bool controls = IsPlaying && _screen == ScreenId.None && !GameFlow.Over;
            if (_pauseButton.activeSelf != controls) _pauseButton.SetActive(controls);
            if (_skillsButton.activeSelf != controls) _skillsButton.SetActive(controls);
            bool back = Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                Gamepad.current?.buttonEast.wasPressedThisFrame == true;
            if (!back || GameFlow.Over) return;
            NavigateBack();
        }

        static bool IsLinuxPlayer => Application.platform == RuntimePlatform.LinuxPlayer;

        void InitializeLinuxDisplay()
        {
            if (!IsLinuxPlayer) return;
            _windowWidth = Mathf.Max(640, PlayerPrefs.GetInt(WindowWidthKey, 1280));
            _windowHeight = Mathf.Max(360, PlayerPrefs.GetInt(WindowHeightKey, 720));
            _fullscreen = Screen.fullScreen;
            if (PlayerPrefs.HasKey(FullscreenKey))
            {
                bool saved = PlayerPrefs.GetInt(FullscreenKey) != 0;
                if (saved != _fullscreen) SetLinuxFullscreen(saved);
            }
        }

        void UpdateLinuxDisplay()
        {
            if (!IsLinuxPlayer) return;
            // Unity applies display changes at the end of a frame. Keep the requested
            // state until it arrives, then also follow native window-manager switches.
            if (Screen.fullScreen == _fullscreen) _fullscreenPending = false;
            else if (!_fullscreenPending)
            {
                _fullscreen = Screen.fullScreen;
                PlayerPrefs.SetInt(FullscreenKey, _fullscreen ? 1 : 0);
                PlayerPrefs.Save();
                RefreshFullscreenLabel();
            }
            if (Keyboard.current?.f11Key.wasPressedThisFrame == true)
                SetLinuxFullscreen(!_fullscreen);
        }

        void SetLinuxFullscreen(bool fullscreen)
        {
            if (!IsLinuxPlayer) return;
            if (fullscreen && !Screen.fullScreen && !_fullscreenPending)
            {
                _windowWidth = Screen.width;
                _windowHeight = Screen.height;
                PlayerPrefs.SetInt(WindowWidthKey, _windowWidth);
                PlayerPrefs.SetInt(WindowHeightKey, _windowHeight);
            }
            var desktop = Screen.currentResolution;
            int width = fullscreen ? desktop.width : Mathf.Min(_windowWidth, desktop.width);
            int height = fullscreen ? desktop.height : Mathf.Min(_windowHeight, desktop.height);
            _fullscreen = fullscreen;
            _fullscreenPending = true;
            Screen.SetResolution(width, height,
                fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            PlayerPrefs.Save();
            RefreshFullscreenLabel();
        }

        void RefreshFullscreenLabel()
        {
            if (_fullscreenValue != null)
                _fullscreenValue.text = "Pantalla completa\n" + (_fullscreen ? "SÍ" : "NO") + " · F11";
        }

        void NavigateBack()
        {
            switch (_screen)
            {
                case ScreenId.None: Pause(); break;
                case ScreenId.Pause: Resume(); break;
                case ScreenId.Settings: CloseSettings(); break;
                case ScreenId.Collection: ShowMainMenu(); break;
                case ScreenId.Credits: ShowMainMenu(); break;
                case ScreenId.Gacha: OpenCollection(); break;
                case ScreenId.Release: CancelRelease(); break;
                case ScreenId.Result: CloseResult(); break;
                case ScreenId.Skills: CloseSkills(); break;
                case ScreenId.Tests: CloseTests(); break;
            }
        }

        MenuPage Add(ScreenId id, string name, string title, string subtitle, CompactMenuKind? compact = null)
        {
            var page = new MenuPage(_canvas, name, title, subtitle, compact);
            _pages.Add(id, page);
            return page;
        }

        void Show(ScreenId next)
        {
            if (_screen == ScreenId.Settings && next != ScreenId.Settings) PlayerPrefs.Save();
            _screen = next;
            bool blocked = next != ScreenId.None || GameFlow.Over;
            GameFlow.Blocked = blocked;
            Time.timeScale = blocked ? 0 : 1;
            foreach (var entry in _pages) entry.Value.Root.gameObject.SetActive(entry.Key == next);
            if (next == ScreenId.None && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            Game.Audio.GameAudio.SyncState();
            if (_pages.TryGetValue(next, out var page))
            {
                page.Wallet.text = PokemonCollection.Progression.IsReady
                    ? $"{PlayerEconomy.Coins:N0} MONEDAS" : "SALDO NO DISPONIBLE";
                page.Focus();
            }
        }

        public void ShowMainMenu()
        {
            IsPlaying = false;
            GameFlow.Over = false;
            RoundManager.Instance?.StopRun();
            if (Game.Weather.WeatherController.Instance != null)
                Game.Weather.WeatherController.Instance.ResetWeather();
            RefreshSnapshots();
            RefreshHero();
            if (!PokemonCollection.Progression.IsReady || PokemonCollection.NeedsStarter)
            {
                RefreshStarter();
                Show(ScreenId.Starter);
            }
            else Show(ScreenId.Main);
        }

        public void StartGame()
        {
            if (!PokemonCollection.Progression.IsReady || PokemonCollection.NeedsStarter || PokemonCollection.Equipped == null)
            { ShowMainMenu(); return; }
            RoundManager.Instance?.StartRun();
            if (RoundManager.Instance == null || !RoundManager.Instance.IsRunActive)
            {
                ShowMainMenu();
                _pages[ScreenId.Main].Status.text = "No se pudo iniciar la partida. Revisa el progreso y la configuración de combate.";
                return;
            }
            IsPlaying = true;
            Show(ScreenId.None);
            Game.Audio.GameAudio.PlayCry(RoundManager.Instance.RunSpecies?.id);
        }

        public void Pause()
        {
            if (IsPlaying && !GameFlow.Over) Show(ScreenId.Pause);
        }
        public void Resume()
        {
            if (IsPlaying && !GameFlow.Over) Show(ScreenId.None);
        }
        public void AbandonRun()
        {
            if (!IsPlaying) return;
            RoundManager.Instance?.AbandonRun();
            ShowMainMenu();
        }
        void OpenSettings()
        {
            _settingsOrigin = _screen;
            RefreshSettings();
            Show(ScreenId.Settings);
        }
        void CloseSettings() => Show(_settingsOrigin);
        void OpenSkills()
        {
            if (!IsPlaying || RoundManager.Instance == null || !RoundManager.Instance.IsRunActive) return;
            _skillsOrigin = _screen;
            RefreshSkills();
            Show(ScreenId.Skills);
        }
        void CloseSkills() => Show(_skillsOrigin);
        void OpenTests()
        {
            // Do not bypass the one-time starter choice, nor replace an active run's snapshot.
            if ((_screen != ScreenId.Main && _screen != ScreenId.Pause) ||
                !PokemonCollection.Progression.IsReady || PokemonCollection.NeedsStarter) return;
            _testsOrigin = _screen;
            RefreshTests();
            _tests.SetFeedback("Colección normal · guardado automático.");
            Show(ScreenId.Tests);
        }
        void CloseTests() => Show(_testsOrigin);
        void RefreshTests() => _tests.Bind(_catalog, PlayerEconomy.Coins, _inventory.Count,
            PokemonCollection.InventoryCapacity, PokemonCollection.Progression.IsReady && !_busy);
        void GrantTestCoins(int amount) => GrantTestReward(amount);
        void GrantTestPokemon(string speciesId, Rarity rarity) => GrantTestReward(0, speciesId, rarity);
        void GrantTestReward(int coins, string speciesId = null, Rarity rarity = Rarity.F)
        {
            if (_busy || _screen != ScreenId.Tests) return;
            _busy = true;
            OperationResult result;
            try { result = PokemonCollection.GrantForTesting(coins, speciesId, rarity); }
            finally { _busy = false; }
            RefreshSnapshots();
            RefreshTests();
            _tests.SetFeedback(result.success
                ? result.instance == null ? $"+{coins:N0} monedas guardadas."
                    : $"{Species(result.instance.speciesId)?.name} · clase {result.instance.rarity} añadido a la colección."
                : ErrorMessage(result.error), !result.success);
        }
        void QuitGame()
        {
            PlayerEconomy.Flush();
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void RefreshSnapshots()
        {
            _catalog = PokemonCollection.Catalog;
            _inventory = PokemonCollection.Inventory;
            _equipped = PokemonCollection.Equipped;
            _pools = PokemonCollection.GetPools();
        }
        SpeciesDefinition Species(string id) => _catalog.FirstOrDefault(s => s.id == id);
        void OnCollectionChanged()
        {
            RefreshSnapshots();
            foreach (var page in _pages.Values) page.Wallet.text = $"{PlayerEconomy.Coins:N0} MONEDAS";
            RefreshHero();
            if (_screen == ScreenId.Collection) RefreshCollection();
            if (_screen == ScreenId.Gacha) RefreshGacha();
            if (_screen == ScreenId.Tests) RefreshTests();
        }

        void OpenCollection()
        {
            _message = "";
            RefreshSnapshots();
            RefreshCollection();
            Show(ScreenId.Collection);
        }
        void SelectPokemon(int index)
        {
            if (index < 0 || index >= _inventory.Count) return;
            _collectionIndex = index;
            _message = "";
            RefreshCollection();
            Game.Audio.GameAudio.PlayCry(_inventory[index].speciesId);
        }
        void RefreshCollection()
        {
            _collectionIndex = Mathf.Clamp(_collectionIndex, 0, Mathf.Max(0, _inventory.Count - 1));
            _collection.Bind(_inventory, _equipped, Species, _collectionIndex,
                PokemonCollection.InventoryCapacity, PlayerEconomy.Coins, _busy, _message);
        }
        void EquipPokemon()
        {
            if (_busy || _inventory.Count == 0) return;
            _busy = true;
            var result = PokemonCollection.Equip(_inventory[_collectionIndex].instanceId);
            _busy = false;
            _message = result.success ? "" : ErrorMessage(result.error);
            RefreshCollection();
        }
        void AskRelease()
        {
            if (_busy || _inventory.Count <= 1) return;
            var pokemon = _inventory[_collectionIndex];
            _releaseTarget = pokemon.instanceId;
            _releaseInfo.text = $"¿Liberar a {Species(pokemon.speciesId)?.name}?\n\nNo se puede deshacer.";
            Show(ScreenId.Release);
        }
        void CancelRelease()
        {
            _releaseTarget = null;
            RefreshCollection();
            Show(ScreenId.Collection);
        }
        void ReleasePokemon()
        {
            if (_busy || string.IsNullOrEmpty(_releaseTarget)) return;
            _busy = true;
            var target = _releaseTarget;
            _releaseTarget = null;
            var result = PokemonCollection.Release(target);
            _busy = false;
            _message = result.success ? "" : ErrorMessage(result.error);
            RefreshCollection();
            Show(ScreenId.Collection);
        }

        void RefreshStarter()
        {
            bool ready = PokemonCollection.Progression.IsReady && _starterButtons.Length > 0;
            _starterConfirm.interactable = ready && !_busy;
            if (!ready)
            {
                _starterInfo.text = "No se pudo cargar el progreso. Reinicia y revisa el almacenamiento.";
                return;
            }
            _starterIndex = Mathf.Clamp(_starterIndex, 0, _starterButtons.Length - 1);
            for (int i = 0; i < _starterButtons.Length; i++)
            {
                _starterButtons[i].image.color = Color.white;
                _starterCards[i].color = UIFactory.Panel;
                var text = _starterButtons[i].GetComponentInChildren<TMPro.TMP_Text>();
                text.color = i == _starterIndex ? UIFactory.Sun : UIFactory.Cream;
                text.text = i == _starterIndex ? "ELEGIDO" : "ELEGIR";
            }
            _starterInfo.text = _message;
        }
        void ConfirmStarter()
        {
            if (_busy || !PokemonCollection.Progression.IsReady || !PokemonCollection.NeedsStarter) return;
            var ids = PokemonCollection.StarterIds;
            if (_starterIndex >= ids.Count) return;
            _busy = true;
            var result = PokemonCollection.ChooseStarter(ids[_starterIndex]);
            _busy = false;
            if (!result.success) { _message = ErrorMessage(result.error); RefreshStarter(); return; }
            ShowResult(result.instance, true);
        }
        void OpenGacha()
        {
            _message = "";
            RefreshSnapshots();
            RefreshGacha();
            Show(ScreenId.Gacha);
        }
        void RefreshGacha() => _gacha.Bind(_pools, Species, PlayerEconomy.Coins, _inventory.Count,
            PokemonCollection.InventoryCapacity, _busy || _pullRequest != null, PokemonCollection.Progression.IsReady, _message);
        void PullPool(int index)
        {
            if (_busy || _pullRequest != null || index < 0 || index >= _pools.Count) return;
            _busy = true;
            _pullRequest = Guid.NewGuid().ToString("N");
            var result = PokemonCollection.Pull(_pools[index].id, _pullRequest);
            _busy = false;
            if (!result.success)
            {
                _pullRequest = null;
                _message = ErrorMessage(result.error);
                RefreshGacha();
                return;
            }
            ShowResult(result.instance, false);
        }
        void ShowResult(PokemonInstance pokemon, bool starter)
        {
            _starterResult = starter;
            RefreshSnapshots();
            _resultDetails.Root.gameObject.SetActive(!starter);
            if (!starter) _resultDetails.Bind(pokemon, Species(pokemon.speciesId), false, "");
            var resultPage = _pages[ScreenId.Result];
            resultPage.SetSize(starter ? new Vector2(520, 480) : new Vector2(1060, 680));
            UIFactory.Region((RectTransform)_resultPortrait.transform.parent.parent, starter ? .12f : 0, 0, starter ? .88f : .42f, 1);
            _resultPortrait.sprite = PokemonCollection.GetSprite(pokemon);
            _resultPortrait.enabled = _resultPortrait.sprite != null;
            _resultBadge.text = Species(pokemon.speciesId)?.name ?? pokemon.speciesId;
            _resultBadge.color = UIFactory.Cream;
            Show(ScreenId.Result);
            Game.Audio.GameAudio.PlayCry(pokemon.speciesId);
        }
        void CloseResult()
        {
            _pullRequest = null;
            _message = "";
            if (_starterResult) ShowMainMenu();
            else OpenGacha();
        }

        void Build()
        {
            _canvas = UIFactory.CreateCanvas("UI_Menus", 100);
            var controls = UIFactory.SafeRoot(_canvas, "RunControls");
            var pause = CompactMenuArt.Button(controls, "PauseButton", "PAUSA", "Amber", Pause);
            pause.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            _pauseButton = pause.gameObject;
            var skills = CompactMenuArt.Button(controls, "RunSkillsButton", "EV", "Blue", OpenSkills);
            skills.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            _skillsButton = skills.gameObject;
            controls.gameObject.AddComponent<RunMenuControlsLayout>().Configure(pause, skills);
            BuildMain();
            BuildPause();
            BuildSettings();
            _tests = TestMenuView.Create(_canvas, GrantTestCoins, GrantTestPokemon, CloseTests);
            _pages.Add(ScreenId.Tests, _tests.Page);
            _pages.Add(ScreenId.Credits, new CreditsView(_canvas, ShowMainMenu).Page);
            _collection = new CollectionView(_canvas, SelectPokemon, EquipPokemon, AskRelease, OpenGacha, ShowMainMenu);
            _pages.Add(ScreenId.Collection, _collection.Page);
            _gacha = new GachaView(_canvas, PullPool, OpenCollection);
            _pages.Add(ScreenId.Gacha, _gacha.Page);
            BuildStarter();
            var result = Add(ScreenId.Result, "ResultMenu", "", "");
            var showcase = UIFactory.Surface("Acquisition", result.Body, UIFactory.CardViolet);
            UIFactory.Region(showcase, 0, 0, .34f, 1);
            _resultPortrait = UIFactory.PortraitStage(showcase, "RevealStage", null);
            UIFactory.Region((RectTransform)_resultPortrait.transform.parent, .06f, .22f, .94f, .98f);
            _resultPortrait.rectTransform.gameObject.AddComponent<UIReveal>();
            _resultBadge = UIFactory.CreateText("AcquisitionBadge", showcase, "", 30, UIFactory.Cream);
            UIFactory.Region(_resultBadge.rectTransform, .06f, .04f, .94f, .22f);
            _resultDetails = new PokemonDetailsView(result.Body, false);
            UIFactory.Region(_resultDetails.Root, .44f, 0, 1, 1);
            result.Action("Continue", "CONTINUAR", UIFactory.Sun, CloseResult);
            var release = Add(ScreenId.Release, "ReleaseMenu", "Liberar Pokémon", "Revisa tu elección antes de continuar.");
            var releaseCard = UIFactory.Surface("Confirmation", release.Body);
            UIFactory.Region(releaseCard, 0, 0, 1, 1);
            var releaseContent = UIFactory.ScrollStack(releaseCard, 32, 20);
            _releaseInfo = UIFactory.Label(releaseContent, "Summary", "", 28, UIFactory.Cream, 160);
            UIFactory.LayoutHeight(_releaseInfo, 160);
            release.Action("Cancel", "Conservar", UIFactory.Sky, CancelRelease);
            release.Action("Confirm", "Liberar", UIFactory.Coral, ReleasePokemon);
            BuildSkills();
        }

        void BuildMain()
        {
            var page = Add(ScreenId.Main, "MainMenu", "PKETER", "", CompactMenuKind.Main);
            CompactMenuArt.Button(page.Body, "PlayButton", "JUGAR", "Green", StartGame);
            CompactMenuArt.Button(page.Body, "Collection", "Colección", "Blue", OpenCollection);
            CompactMenuArt.Button(page.Body, "Gacha", "Gacha", "Blue", OpenGacha);
            CompactMenuArt.Button(page.Body, "Settings", "Ajustes", "Amber", OpenSettings);
            CompactMenuArt.Button(page.Body, "Credits", "Créditos", "Amber", () => Show(ScreenId.Credits));
            CompactMenuArt.Button(page.Body, "Tests", "PRUEBAS", "Blue", OpenTests);
            CompactMenuArt.Button(page.Body, "Quit", "Salir", "Red", QuitGame);
        }
        void RefreshHero()
        {
            if (!_pages.TryGetValue(ScreenId.Main, out var page)) return;
            page.Status.text = _equipped == null ? "" : Species(_equipped.speciesId)?.name ?? _equipped.speciesId;
        }
        void BuildPause()
        {
            var page = Add(ScreenId.Pause, "PauseMenu", "PAUSA", "", CompactMenuKind.Pause);
            CompactMenuArt.Button(page.Body, "Resume", "CONTINUAR", "Green", Resume);
            CompactMenuArt.Button(page.Body, "Skills", "Mejoras EV", "Blue", OpenSkills);
            CompactMenuArt.Button(page.Body, "Settings", "Ajustes", "Amber", OpenSettings);
            CompactMenuArt.Button(page.Body, "Tests", "PRUEBAS", "Blue", OpenTests);
            CompactMenuArt.Button(page.Body, "Abandon", "Salir de la partida", "Red", AbandonRun);
        }
        void BuildStarter()
        {
            var page = Add(ScreenId.Starter, "StarterMenu", "Elegir inicial", "");
            var row = UIFactory.CreateRect("Starters", page.Body);
            UIFactory.Region(row, 0, .08f, 1, 1);
            UIFactory.Row(row, 24);
            var ids = PokemonCollection.StarterIds;
            _starterButtons = new UnityEngine.UI.Button[ids.Count];
            _starterCards = new UnityEngine.UI.Image[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                int index = i;
                var card = UIFactory.Surface("Starter" + i, row);
                var size = card.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                size.minWidth = size.preferredWidth = 0;
                size.flexibleWidth = 1;
                _starterCards[i] = card.GetComponent<UnityEngine.UI.Image>();
                var name = UIFactory.CreateText("Name", card, Species(ids[i])?.name ?? ids[i], 36, UIFactory.Cream);
                UIFactory.Region(name.rectTransform, .06f, .74f, .94f, .92f);
                var image = UIFactory.PortraitStage(card, "StarterStage",
                    PokemonCollection.GetSprite(new PokemonInstance { speciesId = ids[i] }));
                UIFactory.Region((RectTransform)image.transform.parent, .06f, .30f, .94f, .74f);
                _starterButtons[i] = UIFactory.CreateButton(card, "Select", "ELEGIR", UIFactory.CardViolet,
                    new Vector2(320, 76), () =>
                    {
                        _starterIndex = index;
                        _message = "";
                        RefreshStarter();
                        Game.Audio.GameAudio.PlayCry(ids[index]);
                    }, 28);
                var choose = (RectTransform)_starterButtons[i].transform;
                UIFactory.Region(choose, .06f, 0, .94f, 0);
                choose.pivot = new Vector2(.5f, 0);
                choose.offsetMin = new Vector2(0, 12);
                choose.offsetMax = new Vector2(0, 76);
            }
            _starterInfo = UIFactory.CreateText("Odds", page.Body, "", 25, UIFactory.Muted);
            UIFactory.Region(_starterInfo.rectTransform, 0, 0, 1, .07f);
            _starterConfirm = page.Action("ConfirmStarter", "CONFIRMAR", UIFactory.Sun, ConfirmStarter);
        }
        void BuildSettings()
        {
            var page = Add(ScreenId.Settings, "SettingsMenu", "Ajustes", "");
            var audio = UIFactory.Surface("AudioSettings", page.Body);
            UIFactory.Region(audio, 0, 0, .62f, 1);
            var stack = UIFactory.ScrollStack(audio, 18, 8);
            _volumeValue = UIFactory.Label(stack, "Volume", "", 28, UIFactory.Cream, 44);
            _volumeSlider = UIFactory.CreateSlider(stack, new Vector2(600, 64), Game.Audio.GameAudio.MasterVolume, value =>
            {
                Game.Audio.GameAudio.SetMasterVolume(value);
                PlayerPrefs.SetFloat(VolumeKey, value);
                RefreshSettings();
            });
            _volumeSlider.name = "MasterVolumeSlider";
            UIFactory.Layout(_volumeSlider.transform, 64);
            _musicValue = UIFactory.Label(stack, "MusicVolume", "", 28, UIFactory.Cream, 44);
            _musicSlider = UIFactory.CreateSlider(stack, new Vector2(600, 64), Game.Audio.GameAudio.MusicVolume, value =>
            {
                Game.Audio.GameAudio.SetMusicVolume(value);
                RefreshSettings();
            });
            _musicSlider.name = "MusicVolumeSlider";
            UIFactory.Layout(_musicSlider.transform, 64);
            _effectsValue = UIFactory.Label(stack, "EffectsVolume", "", 28, UIFactory.Cream, 44);
            _effectsSlider = UIFactory.CreateSlider(stack, new Vector2(600, 64), Game.Audio.GameAudio.EffectsVolume, value =>
            {
                Game.Audio.GameAudio.SetEffectsVolume(value);
                RefreshSettings();
            });
            _effectsSlider.name = "EffectsVolumeSlider";
            UIFactory.Layout(_effectsSlider.transform, 64);
            var visual = UIFactory.Surface("VisualSettings", page.Body);
            UIFactory.Region(visual, .64f, 0, 1, 1);
            var visualStack = UIFactory.ScrollStack(visual, 28, 18);
            UIFactory.Label(visualStack, "QualityTitle", "Calidad", 28, UIFactory.Cream, 44);
            var quality = StackButton(visualStack, "Quality", "", UIFactory.CardViolet, () =>
            {
                if (QualitySettings.names.Length <= 1) return;
                _quality = (_quality + 1) % QualitySettings.names.Length;
                QualitySettings.SetQualityLevel(_quality, true);
                PlayerPrefs.SetInt(QualityKey, _quality);
                RefreshSettings();
            });
            _qualityValue = quality.GetComponentInChildren<TMPro.TMP_Text>();
            if (IsLinuxPlayer)
            {
                UIFactory.Label(visualStack, "DisplayTitle", "Pantalla", 28, UIFactory.Cream, 44);
                var fullscreen = StackButton(visualStack, "Fullscreen", "", UIFactory.Sky,
                    () => SetLinuxFullscreen(!_fullscreen));
                _fullscreenValue = fullscreen.GetComponentInChildren<TMPro.TMP_Text>();
                RefreshFullscreenLabel();
            }
            page.Action("Back", "Volver", UIFactory.Sun, CloseSettings);
            RefreshSettings();
        }
        void RefreshSettings()
        {
            _volumeValue.text = $"General  /  {Mathf.RoundToInt(Game.Audio.GameAudio.MasterVolume * 100)}%";
            _volumeSlider.SetValueWithoutNotify(Game.Audio.GameAudio.MasterVolume);
            _musicValue.text = $"Música  /  {Mathf.RoundToInt(Game.Audio.GameAudio.MusicVolume * 100)}%";
            _musicSlider.SetValueWithoutNotify(Game.Audio.GameAudio.MusicVolume);
            _effectsValue.text = $"Efectos  /  {Mathf.RoundToInt(Game.Audio.GameAudio.EffectsVolume * 100)}%";
            _effectsSlider.SetValueWithoutNotify(Game.Audio.GameAudio.EffectsVolume);
            if (_qualityValue != null) _qualityValue.text = QualitySettings.names.Length > 0 ? QualitySettings.names[_quality] : "Predeterminada";
        }
        void BuildSkills()
        {
            var page = Add(ScreenId.Skills, "SkillsMenu", "MEJORAS EV", "", CompactMenuKind.Skills);
            string[] names = { "SALUD", "ATAQUE", "DEFENSA", "VELOCIDAD" };
            _skillButtons = new UnityEngine.UI.Button[names.Length];
            _skillProgress = new UnityEngine.UI.Image[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int stat = i;
                _skillButtons[i] = CompactMenuArt.Button(page.Body, "EV_" + names[i], "", "Blue",
                    () => { RoundManager.Instance?.BuyEV(stat); RefreshSkills(); });
                var label = _skillButtons[i].GetComponentInChildren<TMPro.TMP_Text>();
                label.rectTransform.offsetMin = new Vector2(20, 18);
                var track = UIFactory.CreateImage("LevelTrack", _skillButtons[i].transform, null, UIFactory.Ink);
                UIFactory.Region(track.rectTransform, .08f, 0, .92f, 0);
                track.rectTransform.offsetMin = new Vector2(0, 8);
                track.rectTransform.offsetMax = new Vector2(0, 12);
                _skillProgress[i] = UIFactory.CreateImage("LevelFill", track.transform, null, UIFactory.Sun);
                UIFactory.Stretch(_skillProgress[i].rectTransform);
            }
            page.Action("Back", "Volver", UIFactory.Sun, CloseSkills);
        }
        void RefreshSkills()
        {
            var round = RoundManager.Instance;
            if (round == null) return;
            _pages[ScreenId.Skills].Status.text = $"{round.Points} PT";
            string[] names = { "SALUD", "ATAQUE", "DEFENSA", "VELOCIDAD" };
            for (int i = 0; i < 4; i++)
            {
                int level = round.EVLevel(i);
                _skillButtons[i].GetComponentInChildren<TMPro.TMP_Text>().text = $"{names[i]}\n{level}/10 · " +
                    (level >= 10 ? "MÁX." : $"{round.EVCost(i)} PT");
                _skillButtons[i].interactable = round.CanBuyEV(i);
                _skillProgress[i].rectTransform.anchorMax = new Vector2(Mathf.Clamp01(level / 10f), 1);
                _skillProgress[i].color = level >= 10 ? UIFactory.Lime : UIFactory.Sun;
            }
        }
        static UnityEngine.UI.Button StackButton(Transform parent, string name, string text, Color color,
            UnityEngine.Events.UnityAction action)
        {
            var button = UIFactory.CreateButton(parent, name, text, color, new Vector2(500, 84), action, 30);
            UIFactory.Layout(button.transform, 84);
            return button;
        }
        static string ErrorMessage(string error)
        {
            switch (error)
            {
                case "InventoryFull": return "Colección llena. Libera un Pokémon primero.";
                case "InsufficientCoins": return "No tienes suficientes monedas.";
                case "LastInstance": return "No puedes liberar tu último Pokémon.";
                case "StarterRequired": return "Elige primero tu Pokémon inicial.";
                case "InvalidAmount": return "Escribe una cantidad entera positiva.";
                case "CoinOverflow": return "La cantidad supera el saldo máximo de monedas.";
                case "InvalidSpecies": return "Elige una especie del catálogo.";
                case "InvalidRarity": return "Elige una clase F, A o S.";
                case "PersistenceFailure":
                case "SaveUnavailable": return "No se pudo guardar. Reinicia y revisa el almacenamiento.";
                case "InstanceNotFound": return "Ese Pokémon ya no está en tu colección.";
                default: return "No se pudo completar la operación.";
            }
        }
    }
}

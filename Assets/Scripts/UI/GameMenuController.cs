using Game.Core;
using Game.Economy;
using Game.Rendering;
using Game.Rounds;
using Game.Pokemon;
using Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Menú principal, pausa y configuración, construidos por código (sin imágenes).
    ///
    /// Flujo:
    ///   · Al arrancar se muestra el MENÚ PRINCIPAL con Jugar / Configuración / Salir.
    ///   · Jugar congela la partida (GameFlow.Blocked) y muestra el HUD de juego.
    ///   · El botón de pausa (arriba a la derecha) o Escape abre PAUSA:
    ///     Continuar / Configuración / Salir.
    ///
    /// Mientras hay un menú abierto la partida no avanza: Time.timeScale = 0.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class GameMenuController : MonoBehaviour
    {
        const string VolumeKey = "pketer.volumen";
        const string QualityKey = "pketer.calidad";

        public static GameMenuController Instance { get; private set; }

        enum SettingsOrigin { MainMenu, Pause }

        RectTransform _canvas;
        RectTransform _safeCanvas;
        RectTransform _mainPanel;
        RectTransform _pausePanel;
        RectTransform _confirmPanel;
        RectTransform _settingsPanel;
        RectTransform _collectionPanel;
        RectTransform _skillsPanel;
        Text _collectionInfo;
        Image _pokemonPreview;
        Text _skillsInfo;
        Button _buyPokemonButton;
        Button[] _skillButtons;
        int _collectionIndex;
        GameObject _pauseButton;
        GameObject _skillsButton;

        Text _volumeValue;
        Text _qualityValue;
        Text _walletText;
        Text _pauseInfo;
        Image _mainPokemonPreview;
        Text _mainPokemonName;
        Slider _volumeSlider;
        SettingsOrigin _settingsOrigin;

        bool _playing;
        bool _paused;
        bool _settingsOpen;
        bool _subMenuOpen;
        bool _skillsFromPause;

        float _volume = 0.8f;
        int _quality;

        public bool IsPlaying => _playing;
        public bool IsPaused => _paused;

        void Awake()
        {
            Instance = this;

            _volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            _quality = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()),
                                   0, Mathf.Max(0, QualitySettings.names.Length - 1));

            AudioListener.volume = _volume;
            if (QualitySettings.names.Length > 0) QualitySettings.SetQualityLevel(_quality, true);

            Build();
            ShowMainMenu();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;

            GameFlow.Blocked = false;
            GameFlow.Over = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        void Update()
        {
            RefreshPauseButton();
            if (GameFlow.Over) return;

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            if (_settingsOpen)
            {
                CloseSettings();
                return;
            }

            if (_confirmPanel != null && _confirmPanel.gameObject.activeSelf)
            {
                CancelAbandon();
                return;
            }

            if (_subMenuOpen) { CloseSubMenu(); return; }

            if (!_playing) return;
            if (_paused) Resume();
            else Pause();
        }

        // --------------------------------------------------------------- Flujo
        public void ShowMainMenu()
        {
            _playing = false;
            _paused = false;
            _settingsOpen = false;
            _subMenuOpen = false;

            GameFlow.Over = false;
            GameFlow.Blocked = true;
            Time.timeScale = 0f;
            AudioListener.pause = false;

            RoundManager.Instance?.StopRun();
            RefreshWallet();
            RefreshHero();
            ReleaseJoysticks();

            ShowPanel(_mainPanel);
            RefreshPauseButton();
        }

        public void StartGame()
        {
            _playing = true;
            _paused = false;
            _settingsOpen = false;
            _subMenuOpen = false;

            GameFlow.Over = false;
            GameFlow.Blocked = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;

            HideAllPanels();
            RefreshPauseButton();

            RoundManager.Instance?.StartRun();
        }

        public void Pause()
        {
            if (!_playing || GameFlow.Over) return;

            _paused = true;
            _settingsOpen = false;
            GameFlow.Blocked = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            ReleaseJoysticks();

            var round = RoundManager.Instance;
            if (_pauseInfo != null && round != null)
                _pauseInfo.text = "RONDA " + round.Round + "  ·  " + round.EnemiesDefeated +
                                  " DERROTADOS  ·  " + round.Points + " PUNTOS";

            ShowPanel(_pausePanel);
            RefreshPauseButton();
        }

        public void Resume()
        {
            if (!_playing) return;

            _paused = false;
            _settingsOpen = false;
            _subMenuOpen = false;
            GameFlow.Blocked = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;

            HideAllPanels();
            RefreshPauseButton();
        }

        void OpenSettings(SettingsOrigin origin)
        {
            _settingsOrigin = origin;
            _settingsOpen = true;
            GameFlow.Blocked = true;
            ReleaseJoysticks();
            ShowPanel(_settingsPanel);
            RefreshPauseButton();
        }

        void CloseSettings()
        {
            _settingsOpen = false;
            if (_settingsOrigin == SettingsOrigin.Pause) ShowPanel(_pausePanel);
            else ShowPanel(_mainPanel);
            RefreshPauseButton();
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

        public void AbandonRun()
        {
            if (!_playing) return;
            RoundManager.Instance?.AbandonRun();
            ShowMainMenu();
        }

        void HideAllPanels()
        {
            _mainPanel.gameObject.SetActive(false);
            _pausePanel.gameObject.SetActive(false);
            _settingsPanel.gameObject.SetActive(false);
            _collectionPanel.gameObject.SetActive(false);
            _skillsPanel.gameObject.SetActive(false);
            _confirmPanel.gameObject.SetActive(false);
        }

        void ShowPanel(RectTransform panel)
        {
            HideAllPanels();
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
        }

        void RefreshPauseButton()
        {
            if (_pauseButton != null)
                _pauseButton.SetActive(_playing && !_paused && !_settingsOpen && !_subMenuOpen && !GameFlow.Over);
            if (_skillsButton != null)
                _skillsButton.SetActive(_playing && !_paused && !_settingsOpen && !_subMenuOpen && !GameFlow.Over);
        }

        // ------------------------------------------------------------------ UI
        void Build()
        {
            _canvas = UIFactory.CreateCanvas("UI_Menus", 100);
            _safeCanvas = UIFactory.CreateSafeArea(_canvas);

            BuildPauseButton();
            BuildSkillsButton();
            BuildMainMenu();
            BuildPauseMenu();
            BuildConfirmAbandon();
            BuildSettingsMenu();
            BuildCollectionMenu();
            BuildSkillsMenu();

            _pausePanel.gameObject.SetActive(false);
            _settingsPanel.gameObject.SetActive(false);
            _collectionPanel.gameObject.SetActive(false);
            _skillsPanel.gameObject.SetActive(false);
            _confirmPanel.gameObject.SetActive(false);
        }

        void BuildPauseButton()
        {
            var rect = UIFactory.CreateRect("PauseButton", _safeCanvas);
            UIFactory.Place(rect, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                            new Vector2(-90f, -90f), new Vector2(116f, 116f));

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ProceduralSprites.Circle;
            image.color = UIFactory.Sun;
            UIFactory.AddShadow(image, new Color(0f, 0f, 0f, 0.4f), new Vector2(0f, -6f));
            UIFactory.AddOutline(image, UIFactory.Ink, 3f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(Pause);

            var label = UIFactory.CreateText("Label", rect, "II", 54, UIFactory.Ink);
            UIFactory.Stretch(label.rectTransform);

            _pauseButton = rect.gameObject;
        }

        void BuildSkillsButton()
        {
            var button = UIFactory.CreateButton(_safeCanvas, "RunSkillsButton", "EVs", UIFactory.Sky,
                                                new Vector2(150f, 92f), OpenSkills, 35);
            UIFactory.Place((RectTransform)button.transform, new Vector2(1f, 1f), new Vector2(.5f, .5f),
                            new Vector2(-240f, -90f), new Vector2(150f, 92f));
            _skillsButton = button.gameObject;
        }

        void BuildMainMenu()
        {
            _mainPanel = CreateOverlay("MainMenu");
            var root = _mainPanel;
            var background = UIFactory.CreateImage("Background", root,
                ProceduralSprites.VerticalGradient(new Color32(24, 13, 43, 255),
                                                   new Color32(77, 39, 127, 255)),
                Color.white, true);
            UIFactory.Stretch(background.rectTransform);
            var glow = UIFactory.CreateImage("Glow", root, ProceduralSprites.Glow,
                new Color(1f, 0.53f, 0.28f, 0.28f), false);
            UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(500f, -100f), new Vector2(1200f, 900f));
            BuildSparkles(root);
            var content = UIFactory.CreateSafeArea(root);
            var title = UIFactory.CreateText("Title", content, "PKETER", 142, UIFactory.Sun);
            UIFactory.Place(title.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, 410f), new Vector2(1000f, 190f));
            UIFactory.AddOutline(title, UIFactory.Ink, 6f);
            var subtitle = UIFactory.CreateText("Subtitle", content, "POKÉMON  ·  SUPERVIVENCIA", 35, UIFactory.Cream);
            UIFactory.Place(subtitle.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, 292f), new Vector2(1200f, 55f));

            var actionsAnchor = UIFactory.CreateRect("ActionsColumn", content);
            UIFactory.Place(actionsAnchor, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(-390f, -105f), new Vector2(730f, 640f));
            var actions = UIFactory.CreateCard(actionsAnchor, new Vector2(720f, 625f),
                                               UIFactory.DeepViolet, UIFactory.Sky);
            var heading = UIFactory.CreateText("ActionsHeading", actions, "TU AVENTURA", 43, UIFactory.Sun);
            UIFactory.Place(heading.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, 225f), new Vector2(610f, 65f));
            var play = UIFactory.CreateButton(actions, "PlayButton", "JUGAR", UIFactory.Lime,
                                             new Vector2(570f, 100f), StartGame, 48);
            UIFactory.Place((RectTransform)play.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, 125f), new Vector2(570f, 100f));
            var collection = UIFactory.CreateButton(actions, "CollectionButton", "MI EQUIPO POKÉMON", UIFactory.Sky,
                                                    new Vector2(570f, 92f), OpenCollection, 37);
            UIFactory.Place((RectTransform)collection.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, 12f), new Vector2(570f, 92f));
            var settings = UIFactory.CreateButton(actions, "SettingsButton", "AJUSTES", UIFactory.Sun,
                                                  new Vector2(570f, 92f),
                                                  () => OpenSettings(SettingsOrigin.MainMenu), 38);
            UIFactory.Place((RectTransform)settings.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, -100f), new Vector2(570f, 92f));
            var quit = UIFactory.CreateButton(actions, "QuitButton", "SALIR", UIFactory.Coral,
                                              new Vector2(570f, 80f), QuitGame, 36);
            UIFactory.Place((RectTransform)quit.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, -211f), new Vector2(570f, 80f));

            var heroAnchor = UIFactory.CreateRect("HeroColumn", content);
            UIFactory.Place(heroAnchor, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(390f, -105f), new Vector2(650f, 640f));
            var hero = UIFactory.CreateCard(heroAnchor, new Vector2(640f, 625f),
                                            UIFactory.CardViolet, UIFactory.Sun);
            var heroHeading = UIFactory.CreateText("HeroHeading", hero, "TU COMPAÑERO", 36, UIFactory.Cream);
            UIFactory.Place(heroHeading.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, 232f), new Vector2(560f, 58f));
            var halo = UIFactory.CreateImage("PokemonGlow", hero, ProceduralSprites.Glow,
                                             new Color(1f, .87f, .25f, .45f));
            UIFactory.Place(halo.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, 75f), new Vector2(440f, 360f));
            _mainPokemonPreview = UIFactory.CreateImage("SelectedPokemon", hero, null, Color.white);
            UIFactory.Place(_mainPokemonPreview.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, 65f), new Vector2(280f, 280f));
            _mainPokemonPreview.preserveAspect = true;
            _mainPokemonName = UIFactory.CreateText("PokemonName", hero, "PIKACHU", 48, UIFactory.Sun);
            UIFactory.Place(_mainPokemonName.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, -145f), new Vector2(560f, 66f));
            var hint = UIFactory.CreateText("Hint", hero, "Elige a tu Pokémon y sobrevive\na las rondas de jefes legendarios", 25,
                                            UIFactory.Cream);
            UIFactory.Place(hint.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, -230f), new Vector2(560f, 75f));

            var footer = UIFactory.CreateText("Footer", content, "PETER GAMES  ·  v1.0", 22,
                                              new Color(1f, 1f, 1f, .66f));
            UIFactory.Place(footer.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                            new Vector2(0f, 22f), new Vector2(900f, 40f));
            BuildWalletBadge(content);
            RefreshWallet();
            RefreshHero();
        }

        /// <summary>Monedas globales (economía persistente usada por el gacha).</summary>
        void BuildWalletBadge(Transform parent)
        {
            var badge = UIFactory.CreateImage("WalletBadge", parent, ProceduralSprites.RoundedRect,
                                              new Color(0.08f, 0.04f, 0.15f, 0.68f), false, true);
            UIFactory.Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(40f, -40f), new Vector2(320f, 74f));
            UIFactory.AddOutline(badge, new Color(1f, 1f, 1f, 0.18f), 2f);

            var coin = UIFactory.CreateImage("Coin", badge.rectTransform, ProceduralSprites.Circle,
                                             UIFactory.Sun, false);
            UIFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(40f, 0f), new Vector2(44f, 44f));
            UIFactory.AddOutline(coin, UIFactory.Ink, 2f);

            _walletText = UIFactory.CreateText("WalletText", badge.rectTransform, "MONEDAS 0", 28,
                                               UIFactory.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(_walletText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(74f, 0f), new Vector2(232f, 50f));
        }

        void RefreshWallet()
        {
            if (_walletText != null) _walletText.text = "MONEDAS " + PlayerEconomy.Coins;
        }

        void RefreshHero()
        {
            if (_mainPokemonPreview == null || _mainPokemonName == null) return;
            _mainPokemonPreview.sprite = PokemonCollection.GetSprite(PokemonCollection.Selected);
            _mainPokemonName.text = PokemonCollection.GetName(PokemonCollection.Selected).ToUpperInvariant();
        }

        void BuildPauseMenu()
        {
            _pausePanel = CreateOverlay("PauseMenu");
            var root = _pausePanel;

            var dim = UIFactory.CreateImage("Dim", root, null, new Color(0.03f, 0.01f, 0.07f, 0.78f), true);
            UIFactory.Stretch(dim.rectTransform);

            var card = UIFactory.CreateCard(UIFactory.CreateSafeArea(root), new Vector2(900f, 700f),
                                            UIFactory.DeepViolet, UIFactory.Sun);
            UIFactory.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            Vector2.zero, new Vector2(900f, 700f));

            var title = UIFactory.CreateText("Title", card, "PAUSA", 104, UIFactory.Sun);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, 235f), new Vector2(800f, 150f));
            UIFactory.AddOutline(title, UIFactory.Ink, 6f);

            _pauseInfo = UIFactory.CreateText("RunSummary", card, "TU PROGRESO", 28,
                                             new Color(1f, 1f, 1f, 0.7f));
            UIFactory.Place(_pauseInfo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, 140f), new Vector2(800f, 50f));

            var resume = UIFactory.CreateButton(card, "ResumeButton", "CONTINUAR", UIFactory.Lime,
                                                new Vector2(600f, 95f), Resume, 46);
            UIFactory.Place((RectTransform)resume.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(0f, 55f), new Vector2(600f, 95f));

            var evs = UIFactory.CreateButton(card, "RunSkillsButton", "ÁRBOL EV", UIFactory.Lime,
                                             new Vector2(600f, 95f), OpenSkills, 40);
            UIFactory.Place((RectTransform)evs.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, -50f), new Vector2(600f, 95f));

            var settings = UIFactory.CreateButton(card, "SettingsButton", "CONFIGURACIÓN", UIFactory.Sky,
                                                   new Vector2(600f, 95f),
                                                  () => OpenSettings(SettingsOrigin.Pause), 38);
            UIFactory.Place((RectTransform)settings.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(0f, -155f), new Vector2(600f, 95f));

            var quit = UIFactory.CreateButton(card, "QuitButton", "SALIR DE PARTIDA", UIFactory.Coral,
                                                new Vector2(600f, 95f), ConfirmAbandon, 40);
            UIFactory.Place((RectTransform)quit.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(0f, -260f), new Vector2(600f, 95f));
        }

        void BuildConfirmAbandon()
        {
            _confirmPanel = CreateOverlay("ConfirmAbandon");
            var dim = UIFactory.CreateImage("Dim", _confirmPanel, null,
                                            new Color(.03f, .01f, .07f, .86f), true);
            UIFactory.Stretch(dim.rectTransform);
            var card = UIFactory.CreateCard(UIFactory.CreateSafeArea(_confirmPanel),
                                            new Vector2(900f, 460f), UIFactory.DeepViolet, UIFactory.Coral);
            var title = UIFactory.CreateText("Title", card, "¿SALIR DE LA PARTIDA?", 55, UIFactory.Sun);
            UIFactory.Place(title.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, 130f), new Vector2(800f, 90f));
            var note = UIFactory.CreateText("Note", card,
                "Se guardarán las monedas ganadas. Perderás los puntos y mejoras de esta partida.", 28, UIFactory.Cream);
            UIFactory.Place(note.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0f, 15f), new Vector2(750f, 90f));
            var stay = UIFactory.CreateButton(card, "StayButton", "SEGUIR JUGANDO", UIFactory.Lime,
                                               new Vector2(350f, 94f), CancelAbandon, 32);
            UIFactory.Place((RectTransform)stay.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(-200f, -125f), new Vector2(350f, 94f));
            var leave = UIFactory.CreateButton(card, "LeaveButton", "SALIR", UIFactory.Coral,
                                                new Vector2(300f, 94f), AbandonRun, 36);
            UIFactory.Place((RectTransform)leave.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(230f, -125f), new Vector2(300f, 94f));
        }

        void ConfirmAbandon() => ShowPanel(_confirmPanel);
        void CancelAbandon() => ShowPanel(_pausePanel);

        void OpenCollection()
        {
            _subMenuOpen = true;
            RefreshCollection();
            ShowPanel(_collectionPanel);
        }

        void OpenSkills()
        {
            if (!_playing || RoundManager.Instance == null || !RoundManager.Instance.IsRunActive) return;
            _skillsFromPause = _paused;
            _subMenuOpen = true;
            _paused = true;
            GameFlow.Blocked = true;
            ReleaseJoysticks();
            Time.timeScale = 0f;
            AudioListener.pause = true;
            RefreshSkills();
            ShowPanel(_skillsPanel);
            RefreshPauseButton();
        }

        void CloseSubMenu()
        {
            _subMenuOpen = false;
            if (_playing)
            {
                if (_skillsFromPause) ShowPanel(_pausePanel);
                else Resume();
            }
            else { RefreshWallet(); RefreshHero(); ShowPanel(_mainPanel); }
            RefreshPauseButton();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) ReleaseJoysticks();
        }

        void ReleaseJoysticks()
        {
            VirtualJoystick.ResetAll();
        }

        void BuildCollectionMenu()
        {
            _collectionPanel = CreateOverlay("CollectionMenu");
            var dim = UIFactory.CreateImage("Dim", _collectionPanel, null, UIFactory.DeepViolet, true);
            UIFactory.Stretch(dim.rectTransform);
            var content = UIFactory.CreateSafeArea(_collectionPanel);
            var card = UIFactory.CreateCard(content, new Vector2(1520f, 880f),
                                            UIFactory.CardViolet, UIFactory.Sun);
            var title = UIFactory.CreateText("Title", card, "COLECCIÓN POKÉMON", 72, UIFactory.Sun);
            UIFactory.Place(title.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0, 340), new Vector2(1400, 110));
            _collectionInfo = UIFactory.CreateText("Info", card, "", 36, UIFactory.Cream);
            UIFactory.Place(_collectionInfo.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(225, 110), new Vector2(1000, 300));
            _pokemonPreview = UIFactory.CreateImage("PokemonPreview", card, null, Color.white);
            UIFactory.Place(_pokemonPreview.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(-510, 110), new Vector2(270, 270));
            _pokemonPreview.preserveAspect = true;
            var previous = UIFactory.CreateButton(card, "Previous", "◀", UIFactory.Sky,
                                                   new Vector2(180, 90), () => ChangeSelection(-1), 42);
            UIFactory.Place((RectTransform)previous.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(-290, -100), new Vector2(180, 90));
            var next = UIFactory.CreateButton(card, "Next", "▶", UIFactory.Sky,
                                               new Vector2(180, 90), () => ChangeSelection(1), 42);
            UIFactory.Place((RectTransform)next.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(290, -100), new Vector2(180, 90));
            _buyPokemonButton = UIFactory.CreateButton(card, "BuyOrSelect", "", UIFactory.Lime,
                                                       new Vector2(510, 100), BuyOrSelectPokemon, 36);
            UIFactory.Place((RectTransform)_buyPokemonButton.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0, -245), new Vector2(510, 100));
            var back = UIFactory.CreateButton(card, "Back", "VOLVER", UIFactory.Coral,
                                              new Vector2(360, 85), CloseSubMenu, 36);
            UIFactory.Place((RectTransform)back.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0, -370), new Vector2(360, 85));
        }

        void ChangeSelection(int delta)
        {
            int count = PokemonCollection.Count;
            if (count == 0) return;
            _collectionIndex = (_collectionIndex + delta + count) % count;
            RefreshCollection();
        }

        void BuyOrSelectPokemon()
        {
            if (PokemonCollection.IsOwned(_collectionIndex)) PokemonCollection.Select(_collectionIndex);
            else PokemonCollection.Buy(_collectionIndex);
            RefreshCollection();
        }

        void RefreshCollection()
        {
            int count = PokemonCollection.Count;
            if (count == 0) { _collectionInfo.text = "No hay sprites en Resources/PokemonAtlas"; _buyPokemonButton.interactable = false; return; }
            _collectionIndex = Mathf.Clamp(_collectionIndex, 0, count - 1);
            _pokemonPreview.sprite = PokemonCollection.GetSprite(_collectionIndex);
            bool owned = PokemonCollection.IsOwned(_collectionIndex);
            _collectionInfo.text = PokemonCollection.GetName(_collectionIndex) + "  (" + (_collectionIndex + 1) + "/" + count + ")\n" +
                                    (owned ? "DESBLOQUEADO" : "BLOQUEADO") +
                                    "\nMonedas: " + PlayerEconomy.Coins + "  ·  Precio: " +
                                    (_collectionIndex == 0 ? "GRATIS" : PokemonCollection.Price.ToString()) +
                                    (!owned && PlayerEconomy.Coins < PokemonCollection.Price
                                        ? "\nFaltan " + (PokemonCollection.Price - PlayerEconomy.Coins) + " monedas" : "");
            _buyPokemonButton.GetComponentInChildren<Text>().text = owned ?
                (PokemonCollection.Selected == _collectionIndex ? "EQUIPADO" : "EQUIPAR") : "COMPRAR";
            _buyPokemonButton.interactable = owned
                ? PokemonCollection.Selected != _collectionIndex
                : PlayerEconomy.Coins >= PokemonCollection.Price;
        }

        void BuildSkillsMenu()
        {
            _skillsPanel = CreateOverlay("SkillsMenu");
            var dim = UIFactory.CreateImage("Dim", _skillsPanel, null, UIFactory.DeepViolet, true);
            UIFactory.Stretch(dim.rectTransform);
            var content = UIFactory.CreateSafeArea(_skillsPanel);
            var card = UIFactory.CreateCard(content, new Vector2(1380f, 880f),
                                            UIFactory.DeepViolet, UIFactory.Sky);
            var title = UIFactory.CreateText("Title", card, "ÁRBOL DE EVs", 72, UIFactory.Sun);
            UIFactory.Place(title.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0, 390), new Vector2(1300, 100));
            _skillsInfo = UIFactory.CreateText("Info", card, "", 32, UIFactory.Cream);
            UIFactory.Place(_skillsInfo.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0, 270), new Vector2(1300, 90));
            _skillButtons = new Button[4];
            string[] names = { "SALUD", "ATAQUE", "DEFENSA", "VELOCIDAD" };
            for (int i = 0; i < names.Length; i++)
            {
                int stat = i;
                var button = UIFactory.CreateButton(card, "EV_" + names[i], "", UIFactory.Sky,
                                                     new Vector2(720, 100), () => { RoundManager.Instance?.BuyEV(stat); RefreshSkills(); }, 36);
                UIFactory.Place((RectTransform)button.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                                new Vector2(0, 135 - i * 115), new Vector2(720, 100));
                _skillButtons[i] = button;
            }
            var back = UIFactory.CreateButton(card, "Back", "VOLVER", UIFactory.Coral,
                                              new Vector2(360, 85), CloseSubMenu, 36);
            UIFactory.Place((RectTransform)back.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0, -375), new Vector2(360, 85));
        }

        void RefreshSkills()
        {
            var round = RoundManager.Instance;
            if (round == null) return;
            _skillsInfo.text = PokemonCollection.GetName(PokemonCollection.Selected) + " · Puntos: " + round.Points +
                                "  ·  Mejora tus EV con los puntos de esta partida";
            string[] names = { "SALUD", "ATAQUE", "DEFENSA", "VELOCIDAD" };
            for (int i = 0; i < 4; i++)
            {
                string state = round.EVLevel(i) >= 10 ? "NIVEL MÁXIMO" :
                    round.Points < round.EVCost(i) ? "FALTAN " + (round.EVCost(i) - round.Points) + " pt" :
                    round.EVCost(i) + " pt";
                _skillButtons[i].GetComponentInChildren<Text>().text = names[i] + "  " + round.EVLevel(i) +
                    "/10  ·  " + state;
                _skillButtons[i].interactable = round.CanBuyEV(i);
            }
        }

        void BuildSettingsMenu()
        {
            _settingsPanel = CreateOverlay("SettingsMenu");
            var root = _settingsPanel;

            var dim = UIFactory.CreateImage("Dim", root, null, new Color(0.03f, 0.01f, 0.07f, 0.82f), true);
            UIFactory.Stretch(dim.rectTransform);

            var card = UIFactory.CreateCard(UIFactory.CreateSafeArea(root), new Vector2(1080f, 660f),
                                            UIFactory.DeepViolet, UIFactory.Sky);
            UIFactory.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            Vector2.zero, new Vector2(1080f, 660f));

            var title = UIFactory.CreateText("Title", card, "CONFIGURACIÓN", 74, UIFactory.Cream);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, 235f), new Vector2(1000f, 110f));
            UIFactory.AddOutline(title, UIFactory.Ink, 5f);

            // ----- Volumen
            var volumeLabel = UIFactory.CreateText("VolumeLabel", card, "VOLUMEN", 34, UIFactory.Sun,
                                                   TextAnchor.MiddleLeft);
            UIFactory.Place(volumeLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(-390f, 85f), new Vector2(280f, 50f));

            _volumeSlider = UIFactory.CreateSlider(card, new Vector2(430f, 52f), _volume, OnVolumeChanged);
            UIFactory.Place((RectTransform)_volumeSlider.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(40f, 85f), new Vector2(430f, 52f));

            _volumeValue = UIFactory.CreateText("VolumeValue", card, "80%", 34, UIFactory.Cream,
                                                TextAnchor.MiddleCenter);
            UIFactory.Place(_volumeValue.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(345f, 85f), new Vector2(140f, 50f));

            // ----- Calidad
            var qualityLabel = UIFactory.CreateText("QualityLabel", card, "CALIDAD", 34, UIFactory.Sun,
                                                    TextAnchor.MiddleLeft);
            UIFactory.Place(qualityLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(-390f, -25f), new Vector2(280f, 50f));

            var qualityButton = UIFactory.CreateButton(card, "QualityButton", QualityName(_quality),
                                                       UIFactory.Coral, new Vector2(300f, 86f),
                                                       CycleQuality, 34);
            UIFactory.Place((RectTransform)qualityButton.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(90f, -25f), new Vector2(300f, 86f));
            _qualityValue = qualityButton.GetComponentInChildren<Text>();

            var hint = UIFactory.CreateText("Hint", card, "Toca la calidad para cambiarla", 24,
                                            new Color(1f, 1f, 1f, 0.55f));
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, -125f), new Vector2(900f, 40f));

            var back = UIFactory.CreateButton(card, "BackButton", "VOLVER", UIFactory.Lime,
                                              new Vector2(460f, 96f), CloseSettings, 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(0f, -240f), new Vector2(460f, 96f));

            RefreshSettingsLabels();
        }

        void BuildPokeballDecoration(Transform parent)
        {
            var bigBall = UIFactory.CreateImage("Pokeball", parent, ProceduralSprites.Pokeball, Color.white, false);
            UIFactory.Place(bigBall.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                            new Vector2(-280f, -260f), new Vector2(340f, 340f));
            bigBall.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -16f);
            var spin = bigBall.gameObject.AddComponent<MenuDecoration>();
            spin.Configure(10f, 0.9f, 12f);

            var smallBall = UIFactory.CreateImage("PokeballSmall", parent, ProceduralSprites.Pokeball,
                                                  new Color(1f, 1f, 1f, 0.9f), false);
            UIFactory.Place(smallBall.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
                            new Vector2(240f, 235f), new Vector2(210f, 210f));
            smallBall.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            var slow = smallBall.gameObject.AddComponent<MenuDecoration>();
            slow.Configure(7f, 1.3f, -8f);
        }

        void BuildSparkles(Transform parent)
        {
            Color[] colors =
            {
                UIFactory.Sun, UIFactory.Lime, UIFactory.Coral, UIFactory.Cream,
                UIFactory.Sun, UIFactory.Sky, UIFactory.Cream, UIFactory.Coral
            };

            for (int i = 0; i < colors.Length; i++)
            {
                float x = Mathf.Lerp(-830f, 830f, ((i * 37) % 100) / 100f);
                float y = Mathf.Lerp(-430f, 470f, ((i * 61 + 17) % 100) / 100f);
                float size = 12f + ((i * 53) % 16);

                var sparkle = UIFactory.CreateImage("Sparkle" + i, parent, ProceduralSprites.Circle,
                                                    new Color(colors[i].r, colors[i].g, colors[i].b, 0.55f), false);
                UIFactory.Place(sparkle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                new Vector2(x, y), new Vector2(size, size));

                var floaty = sparkle.gameObject.AddComponent<MenuDecoration>();
                floaty.Configure(6f + (i % 4) * 3f, 0.8f + i * 0.14f, 0f);
            }
        }

        RectTransform CreateOverlay(string name)
        {
            var rect = UIFactory.CreateRect(name, _canvas);
            UIFactory.Stretch(rect);
            return rect;
        }

        // ------------------------------------------------------------- Ajustes
        void OnVolumeChanged(float value)
        {
            _volume = value;
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(VolumeKey, value);
            RefreshSettingsLabels();
        }

        void CycleQuality()
        {
            int count = QualitySettings.names.Length;
            if (count <= 1) return;

            _quality = (_quality + 1) % count;
            QualitySettings.SetQualityLevel(_quality, true);
            PlayerPrefs.SetInt(QualityKey, _quality);
            RefreshSettingsLabels();
        }

        void RefreshSettingsLabels()
        {
            if (_volumeValue != null) _volumeValue.text = Mathf.RoundToInt(_volume * 100f) + "%";
            if (_volumeSlider != null) _volumeSlider.SetValueWithoutNotify(_volume);
            if (_qualityValue != null) _qualityValue.text = QualityName(_quality);
        }

        static string QualityName(int index)
        {
            int count = QualitySettings.names.Length;
            if (count <= 1) return "ALTA";
            if (index <= 0) return "BAJA";
            if (index >= count - 1) return "ALTA";
            return "MEDIA";
        }
    }
}

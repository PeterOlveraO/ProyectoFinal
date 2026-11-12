using Game.Core;
using Game.Economy;
using Game.Rendering;
using Game.Rounds;
using Game.Pokemon;
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
        RectTransform _mainPanel;
        RectTransform _pausePanel;
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

        Text _volumeValue;
        Text _qualityValue;
        Text _walletText;
        Slider _volumeSlider;
        SettingsOrigin _settingsOrigin;

        bool _playing;
        bool _paused;
        bool _settingsOpen;
        bool _subMenuOpen;

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

            ShowPanel(_pausePanel);
            RefreshPauseButton();
        }

        public void Resume()
        {
            if (!_playing) return;

            _paused = false;
            _settingsOpen = false;
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
                _pauseButton.SetActive(_playing && !_paused && !_settingsOpen && !GameFlow.Over);
        }

        // ------------------------------------------------------------------ UI
        void Build()
        {
            _canvas = UIFactory.CreateCanvas("UI_Menus", 100);

            BuildPauseButton();
            BuildMainMenu();
            BuildPauseMenu();
            BuildSettingsMenu();
            BuildCollectionMenu();
            BuildSkillsMenu();

            _pausePanel.gameObject.SetActive(false);
            _settingsPanel.gameObject.SetActive(false);
            _collectionPanel.gameObject.SetActive(false);
            _skillsPanel.gameObject.SetActive(false);
        }

        void BuildPauseButton()
        {
            var rect = UIFactory.CreateRect("PauseButton", _canvas);
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

        void BuildMainMenu()
        {
            _mainPanel = CreateOverlay("MainMenu");
            var root = _mainPanel;

            // Fondo degradado (violeta -> coral -> amarillo).
            var background = UIFactory.CreateImage("Background", root,
                ProceduralSprites.VerticalGradient(new Color32(66, 28, 128, 255),
                                                   new Color32(255, 160, 92, 255)),
                Color.white, true);
            UIFactory.Stretch(background.rectTransform);

            var glow = UIFactory.CreateImage("Glow", root, ProceduralSprites.Glow,
                new Color(1f, 0.88f, 0.35f, 0.35f), false);
            UIFactory.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, 280f), new Vector2(1250f, 620f));

            BuildSparkles(root);
            BuildPokeballDecoration(root);

            // Título.
            var title = UIFactory.CreateText("Title", root, "PKETER", 190, UIFactory.Sun);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, 315f), new Vector2(1500f, 270f));
            UIFactory.AddOutline(title, UIFactory.Ink, 7f);

            var subtitle = UIFactory.CreateText("Subtitle", root, "P O K É M O N   T R A I N E R", 40,
                                                UIFactory.Cream);
            subtitle.color = new Color(1f, 0.98f, 0.93f, 0.92f);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, 160f), new Vector2(1500f, 70f));

            var play = UIFactory.CreateButton(root, "PlayButton", "JUGAR", UIFactory.Lime,
                                             new Vector2(640f, 116f), StartGame, 50);
            UIFactory.Place((RectTransform)play.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(640f, 116f));

            var collection = UIFactory.CreateButton(root, "CollectionButton", "POKÉMON", UIFactory.Sky,
                                                    new Vector2(310f, 90f), OpenCollection, 36);
            UIFactory.Place((RectTransform)collection.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(-165f, -75f), new Vector2(310f, 90f));
            var skills = UIFactory.CreateButton(root, "SkillsButton", "ÁRBOL EV", UIFactory.Lime,
                                                new Vector2(310f, 90f), OpenSkills, 36);
            UIFactory.Place((RectTransform)skills.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(165f, -75f), new Vector2(310f, 90f));

            var settings = UIFactory.CreateButton(root, "SettingsButton", "CONFIGURACIÓN", UIFactory.Sky,
                                                 new Vector2(640f, 116f),
                                                 () => OpenSettings(SettingsOrigin.MainMenu), 40);
            UIFactory.Place((RectTransform)settings.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(0f, -185f), new Vector2(640f, 96f));

            var quit = UIFactory.CreateButton(root, "QuitButton", "SALIR", UIFactory.Coral,
                                              new Vector2(640f, 116f), QuitGame, 46);
            UIFactory.Place((RectTransform)quit.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(640f, 96f));

            var footer = UIFactory.CreateText("Footer", root, "v1.0  ·  Peter Games", 24,
                                              new Color(1f, 1f, 1f, 0.65f));
            UIFactory.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 34f), new Vector2(900f, 40f));

            BuildWalletBadge(root);
            RefreshWallet();
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

        void BuildPauseMenu()
        {
            _pausePanel = CreateOverlay("PauseMenu");
            var root = _pausePanel;

            var dim = UIFactory.CreateImage("Dim", root, null, new Color(0.03f, 0.01f, 0.07f, 0.78f), true);
            UIFactory.Stretch(dim.rectTransform);

            var card = UIFactory.CreateCard(root, new Vector2(900f, 700f),
                                            UIFactory.DeepViolet, UIFactory.Sun);
            UIFactory.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            Vector2.zero, new Vector2(900f, 700f));

            var title = UIFactory.CreateText("Title", card, "PAUSA", 104, UIFactory.Sun);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, 235f), new Vector2(800f, 150f));
            UIFactory.AddOutline(title, UIFactory.Ink, 6f);

            var note = UIFactory.CreateText("Note", card, "La partida está en pausa", 30,
                                            new Color(1f, 1f, 1f, 0.7f));
            UIFactory.Place(note.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, 140f), new Vector2(700f, 50f));

            var resume = UIFactory.CreateButton(card, "ResumeButton", "CONTINUAR", UIFactory.Lime,
                                                new Vector2(600f, 110f), Resume, 46);
            UIFactory.Place((RectTransform)resume.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(600f, 110f));

            var settings = UIFactory.CreateButton(card, "SettingsButton", "CONFIGURACIÓN", UIFactory.Sky,
                                                  new Vector2(600f, 110f),
                                                  () => OpenSettings(SettingsOrigin.Pause), 38);
            UIFactory.Place((RectTransform)settings.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(600f, 110f));

            var quit = UIFactory.CreateButton(card, "QuitButton", "SALIR DE PARTIDA", UIFactory.Coral,
                                               new Vector2(600f, 110f), AbandonRun, 40);
            UIFactory.Place((RectTransform)quit.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(600f, 110f));
        }

        void OpenCollection()
        {
            _subMenuOpen = true;
            RefreshCollection();
            ShowPanel(_collectionPanel);
        }

        void OpenSkills()
        {
            _subMenuOpen = true;
            RefreshSkills();
            ShowPanel(_skillsPanel);
        }

        void CloseSubMenu()
        {
            _subMenuOpen = false;
            ShowPanel(_mainPanel);
        }

        void BuildCollectionMenu()
        {
            _collectionPanel = CreateOverlay("CollectionMenu");
            var dim = UIFactory.CreateImage("Dim", _collectionPanel, null, UIFactory.DeepViolet, true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.CreateText("Title", _collectionPanel, "COLECCIÓN POKÉMON", 72, UIFactory.Sun);
            UIFactory.Place(title.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0, 340), new Vector2(1400, 110));
            _collectionInfo = UIFactory.CreateText("Info", _collectionPanel, "", 36, UIFactory.Cream);
            UIFactory.Place(_collectionInfo.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(225, 110), new Vector2(1000, 300));
            _pokemonPreview = UIFactory.CreateImage("PokemonPreview", _collectionPanel, null, Color.white);
            UIFactory.Place(_pokemonPreview.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(-510, 110), new Vector2(270, 270));
            _pokemonPreview.preserveAspect = true;
            var previous = UIFactory.CreateButton(_collectionPanel, "Previous", "◀", UIFactory.Sky,
                                                   new Vector2(180, 90), () => ChangeSelection(-1), 42);
            UIFactory.Place((RectTransform)previous.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(-290, -100), new Vector2(180, 90));
            var next = UIFactory.CreateButton(_collectionPanel, "Next", "▶", UIFactory.Sky,
                                               new Vector2(180, 90), () => ChangeSelection(1), 42);
            UIFactory.Place((RectTransform)next.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(290, -100), new Vector2(180, 90));
            _buyPokemonButton = UIFactory.CreateButton(_collectionPanel, "BuyOrSelect", "", UIFactory.Lime,
                                                       new Vector2(510, 100), BuyOrSelectPokemon, 36);
            UIFactory.Place((RectTransform)_buyPokemonButton.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0, -245), new Vector2(510, 100));
            var back = UIFactory.CreateButton(_collectionPanel, "Back", "VOLVER", UIFactory.Coral,
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
            if (count == 0) { _collectionInfo.text = "No hay sprites en Resources/PokemonSprites"; _buyPokemonButton.interactable = false; return; }
            _collectionIndex = Mathf.Clamp(_collectionIndex, 0, count - 1);
            _pokemonPreview.sprite = PokemonCollection.GetSprite(_collectionIndex);
            bool owned = PokemonCollection.IsOwned(_collectionIndex);
            _collectionInfo.text = PokemonCollection.GetName(_collectionIndex) + "  (" + (_collectionIndex + 1) + "/" + count + ")\n" +
                                   (owned ? "DESBLOQUEADO" : "BLOQUEADO") +
                                   "\nMonedas: " + PlayerEconomy.Coins + "  ·  Precio: " + PokemonCollection.Price;
            _buyPokemonButton.GetComponentInChildren<Text>().text = owned ?
                (PokemonCollection.Selected == _collectionIndex ? "EQUIPADO" : "EQUIPAR") : "COMPRAR";
            _buyPokemonButton.interactable = owned || PlayerEconomy.Coins >= PokemonCollection.Price;
        }

        void BuildSkillsMenu()
        {
            _skillsPanel = CreateOverlay("SkillsMenu");
            var dim = UIFactory.CreateImage("Dim", _skillsPanel, null, UIFactory.DeepViolet, true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.CreateText("Title", _skillsPanel, "ÁRBOL DE EVs", 72, UIFactory.Sun);
            UIFactory.Place(title.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0, 390), new Vector2(1300, 100));
            _skillsInfo = UIFactory.CreateText("Info", _skillsPanel, "", 32, UIFactory.Cream);
            UIFactory.Place(_skillsInfo.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0, 270), new Vector2(1300, 90));
            _skillButtons = new Button[4];
            string[] names = { "SALUD", "ATAQUE", "DEFENSA", "VELOCIDAD" };
            for (int i = 0; i < names.Length; i++)
            {
                int stat = i;
                var button = UIFactory.CreateButton(_skillsPanel, "EV_" + names[i], "", UIFactory.Sky,
                                                     new Vector2(720, 100), () => { PlayerEconomy.BuyEV(stat); RefreshSkills(); }, 36);
                UIFactory.Place((RectTransform)button.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                                new Vector2(0, 135 - i * 115), new Vector2(720, 100));
                _skillButtons[i] = button;
            }
            var back = UIFactory.CreateButton(_skillsPanel, "Back", "VOLVER", UIFactory.Coral,
                                              new Vector2(360, 85), CloseSubMenu, 36);
            UIFactory.Place((RectTransform)back.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                            new Vector2(0, -375), new Vector2(360, 85));
        }

        void RefreshSkills()
        {
            _skillsInfo.text = PokemonCollection.GetName(PokemonCollection.Selected) + " · Puntos: " + PlayerEconomy.SkillPoints +
                               "  ·  30% de probabilidad por enemigo";
            string[] names = { "SALUD", "ATAQUE", "DEFENSA", "VELOCIDAD" };
            for (int i = 0; i < 4; i++)
            {
                _skillButtons[i].GetComponentInChildren<Text>().text = names[i] + "  " + PlayerEconomy.EVLevel(i) +
                    "/10  ·  " + PlayerEconomy.EVCost(i) + " pt";
                _skillButtons[i].interactable = PlayerEconomy.CanBuyEV(i);
            }
        }

        void BuildSettingsMenu()
        {
            _settingsPanel = CreateOverlay("SettingsMenu");
            var root = _settingsPanel;

            var dim = UIFactory.CreateImage("Dim", root, null, new Color(0.03f, 0.01f, 0.07f, 0.82f), true);
            UIFactory.Stretch(dim.rectTransform);

            var card = UIFactory.CreateCard(root, new Vector2(1080f, 660f),
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

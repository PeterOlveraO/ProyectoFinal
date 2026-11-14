using Game.Core;
using Game.Economy;
using Game.Rendering;
using Game.Rounds;
using Game.Pokemon;
using Game.Pokemon.Progression;
using System;
using System.Collections.Generic;
using System.Linq;
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
        RectTransform _starterPanel;
        RectTransform _gachaPanel;
        RectTransform _resultPanel;
        RectTransform _releasePanel;
        Text _collectionInfo;
        Image _pokemonPreview;
        Text _skillsInfo;
        Button _equipButton;
        Button _releaseButton;
        Text _starterInfo;
        Text _gachaInfo;
        Text _resultInfo;
        Image _resultPreview;
        Text _releaseInfo;
        Button _starterConfirm;
        Button[] _poolButtons;
        IReadOnlyList<SpeciesDefinition> _catalog;
        IReadOnlyList<PokemonInstance> _inventory;
        IReadOnlyList<GachaPool> _pools;
        PokemonInstance _equipped;
        int _starterIndex;
        string _message = "";
        string _pullRequest;
        string _releaseTarget;
        bool _busy;
        readonly List<RectTransform> _responsiveContents = new List<RectTransform>();
        Button[] _skillButtons;
        int _collectionIndex;
        GameObject _pauseButton;
        GameObject _skillsButton;

        Text _volumeValue;
        Text _qualityValue;
        Text _walletText;
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
            PokemonCollection.Changed += OnCollectionChanged;
            ShowMainMenu();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            PokemonCollection.Changed -= OnCollectionChanged;

            GameFlow.Blocked = false;
            GameFlow.Over = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        void Update()
        {
            RefreshPauseButton();
            if (GameFlow.Over) return;
            if (_starterPanel.gameObject.activeSelf || _resultPanel.gameObject.activeSelf) return;

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            if (_releasePanel.gameObject.activeSelf) { CancelRelease(); return; }
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

        void LateUpdate()
        {
            if (_canvas == null) return;
            var size = _canvas.rect.size;
            float scale = Mathf.Min(1f, Mathf.Min(size.x / 1540f, size.y / 1020f));
            foreach (var content in _responsiveContents)
                content.localScale = Vector3.one * scale;
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
            RefreshSnapshots();
            if (!PokemonCollection.Progression.IsReady || PokemonCollection.NeedsStarter)
            {
                RefreshStarter();
                ShowPanel(_starterPanel);
            }
            else ShowPanel(_mainPanel);
            RefreshPauseButton();
        }

        public void StartGame()
        {
            if (!PokemonCollection.Progression.IsReady || PokemonCollection.NeedsStarter ||
                PokemonCollection.Equipped == null)
            {
                ShowMainMenu();
                return;
            }
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
            if (RoundManager.Instance == null || !RoundManager.Instance.IsRunActive)
            {
                ShowMainMenu();
            }
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
            _starterPanel.gameObject.SetActive(false);
            _gachaPanel.gameObject.SetActive(false);
            _resultPanel.gameObject.SetActive(false);
            _releasePanel.gameObject.SetActive(false);
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

            BuildPauseButton();
            BuildSkillsButton();
            BuildMainMenu();
            BuildPauseMenu();
            BuildSettingsMenu();
            BuildCollectionMenu();
            BuildStarterMenu();
            BuildGachaMenu();
            BuildResultMenu();
            BuildReleaseMenu();
            BuildSkillsMenu();
            FitPanelContent(_starterPanel);
            FitPanelContent(_collectionPanel);
            FitPanelContent(_gachaPanel);
            FitPanelContent(_resultPanel);
            FitPanelContent(_releasePanel);

            _pausePanel.gameObject.SetActive(false);
            _settingsPanel.gameObject.SetActive(false);
            _collectionPanel.gameObject.SetActive(false);
            _skillsPanel.gameObject.SetActive(false);
            _starterPanel.gameObject.SetActive(false);
            _gachaPanel.gameObject.SetActive(false);
            _resultPanel.gameObject.SetActive(false);
            _releasePanel.gameObject.SetActive(false);
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

        void BuildSkillsButton()
        {
            var button = UIFactory.CreateButton(_canvas, "RunSkillsButton", "EVs", UIFactory.Sky,
                                                new Vector2(150f, 92f), OpenSkills, 35);
            UIFactory.Place((RectTransform)button.transform, new Vector2(1f, 1f), new Vector2(.5f, .5f),
                            new Vector2(-240f, -90f), new Vector2(150f, 92f));
            _skillsButton = button.gameObject;
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
                            new Vector2(0f, -75f), new Vector2(310f, 90f));

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
                                                new Vector2(600f, 95f), AbandonRun, 40);
            UIFactory.Place((RectTransform)quit.transform, new Vector2(0.5f, 0.5f),
                            new Vector2(0.5f, 0.5f), new Vector2(0f, -260f), new Vector2(600f, 95f));
        }

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
            else ShowPanel(_mainPanel);
            RefreshPauseButton();
        }

        void RefreshSnapshots()
        {
            _catalog = PokemonCollection.Catalog;
            _inventory = PokemonCollection.Inventory;
            _equipped = PokemonCollection.Equipped;
            _pools = PokemonCollection.GetPools();
        }

        void OnCollectionChanged()
        {
            RefreshSnapshots();
            RefreshWallet();
            if (_collectionPanel != null && _collectionPanel.gameObject.activeSelf) RefreshCollection();
            if (_gachaPanel != null && _gachaPanel.gameObject.activeSelf) RefreshGacha();
        }

        SpeciesDefinition Species(string id) => _catalog?.FirstOrDefault(s => s.id == id);
        static string Category(string category) => category == "PseudoLegendary" ? "Pseudolegendario" :
            category == "Mythical" ? "Mítico" : "Común";
        static string Stats(StatBlock s) => $"SAL {s.health}  ATQ {s.attack}  DEF {s.defense}  VEL {s.speed}";
        static string NatureName(Nature nature)
        {
            string[] names = { "Fuerte", "Huraña", "Audaz", "Firme", "Pícara",
                "Osada", "Dócil", "Plácida", "Agitada", "Floja",
                "Miedosa", "Activa", "Seria", "Alegre", "Ingenua",
                "Modesta", "Afable", "Mansa", "Tímida", "Alocada",
                "Serena", "Amable", "Grosera", "Cauta", "Rara" };
            int index = (int)nature;
            return index >= 0 && index < names.Length ? names[index] : nature.ToString();
        }
        string Describe(PokemonInstance pokemon)
        {
            if (pokemon == null) return "Sin Pokémon";
            var species = Species(pokemon.speciesId);
            if (species == null) return "Especie desconocida";
            var calculated = PokemonStats.Calculate(species.baseStats, pokemon.ivs, new StatBlock(), pokemon.nature);
            return $"{species.name}  ·  {Category(species.category)}  ·  Rareza {pokemon.rarity}\n" +
                $"Naturaleza: {NatureName(pokemon.nature)}\nIV: {Stats(pokemon.ivs)}\n" +
                $"Base: {Stats(species.baseStats)}\nStats (sin EV): {Stats(calculated)}";
        }
        static string ErrorMessage(string error)
        {
            switch (error)
            {
                case "InventoryFull": return "Inventario lleno. Libera un Pokémon primero.";
                case "InsufficientCoins": return "No tienes suficientes monedas.";
                case "LastInstance": return "No puedes liberar tu último Pokémon.";
                case "StarterRequired": return "Elige primero tu Pokémon inicial.";
                case "PersistenceFailure":
                case "SaveUnavailable": return "No se pudo guardar la partida. Reinicia y revisa el almacenamiento.";
                case "InstanceNotFound": return "Ese Pokémon ya no está en tu inventario.";
                default: return "No se pudo completar la operación. " + (error ?? "");
            }
        }
        static void At(RectTransform rect, float x, float y, float width, float height) =>
            UIFactory.Place(rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(x, y), new Vector2(width, height));
        Text PanelText(Transform root, string name, string text, int size, float x, float y, float width, float height)
        {
            var label = UIFactory.CreateText(name, root, text, size, UIFactory.Cream);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            At(label.rectTransform, x, y, width, height);
            return label;
        }
        Button PanelButton(Transform root, string name, string label, Color color, float x, float y,
            float width, float height, UnityEngine.Events.UnityAction action, int size = 30)
        {
            var button = UIFactory.CreateButton(root, name, label, color, new Vector2(width, height), action, size);
            At((RectTransform)button.transform, x, y, width, height);
            var text = button.GetComponentInChildren<Text>();
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return button;
        }
        RectTransform PlainPanel(string name, string title)
        {
            var panel = CreateOverlay(name);
            var dim = UIFactory.CreateImage("Background", panel, null, UIFactory.DeepViolet, true);
            UIFactory.Stretch(dim.rectTransform);
            var label = PanelText(panel, "Title", title, 66, 0, 430, 1400, 90);
            label.color = UIFactory.Sun;
            return panel;
        }
        void FitPanelContent(RectTransform panel)
        {
            var content = UIFactory.CreateRect("ResponsiveContent", panel);
            At(content, 0, 0, 1540, 1020);
            var children = new List<Transform>();
            foreach (Transform child in panel)
                if (child != content && child.name != "Background") children.Add(child);
            foreach (var child in children) child.SetParent(content, false);
            _responsiveContents.Add(content);
        }

        void BuildStarterMenu()
        {
            _starterPanel = PlainPanel("StarterMenu", "ELIGE TU POKÉMON INICIAL");
            PanelText(_starterPanel, "Note", "Solo puedes elegir una vez. El inicial será gratuito.", 30, 0, 345, 1300, 55);
            var ids = PokemonCollection.StarterIds;
            for (int i = 0; i < ids.Count; i++)
            {
                int index = i;
                float x = (i - (ids.Count - 1) * .5f) * 420f;
                var species = PokemonCollection.Catalog.FirstOrDefault(s => s.id == ids[index]);
                var image = UIFactory.CreateImage("StarterSprite" + i, _starterPanel,
                    PokemonCollection.GetSprite(new PokemonInstance { speciesId = ids[index] }), Color.white);
                image.preserveAspect = true;
                At(image.rectTransform, x, 130, 220, 220);
                PanelButton(_starterPanel, "Starter" + i, species?.name ?? ids[index], UIFactory.Sky,
                    x, -30, 350, 82, () => { _starterIndex = index; _message = ""; RefreshStarter(); }, 32);
            }
            _starterInfo = PanelText(_starterPanel, "StarterInfo", "", 30, 0, -140, 1300, 100);
            _starterConfirm = PanelButton(_starterPanel, "ConfirmStarter", "CONFIRMAR INICIAL", UIFactory.Lime,
                0, -280, 500, 90, ConfirmStarter, 34);
        }
        void RefreshStarter()
        {
            if (!PokemonCollection.Progression.IsReady)
            {
                _starterInfo.text = "No se pudo cargar el progreso. Reinicia y revisa el almacenamiento.";
                _starterConfirm.interactable = false;
                return;
            }
            var ids = PokemonCollection.StarterIds;
            if (ids.Count == 0) { _starterInfo.text = "No hay iniciales configurados."; _starterConfirm.interactable = false; return; }
            _starterIndex = Mathf.Clamp(_starterIndex, 0, ids.Count - 1);
            var config = PokemonCollection.Configuration;
            int total = config.starterF + config.starterA + config.starterS;
            _starterInfo.text = $"Elegido: {Species(ids[_starterIndex])?.name ?? ids[_starterIndex]} · Rareza F {Percent(config.starterF, total)} / A {Percent(config.starterA, total)} / S {Percent(config.starterS, total)}\n{_message}";
            _starterConfirm.interactable = !_busy;
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
        void BuildCollectionMenu()
        {
            _collectionPanel = PlainPanel("CollectionMenu", "INVENTARIO POKÉMON");
            _collectionInfo = PanelText(_collectionPanel, "Info", "", 31, 220, 105, 1040, 310);
            _pokemonPreview = UIFactory.CreateImage("PokemonPreview", _collectionPanel, null, Color.white);
            _pokemonPreview.preserveAspect = true;
            At(_pokemonPreview.rectTransform, -530, 130, 250, 250);
            PanelButton(_collectionPanel, "Previous", "◀ ANTERIOR", UIFactory.Sky, -360, -110, 330, 80, () => ChangeSelection(-1));
            PanelButton(_collectionPanel, "Next", "SIGUIENTE ▶", UIFactory.Sky, 360, -110, 330, 80, () => ChangeSelection(1));
            _equipButton = PanelButton(_collectionPanel, "Equip", "EQUIPAR", UIFactory.Lime, -240, -230, 380, 80, EquipPokemon);
            _releaseButton = PanelButton(_collectionPanel, "Release", "LIBERAR", UIFactory.Coral, 240, -230, 380, 80, AskRelease);
            PanelButton(_collectionPanel, "Gacha", "MÁQUINAS GACHA", UIFactory.Sky, -240, -355, 380, 80, OpenGacha, 27);
            PanelButton(_collectionPanel, "Back", "VOLVER", UIFactory.Coral, 240, -355, 380, 80, CloseSubMenu);
        }
        void ChangeSelection(int delta)
        {
            int count = _inventory?.Count ?? 0;
            if (count == 0) return;
            _collectionIndex = (_collectionIndex + delta + count) % count;
            _message = "";
            RefreshCollection();
        }
        void EquipPokemon()
        {
            if (_busy || _inventory.Count == 0) return;
            _busy = true;
            var result = PokemonCollection.Equip(_inventory[_collectionIndex].instanceId);
            _busy = false;
            _message = result.success ? "Pokémon equipado." : ErrorMessage(result.error);
            RefreshCollection();
        }
        void BuildReleaseMenu()
        {
            _releasePanel = PlainPanel("ReleaseMenu", "CONFIRMAR LIBERACIÓN");
            _releaseInfo = PanelText(_releasePanel, "ReleaseInfo", "", 36, 0, 100, 1200, 220);
            PanelButton(_releasePanel, "CancelRelease", "CANCELAR", UIFactory.Sky, -250, -220, 400, 90, CancelRelease, 34);
            PanelButton(_releasePanel, "ConfirmRelease", "SÍ, LIBERAR", UIFactory.Coral, 250, -220, 400, 90, ReleasePokemon, 34);
        }
        void AskRelease()
        {
            if (_busy || _inventory == null || _inventory.Count <= 1) return;
            var pokemon = _inventory[_collectionIndex];
            _releaseTarget = pokemon.instanceId;
            _releaseInfo.text = $"¿Liberar a {Species(pokemon.speciesId)?.name ?? "este Pokémon"} de rareza {pokemon.rarity}?\nEsta acción no se puede deshacer.";
            ShowPanel(_releasePanel);
        }
        void CancelRelease()
        {
            _releaseTarget = null;
            ShowPanel(_collectionPanel);
            RefreshCollection();
        }
        void ReleasePokemon()
        {
            if (_busy || string.IsNullOrEmpty(_releaseTarget)) return;
            _busy = true;
            var target = _releaseTarget;
            _releaseTarget = null;
            var result = PokemonCollection.Release(target);
            _busy = false;
            _message = result.success ? "Pokémon liberado." : ErrorMessage(result.error);
            ShowPanel(_collectionPanel);
            RefreshCollection();
        }
        void RefreshCollection()
        {
            int count = _inventory?.Count ?? 0;
            if (count == 0)
            {
                _collectionInfo.text = "No hay Pokémon en el inventario.";
                _pokemonPreview.sprite = null;
                _pokemonPreview.enabled = false;
                _equipButton.interactable = _releaseButton.interactable = false;
                return;
            }
            _collectionIndex = Mathf.Clamp(_collectionIndex, 0, count - 1);
            var pokemon = _inventory[_collectionIndex];
            bool equipped = _equipped != null && _equipped.instanceId == pokemon.instanceId;
            _pokemonPreview.sprite = PokemonCollection.GetSprite(pokemon);
            _pokemonPreview.enabled = _pokemonPreview.sprite != null;
            _collectionInfo.text = $"Instancia {_collectionIndex + 1}/{count} · Capacidad {count}/{PokemonCollection.InventoryCapacity} · Monedas {PlayerEconomy.Coins}\n" +
                Describe(pokemon) + (equipped ? "\nEQUIPADO" : "") + "\n" + _message;
            _equipButton.GetComponentInChildren<Text>().text = equipped ? "EQUIPADO" : "EQUIPAR";
            _equipButton.interactable = !_busy && !equipped;
            _releaseButton.interactable = !_busy && count > 1;
        }
        static string Percent(int value, int total) => total > 0 ? (100f * value / total).ToString("0.##") + "%" : "0%";
        void BuildGachaMenu()
        {
            _gachaPanel = PlainPanel("GachaMenu", "MÁQUINAS GACHA");
            _gachaInfo = PanelText(_gachaPanel, "Info", "", 28, 0, 315, 1450, 100);
            _poolButtons = new Button[2];
            for (int i = 0; i < _poolButtons.Length; i++)
            {
                int index = i;
                float x = i == 0 ? -400 : 400;
                _poolButtons[i] = PanelButton(_gachaPanel, "Pool" + i, "", UIFactory.Sky,
                    x, -100, 660, 430, () => PullPool(index), 29);
            }
            PanelButton(_gachaPanel, "Back", "VOLVER AL INVENTARIO", UIFactory.Coral, 0, -400, 500, 75, OpenCollection, 27);
        }
        void OpenGacha()
        {
            _message = "";
            RefreshSnapshots();
            RefreshGacha();
            ShowPanel(_gachaPanel);
        }
        void RefreshGacha()
        {
            _gachaInfo.text = $"Monedas: {PlayerEconomy.Coins} · Inventario: {_inventory.Count}/{PokemonCollection.InventoryCapacity}\n{_message}";
            for (int i = 0; i < _poolButtons.Length; i++)
            {
                var button = _poolButtons[i];
                bool exists = i < _pools.Count;
                button.gameObject.SetActive(exists);
                if (!exists) continue;
                var pool = _pools[i];
                int total = pool.weightF + pool.weightA + pool.weightS;
                string species = string.Join(", ", pool.species.Select(id => Species(id)?.name ?? id));
                button.GetComponentInChildren<Text>().text = $"MÁQUINA {(i == 0 ? "BÁSICA" : "ESPECIAL")}\n\nPrecio: {pool.price} monedas\nRareza F {Percent(pool.weightF, total)} · A {Percent(pool.weightA, total)} · S {Percent(pool.weightS, total)}\n\nEspecies (igual probabilidad):\n{species}\n\nTOCA PARA TIRAR";
                button.interactable = !_busy && _pullRequest == null && PokemonCollection.Progression.IsReady;
            }
        }
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
        void BuildResultMenu()
        {
            _resultPanel = PlainPanel("ResultMenu", "¡POKÉMON OBTENIDO!");
            _resultPreview = UIFactory.CreateImage("ResultSprite", _resultPanel, null, Color.white);
            _resultPreview.preserveAspect = true;
            At(_resultPreview.rectTransform, -480, 110, 300, 300);
            _resultInfo = PanelText(_resultPanel, "ResultInfo", "", 34, 180, 105, 1050, 330);
            PanelButton(_resultPanel, "CloseResult", "CONTINUAR", UIFactory.Lime, 0, -300, 450, 90, CloseResult, 36);
        }
        bool _starterResult;
        void ShowResult(PokemonInstance pokemon, bool starter)
        {
            _starterResult = starter;
            RefreshSnapshots();
            _resultPreview.sprite = PokemonCollection.GetSprite(pokemon);
            _resultPreview.enabled = _resultPreview.sprite != null;
            _resultInfo.text = Describe(pokemon) + "\nGuardado en tu inventario.";
            ShowPanel(_resultPanel);
        }
        void CloseResult()
        {
            _pullRequest = null;
            _message = "";
            if (_starterResult) ShowMainMenu();
            else OpenGacha();
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
                                                     new Vector2(720, 100), () => { RoundManager.Instance?.BuyEV(stat); RefreshSkills(); }, 36);
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
            var round = RoundManager.Instance;
            if (round == null) return;
            _skillsInfo.text = (round.RunSpecies?.name ?? "Pokémon") + " · Puntos: " + round.Points +
                               "  ·  30%: +" + round.BonusPointsPerDrop + " puntos por enemigo";
            string[] names = { "SALUD", "ATAQUE", "DEFENSA", "VELOCIDAD" };
            for (int i = 0; i < 4; i++)
            {
                _skillButtons[i].GetComponentInChildren<Text>().text = names[i] + "  " + round.EVLevel(i) +
                    "/10  ·  " + round.EVCost(i) + " pt";
                _skillButtons[i].interactable = round.CanBuyEV(i);
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

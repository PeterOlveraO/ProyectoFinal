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
        enum ScreenId { None, Main, Pause, Settings, Collection, Skills, Starter, Gacha, Result, Release, Credits }
        public static GameMenuController Instance { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsPaused => IsPlaying && _screen != ScreenId.None;

        readonly Dictionary<ScreenId, MenuPage> _pages = new Dictionary<ScreenId, MenuPage>();
        RectTransform _canvas;
        ScreenId _screen, _settingsOrigin, _skillsOrigin;
        CollectionView _collection;
        GachaView _gacha;
        PokemonDetailsView _resultDetails;
        IReadOnlyList<SpeciesDefinition> _catalog;
        IReadOnlyList<PokemonInstance> _inventory;
        IReadOnlyList<GachaPool> _pools;
        PokemonInstance _equipped;
        int _collectionIndex, _starterIndex;
        bool _busy, _starterResult;
        string _message = "", _pullRequest, _releaseTarget;
        float _volume;
        int _quality;
        UnityEngine.UI.Slider _volumeSlider;
        TMPro.TMP_Text _volumeValue, _qualityValue, _starterInfo, _releaseInfo;
        UnityEngine.UI.Button _starterConfirm;
        UnityEngine.UI.Button[] _starterButtons, _skillButtons;
        GameObject _pauseButton, _skillsButton;

        void Awake()
        {
            Instance = this;
            _volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, .8f));
            _quality = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()),
                0, Mathf.Max(0, QualitySettings.names.Length - 1));
            AudioListener.volume = _volume;
            if (QualitySettings.names.Length > 0) QualitySettings.SetQualityLevel(_quality, true);
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
            AudioListener.pause = false;
        }

        void Update()
        {
            bool controls = IsPlaying && _screen == ScreenId.None && !GameFlow.Over;
            if (_pauseButton.activeSelf != controls) _pauseButton.SetActive(controls);
            if (_skillsButton.activeSelf != controls) _skillsButton.SetActive(controls);
            bool back = Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                Gamepad.current?.buttonEast.wasPressedThisFrame == true;
            if (!back || GameFlow.Over) return;
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
            foreach (var entry in _pages) entry.Value.Root.gameObject.SetActive(entry.Key == next);
            bool blocked = next != ScreenId.None || GameFlow.Over;
            GameFlow.Blocked = blocked;
            Time.timeScale = blocked ? 0 : 1;
            AudioListener.pause = IsPlaying && blocked;
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
            _message = result.success ? "Listo para tu próxima partida." : ErrorMessage(result.error);
            RefreshCollection();
        }
        void AskRelease()
        {
            if (_busy || _inventory.Count <= 1) return;
            var pokemon = _inventory[_collectionIndex];
            _releaseTarget = pokemon.instanceId;
            _releaseInfo.text = $"¿Liberar a {Species(pokemon.speciesId)?.name}?\n\nRareza {pokemon.rarity} · Naturaleza {PokemonUi.NatureName(pokemon.nature)}\n\nEsta acción no se puede deshacer.";
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
            _message = result.success ? "Pokémon liberado. Tienes un espacio disponible." : ErrorMessage(result.error);
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
                _starterButtons[i].image.color = i == _starterIndex ? UIFactory.Sun : UIFactory.CardViolet;
                var text = _starterButtons[i].GetComponentInChildren<TMPro.TMP_Text>();
                text.color = i == _starterIndex ? UIFactory.Ink : UIFactory.Cream;
                text.text = i == _starterIndex ? "SELECCIONADO" : "ELEGIR";
            }
            var config = PokemonCollection.Configuration;
            int total = config.starterF + config.starterA + config.starterS;
            _starterInfo.text = string.IsNullOrEmpty(_message)
                ? $"Rareza inicial: F {PokemonUi.Percent(config.starterF, total)} · A {PokemonUi.Percent(config.starterA, total)} · S {PokemonUi.Percent(config.starterS, total)}" : _message;
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
            _resultDetails.Bind(pokemon, Species(pokemon.speciesId), starter, "Guardado en tu colección.");
            Show(ScreenId.Result);
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
            _pauseButton = pause.gameObject;
            var skills = CompactMenuArt.Button(controls, "RunSkillsButton", "EV", "Blue", OpenSkills);
            _skillsButton = skills.gameObject;
            controls.gameObject.AddComponent<RunMenuControlsLayout>().Configure(pause, skills);
            BuildMain();
            BuildPause();
            BuildSettings();
            _pages.Add(ScreenId.Credits, new CreditsView(_canvas, ShowMainMenu).Page);
            _collection = new CollectionView(_canvas, SelectPokemon, EquipPokemon, AskRelease, OpenGacha, ShowMainMenu);
            _pages.Add(ScreenId.Collection, _collection.Page);
            _gacha = new GachaView(_canvas, PullPool, OpenCollection);
            _pages.Add(ScreenId.Gacha, _gacha.Page);
            BuildStarter();
            var result = Add(ScreenId.Result, "ResultMenu", "¡Un nuevo compañero!", "Cada Pokémon tiene su propia naturaleza y potencial.");
            _resultDetails = new PokemonDetailsView(result.Body);
            UIFactory.Region(_resultDetails.Root, .15f, 0, .85f, 1);
            result.Action("Continue", "CONTINUAR", UIFactory.Sun, CloseResult);
            var release = Add(ScreenId.Release, "ReleaseMenu", "Liberar Pokémon", "Revisa tu elección antes de continuar.");
            _releaseInfo = UIFactory.CreateText("Summary", release.Body, "", 36, UIFactory.Cream);
            UIFactory.Stretch(_releaseInfo.rectTransform, 40);
            release.Action("Cancel", "Conservar Pokémon", UIFactory.Sky, CancelRelease);
            release.Action("Confirm", "Sí, liberar", UIFactory.Coral, ReleasePokemon);
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
            CompactMenuArt.Button(page.Body, "Quit", "Salir", "Red", QuitGame);
        }
        void RefreshHero()
        {
            if (!_pages.TryGetValue(ScreenId.Main, out var page)) return;
            page.Status.text = _equipped == null ? "Elige tu inicial" :
                $"{Species(_equipped.speciesId)?.name} · Rareza {_equipped.rarity} · {PokemonUi.NatureName(_equipped.nature)}";
        }
        void BuildPause()
        {
            var page = Add(ScreenId.Pause, "PauseMenu", "PAUSA", "Partida detenida", CompactMenuKind.Pause);
            CompactMenuArt.Button(page.Body, "Resume", "CONTINUAR", "Green", Resume);
            CompactMenuArt.Button(page.Body, "Skills", "Mejoras EV", "Blue", OpenSkills);
            CompactMenuArt.Button(page.Body, "Settings", "Ajustes", "Amber", OpenSettings);
            CompactMenuArt.Button(page.Body, "Abandon", "Salir de la partida", "Red", AbandonRun);
        }
        void BuildStarter()
        {
            var page = Add(ScreenId.Starter, "StarterMenu", "Elige a tu primer compañero", "Tu inicial es gratuito. Solo puedes elegirlo una vez.");
            var row = UIFactory.CreateRect("Starters", page.Body);
            UIFactory.Region(row, 0, .15f, 1, 1);
            UIFactory.Row(row, 24);
            var ids = PokemonCollection.StarterIds;
            _starterButtons = new UnityEngine.UI.Button[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                int index = i;
                var card = UIFactory.Surface("Starter" + i, row);
                var size = card.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                size.minWidth = size.preferredWidth = 0;
                size.flexibleWidth = 1;
                UIFactory.Stack(card, 24, 14);
                var image = UIFactory.CreateImage("Portrait", card,
                    PokemonCollection.GetSprite(new PokemonInstance { speciesId = ids[i] }), Color.white);
                image.preserveAspect = true;
                UIFactory.Layout(image.transform, 150, 1);
                UIFactory.Label(card, "Name", Species(ids[i])?.name ?? ids[i], 38, UIFactory.Cream, 56, TextAnchor.MiddleCenter);
                _starterButtons[i] = StackButton(card, "Select", "ELEGIR", UIFactory.CardViolet,
                    () => { _starterIndex = index; _message = ""; RefreshStarter(); });
            }
            _starterInfo = UIFactory.CreateText("Odds", page.Body, "", 25, UIFactory.Muted);
            UIFactory.Region(_starterInfo.rectTransform, 0, 0, 1, .13f);
            _starterConfirm = page.Action("ConfirmStarter", "CONFIRMAR INICIAL", UIFactory.Sun, ConfirmStarter);
        }
        void BuildSettings()
        {
            var page = Add(ScreenId.Settings, "SettingsMenu", "A tu manera", "Ajusta el sonido y la calidad visual.");
            var stack = CenterStack(page);
            _volumeValue = UIFactory.Label(stack, "Volume", "", 34, UIFactory.Cream, 56);
            _volumeSlider = UIFactory.CreateSlider(stack, new Vector2(600, 72), _volume, value =>
            {
                _volume = value;
                AudioListener.volume = value;
                PlayerPrefs.SetFloat(VolumeKey, value);
                RefreshSettings();
            });
            UIFactory.Layout(_volumeSlider.transform, 72);
            UIFactory.Label(stack, "QualityTitle", "Calidad gráfica", 34, UIFactory.Cream, 70);
            var quality = StackButton(stack, "Quality", "", UIFactory.CardViolet, () =>
            {
                if (QualitySettings.names.Length <= 1) return;
                _quality = (_quality + 1) % QualitySettings.names.Length;
                QualitySettings.SetQualityLevel(_quality, true);
                PlayerPrefs.SetInt(QualityKey, _quality);
                RefreshSettings();
            });
            _qualityValue = quality.GetComponentInChildren<TMPro.TMP_Text>();
            var hint = UIFactory.Label(stack, "Hint", "Pulsa la calidad para cambiarla. Los ajustes se guardan al volver.", 24, UIFactory.Muted, 80);
            UIFactory.LayoutHeight(hint, 80);
            page.Action("Back", "Guardar y volver", UIFactory.Sun, CloseSettings);
            RefreshSettings();
        }
        void RefreshSettings()
        {
            _volumeValue.text = $"Volumen  ·  {Mathf.RoundToInt(_volume * 100)}%";
            _volumeSlider.SetValueWithoutNotify(_volume);
            if (_qualityValue != null) _qualityValue.text = QualitySettings.names.Length > 0 ? QualitySettings.names[_quality] : "Predeterminada";
        }
        void BuildSkills()
        {
            var page = Add(ScreenId.Skills, "SkillsMenu", "MEJORAS EV", "", CompactMenuKind.Skills);
            string[] names = { "SALUD", "ATAQUE", "DEFENSA", "VELOCIDAD" };
            _skillButtons = new UnityEngine.UI.Button[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int stat = i;
                _skillButtons[i] = CompactMenuArt.Button(page.Body, "EV_" + names[i], "", "Blue",
                    () => { RoundManager.Instance?.BuyEV(stat); RefreshSkills(); });
            }
            page.Action("Back", "Volver", UIFactory.Sun, CloseSkills);
        }
        void RefreshSkills()
        {
            var round = RoundManager.Instance;
            if (round == null) return;
            _pages[ScreenId.Skills].Status.text = $"{round.Points} PT · Solo esta partida";
            string[] names = { "SALUD", "ATAQUE", "DEFENSA", "VELOCIDAD" };
            for (int i = 0; i < 4; i++)
            {
                int level = round.EVLevel(i);
                _skillButtons[i].GetComponentInChildren<TMPro.TMP_Text>().text = $"{names[i]}\n{level}/10 · " +
                    (level >= 10 ? "MÁX." : $"{round.EVCost(i)} PT");
                _skillButtons[i].interactable = round.CanBuyEV(i);
            }
        }
        static RectTransform CenterStack(MenuPage page)
        {
            var root = UIFactory.Surface("Card", page.Body);
            UIFactory.Region(root, .18f, 0, .82f, 1);
            return UIFactory.ScrollStack(root, 28, 20);
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
                case "PersistenceFailure":
                case "SaveUnavailable": return "No se pudo guardar. Reinicia y revisa el almacenamiento.";
                case "InstanceNotFound": return "Ese Pokémon ya no está en tu colección.";
                default: return "No se pudo completar la operación.";
            }
        }
    }
}

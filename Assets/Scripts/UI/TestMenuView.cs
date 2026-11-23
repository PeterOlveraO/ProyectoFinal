using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Pokemon.Progression;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Compact, scrollable test rewards form. All mutations go through the controller/service.</summary>
    public sealed class TestMenuView : MonoBehaviour
    {
        public MenuPage Page { get; private set; }
        readonly List<UnityEngine.UI.LayoutElement> _rows = new List<UnityEngine.UI.LayoutElement>();
        readonly List<UnityEngine.UI.LayoutElement> _captions = new List<UnityEngine.UI.LayoutElement>();
        readonly List<UnityEngine.UI.Selectable> _controls = new List<UnityEngine.UI.Selectable>();
        readonly UnityEngine.UI.Button[] _rarities = new UnityEngine.UI.Button[3];
        IReadOnlyList<SpeciesDefinition> _catalog;
        RectTransform _safe, _content;
        Canvas _canvas;
        TMPro.TMP_Text _summary, _species, _title;
        TMPro.TMP_InputField _amount;
        UnityEngine.UI.LayoutElement _previousWidth, _nextWidth;
        UnityEngine.UI.Button _addPokemon;
        int _speciesIndex;
        Rarity _rarity;
        Vector2 _lastSize;
        float _lastScale;

        public static TestMenuView Create(Transform canvas, Action<int> coins,
            Action<string, Rarity> pokemon, UnityEngine.Events.UnityAction back)
        {
            var page = new MenuPage(canvas, "TestMenu", "PRUEBAS", "");
            var view = page.Root.gameObject.AddComponent<TestMenuView>();
            view.Build(page, coins, pokemon, back);
            return view;
        }

        void Build(MenuPage page, Action<int> coins, Action<string, Rarity> pokemon,
            UnityEngine.Events.UnityAction back)
        {
            Page = page;
            Page.SetSize(new Vector2(1120, 800));
            _safe = (RectTransform)Page.Root.Find("SafeArea");
            _canvas = GetComponentInParent<Canvas>();
            _title = Page.Root.Find("SafeArea/Content/Title").GetComponent<TMPro.TMP_Text>();
            Page.Wallet.gameObject.SetActive(false);
            var surface = UIFactory.Surface("Rewards", Page.Body);
            UIFactory.Stretch(surface);
            _content = UIFactory.ScrollStack(surface, 12, 12);
            _summary = Caption("Summary", "");
            var presets = Row("CoinPresets");
            foreach (int value in new[] { 100, 1000, 10000 })
            {
                int amount = value;
                Button(presets, "Coins" + amount, "+" + amount, "Amber", () => coins(amount));
            }
            var custom = Row("CustomCoins");
            _amount = AmountInput(custom);
            Button(custom, "GrantCoins", "AÑADIR", "Green", () =>
            {
                if (!TryParseAmount(_amount.text, out int value))
                { SetFeedback("Escribe una cantidad entera positiva.", true); return; }
                coins(value);
            });

            Caption("SpeciesTitle", "ESPECIE DEL CATÁLOGO");
            var speciesRow = Row("SpeciesSelector");
            var previous = Button(speciesRow, "PreviousSpecies", "<", "Blue", () => CycleSpecies(-1));
            _previousWidth = previous.GetComponent<UnityEngine.UI.LayoutElement>();
            _previousWidth.flexibleWidth = 0;
            _species = UIFactory.CreateText("SpeciesName", speciesRow, "", 28, UIFactory.Cream);
            Flexible(_species.transform);
            var next = Button(speciesRow, "NextSpecies", ">", "Blue", () => CycleSpecies(1));
            _nextWidth = next.GetComponent<UnityEngine.UI.LayoutElement>();
            _nextWidth.flexibleWidth = 0;
            Caption("RarityTitle", "CLASE / RAREZA");
            var rarities = Row("Rarities");
            for (int i = 0; i < _rarities.Length; i++)
            {
                var rarity = (Rarity)i;
                _rarities[i] = Button(rarities, "Rarity" + rarity, rarity.ToString(), "Blue",
                    () => { _rarity = rarity; RefreshSelection(); });
            }
            _addPokemon = Button(_content, "GrantPokemon", "AÑADIR POKÉMON", "Green", () =>
            {
                if (_catalog != null && _catalog.Count > 0) pokemon(_catalog[_speciesIndex].id, _rarity);
            });
            _rows.Add(_addPokemon.GetComponent<UnityEngine.UI.LayoutElement>());
            Page.Action("Back", "VOLVER", UIFactory.Sun, back);
            SetFeedback("Colección normal · guardado automático.");
            RefreshLayout();
        }

        public void Bind(IReadOnlyList<SpeciesDefinition> catalog, int coins, int count, int capacity, bool ready)
        {
            _catalog = catalog;
            _speciesIndex = Mathf.Clamp(_speciesIndex, 0, Mathf.Max(0, catalog.Count - 1));
            _summary.text = ready ? $"{coins:N0} MONEDAS · {count}/{capacity} POKÉMON" : "PROGRESO NO DISPONIBLE";
            foreach (var control in _controls) control.interactable = ready;
            _addPokemon.interactable = ready && catalog.Count > 0;
            RefreshSelection();
        }

        public void SetFeedback(string text, bool error = false)
        {
            Page.Status.text = text;
            Page.Status.color = error ? UIFactory.Coral : UIFactory.Muted;
        }

        public static bool TryParseAmount(string text, out int amount) =>
            int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out amount) && amount > 0;

        void CycleSpecies(int step)
        {
            if (_catalog == null || _catalog.Count == 0) return;
            _speciesIndex = (_speciesIndex + step + _catalog.Count) % _catalog.Count;
            RefreshSelection();
        }

        void RefreshSelection()
        {
            _species.text = _catalog == null || _catalog.Count == 0 ? "SIN ESPECIES"
                : $"{_catalog[_speciesIndex].name}\n{_speciesIndex + 1}/{_catalog.Count}";
            for (int i = 0; i < _rarities.Length; i++)
            {
                var text = _rarities[i].GetComponentInChildren<TMPro.TMP_Text>();
                text.text = i == (int)_rarity ? $"[{(Rarity)i}]" : ((Rarity)i).ToString();
                text.color = i == (int)_rarity ? UIFactory.Sun : UIFactory.Cream;
            }
        }

        RectTransform Row(string name)
        {
            var row = UIFactory.CreateRect(name, _content);
            _rows.Add(UIFactory.Layout(row, 88));
            UIFactory.Row(row, 12);
            return row;
        }

        TMPro.TMP_Text Caption(string name, string text)
        {
            var label = UIFactory.Label(_content, name, text, 28, UIFactory.Cream, 44);
            _captions.Add(label.GetComponent<UnityEngine.UI.LayoutElement>());
            return label;
        }

        static UnityEngine.UI.LayoutElement Flexible(Transform transform)
        {
            var layout = transform.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minWidth = layout.preferredWidth = 0;
            layout.flexibleWidth = 1;
            return layout;
        }

        UnityEngine.UI.Button Button(Transform parent, string name, string label, string frame,
            UnityEngine.Events.UnityAction action)
        {
            var button = CompactMenuArt.Button(parent, name, label, frame, action);
            Flexible(button.transform);
            _controls.Add(button);
            return button;
        }

        TMPro.TMP_InputField AmountInput(Transform parent)
        {
            var image = UIFactory.CreateImage("CoinAmount", parent, RetroUiArt.Frame, UIFactory.Inner, true, true);
            Flexible(image.transform).flexibleWidth = 1.5f;
            var viewport = UIFactory.CreateRect("TextArea", image.transform);
            UIFactory.Stretch(viewport, 16);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text = UIFactory.CreateText("InputText", viewport, "", 28, UIFactory.Cream, TextAnchor.MiddleLeft);
            UIFactory.Stretch(text.rectTransform);
            var placeholder = UIFactory.CreateText("Placeholder", viewport, "Cantidad positiva", 28,
                UIFactory.Muted, TextAnchor.MiddleLeft);
            UIFactory.Stretch(placeholder.rectTransform);
            var input = image.gameObject.AddComponent<TMPro.TMP_InputField>();
            input.targetGraphic = image;
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.contentType = TMPro.TMP_InputField.ContentType.IntegerNumber;
            input.lineType = TMPro.TMP_InputField.LineType.SingleLine;
            input.characterLimit = 10;
            input.richText = false;
            input.SetTextWithoutNotify("100");
            image.gameObject.AddComponent<UIFocusFeedback>();
            _controls.Add(input);
            return input;
        }

        void OnEnable() { if (Page != null) RefreshLayout(); }
        void OnDisable() { if (_amount != null) _amount.DeactivateInputField(); }
        void LateUpdate()
        {
            if (Page != null && (_lastSize != _safe.rect.size || _lastScale != _canvas.scaleFactor))
                RefreshLayout();
        }

        public void RefreshLayout()
        {
            if (Page == null) return;
            Page.Root.GetComponent<MenuPageLayout>().Refresh();
            _lastSize = _safe.rect.size;
            _lastScale = Mathf.Max(.01f, _canvas.scaleFactor);
            float font = Mathf.Max(28, 14 / _lastScale);
            float touch = Mathf.Max(88, 48 / _lastScale);
            float gap = Mathf.Max(12, 6 / _lastScale);
            float titleHeight = font * 1.5f;
            float statusHeight = font * 2.5f;
            UIFactory.Region(_title.rectTransform, 0, 1, 1, 1);
            _title.rectTransform.offsetMin = new Vector2(gap, -gap - titleHeight);
            _title.rectTransform.offsetMax = new Vector2(-gap, -gap);
            UIFactory.Region(Page.Status.rectTransform, 0, 1, 1, 1);
            Page.Status.rectTransform.offsetMin = new Vector2(gap, -gap - titleHeight - statusHeight);
            Page.Status.rectTransform.offsetMax = new Vector2(-gap, -gap - titleHeight);
            Page.Body.offsetMin = new Vector2(gap, touch + gap * 2);
            Page.Body.offsetMax = new Vector2(-gap, -gap * 2 - titleHeight - statusHeight);
            Page.Footer.offsetMin = new Vector2(gap, gap);
            Page.Footer.offsetMax = new Vector2(-gap, gap + touch);
            foreach (var row in _rows) row.minHeight = row.preferredHeight = touch;
            foreach (var caption in _captions) caption.minHeight = caption.preferredHeight = font * 1.5f;
            _previousWidth.minWidth = _previousWidth.preferredWidth = touch;
            _nextWidth.minWidth = _nextWidth.preferredWidth = touch;
            foreach (var text in Page.Root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                text.fontSize = font;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }
    }
}

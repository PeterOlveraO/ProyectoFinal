using System;
using System.Collections.Generic;
using Game.Pokemon;
using Game.Pokemon.Progression;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Inventory presentation; ownership and transactions stay in the controller/service.</summary>
    public sealed class CollectionView
    {
        public MenuPage Page { get; }
        readonly PokemonDetailsView _details;
        readonly UnityEngine.UI.Button _equip, _release;
        readonly List<Slot> _slots = new List<Slot>();
        readonly RectTransform _content;
        readonly Action<int> _select;

        sealed class Slot
        {
            public UnityEngine.UI.Button button;
            public UnityEngine.UI.Image portrait;
            public TMPro.TMP_Text name, badge;
        }

        public CollectionView(Transform canvas, Action<int> select, UnityEngine.Events.UnityAction equip,
            UnityEngine.Events.UnityAction release, UnityEngine.Events.UnityAction gacha, UnityEngine.Events.UnityAction back)
        {
            _select = select;
            Page = new MenuPage(canvas, "CollectionMenu", "Tu colección", "Elige un compañero para la próxima partida.");
            var scrollRoot = UIFactory.Surface("Inventory", Page.Body, UIFactory.Ink);
            UIFactory.Region(scrollRoot, 0, 0, .51f, 1);
            scrollRoot.offsetMax = new Vector2(-20, 0);
            var scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45;
            var viewport = UIFactory.CreateRect("Viewport", scrollRoot);
            UIFactory.Stretch(viewport, 6);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            // The background receives wheel/drag events even between cards.
            scrollRoot.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            _content = UIFactory.CreateRect("Content", viewport);
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(.5f, 1);
            _content.sizeDelta = Vector2.zero;
            var grid = _content.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.spacing = new Vector2(16, 16);
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            _content.gameObject.AddComponent<InventoryGridLayout>();
            var fitter = _content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = _content;
            _details = new PokemonDetailsView(Page.Body);
            UIFactory.Region(_details.Root, .51f, 0, 1, 1);
            _equip = Page.Action("Equip", "EQUIPAR", UIFactory.Sun, equip);
            _release = Page.Action("Release", "Liberar", UIFactory.CardViolet, release);
            Page.Action("Gacha", "Conseguir Pokémon", UIFactory.Sky, gacha);
            Page.Action("Back", "Volver", UIFactory.CardViolet, back);
        }

        void EnsureSlots(int capacity)
        {
            while (_slots.Count < capacity)
            {
                int index = _slots.Count;
                var button = UIFactory.CreateButton(_content, "Slot" + index, "", UIFactory.CardViolet,
                    new Vector2(210, 210), () => _select(index), 24);
                var label = button.GetComponentInChildren<TMPro.TMP_Text>();
                UIFactory.Region(label.rectTransform, 0, 0, 1, .25f, 8);
                var portrait = UIFactory.CreateImage("Portrait", button.transform, null, Color.white);
                portrait.preserveAspect = true;
                UIFactory.Region(portrait.rectTransform, .15f, .25f, .85f, .84f);
                var badge = UIFactory.CreateText("Badge", button.transform, "", 20, UIFactory.Muted, TextAnchor.MiddleLeft);
                UIFactory.Region(badge.rectTransform, 0, .82f, 1, 1);
                badge.rectTransform.offsetMin = new Vector2(10, 2);
                badge.rectTransform.offsetMax = new Vector2(-10, -2);
                _slots.Add(new Slot { button = button, portrait = portrait, name = label, badge = badge });
            }
        }

        public void Bind(IReadOnlyList<PokemonInstance> inventory, PokemonInstance equipped,
            Func<string, SpeciesDefinition> species, int selected, int capacity, int coins, bool busy, string message)
        {
            EnsureSlots(capacity);
            Page.Wallet.text = $"{coins:N0} MONEDAS";
            Page.Status.text = $"{inventory.Count} / {capacity} espacios  ·  Selecciona una tarjeta para ver sus estadísticas";
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                slot.button.gameObject.SetActive(i < capacity);
                bool exists = i < inventory.Count;
                var pokemon = exists ? inventory[i] : null;
                bool isEquipped = pokemon != null && pokemon.instanceId == equipped?.instanceId;
                slot.button.interactable = exists && !busy;
                slot.button.image.color = exists && i == selected
                    ? Color.Lerp(UIFactory.CardViolet, UIFactory.Sky, .32f) : UIFactory.CardViolet;
                slot.name.text = exists ? species(pokemon.speciesId)?.name ?? pokemon.speciesId : "Espacio libre";
                slot.name.color = exists ? UIFactory.Cream : UIFactory.Muted;
                slot.portrait.sprite = exists ? PokemonCollection.GetSprite(pokemon) : null;
                slot.portrait.enabled = slot.portrait.sprite != null;
                slot.badge.text = exists ? $"{pokemon.rarity}" + (isEquipped ? "  ·  EQUIPADO" : i == selected ? "  ·  SELECCIÓN" : "") : $"{i + 1:00}";
                slot.badge.color = exists ? UIFactory.RarityColor(pokemon.rarity) : UIFactory.Muted;
            }
            var chosen = selected >= 0 && selected < inventory.Count ? inventory[selected] : null;
            bool equippedSelection = chosen != null && chosen.instanceId == equipped?.instanceId;
            _details.Bind(chosen, chosen == null ? null : species(chosen.speciesId), equippedSelection, message);
            _equip.interactable = chosen != null && !busy && !equippedSelection;
            _equip.GetComponentInChildren<TMPro.TMP_Text>().text = equippedSelection ? "EQUIPADO" : "EQUIPAR";
            _release.interactable = chosen != null && inventory.Count > 1 && !busy;
        }
    }

    [RequireComponent(typeof(UnityEngine.UI.GridLayoutGroup))]
    public sealed class InventoryGridLayout : MonoBehaviour
    {
        UnityEngine.UI.GridLayoutGroup _grid;
        float _width = -1;
        void OnEnable() { _grid = GetComponent<UnityEngine.UI.GridLayoutGroup>(); Resize(); }
        void OnRectTransformDimensionsChange() => Resize();
        void Resize()
        {
            if (_grid == null) return;
            float width = ((RectTransform)transform).rect.width;
            if (width < 1 || Mathf.Abs(width - _width) < 1) return;
            _width = width;
            int columns = Mathf.Clamp(Mathf.FloorToInt(width / 220f), 2, 4);
            _grid.constraintCount = columns;
            _grid.cellSize = new Vector2((width - 24 - (columns - 1) * 16) / columns, 210);
        }
    }
}

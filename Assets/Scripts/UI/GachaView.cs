using System;
using System.Collections.Generic;
using System.Linq;
using Game.Pokemon;
using Game.Pokemon.Progression;
using UnityEngine;

namespace Game.UI
{
    public sealed class GachaView
    {
        public MenuPage Page { get; }
        readonly List<Offer> _offers = new List<Offer>();
        readonly Action<int> _pull;
        sealed class Offer
        {
            public RectTransform root;
            public TMPro.TMP_Text title, species, notice;
            public PokemonIvView[] classes;
            public UnityEngine.UI.Button buy;
            public UnityEngine.UI.Image[] portraits;
        }

        public GachaView(Transform canvas, Action<int> pull, UnityEngine.Events.UnityAction back)
        {
            _pull = pull;
            Page = new MenuPage(canvas, "GachaMenu", "Gacha", "");
            UIFactory.Row(Page.Body, 24);
            Page.Action("Back", "Volver", UIFactory.CardViolet, back);
        }

        Offer Create(int index)
        {
            var root = UIFactory.Surface("Pool" + index, Page.Body);
            var width = root.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            width.flexibleWidth = 1;
            width.minWidth = 0;
            width.preferredWidth = 0;
            // Keep the price/action visible while long pool lists scroll above it.
            var info = UIFactory.Surface("Information", root, UIFactory.CardViolet);
            UIFactory.Stretch(info);
            info.offsetMin = new Vector2(8, 104);
            info.offsetMax = new Vector2(-8, -8);
            var content = UIFactory.ScrollStack(info, 12, 6);
            var offer = new Offer { root = root };
            offer.title = UIFactory.Label(content, "Name", "", 28, UIFactory.Cream, 40);
            var classes = UIFactory.CreateRect("Classes", content);
            UIFactory.Layout(classes, 192);
            UIFactory.Row(classes, 8);
            offer.classes = new PokemonIvView[3];
            for (int i = 0; i < 3; i++)
            {
                offer.classes[i] = new PokemonIvView(classes);
                var layout = offer.classes[i].Root.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                layout.minWidth = layout.preferredWidth = 0;
                layout.flexibleWidth = 1;
            }
            var art = UIFactory.Surface("FeaturedPokemon", content, UIFactory.Ink);
            UIFactory.Layout(art, 96);
            UIFactory.Row(art, 12);
            offer.portraits = new UnityEngine.UI.Image[3];
            for (int i = 0; i < 3; i++)
            {
                var image = UIFactory.PortraitStage(art, "Pokemon" + i, null);
                var element = image.transform.parent.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                element.minWidth = 0;
                element.flexibleWidth = 1;
                offer.portraits[i] = image;
            }
            offer.species = UIFactory.Label(content, "Species", "", 20, UIFactory.Cream, 72, TextAnchor.UpperLeft);
            offer.notice = UIFactory.Label(content, "Availability", "", 21, UIFactory.Muted, 40);
            UIFactory.LayoutHeight(offer.notice, 40);
            offer.buy = UIFactory.CreateButton(root, "Pull", "", index == 0 ? UIFactory.Sky : UIFactory.Sun,
                new Vector2(500, 76), () => _pull(index), 30, confirm: true);
            var buyRect = (RectTransform)offer.buy.transform;
            UIFactory.Region(buyRect, 0, 0, 1, 0);
            buyRect.pivot = new Vector2(.5f, 0);
            buyRect.offsetMin = new Vector2(20, 18);
            buyRect.offsetMax = new Vector2(-20, 94);
            _offers.Add(offer);
            return offer;
        }

        public void Bind(IReadOnlyList<GachaPool> pools, Func<string, SpeciesDefinition> species,
            int coins, int count, int capacity, bool busy, bool ready, string message)
        {
            Page.Wallet.text = $"{coins:N0} MONEDAS";
            Page.Status.text = string.IsNullOrEmpty(message) ? $"{count} / {capacity}" : message;
            while (_offers.Count < pools.Count) Create(_offers.Count);
            for (int i = 0; i < _offers.Count; i++)
            {
                var offer = _offers[i];
                offer.root.gameObject.SetActive(i < pools.Count);
                if (i >= pools.Count) continue;
                var pool = pools[i];
                offer.title.text = pool.id == "basic" ? "Básica" : pool.id == "premium" ? "Especial" : pool.id;
                int total = pool.weightF + pool.weightA + pool.weightS;
                int[] weights = { pool.weightF, pool.weightA, pool.weightS };
                for (int c = 0; c < 3; c++)
                    offer.classes[c].Bind((Rarity)c, PokemonIvView.ClassPreview((Rarity)c), PokemonUi.Percent(weights[c], total) + " · IV");
                offer.species.text = string.Join(" · ", pool.species.Select(id => species(id)?.name ?? id));
                UIFactory.LayoutHeight(offer.species, 72);
                for (int p = 0; p < offer.portraits.Length; p++)
                {
                    var portrait = offer.portraits[p];
                    portrait.sprite = p < pool.species.Length
                        ? PokemonCollection.GetSprite(new PokemonInstance { speciesId = pool.species[p] }) : null;
                    portrait.enabled = portrait.sprite != null;
                }
                string reason = !ready ? "Progreso no disponible" : count >= capacity ? "Colección llena: libera un espacio"
                    : coins < pool.price ? $"Faltan {pool.price - coins} monedas" : busy ? "Procesando…" : "";
                offer.notice.text = reason;
                offer.notice.gameObject.SetActive(!string.IsNullOrEmpty(reason));
                offer.notice.color = ready && count < capacity && coins >= pool.price ? UIFactory.Muted : UIFactory.Coral;
                offer.buy.interactable = ready && !busy && count < capacity && coins >= pool.price;
                offer.buy.GetComponentInChildren<TMPro.TMP_Text>().text = $"TIRAR  ·  {pool.price} MONEDAS";
            }
        }
    }
}

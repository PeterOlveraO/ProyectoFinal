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
            public TMPro.TMP_Text title, odds, species, notice;
            public UnityEngine.UI.Button buy;
            public UnityEngine.UI.Image[] portraits;
        }

        public GachaView(Transform canvas, Action<int> pull, UnityEngine.Events.UnityAction back)
        {
            _pull = pull;
            Page = new MenuPage(canvas, "GachaMenu", "Encuentra a tu próximo compañero",
                "Cada tirada añade un Pokémon a tu colección. Los duplicados son independientes.");
            UIFactory.Row(Page.Body, 24);
            Page.Action("Back", "Volver a la colección", UIFactory.CardViolet, back);
        }

        Offer Create(int index)
        {
            var root = UIFactory.Surface("Pool" + index, Page.Body);
            var width = root.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            width.flexibleWidth = 1;
            width.minWidth = 0;
            width.preferredWidth = 0;
            UIFactory.Stack(root, 24, 12);
            var offer = new Offer { root = root };
            offer.title = UIFactory.Label(root, "Name", "", 36, index == 0 ? UIFactory.Cream : UIFactory.Sun, 48);
            var art = UIFactory.CreateRect("FeaturedPokemon", root);
            UIFactory.Layout(art, 120, 1);
            UIFactory.Row(art, 12);
            offer.portraits = new UnityEngine.UI.Image[3];
            for (int i = 0; i < 3; i++)
            {
                var image = UIFactory.CreateImage("Pokemon" + i, art, null, Color.white);
                image.preserveAspect = true;
                var element = image.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                element.minWidth = 0;
                element.flexibleWidth = 1;
                offer.portraits[i] = image;
            }
            offer.odds = UIFactory.Label(root, "Odds", "", 25, UIFactory.Sun, 40);
            offer.species = UIFactory.Label(root, "Species", "", 22, UIFactory.Muted, 112, TextAnchor.UpperLeft);
            offer.notice = UIFactory.Label(root, "Availability", "", 23, UIFactory.Muted, 34);
            offer.buy = UIFactory.CreateButton(root, "Pull", "", index == 0 ? UIFactory.Sky : UIFactory.Sun,
                new Vector2(500, 76), () => _pull(index), 30);
            UIFactory.Layout(offer.buy.transform, 76);
            _offers.Add(offer);
            return offer;
        }

        public void Bind(IReadOnlyList<GachaPool> pools, Func<string, SpeciesDefinition> species,
            int coins, int count, int capacity, bool busy, bool ready, string message)
        {
            Page.Wallet.text = $"{coins:N0} MONEDAS";
            Page.Status.text = string.IsNullOrEmpty(message)
                ? $"{count} / {capacity} espacios ocupados  ·  Especies equiprobables dentro de cada máquina" : message;
            while (_offers.Count < pools.Count) Create(_offers.Count);
            for (int i = 0; i < _offers.Count; i++)
            {
                var offer = _offers[i];
                offer.root.gameObject.SetActive(i < pools.Count);
                if (i >= pools.Count) continue;
                var pool = pools[i];
                offer.title.text = pool.id == "basic" ? "Colección básica" : pool.id == "premium" ? "Selección especial" : pool.id;
                int total = pool.weightF + pool.weightA + pool.weightS;
                offer.odds.text = $"F  {PokemonUi.Percent(pool.weightF, total)}     A  {PokemonUi.Percent(pool.weightA, total)}     S  {PokemonUi.Percent(pool.weightS, total)}";
                offer.species.text = "POKÉMON DISPONIBLES\n" + string.Join(" · ", pool.species.Select(id => species(id)?.name ?? id));
                for (int p = 0; p < offer.portraits.Length; p++)
                {
                    var portrait = offer.portraits[p];
                    portrait.sprite = p < pool.species.Length
                        ? PokemonCollection.GetSprite(new PokemonInstance { speciesId = pool.species[p] }) : null;
                    portrait.enabled = portrait.sprite != null;
                }
                string reason = !ready ? "Progreso no disponible" : count >= capacity ? "Colección llena: libera un espacio"
                    : coins < pool.price ? $"Te faltan {pool.price - coins} monedas" : busy ? "Procesando…" : "Un nuevo compañero en cada tirada";
                offer.notice.text = reason;
                offer.notice.color = ready && count < capacity && coins >= pool.price ? UIFactory.Muted : UIFactory.Coral;
                offer.buy.interactable = ready && !busy && count < capacity && coins >= pool.price;
                offer.buy.GetComponentInChildren<TMPro.TMP_Text>().text = $"TIRAR  ·  {pool.price} MONEDAS";
            }
        }
    }
}

using Game.Pokemon;
using Game.Pokemon.Progression;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Reusable read-only Pokémon sheet for inventory and acquisition results.</summary>
    public sealed class PokemonDetailsView
    {
        readonly UnityEngine.UI.Image _portrait;
        readonly TMPro.TMP_Text _name, _nature;
        readonly PokemonIvView _ivView;
        public TMPro.TMP_Text Message { get; }
        public RectTransform Root { get; }

        public PokemonDetailsView(Transform parent, bool portraitHeader = true)
        {
            Root = UIFactory.Surface("PokemonDetails", parent);
            var content = UIFactory.ScrollStack(Root, 16, 8);
            var header = UIFactory.CreateRect("Identity", content);
            UIFactory.Layout(header, portraitHeader ? 110 : 48);
            _portrait = UIFactory.CreateImage("Portrait", header, null, Color.white);
            _portrait.preserveAspect = true;
            _portrait.gameObject.SetActive(portraitHeader);
            UIFactory.Place(_portrait.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f),
                Vector2.zero, new Vector2(100, 100));
            var identity = UIFactory.CreateRect("Labels", header);
            UIFactory.Stretch(identity);
            identity.offsetMin = new Vector2(portraitHeader ? 112 : 0, 0);
            UIFactory.Stack(identity, 0, 8);
            _name = UIFactory.Label(identity, "Name", "", 30, UIFactory.Cream, 44);
            _name.gameObject.SetActive(portraitHeader);
            _nature = UIFactory.Label(identity, "Nature", "", 22, UIFactory.Muted, 36);
            _ivView = new PokemonIvView(content);
            UIFactory.Layout(_ivView.Root, 192);
            Message = UIFactory.Label(content, "Message", "", 22, UIFactory.Cream, 48);
            UIFactory.LayoutHeight(Message, 48);
        }

        public void Bind(PokemonInstance pokemon, SpeciesDefinition species, bool equipped, string message)
        {
            _portrait.sprite = pokemon == null ? null : PokemonCollection.GetSprite(pokemon);
            _portrait.enabled = _portrait.sprite != null;
            _name.text = species?.name ?? "Sin Pokémon";
            _nature.text = pokemon == null ? "" : PokemonUi.NatureName(pokemon.nature);
            _ivView.Root.gameObject.SetActive(pokemon != null);
            if (pokemon != null) _ivView.Bind(pokemon.rarity, pokemon.ivs, equipped ? "IV · EQUIPADO" : "IV");
            Message.text = message;
            Message.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
    }

    internal static class PokemonUi
    {
        static readonly string[] Natures = { "Fuerte", "Huraña", "Audaz", "Firme", "Pícara",
            "Osada", "Dócil", "Plácida", "Agitada", "Floja", "Miedosa", "Activa", "Seria", "Alegre",
            "Ingenua", "Modesta", "Afable", "Mansa", "Tímida", "Alocada", "Serena", "Amable",
            "Grosera", "Cauta", "Rara" };
        public static string NatureName(Nature nature) =>
            (int)nature >= 0 && (int)nature < Natures.Length ? Natures[(int)nature] : nature.ToString();
        public static string Percent(int value, int total) =>
            total > 0 ? (100f * value / total).ToString("0.##") + "%" : "0%";
    }
}

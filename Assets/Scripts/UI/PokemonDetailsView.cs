using Game.Pokemon;
using Game.Pokemon.Progression;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Reusable read-only Pokémon sheet for inventory and acquisition results.</summary>
    public sealed class PokemonDetailsView
    {
        readonly UnityEngine.UI.Image _portrait;
        readonly TMPro.TMP_Text _name, _category, _nature;
        readonly TMPro.TMP_Text[] _base = new TMPro.TMP_Text[4];
        readonly TMPro.TMP_Text[] _ivs = new TMPro.TMP_Text[4];
        readonly TMPro.TMP_Text[] _total = new TMPro.TMP_Text[4];
        public TMPro.TMP_Text Message { get; }
        public RectTransform Root { get; }

        public PokemonDetailsView(Transform parent)
        {
            Root = UIFactory.Surface("PokemonDetails", parent);
            UIFactory.Stack(Root, 28, 14);
            var header = UIFactory.CreateRect("Identity", Root);
            UIFactory.Layout(header, 150);
            _portrait = UIFactory.CreateImage("Portrait", header, null, Color.white);
            _portrait.preserveAspect = true;
            UIFactory.Place(_portrait.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f),
                Vector2.zero, new Vector2(150, 150));
            var identity = UIFactory.CreateRect("Labels", header);
            UIFactory.Stretch(identity);
            identity.offsetMin = new Vector2(174, 0);
            UIFactory.Stack(identity, 0, 8);
            _category = UIFactory.Label(identity, "Rarity", "", 24, UIFactory.Sun, 32);
            _name = UIFactory.Label(identity, "Name", "", 42, UIFactory.Cream, 56);
            _nature = UIFactory.Label(identity, "Nature", "", 24, UIFactory.Muted, 40);

            var columns = UIFactory.CreateRect("Columns", Root);
            UIFactory.Layout(columns, 32);
            Column(columns, "Stat", "ESTADÍSTICA", 0, .43f, UIFactory.Muted, 20);
            Column(columns, "Base", "BASE", .43f, .62f, UIFactory.Muted, 20);
            Column(columns, "IV", "IV", .62f, .77f, UIFactory.Muted, 20);
            Column(columns, "Total", "TOTAL", .77f, 1, UIFactory.Sun, 20);
            string[] names = { "Salud", "Ataque", "Defensa", "Velocidad" };
            for (int i = 0; i < names.Length; i++)
            {
                var row = UIFactory.Surface(names[i], Root, i % 2 == 0 ? UIFactory.DeepViolet : UIFactory.CardViolet);
                UIFactory.Layout(row, 52);
                Column(row, "Name", names[i], 0, .43f, UIFactory.Cream);
                _base[i] = Column(row, "Base", "", .43f, .62f, UIFactory.Muted);
                _ivs[i] = Column(row, "IV", "", .62f, .77f, UIFactory.Muted);
                _total[i] = Column(row, "Total", "", .77f, 1, UIFactory.Cream);
                _total[i].fontStyle = TMPro.FontStyles.Bold;
            }
            UIFactory.Label(Root, "Explanation", "Total con naturaleza · sin EV de partida", 21, UIFactory.Muted, 30);
            Message = UIFactory.Label(Root, "Message", "", 24, UIFactory.Lime, 54);
        }

        static TMPro.TMP_Text Column(RectTransform parent, string name, string value,
            float min, float max, Color color, int size = 25)
        {
            var label = UIFactory.CreateText(name, parent, value, size, color,
                min == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            UIFactory.Region(label.rectTransform, min, 0, max, 1);
            label.rectTransform.offsetMin = new Vector2(12, 2);
            label.rectTransform.offsetMax = new Vector2(-12, -2);
            return label;
        }

        public void Bind(PokemonInstance pokemon, SpeciesDefinition species, bool equipped, string message)
        {
            _portrait.sprite = pokemon == null ? null : PokemonCollection.GetSprite(pokemon);
            _portrait.enabled = _portrait.sprite != null;
            _name.text = species?.name ?? "Sin Pokémon";
            _category.text = pokemon == null ? "" : $"RAREZA {pokemon.rarity}" + (equipped ? "  ·  EQUIPADO" : "");
            _category.color = pokemon == null ? UIFactory.Muted : UIFactory.RarityColor(pokemon.rarity);
            _nature.text = pokemon == null ? "" : "Naturaleza " + PokemonUi.NatureName(pokemon.nature);
            var baseStats = species?.baseStats ?? new StatBlock();
            var ivs = pokemon?.ivs ?? new StatBlock();
            var total = pokemon == null ? new StatBlock() :
                PokemonStats.Calculate(baseStats, ivs, new StatBlock(), pokemon.nature);
            for (int i = 0; i < 4; i++)
            {
                _base[i].text = Value(baseStats, i).ToString();
                _ivs[i].text = "+" + Value(ivs, i);
                _total[i].text = Value(total, i).ToString();
            }
            Message.text = message;
        }

        static int Value(StatBlock stats, int index) =>
            index == 0 ? stats.health : index == 1 ? stats.attack : index == 2 ? stats.defense : stats.speed;
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

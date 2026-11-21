using Game.Pokemon.Progression;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Class badge and four actual IV values, on a shared 0..31 scale.</summary>
    public sealed class PokemonIvView
    {
        public RectTransform Root { get; }
        readonly TMPro.TMP_Text _badge, _caption;
        readonly TMPro.TMP_Text[] _values = new TMPro.TMP_Text[4];
        readonly UnityEngine.UI.Image[] _bars = new UnityEngine.UI.Image[4];

        public PokemonIvView(Transform parent)
        {
            Root = UIFactory.Surface("ClassIV", parent, UIFactory.Black);
            UIFactory.Stack(Root, 10, 6);
            var header = UIFactory.CreateRect("ClassHeader", Root);
            UIFactory.Layout(header, 36);
            var badge = UIFactory.Surface("Badge", header, UIFactory.Inner);
            UIFactory.Place(badge, new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(36, 34));
            _badge = UIFactory.CreateText("Class", badge, "", 24, UIFactory.Cream);
            UIFactory.Stretch(_badge.rectTransform);
            _caption = UIFactory.CreateText("Probability", header, "", 20, UIFactory.Cream, TextAnchor.MiddleRight);
            UIFactory.Region(_caption.rectTransform, .27f, 0, 1, 1);
            string[] names = { "PS", "ATQ", "DEF", "VEL" };
            for (int i = 0; i < 4; i++)
            {
                var row = UIFactory.CreateRect(names[i], Root);
                UIFactory.Layout(row, 28);
                var label = UIFactory.CreateText("Stat", row, names[i], 17, UIFactory.Muted, TextAnchor.MiddleLeft);
                UIFactory.Region(label.rectTransform, 0, 0, .25f, 1);
                var track = UIFactory.CreateImage("Track", row, null, UIFactory.Inner);
                UIFactory.Region(track.rectTransform, .27f, .35f, .65f, .65f);
                _bars[i] = UIFactory.CreateImage("IV", track.transform, null, UIFactory.Muted);
                UIFactory.Stretch(_bars[i].rectTransform);
                _values[i] = UIFactory.CreateText("Value", row, "", 17, UIFactory.Cream, TextAnchor.MiddleRight);
                UIFactory.Region(_values[i].rectTransform, .67f, 0, 1, 1);
            }
        }

        public void Bind(Rarity rarity, StatBlock ivs, string caption = "IV")
        {
            _badge.text = rarity.ToString();
            _badge.color = UIFactory.RarityColor(rarity);
            _caption.text = caption;
            int[] values = { ivs.health, ivs.attack, ivs.defense, ivs.speed };
            for (int i = 0; i < 4; i++)
            {
                _values[i].text = $"{values[i]}/31";
                _bars[i].rectTransform.anchorMax = new Vector2(Mathf.Clamp01(values[i] / 31f), 1);
                _bars[i].color = UIFactory.RarityColor(rarity);
            }
        }

        // Display-only preview of ProgressionService.Create's fixed class rule; never generates a Pokémon.
        public static StatBlock ClassPreview(Rarity rarity)
        {
            int iv = rarity == Rarity.F ? 0 : rarity == Rarity.A ? 15 : 31;
            return new StatBlock(iv, iv, iv, iv);
        }
    }
}

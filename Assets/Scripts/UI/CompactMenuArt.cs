using UnityEngine;

namespace Game.UI
{
    /// <summary>Small nine-sliced frames extracted from Imagenes/botones.jpg.
    /// Original artwork remains the outer frame, paired with the shared ink inset.</summary>
    public static class CompactMenuArt
    {
        public static readonly Color Text = UIFactory.Cream;
        public static readonly Color Backing = new Color32(41, 71, 78, 246);

        public static UnityEngine.UI.Button Button(Transform parent, string name, string label,
            string frame, UnityEngine.Events.UnityAction callback)
        {
            var button = UIFactory.CreateButton(parent, name, label, Color.white,
                new Vector2(320, 96), callback, 28);
            var sprite = Resources.Load<Sprite>("UI/CompactMenus/" + frame);
            if (sprite != null)
            {
                button.image.sprite = sprite;
                button.image.type = UnityEngine.UI.Image.Type.Sliced;
            }
            else button.image.color = UIFactory.CardViolet;
            var inset = UIFactory.Surface("InkInset", button.transform, UIFactory.DeepViolet);
            UIFactory.Stretch(inset, 7);
            var accent = UIFactory.CreateImage("Accent", inset, null,
                frame == "Green" ? UIFactory.Lime : frame == "Amber" ? UIFactory.Sun :
                frame == "Red" ? UIFactory.Coral : UIFactory.Sky);
            UIFactory.Region(accent.rectTransform, 0, .22f, 0, .78f);
            accent.rectTransform.offsetMin = new Vector2(7, 0);
            accent.rectTransform.offsetMax = new Vector2(11, 0);
            var text = button.GetComponentInChildren<TMPro.TMP_Text>();
            text.transform.SetAsLastSibling();
            text.color = Text;
            text.fontStyle = TMPro.FontStyles.Bold;
            return button;
        }
    }
}

using UnityEngine;

namespace Game.UI
{
    /// <summary>The original main-menu artwork: colored nine-sliced frame, ink inset and side accent.
    /// Shared by every action button so other screens use the same design.</summary>
    public static class CompactMenuArt
    {
        public static readonly Color Text = UIFactory.Cream;
        public static readonly Color Backing = UIFactory.Panel;

        public static UnityEngine.UI.Button Button(Transform parent, string name, string label,
            string frame, UnityEngine.Events.UnityAction callback)
        {
            return UIFactory.CreateButton(parent, name, label, UIFactory.Border,
                new Vector2(320, 96), callback, 28, confirm: frame == "Green", frame: frame);
        }

        internal static void ApplyFrame(UnityEngine.UI.Button button, string frame)
        {
            var sprite = Resources.Load<Sprite>("UI/CompactMenus/" + frame);
            if (sprite != null)
            {
                button.image.sprite = sprite;
                button.image.type = UnityEngine.UI.Image.Type.Sliced;
                button.image.color = Color.white;
            }
            else button.image.color = UIFactory.CardViolet;
            var inset = UIFactory.CreateImage("InkInset", button.transform, RetroUiArt.Frame,
                UIFactory.DeepViolet, false, true).rectTransform;
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
        }
    }
}

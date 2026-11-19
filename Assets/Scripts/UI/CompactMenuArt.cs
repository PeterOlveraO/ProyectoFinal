using UnityEngine;

namespace Game.UI
{
    /// <summary>Small nine-sliced frames extracted from Imagenes/botones.jpg.
    /// Kept opt-in so collection, starter, gacha and institutional credits are untouched.</summary>
    public static class CompactMenuArt
    {
        public static readonly Color Text = new Color32(31, 33, 29, 255);
        public static readonly Color Backing = new Color32(24, 30, 30, 230);

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
            var text = button.GetComponentInChildren<TMPro.TMP_Text>();
            text.color = Text;
            text.fontStyle = TMPro.FontStyles.Bold;
            return button;
        }
    }
}

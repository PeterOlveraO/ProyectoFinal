using UnityEngine;

namespace Game.UI
{
    /// <summary>Author and institutional credits, using bundled official logos.</summary>
    public sealed class CreditsView
    {
        public MenuPage Page { get; }

        public CreditsView(Transform canvas, UnityEngine.Events.UnityAction back)
        {
            Page = new MenuPage(canvas, "CreditsMenu", "PKETER", "");
            Page.Wallet.gameObject.SetActive(false);

            var content = UIFactory.ScrollStack(UIFactory.Surface("Credits", Page.Body), 12, 14);
            UIFactory.Stretch((RectTransform)content.parent.parent);
            UIFactory.Label(content, "Author", "Pedro Olvera", 32, UIFactory.Cream, 50, TextAnchor.MiddleCenter);
            var row = UIFactory.CreateRect("Institutions", content);
            UIFactory.Layout(row, 220);
            UIFactory.Row(row, 16);
            Institution(row, "University", "Universidad Autónoma de Tamaulipas", "Credits/UAT");
            Institution(row, "Faculty", "Facultad de Ingeniería Tampico", "Credits/FIT");

            Page.Action("Back", "Volver", UIFactory.Sun, back);
        }

        void Institution(Transform parent, string name, string institution, string resource)
        {
            var card = UIFactory.Surface(name, parent);
            var layout = card.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minWidth = layout.preferredWidth = 0;
            layout.flexibleWidth = 1;

            // A light backing keeps the original logo colors readable on the dark menu.
            var backing = UIFactory.Surface("LogoBacking", card, Color.white);
            UIFactory.Region(backing, .035f, .20f, .34f, .80f);
            var logo = UIFactory.CreateImage("Logo", backing, Resources.Load<Sprite>(resource), Color.white);
            logo.preserveAspect = true;
            UIFactory.Stretch(logo.rectTransform, 16);
            if (logo.sprite == null)
            {
                logo.enabled = false;
                Debug.LogError($"Missing credits logo: Resources/{resource}");
            }

            var label = UIFactory.CreateText("InstitutionName", card, institution, 26,
                UIFactory.Cream, TextAnchor.MiddleLeft);
            UIFactory.Region(label.rectTransform, .39f, .10f, .96f, .90f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 24;
            label.fontSizeMax = 26;
        }
    }
}

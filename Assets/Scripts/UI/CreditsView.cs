using UnityEngine;

namespace Game.UI
{
    /// <summary>Author and institutional credits, using bundled official logos.</summary>
    public sealed class CreditsView
    {
        public MenuPage Page { get; }

        public CreditsView(Transform canvas, UnityEngine.Events.UnityAction back)
        {
            Page = new MenuPage(canvas, "CreditsMenu", "Créditos", "Un proyecto de desarrollo de videojuegos.");
            Page.Wallet.gameObject.SetActive(false);

            var authorLabel = UIFactory.CreateText("AuthorLabel", Page.Body, "AUTOR", 24, UIFactory.Muted);
            UIFactory.Region(authorLabel.rectTransform, 0, .92f, 1, 1);
            var author = UIFactory.CreateText("Author", Page.Body, "Pedro Olvera", 56, UIFactory.Sun);
            UIFactory.Region(author.rectTransform, 0, .77f, 1, .92f);

            Institution("University", "Universidad Autónoma de Tamaulipas",
                "Credits/UAT", .39f, .74f);
            Institution("Faculty", "Facultad de Ingeniería Tampico",
                "Credits/FIT", 0, .35f);

            Page.Action("Back", "Volver", UIFactory.Sun, back);
        }

        void Institution(string name, string institution, string resource, float bottom, float top)
        {
            var card = UIFactory.Surface(name, Page.Body);
            UIFactory.Region(card, .06f, bottom, .94f, top);

            // A light backing keeps the original logo colors readable on the dark menu.
            var backing = UIFactory.Surface("LogoBacking", card, Color.white);
            UIFactory.Region(backing, .02f, .08f, .30f, .92f);
            var logo = UIFactory.CreateImage("Logo", backing, Resources.Load<Sprite>(resource), Color.white);
            logo.preserveAspect = true;
            UIFactory.Stretch(logo.rectTransform, 16);
            if (logo.sprite == null)
            {
                logo.enabled = false;
                Debug.LogError($"Missing credits logo: Resources/{resource}");
            }

            var label = UIFactory.CreateText("InstitutionName", card, institution, 40,
                UIFactory.Cream, TextAnchor.MiddleLeft);
            UIFactory.Region(label.rectTransform, .34f, .10f, .96f, .90f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 24;
            label.fontSizeMax = 40;
        }
    }
}

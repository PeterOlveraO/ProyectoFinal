using UnityEngine;

namespace Game.UI
{
    /// <summary>Author and institutional credits, using bundled official logos.</summary>
    public sealed class CreditsView
    {
        public MenuPage Page { get; }

        public CreditsView(Transform canvas, UnityEngine.Events.UnityAction back)
        {
            Page = new MenuPage(canvas, "CreditsMenu", "CRÉDITOS", "PKETER", CompactMenuKind.Credits);
            Page.Wallet.gameObject.SetActive(false);

            var surface = UIFactory.Surface("Credits", Page.Body);
            UIFactory.Stretch(surface);
            var content = UIFactory.ScrollStack(surface, 12, 14);
            var author = UIFactory.Label(content, "Author", "Pedro Olvera Ochoa", 32,
                UIFactory.Sun, 52, TextAnchor.MiddleCenter);
            var row = UIFactory.CreateRect("Institutions", content);
            UIFactory.Layout(row, 220);
            var grid = row.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.childAlignment = TextAnchor.UpperCenter;
            Institution(row, "University", "Universidad Autónoma de Tamaulipas", "Credits/UAT");
            Institution(row, "Faculty", "Facultad de Ingeniería Tampico", "Credits/FIT");

            Page.Action("Back", "Volver", UIFactory.Sun, back);
            // MenuPage is inactive during construction; use its known ScrollStack owner.
            var scroll = surface.GetComponent<UnityEngine.UI.ScrollRect>();
            var track = UIFactory.CreateImage("ScrollIndicator", scroll.transform, null, UIFactory.Inner);
            UIFactory.Region(track.rectTransform, 1, 0, 1, 1);
            var handle = UIFactory.CreateImage("Handle", track.transform, null, UIFactory.Sky);
            UIFactory.Stretch(handle.rectTransform);
            var scrollbar = track.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            // A visual indicator; scrolling uses the content. Back remains the sole focus target.
            scrollbar.interactable = false;
            var colors = scrollbar.colors;
            colors.disabledColor = Color.white;
            scrollbar.colors = colors;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            Page.Root.GetComponent<CompactMenuLayout>().ConfigureCredits(content, author, grid, scroll);
        }

        void Institution(Transform parent, string name, string institution, string resource)
        {
            var card = UIFactory.Surface(name, parent, UIFactory.Inner);

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
        }
    }
}

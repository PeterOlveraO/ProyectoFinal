using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>Shared safe-area shell. Content uses layout, never whole-panel scaling.</summary>
    public sealed class MenuPage
    {
        public RectTransform Root { get; }
        public RectTransform Body { get; }
        public RectTransform Footer { get; }
        public TMPro.TMP_Text Status { get; }
        public TMPro.TMP_Text Wallet { get; }

        public MenuPage(Transform canvas, string name, string title, string subtitle)
        {
            Root = UIFactory.CreateRect(name, canvas);
            UIFactory.Stretch(Root);
            var background = UIFactory.CreateImage("Background", Root,
                Game.Rendering.ProceduralSprites.VerticalGradient(UIFactory.DeepViolet, UIFactory.Ink),
                Color.white, true);
            UIFactory.Stretch(background.rectTransform);
            var safe = UIFactory.SafeRoot(Root);
            var content = UIFactory.CreateRect("Content", safe);
            UIFactory.Stretch(content);
            content.offsetMin = new Vector2(56, 32);
            content.offsetMax = new Vector2(-56, -32);

            var eyebrow = UIFactory.CreateText("Brand", content, "PKETER  /  ENTRENADOR", 21, UIFactory.Muted,
                TextAnchor.UpperLeft);
            UIFactory.Region(eyebrow.rectTransform, 0, 1, .65f, 1);
            eyebrow.rectTransform.offsetMin = new Vector2(0, -28);
            var heading = UIFactory.CreateText("Title", content, title, 54, UIFactory.Cream, TextAnchor.MiddleLeft);
            UIFactory.Region(heading.rectTransform, 0, 1, .78f, 1);
            heading.rectTransform.offsetMin = new Vector2(0, -102);
            heading.rectTransform.offsetMax = new Vector2(0, -32);
            Status = UIFactory.CreateText("Subtitle", content, subtitle, 25, UIFactory.Muted, TextAnchor.MiddleLeft);
            UIFactory.Region(Status.rectTransform, 0, 1, 1, 1);
            Status.rectTransform.offsetMin = new Vector2(0, -148);
            Status.rectTransform.offsetMax = new Vector2(0, -104);
            Wallet = UIFactory.CreateText("Wallet", content, "", 28, UIFactory.Sun, TextAnchor.UpperRight);
            UIFactory.Region(Wallet.rectTransform, .68f, 1, 1, 1);
            Wallet.rectTransform.offsetMin = new Vector2(0, -45);

            Body = UIFactory.CreateRect("Body", content);
            UIFactory.Stretch(Body);
            Body.offsetMin = new Vector2(0, 100);
            Body.offsetMax = new Vector2(0, -172);
            Footer = UIFactory.CreateRect("Footer", content);
            UIFactory.Region(Footer, 0, 0, 1, 0);
            Footer.sizeDelta = new Vector2(0, 76);
            Footer.pivot = new Vector2(.5f, 0);
            UIFactory.Row(Footer);
            Root.gameObject.AddComponent<MenuPageFade>();
            Root.gameObject.SetActive(false);
        }

        public UnityEngine.UI.Button Action(string name, string label, Color color,
            UnityEngine.Events.UnityAction callback)
        {
            return UIFactory.CreateButton(Footer, name, label, color, new Vector2(320, 76), callback, 28);
        }

        public void Focus()
        {
            if (EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            foreach (var selectable in Root.GetComponentsInChildren<UnityEngine.UI.Selectable>())
                if (selectable.IsInteractable() && selectable.IsActive())
                {
                    EventSystem.current.SetSelectedGameObject(selectable.gameObject);
                    break;
                }
        }
    }

    public sealed class MenuPageFade : MonoBehaviour
    {
        CanvasGroup _group;
        void Awake() => _group = gameObject.AddComponent<CanvasGroup>();
        void OnEnable()
        {
            if (_group == null) return;
            _group.alpha = 0f;
        }
        void Update()
        {
            _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, Time.unscaledDeltaTime / .18f);
        }
    }
}

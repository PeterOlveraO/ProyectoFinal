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
        readonly bool _compact;
        readonly MenuPageLayout _layout;

        public MenuPage(Transform canvas, string name, string title, string subtitle,
            CompactMenuKind? compact = null)
        {
            Root = UIFactory.CreateRect(name, canvas);
            UIFactory.Stretch(Root);
            _compact = compact.HasValue;
            if (_compact)
            {
                // Transparent input shield prevents clicks reaching the run HUD while paused.
                var shield = UIFactory.CreateImage("InputShield", Root, null, Color.clear, true);
                UIFactory.Stretch(shield.rectTransform);
                var safeRoot = UIFactory.SafeRoot(Root);
                bool main = compact.Value == CompactMenuKind.Main;
                var panel = main ? UIFactory.CreateRect("Controls", safeRoot)
                    : UIFactory.Surface("CompactPanel", safeRoot, CompactMenuArt.Backing);
                var header = main ? UIFactory.Surface("Header", safeRoot, CompactMenuArt.Backing)
                    : UIFactory.CreateRect("Header", panel);
                var headingText = UIFactory.CreateText("Title", header, title, 38, UIFactory.Cream);
                UIFactory.Region(headingText.rectTransform, .02f, .48f, .98f, .98f);
                Status = UIFactory.CreateText("Subtitle", header, subtitle, 28, UIFactory.Muted);
                UIFactory.Region(Status.rectTransform, .02f, .02f, .98f, .48f);
                Wallet = UIFactory.CreateText("Wallet", header, "", 28, UIFactory.Sun);
                Wallet.gameObject.SetActive(false);
                Body = UIFactory.CreateRect("Body", panel);
                Footer = UIFactory.CreateRect("Footer", panel);
                UIFactory.Row(Footer);
                Footer.gameObject.SetActive(compact.Value == CompactMenuKind.Skills);
                Root.gameObject.AddComponent<CompactMenuLayout>().Configure(
                    safeRoot, panel, header, Body, Footer, compact.Value);
                Root.gameObject.AddComponent<MenuPageFade>();
                Root.gameObject.SetActive(false);
                return;
            }
            var background = UIFactory.CreateImage("Background", Root, null, Color.clear, true);
            UIFactory.Stretch(background.rectTransform);
            var safe = UIFactory.SafeRoot(Root);
            var content = UIFactory.Surface("Content", safe, UIFactory.Panel);
            _layout = Root.gameObject.AddComponent<MenuPageLayout>();
            var maximum = name == "StarterMenu" ? new Vector2(1040, 520) :
                name == "CreditsMenu" ? new Vector2(1080, 540) :
                name == "SettingsMenu" ? new Vector2(1120, 620) :
                name == "ReleaseMenu" ? new Vector2(640, 440) : new Vector2(1440, 800);
            _layout.Configure(safe, content, maximum);
            var heading = UIFactory.CreateText("Title", content, title, 30, UIFactory.Cream, TextAnchor.MiddleLeft);
            UIFactory.Region(heading.rectTransform, 0, 1, 1, 1);
            heading.rectTransform.offsetMin = new Vector2(20, -46);
            heading.rectTransform.offsetMax = new Vector2(-20, -8);
            Status = UIFactory.CreateText("Subtitle", content, "", 20, UIFactory.Muted, TextAnchor.MiddleLeft);
            UIFactory.Region(Status.rectTransform, 0, 1, 1, 1);
            Status.rectTransform.offsetMin = new Vector2(20, -72);
            Status.rectTransform.offsetMax = new Vector2(-20, -46);
            Wallet = UIFactory.CreateText("Wallet", content, "", 22, UIFactory.Cream, TextAnchor.UpperRight);
            UIFactory.Region(Wallet.rectTransform, .68f, 1, 1, 1);
            Wallet.rectTransform.offsetMin = new Vector2(0, -40);
            Wallet.rectTransform.offsetMax = new Vector2(-20, -12);
            Wallet.gameObject.SetActive(name == "CollectionMenu" || name == "GachaMenu");

            Body = UIFactory.CreateRect("Body", content);
            UIFactory.Stretch(Body);
            Body.offsetMin = new Vector2(20, 90);
            Body.offsetMax = new Vector2(-20, name == "ResultMenu" ? -16 : -80);
            Footer = UIFactory.CreateRect("Footer", content);
            UIFactory.Region(Footer, 0, 0, 1, 0);
            Footer.pivot = new Vector2(.5f, 0);
            Footer.offsetMin = new Vector2(20, 14);
            Footer.offsetMax = new Vector2(-20, 76);
            UIFactory.Row(Footer);
            Root.gameObject.AddComponent<MenuPageFade>();
            Root.gameObject.SetActive(false);
        }

        public void SetSize(Vector2 maximum) { if (_layout != null) _layout.SetSize(maximum); }

        public UnityEngine.UI.Button Action(string name, string label, Color color,
            UnityEngine.Events.UnityAction callback)
        {
            if (_compact)
                return CompactMenuArt.Button(Footer, name, label, "Green", callback);
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

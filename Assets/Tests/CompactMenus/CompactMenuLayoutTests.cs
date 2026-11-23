using System.Collections;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.CompactMenus
{
    public sealed class CompactMenuLayoutTests
    {
        RectTransform _canvas;

        [TearDown]
        public void TearDown()
        {
            if (_canvas != null) Object.DestroyImmediate(_canvas.gameObject);
        }

        // Real uGUI hierarchy + GridLayoutGroup, with simulated device safe rectangles.
        // No Screen.SetResolution, scene load or shared Editor/game-view changes.
        [TestCase(1920, 1080, 0, 0)]
        [TestCase(1024, 768, 0, 0)]
        [TestCase(800, 360, 44, 16)]
        [TestCase(640, 360, 0, 0)]
        [TestCase(568, 320, 32, 16)]
        [TestCase(2400, 1080, 96, 48)]
        public void MenusFitSafeAreaAndLeaveMostOfTheMapVisible(int width, int height, int side, int bottom)
        {
            _canvas = UIFactory.CreateCanvas("LayoutTest", 0);
            _canvas.GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
            var canvas = _canvas.GetComponent<Canvas>();
            // The project's matchWidthOrHeight=.5 scaler uses this geometric mean.
            canvas.scaleFactor = Mathf.Sqrt(width / 1920f * (height / 1080f));
            foreach (var kind in new[] { CompactMenuKind.Main, CompactMenuKind.Pause, CompactMenuKind.Skills })
            {
                var page = new MenuPage(_canvas, kind.ToString(), "MEJORAS EV",
                    "250 PT · Solo esta partida", kind);
                var labels = kind == CompactMenuKind.Main
                    ? new[] { "JUGAR", "Colección", "Gacha", "Ajustes", "Créditos", "PRUEBAS", "Salir" }
                    : kind == CompactMenuKind.Pause
                        ? new[] { "CONTINUAR", "Mejoras EV", "Ajustes", "PRUEBAS", "Salir de la partida" }
                        : new[] { "SALUD\n10/10 · MÁX.", "ATAQUE\n10/10 · MÁX.", "DEFENSA\n10/10 · MÁX.", "VELOCIDAD\n10/10 · MÁX." };
                for (int i = 0; i < labels.Length; i++)
                    CompactMenuArt.Button(page.Body, "Button" + i,
                        labels[i],
                        "Blue", () => { });
                if (kind == CompactMenuKind.Skills) page.Action("Back", "Volver", UIFactory.Sun, () => { });
                page.Root.gameObject.SetActive(true);
                var safe = (RectTransform)page.Root.Find("SafeArea");
                safe.GetComponent<SafeAreaLayout>().enabled = false;
                UIFactory.Place(safe, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                    new Vector2((width - side * 2) / canvas.scaleFactor, (height - bottom) / canvas.scaleFactor));
                page.Root.GetComponent<CompactMenuLayout>().RefreshLayout();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(page.Body);
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(page.Footer);
                Canvas.ForceUpdateCanvases();

                foreach (var button in page.Root.GetComponentsInChildren<UnityEngine.UI.Button>())
                {
                    var rect = (RectTransform)button.transform;
                    AssertInside(safe, rect);
                    Assert.GreaterOrEqual(rect.rect.height * canvas.scaleFactor, 47.99f);
                    Assert.GreaterOrEqual(rect.rect.width * canvas.scaleFactor, 47.99f);
                    var text = button.GetComponentInChildren<TMPro.TMP_Text>();
                    Assert.GreaterOrEqual(text.fontSize * canvas.scaleFactor, 13.99f);
                    text.ForceMeshUpdate();
                    Assert.IsFalse(text.isTextOverflowing, "Do not shrink or truncate touch labels.");
                    Assert.NotNull(button.image.sprite, "Extracted art must be loadable in a build.");
                    Assert.AreNotEqual(Vector4.zero, button.image.sprite.border);
                    Assert.AreEqual(UnityEngine.UI.Image.Type.Sliced, button.image.type);
                }
                // Exclude the invisible input shield; count actual visible menu footprints.
                var panel = (RectTransform)page.Body.parent;
                float area = panel.rect.width * panel.rect.height;
                AssertInside(safe, panel);
                if (kind == CompactMenuKind.Main)
                {
                    var header = (RectTransform)safe.Find("Header");
                    AssertInside(safe, header);
                    area += header.rect.width * header.rect.height;
                }
                Assert.Less(area / (safe.rect.width * safe.rect.height), .5f,
                    "At least half of the safe map area must remain outside menu panels.");
                Assert.IsNull(page.Root.Find("Background"), "No opaque fullscreen background.");
                var shield = page.Root.Find("InputShield").GetComponent<UnityEngine.UI.Image>();
                Assert.IsTrue(shield.raycastTarget, "Visible run must not receive clicks through the menu.");
                Assert.AreEqual(0, shield.color.a);
                Object.DestroyImmediate(page.Root.gameObject);
            }
        }

        [TestCase(1920, 1080, 0, 0, false)]
        [TestCase(1280, 720, 0, 0, false)]
        [TestCase(1440, 1080, 0, 0, false)]
        [TestCase(568, 320, 32, 16, true)]
        public void TestRewardsFitNormalScreensAndScrollOnSmallScreens(
            int width, int height, int side, int bottom, bool needsScroll)
        {
            _canvas = UIFactory.CreateCanvas("TestRewardsLayout", 0);
            _canvas.GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.scaleFactor = Mathf.Sqrt(width / 1920f * height / 1080f);
            var view = TestMenuView.Create(_canvas, _ => { }, (_, __) => { }, () => { });
            var catalog = JsonUtility.FromJson<Game.Pokemon.Progression.SpeciesCatalog>(
                Resources.Load<TextAsset>("PokemonProgression/catalog").text);
            view.Bind(catalog.species, 10000, 10, 10, true);
            view.SetFeedback("No se pudo guardar. Reinicia y revisa el almacenamiento.", true);
            view.Page.Root.gameObject.SetActive(true);
            var safe = (RectTransform)view.Page.Root.Find("SafeArea");
            safe.GetComponent<SafeAreaLayout>().enabled = false;
            UIFactory.Place(safe, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                new Vector2((width - side * 2) / canvas.scaleFactor, (height - bottom) / canvas.scaleFactor));
            view.RefreshLayout();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(view.Page.Root);
            Canvas.ForceUpdateCanvases();
            AssertInside(safe, (RectTransform)view.Page.Body.parent);
            Assert.LessOrEqual(((RectTransform)view.Page.Body.parent).rect.width, 1120.1f);
            Assert.LessOrEqual(((RectTransform)view.Page.Body.parent).rect.height, 760.1f);
            AssertInside(safe, view.Page.Footer);
            Assert.Greater(view.Page.Body.rect.height * canvas.scaleFactor, 48);
            var scroll = view.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            Assert.NotNull(scroll.viewport.GetComponent<UnityEngine.UI.RectMask2D>());
            Assert.AreSame(scroll.viewport, scroll.content.parent);
            Assert.NotNull(scroll.verticalScrollbar);
            Assert.AreEqual(needsScroll, scroll.verticalScrollbar.gameObject.activeSelf,
                "Show a scroll indicator only when the form does not fit.");
            if (needsScroll)
                Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
            else
            {
                Assert.LessOrEqual(scroll.content.rect.height, scroll.viewport.rect.height + .1f);
                foreach (var button in scroll.content.GetComponentsInChildren<UnityEngine.UI.Button>())
                    AssertInside(scroll.viewport, (RectTransform)button.transform);
            }
            Assert.IsFalse(scroll.horizontal);
            var selector = (RectTransform)scroll.content.Find("SpeciesSelector");
            var previous = (RectTransform)selector.Find("PreviousSpecies");
            var next = (RectTransform)selector.Find("NextSpecies");
            var species = (RectTransform)selector.Find("SpeciesName");
            float arrowWidth = Mathf.Max(64, 48 / canvas.scaleFactor);
            Assert.AreEqual(arrowWidth, previous.rect.width, .1f);
            Assert.AreEqual(arrowWidth, next.rect.width, .1f);
            Assert.Greater(species.rect.width, previous.rect.width * 2,
                "The species name must receive the spare width, not the arrows.");
            foreach (var selectable in view.GetComponentsInChildren<UnityEngine.UI.Selectable>())
                Assert.GreaterOrEqual(((RectTransform)selectable.transform).rect.height * canvas.scaleFactor, 47.99f);
            foreach (var text in view.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                Assert.GreaterOrEqual(text.fontSize * canvas.scaleFactor, 13.99f);
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextOverflowing, text.name);
            }
            foreach (var button in view.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                Assert.NotNull(button.image.sprite);
                Assert.AreEqual(UnityEngine.UI.Image.Type.Sliced, button.image.type);
            }
            Assert.IsTrue(view.Page.Root.Find("Background").GetComponent<UnityEngine.UI.Image>().raycastTarget);
            if (needsScroll)
            {
                scroll.verticalNormalizedPosition = 0;
                Canvas.ForceUpdateCanvases();
            }
            AssertInside(scroll.viewport, (RectTransform)scroll.content.Find("GrantPokemon"));
        }

        [TestCase(1920, 1080, 0, 0, false)]
        [TestCase(1280, 720, 0, 0, false)]
        [TestCase(1440, 1080, 0, 0, false)]
        [TestCase(1024, 768, 0, 0, false)]
        [TestCase(568, 320, 32, 16, true)]
        public void CompactCreditsKeepAuthorLogosAndBackReadable(
            int width, int height, int side, int bottom, bool needsScroll)
        {
            _canvas = UIFactory.CreateCanvas("CreditsLayout", 0);
            _canvas.GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.scaleFactor = Mathf.Sqrt(width / 1920f * height / 1080f);
            int backCalls = 0;
            var page = new CreditsView(_canvas, () => backCalls++).Page;
            page.Root.gameObject.SetActive(true);
            var safe = (RectTransform)page.Root.Find("SafeArea");
            safe.GetComponent<SafeAreaLayout>().enabled = false;
            UIFactory.Place(safe, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                new Vector2((width - side * 2) / canvas.scaleFactor, (height - bottom) / canvas.scaleFactor));
            var layout = page.Root.GetComponent<CompactMenuLayout>();
            Assert.NotNull(layout);
            Assert.IsNull(page.Root.GetComponent<MenuPageLayout>());
            layout.RefreshLayout();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(page.Footer);
            Canvas.ForceUpdateCanvases();

            var panel = (RectTransform)page.Body.parent;
            AssertInside(safe, panel);
            Assert.Less(panel.rect.width * panel.rect.height / (safe.rect.width * safe.rect.height), .5f);
            Assert.IsNull(page.Root.Find("Background"));
            Assert.IsTrue(page.Root.Find("InputShield").GetComponent<UnityEngine.UI.Image>().raycastTarget);
            Assert.IsFalse(page.Wallet.gameObject.activeSelf);
            var scroll = page.Root.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            Assert.IsFalse(scroll.horizontal);
            Assert.NotNull(scroll.viewport.GetComponent<UnityEngine.UI.RectMask2D>());
            Assert.AreSame(scroll.viewport, scroll.content.parent);
            var author = scroll.content.Find("Author").GetComponent<TMPro.TMP_Text>();
            Assert.AreEqual("Pedro Olvera Ochoa", author.text);
            AssertInside(scroll.viewport, author.rectTransform);
            Assert.AreEqual(needsScroll, scroll.verticalScrollbar.gameObject.activeSelf);
            Assert.IsFalse(scroll.verticalScrollbar.interactable, "The indicator must not steal Back focus.");
            if (needsScroll) Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
            else Assert.LessOrEqual(scroll.content.rect.height, scroll.viewport.rect.height + .1f);

            var institutions = scroll.content.Find("Institutions");
            string[] cards = { "University", "Faculty" };
            string[] logos = { "Credits/UAT", "Credits/FIT" };
            string[] names = { "Universidad Autónoma de Tamaulipas", "Facultad de Ingeniería Tampico" };
            for (int i = 0; i < cards.Length; i++)
            {
                if (needsScroll && i == 1)
                {
                    scroll.verticalNormalizedPosition = 0;
                    Canvas.ForceUpdateCanvases();
                }
                var card = institutions.Find(cards[i]);
                var logo = card.Find("LogoBacking/Logo").GetComponent<UnityEngine.UI.Image>();
                Assert.NotNull(logo.sprite);
                Assert.AreSame(Resources.Load<Sprite>(logos[i]), logo.sprite);
                Assert.IsTrue(logo.preserveAspect);
                Assert.IsTrue(logo.enabled && logo.gameObject.activeInHierarchy);
                Assert.Greater(logo.rectTransform.rect.width * canvas.scaleFactor, 30);
                Assert.Greater(logo.rectTransform.rect.height * canvas.scaleFactor, 30);
                AssertInside(scroll.viewport, logo.rectTransform);
                var name = card.Find("InstitutionName").GetComponent<TMPro.TMP_Text>();
                Assert.AreEqual(names[i], name.text);
                AssertInside(scroll.viewport, name.rectTransform);
            }
            foreach (var text in page.Root.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                Assert.GreaterOrEqual(text.fontSize * canvas.scaleFactor, 13.99f);
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextOverflowing, text.name);
            }
            var back = page.Footer.Find("Back").GetComponent<UnityEngine.UI.Button>();
            AssertInside(safe, (RectTransform)back.transform);
            Assert.GreaterOrEqual(((RectTransform)back.transform).rect.height * canvas.scaleFactor, 47.99f);
            Assert.AreSame(Resources.Load<Sprite>("UI/CompactMenus/Green"), back.image.sprite);
            Assert.AreEqual(UnityEngine.UI.Image.Type.Sliced, back.image.type);
            var events = UnityEngine.EventSystems.EventSystem.current == null
                ? new GameObject("CreditsEvents", typeof(UnityEngine.EventSystems.EventSystem)) : null;
            try
            {
                page.Focus();
                Assert.AreSame(back.gameObject, UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject);
                back.onClick.Invoke();
                Assert.AreEqual(1, backCalls);
            }
            finally { if (events != null) Object.DestroyImmediate(events); }
        }

        [UnityTest]
        public IEnumerator CreditsOpenAt720pWithoutManualLayout() => OpenCreditsWithoutManualLayout(1280, 720);

        [UnityTest]
        public IEnumerator CreditsOpenAt1080pWithoutManualLayout() => OpenCreditsWithoutManualLayout(1920, 1080);

        [UnityTest]
        public IEnumerator CreditsOpenAtFourByThreeWithoutManualLayout() => OpenCreditsWithoutManualLayout(1440, 1080);

        IEnumerator OpenCreditsWithoutManualLayout(int width, int height)
        {
            _canvas = UIFactory.CreateCanvas("CreditsOpeningLayout", 0);
            _canvas.GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.scaleFactor = Mathf.Sqrt(width / 1920f * height / 1080f);
            var page = new CreditsView(_canvas, () => { }).Page;
            Assert.IsFalse(page.Root.gameObject.activeSelf, "Exercise construction under the inactive page.");
            var safe = (RectTransform)page.Root.Find("SafeArea");
            safe.GetComponent<SafeAreaLayout>().enabled = false;
            UIFactory.Place(safe, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(width / canvas.scaleFactor, height / canvas.scaleFactor));

            // Both the first activation and reopening use only the normal player/Canvas loop.
            for (int opening = 0; opening < 2; opening++)
            {
                page.Root.gameObject.SetActive(true);
                for (int frame = 0; frame < 4; frame++) yield return null;
                var scroll = page.Root.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
                var institutions = (RectTransform)scroll.content.Find("Institutions");
                AssertInside(safe, (RectTransform)page.Body.parent);
                AssertInside(scroll.viewport, (RectTransform)scroll.content.Find("Author"));
                Assert.AreEqual("Pedro Olvera Ochoa",
                    scroll.content.Find("Author").GetComponent<TMPro.TMP_Text>().text);
                AssertInside(safe, (RectTransform)page.Footer.Find("Back"));
                string[] cards = { "University", "Faculty" };
                string[] logos = { "Credits/UAT", "Credits/FIT" };
                for (int i = 0; i < cards.Length; i++)
                {
                    var card = (RectTransform)institutions.Find(cards[i]);
                    Assert.Greater(card.rect.width * canvas.scaleFactor, 200,
                        "Cards must receive the resolved content width, not the constructor's provisional width.");
                    AssertInside(scroll.viewport, card);
                    var logo = card.Find("LogoBacking/Logo").GetComponent<UnityEngine.UI.Image>();
                    Assert.IsTrue(logo.isActiveAndEnabled);
                    Assert.NotNull(logo.sprite);
                    Assert.AreSame(Resources.Load<Sprite>(logos[i]), logo.sprite);
                    Assert.Greater(logo.rectTransform.rect.width * canvas.scaleFactor, 40);
                    Assert.Greater(logo.rectTransform.rect.height * canvas.scaleFactor, 40);
                    AssertInside(scroll.viewport, logo.rectTransform);
                }
                foreach (var text in page.Root.GetComponentsInChildren<TMPro.TMP_Text>())
                {
                    Assert.GreaterOrEqual(text.fontSize * canvas.scaleFactor, 13.99f);
                    Assert.Greater(text.textInfo.characterCount, 0, text.name);
                    Assert.IsFalse(text.isTextOverflowing, text.name);
                    AssertInside(safe, text.rectTransform);
                }
                page.Root.gameObject.SetActive(false);
                yield return null;
            }
        }

        [TestCase("100", true, 100)]
        [TestCase(" 123 ", true, 123)]
        [TestCase("2147483647", true, int.MaxValue)]
        [TestCase("2147483648", false, 0)]
        [TestCase("", false, 0)]
        [TestCase(null, false, 0)]
        [TestCase("0", false, 0)]
        [TestCase("-1", false, 0)]
        [TestCase("1.5", false, 0)]
        [TestCase("1e3", false, 0)]
        [TestCase("+100", false, 0)]
        public void CustomTestCoinsRequireAPositiveInteger(string text, bool valid, int expected)
        {
            Assert.AreEqual(valid, TestMenuView.TryParseAmount(text, out int amount));
            if (valid) Assert.AreEqual(expected, amount);
        }

        [Test]
        public void RunEntryPointsStayTouchableOnASmallCanvas()
        {
            _canvas = UIFactory.CreateCanvas("RunControlsTest", 0);
            _canvas.GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.scaleFactor = .3f;
            var safe = UIFactory.SafeRoot(_canvas);
            var pause = CompactMenuArt.Button(safe, "Pause", "PAUSA", "Amber", () => { });
            var skills = CompactMenuArt.Button(safe, "EV", "EV", "Blue", () => { });
            safe.gameObject.AddComponent<RunMenuControlsLayout>().Configure(pause, skills);
            foreach (var button in new[] { pause, skills })
            {
                var rect = (RectTransform)button.transform;
                Assert.GreaterOrEqual(rect.rect.width * canvas.scaleFactor, 48);
                Assert.GreaterOrEqual(rect.rect.height * canvas.scaleFactor, 48);
            }
            var pauseRect = (RectTransform)pause.transform;
            var skillsRect = (RectTransform)skills.transform;
            Assert.Less(skillsRect.anchoredPosition.x, pauseRect.anchoredPosition.x - pauseRect.rect.width);
        }

        [Test]
        public void SharedPagesKeepMapVisibleAndActionsWorking()
        {
            _canvas = UIFactory.CreateCanvas("StandardPageTest", 0);
            var page = new MenuPage(_canvas, "CollectionMenu", "Colección", "");
            int calls = 0;
            var button = page.Action("Back", "Volver", UIFactory.Sun, () => calls++);
            button.onClick.Invoke();
            Assert.AreEqual(1, calls);
            Assert.NotNull(page.Root.Find("Background"));
            Assert.AreEqual(0, page.Root.Find("Background").GetComponent<UnityEngine.UI.Image>().color.a);
            Assert.IsNull(page.Root.GetComponent<CompactMenuLayout>());
            Assert.NotNull(page.Root.GetComponent<MenuPageLayout>());
            Assert.AreEqual(Color.white, button.image.color);
            Assert.AreSame(Resources.Load<Sprite>("UI/CompactMenus/Amber"), button.image.sprite);
            Assert.NotNull(button.transform.Find("InkInset/Accent"));
            Assert.AreEqual(UIFactory.DeepViolet,
                button.transform.Find("InkInset").GetComponent<UnityEngine.UI.Image>().color);
            Assert.IsTrue(page.Wallet.gameObject.activeSelf);
        }

        static void AssertInside(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            foreach (var world in corners)
            {
                var local = parent.InverseTransformPoint(world);
                Assert.That(local.x, Is.InRange(parent.rect.xMin - .1f, parent.rect.xMax + .1f), child.name);
                Assert.That(local.y, Is.InRange(parent.rect.yMin - .1f, parent.rect.yMax + .1f), child.name);
            }
        }
    }
}

using Game.UI;
using NUnit.Framework;
using UnityEngine;

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
                int count = kind == CompactMenuKind.Main ? 6 : 4;
                for (int i = 0; i < count; i++)
                    CompactMenuArt.Button(page.Body, "Button" + i,
                        kind == CompactMenuKind.Skills ? "VELOCIDAD\n10/10 · MÁX." : "Colección",
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

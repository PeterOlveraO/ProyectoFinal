using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Shared handheld-inspired UI primitives. Legacy violet names remain for compatibility.
    /// </summary>
    public static class UIFactory
    {
        // ------------------------------------------------------------- Paleta
        public static readonly Color Ink = new Color32(34, 57, 64, 255);
        public static readonly Color DeepViolet = new Color32(41, 71, 78, 255);
        public static readonly Color CardViolet = new Color32(54, 86, 91, 255);
        public static readonly Color Sun = new Color32(233, 196, 106, 255);
        public static readonly Color Lime = new Color32(134, 185, 107, 255);
        public static readonly Color Sky = new Color32(117, 185, 195, 255);
        public static readonly Color Coral = new Color32(216, 120, 101, 255);
        public static readonly Color Cream = new Color32(245, 239, 214, 255);
        public static readonly Color Muted = new Color32(192, 209, 197, 255);
        public static readonly Color Border = new Color32(117, 155, 153, 255);

        static TMPro.TMP_FontAsset _font;

        public static TMPro.TMP_FontAsset UiFont
        {
            get
            {
                if (_font != null) return _font;

                _font = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (_font == null) _font = TMPro.TMP_Settings.defaultFontAsset;
                return _font;
            }
        }

        // ------------------------------------------------------------ Básicos
        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        public static RectTransform CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot,
                                 Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color,
                                        bool raycast = false, bool sliced = false)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            if (sliced && sprite != null && sprite.border != Vector4.zero)
                image.type = Image.Type.Sliced;
            return image;
        }

        public static TMPro.TMP_Text CreateText(string name, Transform parent, string content, int fontSize,
                                      Color color, TextAnchor alignment = TextAnchor.MiddleCenter,
                                      bool shadow = false)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            text.font = UiFont;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontSize >= 32 ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            text.color = color;
            text.alignment = Alignment(alignment);
            text.textWrappingMode = TMPro.TextWrappingModes.Normal;
            text.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            text.enableAutoSizing = false;
            text.raycastTarget = false;
            text.richText = true;

            return text;
        }

        public static Outline AddOutline(Graphic graphic, Color color, float thickness = 4f)
        {
            // TMP owns its geometry; uGUI mesh effects must not be added to TMP text.
            if (graphic is TMPro.TMP_Text) return null;
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(thickness, -thickness);
            return outline;
        }

        public static Shadow AddShadow(Graphic graphic, Color color, Vector2 distance)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            return shadow;
        }

        // ------------------------------------------------------------ Botones
        public static Button CreateButton(Transform parent, string name, string label, Color color,
                                          Vector2 size, UnityAction onClick, int fontSize = 44)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = RetroUiArt.Frame;
            image.type = Image.Type.Sliced;
            image.color = color;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            rect.gameObject.AddComponent<UIFocusFeedback>();

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, .92f, 1f);
            colors.pressedColor = new Color(.76f, .84f, .82f, 1f);
            colors.selectedColor = new Color(1f, 1f, .92f, 1f);
            colors.disabledColor = new Color(.62f, .68f, .66f, .85f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            if (onClick != null) button.onClick.AddListener(onClick);

            var text = CreateText("Label", rect, label, fontSize,
                color == Sun || color == Lime || color == Sky || color == Coral ? Ink : Cream);
            text.fontStyle = TMPro.FontStyles.Bold;
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(20f, 4f);
            text.rectTransform.offsetMax = new Vector2(-20f, -4f);

            return button;
        }

        /// <summary>Tarjeta central con borde brillante y sombra.</summary>
        public static RectTransform CreateCard(Transform parent, Vector2 size, Color fill, Color border)
        {
            var borderRect = CreateRect("CardBorder", parent);
            borderRect.sizeDelta = size + new Vector2(10f, 10f);

            var borderImage = borderRect.gameObject.AddComponent<Image>();
            borderImage.sprite = RetroUiArt.Frame;
            borderImage.type = Image.Type.Sliced;
            borderImage.color = border;
            borderImage.raycastTarget = false;
            AddShadow(borderImage, new Color(0f, 0f, 0f, 0.18f), new Vector2(0f, -3f));

            var cardRect = CreateRect("Card", borderRect);
            Stretch(cardRect, 5f);

            var cardImage = cardRect.gameObject.AddComponent<Image>();
            cardImage.sprite = RetroUiArt.Frame;
            cardImage.type = Image.Type.Sliced;
            cardImage.color = fill;
            cardImage.raycastTarget = false;

            return cardRect;
        }

        // ------------------------------------------------------------ Slider
        public static Slider CreateSlider(Transform parent, Vector2 size, float value, UnityAction<float> onChange)
        {
            var rect = CreateRect("Slider", parent);
            rect.sizeDelta = size;
            var hitArea = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;

            var track = CreateImage("Track", rect, RetroUiArt.Frame, Ink, false, true);
            Stretch(track.rectTransform, 0f);
            track.rectTransform.anchorMin = new Vector2(0f, 0.30f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.70f);

            var fillArea = CreateRect("FillArea", rect);
            fillArea.anchorMin = new Vector2(0f, 0.30f);
            fillArea.anchorMax = new Vector2(1f, 0.70f);
            fillArea.offsetMin = new Vector2(12f, 0f);
            fillArea.offsetMax = new Vector2(-12f, 0f);

            var fill = CreateImage("Fill", fillArea, RetroUiArt.Frame, Sun, false, true);
            Stretch(fill.rectTransform);

            var handleArea = CreateRect("HandleArea", rect);
            Stretch(handleArea);

            var handle = CreateImage("Handle", handleArea, RetroUiArt.Frame, Cream, true, true);
            handle.rectTransform.sizeDelta = new Vector2(size.y * 0.78f, size.y * 0.78f);
            handle.preserveAspect = true;

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };

            var colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.05f;
            slider.colors = colors;

            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            if (onChange != null) slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        static TMPro.TextAlignmentOptions Alignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TMPro.TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TMPro.TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TMPro.TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TMPro.TextAlignmentOptions.MidlineLeft;
                case TextAnchor.MiddleRight: return TMPro.TextAlignmentOptions.MidlineRight;
                case TextAnchor.LowerLeft: return TMPro.TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TMPro.TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TMPro.TextAlignmentOptions.BottomRight;
                default: return TMPro.TextAlignmentOptions.Midline;
            }
        }

        public static RectTransform Surface(string name, Transform parent, Color? color = null)
        {
            return CreateImage(name, parent, RetroUiArt.Frame,
                color ?? CardViolet, false, true).rectTransform;
        }

        /// <summary>Scrollable vertical card content; fixed typography never clips below a short viewport.</summary>
        public static RectTransform ScrollStack(RectTransform root, int padding = 20, float gap = 12)
        {
            var image = root.GetComponent<UnityEngine.UI.Image>();
            if (image != null) image.raycastTarget = true;
            var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36;
            var viewport = CreateRect("Viewport", root);
            Stretch(viewport, 6);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            Stack(content, padding, gap);
            var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        public static RectTransform SafeRoot(Transform parent, string name = "SafeArea")
        {
            var root = CreateRect(name, parent);
            Stretch(root);
            root.gameObject.AddComponent<SafeAreaLayout>();
            return root;
        }

        public static void Region(RectTransform rect, float left, float bottom, float right, float top,
            float padding = 0f)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        public static UnityEngine.UI.LayoutElement Layout(Transform item, float height, float flex = 0f)
        {
            var element = item.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = flex;
            return element;
        }

        public static UnityEngine.UI.VerticalLayoutGroup Stack(RectTransform root, int padding = 0, float gap = 16f)
        {
            var group = root.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            group.padding = new RectOffset(padding, padding, padding, padding);
            group.spacing = gap;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return group;
        }

        public static UnityEngine.UI.HorizontalLayoutGroup Row(RectTransform root, float gap = 16f)
        {
            var group = root.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            group.spacing = gap;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = true;
            return group;
        }

        public static TMPro.TMP_Text Label(Transform parent, string name, string value, int size,
            Color color, float height, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = CreateText(name, parent, value, size, color, alignment);
            Layout(text.transform, height);
            return text;
        }

        public static void LayoutHeight(TMPro.TMP_Text text, float minimum)
        {
            var fitter = text.GetComponent<ContentTextLayout>();
            if (fitter == null) fitter = text.gameObject.AddComponent<ContentTextLayout>();
            fitter.Configure(minimum);
        }

        public static Color RarityColor(Game.Pokemon.Progression.Rarity rarity) =>
            rarity == Game.Pokemon.Progression.Rarity.S ? Sun :
            rarity == Game.Pokemon.Progression.Rarity.A ? Sky : Muted;

        // ------------------------------------------------------------- Color
        public static Color Lighten(Color color, float amount)
        {
            return new Color(Mathf.Clamp01(color.r + amount),
                             Mathf.Clamp01(color.g + amount),
                             Mathf.Clamp01(color.b + amount), color.a);
        }

        public static Color Darken(Color color, float amount)
        {
            return new Color(Mathf.Clamp01(color.r - amount),
                             Mathf.Clamp01(color.g - amount),
                             Mathf.Clamp01(color.b - amount), color.a);
        }
    }
}

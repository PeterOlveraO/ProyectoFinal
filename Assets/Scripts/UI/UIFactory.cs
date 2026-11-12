using Game.Rendering;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Construcción de interfaz por código con un estilo vivo y consistente.
    /// Todo (fondos, botones, pokéballs, degradados) se genera con
    /// <see cref="ProceduralSprites"/>: el menú no necesita ninguna imagen importada.
    /// </summary>
    public static class UIFactory
    {
        // ------------------------------------------------------------- Paleta
        public static readonly Color Ink = new Color32(24, 13, 43, 255);
        public static readonly Color DeepViolet = new Color32(52, 24, 100, 255);
        public static readonly Color CardViolet = new Color32(72, 36, 132, 255);
        public static readonly Color Sun = new Color32(255, 222, 66, 255);
        public static readonly Color Lime = new Color32(48, 209, 116, 255);
        public static readonly Color Sky = new Color32(56, 137, 255, 255);
        public static readonly Color Coral = new Color32(255, 90, 95, 255);
        public static readonly Color Cream = new Color32(255, 249, 240, 255);

        static Font _font;

        public static Font UiFont
        {
            get
            {
                if (_font != null) return _font;

                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 24);
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

        public static Text CreateText(string name, Transform parent, string content, int fontSize,
                                      Color color, TextAnchor alignment = TextAnchor.MiddleCenter,
                                      bool shadow = true)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = UiFont;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;

            if (shadow)
            {
                var effect = rect.gameObject.AddComponent<Shadow>();
                effect.effectColor = new Color(0f, 0f, 0f, 0.55f);
                effect.effectDistance = new Vector2(3f, -4f);
            }

            return text;
        }

        public static Outline AddOutline(Graphic graphic, Color color, float thickness = 4f)
        {
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
            image.sprite = ProceduralSprites.RoundedRect;
            image.type = Image.Type.Sliced;
            image.color = color;

            AddShadow(image, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -9f));
            AddOutline(image, Lighten(color, 0.32f), 3f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            if (onClick != null) button.onClick.AddListener(onClick);

            var text = CreateText("Label", rect, label, fontSize, Cream);
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
            borderImage.sprite = ProceduralSprites.RoundedRect;
            borderImage.type = Image.Type.Sliced;
            borderImage.color = border;
            borderImage.raycastTarget = false;
            AddShadow(borderImage, new Color(0f, 0f, 0f, 0.45f), new Vector2(0f, -14f));

            var cardRect = CreateRect("Card", borderRect);
            Stretch(cardRect, 5f);

            var cardImage = cardRect.gameObject.AddComponent<Image>();
            cardImage.sprite = ProceduralSprites.RoundedRect;
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

            var track = CreateImage("Track", rect, ProceduralSprites.RoundedRect,
                                    new Color(0.10f, 0.05f, 0.20f, 0.85f), false, true);
            Stretch(track.rectTransform, 0f);
            track.rectTransform.anchorMin = new Vector2(0f, 0.30f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.70f);

            var fillArea = CreateRect("FillArea", rect);
            fillArea.anchorMin = new Vector2(0f, 0.30f);
            fillArea.anchorMax = new Vector2(1f, 0.70f);
            fillArea.offsetMin = new Vector2(12f, 0f);
            fillArea.offsetMax = new Vector2(-12f, 0f);

            var fill = CreateImage("Fill", fillArea, ProceduralSprites.RoundedRect, Sun, false, true);
            Stretch(fill.rectTransform);

            var handleArea = CreateRect("HandleArea", rect);
            Stretch(handleArea);

            var handle = CreateImage("Handle", handleArea, ProceduralSprites.Circle, Cream, true);
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
            slider.navigation = new Navigation { mode = Navigation.Mode.None };

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

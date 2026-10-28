using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Player
{
    /// <summary>
    /// Joystick táctil flotante para móvil.
    ///
    /// Este componente vive en una zona de pantalla transparente (p. ej. la mitad izquierda):
    /// al tocar dentro, el joystick aparece bajo el dedo; al arrastrar, <see cref="Value"/>
    /// da la dirección en [-1, 1]; al soltar, vuelve a cero y se oculta.
    ///
    /// Los gráficos (fondo y mando circulares) se generan por código: no necesita sprites.
    /// Solo se muestra en plataformas móviles (o en el editor si showInEditor está activo).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("Radio del recorrido del mando, en píxeles de referencia del Canvas.")]
        [SerializeField] float radius = 110f;

        [Tooltip("Zona muerta (0-1) para ignorar movimientos mínimos del dedo.")]
        [Range(0f, 0.5f)] [SerializeField] float deadZone = 0.12f;

        [Tooltip("Mostrar también en el editor / PC (útil para probar con el ratón).")]
        [SerializeField] bool showInEditor;

        RectTransform _area;
        RectTransform _background;
        RectTransform _handle;
        Canvas _canvas;
        int _pointerId = int.MinValue;
        Vector2 _value;

        static Sprite s_Circle;

        /// <summary>Dirección actual (x = derecha, y = arriba), magnitud ≤ 1.</summary>
        public Vector2 Value => isActiveAndEnabled ? _value : Vector2.zero;

        void Awake()
        {
            if (!Application.isMobilePlatform && !showInEditor)
            {
                gameObject.SetActive(false);
                return;
            }

            _area = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();

            // La zona recibe los toques aunque sea invisible.
            var catcher = GetComponent<Image>();
            if (catcher == null) catcher = gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            // El fondo se ancla al pivote de la zona: así anchoredPosition = punto local del toque.
            _background = CreateCircle("Background", _area, _area.pivot, radius * 2f, new Color(1f, 1f, 1f, 0.25f));
            _handle = CreateCircle("Handle", _background, new Vector2(0.5f, 0.5f), radius * 0.9f, new Color(1f, 1f, 1f, 0.6f));
            _background.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            Release();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != int.MinValue) return;
            _pointerId = eventData.pointerId;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, eventData.position, EventCamera(), out var local))
                _background.anchoredPosition = local;

            _background.gameObject.SetActive(true);
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_background, eventData.position, EventCamera(), out var local))
                return;

            Vector2 offset = Vector2.ClampMagnitude(local, radius);
            _handle.anchoredPosition = offset;

            Vector2 normalized = offset / radius;
            _value = normalized.magnitude < deadZone ? Vector2.zero : normalized;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            Release();
        }

        void Release()
        {
            _pointerId = int.MinValue;
            _value = Vector2.zero;
            if (_handle != null) _handle.anchoredPosition = Vector2.zero;
            if (_background != null) _background.gameObject.SetActive(false);
        }

        Camera EventCamera()
        {
            return _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        }

        static RectTransform CreateCircle(string name, RectTransform parent, Vector2 anchor, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);

            var image = go.GetComponent<Image>();
            image.sprite = CircleSprite();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static Sprite CircleSprite()
        {
            if (s_Circle != null) return s_Circle;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "JoystickCircle" };
            var pixels = new Color[size * size];
            float r = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));   // borde suavizado 1 px
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            s_Circle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            return s_Circle;
        }
    }
}

using UnityEngine;

namespace Game.UI
{
    /// <summary>Limita contenido interactivo al área visible del dispositivo sin recortar el fondo.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        RectTransform _rect;
        Rect _lastArea;
        Vector2Int _lastSize;

        void Awake() => _rect = (RectTransform)transform;
        void OnEnable() => Apply();

        void Update()
        {
            if (_lastArea != Screen.safeArea || _lastSize.x != Screen.width || _lastSize.y != Screen.height)
                Apply();
        }

        void Apply()
        {
            if (_rect == null) _rect = (RectTransform)transform;
            _lastArea = Screen.safeArea;
            _lastSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            _rect.anchorMin = new Vector2(_lastArea.xMin / Screen.width, _lastArea.yMin / Screen.height);
            _rect.anchorMax = new Vector2(_lastArea.xMax / Screen.width, _lastArea.yMax / Screen.height);
            _rect.offsetMin = _rect.offsetMax = Vector2.zero;
        }
    }
}

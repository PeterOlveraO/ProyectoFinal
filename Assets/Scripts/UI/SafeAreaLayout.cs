using UnityEngine;

namespace Game.UI
{
    /// <summary>Fits a full-screen Canvas child to the device's usable area.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaLayout : MonoBehaviour
    {
        RectTransform _rect;
        Rect _lastArea;
        Vector2Int _lastScreen;

        void Awake() => _rect = (RectTransform)transform;
        void OnEnable() => Apply(true);
        void Update() => Apply(false);

        void Apply(bool force)
        {
            if (_rect == null) _rect = (RectTransform)transform;
            var screen = new Vector2Int(Screen.width, Screen.height);
            var area = Screen.safeArea;
            if (screen.x <= 0 || screen.y <= 0) return;
            if (!force && area == _lastArea && screen == _lastScreen) return;
            _lastArea = area;
            _lastScreen = screen;
            _rect.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            _rect.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            _rect.offsetMin = _rect.offsetMax = Vector2.zero;
        }
    }
}

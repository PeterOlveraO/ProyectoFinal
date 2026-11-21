using UnityEngine;

namespace Game.UI
{
    /// <summary>Each popup leaves at least ten percent of the safe map on every edge.</summary>
    public sealed class MenuPageLayout : MonoBehaviour
    {
        RectTransform _safe, _panel;
        Vector2 _maximum, _last;
        public void Configure(RectTransform safe, RectTransform panel, Vector2 maximum)
        {
            _safe = safe;
            _panel = panel;
            _maximum = maximum;
            Refresh();
        }
        public void SetSize(Vector2 maximum) { _maximum = maximum; Refresh(); }
        void OnEnable() { if (_safe != null) Refresh(); }
        void LateUpdate() { if (_safe != null && _last != _safe.rect.size) Refresh(); }
        public void Refresh()
        {
            _last = _safe.rect.size;
            UIFactory.Place(_panel, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(Mathf.Min(_maximum.x, _last.x * .8f), Mathf.Min(_maximum.y, _last.y * .8f)));
        }
    }
}

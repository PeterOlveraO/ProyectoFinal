using UnityEngine;

namespace Game.UI
{
    public enum CompactMenuKind { Main, Pause, Skills }

    /// <summary>Fits the small menu controls inside the existing safe area, not over the map.
    /// Touch and type sizes have screen-pixel floors as well as reference-canvas sizes.</summary>
    [DisallowMultipleComponent]
    public sealed class CompactMenuLayout : MonoBehaviour
    {
        RectTransform _safe, _panel, _header, _body, _footer;
        UnityEngine.UI.GridLayoutGroup _grid;
        Canvas _canvas;
        CompactMenuKind _kind;
        Vector2 _lastSize;
        float _lastScale;

        public void Configure(RectTransform safe, RectTransform panel, RectTransform header,
            RectTransform body, RectTransform footer, CompactMenuKind kind)
        {
            _safe = safe;
            _panel = panel;
            _header = header;
            _body = body;
            _footer = footer;
            _kind = kind;
            _canvas = GetComponentInParent<Canvas>();
            _grid = body.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            _grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.childAlignment = TextAnchor.MiddleCenter;
            RefreshLayout();
        }

        void OnEnable() { if (_safe != null) RefreshLayout(); }
        void LateUpdate()
        {
            if (_safe != null && (_lastSize != _safe.rect.size || _lastScale != _canvas.scaleFactor))
                RefreshLayout();
        }

        public void RefreshLayout()
        {
            if (_safe == null) return;
            _lastSize = _safe.rect.size;
            _lastScale = Mathf.Max(.01f, _canvas.scaleFactor);
            float margin = 16;
            float gap = Mathf.Max(12, 6 / _lastScale);
            float button = Mathf.Max(88, 48 / _lastScale);
            float header = Mathf.Max(112, 56 / _lastScale);
            float font = Mathf.Max(28, 14 / _lastScale);
            bool main = _kind == CompactMenuKind.Main;
            bool skills = _kind == CompactMenuKind.Skills;
            int columns = main ? 3 : skills ? 2 : 1;
            int rows = main || skills ? 2 : 4;
            float width = Mathf.Min(_lastSize.x - margin * 2, main ? 1120 : skills ? 760 : 520);
            float bodyHeight = rows * button + (rows - 1) * gap;
            float footerHeight = skills ? button + gap : 0;

            if (main)
            {
                UIFactory.Place(_panel, new Vector2(.5f, 0), new Vector2(.5f, 0),
                    new Vector2(0, margin), new Vector2(width, bodyHeight));
                UIFactory.Place(_header, new Vector2(.5f, 1), new Vector2(.5f, 1),
                    new Vector2(0, -margin), new Vector2(Mathf.Min(width, 660), header));
                UIFactory.Stretch(_body);
            }
            else
            {
                float height = header + bodyHeight + footerHeight + margin * 2 + gap;
                UIFactory.Place(_panel, new Vector2(1, .5f), new Vector2(1, .5f),
                    new Vector2(-margin, 0), new Vector2(width, height));
                UIFactory.Place(_header, new Vector2(.5f, 1), new Vector2(.5f, 1),
                    new Vector2(0, -margin), new Vector2(width - margin * 2, header));
                UIFactory.Place(_body, new Vector2(.5f, 0), new Vector2(.5f, 0),
                    new Vector2(0, margin + footerHeight),
                    new Vector2(width - margin * 2, bodyHeight));
                UIFactory.Place(_footer, new Vector2(.5f, 0), new Vector2(.5f, 0),
                    new Vector2(0, margin), new Vector2(width - margin * 2, button));
            }
            _grid.constraintCount = columns;
            _grid.spacing = new Vector2(gap, gap);
            _grid.cellSize = new Vector2((_body.rect.width - gap * (columns - 1)) / columns, button);
            foreach (var text in _panel.GetComponentsInChildren<TMPro.TMP_Text>(true))
                text.fontSize = font;
            foreach (var text in _header.GetComponentsInChildren<TMPro.TMP_Text>(true))
                text.fontSize = text.name == "Title" ? font * 1.35f : font;
        }
    }
}

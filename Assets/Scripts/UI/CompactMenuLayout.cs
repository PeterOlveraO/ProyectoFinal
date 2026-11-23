using UnityEngine;

namespace Game.UI
{
    public enum CompactMenuKind { Main, Pause, Skills, Credits }

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
        RectTransform _creditsContent;
        TMPro.TMP_Text _creditsAuthor;
        UnityEngine.UI.GridLayoutGroup _institutions;
        UnityEngine.UI.ScrollRect _creditsScroll;

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
            if (kind == CompactMenuKind.Credits)
            {
                _footer.gameObject.SetActive(true);
                RefreshLayout();
                return;
            }
            _grid = body.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            _grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.childAlignment = TextAnchor.MiddleCenter;
            RefreshLayout();
        }

        public void ConfigureCredits(RectTransform content, TMPro.TMP_Text author,
            UnityEngine.UI.GridLayoutGroup institutions, UnityEngine.UI.ScrollRect scroll)
        {
            _creditsContent = content;
            _creditsAuthor = author;
            _institutions = institutions;
            _creditsScroll = scroll;
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
            if (_kind == CompactMenuKind.Credits)
            {
                RefreshCreditsLayout();
                return;
            }
            float margin = 16;
            float gap = Mathf.Max(12, 6 / _lastScale);
            float button = Mathf.Max(88, 48 / _lastScale);
            float header = Mathf.Max(112, 56 / _lastScale);
            float font = Mathf.Max(28, 14 / _lastScale);
            bool main = _kind == CompactMenuKind.Main;
            bool skills = _kind == CompactMenuKind.Skills;
            int columns = main ? 4 : 2;
            int rows = main || skills ? 2 : 3;
            float width = Mathf.Min(_lastSize.x - margin * 2, main ? 1280 : skills ? 760 : 900);
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

        void RefreshCreditsLayout()
        {
            float margin = Mathf.Max(16, 6 / _lastScale);
            float gap = Mathf.Max(12, 6 / _lastScale);
            float font = Mathf.Max(28, 14 / _lastScale);
            float button = Mathf.Max(64, 48 / _lastScale);
            float header = font * 3.2f;
            float width = Mathf.Min(1000, _lastSize.x * .84f);
            float height = Mathf.Min(Mathf.Max(560, 240 / _lastScale), _lastSize.y * .82f);
            UIFactory.Place(_panel, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(width, height));
            UIFactory.Place(_header, new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -margin), new Vector2(width - margin * 2, header));
            UIFactory.Place(_footer, new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, margin), new Vector2(width - margin * 2, button));
            UIFactory.Stretch(_body);
            _body.offsetMin = new Vector2(margin, margin + button + gap);
            _body.offsetMax = new Vector2(-margin, -margin - header - gap);
            foreach (var text in _panel.GetComponentsInChildren<TMPro.TMP_Text>(true))
                text.fontSize = text.name == "Title" ? font * 1.35f : font;
            if (_creditsContent == null) return;

            _creditsAuthor.fontSize = font * 1.15f;
            var authorSize = _creditsAuthor.GetComponent<UnityEngine.UI.LayoutElement>();
            authorSize.minHeight = authorSize.preferredHeight = _creditsAuthor.fontSize * 1.5f;
            _creditsContent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing = gap;
            float barWidth = Mathf.Max(12, 6 / _lastScale);
            var bar = (RectTransform)_creditsScroll.verticalScrollbar.transform;
            bar.offsetMin = new Vector2(-6 - barWidth, 6);
            bar.offsetMax = new Vector2(-6, -6);
            _creditsScroll.viewport.offsetMax = new Vector2(-12 - barWidth, -6);
            int columns = width * _lastScale >= 520 ? 2 : 1;
            float cardHeight = Mathf.Max(220, 80 / _lastScale);
            _institutions.constraintCount = columns;
            _institutions.spacing = new Vector2(gap, gap);
            var institutionSize = _institutions.GetComponent<UnityEngine.UI.LayoutElement>();
            institutionSize.minHeight = institutionSize.preferredHeight =
                cardHeight * (2 / columns) + (columns == 1 ? gap : 0);
            // The outer vertical stack determines the grid width before sizing its cells.
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_creditsContent);
            _institutions.cellSize = new Vector2(
                (((RectTransform)_institutions.transform).rect.width - gap * (columns - 1)) / columns, cardHeight);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_creditsContent);
            bar.gameObject.SetActive(_creditsContent.rect.height > _creditsScroll.viewport.rect.height + .01f);
        }
    }
}

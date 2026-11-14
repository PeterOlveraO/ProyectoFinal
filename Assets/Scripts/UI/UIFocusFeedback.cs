using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>A visible focus ring shared by mouse, keyboard and controller input.</summary>
    [RequireComponent(typeof(UnityEngine.UI.Selectable))]
    public sealed class UIFocusFeedback : MonoBehaviour, ISelectHandler, IDeselectHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        UnityEngine.UI.Outline _outline;
        UnityEngine.UI.Selectable _selectable;
        bool _focused, _hovered;

        void Awake()
        {
            _selectable = GetComponent<UnityEngine.UI.Selectable>();
            _outline = gameObject.AddComponent<UnityEngine.UI.Outline>();
            _outline.effectColor = UIFactory.Sun;
            _outline.effectDistance = new Vector2(2f, -2f);
            _outline.enabled = false;
        }

        void OnDisable()
        {
            _focused = _hovered = false;
            if (_outline != null) _outline.enabled = false;
        }

        public void OnSelect(BaseEventData data)
        {
            _focused = true;
            Refresh();
            // Keep keyboard-selected cards visible in an inventory scroll view.
            var scroll = GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scroll == null || scroll.content == null || scroll.viewport == null) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, transform);
            float delta = bounds.min.y < scroll.viewport.rect.yMin
                ? scroll.viewport.rect.yMin - bounds.min.y
                : bounds.max.y > scroll.viewport.rect.yMax ? scroll.viewport.rect.yMax - bounds.max.y : 0f;
            if (Mathf.Abs(delta) > .1f)
                scroll.content.anchoredPosition += new Vector2(0f, delta);
        }

        public void OnDeselect(BaseEventData data) { _focused = false; Refresh(); }
        public void OnPointerEnter(PointerEventData data) { _hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData data) { _hovered = false; Refresh(); }
        void Refresh() => _outline.enabled = (_focused || _hovered) && _selectable.IsInteractable();
    }
}

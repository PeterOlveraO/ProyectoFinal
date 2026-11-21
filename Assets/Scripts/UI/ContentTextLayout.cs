using UnityEngine;

namespace Game.UI
{
    /// <summary>Measures wrapped copy after its real column width is known, including resolution changes.</summary>
    [RequireComponent(typeof(TMPro.TMP_Text), typeof(UnityEngine.UI.LayoutElement))]
    public sealed class ContentTextLayout : MonoBehaviour
    {
        TMPro.TMP_Text _text;
        UnityEngine.UI.LayoutElement _layout;
        string _lastText;
        float _lastWidth = -1, _minimum;

        public void Configure(float minimum)
        {
            _minimum = minimum;
            _lastWidth = -1;
        }

        void Awake()
        {
            _text = GetComponent<TMPro.TMP_Text>();
            _layout = GetComponent<UnityEngine.UI.LayoutElement>();
        }

        void LateUpdate()
        {
            float width = _text.rectTransform.rect.width;
            if (width <= 1 || (Mathf.Abs(width - _lastWidth) < .5f && _lastText == _text.text)) return;
            _lastWidth = width;
            _lastText = _text.text;
            _layout.preferredHeight = Mathf.Max(_minimum,
                _text.GetPreferredValues(_text.text, width, Mathf.Infinity).y + 8);
        }
    }
}

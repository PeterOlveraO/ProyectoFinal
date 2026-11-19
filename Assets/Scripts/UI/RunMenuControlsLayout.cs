using UnityEngine;

namespace Game.UI
{
    /// <summary>Keeps the two menu entry points touchable after CanvasScaler downsizing.</summary>
    public sealed class RunMenuControlsLayout : MonoBehaviour
    {
        RectTransform _pause, _skills;
        Canvas _canvas;
        float _scale;

        public void Configure(UnityEngine.UI.Button pause, UnityEngine.UI.Button skills)
        {
            _pause = (RectTransform)pause.transform;
            _skills = (RectTransform)skills.transform;
            _canvas = GetComponentInParent<Canvas>();
            RefreshLayout();
        }

        void LateUpdate()
        {
            if (_canvas != null && _scale != _canvas.scaleFactor) RefreshLayout();
        }

        public void RefreshLayout()
        {
            _scale = Mathf.Max(.01f, _canvas.scaleFactor);
            float height = Mathf.Max(76, 48 / _scale);
            float pauseWidth = Mathf.Max(140, 80 / _scale);
            float skillsWidth = Mathf.Max(90, 48 / _scale);
            float margin = Mathf.Max(16, 6 / _scale);
            UIFactory.Place(_pause, Vector2.one, Vector2.one,
                new Vector2(-margin, -margin), new Vector2(pauseWidth, height));
            UIFactory.Place(_skills, Vector2.one, Vector2.one,
                new Vector2(-margin * 2 - pauseWidth, -margin), new Vector2(skillsWidth, height));
            foreach (var text in GetComponentsInChildren<TMPro.TMP_Text>(true))
                text.fontSize = Mathf.Max(25, 14 / _scale);
        }
    }
}

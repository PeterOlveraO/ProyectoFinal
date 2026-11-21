using UnityEngine;

namespace Game.UI
{
    /// <summary>A bounded, unscaled reveal on the portrait only; never moves buttons or input bounds.</summary>
    public sealed class UIReveal : MonoBehaviour
    {
        float _elapsed;
        void OnEnable()
        {
            _elapsed = 0;
            transform.localScale = Vector3.one * .96f;
        }
        void OnDisable() => transform.localScale = Vector3.one;
        void Update()
        {
            if (_elapsed >= .28f) return;
            _elapsed = Mathf.Min(.28f, _elapsed + Time.unscaledDeltaTime);
            float t = _elapsed / .28f;
            transform.localScale = Vector3.one * Mathf.Lerp(.96f, 1f, 1f - Mathf.Pow(1f - t, 3f));
        }
    }
}

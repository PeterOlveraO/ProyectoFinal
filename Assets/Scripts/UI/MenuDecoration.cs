using UnityEngine;

namespace Game.UI
{
    /// <summary>Animación suave (flotar + girar) para adornos del menú. Usa tiempo sin escalar
    /// para seguir animándose con la partida congelada.</summary>
    [DisallowMultipleComponent]
    public sealed class MenuDecoration : MonoBehaviour
    {
        [SerializeField] float bobAmplitude = 12f;
        [SerializeField] float bobSpeed = 1.2f;
        [SerializeField] float spinSpeed = 10f;

        RectTransform _rect;
        Vector2 _basePosition;
        float _phase;

        public void Configure(float amplitude, float speed, float spin)
        {
            bobAmplitude = amplitude;
            bobSpeed = speed;
            spinSpeed = spin;
        }

        void Awake()
        {
            _rect = (RectTransform)transform;
            _basePosition = _rect.anchoredPosition;
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        void Update()
        {
            if (_rect == null) return;

            float t = Time.unscaledTime;
            _rect.anchoredPosition = _basePosition + new Vector2(0f, Mathf.Sin(t * bobSpeed + _phase) * bobAmplitude);

            if (Mathf.Abs(spinSpeed) > 0.001f)
                _rect.Rotate(0f, 0f, spinSpeed * Time.unscaledDeltaTime);
        }
    }
}

using Unity.Cinemachine;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.World
{
    /// <summary>
    /// Zoom isométrico correcto: cambia la <b>Orthographic Size</b> de la cámara virtual,
    /// nunca la distancia al target.
    ///
    /// Por qué: en ortográfica, mover la cámara hacia el target no acerca nada (el tamaño
    /// en pantalla depende solo de orthographicSize). Moverla solo sirve para recortar
    /// con los planos near/far; el zoom real es orthographicSize.
    ///
    /// Entrada: pinza de dos dedos en móvil, rueda del ratón en editor/PC.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    [DisallowMultipleComponent]
    public sealed class IsoCameraZoom : MonoBehaviour
    {
        [Tooltip("Orthographic Size mínima (más pequeño = más zoom in).")]
        [SerializeField] float minSize = 8f;

        [Tooltip("Orthographic Size máxima (más grande = más campo de visión).")]
        [SerializeField] float maxSize = 44f;

        [Tooltip("Cuánto cambia la Orthographic Size por muesca de rueda.")]
        [SerializeField] float stepPerNotch = 1.5f;

        [Tooltip("Sensibilidad de la pinza (unidades de Orthographic Size por píxel de pinza).")]
        [SerializeField] float pinchSensitivity = 0.05f;

        [Tooltip("Suavizado del zoom. Más alto = más instantáneo.")]
        [SerializeField] float smoothing = 12f;

        CinemachineCamera _vcam;
        float _target;
        bool _pinching;
        float _lastPinchDistance;

        void Awake()
        {
            _vcam = GetComponent<CinemachineCamera>();
            if (_vcam != null) _target = _vcam.Lens.OrthographicSize;
        }

        void OnEnable()
        {
            if (_vcam == null) _vcam = GetComponent<CinemachineCamera>();
            if (_vcam != null) _target = _vcam.Lens.OrthographicSize;
        }

        void Update()
        {
            if (_vcam == null) return;

            ReadInput();

            var lens = _vcam.Lens;
            float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            lens.OrthographicSize = Mathf.Lerp(lens.OrthographicSize, _target, t);
            _vcam.Lens = lens;
        }

        void ReadInput()
        {
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            if (touch != null &&
                touch.touches[0].press.isPressed &&
                touch.touches[1].press.isPressed)
            {
                float distance = Vector2.Distance(
                    touch.touches[0].position.ReadValue(),
                    touch.touches[1].position.ReadValue());

                if (_pinching)
                    _target = Mathf.Clamp(_target - (distance - _lastPinchDistance) * pinchSensitivity,
                                          minSize, maxSize);

                _lastPinchDistance = distance;
                _pinching = true;
                return;
            }

            _pinching = false;

            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            _target = Mathf.Clamp(_target - Mathf.Sign(scroll) * stepPerNotch, minSize, maxSize);
#endif
        }
    }
}

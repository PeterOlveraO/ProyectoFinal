using UnityEngine;

namespace Game.Rendering
{
    /// <summary>
    /// Orienta un sprite 2D para que mire a la cámara isométrica.
    ///
    /// Con cámara FIJA la rotación es constante: se calcula una vez en Start (coste 0 por frame).
    /// Solo activa "continuous" si algún día la cámara rota.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BillboardSprite : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;

        [Tooltip("Recalcular cada frame. Déjalo apagado con cámara isométrica fija.")]
        [SerializeField] bool continuous;

        void Start()
        {
            if (!continuous) Align();
        }

        void LateUpdate()
        {
            if (continuous) Align();
        }

        void Align()
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            // El sprite (plano XY) debe mirar hacia la cámara: forward = -forward de la cámara.
            Vector3 forward = -cam.transform.forward;
            forward.y = 0f;                     // cámara isométrica: el sprite queda vertical
            if (forward.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}

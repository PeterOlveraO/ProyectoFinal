using System.Collections.Generic;
using UnityEngine;

namespace Game.Terrain
{
    /// <summary>
    /// Optimización de dibujado: apaga SOLO el <see cref="Renderer"/> de lo que queda fuera
    /// del frustum de la cámara principal. El GameObject, sus scripts y sus colliders siguen
    /// activos, así que la lógica (persecución, spawns, colisiones) no se pierde: únicamente
    /// se deja de dibujar.
    ///
    /// - Terreno: los chunks registran su renderer; el collider y la malla siguen vivos.
    /// - Enemigos: se registran al activarse y se dan de baja al desactivarse.
    /// - Se recalcula cada <see cref="updateInterval"/> segundos (no cada frame) y con un
    ///   margen alrededor de los bounds para evitar parpadeos en los bordes de la pantalla.
    ///
    /// Los objetos se siguen actualizando (Update) aunque no se rendericen.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class VisibilityCuller : MonoBehaviour
    {
        [Tooltip("Segundos entre recálculos del frustum. Más alto = menos CPU, más popping.")]
        [SerializeField, Min(0.02f)] float updateInterval = 0.15f;

        [Tooltip("Margen en unidades alrededor del objeto para evitar que aparezca/desaparezca en el borde.")]
        [SerializeField, Min(0f)] float boundsMargin = 1.5f;

        static readonly List<Renderer> TerrainRenderers = new List<Renderer>(128);
        static readonly List<Renderer> EnemyRenderers = new List<Renderer>(128);

        readonly Plane[] _planes = new Plane[6];
        Camera _camera;
        float _timer;

        // ------------------------------------------------------- Registro

        public static void RegisterTerrain(Renderer renderer) => Register(TerrainRenderers, renderer);

        public static void UnregisterTerrain(Renderer renderer) => TerrainRenderers.Remove(renderer);

        public static void RegisterEnemy(Renderer renderer) => Register(EnemyRenderers, renderer);

        public static void UnregisterEnemy(Renderer renderer) => EnemyRenderers.Remove(renderer);

        static void Register(List<Renderer> list, Renderer renderer)
        {
            if (renderer != null && !list.Contains(renderer)) list.Add(renderer);
        }

        // ------------------------------------------------------- Ciclo

        void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = updateInterval;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            GeometryUtility.CalculateFrustumPlanes(_camera, _planes);
            ApplyVisibility(TerrainRenderers);
            ApplyVisibility(EnemyRenderers);
        }

        void ApplyVisibility(List<Renderer> renderers)
        {
            for (int i = renderers.Count - 1; i >= 0; i--)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    renderers.RemoveAt(i);
                    continue;
                }

                // Un chunk desactivado por el sistema de carga no se toca: al reactivarse
                // el siguiente recálculo decide si se ve o no.
                if (!renderer.gameObject.activeInHierarchy) continue;

                renderer.enabled = IsVisible(renderer.bounds);
            }
        }

        bool IsVisible(Bounds bounds)
        {
            bounds.Expand(boundsMargin * 2f);
            return GeometryUtility.TestPlanesAABB(_planes, bounds);
        }

        void OnDestroy()
        {
            RestoreAll(TerrainRenderers);
            RestoreAll(EnemyRenderers);
            TerrainRenderers.Clear();
            EnemyRenderers.Clear();
        }

        static void RestoreAll(List<Renderer> renderers)
        {
            foreach (var renderer in renderers)
                if (renderer != null) renderer.enabled = true;
        }
    }
}

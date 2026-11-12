using UnityEngine;

namespace Game.Enemies
{
    /// <summary>Punto de aparición visible en la escena (solo gizmo, sin coste en runtime).</summary>
    [DisallowMultipleComponent]
    public sealed class SpawnPointMarker : MonoBehaviour
    {
        public int Zone = 1;

        void OnDrawGizmos()
        {
            Gizmos.color = ZoneColor(Zone);
            var basePosition = transform.position + Vector3.up * 0.5f;
            Gizmos.DrawWireCube(basePosition, new Vector3(0.8f, 1f, 0.8f));
            Gizmos.DrawLine(basePosition, basePosition + Vector3.up * 1.5f);
        }

        public static Color ZoneColor(int zone)
        {
            switch (zone)
            {
                case 2: return new Color(1f, 0.65f, 0.15f, 1f);
                case 3: return new Color(0.35f, 0.55f, 1f, 1f);
                default: return new Color(0.35f, 0.9f, 0.4f, 1f);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Game.Terrain
{
    /// <summary>Lago circular decorativo: centro y radio en bloques, y niveles del borde y del fondo.</summary>
    public struct LakeInfo
    {
        public Vector2Int Center;
        public int Radius;
        public int RimLevel;
        public int BottomLevel;
    }

    /// <summary>
    /// Niveles enteros del mundo completo, un valor por bloque (x hacia +X, z hacia +Z),
    /// más la zona (1..3) de cada bloque y la lista de lagos generados.
    /// </summary>
    public sealed class HeightMap
    {
        public int Width { get; }
        public int Depth { get; }

        readonly int[] _levels;
        readonly byte[] _zones;

        /// <summary>Lagos generados (solo informativo: el terreno ya está excavado en _levels).</summary>
        public readonly List<LakeInfo> Lakes = new List<LakeInfo>();

        public HeightMap(int width, int depth)
        {
            Width = width;
            Depth = depth;
            _levels = new int[width * depth];
            _zones = new byte[width * depth];
        }

        public bool Contains(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Depth;

        public int Get(int x, int z) => _levels[z * Width + x];

        public void Set(int x, int z, int level) => _levels[z * Width + x] = level;

        /// <summary>Zona del bloque: 1 (normal), 2 (elevada) o 3 (baja).</summary>
        public int GetZone(int x, int z)
        {
            int zone = _zones[z * Width + x];
            return zone == 0 ? 1 : zone;
        }

        public void SetZone(int x, int z, int zone) => _zones[z * Width + x] = (byte)Mathf.Clamp(zone, 1, 3);
    }
}

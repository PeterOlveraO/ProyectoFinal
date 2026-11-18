using System.Collections.Generic;
using UnityEngine;

namespace Game.Terrain
{
    /// <summary>Disco de celdas: la misma máscara define excavación, agua y obstáculos.</summary>
    public struct LakeInfo
    {
        public Vector2Int Center;
        public int Radius;
        public int RimLevel;
        public int BottomLevel;

        public bool ContainsCell(int x, int z)
        {
            int dx = x - Center.x;
            int dz = z - Center.y;
            return dx * dx + dz * dz <= Radius * Radius;
        }

        /// <summary>Tramo inclusivo de una fila del disco (z debe estar dentro del radio).</summary>
        public void GetRowBounds(int z, out int minX, out int maxX)
        {
            int dz = z - Center.y;
            int extent = Mathf.FloorToInt(Mathf.Sqrt(Mathf.Max(0, Radius * Radius - dz * dz)));
            minX = Center.x - extent;
            maxX = Center.x + extent;
        }
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

        /// <summary>Lagos generados: máscara compartida con agua, colliders y consultas.</summary>
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

        public bool IsLakeCell(int x, int z)
        {
            if (!Contains(x, z)) return false;
            foreach (var lake in Lakes)
                if (lake.ContainsCell(x, z)) return true;
            return false;
        }
    }
}

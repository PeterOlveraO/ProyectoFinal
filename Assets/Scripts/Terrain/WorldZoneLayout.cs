using UnityEngine;

namespace Game.Terrain
{
    /// <summary>Purchase regions, independent of the three concentric terrain biomes.</summary>
    public sealed class WorldZoneLayout
    {
        public const int ZoneCount = 9;
        public const int AllUnlockedMask = (1 << ZoneCount) - 1;
        // Rows run from south (-Z) to north (+Z).
        static readonly int[] Ids = { 8, 4, 7, 5, 1, 3, 9, 2, 6 };
        public Vector3 Origin { get; }
        public float Width { get; }
        public float Depth { get; }
        public float MountainWidth { get; }

        public WorldZoneLayout(int width, int depth, float blockSize, Vector3 origin)
        {
            Origin = origin;
            Width = width * blockSize;
            Depth = depth * blockSize;
            // Cell-aligned, 16 units at 256x256; scale down for small fixtures.
            MountainWidth = Mathf.Min(Mathf.Min(Width, Depth) * .25f,
                Mathf.Max(blockSize, Mathf.Floor(Mathf.Min(16f,
                    Mathf.Min(Width, Depth) / 16f) / blockSize) * blockSize));
        }

        public int ZoneAt(Vector3 position)
        {
            float x = position.x - Origin.x, z = position.z - Origin.z;
            if (!Finite(x) || !Finite(z) || x < 0 || z < 0 || x >= Width || z >= Depth) return 0;
            return Ids[Band(z, Depth) * 3 + Band(x, Width)];
        }

        public Rect Bounds(int zoneId)
        {
            int index = Index(zoneId);
            if (index < 0) return default;
            int col = index % 3, row = index / 3;
            float x = Split(col, Width), z = Split(row, Depth);
            return Rect.MinMaxRect(Origin.x + x, Origin.z + z,
                Origin.x + Split(col + 1, Width), Origin.z + Split(row + 1, Depth));
        }

        /// <summary>Four-unit roads: central cross plus the outer rectangle joining all corner gates.</summary>
        public bool IsAccessCorridor(Vector3 position, float clearance = 0f)
        {
            float x = position.x - Origin.x, z = position.z - Origin.z;
            float half = 2f + Mathf.Max(0f, clearance);
            float west = Width * 5f / 32f, east = Width * 27f / 32f;
            float south = Depth * 5f / 32f, north = Depth * 27f / 32f;
            return Mathf.Abs(x - Width * .5f) <= half || Mathf.Abs(z - Depth * .5f) <= half ||
                ((Mathf.Abs(x - west) <= half || Mathf.Abs(x - east) <= half) &&
                    z >= south - half && z <= north + half) ||
                ((Mathf.Abs(z - south) <= half || Mathf.Abs(z - north) <= half) &&
                    x >= west - half && x <= east + half);
        }

        /// <summary>Geometry-only vendor layout, in cells. Runtime shop identities map onto these slots.</summary>
        public static int VendorCount(int zoneId) => zoneId == 1 ? 3 :
            zoneId >= 2 && zoneId <= 5 ? 2 : zoneId >= 6 && zoneId <= 9 ? 1 : 0;
        public static Vector2Int VendorOffset(int zoneId, int slot)
        {
            if (zoneId == 1)
                return slot == 0 ? new Vector2Int(-7, 4) : slot == 1 ? new Vector2Int(7, 4) : new Vector2Int(0, -8);
            if (zoneId >= 2 && zoneId <= 5) return new Vector2Int(slot == 0 ? -4 : 4, 0);
            return Vector2Int.zero;
        }

        public bool IsMountain(Vector3 position)
        {
            float x = position.x - Origin.x, z = position.z - Origin.z;
            return ZoneAt(position) == 0 || x < MountainWidth || z < MountainWidth ||
                x >= Width - MountainWidth || z >= Depth - MountainWidth;
        }

        public static int Cost(int zoneId) => zoneId == 1 ? 0 :
            zoneId >= 2 && zoneId <= 5 ? 250 : zoneId >= 6 && zoneId <= 9 ? 600 : -1;

        public static bool AreAdjacent(int a, int b)
        {
            int ai = Index(a), bi = Index(b);
            return ai >= 0 && bi >= 0 &&
                Mathf.Abs(ai % 3 - bi % 3) + Mathf.Abs(ai / 3 - bi / 3) == 1;
        }

        public bool TryGetGate(int fromZone, int toZone, out Vector3 boundary, out Vector3 direction)
        {
            boundary = direction = default;
            if (!AreAdjacent(fromZone, toZone)) return false;
            Rect a = Bounds(fromZone), b = Bounds(toZone);
            Vector2 delta = b.center - a.center;
            direction = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? new Vector3(Mathf.Sign(delta.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(delta.y));
            boundary = new Vector3(direction.x > 0 ? a.xMax : direction.x < 0 ? a.xMin : a.center.x,
                Origin.y, direction.z > 0 ? a.yMax : direction.z < 0 ? a.yMin : a.center.y);
            return true;
        }

        static int Band(float value, float size) => value < size * (5f / 16f) ? 0 :
            value < size * (11f / 16f) ? 1 : 2;
        static float Split(int index, float size) => index == 0 ? 0 :
            index == 1 ? size * (5f / 16f) : index == 2 ? size * (11f / 16f) : size;
        static int Index(int id)
        {
            for (int i = 0; i < Ids.Length; i++) if (Ids[i] == id) return i;
            return -1;
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>No dependency on Game.Runtime; implemented by the run's purchase state.</summary>
    public interface IWorldZoneAccess
    {
        int Revision { get; }
        int UnlockedMask { get; }
        bool IsUnlocked(int zoneId);
    }
}

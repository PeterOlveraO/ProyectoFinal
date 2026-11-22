using System.Collections.Generic;
using System;
using Game.Player;
using Game.Terrain;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>
    /// Shared deterministic shop reservations and bounded placement searches.
    /// Zoned worlds distribute vendors; small legacy maps retain a central hub.
    /// </summary>
    public static class WorldShopPlacement
    {
        // Reserved for tycoon inside the flora-free spawn clearing (15 world units).
        public const float TycoonReservationRadius = 15f;
        public const int TycoonSlots = 15;
        public const float VendorClearingRadius = 6f;
        const float AnchorSearchRadius = 3f;
        const float SpawnClearance = 5f;
        const float CollisionRadius = 0.48f;
        const float CollisionHeight = 0.72f;
        static readonly Collider[] OverlapBuffer = new Collider[32];
        static readonly WorldZoneLayout CellLayout = new WorldZoneLayout(256, 256, 1f, Vector3.zero);

        // Set by the run's world controller. Null keeps legacy / isolated fixtures usable.
        public static Func<int, bool> IsZoneAccessible { get; set; }
        public static bool CanAccessZone(int zone)
        {
            if (zone <= 0) return false;
            var terrain = WorldTerrain.Instance;
            return (terrain == null || terrain.Traversal == null || terrain.Traversal.IsUnlocked(zone)) &&
                (IsZoneAccessible == null || IsZoneAccessible(zone));
        }

        public static bool UsesWorldZones(WorldTerrain terrain) =>
            terrain != null && terrain.Settings != null && terrain.Settings.enableWorldZones &&
            terrain.Map != null && terrain.Map.Width >= 256 && terrain.Map.Depth >= 256;

        public static Vector3[] FindDrinkPositions(WorldTerrain terrain) =>
            UsesWorldZones(terrain) ? FindZonePositions(terrain, false) :
            FindPositions(terrain, DrinkStations.OfferCount, 0x51A7, null, TycoonSlots);

        public static Vector3[] FindShotPositions(WorldTerrain terrain, IReadOnlyList<Vector3> reserved = null) =>
            UsesWorldZones(terrain) ? FindZonePositions(terrain, true, reserved) :
            FindPositions(terrain, ShotStations.OfferCount, 104729, reserved, TycoonSlots);

        /// <summary>Call before flora is generated. Radius includes the decoration's own footprint.</summary>
        public static bool IsReservedForShop(WorldTerrain terrain, Vector3 point, float radius)
        {
            if (terrain == null || terrain.Map == null) return false;
            radius = Mathf.Max(0f, radius);
            if (!UsesWorldZones(terrain))
                return HorizontalDistanceSquared(point, terrain.SpawnPosition) <=
                    (TycoonReservationRadius + radius) * (TycoonReservationRadius + radius);
            float clearance = VendorClearingRadius + radius;
            for (int i = 0; i < DrinkStations.OfferCount; i++)
                if (HorizontalDistanceSquared(point, GetAnchor(terrain, false, i)) <= clearance * clearance) return true;
            for (int i = 0; i < ShotStations.OfferCount; i++)
                if (HorizontalDistanceSquared(point, GetAnchor(terrain, true, i)) <= clearance * clearance) return true;
            return false;
        }

        /// <summary>Pure layout offsets in cells; also usable by terrain generation to flatten vendor clearings.</summary>
        public static Vector2Int GetAnchorOffset(bool shot, int index)
        {
            if (shot)
                return index == 3 ? new Vector2Int(0, -8) :
                    index < 3 ? new Vector2Int(4, 0) : Vector2Int.zero;
            switch ((DrinkType)index)
            {
                case DrinkType.Power: return new Vector2Int(-7, 4);
                case DrinkType.Vitality: return new Vector2Int(7, 4);
                case DrinkType.Recovery: return new Vector2Int(4, 0);
                case DrinkType.Reserve: return Vector2Int.zero;
                default: return new Vector2Int(-4, 0);
            }
        }

        public static Vector3 GetAnchor(WorldTerrain terrain, bool shot, int index)
        {
            int zone = shot ? ShotStations.OfferZone(index) : DrinkStations.OfferZone(index);
            Vector2Int center = ZoneCenterCell(terrain, zone);
            Vector2Int offset = GetAnchorOffset(shot, index);
            return terrain.BlockTopCenter(center.x + offset.x, center.y + offset.y);
        }

        static Vector2Int ZoneCenterCell(WorldTerrain terrain, int zone)
        {
            if (zone < 1 || zone > WorldZoneLayout.ZoneCount)
                throw new ArgumentOutOfRangeException(nameof(zone));
            Vector2 center = CellLayout.Bounds(zone).center;
            return new Vector2Int(Mathf.FloorToInt(center.x * terrain.Map.Width / 256f),
                Mathf.FloorToInt(center.y * terrain.Map.Depth / 256f));
        }

        static Vector3[] FindZonePositions(WorldTerrain terrain, bool shot, IReadOnlyList<Vector3> reserved = null)
        {
            var result = new Vector3[shot ? ShotStations.OfferCount : DrinkStations.OfferCount];
            Physics.SyncTransforms();
            int cells = Mathf.CeilToInt(AnchorSearchRadius / terrain.BlockSize);
            for (int i = 0; i < result.Length; i++)
            {
                int zone = shot ? ShotStations.OfferZone(i) : DrinkStations.OfferZone(i);
                Vector2Int center = ZoneCenterCell(terrain, zone);
                Vector2Int anchorCell = center + GetAnchorOffset(shot, i);
                Vector3 anchor = terrain.BlockTopCenter(anchorCell.x, anchorCell.y);
                float best = float.PositiveInfinity;
                bool found = false;
                for (int z = anchorCell.y - cells; z <= anchorCell.y + cells; z++)
                for (int x = anchorCell.x - cells; x <= anchorCell.x + cells; x++)
                {
                    if (!terrain.Map.Contains(x, z) || !IsLocallyFlat(terrain.Map, x, z)) continue;
                    Vector3 point = terrain.BlockTopCenter(x, z);
                    float distance = HorizontalDistanceSquared(point, anchor);
                    if (distance > AnchorSearchRadius * AnchorSearchRadius || distance >= best ||
                        !IsDryAndInsideClearance(terrain, point) || IsReserved(point, reserved) || HasBlockingCollider(terrain, point) ||
                        !HasLocalApproach(terrain, center, point)) continue;
                    // Keep all stations separated even when the nearest valid cell shifts.
                    bool overlaps = false;
                    for (int j = 0; j < i; j++)
                        if (HorizontalDistanceSquared(point, result[j]) < 4.5f * 4.5f) overlaps = true;
                    if (overlaps) continue;
                    result[i] = point;
                    best = distance;
                    found = true;
                }
                if (!found)
                    throw new InvalidOperationException("No hay un claro seco, llano y accesible para el puesto " +
                        i + " de la zona " + zone + ".");
            }
            return result;
        }

        static bool HasLocalApproach(WorldTerrain terrain, Vector2Int center, Vector3 point)
        {
            Vector3 start = terrain.BlockTopCenter(center.x, center.y);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, point) / (terrain.BlockSize * .5f)));
            int previousLevel = terrain.Map.Get(center.x, center.y);
            for (int step = 0; step <= steps; step++)
            {
                Vector3 sample = Vector3.Lerp(start, point, step / (float)steps);
                Vector3 local = sample - terrain.transform.position;
                int x = Mathf.FloorToInt(local.x / terrain.BlockSize);
                int z = Mathf.FloorToInt(local.z / terrain.BlockSize);
                if (!terrain.Map.Contains(x, z)) return false;
                int level = terrain.Map.Get(x, z);
                Vector3 ground = terrain.BlockTopCenter(x, z);
                if (Mathf.Abs(level - previousLevel) > 1 || !IsDryAndInsideClearance(terrain, ground) ||
                    HasBlockingCollider(terrain, ground)) return false;
                previousLevel = level;
            }
            return true;
        }

        /// <summary>
        /// Encuentra exactamente <paramref name="count"/> posiciones de suelo. reserved permite
        /// evitar ubicaciones ocupadas por otros grupos y no se modifica. Lanza una excepción
        /// descriptiva si no hay terreno válido o no se logra el grupo completo.
        /// </summary>
        public static Vector3[] FindPositions(WorldTerrain terrain, int count, int seedOffset,
            IReadOnlyList<Vector3> reserved = null, int slots = TycoonSlots)
        {
            if (terrain == null || terrain.Map == null || count < 1)
                throw new ArgumentException("Se requiere terreno generado y una cantidad positiva de puestos.");

            HeightMap map = terrain.Map;
            Vector3 spawn = terrain.SpawnPosition;
            Vector3 localSpawn = spawn - terrain.transform.position;
            int spawnX = Mathf.FloorToInt(localSpawn.x / terrain.BlockSize);
            int spawnZ = Mathf.FloorToInt(localSpawn.z / terrain.BlockSize);
            bool[,] reachable = FindReachable(terrain, spawnX, spawnZ);
            var candidates = new List<Candidate>();

            int searchCells = Mathf.CeilToInt(12f / terrain.BlockSize);
            for (int z = Mathf.Max(1, spawnZ - searchCells); z <= Mathf.Min(map.Depth - 2, spawnZ + searchCells); z++)
            for (int x = Mathf.Max(1, spawnX - searchCells); x <= Mathf.Min(map.Width - 2, spawnX + searchCells); x++)
            {
                if (!reachable[x, z] || !IsLocallyFlat(map, x, z)) continue;

                Vector3 point = terrain.BlockTopCenter(x, z);
                Vector3 delta = point - spawn;
                delta.y = 0f;
                if (delta.sqrMagnitude < SpawnClearance * SpawnClearance ||
                    delta.sqrMagnitude > 12f * 12f ||
                    !IsDryAndInsideClearance(terrain, point))
                    continue;

                if (IsReserved(point, reserved) || HasBlockingCollider(terrain, point))
                    continue;

                candidates.Add(new Candidate(x, z,
                    Hash(x, z, terrain.Settings.seed + seedOffset)));
            }

            candidates.Sort((a, b) => a.Order.CompareTo(b.Order));
            var positions = new List<Vector3>(count);
            // Two rings fit fifteen pads while retaining a clear approach from spawn.
            // The legacy nine-slot API remains available for compact existing callers.
            for (int slot = 0; slot < slots && positions.Count < count; slot++)
            {
                bool inner = slots == TycoonSlots && slot < 5;
                int ringSlot = slots == TycoonSlots && !inner ? slot - 5 : slot;
                int ringCount = slots == TycoonSlots ? (inner ? 5 : 10) : slots;
                float angle = ringSlot * Mathf.PI * 2f / ringCount;
                float ringRadius = slots == TycoonSlots ? (inner ? 6f : 11.5f) : 9f;
                Vector3 target = spawn + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * ringRadius;
                Candidate best = default;
                float score = float.PositiveInfinity;
                bool found = false;
                foreach (Candidate candidate in candidates)
                {
                    Vector3 point = terrain.BlockTopCenter(candidate.X, candidate.Z);
                    if (IsSelected(candidate, positions, terrain) || IsReserved(point, reserved)) continue;
                    float distance = HorizontalDistanceSquared(point, target);
                    if (distance > 3f * 3f || distance >= score) continue;
                    best = candidate;
                    score = distance;
                    found = true;
                }
                if (found) positions.Add(terrain.BlockTopCenter(best.X, best.Z));
            }

            if (positions.Count != count)
            {
                throw new InvalidOperationException($"Solo se encontraron {positions.Count}/{count} ubicaciones válidas " +
                    "para los puestos (terreno conectado, llano, seco, separado y sin colisiones).");
            }

            return positions.ToArray();
        }

        static bool IsSelected(Candidate candidate, List<Vector3> selected, WorldTerrain terrain)
        {
            Vector3 point = terrain.BlockTopCenter(candidate.X, candidate.Z);
            foreach (Vector3 other in selected)
            {
                Vector3 delta = point - other;
                delta.y = 0f;
                if (delta.sqrMagnitude < 4.5f * 4.5f) return true;
            }
            return false;
        }

        static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        static bool IsReserved(Vector3 point, IReadOnlyList<Vector3> reserved)
        {
            if (reserved == null) return false;
            foreach (Vector3 other in reserved)
                if (HorizontalDistanceSquared(point, other) < 4.5f * 4.5f) return true;
            return false;
        }

        static bool HasBlockingCollider(WorldTerrain terrain, Vector3 ground)
        {
            int count = Physics.OverlapSphereNonAlloc(ground + Vector3.up * CollisionHeight, CollisionRadius,
                OverlapBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // Un resultado lleno puede haber truncado colliders: fallar de forma conservadora.
            if (count == OverlapBuffer.Length) return true;
            for (int i = 0; i < count; i++)
                if (BlocksPath(OverlapBuffer[i], terrain))
                    return true;
            return false;
        }

        static bool IsDryAndInsideClearance(WorldTerrain terrain, Vector3 position)
        {
            HeightMap map = terrain.Map;
            float blockSize = terrain.BlockSize;
            float bx = (position.x - terrain.transform.position.x) / blockSize;
            float bz = (position.z - terrain.transform.position.z) / blockSize;
            float radius = CollisionRadius / blockSize;
            if (bx - radius < 0f || bz - radius < 0f ||
                bx + radius >= map.Width || bz + radius >= map.Depth)
                return false;

            int minX = Mathf.FloorToInt(bx - radius);
            int maxX = Mathf.FloorToInt(bx + radius);
            int minZ = Mathf.FloorToInt(bz - radius);
            int maxZ = Mathf.FloorToInt(bz + radius);
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                Vector3 cellCenter = terrain.BlockTopCenter(x, z);
                if (!terrain.IsLakeAtWorld(cellCenter)) continue;
                float dx = Mathf.Max(Mathf.Max(x - bx, 0f), bx - x - 1f);
                float dz = Mathf.Max(Mathf.Max(z - bz, 0f), bz - z - 1f);
                if (dx * dx + dz * dz <= radius * radius) return false;
            }
            return true;
        }

        static bool BlocksPath(Collider collider, WorldTerrain terrain)
        {
            if (collider == null ||
                (collider is MeshCollider && collider.transform.parent == terrain.transform))
                return false;
            // El jugador y los cuerpos móviles no convierten una ruta en inaccesible.
            if (collider.GetComponentInParent<PlayerSphereController>() != null) return false;
            Rigidbody body = collider.attachedRigidbody;
            if (body != null && !body.isKinematic) return false;
            return true;
        }

        static bool IsLocallyFlat(HeightMap map, int x, int z)
        {
            int level = map.Get(x, z);
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (!map.Contains(x + dx, z + dz) || map.Get(x + dx, z + dz) != level)
                    return false;
            return true;
        }

        static bool[,] FindReachable(WorldTerrain terrain, int startX, int startZ)
        {
            HeightMap map = terrain.Map;
            var reachable = new bool[map.Width, map.Depth];
            Physics.SyncTransforms();
            if (!map.Contains(startX, startZ))
                return reachable;
            Vector3 start = terrain.BlockTopCenter(startX, startZ);
            if (!IsDryAndInsideClearance(terrain, start) || HasBlockingCollider(terrain, start))
                return reachable;

            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(startX, startZ));
            reachable[startX, startZ] = true;
            int[] dx = { 1, -1, 0, 0 };
            int[] dz = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int x = cell.x + dx[i];
                    int z = cell.y + dz[i];
                    if (!map.Contains(x, z) || reachable[x, z]) continue;
                    if (Mathf.Abs(map.Get(x, z) - map.Get(cell.x, cell.y)) > 1) continue;
                    Vector3 point = terrain.BlockTopCenter(x, z);
                    if (HorizontalDistanceSquared(point, start) >
                        TycoonReservationRadius * TycoonReservationRadius) continue;
                    if (!IsDryAndInsideClearance(terrain, point) || HasBlockingCollider(terrain, point))
                        continue;
                    reachable[x, z] = true;
                    queue.Enqueue(new Vector2Int(x, z));
                }
            }
            return reachable;
        }

        static int Hash(int x, int z, int seed)
        {
            unchecked
            {
                uint value = (uint)(x * 0x1f1f1f1f ^ z * 0x5f356495 ^ seed);
                value ^= value >> 16;
                value *= 0x7feb352d;
                value ^= value >> 15;
                value *= 0x846ca68b;
                value ^= value >> 16;
                return (int)(value & 0x7fffffff);
            }
        }

        readonly struct Candidate
        {
            public readonly int X;
            public readonly int Z;
            public readonly int Order;

            public Candidate(int x, int z, int order)
            {
                X = x;
                Z = z;
                Order = order;
            }
        }
    }
}

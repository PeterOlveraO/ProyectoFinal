using System.Collections.Generic;
using System;
using Game.Player;
using Game.Terrain;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>
    /// Compact run-only tycoon hub in the flora-free spawn clearing. All pads must be
    /// dry, flat, collision-free and connected to spawn; failure never scatters vendors.
    /// </summary>
    public static class WorldShopPlacement
    {
        // Reserved for tycoon inside the flora-free spawn clearing (15 world units).
        public const float TycoonReservationRadius = 15f;
        public const int TycoonSlots = 9;
        const float SpawnClearance = 5f;
        const float CollisionRadius = 0.48f;
        const float CollisionHeight = 0.72f;
        static readonly Collider[] OverlapBuffer = new Collider[32];

        /// <summary>
        /// Encuentra exactamente <paramref name="count"/> posiciones de suelo. reserved permite
        /// evitar ubicaciones ocupadas por otros grupos y no se modifica. Lanza una excepción
        /// descriptiva si no hay terreno válido o no se logra el grupo completo.
        /// </summary>
        public static Vector3[] FindPositions(WorldTerrain terrain, int count, int seedOffset,
            IReadOnlyList<Vector3> reserved = null)
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

            for (int z = 1; z < map.Depth - 1; z++)
            for (int x = 1; x < map.Width - 1; x++)
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

                candidates.Add(new Candidate(x, z, map.GetZone(x, z),
                    Hash(x, z, terrain.Settings.seed + seedOffset)));
            }

            candidates.Sort((a, b) => a.Order.CompareTo(b.Order));
            var positions = new List<Vector3>(count);
            // Nine sectors around the spawn: one compact hub, never scattered over zones.
            // Both groups traverse the same order; reserved pads consume their sectors.
            for (int slot = 0; slot < TycoonSlots && positions.Count < count; slot++)
            {
                float angle = slot * Mathf.PI * 2f / TycoonSlots;
                Vector3 target = spawn + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 9f;
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

        static int[] BuildQuotas(int count)
        {
            int[] quota = { count / 3, count / 3, count / 3 };
            for (int i = 0; i < count % 3; i++) quota[i]++;
            return quota;
        }

        static void SelectFromZone(WorldTerrain terrain, List<Candidate> candidates, int zone, int quota,
            IReadOnlyList<Vector3> reserved, List<Vector3> selected)
        {
            for (int i = 0; i < quota; i++)
            {
                Candidate best = default;
                float bestScore = float.NegativeInfinity;
                bool found = false;
                foreach (Candidate candidate in candidates)
                {
                    if (candidate.Zone != zone || IsSelected(candidate, selected, terrain)) continue;
                    Vector3 point = terrain.BlockTopCenter(candidate.X, candidate.Z);
                    float score = Dispersion(point, selected, reserved);
                    // Hash estable desempata; prioriza separación y ligeramente el orden semialeatorio.
                    score += (candidate.Order & 0xffff) / 65535f;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    best = candidate;
                    found = true;
                }

                if (!found) return;
                selected.Add(terrain.BlockTopCenter(best.X, best.Z));
            }
        }

        static void SelectFromAnyZone(WorldTerrain terrain, List<Candidate> candidates, int count,
            IReadOnlyList<Vector3> reserved, List<Vector3> selected)
        {
            while (selected.Count < count)
            {
                Candidate best = default;
                float bestScore = float.NegativeInfinity;
                bool found = false;
                foreach (Candidate candidate in candidates)
                {
                    if (IsSelected(candidate, selected, terrain)) continue;
                    Vector3 point = terrain.BlockTopCenter(candidate.X, candidate.Z);
                    float score = Dispersion(point, selected, reserved) +
                                  (candidate.Order & 0xffff) / 65535f;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    best = candidate;
                    found = true;
                }

                if (!found) return;
                selected.Add(terrain.BlockTopCenter(best.X, best.Z));
            }
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

        static float Dispersion(Vector3 point, List<Vector3> selected, IReadOnlyList<Vector3> reserved)
        {
            float nearest = float.PositiveInfinity;
            if (selected.Count == 0 && (reserved == null || reserved.Count == 0))
                return point.x * 0.0137f + point.z * 0.0211f;

            if (selected != null)
                foreach (Vector3 other in selected)
                    nearest = Mathf.Min(nearest, HorizontalDistanceSquared(point, other));
            if (reserved != null)
                foreach (Vector3 other in reserved)
                    nearest = Mathf.Min(nearest, HorizontalDistanceSquared(point, other));
            return nearest;
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
            public readonly int Zone;
            public readonly int Order;

            public Candidate(int x, int z, int zone, int order)
            {
                X = x;
                Z = z;
                Zone = zone;
                Order = order;
            }
        }
    }
}

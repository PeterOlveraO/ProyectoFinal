using System.Collections.Generic;
using System;
using Game.Terrain;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>
    /// Planificador determinista para puestos temporales del mundo. Los puntos se distribuyen
    /// por las tres zonas, solo en bloques llanos conectados al spawn y libres de agua/colisiones.
    /// También puede usarse para puestos de armas u otros servicios de la partida.
    /// </summary>
    public static class WorldShopPlacement
    {
        const float SpawnClearance = 7f;
        const float CollisionRadius = 0.48f;
        const float CollisionHeight = 0.72f;

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
                if (delta.sqrMagnitude < SpawnClearance * SpawnClearance || terrain.IsLakeAtWorld(point))
                    continue;

                if (IsReserved(point, reserved) || HasBlockingCollider(point))
                    continue;

                candidates.Add(new Candidate(x, z, map.GetZone(x, z),
                    Hash(x, z, terrain.Settings.seed + seedOffset)));
            }

            candidates.Sort((a, b) => a.Order.CompareTo(b.Order));
            var positions = new List<Vector3>(count);
            // Reparto objetivo: dos en zona 1, dos en zona 2 y uno en zona 3.
            int[] quota = BuildQuotas(count);
            for (int zone = 1; zone <= 3; zone++)
                SelectFromZone(terrain, candidates, zone, quota[zone - 1], reserved, positions);

            // Si una zona no ofrece suficientes superficies accesibles, usar otros claros del mapa.
            if (positions.Count < count)
                SelectFromAnyZone(terrain, candidates, count, reserved, positions);

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
            IList<Vector3> reserved, List<Vector3> selected)
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
            IList<Vector3> reserved, List<Vector3> selected)
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
                if (delta.sqrMagnitude < 10f * 10f) return true;
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
                if (HorizontalDistanceSquared(point, other) < 10f * 10f) return true;
            return false;
        }

        static bool HasBlockingCollider(Vector3 ground)
        {
            return Physics.CheckSphere(ground + Vector3.up * CollisionHeight, CollisionRadius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
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
            if (!map.Contains(startX, startZ) || terrain.IsLakeAtWorld(terrain.BlockTopCenter(startX, startZ)))
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
                    if (terrain.IsLakeAtWorld(point)) continue;
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

using System.Collections.Generic;
using Game.Terrain;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Spawner de una zona. Genera varios puntos de aparición repartidos por el anillo de la
    /// zona y va soltando lotes de enemigos respetando el cupo de vivos de esa zona:
    ///
    ///   Zona 1: 20 vivos, 2 cada ~3 s.
    ///   Zona 2: 40 vivos, 3 cada ~2 s.
    ///   Zona 3: 100 vivos, 6 cada ~1 s.
    ///
    /// Al empezar se llena el 60 % del cupo para que el mundo no aparezca vacío.
    /// Los valores llegan desde <see cref="TerrainSettings"/> a través de
    /// <see cref="ZoneSpawnBootstrap"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ZoneSpawnManager : MonoBehaviour
    {
        [SerializeField] int zone = 1;
        [SerializeField] int maxAlive = 10;
        [SerializeField] float spawnInterval = 5f;
        [SerializeField] int spawnBatch = 1;
        [SerializeField] float chaseSpeed = 2.5f;

        [Tooltip("Fracción del cupo que aparece de golpe al empezar, para que el mundo no esté vacío.")]
        [SerializeField, Range(0f, 1f)] float initialFill = 0.6f;

        readonly List<Vector3> _points = new List<Vector3>();

        TerrainSettings _settings;
        WorldTerrain _terrain;
        float _timer;
        int _alive;

        public int Zone => zone;
        public int Alive => _alive;
        public int SpawnPointCount => _points.Count;
        public IReadOnlyList<Vector3> SpawnPoints => _points;

        public void Configure(int zoneIndex, TerrainSettings settings, int maxAliveCount,
                              float interval, int batch, float enemySpeed)
        {
            zone = Mathf.Clamp(zoneIndex, 1, 3);
            _settings = settings;
            maxAlive = Mathf.Max(1, maxAliveCount);
            spawnInterval = Mathf.Max(0.1f, interval);
            spawnBatch = Mathf.Max(1, batch);
            chaseSpeed = Mathf.Max(0.05f, enemySpeed);
        }

        void Start()
        {
            _terrain = WorldTerrain.Instance;
            if (_terrain == null)
            {
                Debug.LogError("[ZoneSpawnManager] No hay WorldTerrain en la escena.", this);
                enabled = false;
                return;
            }

            if (_settings == null) _settings = _terrain.Settings;
            BuildSpawnPoints();
            _timer = 0f;
            Prefill();
        }

        void Update()
        {
            if (_terrain == null) _terrain = WorldTerrain.Instance;
            if (_terrain == null) return;
            if (_settings == null) _settings = _terrain.Settings;

            if (_points.Count == 0)
            {
                // Tras una recarga de dominio los campos no serializados se pierden;
                // se reconstruyen desde los spawners que ya existen en la escena.
                BuildSpawnPoints();
                if (_points.Count == 0) return;
            }

            _timer += Time.deltaTime;
            if (_timer < spawnInterval) return;

            if (_alive >= maxAlive)
            {
                // Cupo lleno: no acumular lotes pendientes.
                _timer = spawnInterval;
                return;
            }

            _timer -= spawnInterval;
            SpawnBatch();
        }

        public void NotifyDestroyed()
        {
            _alive = Mathf.Max(0, _alive - 1);
        }

        /// <summary>Llena parte del cupo nada más empezar para que la zona no se vea vacía.</summary>
        void Prefill()
        {
            if (_points.Count == 0 || initialFill <= 0f) return;

            int target = Mathf.Clamp(Mathf.CeilToInt(maxAlive * initialFill), 0, maxAlive);
            while (_alive < target) SpawnBatch();
        }

        void SpawnBatch()
        {
            int free = maxAlive - _alive;
            int count = Mathf.Min(spawnBatch, free);

            for (int i = 0; i < count; i++)
            {
                var go = EnemyTriangleFactory.Create(zone);
                go.transform.SetParent(transform, true);
                go.transform.position = PickSpawnPosition();

                var enemy = go.GetComponent<EnemyTriangle>();
                if (enemy != null) enemy.Initialize(this, zone, chaseSpeed);

                _alive++;
            }
        }

        Vector3 PickSpawnPosition()
        {
            Vector3 basePoint = _points[Random.Range(0, _points.Count)];
            Vector3 candidate = basePoint + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));

            return TryGroundPosition(candidate, out var ground) ? ground : basePoint;
        }

        bool TryGroundPosition(Vector3 worldPosition, out Vector3 ground)
        {
            ground = worldPosition;

            var map = _terrain.Map;
            if (map == null) return false;

            Vector3 local = worldPosition - _terrain.transform.position;
            int x = Mathf.FloorToInt(local.x / _terrain.BlockSize);
            int z = Mathf.FloorToInt(local.z / _terrain.BlockSize);

            if (!map.Contains(x, z)) return false;
            if (map.GetZone(x, z) != zone) return false;
            if (_terrain.IsLakeAtWorld(worldPosition)) return false;

            ground = _terrain.BlockTopCenter(x, z);
            return true;
        }

        // ------------------------------------------------------ Puntos de spawn

        void BuildSpawnPoints()
        {
            _points.Clear();

            // Reutiliza los spawners ya creados (por si el dominio se recargó).
            var existing = GetComponentsInChildren<SpawnPointMarker>(true);
            foreach (var marker in existing)
            {
                if (marker.Zone == zone)
                    _points.Add(marker.transform.position);
            }
            if (_points.Count > 0) return;

            var map = _terrain != null ? _terrain.Map : null;
            if (map == null) return;

            float halfX = map.Width * 0.5f;
            float halfZ = map.Depth * 0.5f;
            float worldRadius = Mathf.Min(halfX, halfZ);
            float zone1Radius = _settings.zone1Radius;
            float zone2Radius = _settings.zone2Radius;

            float[] radii;
            int perRing;
            float angleOffset;

            if (zone == 1)
            {
                radii = new[] { zone1Radius * 0.45f, zone1Radius * 0.80f };
                perRing = 3;
                angleOffset = 30f;
            }
            else if (zone == 2)
            {
                float width = Mathf.Max(1f, zone2Radius - zone1Radius);
                radii = new[] { zone1Radius + width * 0.32f, zone1Radius + width * 0.74f };
                perRing = 4;
                angleOffset = 15f;
            }
            else
            {
                float width = Mathf.Max(2f, worldRadius - zone2Radius);
                radii = new[] { zone2Radius + width * 0.35f, zone2Radius + width * 0.75f };
                perRing = 5;
                angleOffset = 8f;
            }

            int index = 0;

            for (int ring = 0; ring < radii.Length; ring++)
            {
                for (int i = 0; i < perRing; i++)
                {
                    float angle = (angleOffset + 360f * i / perRing + ring * 180f / perRing) * Mathf.Deg2Rad;
                    int x = Mathf.RoundToInt(halfX + Mathf.Cos(angle) * radii[ring]);
                    int z = Mathf.RoundToInt(halfZ + Mathf.Sin(angle) * radii[ring]);

                    if (!map.Contains(x, z)) continue;
                    if (map.GetZone(x, z) != zone) continue;
                    if (IsNearLake(map, x, z)) continue;

                    CreatePoint(_terrain.BlockTopCenter(x, z), index++);
                }
            }

            if (_points.Count > 0) return;

            // Reserva: primer bloque válido de la zona (no debería hacer falta).
            for (int z = 1; z < map.Depth - 1; z++)
            {
                for (int x = 1; x < map.Width - 1; x++)
                {
                    if (map.GetZone(x, z) != zone || IsNearLake(map, x, z)) continue;
                    CreatePoint(_terrain.BlockTopCenter(x, z), 0);
                    return;
                }
            }
        }

        void CreatePoint(Vector3 position, int index)
        {
            var go = new GameObject("Spawner Z" + zone + "-" + index.ToString("00"));
            go.transform.SetParent(transform, true);
            go.transform.position = position;

            var marker = go.AddComponent<SpawnPointMarker>();
            marker.Zone = zone;

            _points.Add(position);
        }

        static bool IsNearLake(HeightMap map, int x, int z)
        {
            foreach (var lake in map.Lakes)
            {
                float dx = x - lake.Center.x;
                float dz = z - lake.Center.y;
                float radius = lake.Radius + 2f;
                if (dx * dx + dz * dz <= radius * radius) return true;
            }
            return false;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = SpawnPointMarker.ZoneColor(zone);
            foreach (var point in _points)
                Gizmos.DrawWireSphere(point + Vector3.up * 0.5f, 0.4f);
        }
    }
}

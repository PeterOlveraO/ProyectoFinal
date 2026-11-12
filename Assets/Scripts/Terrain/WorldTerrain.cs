using UnityEngine;

namespace Game.Terrain
{
    /// <summary>
    /// Mundo finito de chunksX × chunksZ chunks dividido en tres zonas concéntricas
    /// (ver <see cref="HeightMapGenerator"/>).
    ///
    /// Al iniciar genera el mapa de niveles y TODOS los chunks (malla + MeshCollider).
    /// Después solo activa/desactiva: quedan activos los chunks a distancia de Chebyshev
    /// ≤ loadRadius del chunk del jugador (un cuadrado, no un círculo). El recálculo ocurre
    /// únicamente cuando el jugador cambia de chunk. loadRadius = 0 deja todo activo.
    /// También crea el agua y los volúmenes de los lagos.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class WorldTerrain : MonoBehaviour
    {
        public static WorldTerrain Instance { get; private set; }

        [SerializeField] TerrainSettings settings;

        [Tooltip("Centro de la carga de chunks.")]
        [SerializeField] Transform player;

        HeightMap _map;
        GameObject[] _chunks = new GameObject[0];
        Mesh[] _meshes = new Mesh[0];
        Material[] _materials;
        Material _waterMaterial;
        Vector2Int _playerChunk;
        bool _hasPlayerChunk;
        int _activeCount;

        public TerrainSettings Settings => settings;
        public HeightMap Map => _map;
        public int ChunkCount => _chunks.Length;
        public int ActiveChunkCount => _activeCount;
        public float ChunkWorldSize => settings.chunkSize * settings.blockSize;
        public float BlockSize => settings.blockSize;
        public float StepHeight => settings.stepHeight;

        public Transform Player
        {
            get => player;
            set
            {
                player = value;
                _hasPlayerChunk = false;
            }
        }

        /// <summary>Centro de la tapa del bloque central del mundo (coordenadas de mundo).</summary>
        public Vector3 SpawnPosition => BlockTopCenter(_map.Width / 2, _map.Depth / 2);

        void Awake()
        {
            Instance = this;

            if (settings == null)
            {
                Debug.LogError("[WorldTerrain] Falta TerrainSettings.", this);
                enabled = false;
                return;
            }

            Generate();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;

            foreach (var mesh in _meshes)
                if (mesh != null) Destroy(mesh);
            TerrainMaterials.Destroy(_materials);
            if (_waterMaterial != null) Destroy(_waterMaterial);
        }

        void Update()
        {
            if (settings.loadRadius <= 0)
            {
                if (_activeCount != _chunks.Length) SetAllActive();
                return;
            }

            if (player == null) return;

            Vector2Int current = WorldToChunk(player.position);
            if (_hasPlayerChunk && current == _playerChunk) return;

            _playerChunk = current;
            _hasPlayerChunk = true;
            RefreshActiveChunks();
        }

        // ------------------------------------------------------------------ API

        public Vector2Int WorldToChunk(Vector3 worldPosition)
        {
            Vector3 local = worldPosition - transform.position;
            float size = ChunkWorldSize;
            return new Vector2Int(Mathf.FloorToInt(local.x / size), Mathf.FloorToInt(local.z / size));
        }

        public Vector3 BlockTopCenter(int x, int z)
        {
            float y = _map.Contains(x, z) ? _map.Get(x, z) * settings.stepHeight : 0f;
            return transform.position + new Vector3((x + 0.5f) * settings.blockSize, y, (z + 0.5f) * settings.blockSize);
        }

        /// <summary>Zona (1..3) del bloque que contiene la posición de mundo.</summary>
        public int ZoneAtWorld(Vector3 worldPosition)
        {
            Vector3 local = worldPosition - transform.position;
            int x = Mathf.FloorToInt(local.x / settings.blockSize);
            int z = Mathf.FloorToInt(local.z / settings.blockSize);
            return _map.Contains(x, z) ? _map.GetZone(x, z) : 0;
        }

        /// <summary>True si la posición está sobre el agua de un lago.</summary>
        public bool IsLakeAtWorld(Vector3 worldPosition)
        {
            if (_map == null) return false;

            Vector3 local = worldPosition - transform.position;
            float bx = local.x / settings.blockSize;
            float bz = local.z / settings.blockSize;

            foreach (var lake in _map.Lakes)
            {
                float dx = bx - (lake.Center.x + 0.5f);
                float dz = bz - (lake.Center.y + 0.5f);
                float radius = lake.Radius + 0.5f;
                if (dx * dx + dz * dz <= radius * radius) return true;
            }
            return false;
        }

        public GameObject GetChunk(int chunkX, int chunkZ)
        {
            if (chunkX < 0 || chunkZ < 0 || chunkX >= settings.chunksX || chunkZ >= settings.chunksZ) return null;
            return _chunks[chunkZ * settings.chunksX + chunkX];
        }

        // ---------------------------------------------------------- Generación

        void Generate()
        {
            _map = HeightMapGenerator.Generate(settings);
            _materials = TerrainMaterials.Create(settings);

            int count = settings.chunksX * settings.chunksZ;
            _chunks = new GameObject[count];
            _meshes = new Mesh[count];

            var data = new ChunkMeshData();
            float size = ChunkWorldSize;

            for (int cz = 0; cz < settings.chunksZ; cz++)
            {
                for (int cx = 0; cx < settings.chunksX; cx++)
                {
                    ChunkMeshBuilder.Build(_map, cx, cz, settings.chunkSize, settings.blockSize,
                                           settings.stepHeight, data);

                    var mesh = new Mesh { name = "Chunk_" + cx + "_" + cz };
                    ChunkMeshBuilder.Apply(data, mesh);

                    var go = new GameObject(mesh.name);
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(cx * size, 0f, cz * size);

                    // Sin static batching: cada chunk conserva su propia malla y sus bounds,
                    // imprescindibles para poder descartar por frustum chunk a chunk.

                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var chunkRenderer = go.AddComponent<MeshRenderer>();
                    chunkRenderer.sharedMaterials = _materials;
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;

                    // La carga por radio ya limita los chunks. No apagar su renderer con un
                    // segundo frustum: en iOS la cámara puede actualizarse después del culler.

                    int index = cz * settings.chunksX + cx;
                    _chunks[index] = go;
                    _meshes[index] = mesh;
                }
            }

            CreateLakes();

            if (GetComponent<VisibilityCuller>() == null) gameObject.AddComponent<VisibilityCuller>();

            _activeCount = count;
            _hasPlayerChunk = false;
        }

        /// <summary>Agua y volumen de respawn de cada lago generado por el mapa.</summary>
        void CreateLakes()
        {
            if (_map.Lakes.Count == 0) return;

            _waterMaterial = TerrainMaterials.CreateWaterMaterial(settings);

            var root = new GameObject("Lakes");
            root.transform.SetParent(transform, false);

            for (int i = 0; i < _map.Lakes.Count; i++)
            {
                var lake = _map.Lakes[i];
                float diameter = lake.Radius * 2f * settings.blockSize;
                float rimY = lake.RimLevel * settings.stepHeight;
                float bottomY = lake.BottomLevel * settings.stepHeight;
                var center = transform.position + new Vector3(
                    (lake.Center.x + 0.5f) * settings.blockSize, 0f, (lake.Center.y + 0.5f) * settings.blockSize);

                // Lámina de agua, justo por debajo del borde.
                var water = GameObject.CreatePrimitive(PrimitiveType.Cube);
                water.name = "Lake_" + i + "_Water";
                water.transform.SetParent(root.transform, true);
                water.transform.position = center + Vector3.up * (rimY - settings.stepHeight * 0.25f);
                water.transform.localScale = new Vector3(diameter, settings.stepHeight * 0.15f, diameter);
                var waterCollider = water.GetComponent<Collider>();
                if (waterCollider != null) Destroy(waterCollider);
                water.GetComponent<MeshRenderer>().sharedMaterial = _waterMaterial;

                // Volumen que devuelve al jugador a un punto seguro si cae al lago.
                var triggerGo = new GameObject("Lake_" + i + "_Trigger");
                triggerGo.transform.SetParent(root.transform, true);
                triggerGo.transform.position = center + Vector3.up * ((rimY + bottomY) * 0.5f);

                var box = triggerGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(diameter * 0.8f, Mathf.Max(0.1f, rimY - bottomY), diameter * 0.8f);

                var body = triggerGo.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                triggerGo.AddComponent<LakeVolume>();
            }
        }

        // ---------------------------------------------------------------- Carga

        void RefreshActiveChunks()
        {
            int radius = settings.loadRadius;
            _activeCount = 0;

            for (int cz = 0; cz < settings.chunksZ; cz++)
            {
                for (int cx = 0; cx < settings.chunksX; cx++)
                {
                    int distance = Mathf.Max(Mathf.Abs(cx - _playerChunk.x), Mathf.Abs(cz - _playerChunk.y));
                    bool active = distance <= radius;

                    var go = _chunks[cz * settings.chunksX + cx];
                    if (go.activeSelf != active) go.SetActive(active);
                    if (active) _activeCount++;
                }
            }
        }

        void SetAllActive()
        {
            foreach (var go in _chunks)
                if (!go.activeSelf) go.SetActive(true);
            _activeCount = _chunks.Length;
            _hasPlayerChunk = false;
        }

        void OnDrawGizmos()
        {
            if (settings == null) return;

            float size = settings.chunkSize * settings.blockSize;
            var extent = new Vector3(settings.chunksX * size, 0f, settings.chunksZ * size);
            Gizmos.color = new Color(0.25f, 0.75f, 1f, 1f);
            Gizmos.DrawWireCube(transform.position + extent * 0.5f, extent);

            if (!Application.isPlaying || !_hasPlayerChunk || settings.loadRadius <= 0) return;

            // Área cargada: cuadrado de (2r+1) × (2r+1) chunks.
            int r = settings.loadRadius;
            var min = new Vector3((_playerChunk.x - r) * size, 0f, (_playerChunk.y - r) * size);
            var side = new Vector3((2 * r + 1) * size, 0f, (2 * r + 1) * size);
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 1f);
            Gizmos.DrawWireCube(transform.position + min + side * 0.5f, side);
        }
    }
}

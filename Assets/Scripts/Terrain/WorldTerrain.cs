using UnityEngine;

namespace Game.Terrain
{
    /// <summary>
    /// Mundo finito de chunksX × chunksZ chunks.
    ///
    /// Al iniciar genera el mapa de niveles y TODOS los chunks (malla + MeshCollider).
    /// Después solo activa/desactiva: quedan activos los chunks a distancia de Chebyshev
    /// ≤ loadRadius del chunk del jugador (un cuadrado, no un círculo). El recálculo ocurre
    /// únicamente cuando el jugador cambia de chunk. loadRadius = 0 deja todo activo.
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
        Vector2Int _playerChunk;
        bool _hasPlayerChunk;
        int _activeCount;

        public TerrainSettings Settings => settings;
        public HeightMap Map => _map;
        public int ChunkCount => _chunks.Length;
        public int ActiveChunkCount => _activeCount;
        public float ChunkWorldSize => settings.chunkSize * settings.blockSize;

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

        public GameObject GetChunk(int chunkX, int chunkZ)
        {
            if (chunkX < 0 || chunkZ < 0 || chunkX >= settings.chunksX || chunkZ >= settings.chunksZ) return null;
            return _chunks[chunkZ * settings.chunksX + chunkX];
        }

        // ---------------------------------------------------------- Generación

        void Generate()
        {
            _map = HeightMapGenerator.Generate(settings);
            _materials = TerrainMaterials.Create(settings.maxLevel);

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
                                           settings.stepHeight, settings.maxLevel, data);

                    var mesh = new Mesh { name = "Chunk_" + cx + "_" + cz };
                    ChunkMeshBuilder.Apply(data, mesh);

                    var go = new GameObject(mesh.name);
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(cx * size, 0f, cz * size);
                    go.isStatic = true;

                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials = _materials;
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;

                    int index = cz * settings.chunksX + cx;
                    _chunks[index] = go;
                    _meshes[index] = mesh;
                }
            }

            _activeCount = count;
            _hasPlayerChunk = false;
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

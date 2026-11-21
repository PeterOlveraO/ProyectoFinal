using System.Collections.Generic;
using UnityEngine;

namespace Game.Terrain
{
    /// <summary>Decorative dirt lanes painted only onto broad, dry, level ground.</summary>
    [DisallowMultipleComponent]
    public sealed class CardinalPathOverlay : MonoBehaviour
    {
        Mesh _mesh;
        Material _material;
        Texture2D _texture;
        Mesh _hubMesh;
        Material[] _hubMaterials;

        public static void Create(WorldTerrain terrain)
        {
            if (terrain == null || terrain.Map == null || terrain.Settings == null) return;
            var root = new GameObject("CardinalPaths");
            root.transform.SetParent(terrain.transform, false);
            root.AddComponent<CardinalPathOverlay>().Build(terrain);
        }

        void Build(WorldTerrain terrain)
        {
            var map = terrain.Map;
            float block = terrain.BlockSize;
            int cx = map.Width / 2, cz = map.Depth / 2;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var eligible = new HashSet<Vector2Int>();

            // A quiet crossroads and a ring connect the nine hub foundations.
            // A complete 3x3 plateau is required; lake cells and broken/stepped ground
            // are omitted, so every overlay quad rests directly on existing top geometry.
            for (int z = 2; z < map.Depth - 2; z++)
            for (int x = 2; x < map.Width - 2; x++)
            {
                int dx = x - cx, dz = z - cz;
                int radiusSquared = dx * dx + dz * dz;
                bool onLane =
                    (Mathf.Abs(dx) <= 76 && Mathf.Abs(dz) <= 1) ||
                    (Mathf.Abs(dz) <= 76 && Mathf.Abs(dx) <= 1) ||
                    (radiusSquared >= 64 && radiusSquared <= 100);
                if (!onLane || terrain.IsLakeAtWorld(terrain.BlockTopCenter(x, z))) continue;

                int level = map.Get(x, z);
                bool flat = true;
                for (int nz = z - 1; nz <= z + 1 && flat; nz++)
                for (int nx = x - 1; nx <= x + 1; nx++)
                    if (!map.Contains(nx, nz) || map.Get(nx, nz) != level ||
                        terrain.IsLakeAtWorld(terrain.BlockTopCenter(nx, nz)))
                    {
                        flat = false;
                        break;
                    }
                if (!flat) continue;
                eligible.Add(new Vector2Int(x, z));
            }

            // Keep only the network connected to the hub: no isolated dirt tiles on hills.
            var connected = new HashSet<Vector2Int>();
            var pending = new Queue<Vector2Int>();
            var origin = new Vector2Int(cx, cz);
            if (eligible.Contains(origin)) { pending.Enqueue(origin); connected.Add(origin); }
            var directions = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            while (pending.Count > 0)
            {
                Vector2Int cell = pending.Dequeue();
                foreach (var direction in directions)
                {
                    Vector2Int next = cell + direction;
                    if (eligible.Contains(next) && connected.Add(next)) pending.Enqueue(next);
                }
            }
            foreach (Vector2Int cell in connected)
            {
                int x = cell.x, z = cell.y, level = map.Get(cell.x, cell.y);
                float x0 = x * block, x1 = (x + 1) * block;
                float z0 = z * block, z1 = (z + 1) * block;
                float y = level * terrain.Settings.stepHeight + .018f;
                int start = vertices.Count;
                vertices.Add(new Vector3(x0, y, z0));
                vertices.Add(new Vector3(x1, y, z0));
                vertices.Add(new Vector3(x1, y, z1));
                vertices.Add(new Vector3(x0, y, z1));
                for (int i = 0; i < 4; i++) normals.Add(Vector3.up);
                uvs.Add(new Vector2(x0 / block, z0 / block));
                uvs.Add(new Vector2(x1 / block, z0 / block));
                uvs.Add(new Vector2(x1 / block, z1 / block));
                uvs.Add(new Vector2(x0 / block, z1 / block));
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start);
                triangles.Add(start + 3);
                triangles.Add(start + 2);
            }

            if (vertices.Count == 0)
            {
                BuildHubDetails(terrain);
                enabled = false;
                return;
            }

            _mesh = new Mesh { name = "Cardinal_Dirt_Path" };
            _mesh.indexFormat = vertices.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            _mesh.SetVertices(vertices);
            _mesh.SetNormals(normals);
            _mesh.SetUVs(0, uvs);
            _mesh.SetTriangles(triangles, 0, false);
            _mesh.RecalculateBounds();

            _texture = CreateDirtTexture();
            _material = new Material(terrain.Settings.terrainMaterial)
            {
                name = "Terrain_CardinalPath",
                color = Color.white,
                mainTexture = _texture
            };
            if (_material.HasProperty("_Smoothness")) _material.SetFloat("_Smoothness", 0f);
            if (_material.HasProperty("_Glossiness")) _material.SetFloat("_Glossiness", 0f);
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            BuildHubDetails(terrain);
        }

        void BuildHubDetails(WorldTerrain terrain)
        {
            HeightMap map = terrain.Map;
            float block = terrain.BlockSize;
            Vector3 spawn = terrain.SpawnPosition - terrain.transform.position;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new[] { new List<int>(), new List<int>() };

            // Tiny, non-colliding tufts sit in the nine arcs between the shop spokes.
            // Keep them well off the spawn point and the radius-9 shop pads.
            for (int slot = 0; slot < 9; slot++)
            {
                float angle = (slot + .5f) * Mathf.PI * 2f / 9f;
                float radius = slot % 2 == 0 ? 5.1f : 6.1f;
                float wx = spawn.x + Mathf.Sin(angle) * radius;
                float wz = spawn.z + Mathf.Cos(angle) * radius;
                int x = Mathf.FloorToInt(wx / block);
                int z = Mathf.FloorToInt(wz / block);
                if (x < 1 || z < 1 || x >= map.Width - 1 || z >= map.Depth - 1) continue;
                int level = map.Get(x, z);
                bool flat = true;
                for (int nz = z - 1; nz <= z + 1 && flat; nz++)
                for (int nx = x - 1; nx <= x + 1; nx++)
                    if (map.Get(nx, nz) != level || terrain.IsLakeAtWorld(terrain.BlockTopCenter(nx, nz)))
                    {
                        flat = false;
                        break;
                    }
                if (!flat) continue;

                float y = level * terrain.Settings.stepHeight + .025f;
                for (int tuft = 0; tuft < 3; tuft++)
                {
                    float tx = wx + tuft * .18f, tz = wz + (tuft % 2) * .25f;
                    AddBlade(vertices, normals, triangles[0], tx, tz, y, .1f, .28f, 0f);
                    AddBlade(vertices, normals, triangles[0], tx, tz, y, .09f, .25f, Mathf.PI * .5f);
                    AddFlower(vertices, normals, triangles[1], tx, tz, y + .22f, .1f);
                }
            }
            if (vertices.Count == 0) return;

            _hubMesh = new Mesh { name = "Hub_Arc_GroundFlora" };
            _hubMesh.SetVertices(vertices);
            _hubMesh.SetNormals(normals);
            _hubMesh.subMeshCount = 2;
            _hubMesh.SetTriangles(triangles[0], 0, false);
            _hubMesh.SetTriangles(triangles[1], 1, false);
            _hubMesh.RecalculateBounds();
            _hubMaterials = new[]
            {
                new Material(terrain.Settings.terrainMaterial) { name = "Hub_Tuft_Green", color = new Color(.38f, .62f, .34f) },
                new Material(terrain.Settings.terrainMaterial) { name = "Hub_Tuft_Cream", color = new Color(.96f, .83f, .48f) }
            };
            foreach (var material in _hubMaterials)
            {
                material.mainTexture = null;
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            }
            var detail = new GameObject("HubArc_GroundFlora");
            detail.transform.SetParent(transform, false);
            detail.AddComponent<MeshFilter>().sharedMesh = _hubMesh;
            var detailRenderer = detail.AddComponent<MeshRenderer>();
            detailRenderer.sharedMaterials = _hubMaterials;
            detailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            detailRenderer.receiveShadows = false;
        }

        static void AddBlade(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            float x, float z, float y, float width, float height, float angle)
        {
            Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * width;
            int start = vertices.Count;
            vertices.Add(new Vector3(x - side.x, y, z - side.z));
            vertices.Add(new Vector3(x + side.x, y, z + side.z));
            vertices.Add(new Vector3(x + side.x * .35f, y + height, z + side.z * .35f));
            vertices.Add(new Vector3(x - side.x * .35f, y + height, z - side.z * .35f));
            Vector3 normal = new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle));
            for (int i = 0; i < 4; i++) normals.Add(normal);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }

        static void AddFlower(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            float x, float z, float y, float radius)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(x - radius, y, z));
            vertices.Add(new Vector3(x, y, z - radius));
            vertices.Add(new Vector3(x + radius, y, z));
            vertices.Add(new Vector3(x, y, z + radius));
            for (int i = 0; i < 4; i++) normals.Add(Vector3.up);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }

        static Texture2D CreateDirtTexture()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Terrain_DirtPathPixels",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                uint hash = unchecked((uint)(x * 374761393 + y * 668265263));
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                float shade = hash % 31u == 0u ? .88f : hash % 19u == 0u ? 1.06f : 1f;
                pixels[y * size + x] = new Color(.63f * shade, .54f * shade, .39f * shade, 1f);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
            if (_texture != null) Destroy(_texture);
            if (_hubMesh != null) Destroy(_hubMesh);
            if (_hubMaterials != null)
                foreach (var material in _hubMaterials)
                    if (material != null) Destroy(material);
        }
    }
}

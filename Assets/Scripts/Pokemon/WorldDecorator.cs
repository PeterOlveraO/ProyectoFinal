using System;
using System.Collections.Generic;
using Game.Rendering;
using Game.Terrain;
using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>Decoración determinista con huellas físicas independientes del billboard.</summary>
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class WorldDecorator : MonoBehaviour
    {
        [Header("Cantidades máximas")]
        // Nuevos campos: las cantidades antiguas de la escena no limitan la nueva flora.
        [SerializeField, Min(0)] int canopyTrees = 150;
        [SerializeField, Min(0)] int shrubs = 80;
        [SerializeField, Min(0)] int groundPlants = 60;
        [SerializeField, Min(15f)] float spawnClearRadius = 15f;

        enum Kind { Tree, Shrub, Plant }
        struct Profile
        {
            public string Path;
            public Kind Kind;
            public float Height, Width, Footprint, ColliderHeight;
            // El billboard puede girar: reservar también su semiancho, no solo el tronco.
            public float Clearance => Mathf.Max(Footprint, Width * .5f);
            public Profile(string path, Kind kind, float height, float width, float footprint, float colliderHeight)
            {
                Path = path; Kind = kind; Height = height; Width = width;
                Footprint = footprint; ColliderHeight = colliderHeight;
            }
        }

        struct Occupied
        {
            public Vector3 Position;
            public float Radius;
            public Occupied(Vector3 position, float radius) { Position = position; Radius = radius; }
        }

        readonly List<Occupied> _placed = new List<Occupied>();
        System.Random _random;

        void Start()
        {
            var terrain = WorldTerrain.Instance;
            if (terrain == null || terrain.Map == null || terrain.Settings == null)
            {
                Debug.LogError("[WorldDecorator] No hay WorldTerrain generado en la escena.", this);
                return;
            }

            _random = new System.Random(unchecked(terrain.Settings.seed * 7919 + 101));
            _placed.Clear();
            var root = new GameObject("Decor").transform;
            root.SetParent(transform, false);

            // El lago puede ocupar casi toda la zona central: buscar también en los anillos exteriores.
            PlaceGroup(terrain, root, CatalogTrees(), canopyTrees, 12f, 76f);
            PlaceGroup(terrain, root, CatalogShrubs(), shrubs, 12f, 76f);
            PlaceGroup(terrain, root, CatalogPlants(), groundPlants, 12f, 76f);
            Debug.Log($"[WorldDecorator] Colocados {_placed.Count} elementos; cantidades configuradas son máximos.");
        }

        static void Add(List<Profile> list, string folder, string prefix, int[] ids, Kind kind,
                        float height, float width, float footprint, float colliderHeight)
        {
            foreach (int id in ids)
                list.Add(new Profile("Decor/" + folder + "/" + prefix + "_" + id.ToString("00"),
                                     kind, height, width, footprint, colliderHeight));
        }

        static List<Profile> CatalogTrees()
        {
            var p = new List<Profile>();
            // Inspección de PNG: árboles verdes completos; 23 está vacío, 20-22 son otoñales,
            // 40-43 son arbustos. Props contiene recortes/carteles incluso junto a algunos árboles.
            Add(p, "Trees", "tree", new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
                16, 17, 18, 19, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39 },
                Kind.Tree, 2.6f, 2.8f, .22f, 1.3f);
            return p;
        }

        static List<Profile> CatalogShrubs()
        {
            var p = new List<Profile>();
            Add(p, "Trees", "tree", new[] { 40, 41, 42, 43 }, Kind.Shrub, 1f, 1.1f, .32f, .75f);
            return p;
        }

        static List<Profile> CatalogPlants()
        {
            var p = new List<Profile>();
            // Follaje redondeado sin tronco visible, reutilizado a escala de planta baja.
            // Transitable: no crea pequeños obstáculos invisibles bajo el jugador.
            Add(p, "Trees", "tree", new[] { 12, 13, 14, 15 }, Kind.Plant, .45f, .6f, .2f, 0f);
            return p;
        }

        void PlaceGroup(WorldTerrain terrain, Transform root, List<Profile> profiles, int count, float minRadius, float maxRadius)
        {
            for (int i = 0; i < count; i++)
            {
                Profile profile = profiles[_random.Next(profiles.Count)];
                Sprite sprite = Resources.Load<Sprite>(profile.Path);
                if (sprite == null)
                {
                    Debug.LogWarning("[WorldDecorator] Sprite no encontrado: " + profile.Path);
                    continue;
                }
                if (TryFindSpot(terrain, profile, minRadius, maxRadius, out Vector3 position))
                    Spawn(terrain, root, sprite, profile, position);
            }
        }

        void Spawn(WorldTerrain terrain, Transform root, Sprite sprite, Profile profile, Vector3 position)
        {
            var go = new GameObject("Decor " + sprite.name);
            var coord = terrain.WorldToChunk(position);
            var chunk = terrain.GetChunk(coord.x, coord.y);
            go.transform.SetParent(chunk != null ? chunk.transform : root, true);
            go.transform.position = position;
            AddFloraCollider(go, profile);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            float spriteHeight = sprite.rect.height / sprite.pixelsPerUnit;
            float spriteWidth = sprite.rect.width / sprite.pixelsPerUnit;
            float scale = Mathf.Min(profile.Height / spriteHeight, profile.Width / spriteWidth);
            visual.transform.localScale = Vector3.one * scale;
            // Independiente del pivot de importación: el borde inferior toca el suelo.
            visual.transform.localPosition = Vector3.up * (sprite.pivot.y / sprite.pixelsPerUnit * scale);
            visual.AddComponent<BillboardSprite>();
        }

        static void AddFloraCollider(GameObject go, Profile profile)
        {
            if (profile.Kind == Kind.Plant) return;
            // Cápsula vertical: tronco/arbusto redondeado, sin esquinas ni copa sólida.
            // Está en la raíz y no gira con el billboard.
            var collider = go.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = profile.Footprint;
            collider.height = profile.ColliderHeight;
            collider.center = Vector3.up * (profile.ColliderHeight * .5f);
        }

        static bool IsReserved(Vector3 offset, float clearance, float spawnRadius,
                               float rampWidth, float rampHalfAngle)
        {
            float clear = Mathf.Max(spawnRadius, 15f) + clearance;
            if (offset.x * offset.x + offset.z * offset.z < clear * clear) return true;
            float corridor = Mathf.Max(rampWidth * .5f,
                Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.z)) *
                Mathf.Tan(rampHalfAngle * Mathf.Deg2Rad));
            return Mathf.Min(Mathf.Abs(offset.x), Mathf.Abs(offset.z)) < corridor + clearance + 2f;
        }

        bool TryFindSpot(WorldTerrain terrain, Profile profile, float minRadius, float maxRadius, out Vector3 position)
        {
            var map = terrain.Map;
            Vector3 spawn = terrain.SpawnPosition;
            position = Vector3.zero;
            float block = terrain.BlockSize;
            for (int attempt = 0; attempt < 640; attempt++)
            {
                double angle = _random.NextDouble() * Math.PI * 2;
                float radius = Mathf.Lerp(minRadius, maxRadius, Mathf.Sqrt((float)_random.NextDouble()));
                int x = Mathf.RoundToInt(map.Width * .5f + (float)Math.Cos(angle) * radius);
                int z = Mathf.RoundToInt(map.Depth * .5f + (float)Math.Sin(angle) * radius);
                if (!map.Contains(x, z)) continue;
                Vector3 candidate = terrain.BlockTopCenter(x, z);
                // Reserva tycoon: disco de radio >=15 MÁS huella visual, y cuatro corredores.
                if (IsReserved(candidate - spawn, profile.Clearance, spawnClearRadius,
                    terrain.Settings.rampWidth * block, terrain.Settings.rampHalfAngle)) continue;
                if (!SuitableGround(terrain, x, z, profile)) continue;
                bool close = false;
                foreach (var other in _placed)
                {
                    float separation = profile.Clearance + other.Radius + 2.5f;
                    float ox = other.Position.x - candidate.x, oz = other.Position.z - candidate.z;
                    if (ox * ox + oz * oz < separation * separation) { close = true; break; }
                }
                if (close) continue;
                position = candidate;
                _placed.Add(new Occupied(candidate, profile.Clearance));
                return true;
            }
            return false;
        }

        static bool SuitableGround(WorldTerrain terrain, int x, int z, Profile profile)
        {
            var map = terrain.Map;
            int level = map.Get(x, z);
            // La raíz y su entorno deben ser planos; la copa no es una huella sólida.
            // Clearance (copa incluida) se aplica aparte a rutas y separación entre plantas.
            int cells = Mathf.CeilToInt((profile.Footprint + 2f) / terrain.BlockSize);
            for (int dz = -cells; dz <= cells; dz++)
                for (int dx = -cells; dx <= cells; dx++)
                {
                    int sx = x + dx, sz = z + dz;
                    if (!map.Contains(sx, sz) || map.Get(sx, sz) != level ||
                        terrain.IsLakeAtWorld(terrain.BlockTopCenter(sx, sz))) return false;
                }
            return true;
        }
    }
}

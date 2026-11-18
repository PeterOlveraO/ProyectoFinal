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
        [SerializeField] int buildings = 20;
        [SerializeField] int extras = 20;
        [SerializeField] int trees = 90;
        [SerializeField] int props = 34;
        [SerializeField, Min(0f)] float spawnClearRadius = 6f;

        enum Kind { Building, Tree, Object, Shore }
        struct Profile
        {
            public string Path;
            public Kind Kind;
            public float Height, Width, Footprint, ColliderHeight;
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
            PlaceGroup(terrain, root, CatalogBuildings(), buildings, 12f, 70f);
            PlaceGroup(terrain, root, CatalogExtras(), extras, 12f, 70f);
            PlaceGroup(terrain, root, CatalogTrees(), trees, 12f, 76f);
            PlaceGroup(terrain, root, CatalogProps(), props, 12f, 60f);
            Debug.Log($"[WorldDecorator] Colocados {_placed.Count} elementos; cantidades configuradas son máximos.");
        }

        static void Add(List<Profile> list, string folder, string prefix, int[] ids, Kind kind,
                        float height, float width, float footprint, float colliderHeight)
        {
            foreach (int id in ids)
                list.Add(new Profile("Decor/" + folder + "/" + prefix + "_" + id.ToString("00"),
                                     kind, height, width, footprint, colliderHeight));
        }

        static List<Profile> CatalogBuildings()
        {
            var p = new List<Profile>();
            Add(p, "Buildings", "building", new[] { 0, 1, 3, 4, 5, 6, 7, 9, 10, 12, 13, 14, 15, 16, 17, 18, 19 },
                Kind.Building, 4.5f, 5f, 1.8f, 2.6f);
            return p;
        }

        static List<Profile> CatalogExtras()
        {
            var p = new List<Profile>();
            Add(p, "Extras", "extra", new[] { 1, 2, 3, 6, 7, 8, 9, 10, 11 },
                Kind.Building, 3.5f, 4.5f, 1.5f, 2f);
            Add(p, "Extras", "extra", new[] { 12, 13, 14 }, Kind.Object, 2.5f, 2.5f, .75f, 1.5f);
            Add(p, "Extras", "extra", new[] { 4, 5 }, Kind.Shore, 2f, 4f, 1.3f, 1f);
            return p;
        }

        static List<Profile> CatalogTrees()
        {
            var p = new List<Profile>();
            Add(p, "Trees", "tree", new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43 },
                Kind.Tree, 2.6f, 2.8f, .4f, 1.5f);
            return p;
        }

        static List<Profile> CatalogProps()
        {
            var p = new List<Profile>();
            Add(p, "Props", "prop", new[] { 1, 5, 7, 8, 14, 16, 17 }, Kind.Tree, 3f, 3.2f, .5f, 1.6f);
            Add(p, "Props", "prop", new[] { 20, 31 }, Kind.Object, 2f, 2.4f, .8f, 1.3f);
            Add(p, "Props", "prop", new[] { 25, 27, 30, 33, 35, 37, 40, 43 },
                Kind.Building, 3.4f, 4.5f, 1.5f, 2f);
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
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(profile.Footprint * 2f, profile.ColliderHeight, profile.Footprint * 2f);
            collider.center = Vector3.up * (profile.ColliderHeight * .5f);

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

        bool TryFindSpot(WorldTerrain terrain, Profile profile, float minRadius, float maxRadius, out Vector3 position)
        {
            var map = terrain.Map;
            Vector3 spawn = terrain.SpawnPosition;
            position = Vector3.zero;
            float block = terrain.BlockSize;
            for (int attempt = 0; attempt < 160; attempt++)
            {
                double angle = _random.NextDouble() * Math.PI * 2;
                float radius = Mathf.Lerp(minRadius, maxRadius, Mathf.Sqrt((float)_random.NextDouble()));
                int x = Mathf.RoundToInt(map.Width * .5f + (float)Math.Cos(angle) * radius);
                int z = Mathf.RoundToInt(map.Depth * .5f + (float)Math.Sin(angle) * radius);
                if (!map.Contains(x, z)) continue;
                Vector3 candidate = terrain.BlockTopCenter(x, z);
                float dx = candidate.x - spawn.x, dz = candidate.z - spawn.z;
                // La zona de tiendas puede extenderse hasta 11.65 unidades desde spawn.
                float clear = Mathf.Max(spawnClearRadius, 15f) + profile.Footprint;
                if (dx * dx + dz * dz < clear * clear) continue;
                // Corredores cardinales, incluyendo rampas; se amplían con la distancia angular.
                float corridor = Mathf.Max(terrain.Settings.rampWidth * block * .5f,
                    Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) * Mathf.Tan(terrain.Settings.rampHalfAngle * Mathf.Deg2Rad));
                if (Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dz)) < corridor + profile.Footprint + 2f) continue;
                if (!SuitableGround(terrain, x, z, profile)) continue;
                bool close = false;
                foreach (var other in _placed)
                {
                    float separation = profile.Footprint + other.Radius + 2.5f;
                    float ox = other.Position.x - candidate.x, oz = other.Position.z - candidate.z;
                    if (ox * ox + oz * oz < separation * separation) { close = true; break; }
                }
                if (close) continue;
                position = candidate;
                _placed.Add(new Occupied(candidate, profile.Footprint));
                return true;
            }
            return false;
        }

        static bool SuitableGround(WorldTerrain terrain, int x, int z, Profile profile)
        {
            var map = terrain.Map;
            int level = map.Get(x, z);
            // Margen extra mantiene despejadas las orillas y evita bordes y pendientes.
            int cells = Mathf.CeilToInt((profile.Footprint + 2f) / terrain.BlockSize);
            for (int dz = -cells; dz <= cells; dz++)
                for (int dx = -cells; dx <= cells; dx++)
                {
                    int sx = x + dx, sz = z + dz;
                    if (!map.Contains(sx, sz) || map.Get(sx, sz) != level ||
                        terrain.IsLakeAtWorld(terrain.BlockTopCenter(sx, sz))) return false;
                }
            if (profile.Kind != Kind.Shore) return true;
            // Barcos varados en tierra cerca de agua, sin ocupar el acceso ni el agua.
            int outer = cells + Mathf.CeilToInt(5f / terrain.BlockSize);
            for (int dz = -outer; dz <= outer; dz++)
                for (int dx = -outer; dx <= outer; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) <= cells) continue;
                    int sx = x + dx, sz = z + dz;
                    if (map.Contains(sx, sz) && terrain.IsLakeAtWorld(terrain.BlockTopCenter(sx, sz)))
                        return true;
                }
            return false;
        }
    }
}

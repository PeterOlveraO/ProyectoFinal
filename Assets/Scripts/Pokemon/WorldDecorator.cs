using System.Collections.Generic;
using Game.Rendering;
using Game.Terrain;
using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>
    /// Decora el mundo con los sprites de <c>Assets/Imagenes</c>:
    ///
    ///   * Buildings -> edificios (buildings.png).
    ///   * Extras    -> barcos, estatuas y casas (extras.png).
    ///   * Trees     -> árboles y rocas (floor.png).
    ///   * Props     -> farolas, carteles, vallas... (tileset-pokemon_dawn.png).
    ///
    /// Todo se coloca en <see cref="Start"/>, cuando <see cref="WorldTerrain"/> ya generó el mapa.
    /// Se buscan bloques llanos, fuera de los lagos y lejos del punto de aparición del jugador.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class WorldDecorator : MonoBehaviour
    {
        [Header("Cantidades")]
        [SerializeField] int buildings = 20;
        [SerializeField] int extras = 20;
        [SerializeField] int trees = 90;
        [SerializeField] int props = 34;

        [Tooltip("Radio (bloques) alrededor del spawn del jugador que se deja despejado.")]
        [SerializeField, Min(0f)] float spawnClearRadius = 6f;

        readonly List<Vector3> _placed = new List<Vector3>(256);

        void Start()
        {
            var terrain = WorldTerrain.Instance;
            if (terrain == null || terrain.Map == null)
            {
                Debug.LogError("[WorldDecorator] No hay WorldTerrain generado en la escena.", this);
                return;
            }

            var buildingSprites = Load("Decor/Buildings");
            var extraSprites = Load("Decor/Extras");
            var treeSprites = Load("Decor/Trees");
            var propSprites = Load("Decor/Props");

            var root = new GameObject("Decor").transform;
            root.SetParent(transform, false);

            // Barajado determinista para que la decoración sea la misma en cada partida.
            Random.State previous = Random.state;
            Random.InitState(terrain.Settings.seed * 7919 + 101);
            _placed.Clear();

            PlaceGroup(terrain, root, buildingSprites, buildings, 9f, 34f, 3.0f, 5.0f, 7.5f, 4.5f, true, "Edificio");
            PlaceGroup(terrain, root, extraSprites, extras, 36f, 70f, 2.6f, 4.4f, 6.5f, 5f, true, "Extra");
            PlaceGroup(terrain, root, treeSprites, trees, 5f, 76f, 1.4f, 2.6f, 3.2f, 1.2f, false, "Arbol");
            PlaceGroup(terrain, root, propSprites, props, 6f, 56f, 1.2f, 3.0f, 4.0f, 2.0f, true, "Prop");

            Random.state = previous;

            Debug.Log($"[WorldDecorator] Colocados {_placed.Count} elementos: {buildings} edificios, " +
                      $"{extras} extras, {trees} árboles y {props} props.");
        }

        static Sprite[] Load(string path)
        {
            var sprites = Resources.LoadAll<Sprite>(path);
            if (sprites == null || sprites.Length == 0)
                Debug.LogWarning("[WorldDecorator] Sin sprites en Resources/" + path);
            return sprites;
        }

        void PlaceGroup(WorldTerrain terrain, Transform root, Sprite[] sprites, int count,
                        float minRadius, float maxRadius, float minHeight, float maxHeight,
                        float maxWidth, float separation, bool requireFlat, string prefix)
        {
            if (sprites == null || sprites.Length == 0 || count <= 0) return;

            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                var sprite = sprites[Random.Range(0, sprites.Length)];
                if (sprite == null) continue;
                if (!TryFindSpot(terrain, minRadius, maxRadius, separation, requireFlat, out Vector3 position))
                    continue;

                Spawn(terrain, root, sprite, position, Random.Range(minHeight, maxHeight), maxWidth, prefix);
                placed++;
            }

            if (placed < count)
                Debug.Log($"[WorldDecorator] {prefix}: {placed}/{count} (no quedaban huecos llanos).");
        }

        void Spawn(WorldTerrain terrain, Transform root, Sprite sprite, Vector3 position,
                   float targetHeight, float maxWidth, string prefix)
        {
            var go = new GameObject(prefix + " " + sprite.name);

            // Se cuelga del chunk del terreno: así la decoración aparece y desaparece
            // junto con el trozo de mundo que se carga alrededor del jugador.
            var chunk = terrain.GetChunk(terrain.WorldToChunk(position).x, terrain.WorldToChunk(position).y);
            go.transform.SetParent(chunk != null ? chunk.transform : root, true);
            go.transform.position = position;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;

            float spriteHeight = sprite.rect.height / sprite.pixelsPerUnit;
            float spriteWidth = sprite.rect.width / sprite.pixelsPerUnit;
            float scale = spriteHeight > 0.0001f ? targetHeight / spriteHeight : 1f;
            if (spriteWidth * scale > maxWidth && spriteWidth > 0.0001f)
                scale = maxWidth / spriteWidth;

            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.AddComponent<BillboardSprite>();
        }

        bool TryFindSpot(WorldTerrain terrain, float minRadius, float maxRadius, float separation,
                         bool requireFlat, out Vector3 position)
        {
            position = Vector3.zero;

            var map = terrain.Map;
            float centerX = map.Width * 0.5f;
            float centerZ = map.Depth * 0.5f;
            Vector3 spawn = terrain.SpawnPosition;

            for (int attempt = 0; attempt < 60; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float radius = Mathf.Lerp(minRadius, maxRadius, Mathf.Sqrt(Random.value));

                int x = Mathf.RoundToInt(centerX + Mathf.Cos(angle) * radius);
                int z = Mathf.RoundToInt(centerZ + Mathf.Sin(angle) * radius);

                if (!map.Contains(x, z)) continue;
                if (map.GetZone(x, z) == 0) continue;

                Vector3 candidate = terrain.BlockTopCenter(x, z);
                if (terrain.IsLakeAtWorld(candidate)) continue;

                if (Mathf.Abs(candidate.x - spawn.x) < spawnClearRadius &&
                    Mathf.Abs(candidate.z - spawn.z) < spawnClearRadius) continue;

                if (requireFlat && !IsFlat(map, x, z)) continue;

                bool tooClose = false;
                foreach (var other in _placed)
                {
                    if ((other - candidate).sqrMagnitude < separation * separation)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                // Pequeño desvío para que no queden todos clavados en el centro del bloque.
                candidate.x += Random.Range(-0.22f, 0.22f);
                candidate.z += Random.Range(-0.22f, 0.22f);

                position = candidate;
                _placed.Add(candidate);
                return true;
            }

            return false;
        }

        static bool IsFlat(HeightMap map, int x, int z)
        {
            int level = map.Get(x, z);
            if (!map.Contains(x + 1, z) || map.Get(x + 1, z) != level) return false;
            if (!map.Contains(x - 1, z) || map.Get(x - 1, z) != level) return false;
            if (!map.Contains(x, z + 1) || map.Get(x, z + 1) != level) return false;
            if (!map.Contains(x, z - 1) || map.Get(x, z - 1) != level) return false;
            return true;
        }
    }
}

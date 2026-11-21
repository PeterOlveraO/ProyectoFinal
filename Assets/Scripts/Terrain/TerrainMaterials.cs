using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Terrain
{
    /// <summary>
    /// Materiales del terreno generados por código:
    ///   - Atlas de pasto Point: variación tonal por bloque y pequeñas briznas sin geometría.
    ///   - Una tapa por zona, con el color de esa zona, y un costado más oscuro de la misma gama.
    ///   - URP Lit.
    /// El orden coincide con los submeshes de <see cref="ChunkMeshBuilder"/>:
    /// 0..2 tapas de zona 1..3, 3..5 costados de zona 1..3.
    ///
    /// El shader se toma de <see cref="TerrainSettings.terrainMaterial"/> y no de <c>Shader.Find</c>:
    /// en un build Unity elimina los shaders que ningún asset referencia, así que
    /// <c>Shader.Find("Universal Render Pipeline/Lit")</c> devuelve null en el dispositivo y el
    /// terreno se queda sin dibujar (se veían los personajes, pero no el mapa).
    /// </summary>
    public static class TerrainMaterials
    {
        public const int GrassTilesPerAxis = 4;
        const int TileSize = 16;

        public static Material[] Create(TerrainSettings settings)
        {
            var texture = CreateGrassTexture();
            var sideTexture = CreateSideTexture();

            var zoneColors = new[]
            {
                settings.zone1Color,
                settings.zone2Color,
                settings.zone3Color
            };

            var materials = new Material[ChunkMeshBuilder.SubMeshCount];
            for (int zone = 1; zone <= 3; zone++)
            {
                Color top = zoneColors[zone - 1];
                Color side = Color.Lerp(top, new Color(.78f, .68f, .49f), 0.35f);

                materials[zone - 1] = CreateMaterial(settings, texture, top, "Terrain_Zone" + zone + "_Top");
                materials[3 + zone - 1] = CreateMaterial(settings, sideTexture, side, "Terrain_Zone" + zone + "_Side");
            }

            return materials;
        }

        /// <summary>Agua opaca de los lagos (azul, algo brillante).</summary>
        public static Material CreateWaterMaterial(TerrainSettings settings)
        {
            var water = NewMaterial(settings);
            water.name = "Water";
            water.mainTexture = CreateWaterTexture();
            water.color = new Color(.33f, .62f, .69f, 1f);
            if (water.HasProperty("_Smoothness")) water.SetFloat("_Smoothness", 0.2f);
            if (water.HasProperty("_Glossiness")) water.SetFloat("_Glossiness", 0.2f);
            if (water.HasProperty("_Metallic")) water.SetFloat("_Metallic", 0f);
            return water;
        }

        /// <summary>Destruye los materiales y la textura compartida creados por <see cref="Create"/>.</summary>
        public static void Destroy(Material[] materials)
        {
            if (materials == null) return;

            var textures = new System.Collections.Generic.HashSet<Texture>();
            foreach (var material in materials)
            {
                if (material == null) continue;
                if (material.mainTexture != null) textures.Add(material.mainTexture);
                Object.Destroy(material);
            }
            foreach (var texture in textures) Object.Destroy(texture);
        }

        static Shader FindShader(TerrainSettings settings)
        {
            // El shader tiene que venir de un asset incluido en el build. Shader.Find solo
            // funciona en el editor: al compilar, Unity descarta el shader si ningún asset
            // lo referencia y en el iPhone devuelve null (terreno invisible, personajes sí).
            if (settings != null && settings.terrainMaterial != null && settings.terrainMaterial.shader != null)
                return settings.terrainMaterial.shader;

            Debug.LogError(
                "[TerrainMaterials] TerrainSettings.terrainMaterial está vacío. " +
                "Asigna Assets/Materials/M_TerrainBase.mat; sin él el terreno no se verá en el build.");

            if (GraphicsSettings.currentRenderPipeline != null)
                return Shader.Find("Universal Render Pipeline/Lit");
            return Shader.Find("Standard");
        }

        static Material NewMaterial(TerrainSettings settings)
        {
            // Clonar el material referenciado conserva variantes y propiedades del shader URP
            // en el player iOS, a diferencia de crear uno solo a partir de Shader.Find.
            return settings != null && settings.terrainMaterial != null
                ? new Material(settings.terrainMaterial) : new Material(FindShader(settings));
        }

        static Material CreateMaterial(TerrainSettings settings, Texture2D texture, Color color, string name)
        {
            var material = NewMaterial(settings);
            material.name = name;
            material.mainTexture = texture;
            material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0f);
            return material;
        }

        static Texture2D CreateGrassTexture()
        {
            const int size = TileSize * GrassTilesPerAxis;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Terrain_GrassAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int tile = (y / TileSize) * GrassTilesPerAxis + x / TileSize;
                    int tx = x % TileSize;
                    int ty = y % TileSize;
                    // Grandes manchas de color + pocos píxeles de hierba: lectura pixel-art
                    // sin bordes oscuros que dibujen una cuadrícula alrededor de cada bloque.
                    uint hash = Hash((uint)(tx / 4 + tile * 17), (uint)(ty / 4 + tile * 31));
                    uint grain = Hash((uint)(tx + tile * 11), (uint)(ty + tile * 23));
                    float shade = 0.92f + ((int)(hash % 17u) - 8) * 0.009f;
                    if (grain % 37u == 0u) shade *= 0.72f;
                    else if (grain % 29u == 0u) shade *= 1.10f;
                    pixels[y * size + x] = new Color(shade, shade, shade, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Texture2D CreateSideTexture()
        {
            const int size = TileSize * GrassTilesPerAxis;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Terrain_EarthAtlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int localY = y % TileSize;
                uint hash = Hash((uint)(x / 3), (uint)(y / 3));
                float variation = hash % 11u < 2u ? .82f : 1f;
                // Banda superior fina de césped que corona los cortes de altura.
                pixels[y * size + x] = localY >= TileSize - 2
                    ? new Color(.62f, .76f, .49f, 1f)
                    : new Color(.63f * variation, .53f * variation, .37f * variation, 1f);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>Small opaque pixel pattern used by the animated lake surface.</summary>
        public static Texture2D CreateWaterTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Terrain_WaterPixels", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat
            };
            var pixels = new Color[size * size];
            Color baseWater = new Color(.72f, .88f, .86f, 1f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Thin, staggered wave crests rather than floating square noise.
                int waveX = (x + (y / 8) * 7) % 16;
                bool crest = y % 8 == 3 && waveX >= 3 && waveX <= 7;
                bool trough = y % 8 == 4 && waveX >= 5 && waveX <= 9;
                pixels[y * size + x] = crest ? new Color(.9f, 1f, .94f, 1f)
                    : trough ? new Color(.66f, .83f, .85f, 1f) : baseWater;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static uint Hash(uint x, uint y)
        {
            uint value = x * 374761393u + y * 668265263u + 1274126177u;
            value = (value ^ (value >> 13)) * 1274126177u;
            return value ^ (value >> 16);
        }
    }
}

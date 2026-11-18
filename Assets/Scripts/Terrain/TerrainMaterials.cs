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
                Color side = Color.Lerp(top, Color.black, 0.45f);

                materials[zone - 1] = CreateMaterial(settings, texture, top, "Terrain_Zone" + zone + "_Top");
                materials[3 + zone - 1] = CreateMaterial(settings, texture, side, "Terrain_Zone" + zone + "_Side");
            }

            return materials;
        }

        /// <summary>Agua opaca de los lagos (azul, algo brillante).</summary>
        public static Material CreateWaterMaterial(TerrainSettings settings)
        {
            var water = NewMaterial(settings);
            water.name = "Water";
            water.mainTexture = null;
            water.color = new Color(0.10f, 0.42f, 0.72f, 1f);
            if (water.HasProperty("_Smoothness")) water.SetFloat("_Smoothness", 0.75f);
            if (water.HasProperty("_Glossiness")) water.SetFloat("_Glossiness", 0.75f);
            if (water.HasProperty("_Metallic")) water.SetFloat("_Metallic", 0.1f);
            return water;
        }

        /// <summary>Destruye los materiales y la textura compartida creados por <see cref="Create"/>.</summary>
        public static void Destroy(Material[] materials)
        {
            if (materials == null) return;

            Texture texture = null;
            foreach (var material in materials)
            {
                if (material == null) continue;
                if (texture == null) texture = material.mainTexture;
                Object.Destroy(material);
            }
            if (texture != null) Object.Destroy(texture);
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
                    // Hash entero, no estado global de Random. 16 tonos suaves y
                    // briznas de 1-2 texels; atlas compartido de solo 64x64 (16 KiB).
                    uint hash = unchecked((uint)(tx * 374761393 + (ty / 2) * 668265263 + tile * 1274126177));
                    hash = (hash ^ (hash >> 13)) * 1274126177u;
                    float shade = 0.85f + tile * 0.01f;
                    if (hash % 13u < 2u) shade *= ty % 2 == 0 ? 0.78f : 1.08f;
                    if (tx == 0 || ty == 0 || tx == TileSize - 1 || ty == TileSize - 1)
                        shade *= 0.9f;
                    pixels[y * size + x] = new Color(shade, shade, shade, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Terrain
{
    /// <summary>
    /// Materiales del terreno generados por código:
    ///   - Textura de cuadrícula (filtro Point) que marca el contorno de cada cara de bloque.
    ///   - Una tapa por zona, con el color de esa zona, y un costado más oscuro de la misma gama.
    ///   - URP Lit si hay pipeline asignado, Standard si no.
    /// El orden coincide con los submeshes de <see cref="ChunkMeshBuilder"/>:
    /// 0..2 tapas de zona 1..3, 3..5 costados de zona 1..3.
    /// </summary>
    public static class TerrainMaterials
    {
        const int GridTextureSize = 16;

        public static Material[] Create(TerrainSettings settings)
        {
            var texture = CreateGridTexture();
            var shader = FindShader();

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

                materials[zone - 1] = CreateMaterial(shader, texture, top, "Terrain_Zone" + zone + "_Top");
                materials[3 + zone - 1] = CreateMaterial(shader, texture, side, "Terrain_Zone" + zone + "_Side");
            }

            return materials;
        }

        /// <summary>Agua opaca de los lagos (azul, algo brillante).</summary>
        public static Material CreateWaterMaterial()
        {
            var shader = FindShader();
            if (shader == null) shader = Shader.Find("Unlit/Color");

            var water = new Material(shader)
            {
                name = "Water",
                color = new Color(0.10f, 0.42f, 0.72f, 1f)
            };
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

        static Shader FindShader()
        {
            Shader shader = null;
            if (GraphicsSettings.currentRenderPipeline != null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            return shader;
        }

        static Material CreateMaterial(Shader shader, Texture2D texture, Color color, string name)
        {
            var material = new Material(shader) { name = name, mainTexture = texture, color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0f);
            return material;
        }

        static Texture2D CreateGridTexture()
        {
            const int size = GridTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Terrain_Grid",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            var fill = new Color(1f, 1f, 1f, 1f);
            var line = new Color(0.7f, 0.7f, 0.7f, 1f);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    pixels[y * size + x] = edge ? line : fill;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}

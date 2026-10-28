using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Terrain
{
    /// <summary>
    /// Materiales grises temporales del terreno, generados por código:
    ///   - Textura de cuadrícula (filtro Point) que marca el contorno de cada cara de bloque.
    ///   - Una tapa por nivel, más clara cuanto más alto; costados más oscuros.
    ///   - URP Lit si hay pipeline asignado, Standard si no.
    /// El orden coincide con los submeshes de <see cref="ChunkMeshBuilder"/>.
    /// </summary>
    public static class TerrainMaterials
    {
        const int GridTextureSize = 16;

        public static Material[] Create(int maxLevel)
        {
            var texture = CreateGridTexture();
            var shader = FindShader();

            var materials = new Material[maxLevel + 2];
            for (int level = 0; level <= maxLevel; level++)
            {
                float t = maxLevel > 0 ? (float)level / maxLevel : 1f;
                float gray = Mathf.Lerp(0.45f, 0.85f, t);
                materials[level] = CreateMaterial(shader, texture, new Color(gray, gray, gray, 1f), "Terrain_Top_" + level);
            }

            materials[maxLevel + 1] = CreateMaterial(shader, texture, new Color(0.3f, 0.3f, 0.3f, 1f), "Terrain_Side");
            return materials;
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

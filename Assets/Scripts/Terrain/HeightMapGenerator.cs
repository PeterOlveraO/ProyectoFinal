using UnityEngine;

namespace Game.Terrain
{
    /// <summary>
    /// Genera el mapa de niveles del mundo completo de forma determinista (misma semilla,
    /// mismo resultado):
    ///   1. fBm de Perlin (octavas con desplazamientos derivados de la semilla).
    ///   2. Normalización por el mínimo/máximo del mapa y contraste, para usar todos los niveles.
    ///   3. Zona plana en el centro con transición suave hacia el relieve.
    ///   4. Cuantización a niveles enteros 0..maxLevel.
    /// </summary>
    public static class HeightMapGenerator
    {
        public static HeightMap Generate(TerrainSettings settings)
        {
            int width = Mathf.Max(1, settings.WorldBlocksX);
            int depth = Mathf.Max(1, settings.WorldBlocksZ);
            int octaves = Mathf.Max(1, settings.octaves);
            int maxLevel = Mathf.Max(0, settings.maxLevel);

            // Desplazamientos por octava: la semilla mueve el muestreo a otra región del ruido.
            var rng = new System.Random(settings.seed);
            var offsets = new Vector2[octaves];
            for (int i = 0; i < octaves; i++)
                offsets[i] = new Vector2(rng.Next(-1000, 1000), rng.Next(-1000, 1000));

            var raw = new float[width * depth];
            float min = float.MaxValue;
            float max = float.MinValue;

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float value = Fbm(x + 0.5f, z + 0.5f, settings, offsets);
                    raw[z * width + x] = value;
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
            }

            float range = max - min;
            float centerX = width * 0.5f;
            float centerZ = depth * 0.5f;
            int flatLevel = Mathf.Clamp(settings.flatLevel, 0, maxLevel);

            var map = new HeightMap(width, depth);

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float v = range > 0f ? (raw[z * width + x] - min) / range : 0.5f;
                    v = Mathf.Clamp01((v - 0.5f) * settings.contrast + 0.5f);

                    // Valor continuo en [0, maxLevel + 1): cada nivel ocupa el mismo intervalo.
                    float height = v * (maxLevel + 1);

                    // Zona plana: dentro del radio se fuerza flatLevel (+0.5 = centro del
                    // intervalo, así floor() da exactamente flatLevel); en la transición se mezcla.
                    float dx = x + 0.5f - centerX;
                    float dz = z + 0.5f - centerZ;
                    float distance = Mathf.Sqrt(dx * dx + dz * dz);
                    float t = settings.flatBlend > 0f
                        ? Mathf.SmoothStep(0f, 1f, (distance - settings.flatRadius) / settings.flatBlend)
                        : (distance <= settings.flatRadius ? 0f : 1f);
                    height = Mathf.Lerp(flatLevel + 0.5f, height, t);

                    map.Set(x, z, Mathf.Clamp(Mathf.FloorToInt(height), 0, maxLevel));
                }
            }

            return map;
        }

        static float Fbm(float x, float z, TerrainSettings settings, Vector2[] offsets)
        {
            float sum = 0f;
            float amplitude = 1f;
            float frequency = settings.noiseScale;

            for (int i = 0; i < offsets.Length; i++)
            {
                sum += Mathf.PerlinNoise(x * frequency + offsets[i].x, z * frequency + offsets[i].y) * amplitude;
                amplitude *= settings.persistence;
                frequency *= settings.lacunarity;
            }

            return sum;
        }
    }
}

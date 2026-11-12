using UnityEngine;

namespace Game.Terrain
{
    /// <summary>
    /// Genera el mapa de niveles del mundo completo de forma determinista (misma semilla,
    /// mismo resultado). El mundo se divide en tres zonas concéntricas:
    ///
    ///   - Zona 1 (radio zone1Radius): el terreno "normal" de siempre, con su zona plana central.
    ///   - Zona 2 (anillo hasta zone2Radius): meseta elevada (zone2BaseLevel), rodea la zona 1.
    ///   - Zona 3 (resto hasta el borde): tierras bajas (zone3BaseLevel), rodean la zona 2.
    ///
    /// Las fronteras son cortados verticales (más de 2 bloques: no se pueden escalar) salvo
    /// cuatro rampas en N/S/E/O, donde la altura se interpola suavemente para poder pasar.
    /// Además se excavan lagos circulares de <see cref="TerrainSettings.lakeDepth"/> bloques
    /// de profundidad en la zona 1.
    /// </summary>
    public static class HeightMapGenerator
    {
        public static HeightMap Generate(TerrainSettings settings)
        {
            int width = Mathf.Max(1, settings.WorldBlocksX);
            int depth = Mathf.Max(1, settings.WorldBlocksZ);
            int octaves = Mathf.Max(1, settings.octaves);
            int maxLevel = Mathf.Max(0, settings.maxLevel);
            int relief = Mathf.Max(1, settings.zone1ReliefLevels);
            int ringRelief = Mathf.Max(0, settings.ringReliefLevels);

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

            float halfWorld = Mathf.Min(width, depth) * 0.5f;
            float zone1Radius = Mathf.Clamp(settings.zone1Radius, 1f, halfWorld - 2f);
            float zone2Radius = Mathf.Clamp(settings.zone2Radius, zone1Radius + 2f, halfWorld);
            float rampWidth = Mathf.Max(2f, settings.rampWidth);
            float rampHalfWidth = rampWidth * 0.5f;
            float rampHalfAngle = Mathf.Clamp(settings.rampHalfAngle, 1f, 45f);

            var map = new HeightMap(width, depth);

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float v = range > 0f ? (raw[z * width + x] - min) / range : 0.5f;
                    v = Mathf.Clamp01((v - 0.5f) * settings.contrast + 0.5f);

                    // Zona 1: el terreno de siempre (relieve + zona plana central).
                    float h1 = v * (relief + 1);
                    float dx = x + 0.5f - centerX;
                    float dz = z + 0.5f - centerZ;
                    float distance = Mathf.Sqrt(dx * dx + dz * dz);

                    float flatT = settings.flatBlend > 0f
                        ? Mathf.SmoothStep(0f, 1f, (distance - settings.flatRadius) / settings.flatBlend)
                        : (distance <= settings.flatRadius ? 0f : 1f);
                    h1 = Mathf.Lerp(flatLevel + 0.5f, h1, flatT);

                    // Zonas 2 y 3: meseta alta y tierras bajas, con el mismo ruido de base.
                    float h2 = Mathf.Clamp(settings.zone2BaseLevel + v * ringRelief, 0f, maxLevel);
                    float h3 = Mathf.Clamp(settings.zone3BaseLevel + v * ringRelief, 0f, maxLevel);

                    int zone = distance < zone1Radius ? 1 : distance < zone2Radius ? 2 : 3;
                    float height = zone == 1 ? h1 : zone == 2 ? h2 : h3;

                    // Rampas de acceso: solo dentro del ancho de la frontera y del sector angular.
                    if (Mathf.Abs(distance - zone1Radius) <= rampHalfWidth && IsRampAngle(dx, dz, rampHalfAngle))
                    {
                        float t = Mathf.SmoothStep(0f, 1f,
                            (distance - (zone1Radius - rampHalfWidth)) / rampWidth);
                        height = Mathf.Lerp(h1, h2, t);
                    }
                    else if (Mathf.Abs(distance - zone2Radius) <= rampHalfWidth && IsRampAngle(dx, dz, rampHalfAngle))
                    {
                        float t = Mathf.SmoothStep(0f, 1f,
                            (distance - (zone2Radius - rampHalfWidth)) / rampWidth);
                        height = Mathf.Lerp(h2, h3, t);
                    }

                    map.Set(x, z, Mathf.Clamp(Mathf.FloorToInt(height), 0, maxLevel));
                    map.SetZone(x, z, zone);
                }
            }

            CarveLakes(map, settings, rng, centerX, centerZ, zone1Radius, rampHalfWidth);
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

        /// <summary>Cuatro rampas alineadas con los ejes (+X, +Z, -X, -Z).</summary>
        static bool IsRampAngle(float dx, float dz, float halfAngle)
        {
            float angle = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
            float nearest = Mathf.Round(angle / 90f) * 90f;
            return Mathf.Abs(Mathf.DeltaAngle(angle, nearest)) <= halfAngle;
        }

        static void CarveLakes(HeightMap map, TerrainSettings settings, System.Random rng,
                               float centerX, float centerZ, float zone1Radius, float rampHalfWidth)
        {
            int lakeCount = Mathf.Clamp(settings.lakeCount, 0, 12);
            if (lakeCount == 0) return;

            int radius = Mathf.Clamp(settings.lakeRadius, 2, 12);
            int depth = Mathf.Clamp(settings.lakeDepth, 1, 4);

            float minRadius = settings.flatRadius + 6f;
            float maxRadius = zone1Radius - rampHalfWidth - radius - 2f;
            if (maxRadius <= minRadius) return;

            int attempts = 0;
            int maxAttempts = lakeCount * 60;

            while (map.Lakes.Count < lakeCount && attempts < maxAttempts)
            {
                attempts++;

                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float distance = Mathf.Lerp(minRadius, maxRadius, (float)rng.NextDouble());
                int cx = Mathf.FloorToInt(centerX + Mathf.Cos(angle) * distance);
                int cz = Mathf.FloorToInt(centerZ + Mathf.Sin(angle) * distance);

                if (!map.Contains(cx, cz) || map.GetZone(cx, cz) != 1) continue;
                if (OverlapsLake(map, cx, cz, radius)) continue;

                // Borde del lago: el punto más alto en un anillo alrededor del hoyo.
                int rimLevel = int.MinValue;
                for (int z = cz - radius - 2; z <= cz + radius + 2; z++)
                {
                    for (int x = cx - radius - 2; x <= cx + radius + 2; x++)
                    {
                        if (!map.Contains(x, z)) continue;
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                        if (d <= radius + 2f && map.Get(x, z) > rimLevel)
                            rimLevel = map.Get(x, z);
                    }
                }

                if (rimLevel < depth + 1) continue;

                int bottomLevel = Mathf.Max(0, rimLevel - depth);

                // Hoyo circular + orilla plana de un bloque: el corte queda a plomo.
                for (int z = cz - radius - 1; z <= cz + radius + 1; z++)
                {
                    for (int x = cx - radius - 1; x <= cx + radius + 1; x++)
                    {
                        if (!map.Contains(x, z)) continue;

                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
                        if (d <= radius)
                            map.Set(x, z, bottomLevel);
                        else if (d <= radius + 1f)
                            map.Set(x, z, rimLevel);
                    }
                }

                map.Lakes.Add(new LakeInfo
                {
                    Center = new Vector2Int(cx, cz),
                    Radius = radius,
                    RimLevel = rimLevel,
                    BottomLevel = bottomLevel
                });
            }
        }

        static bool OverlapsLake(HeightMap map, int x, int z, int radius)
        {
            foreach (var lake in map.Lakes)
            {
                float dx = x - lake.Center.x;
                float dz = z - lake.Center.y;
                float minDistance = radius + lake.Radius + 3f;
                if (dx * dx + dz * dz < minDistance * minDistance) return true;
            }
            return false;
        }
    }
}

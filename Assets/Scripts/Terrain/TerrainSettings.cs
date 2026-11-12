using UnityEngine;

namespace Game.Terrain
{
    /// <summary>
    /// Configuración única del terreno: tamaño del mundo, relieve, zona plana y carga.
    ///
    /// El mundo es finito: chunksX × chunksZ chunks de chunkSize × chunkSize bloques.
    /// Las alturas son niveles enteros (0..maxLevel); cada nivel sube stepHeight unidades.
    /// </summary>
    [CreateAssetMenu(menuName = "World/Terrain Settings", fileName = "TerrainSettings")]
    public sealed class TerrainSettings : ScriptableObject
    {
        [Header("Mundo")]
        [Tooltip("Bloques por lado de cada chunk.")]
        [Min(1)] public int chunkSize = 16;

        [Tooltip("Chunks en el eje X.")]
        [Min(1)] public int chunksX = 10;

        [Tooltip("Chunks en el eje Z.")]
        [Min(1)] public int chunksZ = 10;

        [Tooltip("Lado de un bloque en planta (unidades de Unity).")]
        [Min(0.01f)] public float blockSize = 1f;

        [Header("Niveles")]
        [Tooltip("Altura de cada nivel. 0.5 = medio bloque.")]
        [Min(0.01f)] public float stepHeight = 0.5f;

        [Tooltip("Nivel más alto (los niveles van de 0 a maxLevel).")]
        [Range(0, 32)] public int maxLevel = 4;

        [Header("Ruido (fBm Perlin)")]
        public int seed = 12345;

        [Tooltip("Frecuencia base del ruido por bloque. Más alto = relieve más pequeño y frecuente.")]
        [Min(0.0001f)] public float noiseScale = 0.06f;

        [Range(1, 8)] public int octaves = 3;

        [Tooltip("Factor de amplitud entre octavas.")]
        [Range(0f, 1f)] public float persistence = 0.5f;

        [Tooltip("Factor de frecuencia entre octavas.")]
        [Min(1f)] public float lacunarity = 2f;

        [Tooltip("Contraste aplicado al ruido normalizado para que se usen todos los niveles. 1 = sin cambio.")]
        [Min(0.1f)] public float contrast = 1.6f;

        [Header("Zona plana central (base)")]
        [Tooltip("Radio en bloques de la zona totalmente plana.")]
        [Min(0f)] public float flatRadius = 12f;

        [Tooltip("Ancho en bloques de la transición entre la zona plana y el relieve.")]
        [Min(0f)] public float flatBlend = 8f;

        [Tooltip("Nivel de la zona plana.")]
        [Min(0)] public int flatLevel = 1;

        [Header("Carga")]
        [Tooltip("Chunks activos alrededor del jugador (distancia de Chebyshev). 3 = 7x7. 0 = todos activos.")]
        [Min(0)] public int loadRadius = 3;

        [Header("Zonas concéntricas")]
        [Tooltip("Radio en bloques de la zona 1 (terreno normal, la de siempre).")]
        [Min(4f)] public float zone1Radius = 36f;

        [Tooltip("Radio en bloques de la zona 2 (rodea la zona 1). El resto hasta el borde es la zona 3.")]
        [Min(5f)] public float zone2Radius = 58f;

        [Tooltip("Niveles de relieve de la zona 1 (misma escala que antes).")]
        [Range(1, 16)] public int zone1ReliefLevels = 4;

        [Tooltip("Nivel base de la zona 2: más alta que cualquier punto de la zona 1.")]
        [Min(0)] public int zone2BaseLevel = 6;

        [Tooltip("Nivel base de la zona 3: la más baja del mundo.")]
        [Min(0)] public int zone3BaseLevel = 0;

        [Tooltip("Niveles de relieve que se suman en las zonas 2 y 3.")]
        [Range(0, 8)] public int ringReliefLevels = 2;

        [Tooltip("Ancho en bloques de las rampas que conectan una zona con la siguiente.")]
        [Min(2f)] public float rampWidth = 10f;

        [Tooltip("Semiángulo (grados) de las cuatro rampas (N, S, E, O).")]
        [Range(1f, 45f)] public float rampHalfAngle = 9f;

        [Header("Colores de zona")]
        public Color zone1Color = new Color(0.30f, 0.55f, 0.28f, 1f);
        public Color zone2Color = new Color(0.80f, 0.52f, 0.18f, 1f);
        public Color zone3Color = new Color(0.30f, 0.45f, 0.75f, 1f);

        [Header("Lagos")]
        [Tooltip("Cantidad de lagos decorativos (solo en la zona 1).")]
        [Range(0, 12)] public int lakeCount = 3;

        [Tooltip("Radio en bloques de cada lago.")]
        [Range(2, 12)] public int lakeRadius = 4;

        [Tooltip("Profundidad en bloques. 2 bloques ya no se pueden escalar.")]
        [Range(1, 4)] public int lakeDepth = 2;

        [Header("Enemigos por zona")]
        [Tooltip("Máximo de enemigos vivos en la zona 1 a la vez.")]
        [Min(1)] public int zone1MaxAlive = 20;

        [Tooltip("Cada cuántos segundos aparece un lote en la zona 1.")]
        [Min(0.1f)] public float zone1SpawnInterval = 3f;

        [Min(1)] public int zone1SpawnBatch = 2;

        [Tooltip("Máximo de enemigos vivos en la zona 2 a la vez.")]
        [Min(1)] public int zone2MaxAlive = 40;

        [Tooltip("Cada cuántos segundos aparece un lote en la zona 2.")]
        [Min(0.1f)] public float zone2SpawnInterval = 2f;

        [Min(1)] public int zone2SpawnBatch = 3;

        [Tooltip("Máximo de enemigos vivos en la zona 3 a la vez.")]
        [Min(1)] public int zone3MaxAlive = 100;

        [Tooltip("Cada cuántos segundos aparece un lote en la zona 3.")]
        [Min(0.1f)] public float zone3SpawnInterval = 1f;

        [Min(1)] public int zone3SpawnBatch = 6;

        [Tooltip("Velocidad de persecución de los triángulos (bloques por segundo). El jugador se mueve a 5.")]
        [Min(0.1f)] public float enemyChaseSpeed = 2.5f;

        /// <summary>Bloques del mundo en X.</summary>
        public int WorldBlocksX => chunksX * chunkSize;

        /// <summary>Bloques del mundo en Z.</summary>
        public int WorldBlocksZ => chunksZ * chunkSize;

        /// <summary>Radio en bloques del mundo (hasta el borde).</summary>
        public float WorldRadius => Mathf.Min(WorldBlocksX, WorldBlocksZ) * 0.5f;

        void OnValidate()
        {
            flatLevel = Mathf.Clamp(flatLevel, 0, maxLevel);

            float half = WorldRadius;
            zone1Radius = Mathf.Clamp(zone1Radius, 4f, half - 4f);
            zone2Radius = Mathf.Clamp(zone2Radius, zone1Radius + 4f, half);
            maxLevel = Mathf.Max(maxLevel, zone2BaseLevel + ringReliefLevels);

            lakeRadius = Mathf.Clamp(lakeRadius, 2, 12);
            lakeDepth = Mathf.Clamp(lakeDepth, 1, 4);
        }
    }
}

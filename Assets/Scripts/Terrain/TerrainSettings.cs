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

        /// <summary>Bloques del mundo en X.</summary>
        public int WorldBlocksX => chunksX * chunkSize;

        /// <summary>Bloques del mundo en Z.</summary>
        public int WorldBlocksZ => chunksZ * chunkSize;

        void OnValidate()
        {
            flatLevel = Mathf.Clamp(flatLevel, 0, maxLevel);
        }
    }
}

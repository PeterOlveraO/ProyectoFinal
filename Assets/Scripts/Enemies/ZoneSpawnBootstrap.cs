using Game.Terrain;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Crea en runtime un spawner por zona (con sus puntos repartidos por el anillo).
    /// Los cupos y ritmos salen de <see cref="TerrainSettings"/>.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class ZoneSpawnBootstrap : MonoBehaviour
    {
        [SerializeField] TerrainSettings settings;

        void Start()
        {
            var terrain = WorldTerrain.Instance;
            if (terrain == null)
            {
                Debug.LogError("[ZoneSpawnBootstrap] No hay WorldTerrain en la escena.", this);
                return;
            }

            var config = settings != null ? settings : terrain.Settings;
            if (config == null)
            {
                Debug.LogError("[ZoneSpawnBootstrap] Falta TerrainSettings.", this);
                return;
            }

            CreateZone(terrain, config, 1, config.zone1MaxAlive, config.zone1SpawnInterval, config.zone1SpawnBatch, config.enemyChaseSpeed);
            CreateZone(terrain, config, 2, config.zone2MaxAlive, config.zone2SpawnInterval, config.zone2SpawnBatch, config.enemyChaseSpeed);
            CreateZone(terrain, config, 3, config.zone3MaxAlive, config.zone3SpawnInterval, config.zone3SpawnBatch, config.enemyChaseSpeed);
        }

        static void CreateZone(WorldTerrain terrain, TerrainSettings settings, int zone,
                               int maxAlive, float interval, int batch, float enemySpeed)
        {
            var root = new GameObject("Zone " + zone + " Spawners");
            root.transform.SetParent(terrain.transform, false);

            var manager = root.AddComponent<ZoneSpawnManager>();
            manager.Configure(zone, settings, maxAlive, interval, batch, enemySpeed);
        }
    }
}

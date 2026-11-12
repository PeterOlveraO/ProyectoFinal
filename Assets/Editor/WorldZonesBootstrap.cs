#if UNITY_EDITOR
using Game.Enemies;
using Game.Rounds;
using Game.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Configura las tres zonas concéntricas y los spawners de enemigos. Es idempotente:
    /// se puede ejecutar varias veces desde Tools/World/Rebuild Zones And Spawners.
    ///
    ///   Zona 1: terreno normal (verde).
    ///   Zona 2: meseta elevada (naranja), rodea la zona 1.
    ///   Zona 3: tierras bajas (azul), rodea la zona 2.
    /// </summary>
    public static class WorldZonesBootstrap
    {
        const string ScenePath = "Assets/Scenes/World.unity";
        const string SettingsPath = "Assets/Settings/TerrainSettings.asset";

        [MenuItem("Tools/World/Rebuild Zones And Spawners")]
        public static void Rebuild()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);

            var settings = AssetDatabase.LoadAssetAtPath<TerrainSettings>(SettingsPath);
            if (settings == null)
            {
                Debug.LogError("[WorldZonesBootstrap] No se encontró " + SettingsPath);
                return;
            }

            ApplyZoneSettings(settings);

            var world = GameObject.Find("World");
            if (world == null)
            {
                Debug.LogError("[WorldZonesBootstrap] No hay objeto 'World' en la escena.");
                return;
            }

            // Los enemigos ya no salen de spawners infinitos por zona: los maneja el
            // sistema de rondas alrededor del jugador.
            var oldBootstrap = world.GetComponent<ZoneSpawnBootstrap>();
            if (oldBootstrap != null) Object.DestroyImmediate(oldBootstrap, true);

            if (world.GetComponent<RoundManager>() == null) world.AddComponent<RoundManager>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[WorldZonesBootstrap] Zonas 1/2/3 configuradas: radios " + settings.zone1Radius + "/" +
                      settings.zone2Radius + " bloques, colores distintos, lagos y sistema de rondas.");
        }

        static void ApplyZoneSettings(TerrainSettings settings)
        {
            settings.maxLevel = 8;

            settings.zone1Radius = 36f;
            settings.zone2Radius = 58f;
            settings.zone1ReliefLevels = 4;
            settings.zone2BaseLevel = 6;
            settings.zone3BaseLevel = 0;
            settings.ringReliefLevels = 2;
            settings.rampWidth = 10f;
            settings.rampHalfAngle = 9f;

            settings.zone1Color = new Color(0.30f, 0.55f, 0.28f, 1f);
            settings.zone2Color = new Color(0.80f, 0.52f, 0.18f, 1f);
            settings.zone3Color = new Color(0.30f, 0.45f, 0.75f, 1f);

            settings.lakeCount = 3;
            settings.lakeRadius = 4;
            settings.lakeDepth = 2;

            settings.loadRadius = 5;

            settings.zone1MaxAlive = 10;
            settings.zone1SpawnInterval = 5f;
            settings.zone1SpawnBatch = 1;

            settings.zone2MaxAlive = 20;
            settings.zone2SpawnInterval = 3f;
            settings.zone2SpawnBatch = 2;

            settings.zone3MaxAlive = 50;
            settings.zone3SpawnInterval = 2f;
            settings.zone3SpawnBatch = 5;

            settings.enemyChaseSpeed = 2.5f;

            EditorUtility.SetDirty(settings);
        }
    }
}
#endif

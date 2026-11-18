#if UNITY_EDITOR
using System;
using Game.Terrain;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Migración explícita e idempotente de issue #13. Ejecutar tras integrar;
    /// no abre ni modifica escenas, ni depende de InitializeOnLoad.
    /// </summary>
    public static class GrassLakeSetup
    {
        [MenuItem("Tools/World/Apply Grass And Large Lake")]
        public static void ApplyGrassAndLargeLake()
        {
            const string path = "Assets/Settings/TerrainSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<TerrainSettings>(path);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_TerrainBase.mat");
            if (settings == null || material == null || material.shader == null ||
                material.shader.name != "Universal Render Pipeline/Lit")
                throw new InvalidOperationException("Faltan TerrainSettings o el material persistente URP/Lit.");

            Undo.RecordObject(settings, "Apply grass and large lake");
            settings.zone1Radius = 42f;
            settings.zone1Color = new Color(0.30f, 0.55f, 0.28f, 1f);
            settings.zone2Color = new Color(0.38f, 0.61f, 0.30f, 1f);
            settings.zone3Color = new Color(0.24f, 0.46f, 0.23f, 1f);
            settings.lakeCount = 1;
            settings.lakeRadius = 11;
            settings.lakeDepth = 2;
            settings.terrainMaterial = material; // Referencia persistente contra stripping iOS.
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[GrassLakeSetup] Pasto verde y lago de 23 bloques de diámetro aplicados.");
        }
    }
}
#endif

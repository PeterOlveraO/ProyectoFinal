#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.EditorTools
{
    /// <summary>
    /// Preset de render para móvil (iOS/Android) sobre el URP Asset de la calidad Mobile.
    /// Menú: Tools/World/Apply Mobile Rendering Preset.
    ///
    /// Objetivo: menos ancho de banda y menos overdraw manteniendo el look.
    /// </summary>
    public static class MobilePreset
    {
        const string MobileRpPath = "Assets/Settings/Mobile_RPAsset.asset";

        [MenuItem("Tools/World/Apply Mobile Rendering Preset")]
        public static void Apply()
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MobileRpPath);
            if (rp == null)
            {
                Debug.LogError("[MobilePreset] No encontrado: " + MobileRpPath);
                return;
            }

            var so = new SerializedObject(rp);

            SetBool(so, "m_SupportsHDR", false);              // sin HDR: ahorra ancho de banda
            SetBool(so, "m_UseSRPBatcher", true);             // batching de SRP
            SetFloat(so, "m_RenderScale", 0.8f);              // 80% de resolución interna
            SetInt(so, "m_MSAA", 1);                          // MSAA off (caro en tile-based)
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetInt(so, "m_MainLightShadowmapResolution", 1024);
            SetInt(so, "m_ShadowCascadeCount", 1);
            SetFloat(so, "m_ShadowDistance", 40f);
            SetBool(so, "m_SoftShadowsSupported", false);
            SetBool(so, "m_AdditionalLightShadowsSupported", false);

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            Debug.Log("[MobilePreset] Mobile RP Asset ajustado: HDR off, renderScale 0.8, sombras 1024/1 cascada, " +
                      "sin MSAA ni sombras de luces adicionales, SRP Batcher on.");
        }

        static void SetBool(SerializedObject so, string path, bool value)
        {
            var p = so.FindProperty(path);
            if (p == null) { Warn(path); return; }
            p.boolValue = value;
        }

        static void SetInt(SerializedObject so, string path, int value)
        {
            var p = so.FindProperty(path);
            if (p == null) { Warn(path); return; }
            p.intValue = value;
        }

        static void SetFloat(SerializedObject so, string path, float value)
        {
            var p = so.FindProperty(path);
            if (p == null) { Warn(path); return; }
            p.floatValue = value;
        }

        static void Warn(string path) => Debug.LogWarning("[MobilePreset] Propiedad no encontrada: " + path);
    }
}
#endif

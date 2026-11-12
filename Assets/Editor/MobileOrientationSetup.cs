#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Fija la orientación horizontal del juego: es un juego de móvil y se juega en apaisado.
    /// Bloquea el vertical y permite las dos orientaciones horizontales.
    ///
    /// Se aplica desde <c>Tools > Mobile > Forzar Horizontal</c>.
    /// </summary>
    public static class MobileOrientationSetup
    {
        [MenuItem("Tools/Mobile/Forzar Horizontal")]
        public static void Apply()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            AssetDatabase.SaveAssets();
            Debug.Log("[MobileOrientationSetup] Orientación horizontal aplicada (LandscapeLeft/LandscapeRight).");
        }
    }
}
#endif

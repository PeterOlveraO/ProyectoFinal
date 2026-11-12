using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Ajustes de runtime para móvil (iOS/Android). No hace nada en el editor ni en PC.
    ///
    /// Por qué existe: en móvil el framerate objetivo no se fija solo, y el vSync del
    /// editor no aplica. Además conviene evitar que la pantalla se apague en sesiones largas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileRuntimeSettings : MonoBehaviour
    {
        [Tooltip("Framerate objetivo en móvil. 60 para gama media/alta, 30 si el juego es pesado.")]
        [SerializeField, Range(30, 120)] int targetFrameRate = 60;

        [Tooltip("Desactiva vSync en móvil (el control lo lleva targetFrameRate).")]
        [SerializeField] bool disableVSync = true;

        [Tooltip("Evita que la pantalla se apague durante la partida.")]
        [SerializeField] bool neverSleep = true;

        [Tooltip("Fuerza la orientación horizontal en móvil (el juego no se juega en vertical).")]
        [SerializeField] bool forceLandscape = true;

        void Awake()
        {
            if (!Application.isMobilePlatform) return;

            Application.targetFrameRate = targetFrameRate;
            if (disableVSync) QualitySettings.vSyncCount = 0;
            if (neverSleep) Screen.sleepTimeout = SleepTimeout.NeverSleep;

            if (forceLandscape)
            {
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
        }
    }
}

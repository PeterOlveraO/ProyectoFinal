using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Weather
{
    /// <summary>Owns a reversible snapshot, never a scene light or an asset.</summary>
    internal sealed class WeatherEnvironment
    {
        Light _sun;
        float _sunIntensity, _ambientIntensity;
        Color _sunColor, _ambientLight, _sky, _equator, _ground;
        bool _fog, _captured;
        FogMode _fogMode;
        Color _fogColor;
        float _fogDensity, _fogStart, _fogEnd;

        public void Capture()
        {
            if (_captured) return;
            _captured = true;
            _sun = RenderSettings.sun;
            if (_sun == null || _sun.type != LightType.Directional)
            {
                _sun = null;
                // One discovery per enable, not per frame. Never create a competing sun.
                foreach (var light in Object.FindObjectsByType<Light>())
                {
                    if (!light.isActiveAndEnabled || light.type != LightType.Directional) continue;
                    _sun = light;
                    break;
                }
            }
            if (_sun != null)
            {
                _sunIntensity = _sun.intensity;
                _sunColor = _sun.color;
            }
            _ambientIntensity = RenderSettings.ambientIntensity;
            _ambientLight = RenderSettings.ambientLight;
            _sky = RenderSettings.ambientSkyColor;
            _equator = RenderSettings.ambientEquatorColor;
            _ground = RenderSettings.ambientGroundColor;
            _fog = RenderSettings.fog;
            _fogMode = RenderSettings.fogMode;
            _fogColor = RenderSettings.fogColor;
            _fogDensity = RenderSettings.fogDensity;
            _fogStart = RenderSettings.fogStartDistance;
            _fogEnd = RenderSettings.fogEndDistance;
        }

        public void Apply(WeatherStyle style)
        {
            if (!_captured) return;
            if (_sun != null)
            {
                _sun.intensity = _sunIntensity * style.SunStrength;
                _sun.color = _sunColor * style.SunColor;
            }
            // Preserve ambient mode, skybox, sun assignment, shadows and light rotation.
            RenderSettings.ambientIntensity = _ambientIntensity * style.AmbientStrength;
            RenderSettings.ambientLight = _ambientLight * style.AmbientStrength;
            RenderSettings.ambientSkyColor = _sky * style.AmbientStrength;
            RenderSettings.ambientEquatorColor = _equator * style.AmbientStrength;
            RenderSettings.ambientGroundColor = _ground * style.AmbientStrength;

            if (style.FogDensity <= .00001f) RestoreFog();
            else
            {
                // URP Lit applies atmospheric fog before WorldZoneFog (queue 2499).
                // Its black unlit overlay does not contain MixFog, so closed ground stays black.
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = style.FogDensity;
                RenderSettings.fogColor = style.FogColor;
            }
        }

        void RestoreFog()
        {
            RenderSettings.fog = _fog;
            RenderSettings.fogMode = _fogMode;
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogDensity = _fogDensity;
            RenderSettings.fogStartDistance = _fogStart;
            RenderSettings.fogEndDistance = _fogEnd;
        }

        public void Restore()
        {
            if (!_captured) return;
            if (_sun != null)
            {
                _sun.intensity = _sunIntensity;
                _sun.color = _sunColor;
            }
            RenderSettings.ambientIntensity = _ambientIntensity;
            RenderSettings.ambientLight = _ambientLight;
            RenderSettings.ambientSkyColor = _sky;
            RenderSettings.ambientEquatorColor = _equator;
            RenderSettings.ambientGroundColor = _ground;
            RestoreFog();
            _captured = false;
            _sun = null;
        }
    }
}

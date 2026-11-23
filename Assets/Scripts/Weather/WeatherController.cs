using System;
using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.Terrain;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Game.Weather
{
    /// <summary>
    /// Scene/run weather. Attach to the persistent menu host, not a per-round object.
    /// Changed reports the selected target once; visual blending runs on gameplay seconds.
    /// ResetWeather is the explicit return-to-main-menu operation, including while paused.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeatherController : MonoBehaviour
    {
        [SerializeField, Min(.1f)] float transitionSeconds = 2.4f;
        readonly WeatherTransition _transition = new WeatherTransition();
        readonly WeatherEnvironment _environment = new WeatherEnvironment();
        readonly List<UniversalAdditionalCameraData> _createdCameraData = new List<UniversalAdditionalCameraData>(2);
        GameObject _runtimeRoot;
        Volume _volume;
        VolumeProfile _profile;
        ColorAdjustments _grade;
        WeatherRain _rain;
        Camera _camera;
        UniversalAdditionalCameraData _cameraData;
        bool _previousPostProcessing, _ownsEnvironment;
        LayerMask _previousVolumeMask;
        VolumeFrameworkUpdateMode _previousUpdateMode;
        float _cameraRetryAt;

        public static WeatherController Instance { get; private set; }
        public WeatherKind Current => _transition.Current;
        public string CurrentDisplayName => DisplayName(Current);
        public bool IsTransitioning => _transition.IsTransitioning;
        public float TransitionProgress => _transition.Progress;
        public event Action Changed;
        internal WeatherStyle VisualState => _transition.Value;

        public static WeatherController EnsureExists(GameObject host)
        {
            if (Instance != null) return Instance;
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (!host.TryGetComponent<WeatherController>(out var weather))
                weather = host.AddComponent<WeatherController>();
            else if (!weather.enabled) weather.enabled = true;
            return weather;
        }

        public static string DisplayName(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Sunny: return "Soleado";
                case WeatherKind.Cloudy: return "Nublado";
                case WeatherKind.Fog: return "Niebla";
                case WeatherKind.Rain: return "Lluvia";
                case WeatherKind.Sunset: return "Atardecer";
                case WeatherKind.Night: return "Noche";
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown weather.");
            }
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            if (_runtimeRoot == null) CreateResources();
            _environment.Capture();
            _ownsEnvironment = true;
            _volume.enabled = true;
            BindCamera(Camera.main);
            ApplyVisuals();
            _rain.SetVisible(true);
        }

        void CreateResources()
        {
            _runtimeRoot = new GameObject("Weather Runtime") { hideFlags = HideFlags.DontSave, layer = 0 };
            _runtimeRoot.transform.SetParent(transform, false);
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "Weather Grade (runtime)";
            _profile.hideFlags = HideFlags.DontSave;
            _grade = _profile.Add<ColorAdjustments>(true);
            _grade.hideFlags = HideFlags.DontSave;
            // No lift, negative contrast, bloom, DoF or vignette: black stays black and
            // pixel-art edges stay sharp. ScreenSpaceOverlay UI is rendered after this.
            _volume = _runtimeRoot.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.weight = 1f;
            _volume.sharedProfile = _profile; // Owned runtime profile; never mutate a project asset.
            _rain = new WeatherRain(_runtimeRoot.transform);
        }

        void Update()
        {
            ReadInput();
            Advance(Time.deltaTime);
        }

        internal void ReadInput()
        {
            if (!isActiveAndEnabled || !GameFlow.Playing || Time.timeScale <= 0f ||
                Keyboard.current?.mKey.wasPressedThisFrame != true || IsEditingText()) return;
            NextWeather();
        }

        static bool IsEditingText()
        {
            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            return selected != null && (selected.GetComponentInParent<TMP_InputField>() != null ||
                selected.GetComponentInParent<InputField>() != null);
        }

        /// <summary>Programmatic selections can be queued in pause; visuals stay frozen until gameplay resumes.</summary>
        public void SetWeather(WeatherKind kind)
        {
            if (!_transition.Set(kind, transitionSeconds)) return;
            Changed?.Invoke();
        }

        public void NextWeather() => SetWeather(WeatherTransition.Next(Current));

        /// <summary>Call from ShowMainMenu, not from Pause/Resume or round transitions.</summary>
        public void ResetWeather()
        {
            bool changed = Current != WeatherKind.Sunny || IsTransitioning;
            _transition.Reset();
            if (_ownsEnvironment) ApplyVisuals();
            _rain?.Clear();
            if (changed) Changed?.Invoke();
        }

        internal void Advance(float deltaTime)
        {
            if (!_ownsEnvironment || !isActiveAndEnabled || !GameFlow.Playing || Time.timeScale <= 0f ||
                deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            bool wasTransitioning = IsTransitioning;
            _transition.Advance(deltaTime, true);
            if (wasTransitioning) ApplyVisuals();
            // Camera.main is a tagged lookup. Refresh at most once per second for camera swaps;
            // actor/world references use their existing singletons, never global renderer scans.
            if (Time.unscaledTime >= _cameraRetryAt)
            {
                BindCamera(Camera.main);
                _cameraRetryAt = Time.unscaledTime + 1f;
            }
            if (_transition.Value.Rain <= .0001f)
            {
                _rain.Clear();
                return;
            }
            var world = WorldTerrain.Instance;
            var player = PlayerSphereController.Instance;
            Vector3 centre;
            if (player != null) centre = player.transform.position;
            else if (world != null && world.Player != null) centre = world.Player.position;
            else if (_camera != null)
            {
                var ray = _camera.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
                float height = world != null ? world.transform.position.y : 0f;
                var plane = new Plane(Vector3.up, Vector3.up * height);
                centre = plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : _camera.transform.position;
            }
            else return;
            float radius = _camera != null && _camera.orthographic
                ? Mathf.Clamp(_camera.orthographicSize * Mathf.Max(1f, _camera.aspect) * 1.25f, 10f, 24f)
                : 16f;
            _rain.Advance(deltaTime, _transition.Value.Rain, centre, radius, world != null ? world.Traversal : null);
        }

        void ApplyVisuals()
        {
            WeatherStyle style = _transition.Value;
            _grade.postExposure.value = style.Exposure;
            _grade.colorFilter.value = style.Filter;
            _grade.saturation.value = style.Saturation;
            _environment.Apply(style);
        }

        void BindCamera(Camera camera)
        {
            if (camera == _camera && _cameraData != null) return;
            RestoreCamera();
            _camera = camera;
            if (camera == null) return;
            if (!camera.TryGetComponent(out _cameraData))
            {
                _cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                _createdCameraData.Add(_cameraData);
            }
            _previousPostProcessing = _cameraData.renderPostProcessing;
            _previousVolumeMask = _cameraData.volumeLayerMask;
            _previousUpdateMode = camera.GetVolumeFrameworkUpdateMode();
            _cameraData.renderPostProcessing = true;
            _cameraData.volumeLayerMask = _previousVolumeMask | (1 << _runtimeRoot.layer);
            camera.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.EveryFrame);
        }

        void RestoreCamera()
        {
            if (_cameraData != null)
            {
                _cameraData.renderPostProcessing = _previousPostProcessing;
                _cameraData.volumeLayerMask = _previousVolumeMask;
                if (_camera != null) _camera.SetVolumeFrameworkUpdateMode(_previousUpdateMode);
            }
            _cameraData = null;
            _camera = null;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (!_ownsEnvironment) return;
            _ownsEnvironment = false;
            _environment.Restore();
            if (_volume != null) _volume.enabled = false;
            // ViaScripting restoration can refresh the camera's volume stack immediately.
            // Unregister our volume first so that stack cannot retain the weather grade.
            RestoreCamera();
            _rain?.SetVisible(false);
        }

        void OnDestroy()
        {
            OnDisable();
            _rain?.Dispose();
            _rain = null;
            if (_volume != null) _volume.sharedProfile = null;
            if (_profile != null)
            {
                foreach (var component in _profile.components) WeatherRain.DestroyOwned(component);
                _profile.components.Clear();
                WeatherRain.DestroyOwned(_profile);
            }
            foreach (var data in _createdCameraData) WeatherRain.DestroyOwned(data);
            _createdCameraData.Clear();
            WeatherRain.DestroyOwned(_runtimeRoot);
            Changed = null;
        }
    }
}

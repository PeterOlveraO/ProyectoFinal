using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Terrain;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Weather.Tests
{
    /// <summary>Empty-scene runtime contracts; no menu bootstrapping, progression or disk saves.</summary>
    public sealed class WeatherRuntimeTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> _objects = new List<GameObject>();
        readonly WeatherEnvironment _originalEnvironment = new WeatherEnvironment();
        Camera _camera;
        Light _sun, _originalSun;
        Keyboard _keyboard, _previousKeyboard;
        WeatherController _weather;
        bool _blocked, _over;
        float _timeScale;

        [SetUp]
        public void SetUp()
        {
            Assert.That(WeatherController.Instance, Is.Null, "Requires an empty PlayMode test scene.");
            Assert.That(Camera.main, Is.Null);
            Assert.That(EventSystem.current, Is.Null);
            _blocked = GameFlow.Blocked;
            _over = GameFlow.Over;
            _timeScale = Time.timeScale;
            _originalSun = RenderSettings.sun;
            _originalEnvironment.Capture();
            GameFlow.Blocked = GameFlow.Over = false;
            Time.timeScale = 1f;
            _sun = Create("WeatherTestSun").AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.color = new Color(.9f, .8f, .7f);
            _sun.intensity = 1.3f;
            _sun.transform.rotation = Quaternion.Euler(45f, 25f, 0f);
            RenderSettings.sun = _sun;
            RenderSettings.fog = false;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.2f, .3f, .4f);
            RenderSettings.fogDensity = .013f;
            RenderSettings.fogStartDistance = 11f;
            RenderSettings.fogEndDistance = 77f;
            _camera = Create("WeatherTestCamera").AddComponent<Camera>();
            _camera.tag = "MainCamera";
            _camera.orthographic = true;
            _camera.transform.SetPositionAndRotation(new Vector3(0f, 10f, -10f), Quaternion.Euler(45f, 0f, 0f));
            _previousKeyboard = Keyboard.current;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _keyboard.MakeCurrent();
        }

        [TearDown]
        public void TearDown()
        {
            // Restoring globals is immediate in OnDisable; owned native resources destroy at end of frame.
            if (_weather != null) Object.DestroyImmediate(_weather.gameObject);
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            _originalEnvironment.Restore();
            RenderSettings.sun = _originalSun;
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            if (_previousKeyboard != null && _previousKeyboard.added) _previousKeyboard.MakeCurrent();
            GameFlow.Blocked = _blocked;
            GameFlow.Over = _over;
            Time.timeScale = _timeScale;
        }

        WeatherController StartWeather()
        {
            _weather = WeatherController.EnsureExists(Create("WeatherTestHost"));
            return _weather;
        }

        [Test]
        public void EnsureExistsIsIdempotentAndDuplicateCannotStealTheSingleton()
        {
            StartWeather();
            var other = Create("AnotherHost");
            Assert.That(WeatherController.EnsureExists(other), Is.SameAs(_weather));
            Assert.That(other.GetComponent<WeatherController>(), Is.Null);
            var duplicate = other.AddComponent<WeatherController>();
            Assert.That(duplicate.enabled, Is.False);
            Assert.That(WeatherController.Instance, Is.SameAs(_weather));
            Assert.That(_weather.GetComponentsInChildren<Volume>().Length, Is.EqualTo(1));
            Object.DestroyImmediate(duplicate);
            Assert.That(WeatherController.Instance, Is.SameAs(_weather));
        }

        [Test]
        public void RealMKeyCyclesOnPressNotHoldAndNotifiesOncePerSelection()
        {
            StartWeather();
            int notifications = 0;
            _weather.Changed += () => notifications++;
            foreach (var kind in new[] { WeatherKind.Cloudy, WeatherKind.Fog, WeatherKind.Rain,
                         WeatherKind.Sunset, WeatherKind.Night, WeatherKind.Sunny })
            {
                KeyState(false);
                KeyState(true);
                Assert.That(_weather.Current, Is.EqualTo(kind));
                // A subsequent Input System update with M still down is not another press.
                KeyState(true);
                Assert.That(_weather.Current, Is.EqualTo(kind));
            }
            _weather.SetWeather(WeatherKind.Sunny);
            Assert.That(notifications, Is.EqualTo(6));
        }

        [TestCase("blocked")]
        [TestCase("over")]
        [TestCase("timeScale")]
        [TestCase("legacyField")]
        [TestCase("tmpField")]
        public void MIsIgnoredOutsideGameplayOrWhileEditingText(string reason)
        {
            StartWeather();
            switch (reason)
            {
                case "blocked": GameFlow.Blocked = true; break;
                case "over": GameFlow.Over = true; break;
                case "timeScale": Time.timeScale = 0f; break;
                default:
                    var eventSystem = Create("TestEventSystem").AddComponent<EventSystem>();
                    var field = Create("TextField", typeof(RectTransform));
                    if (reason == "tmpField") field.AddComponent<TMP_InputField>();
                    else field.AddComponent<InputField>();
                    var child = Create("SelectedText", typeof(RectTransform));
                    child.transform.SetParent(field.transform, false);
                    eventSystem.SetSelectedGameObject(child);
                    break;
            }
            KeyState(true);
            Assert.That(_weather.Current, Is.EqualTo(WeatherKind.Sunny));
            // Removing the blocker with M still held must not replay the blocked press.
            GameFlow.Blocked = GameFlow.Over = false;
            Time.timeScale = 1f;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            KeyState(true);
            Assert.That(_weather.Current, Is.EqualTo(WeatherKind.Sunny));
            KeyState(false);
            KeyState(true);
            Assert.That(_weather.Current, Is.EqualTo(WeatherKind.Cloudy));
        }

        [TestCase(true, 1f)]
        [TestCase(false, 0f)]
        public void PauseFreezesTransitionParticleAgesAndPositions(bool blocked, float timeScale)
        {
            StartWeather();
            _weather.SetWeather(WeatherKind.Rain);
            _weather.Advance(1.2f);
            var rain = Field<WeatherRain>(_weather, "_rain");
            var before = new ParticleSystem.Particle[WeatherRain.ParticleBudget];
            int count = rain.System.GetParticles(before);
            Assert.That(count, Is.GreaterThan(0));
            float progress = _weather.TransitionProgress;
            float fog = RenderSettings.fogDensity;
            GameFlow.Blocked = blocked;
            Time.timeScale = timeScale;
            _weather.Advance(5f);
            var after = new ParticleSystem.Particle[WeatherRain.ParticleBudget];
            Assert.That(rain.System.GetParticles(after), Is.EqualTo(count));
            Assert.That(_weather.TransitionProgress, Is.EqualTo(progress));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(fog));
            for (int i = 0; i < count; i++)
            {
                Assert.That(after[i].remainingLifetime, Is.EqualTo(before[i].remainingLifetime));
                Assert.That(after[i].position, Is.EqualTo(before[i].position));
            }
            Assert.That(rain.System.isPlaying, Is.False, "Only WeatherController may advance particle time.");
            GameFlow.Blocked = false;
            Time.timeScale = 1f;
            _weather.Advance(.1f);
            Assert.That(_weather.Current, Is.EqualTo(WeatherKind.Rain));
            Assert.That(_weather.TransitionProgress, Is.GreaterThan(progress));
            rain.System.GetParticles(after);
            Assert.That(after[0].remainingLifetime, Is.LessThan(before[0].remainingLifetime));
        }

        [UnityTest]
        public IEnumerator ParticleSystemDoesNotAdvanceItselfWhileGameFlowIsBlockedAtTimeScaleOne()
        {
            StartWeather();
            _weather.SetWeather(WeatherKind.Rain);
            _weather.Advance(3f);
            var rain = Field<WeatherRain>(_weather, "_rain");
            var particles = new ParticleSystem.Particle[WeatherRain.ParticleBudget];
            int count = rain.System.GetParticles(particles);
            Assert.That(count, Is.GreaterThan(0));
            float lifetime = particles[0].remainingLifetime;
            GameFlow.Blocked = true;
            yield return null;
            yield return null;
            Assert.That(rain.System.GetParticles(particles), Is.EqualTo(count));
            Assert.That(particles[0].remainingLifetime, Is.EqualTo(lifetime));
        }

        [Test]
        public void NightGradesUnlitSpritesAsWellAsLightingAndResetWorksInPause()
        {
            StartWeather();
            _weather.SetWeather(WeatherKind.Night);
            _weather.Advance(3f);
            var profile = Field<VolumeProfile>(_weather, "_profile");
            Assert.That(profile.TryGet(out ColorAdjustments grade), Is.True);
            Assert.That(grade.postExposure.overrideState, Is.True);
            Assert.That(grade.postExposure.value, Is.LessThan(-.7f).And.GreaterThan(-1.5f));
            Assert.That(grade.colorFilter.overrideState, Is.True);
            Assert.That(grade.colorFilter.value.b, Is.GreaterThan(grade.colorFilter.value.r));
            Assert.That(grade.contrast.value, Is.Zero, "Negative contrast would lift WorldZoneFog's black.");
            Assert.That(_camera.GetUniversalAdditionalCameraData().renderPostProcessing, Is.True);
            Assert.That(_sun.intensity, Is.GreaterThan(.5f).And.LessThan(1.3f));
            float intensity = _sun.intensity;
            _weather.Advance(10f);
            Assert.That(_sun.intensity, Is.EqualTo(intensity), "Do not compound multipliers every frame.");
            GameFlow.Blocked = true;
            _weather.ResetWeather();
            Assert.That(_weather.Current, Is.EqualTo(WeatherKind.Sunny));
            Assert.That(_sun.intensity, Is.EqualTo(1.3f));
            Assert.That(grade.postExposure.value, Is.Zero);
            Assert.That(RenderSettings.fog, Is.False);
        }

        [Test]
        public void ExplicitMenuResetClearsPrecipitationWithoutDiscardingWeatherOnPause()
        {
            StartWeather();
            _weather.SetWeather(WeatherKind.Rain);
            _weather.Advance(3f);
            var rain = Field<WeatherRain>(_weather, "_rain");
            Assert.That(rain.System.particleCount, Is.GreaterThan(0));
            GameFlow.Blocked = true;
            _weather.Advance(30f);
            Assert.That(_weather.Current, Is.EqualTo(WeatherKind.Rain));
            _weather.SetWeather(WeatherKind.Sunset); // Test-menu request while paused queues a target.
            Assert.That(_weather.VisualState.Rain, Is.EqualTo(1f));
            _weather.ResetWeather();
            Assert.That(rain.System.particleCount, Is.Zero);
            Assert.That(_weather.IsTransitioning, Is.False);
            Assert.That(_weather.Current, Is.EqualTo(WeatherKind.Sunny));
        }

        [UnityTest]
        public IEnumerator DisableRestoresBorrowedCameraAndSunAndDestroyReleasesOwnedNativeObjects()
        {
            var data = _camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.volumeLayerMask = 1 << 4;
            _camera.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.ViaScripting);
            Quaternion rotation = _sun.transform.rotation;
            Color color = _sun.color;
            Color ambient = RenderSettings.ambientLight;
            float ambientIntensity = RenderSettings.ambientIntensity;
            StartWeather();
            _weather.SetWeather(WeatherKind.Sunset);
            _weather.Advance(3f);
            var volume = _weather.GetComponentInChildren<Volume>();
            var profile = Field<VolumeProfile>(_weather, "_profile");
            var grade = profile.components[0];
            var rain = Field<WeatherRain>(_weather, "_rain");
            var system = rain.System;
            var material = rain.Material;
            _weather.enabled = false;
            Assert.That(data.renderPostProcessing, Is.False);
            Assert.That((int)data.volumeLayerMask, Is.EqualTo(1 << 4));
            Assert.That(_camera.GetVolumeFrameworkUpdateMode(), Is.EqualTo(VolumeFrameworkUpdateMode.ViaScripting));
            Assert.That(volume.enabled, Is.False);
            Assert.That(_sun.intensity, Is.EqualTo(1.3f));
            Assert.That(_sun.color, Is.EqualTo(color));
            Assert.That(_sun.transform.rotation, Is.EqualTo(rotation));
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(ambient));
            Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(ambientIntensity));
            Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.Linear));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(.013f));
            Assert.That(RenderSettings.fogStartDistance, Is.EqualTo(11f));
            Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(77f));
            // Re-enable uses the same resources and retained weather, with a new baseline snapshot.
            _weather.enabled = true;
            Assert.That(Field<VolumeProfile>(_weather, "_profile"), Is.SameAs(profile));
            Assert.That(_weather.Current, Is.EqualTo(WeatherKind.Sunset));
            Object.Destroy(_weather);
            yield return null;
            yield return null;
            Assert.That(WeatherController.Instance, Is.Null);
            Assert.That(profile == null && grade == null && material == null && system == null, Is.True);
            Assert.That(volume == null, Is.True);
            Assert.That(_sun != null && data != null, Is.True, "Never destroy borrowed scene objects.");
            Assert.That(_sun.color, Is.EqualTo(color));
            Assert.That(data.renderPostProcessing, Is.False);
        }

        [UnityTest]
        public IEnumerator CameraDataCreatedByWeatherIsRemovedOnDestroy()
        {
            Assert.That(_camera.GetComponent<UniversalAdditionalCameraData>(), Is.Null);
            StartWeather();
            var data = _camera.GetComponent<UniversalAdditionalCameraData>();
            Assert.That(data, Is.Not.Null);
            Object.Destroy(_weather);
            // Stop the camera rendering: URP is otherwise allowed to recreate its own additional data.
            _camera.enabled = false;
            yield return null;
            yield return null;
            Assert.That(data == null, Is.True);
            Assert.That(_camera != null, Is.True);
        }

        [Test]
        public void ResourceShaderAndParticleBudgetSurviveLongRainAndMaskTracksPurchases()
        {
            StartWeather();
            var rain = Field<WeatherRain>(_weather, "_rain");
            Assert.That(Resources.Load<Shader>(WeatherRain.ShaderResource), Is.Not.Null);
            Assert.That(rain.Material.shader.name, Is.EqualTo("Game/Weather/LocalRain"));
            Assert.That(rain.Material.renderQueue, Is.GreaterThan(2499).And.LessThan(4000));
            var settings = ScriptableObject.CreateInstance<TerrainSettings>();
            try
            {
                var map = new HeightMap(32, 32);
                var access = new ZoneAccess();
                var traversal = new WorldTraversal(map, settings, Vector3.zero, access);
                for (int i = 0; i < 100; i++)
                    rain.Advance(.1f, 1f, new Vector3(16f, 0f, 16f), 12f, traversal);
                Assert.That(rain.System.particleCount, Is.GreaterThan(0).And.LessThanOrEqualTo(WeatherRain.ParticleBudget));
                Assert.That(rain.Material.GetInt("_ClosedCount"), Is.EqualTo(8));
                access.UnlockedMask |= 1 << 1;
                rain.Advance(.1f, 1f, new Vector3(16f, 0f, 16f), 12f, traversal);
                Assert.That(rain.Material.GetInt("_ClosedCount"), Is.EqualTo(7));
                access.UnlockedMask = WorldZoneLayout.AllUnlockedMask;
                rain.Advance(.1f, 1f, Vector3.zero, 12f, traversal);
                Assert.That(rain.Material.GetInt("_ClosedCount"), Is.Zero);
                Assert.That(rain.Material.GetFloat("_HasWorldBounds"), Is.EqualTo(1f));
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void RainShaderBrightensVisibleGroundButCannotPaintOverBlackFog()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Requires a graphics device; run with the Editor/player graphics enabled.");
            Shader shader = Resources.Load<Shader>(WeatherRain.ShaderResource);
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
            var material = new Material(shader);
            var mesh = new Mesh();
            var target = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(8, 8, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            var commands = new CommandBuffer { name = "Weather black-preservation contract" };
            try
            {
                target.Create();
                mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0),
                    new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                // Probe a full-strength drop fragment, independent of streak coverage.
                mesh.uv = new[] { Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f, Vector2.one * .5f };
                material.SetFloat("_Intensity", 1f);
                material.SetVector("_LocalArea", new Vector4(0f, 0f, 100f, 0f));
                material.SetFloat("_HasWorldBounds", 0f);
                material.SetInt("_ClosedCount", 0);
                Color black = DrawRainProbe(Color.black, material, mesh, target, readback, commands);
                Assert.That(Mathf.Max(black.r, black.g, black.b), Is.LessThanOrEqualTo(1f / 255f));
                Color ground = DrawRainProbe(new Color(.25f, .25f, .25f), material, mesh, target, readback, commands);
                Assert.That(ground.r, Is.GreaterThan(.4f).And.LessThan(.6f),
                    "The black test must not pass merely because the shader did not draw.");
                // The same fragment is rejected inside a closed purchase territory even over lit pixels.
                material.SetVectorArray("_ClosedRects", new Vector4[9]
                    { new Vector4(-2, -2, 2, 2), default, default, default, default, default, default, default, default });
                material.SetInt("_ClosedCount", 1);
                Color closed = DrawRainProbe(new Color(.25f, .25f, .25f), material, mesh, target, readback, commands);
                Assert.That(closed.r, Is.EqualTo(.25f).Within(.015f));
            }
            finally
            {
                RenderTexture.active = previous;
                commands.Release();
                target.Release();
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
            }
        }

        static Color DrawRainProbe(Color background, Material material, Mesh mesh, RenderTexture target,
            Texture2D readback, CommandBuffer commands)
        {
            commands.Clear();
            commands.SetRenderTarget(target);
            commands.SetViewport(new Rect(0, 0, 8, 8));
            commands.ClearRenderTarget(false, true, background);
            commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, 0);
            Graphics.ExecuteCommandBuffer(commands);
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, 8, 8), 0, 0, false);
            readback.Apply(false, false);
            return readback.GetPixel(4, 4);
        }

        sealed class ZoneAccess : IWorldZoneAccess
        {
            public int Revision => UnlockedMask;
            public int UnlockedMask { get; set; } = 1;
            public bool IsUnlocked(int id) => id >= 1 && id <= 9 && (UnlockedMask & (1 << (id - 1))) != 0;
        }

        void KeyState(bool pressed)
        {
            InputSystem.QueueStateEvent(_keyboard, pressed ? new KeyboardState(Key.M) : new KeyboardState());
            InputSystem.Update();
            _weather.ReadInput();
        }

        GameObject Create(string name, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            _objects.Add(go);
            return go;
        }

        static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    }
}

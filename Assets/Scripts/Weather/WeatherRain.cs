using System;
using Game.Terrain;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Weather
{
    /// <summary>
    /// One local, manually simulated emitter: at most 160 particles, no physics/collision,
    /// shadows, lights, textures, scene scans or allocations in the frame loop.
    /// </summary>
    internal sealed class WeatherRain : IDisposable
    {
        public const string ShaderResource = "Weather/LocalRain";
        public const int ParticleBudget = 160;
        readonly System.Random _random = new System.Random(73013);
        readonly Vector4[] _closedRects = new Vector4[WorldZoneLayout.ZoneCount];
        readonly ParticleSystem _system;
        readonly ParticleSystemRenderer _renderer;
        readonly Material _material;
        WorldTraversal _traversal;
        int _lastMask = -1;
        float _emission;
        bool _clear = true;
        static readonly int Intensity = Shader.PropertyToID("_Intensity");
        static readonly int ClosedRects = Shader.PropertyToID("_ClosedRects");
        static readonly int ClosedCount = Shader.PropertyToID("_ClosedCount");
        static readonly int WorldBounds = Shader.PropertyToID("_WorldBounds");
        static readonly int HasWorldBounds = Shader.PropertyToID("_HasWorldBounds");
        static readonly int LocalArea = Shader.PropertyToID("_LocalArea");
        internal ParticleSystem System => _system;
        internal Material Material => _material;

        public WeatherRain(Transform parent)
        {
            // Loading a Shader asset from Resources keeps it in a player build without Shader.Find.
            Shader shader = Resources.Load<Shader>(ShaderResource);
            if (shader == null)
            {
                Debug.LogError("[Weather] Falta Resources/Weather/LocalRain.shader.");
                return;
            }
            _material = new Material(shader) { name = "Weather Rain (runtime)", hideFlags = HideFlags.DontSave };
            var go = new GameObject("Local Weather Rain") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            _system = go.AddComponent<ParticleSystem>();
            _system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = false;
            main.maxParticles = ParticleBudget;
            main.startLifetime = .72f;
            main.startSpeed = 0f;
            main.startSize = .055f;
            main.startColor = new Color(.70f, .84f, 1f, .70f);
            var emission = _system.emission;
            emission.enabled = false;
            var shape = _system.shape;
            shape.enabled = false;
            _renderer = go.GetComponent<ParticleSystemRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.renderMode = ParticleSystemRenderMode.Stretch;
            _renderer.velocityScale = .035f;
            _renderer.lengthScale = 3f;
            _renderer.maxParticleSize = .035f;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.enabled = false;
            _system.Simulate(0f, false, true, false);
        }

        public void Advance(float deltaTime, float amount, Vector3 centre, float radius, WorldTraversal traversal)
        {
            if (_system == null) return;
            if (amount <= .0001f) { Clear(); return; }
            UpdateMask(traversal);
            _material.SetVector(LocalArea, new Vector4(centre.x, centre.z, radius, 0f));
            _material.SetFloat(Intensity, amount);
            _renderer.enabled = amount > .0001f;
            _clear = false;
            _system.transform.position = centre;
            // Bound catch-up after a stall. Simulate leaves the emitter paused: there is never
            // an autonomous ParticleSystem update advancing time while GameFlow is blocked.
            float step = Mathf.Min(deltaTime, .25f);
            _system.Simulate(step, false, false, false);
            _emission += step * 150f * amount;
            int count = Mathf.Min(32, Mathf.FloorToInt(_emission));
            _emission -= Mathf.Floor(_emission);
            for (int i = 0; i < count; i++)
            {
                var emit = new ParticleSystem.EmitParams
                {
                    position = centre + new Vector3(RandomSigned() * radius, 8f, RandomSigned() * radius),
                    velocity = new Vector3(.9f, -17f, .25f),
                    startLifetime = .66f + (float)_random.NextDouble() * .12f
                };
                _system.Emit(emit, 1);
            }
            _system.Pause(false);
        }

        float RandomSigned() => (float)_random.NextDouble() * 2f - 1f;

        void UpdateMask(WorldTraversal traversal)
        {
            int mask = traversal == null ? WorldZoneLayout.AllUnlockedMask : traversal.UnlockedMask;
            if (ReferenceEquals(traversal, _traversal) && mask == _lastMask) return;
            _traversal = traversal;
            _lastMask = mask;
            int count = 0;
            if (traversal != null)
            {
                for (int zone = 1; zone <= WorldZoneLayout.ZoneCount; zone++)
                {
                    if (traversal.IsUnlocked(zone)) continue;
                    Rect rect = traversal.Layout.Bounds(zone);
                    _closedRects[count++] = new Vector4(rect.xMin, rect.yMin, rect.xMax, rect.yMax);
                }
                var layout = traversal.Layout;
                _material.SetVector(WorldBounds, new Vector4(layout.Origin.x, layout.Origin.z,
                    layout.Origin.x + layout.Width, layout.Origin.z + layout.Depth));
            }
            _material.SetFloat(HasWorldBounds, traversal == null ? 0f : 1f);
            _material.SetVectorArray(ClosedRects, _closedRects);
            _material.SetInt(ClosedCount, count);
        }

        public void SetVisible(bool visible)
        {
            if (_renderer != null) _renderer.enabled = visible && !_clear;
        }

        public void Clear()
        {
            if (_clear) return;
            if (_system != null && !_clear) _system.Clear(false);
            if (_renderer != null) _renderer.enabled = false;
            _emission = 0f;
            _clear = true;
        }

        public void Dispose()
        {
            if (_system != null)
            {
                _system.gameObject.SetActive(false);
                DestroyOwned(_system.gameObject);
            }
            DestroyOwned(_material);
        }

        internal static void DestroyOwned(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }
}

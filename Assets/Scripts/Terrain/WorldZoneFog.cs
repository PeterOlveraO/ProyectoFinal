using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Terrain
{
    /// <summary>
    /// Ground-following black fog using the existing surface meshes. Props are hidden
    /// separately; forceRenderingOff composes with the frustum culler's renderer.enabled.
    /// </summary>
    public sealed class WorldZoneFog : MonoBehaviour
    {
        struct Prop
        {
            public Renderer Renderer;
            public int Zone;
            public bool WasForcedOff;
        }
        readonly List<Prop> _props = new List<Prop>();
        readonly HashSet<Renderer> _registeredProps = new HashSet<Renderer>();
        readonly HashSet<Renderer> _surfaces = new HashSet<Renderer>();
        readonly List<MeshRenderer> _overlays = new List<MeshRenderer>();
        readonly Vector4[] _rects = new Vector4[9];
        WorldTerrain _terrain;
        Material _material;
        int _lastMask = -1;
        float _revealAlpha;
        static readonly int ClosedRects = Shader.PropertyToID("_ClosedRects");
        static readonly int ClosedCount = Shader.PropertyToID("_ClosedCount");
        static readonly int RevealRect = Shader.PropertyToID("_RevealRect");
        static readonly int RevealAlpha = Shader.PropertyToID("_RevealAlpha");
        public int RegisteredPropCount => _props.Count;
        public int SurfaceCount => _surfaces.Count;

        public void Initialize(WorldTerrain terrain)
        {
            _terrain = terrain;
            Material source = terrain.Settings.worldZoneFogMaterial;
            if (source == null) source = Resources.Load<Material>("WorldZoneFog");
            if (source == null)
            {
                Debug.LogError("[WorldZoneFog] Falta Resources/WorldZoneFog o TerrainSettings.worldZoneFogMaterial.", this);
                return;
            }
            _material = new Material(source) { name = "RunWorldZoneFog" };
            _material.SetFloat("_Feather", 2f);
            Refresh();
        }

        public void RegisterSurface(Renderer surface)
        {
            if (_material == null || surface == null || !_surfaces.Add(surface)) return;
            var filter = surface.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            var go = new GameObject("ZoneFogSurface");
            go.transform.SetParent(surface.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var overlay = go.AddComponent<MeshRenderer>();
            var materials = new Material[Mathf.Max(1, filter.sharedMesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++) materials[i] = _material;
            overlay.sharedMaterials = materials;
            overlay.shadowCastingMode = ShadowCastingMode.Off;
            overlay.receiveShadows = false;
            overlay.lightProbeUsage = LightProbeUsage.Off;
            overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
            overlay.enabled = _lastMask != WorldZoneLayout.AllUnlockedMask;
            _overlays.Add(overlay);
        }

        /// <summary>Call when building a prop, using its ground/root anchor, not its elevated bounds centre.</summary>
        public void RegisterProp(Renderer renderer, Vector3 groundPosition)
        {
            if (renderer == null || _terrain == null || !_registeredProps.Add(renderer)) return;
            int zone = _terrain.Traversal.ZoneAt(groundPosition);
            var prop = new Prop { Renderer = renderer, Zone = zone, WasForcedOff = renderer.forceRenderingOff };
            _props.Add(prop);
            renderer.forceRenderingOff = prop.WasForcedOff || !_terrain.Traversal.IsUnlocked(zone);
        }

        public void UnregisterProp(Renderer renderer)
        {
            if (!_registeredProps.Remove(renderer)) return;
            for (int i = _props.Count - 1; i >= 0; i--)
            {
                if (_props[i].Renderer != renderer) continue;
                if (renderer != null) renderer.forceRenderingOff = _props[i].WasForcedOff;
                _props.RemoveAt(i);
                return;
            }
        }

        /// <summary>Bounded full refresh only on purchases/reset; no scene-wide per-frame renderer scans.</summary>
        public void Refresh()
        {
            if (_terrain == null || _terrain.Traversal == null) return;
            int mask = _terrain.Traversal.UnlockedMask;
            if (mask == _lastMask) return;
            int opened = _lastMask < 0 ? 0 : mask & ~_lastMask;
            _revealAlpha = opened == 0 ? 0 : 1;
            if (_material != null)
            {
                _material.SetFloat(RevealAlpha, _revealAlpha);
                for (int id = 1; id <= 9; id++)
                {
                    if ((opened & (1 << (id - 1))) == 0) continue;
                    Rect reveal = _terrain.Traversal.Layout.Bounds(id);
                    _material.SetVector(RevealRect, new Vector4(reveal.xMin, reveal.yMin, reveal.xMax, reveal.yMax));
                    break;
                }
            }
            _lastMask = mask;
            int count = 0;
            for (int id = 1; id <= 9; id++)
            {
                if (_terrain.Traversal.IsUnlocked(id)) continue;
                Rect bounds = _terrain.Traversal.Layout.Bounds(id);
                _rects[count++] = new Vector4(bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax);
            }
            if (_material != null)
            {
                _material.SetVectorArray(ClosedRects, _rects);
                _material.SetInt(ClosedCount, count);
            }
            for (int i = _props.Count - 1; i >= 0; i--)
            {
                Prop prop = _props[i];
                if (prop.Renderer == null)
                {
                    _registeredProps.Remove(prop.Renderer);
                    _props.RemoveAt(i);
                    continue;
                }
                prop.Renderer.forceRenderingOff = prop.WasForcedOff || !_terrain.Traversal.IsUnlocked(prop.Zone);
            }
            foreach (var overlay in _overlays) if (overlay != null) overlay.enabled = count > 0 || _revealAlpha > 0;
        }

        void LateUpdate()
        {
            Refresh();
            if (_revealAlpha <= 0 || _material == null) return;
            _revealAlpha = Mathf.Max(0, _revealAlpha - Time.deltaTime / .35f);
            _material.SetFloat(RevealAlpha, _revealAlpha);
            if (_revealAlpha == 0 && _lastMask == WorldZoneLayout.AllUnlockedMask)
                foreach (var overlay in _overlays) if (overlay != null) overlay.enabled = false;
        }
        void OnDestroy()
        {
            foreach (var prop in _props)
                if (prop.Renderer != null) prop.Renderer.forceRenderingOff = prop.WasForcedOff;
            foreach (var overlay in _overlays)
                if (overlay != null) Destroy(overlay.gameObject);
            if (_material != null) Destroy(_material);
        }
    }
}

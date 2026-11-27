using Game.Pokemon;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>Presentation only. Trails remember world positions and never steer the projectile.</summary>
    internal sealed class ShotFlightVisual
    {
        const int HistorySize = 16;
        const float SampleInterval = .025f;
        readonly Transform _root;
        readonly Transform _anchor;
        readonly SpriteRenderer _elementVisual;
        readonly SpriteRenderer _pattern;
        readonly SpriteRenderer[] _echoes = new SpriteRenderer[3];
        readonly SpriteRenderer[] _motes = new SpriteRenderer[2];
        readonly Vector3[] _history = new Vector3[HistorySize];
        readonly Quaternion[] _rotations = new Quaternion[HistorySize];
        readonly ShotType _type;
        readonly PokemonElement _element;
        readonly Color _tint;
        readonly Vector3 _bodyScale;
        Camera _camera;
        Vector3 _lastPosition;
        float _time;
        float _sampleTime;
        int _head;
        int _samples;

        internal ShotFlightVisual(Transform root, ShotType type, PokemonElement element, Color color, bool elemental)
        {
            _root = root;
            _type = type;
            _element = element;
            _tint = elemental ? PixelCombatSprites.ElementColor(element) : color;
            _camera = Camera.main;
            _anchor = new GameObject("VisualAnchor").transform;
            _anchor.SetParent(root, false);
            _pattern = Make("PatternVisual", _anchor, 18);
            _elementVisual = Make("ElementVisual", _anchor, 20);
            _elementVisual.color = elemental ? Color.white : color;
            _bodyScale = type == ShotType.Precision ? new Vector3(1.8f, .55f, 1) :
                type == ShotType.Piercing ? new Vector3(1.6f, .7f, 1) :
                type == ShotType.Burst ? new Vector3(1.15f, .7f, 1) :
                type == ShotType.Fan ? new Vector3(.95f, 1.35f, 1) : Vector3.one * 1.2f;
            for (int i = 0; i < _echoes.Length; i++)
            {
                _echoes[i] = Make("Afterimage" + i, root, 15 + i);
                _echoes[i].sprite = type == ShotType.Basic ? ShotAnimationAtlas.Element(element, 0)
                    : ShotAnimationAtlas.Pattern(type, 0);
                _echoes[i].enabled = false;
            }
            for (int i = 0; i < _motes.Length; i++)
                _motes[i] = Make("ElementMote" + i, _anchor, 19);
            _lastPosition = root.position;
            for (int i = 0; i < HistorySize; i++) _history[i] = root.position;
        }

        internal static SpriteRenderer Make(string name, Transform parent, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = PokemonSpriteMaterial.Shared;
            renderer.sortingOrder = order;
            return renderer;
        }

        internal void Tick(float deltaTime, Vector3 direction, bool returning)
        {
            _time += Mathf.Max(0f, deltaTime);
            if (_camera == null) _camera = Camera.main;
            Quaternion facing = _camera != null ? _camera.transform.rotation : Quaternion.identity;
            Vector3 view = Quaternion.Inverse(facing) * direction;
            float aim = view.x * view.x + view.y * view.y > .00001f
                ? Mathf.Atan2(view.y, view.x) * Mathf.Rad2Deg : 0f;
            float spin = _type == ShotType.Boomerang ? _time * (returning ? -900 : 900) :
                _type == ShotType.Explosive ? _time * 150 : 0f;
            _anchor.rotation = facing * Quaternion.Euler(0, 0, aim);
            int frame = (int)(_time * (_type == ShotType.Burst ? 22f : 16f));
            _elementVisual.sprite = ShotAnimationAtlas.Element(_element, frame);
            _pattern.sprite = ShotAnimationAtlas.Pattern(_type, frame);
            _pattern.color = Color.Lerp(_tint, Color.white, .38f);
            // A quick stretch on release, followed by a restrained living silhouette.
            float release = 1f + .22f * Mathf.Exp(-_time * 22f);
            float breathe = 1f + Mathf.Sin(_time * 18) * .045f;
            _pattern.transform.localScale = Vector3.Scale(_bodyScale, new Vector3(release, breathe, 1));
            _pattern.transform.localRotation = Quaternion.Euler(0, 0, spin);
            float coreSize = _type == ShotType.Basic ? 1.15f : _type == ShotType.Burst ? .86f : .7f;
            _elementVisual.transform.localScale = Vector3.Scale(_bodyScale, new Vector3(coreSize * release, coreSize * breathe, 1));
            _elementVisual.transform.localRotation = Quaternion.Euler(0, 0,
                _element == PokemonElement.Grass || _element == PokemonElement.Psychic ? _time * 240 : spin * .25f);

            for (int i = 0; i < _motes.Length; i++)
            {
                var mote = _motes[i];
                float phase = _time * 13 + i * Mathf.PI;
                bool orbit = _type == ShotType.Explosive || _type == ShotType.Boomerang ||
                    _element == PokemonElement.Psychic;
                mote.sprite = ShotAnimationAtlas.Element(_element, frame + i * 3);
                mote.transform.localPosition = orbit
                    ? new Vector3(Mathf.Cos(phase) * .45f, Mathf.Sin(phase) * .4f, 0)
                    : new Vector3(-.35f - i * .24f, Mathf.Sin(phase) * .19f, 0);
                mote.transform.localRotation = Quaternion.Euler(0, 0, phase * Mathf.Rad2Deg);
                mote.transform.localScale = Vector3.one * (orbit ? .23f : .19f);
                mote.color = new Color(1, 1, 1, .65f + Mathf.Sin(phase) * .15f);
            }

            _sampleTime += Mathf.Max(0, deltaTime);
            // Bounded even after a long frame. Interpolate samples rather than stamping one point.
            _sampleTime = Mathf.Min(_sampleTime, SampleInterval * HistorySize);
            while (_sampleTime >= SampleInterval)
            {
                _sampleTime -= SampleInterval;
                _head = (_head + 1) % HistorySize;
                _history[_head] = Vector3.Lerp(_lastPosition, _root.position,
                    deltaTime > 0 ? 1 - _sampleTime / deltaTime : 1);
                _rotations[_head] = _anchor.rotation * Quaternion.Euler(0, 0, spin);
                _samples = Mathf.Min(HistorySize, _samples + 1);
            }
            _lastPosition = _root.position;
            for (int i = 0; i < _echoes.Length; i++)
            {
                int delay = (i + 1) * 3;
                var echo = _echoes[i];
                echo.enabled = _samples > delay;
                if (!echo.enabled) continue;
                int index = (_head - delay + HistorySize) % HistorySize;
                echo.transform.SetPositionAndRotation(_history[index], _rotations[index]);
                echo.transform.localScale = _bodyScale * (1f - (i + 1) * .18f);
                echo.sprite = _type == ShotType.Basic
                    ? ShotAnimationAtlas.Element(_element, frame - delay)
                    : ShotAnimationAtlas.Pattern(_type, frame - delay);
                Color tint = _type == ShotType.Basic ? Color.white : _tint;
                tint.a = .30f - i * .075f;
                echo.color = tint;
            }
        }
    }
}

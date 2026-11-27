using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>Bounded, scene-local pool for release, impact, turn and dissipating pixel animations.</summary>
    [DisallowMultipleComponent]
    public sealed class HitBurst : MonoBehaviour
    {
        enum Beat { Impact, Muzzle, Turn, Dissipate }

        sealed class Spark
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public float Size;
            public float Spin;
        }

        const int MaxBursts = 32;
        const int MaxSparks = 6;
        static readonly List<HitBurst> Pool = new List<HitBurst>(MaxBursts);
        static int _sequence;
        Spark[] _sparks;
        SpriteRenderer _core;
        SpriteRenderer _ring;
        SpriteRenderer _accent;
        Quaternion _facing;
        Quaternion _aim;
        float _time;
        int _count;
        Color _color;
        float _life;
        bool _elemental;
        bool _patterned;
        PokemonElement _element;
        ShotType _type;
        Beat _beat;

        public static void Spawn(Vector3 position, Color color, int count = 9, float spread = 1f)
            => SpawnVisual(position, color, PokemonElement.Normal, count, spread, false);

        public static void SpawnElement(Vector3 position, PokemonElement element, int count = 6, float spread = 1f)
            => SpawnVisual(position, PixelCombatSprites.ElementColor(element), element, count, spread, true);

        public static void SpawnMuzzle(Vector3 position, PokemonElement element)
            => SpawnMuzzle(position, element, ShotType.Basic, Vector3.forward);

        public static void SpawnMuzzle(Vector3 position, PokemonElement element, ShotType type, Vector3 direction)
            => SpawnVisual(position, PixelCombatSprites.ElementColor(element), element, 2, .3f, true,
                true, type, direction, Beat.Muzzle);

        public static void SpawnImpact(Vector3 position, PokemonElement element, ShotType type, Vector3 direction)
            => SpawnVisual(position, PixelCombatSprites.ElementColor(element), element, 6,
                type == ShotType.Explosive ? ShotDefinitions.ExplosionRadius : .85f, true,
                true, type, direction, Beat.Impact);

        public static void SpawnTurn(Vector3 position, PokemonElement element, Vector3 direction)
            => SpawnVisual(position, PixelCombatSprites.ElementColor(element), element, 3, .35f, true,
                true, ShotType.Boomerang, direction, Beat.Turn);

        public static void SpawnDissipate(Vector3 position, PokemonElement element, ShotType type, Vector3 direction)
            => SpawnVisual(position, PixelCombatSprites.ElementColor(element), element, 3, .3f, true,
                true, type, direction, Beat.Dissipate);

        static void SpawnVisual(Vector3 position, Color color, PokemonElement element, int count, float spread,
            bool elemental, bool patterned = false, ShotType type = ShotType.Basic, Vector3 direction = default,
            Beat beat = Beat.Impact)
        {
            HitBurst burst = null;
            HitBurst oldest = null;
            for (int i = Pool.Count - 1; i >= 0; i--)
            {
                var candidate = Pool[i];
                if (candidate == null) { Pool.RemoveAt(i); continue; }
                if (!candidate.gameObject.activeSelf) { burst = candidate; break; }
                if (oldest == null || candidate._time / candidate._life > oldest._time / oldest._life) oldest = candidate;
            }
            if (burst == null && Pool.Count < MaxBursts)
            {
                var go = new GameObject("HitBurst");
                burst = go.AddComponent<HitBurst>();
                burst.CreateVisuals();
                Pool.Add(burst);
            }
            else if (burst == null) burst = oldest;
            burst.Play(position, color, element, count, spread, elemental, patterned, type, direction, beat);
            burst.gameObject.SetActive(true);
        }

        void CreateVisuals()
        {
            _ring = ShotFlightVisual.Make("ShockRing", transform, 26);
            _core = ShotFlightVisual.Make("Core", transform, 28);
            _accent = ShotFlightVisual.Make("ImpactSlash", transform, 29);
            _sparks = new Spark[MaxSparks];
            for (int i = 0; i < MaxSparks; i++)
            {
                var renderer = ShotFlightVisual.Make("Spark" + i, transform, 27);
                _sparks[i] = new Spark { Transform = renderer.transform, Renderer = renderer };
            }
        }

        void Play(Vector3 position, Color color, PokemonElement element, int count, float spread,
            bool elemental, bool patterned, ShotType type, Vector3 direction, Beat beat)
        {
            transform.position = position;
            _time = 0f;
            _color = color;
            _element = element;
            _elemental = elemental;
            _patterned = patterned;
            _type = type;
            _beat = beat;
            _life = beat == Beat.Muzzle ? .16f : beat == Beat.Turn ? .24f :
                beat == Beat.Dissipate ? .22f : patterned && type == ShotType.Explosive ? .46f : .30f;
            _count = Mathf.Clamp(count, 0, MaxSparks);
            var camera = Camera.main;
            _facing = camera != null ? camera.transform.rotation : Quaternion.identity;
            Vector3 view = Quaternion.Inverse(_facing) * direction;
            _aim = _facing * Quaternion.Euler(0, 0, Mathf.Atan2(view.y, view.x) * Mathf.Rad2Deg);
            _core.transform.rotation = _aim;
            _accent.transform.rotation = _aim;
            _ring.transform.rotation = IsExplosion ? Quaternion.Euler(90, 0, 0) : _aim;
            _core.transform.localPosition = Vector3.zero;
            _accent.transform.localPosition = Vector3.zero;
            _ring.transform.localPosition = Vector3.zero;
            // Local variation does not consume the gameplay random stream.
            float seed = (++_sequence % 97) * 2.39996f;
            for (int i = 0; i < MaxSparks; i++)
            {
                var spark = _sparks[i];
                spark.Renderer.enabled = i < _count;
                if (i >= _count) continue;
                float angle = seed + i * Mathf.PI * 2 / Mathf.Max(1, _count);
                Vector3 radial = _facing * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                spark.Velocity = (radial + direction * (beat == Beat.Muzzle ? 1.6f : .25f)) * (spread * 3.4f);
                spark.Size = patterned ? .22f + (i % 3) * .05f : .12f;
                spark.Spin = (i % 2 == 0 ? 1 : -1) * (160 + i * 50);
                spark.Transform.localPosition = Vector3.zero;
            }
            Animate();
        }

        bool IsExplosion => _patterned && _type == ShotType.Explosive && _beat == Beat.Impact;

        void Update()
        {
            if (GameFlow.Over) { gameObject.SetActive(false); return; }
            if (GameFlow.Blocked) return;
            _time += Time.deltaTime;
            Animate();
            if (_time >= _life) gameObject.SetActive(false);
        }

        void Animate()
        {
            float t = Mathf.Clamp01(_time / _life);
            float ease = 1 - (1 - t) * (1 - t);
            int frame = Mathf.Min(7, (int)(t * 8));
            bool muzzle = _beat == Beat.Muzzle;
            bool dissipate = _beat == Beat.Dissipate;
            float coreT = Mathf.Clamp01(_time / (IsExplosion ? .30f : muzzle ? .13f : .18f));
            _core.enabled = coreT < 1;
            _core.sprite = !_elemental ? PixelCombatSprites.Burst : _patterned && muzzle
                ? ShotAnimationAtlas.Pattern(_type, frame) : ShotAnimationAtlas.Element(_element, frame);
            Color coreColor = _patterned && muzzle ? Color.Lerp(_color, Color.white, .4f) :
                _elemental ? Color.white : Color.Lerp(_color, Color.white, .7f);
            coreColor.a = 1 - coreT * coreT;
            _core.color = coreColor;
            float size = IsExplosion ? .8f + ease * 1.7f : dissipate ? .65f * (1 - ease) :
                muzzle ? .75f * (1 - coreT * .7f) : .6f + Mathf.Sin(coreT * Mathf.PI) * .4f;
            _core.transform.localScale = muzzle ? new Vector3(size * 1.6f, size * .7f, 1) : Vector3.one * size;

            _ring.enabled = _patterned && !dissipate;
            _ring.sprite = ShotAnimationAtlas.Ring(frame);
            // The ground ring reaches the real 2 m blast boundary (24/32 of its quad).
            float ringSize = IsExplosion ? (ShotDefinitions.ExplosionRadius * 2 / .75f) * ease :
                muzzle ? .25f + ease * .8f : .4f + ease * 1.1f;
            _ring.transform.localScale = IsExplosion ? Vector3.one * ringSize :
                new Vector3(ringSize * (muzzle ? .45f : 1), ringSize, 1);
            _ring.color = new Color(_color.r, _color.g, _color.b, (1 - t) * .8f);

            _accent.enabled = _patterned && !dissipate && t < .8f;
            bool slash = _type == ShotType.Fan || _type == ShotType.Boomerang;
            _accent.sprite = slash ? ShotAnimationAtlas.Slash(frame) : ShotAnimationAtlas.Flash(frame);
            _accent.transform.rotation = _aim * Quaternion.Euler(0, 0, slash ? ease * 160 - 80 : 0);
            float accentSize = IsExplosion ? .8f + ease * 2 : muzzle ? .65f : 1.1f;
            _accent.transform.localScale = new Vector3(accentSize *
                (_type == ShotType.Precision || _type == ShotType.Piercing ? 1.8f : 1), accentSize, 1);
            Color accentColor = Color.Lerp(_color, Color.white, .7f);
            accentColor.a = (1 - t) * .8f;
            _accent.color = accentColor;

            if (_patterned && _beat == Beat.Impact)
            {
                switch (_type)
                {
                    case ShotType.Burst:
                        // Three staggered punches: the burst reads even on a single target.
                        _core.transform.localPosition = _aim * Vector3.right * .24f;
                        _accent.transform.localPosition = _aim * new Vector3(-.23f, .18f, 0);
                        _ring.transform.localPosition = _aim * new Vector3(-.25f, -.22f, 0);
                        _ring.sprite = ShotAnimationAtlas.Flash((frame + 3) & 7);
                        _ring.transform.localScale = Vector3.one * (.45f + ease * .4f);
                        _accent.sprite = ShotAnimationAtlas.Flash((frame + 5) & 7);
                        _accent.transform.localScale = Vector3.one * .8f;
                        break;
                    case ShotType.Piercing:
                        // A tall compression hoop around the continuing spear axis.
                        _ring.transform.localScale = new Vector3(ringSize * .35f, ringSize * 1.45f, 1);
                        _accent.transform.localScale = new Vector3(accentSize * 1.7f, accentSize * .4f, 1);
                        break;
                    case ShotType.Precision:
                        _ring.enabled = false;
                        _accent.transform.rotation = _aim * Quaternion.Euler(0, 0, 45);
                        _accent.transform.localScale = new Vector3(accentSize * 1.6f, accentSize * .65f, 1);
                        break;
                    case ShotType.Fan:
                        _accent.transform.localScale = Vector3.one * (1.15f + ease * .7f);
                        break;
                    case ShotType.Boomerang:
                        _ring.sprite = ShotAnimationAtlas.Slash(frame);
                        _ring.transform.rotation = _aim * Quaternion.Euler(0, 0, 180 - ease * 160);
                        break;
                }
            }

            for (int i = 0; i < _count; i++)
            {
                var spark = _sparks[i];
                spark.Transform.localPosition = spark.Velocity * (_time * (1 - t * .35f)) +
                    Vector3.down * (_time * _time * (IsExplosion ? 1.5f : 2.5f));
                spark.Transform.rotation = _facing * Quaternion.Euler(0, 0, spark.Spin * _time);
                spark.Transform.localScale = Vector3.one * (spark.Size * (1 - t * t));
                spark.Renderer.sprite = _elemental ? ShotAnimationAtlas.Element(_element, frame + i)
                    : PixelCombatSprites.Spark;
                Color tint = _elemental ? Color.white : _color;
                tint.a = 1 - t * t;
                spark.Renderer.color = tint;
            }
        }

        void OnDestroy() => Pool.Remove(this);
    }
}

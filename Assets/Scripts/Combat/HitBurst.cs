using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>Scene-local, bounded pool of brief pixel impacts.</summary>
    [DisallowMultipleComponent]
    public sealed class HitBurst : MonoBehaviour
    {
        sealed class Spark
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public float Size;
            public Color Color;
        }

        const float Life = .30f;
        const float CoreLife = .12f;
        const int MaxBursts = 32;
        const int MaxSparks = 6;
        static readonly List<HitBurst> Pool = new List<HitBurst>(MaxBursts);

        Spark[] _sparks;
        SpriteRenderer _core;
        float _time;
        int _count;
        Color _color;

        public static void Spawn(Vector3 position, Color color, int count = 9, float spread = 1f)
        {
            HitBurst burst = null;
            HitBurst oldest = null;
            for (int i = Pool.Count - 1; i >= 0; i--)
            {
                var candidate = Pool[i];
                if (candidate == null) { Pool.RemoveAt(i); continue; }
                if (!candidate.gameObject.activeSelf) { burst = candidate; break; }
                if (oldest == null || candidate._time > oldest._time) oldest = candidate;
            }

            if (burst == null && Pool.Count < MaxBursts)
            {
                var go = new GameObject("HitBurst");
                burst = go.AddComponent<HitBurst>();
                burst.CreateVisuals();
                Pool.Add(burst);
            }
            else if (burst == null) burst = oldest;

            burst.gameObject.SetActive(true);
            burst.Play(position, color, count, spread);
        }

        void CreateVisuals()
        {
            var core = new GameObject("Core");
            core.transform.SetParent(transform, false);
            _core = core.AddComponent<SpriteRenderer>();
            _core.sprite = PixelCombatSprites.Burst;
            _core.sortingOrder = 28;
            core.AddComponent<BillboardSprite>();

            _sparks = new Spark[MaxSparks];
            for (int i = 0; i < MaxSparks; i++)
            {
                var go = new GameObject("Spark" + i);
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = PixelCombatSprites.Spark;
                renderer.sortingOrder = 27;
                go.AddComponent<BillboardSprite>();
                _sparks[i] = new Spark { Transform = go.transform, Renderer = renderer };
            }
        }

        void Play(Vector3 position, Color color, int count, float spread)
        {
            transform.position = position;
            _time = 0f;
            _color = color;
            _count = Mathf.Clamp(count, 1, MaxSparks);
            _core.enabled = true;
            _core.color = Color.Lerp(color, new Color(1f, .97f, .78f, color.a), .7f);
            _core.transform.localScale = Vector3.one * .48f;
            for (int i = 0; i < MaxSparks; i++)
            {
                var spark = _sparks[i];
                spark.Renderer.enabled = i < _count;
                if (i >= _count) continue;
                spark.Transform.localPosition = Vector3.zero;
                Vector2 direction = Random.insideUnitCircle;
                if (direction.sqrMagnitude < .01f) direction = Vector2.right;
                spark.Velocity = new Vector3(direction.x, Random.Range(.25f, .7f), direction.y).normalized *
                                 Random.Range(1.2f, 2.7f) * Mathf.Max(.1f, spread);
                spark.Size = Random.Range(.09f, .16f);
                spark.Transform.localScale = Vector3.one * spark.Size;
                spark.Color = color;
                spark.Renderer.color = color;
            }
        }

        void Update()
        {
            if (GameFlow.Over)
            {
                gameObject.SetActive(false);
                return;
            }
            if (GameFlow.Blocked) return;
            _time += Time.deltaTime;
            float t = Mathf.Clamp01(_time / Life);
            float coreT = Mathf.Clamp01(_time / CoreLife);
            _core.enabled = coreT < 1f;
            _core.transform.localScale = Vector3.one * (.48f * (1f - coreT * .55f));
            Color coreColor = Color.Lerp(_color, new Color(1f, .97f, .78f, _color.a), .7f);
            coreColor.a *= 1f - coreT;
            _core.color = coreColor;
            for (int i = 0; i < _count; i++)
            {
                var spark = _sparks[i];
                spark.Velocity += Vector3.down * (5f * Time.deltaTime);
                spark.Transform.position += spark.Velocity * Time.deltaTime;
                spark.Transform.localScale = Vector3.one * (spark.Size * (1f - t));
                Color color = spark.Color;
                color.a *= 1f - t;
                spark.Renderer.color = color;
            }
            if (t >= 1f) gameObject.SetActive(false);
        }

        void OnDestroy() => Pool.Remove(this);
    }
}

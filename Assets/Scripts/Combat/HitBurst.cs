using Game.Rendering;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Destello de impactos: unos círculos de color salen disparados y se apagan.
    /// Se genera por código (sin partículas ni materiales externos).
    /// </summary>
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

        const float Life = 0.42f;

        Spark[] _sparks;
        float _time;

        public static void Spawn(Vector3 position, Color color, int count = 9, float spread = 1f)
        {
            var go = new GameObject("HitBurst");
            go.transform.position = position;

            var burst = go.AddComponent<HitBurst>();
            burst.Build(position, color, count, spread);
        }

        void Build(Vector3 position, Color color, int count, float spread)
        {
            transform.position = position;
            count = Mathf.Clamp(count, 1, 40);
            _sparks = new Spark[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Spark" + i);
                go.transform.SetParent(transform, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = ProceduralSprites.Circle;
                renderer.color = color;
                renderer.sortingOrder = 27;

                var billboard = go.AddComponent<BillboardSprite>();
                billboard.SetContinuous(false);

                Vector2 direction = Random.insideUnitCircle;
                if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;

                float size = Random.Range(0.10f, 0.26f);
                go.transform.localScale = Vector3.one * size;

                _sparks[i] = new Spark
                {
                    Transform = go.transform,
                    Renderer = renderer,
                    Velocity = new Vector3(direction.x, Random.Range(0.25f, 0.7f), direction.y).normalized *
                               Random.Range(3.5f, 7.5f) * Mathf.Max(0.1f, spread),
                    Size = size,
                    Color = color
                };
            }
        }

        void Update()
        {
            _time += Time.deltaTime;
            float t = Mathf.Clamp01(_time / Life);

            if (_sparks != null)
            {
                for (int i = 0; i < _sparks.Length; i++)
                {
                    var spark = _sparks[i];
                    if (spark.Transform == null) continue;

                    spark.Velocity += Vector3.down * (16f * Time.deltaTime);
                    spark.Transform.position += spark.Velocity * Time.deltaTime;
                    spark.Transform.localScale = Vector3.one * (spark.Size * (1f - t));

                    if (spark.Renderer != null)
                    {
                        Color color = spark.Color;
                        color.a = 1f - t;
                        spark.Renderer.color = color;
                    }
                }
            }

            if (t >= 1f) Destroy(gameObject);
        }
    }
}

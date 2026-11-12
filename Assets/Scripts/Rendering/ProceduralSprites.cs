using System.Collections.Generic;
using UnityEngine;

namespace Game.Rendering
{
    /// <summary>
    /// Sprites generados por código para la interfaz y los efectos: no depende de imágenes
    /// importadas. Todos se cachean, así que crear cien balas no crea cien texturas.
    /// </summary>
    public static class ProceduralSprites
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>Círculo blanco con borde suavizado (se tiñe con Image/SpriteRenderer.color).</summary>
        public static Sprite Circle
        {
            get
            {
                const string key = "circle";
                if (Cache.TryGetValue(key, out var sprite)) return sprite;

                const int size = 128;
                var pixels = new Color32[size * size];
                float radius = (size - 1) * 0.5f;
                var center = new Vector2(radius, radius);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                        byte alpha = (byte)(Mathf.Clamp01(radius - distance) * 255f);
                        pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                    }
                }

                sprite = Build("ProceduralCircle", size, pixels);
                Cache[key] = sprite;
                return sprite;
            }
        }

        /// <summary>Halo radial: opaco en el centro y transparente en el borde (balas y brillos).</summary>
        public static Sprite Glow
        {
            get
            {
                const string key = "glow";
                if (Cache.TryGetValue(key, out var sprite)) return sprite;

                const int size = 128;
                var pixels = new Color32[size * size];
                float radius = (size - 1) * 0.5f;
                var center = new Vector2(radius, radius);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / radius;
                        float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.2f);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                }

                sprite = Build("ProceduralGlow", size, pixels);
                Cache[key] = sprite;
                return sprite;
            }
        }

        /// <summary>Rectángulo redondeado en 9 secciones: se puede estirar sin deformar las esquinas.</summary>
        public static Sprite RoundedRect
        {
            get
            {
                const string key = "rounded";
                if (Cache.TryGetValue(key, out var sprite)) return sprite;

                const int size = 64;
                const float radius = 18f;
                var pixels = new Color32[size * size];

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f) - new Vector2(size * 0.5f, size * 0.5f);
                        var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - Vector2.one * (size * 0.5f - radius);
                        float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
                        float distance = Mathf.Min(Mathf.Max(q.x, q.y), 0f) + outside - radius;
                        byte alpha = (byte)(Mathf.Clamp01(0.5f - distance) * 255f);
                        pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                    }
                }

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "ProceduralRoundedRect",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                texture.SetPixels32(pixels);
                texture.Apply(false, true);

                float border = radius + 2f;
                sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                       SpriteMeshType.FullRect, new Vector4(border, border, border, border));
                sprite.name = "ProceduralRoundedRect";
                Cache[key] = sprite;
                return sprite;
            }
        }

        /// <summary>Degradado vertical (arriba -> abajo) para el fondo del menú.</summary>
        public static Sprite VerticalGradient(Color top, Color bottom)
        {
            string key = "gradient_" + ColorUtility.ToHtmlStringRGBA(top) + ColorUtility.ToHtmlStringRGBA(bottom);
            if (Cache.TryGetValue(key, out var sprite)) return sprite;

            const int width = 4;
            const int height = 256;
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                Color color = Color.Lerp(bottom, top, y / (height - 1f));
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = color;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "ProceduralGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "ProceduralGradient";
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Pokéball dibujada a mano: rojo arriba, blanco abajo y banda central.</summary>
        public static Sprite Pokeball
        {
            get
            {
                const string key = "pokeball";
                if (Cache.TryGetValue(key, out var sprite)) return sprite;

                const int size = 256;
                var pixels = new Color32[size * size];
                float radius = (size - 1) * 0.5f;
                var center = new Vector2(radius, radius);

                var red = new Color32(255, 77, 77, 255);
                var white = new Color32(247, 243, 236, 255);
                var ink = new Color32(27, 16, 48, 255);
                var clear = new Color32(0, 0, 0, 0);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        var point = new Vector2(x + 0.5f, y + 0.5f);
                        Vector2 fromCenter = point - center;
                        float distance = fromCenter.magnitude;
                        float coverage = Mathf.Clamp01(radius - distance + 0.5f);
                        if (coverage <= 0f)
                        {
                            pixels[y * size + x] = clear;
                            continue;
                        }

                        Color32 color = fromCenter.y >= 0f ? red : white;

                        // Banda horizontal.
                        if (Mathf.Abs(fromCenter.y) <= radius * 0.075f) color = ink;

                        // Anillo central.
                        float centerRing = fromCenter.magnitude;
                        if (centerRing <= radius * 0.31f)
                        {
                            color = centerRing <= radius * 0.23f ? white : ink;
                        }

                        // Borde exterior.
                        if (distance >= radius * 0.93f) color = ink;

                        color.a = (byte)(coverage * 255f);
                        pixels[y * size + x] = color;
                    }
                }

                sprite = Build("ProceduralPokeball", size, pixels);
                Cache[key] = sprite;
                return sprite;
            }
        }

        /// <summary>Barra blanca con las esquinas redondeadas; se estira con SpriteRenderer.</summary>
        public static Sprite HealthBar => RoundedRect;

        static Sprite Build(string name, int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }
    }
}

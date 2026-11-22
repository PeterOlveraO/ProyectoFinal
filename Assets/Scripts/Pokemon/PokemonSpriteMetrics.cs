using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>
    /// Offline alpha bounds, in pixel edges with a bottom-left origin. Loaded once, never
    /// reads texture pixels or requires readable textures. See Tools/PokemonAnimations/metrics.py.
    /// </summary>
    public static class PokemonSpriteMetrics
    {
        [Serializable]
        public sealed class Bounds
        {
            public int width, height, left, bottom, visibleWidth, visibleHeight;

            public bool Matches(Sprite sprite)
            {
                return sprite != null && width == sprite.rect.width && height == sprite.rect.height &&
                       visibleWidth > 0 && visibleHeight > 0 && left >= 0 && bottom >= 0 &&
                       left + visibleWidth <= width && bottom + visibleHeight <= height;
            }
        }

        [Serializable]
        public sealed class Family
        {
            public string name;
            public int maxVisibleWidth, maxVisibleHeight;
            public Bounds source;
            public Bounds[] frames;
        }

        [Serializable]
        sealed class Document
        {
            public int version;
            public Family[] families;
        }

        static Dictionary<string, Family> families;

        public static Family Find(string name)
        {
            if (families == null)
            {
                families = new Dictionary<string, Family>(StringComparer.Ordinal);
                var asset = Resources.Load<TextAsset>("PokemonVisualMetrics");
                var document = asset != null ? JsonUtility.FromJson<Document>(asset.text) : null;
                if (document != null && document.version == 1 && document.families != null)
                    foreach (var family in document.families)
                        if (family != null && !string.IsNullOrEmpty(family.name) &&
                            family.maxVisibleHeight > 0 && family.maxVisibleWidth > 0 &&
                            family.frames != null && family.frames.Length == 6)
                            families[family.name] = family;
            }
            return name != null && families.TryGetValue(name, out var result) ? result : null;
        }

        public static Bounds Source(Sprite sprite)
        {
            var bounds = Find(sprite.name)?.source;
            return bounds != null && bounds.Matches(sprite) ? bounds : FullRect(sprite);
        }

        // Unknown/modded sprites still have a safe, deterministic fallback without GPU readback.
        public static Bounds FullRect(Sprite sprite)
        {
            return new Bounds {
                width = Mathf.RoundToInt(sprite.rect.width),
                height = Mathf.RoundToInt(sprite.rect.height),
                visibleWidth = Mathf.RoundToInt(sprite.rect.width),
                visibleHeight = Mathf.RoundToInt(sprite.rect.height)
            };
        }
    }
}

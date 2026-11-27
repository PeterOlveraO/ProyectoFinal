using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>Standard Pokédex measurements, keyed by source family (never shuffled pool index).</summary>
    public static class PokemonPhysicalSize
    {
        public const float WorldUnitsPerMeter = 1f;
        public const float FallbackHeight = 1.35f;

        [Serializable]
        public sealed class Measurement
        {
            public string name, pokemon;
            public int pokedexId, pokemonId;
            public float heightMeters, weightKilograms;
        }

        [Serializable]
        sealed class Document
        {
            public int version;
            public Measurement[] families;
        }

        static Dictionary<string, Measurement> measurements;

        public static Measurement Find(string sourceName)
        {
            if (measurements == null)
            {
                measurements = new Dictionary<string, Measurement>(StringComparer.Ordinal);
                var asset = Resources.Load<TextAsset>("PokemonPhysicalSizes");
                var data = asset != null ? JsonUtility.FromJson<Document>(asset.text) : null;
                if (data != null && data.version == 1 && data.families != null)
                    foreach (var entry in data.families)
                        if (entry != null && !string.IsNullOrEmpty(entry.name) && entry.heightMeters > 0f)
                            measurements.Add(entry.name, entry);
            }
            return sourceName != null && measurements.TryGetValue(sourceName, out var size) ? size : null;
        }

        public static float HeightFor(Sprite source)
        {
            var size = source != null ? Find(source.name) : null;
            return size != null ? size.heightMeters * WorldUnitsPerMeter : FallbackHeight;
        }

        /// <summary>Allow wide species to reach their proper height without squeezing or capping them.</summary>
        public static float WidthFor(Sprite source, float height)
        {
            if (source == null) return height * (2.25f / 1.35f);
            var bounds = PokemonSpriteMetrics.Source(source);
            float aspect = (float)bounds.visibleWidth / bounds.visibleHeight;
            var family = PokemonSpriteMetrics.Find(source.name);
            if (family != null)
                aspect = Mathf.Max(aspect, (float)family.maxVisibleWidth / family.maxVisibleHeight);
            return height * aspect;
        }
    }
}

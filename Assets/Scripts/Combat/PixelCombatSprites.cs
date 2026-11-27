using UnityEngine;

namespace Game.Combat
{
    /// <summary>Small, shared, hard-edged masks for combat feedback.</summary>
    internal static class PixelCombatSprites
    {
        static Sprite _burst;
        static Sprite _spark;
        static Sprite _shot;

        public static Sprite Burst => _burst != null ? _burst : _burst = Make(16, 0);
        public static Sprite Spark => _spark != null ? _spark : _spark = Make(8, 1);
        public static Sprite Shot => _shot != null ? _shot : _shot = Make(16, 2);

        public static Sprite ElementShot(PokemonElement element, int frame) => ShotAnimationAtlas.Element(element, frame);

        public static Color ElementColor(PokemonElement element)
        {
            switch (element)
            {
                case PokemonElement.Electric: return new Color(1f, .83f, .18f);
                case PokemonElement.Fire: return new Color(1f, .36f, .12f);
                case PokemonElement.Water: return new Color(.22f, .72f, 1f);
                case PokemonElement.Grass: return new Color(.43f, .83f, .22f);
                case PokemonElement.Ice: return new Color(.63f, .92f, 1f);
                case PokemonElement.Psychic: return new Color(1f, .48f, .75f);
                case PokemonElement.Ghost: return new Color(.68f, .43f, .92f);
                case PokemonElement.Rock: case PokemonElement.Ground: return new Color(.71f, .53f, .33f);
                case PokemonElement.Dragon: return new Color(.42f, .44f, 1f);
                case PokemonElement.Fighting: return new Color(.95f, .48f, .27f);
                case PokemonElement.Steel: return new Color(.72f, .81f, .86f);
                default: return new Color(.95f, .85f, .59f);
            }
        }

        static Sprite Make(int size, int shape)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "CombatPixelMask" + shape,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int dx = Mathf.Abs(2 * x + 1 - size);
                int dy = Mathf.Abs(2 * y + 1 - size);
                bool filled = shape == 0
                    ? (dx <= 3 && dy <= 13) || (dy <= 3 && dx <= 13) || (dx <= 7 && dy <= 7)
                    : shape == 1
                        ? dx <= 3 && dy <= 5
                        : dx + dy <= 16 && dx <= 11 && dy <= 11;
                pixels[y * size + x] = filled
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }
    }
}

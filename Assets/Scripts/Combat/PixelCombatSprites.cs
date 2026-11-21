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

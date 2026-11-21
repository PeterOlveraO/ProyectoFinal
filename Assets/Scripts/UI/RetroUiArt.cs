using UnityEngine;

namespace Game.UI
{
    /// <summary>Shared point-filtered, nine-sliced handheld frame. No imported art is replaced.</summary>
    internal static class RetroUiArt
    {
        static Sprite _frame;

        public static Sprite Frame
        {
            get
            {
                if (_frame != null) return _frame;
                const int size = 24;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "UI_PixelFrame",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    bool corner = (x < 2 || x >= size - 2) && (y < 2 || y >= size - 2);
                    float shade = edge == 0 ? .42f : edge == 1 ? .64f :
                        edge == 2 ? (y > size / 2 ? 1f : .65f) : .92f;
                    pixels[y * size + x] = corner ? Color.clear : new Color(shade, shade, shade, 1);
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                _frame = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f),
                    100, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
                _frame.name = "UI_PixelFrame";
                _frame.hideFlags = HideFlags.HideAndDontSave;
                return _frame;
            }
        }
    }
}

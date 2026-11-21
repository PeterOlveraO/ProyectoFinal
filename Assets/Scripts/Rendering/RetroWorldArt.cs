using System.Collections.Generic;
using UnityEngine;

namespace Game.Rendering
{
    /// <summary>Original, cached pixel motifs for the hub. One world unit per 32 texels.</summary>
    public static class RetroWorldArt
    {
        public static readonly Color Ink = new Color32(34, 57, 64, 255);
        public static readonly Color Cream = new Color32(245, 239, 214, 255);
        public static readonly Color Gold = new Color32(233, 196, 106, 255);
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Pad => Get("HubStonePad", 32, 32, (x, y) =>
        {
            int edge = Mathf.Min(x, y, 31 - x, 31 - y);
            if (edge == 0 && (x < 2 || x > 29 || y < 2 || y > 29)) return Color.clear;
            if (edge == 0) return new Color32(67, 81, 72, 255);
            if (edge == 1) return new Color32(212, 206, 166, 255);
            if (edge == 2) return new Color32(122, 132, 103, 255);
            bool joint = y % 8 == 0 || (x + (y / 8 % 2) * 4) % 8 == 0;
            return joint ? new Color32(144, 153, 121, 255) : new Color32(183, 187, 148, 255);
        });

        public static Sprite Corners => Get("HubAccentCorners", 32, 32, (x, y) =>
        {
            bool corner = (x < 7 || x > 24) && (y < 7 || y > 24);
            int edge = Mathf.Min(x, y, 31 - x, 31 - y);
            return corner && (edge == 2 || edge == 3) ? Color.white : Color.clear;
        });

        public static Sprite Sign => Get("HubSign", 64, 24, (x, y) =>
        {
            int edge = Mathf.Min(x, y, 63 - x, 23 - y);
            if (edge == 0 && (x < 2 || x > 61) && (y < 2 || y > 21)) return Color.clear;
            if (edge == 0) return Ink;
            if (edge == 1) return Gold;
            if (edge == 2) return new Color32(88, 112, 99, 255);
            return new Color32(41, 71, 78, 255);
        });

        public static Sprite Construction => Get("HubConstruction", 24, 32, (x, y) =>
        {
            // A wooden placard on two short stakes, rather than a glowing floor button.
            if (y < 10)
                return (x >= 5 && x <= 7) || (x >= 16 && x <= 18)
                    ? new Color32(110, 89, 65, 255) : Color.clear;
            if (x < 2 || x > 21 || y > 27) return Color.clear;
            if (x == 2 || x == 21 || y == 10 || y == 27) return Ink;
            if (x == 3 || y == 26) return new Color32(230, 210, 157, 255);
            return new Color32(177, 145, 98, 255);
        });

        public static Sprite Terminal => Get("HubArsenal", 32, 40, (x, y) =>
        {
            if (x < 3 || x > 28 || y < 2 || y > 37) return Color.clear;
            if (x == 3 || x == 28 || y == 2 || y == 37) return Ink;
            if (y < 5) return new Color32(89, 104, 103, 255);
            if (y == 36 || x == 4) return Cream;
            if (x >= 7 && x <= 24 && y >= 14 && y <= 30)
                return x == 7 || x == 24 || y == 14 || y == 30
                    ? Ink : new Color32(59, 89, 89, 255);
            if (y >= 7 && y <= 9 && x >= 8 && x <= 23) return Ink;
            return new Color32(166, 185, 169, 255);
        });

        public static Sprite Shadow => Get("GroundContactShadow", 32, 16, (x, y) =>
        {
            float dx = (x - 15.5f) / 15f, dy = (y - 7.5f) / 7f;
            float d = dx * dx + dy * dy;
            return new Color(0.12f, 0.22f, 0.20f, d < .65f ? .27f : d < 1f ? .12f : 0f);
        });

        static Sprite Get(string name, int width, int height, System.Func<int, int, Color> pixel)
        {
            if (Cache.TryGetValue(name, out var sprite) && sprite != null) return sprite;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            var colors = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++) colors[y * width + x] = pixel(x, y);
            texture.SetPixels(colors);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f),
                32f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            Cache[name] = sprite;
            return sprite;
        }
    }
}

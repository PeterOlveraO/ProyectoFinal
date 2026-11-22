using System;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>
    /// Original code-authored pickup family: 16px RGBA, point filtering, no mipmaps,
    /// 28 pixels/unit, ink outline + cream highlight + one accent per silhouette.
    /// Bolt / wing / shield / cross / coin distinguish rewards without relying on colour.
    /// Owned by one EnemyDropSystem, shared by its entire pool and disposed with that owner.
    /// </summary>
    internal sealed class DropPickupArt : IDisposable
    {
        static readonly Color32 Ink = new Color32(20, 28, 43, 255);
        static readonly Color32 Cream = new Color32(255, 248, 212, 255);
        internal static readonly Color PointsColor = new Color32(255, 211, 80, 255);
        static readonly string[] Masks =
        {
            ".......##...|......###...|.....###+...|....###+....|...####.....|..########..|...++####...|.....###....|....###.....|...###......|...##.......|...#........",
            ".......##...|......#++#..|.....#++##..|....#++##...|...#++##....|..#++##.....|.#########..|..##++####..|...##++##...|....####....|.....##.....|............",
            "..########..|.##++++++##.|.##+####+##.|.##########.|..########..|..########..|...######...|...######...|....####....|....####....|.....##.....|............",
            "....####....|....#++#....|....#++#....|.####++####.|.#++++++++#.|.#++++++++#.|.####++####.|....#++#....|....#++#....|....####....|............|............",
            "....####....|..###++###..|.##++++++##.|.#++####++#.|##++#++#++##|##++#++#++##|##++#++#++##|##++#++#++##|.#++####++#.|.##++++++##.|..########..|....####...."
        };

        readonly Sprite[] _sprites = new Sprite[5];
        readonly Texture2D[] _textures = new Texture2D[6];
        public Sprite Ring { get; private set; }

        public DropPickupArt()
        {
            for (int i = 0; i < _sprites.Length; i++)
                _sprites[i] = MakeIcon(i, i == 4 ? PointsColor : DropDefinitions.Color((TemporaryEffect)i));
            Ring = MakeRing();
        }

        public Sprite Get(GroundPickupKind kind, TemporaryEffect effect) =>
            _sprites[kind == GroundPickupKind.BonusPoints ? 4 : (int)effect];

        Sprite MakeIcon(int index, Color accent)
        {
            const int size = 16;
            var pixels = new Color32[size * size];
            var rows = Masks[index].Split('|');
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                {
                    if (rows[y][x] == '.') continue;
                    int px = x + 2, py = 13 - y;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            pixels[(py + dy) * size + px + dx] = Ink;
                }
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                {
                    char pixel = rows[y][x];
                    if (pixel == '.') continue;
                    pixels[(13 - y) * size + x + 2] = pixel == '+' ? Cream : (Color32)accent;
                }
            return MakeSprite(index, "DropSymbol_" + index, pixels, 28f);
        }

        Sprite MakeRing()
        {
            var pixels = new Color32[16 * 16];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dx = Mathf.Abs(x - 7.5f), dy = Mathf.Abs(y - 7.5f);
                    float edge = Mathf.Max(dx, dy) + Mathf.Min(dx, dy) * .5f;
                    if (edge < 5f || edge > 8.5f) continue;
                    pixels[y * 16 + x] = edge < 6f || edge > 7.5f ? Ink : Cream;
                }
            return MakeSprite(5, "DropGroundRing", pixels, 18f);
        }

        Sprite MakeSprite(int index, string name, Color32[] pixels, float pixelsPerUnit)
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            _textures[index] = texture;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .5f),
                pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        public void Dispose()
        {
            foreach (var sprite in _sprites) Release(sprite);
            Release(Ring);
            foreach (var texture in _textures) Release(texture);
            Array.Clear(_sprites, 0, _sprites.Length);
            Array.Clear(_textures, 0, _textures.Length);
            Ring = null;
        }

        static void Release(UnityEngine.Object asset)
        {
            if (asset == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(asset);
            else UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}

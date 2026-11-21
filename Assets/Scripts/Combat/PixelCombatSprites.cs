using UnityEngine;

namespace Game.Combat
{
    /// <summary>Small, shared, hard-edged masks for combat feedback.</summary>
    internal static class PixelCombatSprites
    {
        static Sprite _burst;
        static Sprite _spark;
        static Sprite _shot;
        static readonly Sprite[,] ElementShots = new Sprite[13, 2];

        public static Sprite Burst => _burst != null ? _burst : _burst = Make(16, 0);
        public static Sprite Spark => _spark != null ? _spark : _spark = Make(8, 1);
        public static Sprite Shot => _shot != null ? _shot : _shot = Make(16, 2);

        public static Sprite ElementShot(PokemonElement element, int frame)
        {
            int type = Mathf.Clamp((int)element, 0, ElementShots.GetLength(0) - 1);
            frame &= 1;
            var sprite = ElementShots[type, frame];
            if (sprite == null) ElementShots[type, frame] = sprite = MakeElement((PokemonElement)type, frame);
            return sprite;
        }

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

        // Hand-authored pixel shapes point to +X; the outermost pixel is a dark outline.
        static Sprite MakeElement(PokemonElement element, int frame)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Combat_" + element + "_" + frame,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            Color body = ElementColor(element);
            Color dark = Color.Lerp(body, new Color(.09f, .08f, .16f), .68f);
            Color shade = Color.Lerp(body, new Color(.18f, .11f, .18f), .32f);
            Color light = Color.Lerp(body, new Color(1f, .98f, .82f), .72f);
            if (element == PokemonElement.Fire)
            {
                dark = new Color(.39f, .09f, .08f);
                shade = new Color(.80f, .19f, .08f);
                body = new Color(1f, .47f, .08f);
                light = new Color(1f, .94f, .56f);
            }
            else if (element == PokemonElement.Electric)
            {
                dark = new Color(.46f, .28f, .08f);
                shade = new Color(.90f, .62f, .12f);
                body = new Color(1f, .85f, .31f);
                light = new Color(1f, 1f, .79f);
            }
            else if (element == PokemonElement.Dragon)
            {
                dark = new Color(.14f, .12f, .40f);
                shade = new Color(.33f, .24f, .70f);
                body = new Color(.37f, .51f, .98f);
                light = new Color(.76f, .88f, 1f);
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (!ElementFilled(element, x, y)) continue;
                bool edge = !ElementFilled(element, x - 1, y) || !ElementFilled(element, x + 1, y) ||
                            !ElementFilled(element, x, y - 1) || !ElementFilled(element, x, y + 1);
                Color c = edge ? dark : ElementDetail(element, x, y, frame) == 2 ? light :
                          ElementDetail(element, x, y, frame) == 1 ? shade : body;
                pixels[y * size + x] = c;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }

        static bool ElementFilled(PokemonElement element, int x, int y)
        {
            if (x < 2 || x > 29 || y < 2 || y > 29) return false;
            int a = Mathf.Abs(y - 16), d = Mathf.Abs(x - 16);
            switch (element)
            {
                case PokemonElement.Electric:
                    int zig = x < 10 ? 19 : x < 20 ? 12 : 17;
                    return x >= 3 && x <= 28 &&
                           (Mathf.Abs(y - zig) <= 2 || ((x >= 8 && x <= 11 || x >= 18 && x <= 21) &&
                            y >= 10 && y <= 21) || (x >= 13 && x <= 17 && y >= 18 && y <= 25 - (x - 13)));
                case PokemonElement.Fire:
                    return x >= 4 && x <= 27 && a <= (x < 19 ? (x - 3) / 2 : 28 - x) &&
                           !(x < 13 && (x + y) % 9 < 2);
                case PokemonElement.Water:
                    return x >= 5 && x <= 27 && a <= (x < 16 ? (x - 4) / 2 : (28 - x) / 2 + 1);
                case PokemonElement.Grass:
                    return x >= 5 && x <= 27 && a <= Mathf.Min(x - 4, 28 - x) / 2 + 1;
                case PokemonElement.Ice:
                case PokemonElement.Steel:
                    return d * 2 + a * 3 <= 34;
                case PokemonElement.Psychic:
                    int radius = (x - 16) * (x - 16) + (y - 16) * (y - 16);
                    return radius <= 120 && (radius >= 50 || (x >= 14 && y <= 16) ||
                                              (radius <= 22 && y >= 14));
                case PokemonElement.Ghost:
                    return x >= 7 && x <= 25 && a <= 8 && (d + a <= 14) &&
                           !(x < 13 && ((y < 14 && x < 11) || (y > 18 && x < 10)));
                case PokemonElement.Rock:
                case PokemonElement.Ground:
                    return d <= 10 && a <= 8 && d + a <= 14;
                case PokemonElement.Dragon:
                    return x >= 4 && x <= 27 && a <= (x < 18 ? (x - 3) / 3 + 1 : (28 - x) / 2 + 1) &&
                           !(x < 12 && (x + y) % 8 < 2);
                case PokemonElement.Fighting:
                    return (x >= 9 && x <= 24 && a <= 7 && d + a <= 16) ||
                           (x >= 4 && x <= 12 && a <= 3);
                default:
                    return d + a <= 11 && d <= 9 && a <= 9;
            }
        }

        // Changes are internal highlights, not a jumping collision-sized silhouette.
        static int ElementDetail(PokemonElement element, int x, int y, int frame)
        {
            switch (element)
            {
                case PokemonElement.Fire:
                case PokemonElement.Dragon:
                    return x >= 12 + frame && x <= 23 && Mathf.Abs(y - 16 - frame) <= 2 ? 2 :
                           y < 14 || x < 11 ? 1 : 0;
                case PokemonElement.Electric:
                    int zig = x < 10 ? 19 : x < 20 ? 12 : 17;
                    return Mathf.Abs(y - zig) <= 1 && (x + frame * 4) % 12 < 9 ? 2 : 1;
                case PokemonElement.Water:
                    return x >= 14 && x <= 21 && y >= 18 && y <= 19 + frame ? 2 : y < 13 ? 1 : 0;
                case PokemonElement.Grass:
                    return Mathf.Abs(y - 16) <= 1 || (x >= 13 && x <= 22 && y == 16 + (x - 13) / 3 + frame)
                        ? 2 : y < 13 ? 1 : 0;
                case PokemonElement.Ice:
                case PokemonElement.Steel:
                    return x == 16 + frame || y == 16 ? 2 : x + y < 31 ? 1 : 0;
                case PokemonElement.Psychic:
                    return (x + y + frame * 2) % 10 < 3 ? 2 : x < 15 ? 1 : 0;
                case PokemonElement.Ghost:
                    return x >= 17 && x <= 20 && (y == 13 || y == 19) ? 2 :
                           (x + y + frame * 2) % 9 < 3 ? 1 : 0;
                case PokemonElement.Rock:
                case PokemonElement.Ground:
                    return x + y > 34 + frame && x > 15 ? 2 : x + y < 28 ? 1 : 0;
                case PokemonElement.Fighting:
                    return x >= 18 && x <= 21 && (y == 12 || y == 15 || y == 18 || y == 21) ? 2 :
                           y < 13 + frame ? 1 : 0;
                default:
                    return x >= 16 && y >= 16 && x + y > 34 + frame ? 2 : y < 14 ? 1 : 0;
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

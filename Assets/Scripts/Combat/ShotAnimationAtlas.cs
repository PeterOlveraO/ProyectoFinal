using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Original pixel flipbooks: thirteen elements, seven weapon silhouettes and three accents.
    /// One point-filtered atlas, shared by every projectile and impact; no per-shot textures.
    /// </summary>
    internal static class ShotAnimationAtlas
    {
        internal const int Frames = 8;
        const int Size = 32;
        const int Elements = 13;
        const int Rows = Elements + ShotDefinitions.Count + 3;
        static readonly Sprite[,] Sprites = new Sprite[Rows, Frames];
        static Texture2D _atlas;

        internal static Sprite Element(PokemonElement element, int frame)
            => Get(Mathf.Clamp((int)element, 0, Elements - 1), frame);
        internal static Sprite Pattern(ShotType type, int frame)
            => Get(Elements + Mathf.Clamp((int)type, 0, ShotDefinitions.Count - 1), frame);
        internal static Sprite Ring(int frame) => Get(Elements + ShotDefinitions.Count, frame);
        internal static Sprite Slash(int frame) => Get(Elements + ShotDefinitions.Count + 1, frame);
        internal static Sprite Flash(int frame) => Get(Elements + ShotDefinitions.Count + 2, frame);

        // Build once during scene loading instead of on the first combat input.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Warm() => Get(0, 0);

        static Sprite Get(int row, int frame)
        {
            if (_atlas == null) Build();
            return Sprites[row, frame & (Frames - 1)];
        }

        static void Build()
        {
            int width = Size * Frames;
            _atlas = new Texture2D(width, Size * Rows, TextureFormat.RGBA32, false)
            {
                name = "Combat_AnimatedPixelAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[width * Size * Rows];
            var mask = new bool[Size * Size];
            for (int row = 0; row < Rows; row++)
            for (int frame = 0; frame < Frames; frame++)
            {
                float phase = frame * Mathf.PI * .25f;
                Color body = row < Elements ? PixelCombatSprites.ElementColor((PokemonElement)row) : Color.white;
                Color dark = row < Elements ? Color.Lerp(body, new Color(.08f, .06f, .18f), .65f)
                    : new Color(.32f, .36f, .46f);
                Color shade = body * .65f;
                shade.a = 1f;
                Color light = Color.Lerp(body, new Color(1f, 1f, .94f), .83f);
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    mask[y * Size + x] = x > 1 && x < 30 && y > 1 && y < 30 &&
                        Filled(row, x - 16, y - 16, phase, frame);

                for (int y = 2; y < Size - 2; y++)
                for (int x = 2; x < Size - 2; x++)
                {
                    int index = y * Size + x;
                    if (!mask[index]) continue;
                    bool edge = !mask[index - 1] || !mask[index + 1] || !mask[index - Size] || !mask[index + Size];
                    bool hot = Mathf.Abs(y - 16) <= 1 || (x + y + frame * 3) % 13 < 3;
                    // Outlined bodies, bright moving veins and a darker underside.
                    Color color = edge ? dark : hot ? light : y < 14 ? shade : body;
                    if (row >= Elements + ShotDefinitions.Count) color = Color.white;
                    pixels[(row * Size + y) * width + frame * Size + x] = color;
                }
            }
            _atlas.SetPixels32(pixels);
            _atlas.Apply(false, true);
            for (int row = 0; row < Rows; row++)
            for (int frame = 0; frame < Frames; frame++)
            {
                var sprite = Sprite.Create(_atlas, new Rect(frame * Size, row * Size, Size, Size),
                    new Vector2(.5f, .5f), Size, 0, SpriteMeshType.FullRect);
                sprite.name = row < Elements ? "Combat_" + (PokemonElement)row + "_" + frame :
                    row < Elements + ShotDefinitions.Count ? "Combat_" + (ShotType)(row - Elements) + "_" + frame :
                    "Combat_Accent" + (row - Elements - ShotDefinitions.Count) + "_" + frame;
                sprite.hideFlags = HideFlags.DontSave;
                Sprites[row, frame] = sprite;
            }
        }

        static bool Filled(int row, float x, float y, float phase, int frame)
        {
            float a = Mathf.Abs(y);
            float r = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(y, x);
            if (row < Elements)
            {
                float wave = Mathf.Sin(x * .55f - phase);
                switch ((PokemonElement)row)
                {
                    case PokemonElement.Electric:
                        float zig = x < -5 ? 4 : x < 3 ? -3 : 2;
                        zig += Mathf.Round(Mathf.Sin(phase + x * .4f) * 2f);
                        return Mathf.Abs(y - zig) < 1.7f || (x > -5 && x < -2 && a < 7) ||
                            (x > 2 && x < 5 && a < 5) || (x < -7 && y > 7 && y < 9 && (int)(x + frame) % 4 == 0);
                    case PokemonElement.Fire:
                        float fire = x < 4 ? (x + 14) * .43f : (13 - x) * .8f;
                        return Mathf.Abs(y - wave * Mathf.Max(0, 4 - x) * .15f) < fire &&
                            !(x < -3 && Mathf.Sin(y * 1.5f + phase + x * .5f) > .4f) ||
                            (x < -8 && x > -13 && Mathf.Abs(y - 7 - wave * 2) < 1);
                    case PokemonElement.Water:
                        return x > -5 && (x - 3) * (x - 3) * .6f + y * y < 42 ||
                            x < 3 && Mathf.Abs(y - wave * 2) < (x + 15) * .18f ||
                            (x < -6 && Mathf.Abs(y - 6 * Mathf.Sin(phase + x)) < 1.4f && (int)x % 4 == 0);
                    case PokemonElement.Grass:
                        float leaf = y - x * .42f - Mathf.Sin(phase) * 1.5f;
                        float leaf2 = y + x * .65f + 4;
                        return Mathf.Abs(leaf) < Mathf.Max(0, 8 - Mathf.Abs(x)) * .6f ||
                            (x < 1 && x > -12 && Mathf.Abs(leaf2) < (6 - Mathf.Abs(x + 6)) * .55f);
                    case PokemonElement.Ice:
                        return Mathf.Abs(x - 3) + a * 2.7f < 13 ||
                            Mathf.Abs(x + 5) * 1.7f + Mathf.Abs(y - 6 - Mathf.Sin(phase)) * 2 < 8 ||
                            Mathf.Abs(x + 7) * 1.7f + Mathf.Abs(y + 6 + Mathf.Sin(phase)) * 2 < 7;
                    case PokemonElement.Psychic:
                        return Mathf.Abs(r - (4 + Mathf.Repeat(angle - phase, Mathf.PI * 2) * 1.2f)) < 1.4f ||
                            r < 2.5f || (Mathf.Abs(r - 12) < 1 && Mathf.Sin(angle * 3 + phase) > .55f);
                    case PokemonElement.Ghost:
                        return (x - 4) * (x - 4) + y * y < 39 &&
                            !(x > 3 && x < 7 && (y == 2 || y == -2)) ||
                            x < 4 && Mathf.Abs(y - wave * 3) < (x + 15) * .28f &&
                            !(x < -6 && Mathf.Sin(y + phase) > .1f);
                    case PokemonElement.Rock:
                        return Mathf.Abs(x - 3) + a < 10 && a < 7 && x < 11 &&
                            !(x + y > 7 && x - y < 0) ||
                            (Mathf.Abs(x + 8) + Mathf.Abs(y - 5 - Mathf.Sin(phase) * 2) < 3) ||
                            (Mathf.Abs(x + 10) + Mathf.Abs(y + 5 + Mathf.Cos(phase) * 2) < 2.5f);
                    case PokemonElement.Ground:
                        return Mathf.Abs(y - Mathf.Sin(x * .4f - phase) * 5) < 1.7f && x < 9 ||
                            Mathf.Abs(y + Mathf.Sin(x * .4f - phase) * 5) < 1.2f && x < 7 ||
                            Mathf.Abs(x - 8) + a * 1.5f < 5;
                    case PokemonElement.Dragon:
                        return x < 5 && Mathf.Abs(y - Mathf.Sin(x * .4f - phase) * 4) < 2.3f ||
                            Mathf.Abs(x - 5) + a * 1.5f < 8 ||
                            x > 4 && x < 10 && Mathf.Abs(a - 6 + (x - 4) * .5f) < 1.3f;
                    case PokemonElement.Fighting:
                        return x > -1 && x < 10 && a < 6 && !(x > 7 && (int)y % 3 == 0) ||
                            x > -5 && x < 2 && a < 4 ||
                            x < -6 && a < 8 && (int)(y + 16) % 5 == 0 && (int)(x + frame) % 6 < 4;
                    case PokemonElement.Steel:
                        return Mathf.Abs(x) + a * 4 < 13 ||
                            x < 2 && x > -9 && Mathf.Abs(a - (3 - x * .5f)) < 1.2f ||
                            Mathf.Abs(x - 5 + frame) < 1 && a < 8;
                    default:
                        return Mathf.Abs(x - 3) + a * 1.6f < 10 ||
                            x < 0 && Mathf.Abs(a - (x + 14) * .3f) < 1.2f ||
                            x < -6 && a < 1.5f && (int)(x + frame * 2) % 5 != 0;
                }
            }
            if (row < Elements + ShotDefinitions.Count)
            {
                switch ((ShotType)(row - Elements))
                {
                    case ShotType.Burst:
                        return Mathf.Abs(x + 7 + a * .65f) < 1.4f && a < 7 ||
                            Mathf.Abs(x + 1 + a * .65f) < 1.3f && a < 5 ||
                            x > 4 && a < 1.5f && x < 12;
                    case ShotType.Fan:
                        return r < 13 && r > 8 && x > 0 && a < 11 ||
                            Mathf.Abs(x - 4 - a * .35f) < 1.5f && a < 12;
                    case ShotType.Piercing:
                        return a < 1.5f || Mathf.Abs(x - 7 + a * 1.4f) < 1.5f && a < 6 ||
                            x < -3 && Mathf.Abs(y - Mathf.Sin(x * .6f - phase) * 5) < 1.2f;
                    case ShotType.Precision:
                        return a < 1 || Mathf.Abs(x - 7) + a * 2.7f < 7 ||
                            Mathf.Abs(x + 6) < 1.2f && a > 4 && a < 8 ||
                            x < 4 && x > -10 && Mathf.Abs(a - 4) < .5f;
                    case ShotType.Boomerang:
                        return Mathf.Abs(x - (7 - a * 1.05f)) < 2.5f && a < 12 ||
                            Mathf.Abs(x - (2 - a * .8f)) < 1 && a < 9;
                    case ShotType.Explosive:
                        return Mathf.Abs(r - 8) < 1.5f ||
                            Mathf.Abs(r - 12) < 1.5f && Mathf.Sin(angle * 4 + phase) > .3f ||
                            Mathf.Abs(x) + a < 7 && Mathf.Abs(y - x * .5f) > 1;
                    default:
                        return x < -2 && Mathf.Abs(a - (x + 14) * .42f) < 1.1f ||
                            Mathf.Abs(x - 9) + a < 3;
                }
            }
            int accent = row - Elements - ShotDefinitions.Count;
            if (accent == 0)
                return Mathf.Abs(r - 12) < 1.2f && (frame < 3 || Mathf.Sin(angle * 8 + phase) > (frame - 5) * .2f);
            if (accent == 1)
                return r < 13 && r > 10 - Mathf.Sin(angle) * 2 && x > -6 + frame &&
                    Mathf.Sin(angle - phase * .15f) > -.8f;
            return (a < 1.5f && Mathf.Abs(x) < 13 - frame) ||
                (Mathf.Abs(x) < 1.5f && a < 12 - frame) ||
                (Mathf.Abs(a - Mathf.Abs(x)) < 1 && r < 9 - frame * .5f);
        }
    }
}

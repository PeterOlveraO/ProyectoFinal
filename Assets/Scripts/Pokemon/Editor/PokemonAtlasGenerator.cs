#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Pokemon.Editor
{
    /// <summary>
    /// Extrae celdas 32x32 del PNG original. Coordenadas con origen superior izquierdo:
    /// dos columnas por especie; filas: espalda, frente, perfil. Cada fila tiene dos poses.
    /// El color de fondo uniforme de cada celda se sustituye por transparencia.
    /// </summary>
    [InitializeOnLoad]
    public sealed class PokemonAtlasGenerator : IPreprocessBuildWithReport
    {
        const string Source = "Assets/Imagenes/Pokemones.png";
        const string Output = "Assets/Resources/PokemonAtlas";
        const string Version = "atlas-v2";
        // x, y de la primera celda, en píxeles del PNG sin reescalar.
        static readonly Vector2Int[] Players = {
            new Vector2Int(576,128), new Vector2Int(384,0), new Vector2Int(256,4256),
            new Vector2Int(448,1024), new Vector2Int(384,1024), new Vector2Int(0,1152),
            new Vector2Int(576,0), new Vector2Int(384,1152), new Vector2Int(512,4256),
            new Vector2Int(0,0)
        };
        static readonly Vector2Int[] Enemies = {
            new Vector2Int(640,0), new Vector2Int(704,0), new Vector2Int(768,0),
            new Vector2Int(832,0), new Vector2Int(896,0), new Vector2Int(960,0),
            new Vector2Int(0,128), new Vector2Int(64,128), new Vector2Int(128,128),
            new Vector2Int(192,128), new Vector2Int(256,128), new Vector2Int(320,128),
            new Vector2Int(384,128), new Vector2Int(448,128), new Vector2Int(512,128),
            new Vector2Int(704,128), new Vector2Int(768,128), new Vector2Int(832,128),
            new Vector2Int(896,128), new Vector2Int(960,128)
        };
        static readonly Vector2Int[] Bosses = {
            new Vector2Int(64,1152), new Vector2Int(128,1152), new Vector2Int(192,1152)
        };
        static readonly string[] Directions = { "back", "front", "side" };
        static PokemonAtlasGenerator() { EditorApplication.delayCall += Generate; }
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) { Generate(); }

        [MenuItem("Tools/Pokemon/Regenerar atlas")]
        public static void Regenerate()
        {
            if (File.Exists(Output + "/" + Version + ".txt")) File.Delete(Output + "/" + Version + ".txt");
            Generate();
        }

        static void Generate()
        {
            if (File.Exists(Output + "/" + Version + ".txt") && File.Exists(Output + "/Moltres_side_1.png")) return;
            if (!File.Exists(Source)) { Debug.LogError("Falta " + Source); return; }
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!atlas.LoadImage(File.ReadAllBytes(Source))) { Object.DestroyImmediate(atlas); return; }
            if (atlas.width != 1024 || atlas.height != 5408)
            {
                Debug.LogError("Dimensiones inesperadas de " + Source);
                Object.DestroyImmediate(atlas);
                return;
            }
            Directory.CreateDirectory(Output);
            Color32[] pixels = atlas.GetPixels32();
            WriteGroup(atlas, pixels, PokemonSpriteLibrary.PlayerNames, Players);
            WriteGroup(atlas, pixels, PokemonSpriteLibrary.EnemyNames, Enemies);
            WriteGroup(atlas, pixels, PokemonSpriteLibrary.BossNames, Bosses);
            Object.DestroyImmediate(atlas);
            AssetDatabase.Refresh();
            foreach (string file in Directory.GetFiles(Output, "*.png"))
            {
                var importer = AssetImporter.GetAtPath(file.Replace('\\', '/')) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            File.WriteAllText(Output + "/" + Version + ".txt", Source);
            AssetDatabase.Refresh();
        }

        static void WriteGroup(Texture2D atlas, Color32[] pixels, string[] names, Vector2Int[] positions)
        {
            for (int i = 0; i < names.Length; i++)
                for (int direction = 0; direction < Directions.Length; direction++)
                    for (int frame = 0; frame < 2; frame++)
                    {
                        int x = positions[i].x + frame * 32;
                        int top = positions[i].y + direction * 32;
                        Color32 background = pixels[(atlas.height - 1 - top) * atlas.width + x];
                        var cell = new Color32[32 * 32];
                        for (int row = 0; row < 32; row++)
                            for (int col = 0; col < 32; col++)
                            {
                                Color32 pixel = pixels[(atlas.height - 1 - top - row) * atlas.width + x + col];
                                if (pixel.r == background.r && pixel.g == background.g && pixel.b == background.b) pixel.a = 0;
                                cell[(31 - row) * 32 + col] = pixel;
                            }
                        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                        texture.SetPixels32(cell);
                        texture.Apply();
                        File.WriteAllBytes(Output + "/" + names[i] + "_" + Directions[direction] + "_" + frame + ".png", texture.EncodeToPNG());
                        Object.DestroyImmediate(texture);
                    }
        }
    }
}
#endif

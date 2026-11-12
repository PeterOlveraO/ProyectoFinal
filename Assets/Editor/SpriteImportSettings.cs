#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Ajustes de importación para los sprites sacados de las hojas de <c>Assets/Imagenes</c>:
    ///
    ///   * Resources/PokemonSprites  -> píxel art, filtro Point, pivote centrado.
    ///   * Resources/Decor/...       -> píxel art, filtro Point, pivote en la base
    ///                                  (así se apoyan en el bloque sin calcular offsets).
    ///
    /// Sin compresión para que los sprites de 32x32 no se emborronen al ampliarlos en 3D.
    /// </summary>
    public sealed class SpriteImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');

            bool pokemon = path.Contains("/Resources/PokemonSprites/");
            bool decor = path.Contains("/Resources/Decor/");
            if (!pokemon && !decor) return;

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pokemon ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
#endif

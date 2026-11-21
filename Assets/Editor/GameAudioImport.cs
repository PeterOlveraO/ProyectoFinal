#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Import rules apply only to the game's new original soundtrack and short cues.</summary>
    public sealed class GameAudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.Contains("/Music/");
            importer.forceToMono = !music;
            importer.loadInBackground = music;
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = .8f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = !music;
            importer.defaultSampleSettings = settings;
        }
    }
}
#endif

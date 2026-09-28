using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
public class MusicImportSettings : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith("Assets/Game/Audio/Music/"))
        {
            return;
        }

        AudioImporter importer = assetImporter as AudioImporter;
        if (importer == null)
        {
            return;
        }

        importer.loadInBackground = true;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.6f;
        settings.preloadAudioData = false;
        importer.defaultSampleSettings = settings;
    }
}
}

using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
// Applies audio import settings by folder under Assets/Game/Audio.
// Music and Ambience stream (Vorbis q0.5). Sfx, UI and Stingers decompress on load (Vorbis q0.7).
public class AudioImportRules : AssetPostprocessor
{
    const string Root = "Assets/Game/Audio/";

    void OnPreprocessAudio()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith(Root))
        {
            return;
        }

        AudioImporter importer = assetImporter as AudioImporter;
        if (importer == null)
        {
            return;
        }

        bool streaming = path.StartsWith(Root + "Music/") || path.StartsWith(Root + "Ambience/");
        bool shortClip = path.StartsWith(Root + "Sfx/") || path.StartsWith(Root + "UI/") || path.StartsWith(Root + "Stingers/");
        if (!streaming && !shortClip)
        {
            return;
        }

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        if (streaming)
        {
            importer.loadInBackground = true;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.quality = 0.5f;
            settings.preloadAudioData = false;
        }
        else
        {
            importer.loadInBackground = false;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.quality = 0.7f;
            settings.preloadAudioData = true;
        }

        importer.defaultSampleSettings = settings;
    }
}
}

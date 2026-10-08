using UnityEditor;
using UnityEngine;

namespace LanternKeeper
{
// Applies audio import settings by folder under Assets/Game/Audio.
// Music streams (Vorbis q0.5). Ambience beds and loops stay compressed in memory (Vorbis q0.5): short looping streams cost
// a stream-thread read and reopen per loop. Sfx, UI and Stingers decompress on load (Vorbis q0.7).
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

        bool streaming = path.StartsWith(Root + "Music/");
        bool bed = path.StartsWith(Root + "Ambience/");
        bool shortClip = path.StartsWith(Root + "Sfx/") || path.StartsWith(Root + "UI/") || path.StartsWith(Root + "Stingers/");
        if (!streaming && !bed && !shortClip)
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
        else if (bed)
        {
            importer.loadInBackground = true;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.quality = 0.5f;
            settings.preloadAudioData = true;
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

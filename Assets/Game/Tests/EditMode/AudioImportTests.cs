using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class AudioImportTests
{
    const string Root = "Assets/Game/Audio";

    static void Check(string folder, AudioClipLoadType expected, ref int count)
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/" + folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            Assert.IsNotNull(importer, path);
            Assert.AreEqual(expected, importer.defaultSampleSettings.loadType, path);
            Assert.AreEqual(AudioCompressionFormat.Vorbis, importer.defaultSampleSettings.compressionFormat, path);
            count++;
        }
    }

    [Test]
    public void ImportSettingsByFolder()
    {
        int streamed = 0;
        int decoded = 0;
        Check("Music", AudioClipLoadType.Streaming, ref streamed);
        Check("Ambience", AudioClipLoadType.CompressedInMemory, ref decoded);
        Check("Sfx", AudioClipLoadType.DecompressOnLoad, ref decoded);
        Check("UI", AudioClipLoadType.DecompressOnLoad, ref decoded);
        Check("Stingers", AudioClipLoadType.DecompressOnLoad, ref decoded);
        Assert.Greater(streamed, 0, "No Music or Ambience clips found");
        Assert.Greater(decoded, 0, "No Sfx, UI or Stingers clips found");
    }
}
}

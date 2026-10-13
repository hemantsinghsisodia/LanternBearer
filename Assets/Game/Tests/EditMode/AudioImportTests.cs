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

    [Test]
    public void FinaleCueAssigned()
    {
        // MusicLibrary lives in Assembly-CSharp, which this assembly can't reference: read it through SerializedObject.
        ScriptableObject library = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Game/Resources/MusicLibrary.asset");
        Assert.IsNotNull(library, "MusicLibrary missing");
        AudioClip cue = new SerializedObject(library).FindProperty("finaleCue").objectReferenceValue as AudioClip;
        Assert.IsNotNull(cue, "finaleCue is not assigned");
        bool realExists = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Music/Finale_1.ogg") != null;
        if (realExists)
        {
            Assert.AreEqual("Finale_1", cue.name, "finaleCue is the fallback while Finale_1.ogg exists");
        }

        Assert.GreaterOrEqual(cue.length, 45f);
        Assert.LessOrEqual(cue.length, 95f);
    }
}
}

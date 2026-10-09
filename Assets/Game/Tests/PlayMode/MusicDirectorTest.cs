using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LanternKeeper.Tests
{
// The music director on Island1: sync, threat, scene reset, stinger ducking and the mute rule. Game types are reached by reflection.
public class MusicDirectorTest
{
    // MusicPlayer.BaseVolume: 0.35 raised by 4 dB (x1.585). This assembly can't reference the game code, so it is repeated here.
    const float MusicPlayerBase = 0.35f * 1.58489f;
    static readonly string[] IntroKeys = { "LanternKeeperIntro_island1", "LanternKeeperIntro_island2", "LanternKeeperIntro_island3", "LanternKeeperIntro_island4" };
    readonly int[] previousIntro = new int[4];
    float previousMusicVolume;
    Object library;

    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    [SetUp]
    public void SetUp()
    {
        for (int i = 0; i < IntroKeys.Length; i++)
        {
            previousIntro[i] = PlayerPrefs.HasKey(IntroKeys[i]) ? PlayerPrefs.GetInt(IntroKeys[i], 0) : -1;
            PlayerPrefs.SetInt(IntroKeys[i], 1);
        }

        previousMusicVolume = UserSettings.MusicVolume;
        UserSettings.MusicVolume = 0.8f;
        MusicPlayerReloadMute();
    }

    [TearDown]
    public void TearDown()
    {
        UserSettings.MusicVolume = previousMusicVolume;
        MusicPlayerReloadMute();
        for (int i = 0; i < IntroKeys.Length; i++)
        {
            if (previousIntro[i] < 0)
            {
                PlayerPrefs.DeleteKey(IntroKeys[i]);
            }
            else
            {
                PlayerPrefs.SetInt(IntroKeys[i], previousIntro[i]);
            }
        }

        PlayerPrefs.Save();
        if (library != null)
        {
            Object.DestroyImmediate(library);
        }
    }

    static void MusicPlayerReloadMute()
    {
        GameType("MusicPlayer").GetMethod("ReloadMute", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
    }

    static IEnumerator LoadIsland()
    {
        SceneManager.LoadScene("Island1");
        yield return null;
        yield return null;
    }

    static Component Director()
    {
        return (Component)Object.FindAnyObjectByType(GameType("MusicDirector"));
    }

    static AudioClip TestClip(float seconds)
    {
        const int Rate = 44100;
        AudioClip clip = AudioClip.Create("test", (int)(Rate * seconds), 1, Rate, false);
        float[] data = new float[(int)(Rate * seconds)];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = Mathf.Sin(i * 0.05f) * 0.1f;
        }

        clip.SetData(data, 0);
        return clip;
    }

    // A library holding test clips for Island1: a track and a tension loop of equal length, plus stingers.
    Object MakeLibrary()
    {
        Type libraryType = GameType("MusicLibrary");
        Type islandType = libraryType.GetNestedType("IslandMusic");
        Object lib = ScriptableObject.CreateInstance(libraryType);
        object entry = Activator.CreateInstance(islandType);
        islandType.GetField("scene").SetValue(entry, "Island1");
        islandType.GetField("track").SetValue(entry, TestClip(4f));
        islandType.GetField("tension").SetValue(entry, TestClip(4f));
        Array islands = Array.CreateInstance(islandType, 1);
        islands.SetValue(entry, 0);
        libraryType.GetField("islands").SetValue(lib, islands);
        libraryType.GetField("stingerWin").SetValue(lib, TestClip(1f));
        library = lib;
        return lib;
    }

    static void Begin(Component director, Object lib)
    {
        director.GetType().GetMethod("Begin").Invoke(director, new object[] { lib });
    }

    static T Get<T>(object target, string property)
    {
        return (T)target.GetType().GetProperty(property).GetValue(target);
    }

    static AudioMixer Mixer()
    {
        object audio = GameType("AudioManager").GetProperty("Instance").GetValue(null);
        Assert.IsNotNull(audio, "AudioManager missing");
        return (AudioMixer)audio.GetType().GetProperty("Mixer").GetValue(audio);
    }

    static float MixerValue(string parameter)
    {
        float value;
        Assert.IsTrue(Mixer().GetFloat(parameter, out value), parameter + " not exposed");
        return value;
    }

    [UnityTest]
    public IEnumerator TrackAndTensionStartTogether()
    {
        yield return LoadIsland();
        Component director = Director();
        Assert.IsNotNull(director, "No MusicDirector on Island1");
        Begin(director, MakeLibrary());
        yield return new WaitForSecondsRealtime(0.5f);
        AudioSource track = Get<AudioSource>(director, "Track");
        AudioSource tension = Get<AudioSource>(director, "Tension");
        Assert.IsTrue(track.isPlaying, "track not playing");
        Assert.IsTrue(tension.isPlaying, "tension not playing");
        Assert.Less(Mathf.Abs(track.timeSamples - tension.timeSamples), 1024);
    }

    [UnityTest]
    public IEnumerator ThreatRisesWithDrainAndFallsInRing()
    {
        yield return LoadIsland();
        Component director = Director();
        Begin(director, MakeLibrary());
        Object lantern = Object.FindAnyObjectByType(GameType("Lantern"));
        Assert.IsNotNull(lantern);
        GameObject mothHost = new GameObject("TestMoth");
        mothHost.SetActive(false);
        Component moth = mothHost.AddComponent(GameType("Moth"));
        lantern.GetType().GetMethod("SetDrainModifier").Invoke(lantern, new object[] { moth, 2f });
        float deadline = Time.realtimeSinceStartup + 2f;
        while (Get<float>(director, "Threat") < 0.9f && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.GreaterOrEqual(Get<float>(director, "Threat"), 0.9f, "threat did not rise with moth drain");

        // Light a beacon on top of the lantern: the safe ring wins over everything.
        Component beacon = (Component)Object.FindAnyObjectByType(GameType("Beacon"));
        Assert.IsNotNull(beacon);
        beacon.GetType().GetProperty("IsLit").SetValue(beacon, true);
        beacon.transform.position = ((Component)lantern).transform.position;
        deadline = Time.realtimeSinceStartup + 4.5f;
        while (Get<float>(director, "Threat") > 0.05f && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.LessOrEqual(Get<float>(director, "Threat"), 0.05f, "threat did not fall inside the ring");
        Object.Destroy(mothHost);
    }

    [UnityTest]
    public IEnumerator TensionResetsOnSceneLoad()
    {
        yield return LoadIsland();
        Component director = Director();
        Begin(director, MakeLibrary());
        Object lantern = Object.FindAnyObjectByType(GameType("Lantern"));
        GameObject mothHost = new GameObject("TestMoth");
        mothHost.SetActive(false);
        Component moth = mothHost.AddComponent(GameType("Moth"));
        lantern.GetType().GetMethod("SetDrainModifier").Invoke(lantern, new object[] { moth, 2f });
        float deadline = Time.realtimeSinceStartup + 3f;
        while (Get<float>(director, "Threat") < 0.99f && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.GreaterOrEqual(Get<float>(director, "Threat"), 0.99f);
        Object.Destroy(mothHost);
        SceneManager.LoadScene("Island1");
        yield return null;
        Component fresh = Director();
        Assert.IsNotNull(fresh);
        Assert.AreEqual(0f, Get<float>(fresh, "Threat"));
        Assert.AreEqual(0f, Get<AudioSource>(fresh, "Tension").volume);
    }

    [UnityTest]
    public IEnumerator StingerDucksAndRecovers()
    {
        yield return LoadIsland();
        Component director = Director();
        Begin(director, MakeLibrary());
        yield return new WaitForSecondsRealtime(0.3f);
        director.GetType().GetMethod("PlayStinger").Invoke(director, new object[] { TestClip(1f) });
        AudioSource stingerSource = Get<AudioSource>(director, "StingerSource");
        float lowest = 0f;
        float stingerBase = MixerValue("StingerVol");
        Assert.AreEqual(2.06f, stingerBase - MixerValue("MusicVol"), 0.05f, "the Stinger group carries the part of the +4 dB the source volume (capped at 1.0) cannot");
        float stingerLowest = stingerBase;
        float end = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < end)
        {
            lowest = Mathf.Min(lowest, MixerValue("MusicDuck"));
            stingerLowest = Mathf.Min(stingerLowest, MixerValue("StingerVol"));
            yield return null;
        }

        Assert.LessOrEqual(lowest, -5.9f, "music did not duck");
        Assert.IsNotNull(stingerSource.outputAudioMixerGroup, "stinger is routed");
        Assert.AreEqual("Stinger", stingerSource.outputAudioMixerGroup.name, "stinger has its own group");
        Assert.AreEqual(1, stingerSource.outputAudioMixerGroup.audioMixer.FindMatchingGroups("Master/Stinger").Length,
            "the Stinger group sits directly under Master, outside MusicBus");
        Assert.GreaterOrEqual(stingerLowest, stingerBase - 0.01f, "the music duck must not lower the stinger");
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.GreaterOrEqual(MixerValue("MusicDuck"), -0.1f, "music duck did not release");
    }

    [UnityTest]
    public IEnumerator StingerPlaysAudibly()
    {
        yield return LoadIsland();
        Component director = Director();
        Begin(director, MakeLibrary());
        yield return new WaitForSecondsRealtime(0.2f);
        director.GetType().GetMethod("PlayStinger").Invoke(director, new object[] { TestClip(1f) });
        AudioSource stinger = Get<AudioSource>(director, "StingerSource");
        yield return null;
        Assert.IsTrue(stinger.isPlaying, "stinger is not playing");
        Assert.Greater(stinger.volume, 0f, "stinger source is silent");
    }

    [UnityTest]
    public IEnumerator StingerRespectsMusicMute()
    {
        yield return LoadIsland();
        Component director = Director();
        Object lib = MakeLibrary();
        Begin(director, lib);
        UserSettings.MusicVolume = 0f;
        MusicPlayerReloadMute();
        yield return null;
        yield return null;
        director.GetType().GetMethod("PlayStinger").Invoke(director, new object[] { TestClip(1f) });
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.IsFalse(Get<AudioSource>(director, "StingerSource").isPlaying, "stinger played while muted");
        Assert.LessOrEqual(MixerValue("MusicVol") + MixerValue("MusicDuck"), -79f, "music is not muted");
        Assert.AreEqual(0f, Get<AudioSource>(director, "Track").volume);
    }

    [UnityTest]
    public IEnumerator LowFuelDoesNotHalveTension()
    {
        yield return LoadIsland();
        Component director = Director();
        Begin(director, MakeLibrary());
        Object lantern = Object.FindAnyObjectByType(GameType("Lantern"));
        GameObject mothHost = new GameObject("TestMoth");
        mothHost.SetActive(false);
        Component moth = mothHost.AddComponent(GameType("Moth"));
        lantern.GetType().GetMethod("SetDrainModifier").Invoke(lantern, new object[] { moth, 2f });
        float max = (float)lantern.GetType().GetField("maxFuel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(lantern);
        lantern.GetType().GetMethod("SetFuel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(lantern, new object[] { max * 0.19f });
        yield return new WaitForSecondsRealtime(3.4f);
        float fuel = Get<float>(lantern, "FuelNormalized");
        Assert.Greater(fuel, 0f);
        Assert.Less(fuel, 0.2f);
        AudioSource track = Get<AudioSource>(director, "Track");
        AudioSource tension = Get<AudioSource>(director, "Tension");
        Assert.AreEqual(1f, Get<float>(director, "Threat"), 0.01f);
        Assert.AreEqual(MusicPlayerBase * 0.5f, track.volume, 0.005f, "track should dip on low fuel");
        Assert.AreEqual(MusicPlayerBase * 0.708f, tension.volume, 0.005f, "tension must not be halved by low fuel");
        Object.Destroy(mothHost);
    }
}
}

using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
public class SoundCuePlayTest
{
    const string BankPath = "Assets/Game/Audio/SoundBank.asset";
    const string MixerPath = "Assets/Game/Audio/LanternMixer.mixer";

    GameObject host;
    Component manager;
    System.Type managerType;
    float savedTimeScale;
    bool savedPause;

    [SetUp]
    public void SetUp()
    {
        savedTimeScale = Time.timeScale;
        savedPause = AudioListener.pause;
        managerType = System.Type.GetType("LanternKeeper.AudioManager, Assembly-CSharp");
        Assert.IsNotNull(managerType);
        // A scene left over from an earlier test may still own the AudioManager singleton; remove just that component.
        Object leftover = (Object)GetStatic("Instance");
        if (leftover != null)
        {
            Object.DestroyImmediate(leftover);
        }

        host = new GameObject("TestAudioManager");
        host.SetActive(false);
        manager = host.AddComponent(managerType);
#if UNITY_EDITOR
        SetField("mixer", UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath));
        SetField("bank", UnityEditor.AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath));
#endif
        host.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = savedTimeScale;
        AudioListener.pause = savedPause;
        if (host != null)
        {
            Object.DestroyImmediate(host);
        }
    }

    object GetStatic(string name)
    {
        return managerType.GetProperty(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);
    }

    void SetField(string name, object value)
    {
        managerType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager, value);
    }

    AudioSource Cue(string cue, Vector3 position)
    {
        return (AudioSource)managerType.GetMethod("PlayCue").Invoke(manager, new object[] { cue, position });
    }

    [UnityTest]
    public IEnumerator PlayCueReturnsSourceOnRightGroup()
    {
        yield return null;
        AudioSource step = Cue(SoundCues.FootstepGrass, Vector3.zero);
        Assert.IsNotNull(step);
        Assert.AreEqual("SFX", step.outputAudioMixerGroup.name);

        AudioSource click = Cue(SoundCues.UiClick, Vector3.zero);
        Assert.IsNotNull(click);
        Assert.AreEqual("UI", click.outputAudioMixerGroup.name);

        AudioSource amb = Cue(SoundCues.AmbienceIsland1, Vector3.zero);
        Assert.IsNotNull(amb);
        Assert.AreEqual("Ambience", amb.outputAudioMixerGroup.name);
    }

    [UnityTest]
    public IEnumerator MissingClipFallsBackOnce()
    {
        yield return null;
        int warnings = 0;
        Application.LogCallback count = (condition, stack, type) =>
        {
            if (type == LogType.Warning && condition.Contains(SoundCues.FireflyChime))
            {
                warnings++;
            }
        };
        Application.logMessageReceived += count;
        AudioSource source = null;
        for (int i = 0; i < 3; i++)
        {
            source = Cue(SoundCues.FireflyChime, Vector3.zero);
        }

        Application.logMessageReceived -= count;
        Assert.IsNotNull(source);
        Assert.IsNotNull(source.clip, "synth fallback clip");
        Assert.AreEqual(1, warnings);
    }

    [UnityTest]
    public IEnumerator PriorityCueNotStolen()
    {
        yield return null;
        int poolSize = (int)managerType.GetField("PoolSize", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();
        for (int i = 0; i < poolSize; i++)
        {
            Cue(SoundCues.FootstepGrass, Vector3.zero);
        }

        AudioSource steal = Cue(SoundCues.ShadeSteal, Vector3.zero);
        Assert.IsNotNull(steal);
        AudioClip clip = steal.clip;
        for (int i = 0; i < poolSize; i++)
        {
            Cue(SoundCues.FootstepDirt, Vector3.zero);
        }

        Assert.IsTrue(steal.isPlaying);
        Assert.AreSame(clip, steal.clip);
    }

    [UnityTest]
    public IEnumerator UiClickPlaysWhilePaused()
    {
        yield return null;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        UiSound.Play(SoundCues.UiClick);
        bool playing = false;
        AudioSource[] sources = host.GetComponents<AudioSource>();
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i].outputAudioMixerGroup != null && sources[i].outputAudioMixerGroup.name == "UI" && sources[i].isPlaying)
            {
                playing = true;
            }
        }

        Assert.IsTrue(playing);
    }
}
}

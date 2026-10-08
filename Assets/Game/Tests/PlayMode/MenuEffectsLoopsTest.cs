using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
public class MenuEffectsLoopsTest
{
    const string IntroKey = "LanternKeeperIntro_island1";
    bool hadIntro;
    int savedIntro;

    [SetUp]
    public void SetUp()
    {
        hadIntro = PlayerPrefs.HasKey(IntroKey);
        savedIntro = PlayerPrefs.GetInt(IntroKey, 0);
        PlayerPrefs.SetInt(IntroKey, 1);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        if (hadIntro)
        {
            PlayerPrefs.SetInt(IntroKey, savedIntro);
        }
        else
        {
            PlayerPrefs.DeleteKey(IntroKey);
        }
    }

    static Object Manager()
    {
        System.Type t = System.Type.GetType("LanternKeeper.AudioManager, Assembly-CSharp");
        Assert.IsNotNull(t, "AudioManager type");
        Object m = Object.FindAnyObjectByType(t);
        Assert.IsNotNull(m, "AudioManager in scene");
        return m;
    }

    static float CrackleVolume()
    {
        Object m = Manager();
        return (float)m.GetType().GetProperty("CrackleVolume").GetValue(m);
    }

    [UnityTest]
    public IEnumerator MenuHasNoEffectsLoops()
    {
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return new WaitForSecondsRealtime(1f);

        StringBuilder offenders = new StringBuilder();
        foreach (AudioSource s in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
        {
            string group = s.outputAudioMixerGroup != null ? s.outputAudioMixerGroup.name : "none";
            if (s.isPlaying && s.volume > 0f && (group == "SFX" || group == "Ambience"))
            {
                offenders.Append(s.gameObject.name).Append(':').Append(s.clip != null ? s.clip.name : "null").Append('@').Append(group).Append(' ');
            }
        }

        Assert.AreEqual(0, offenders.Length, "Playing on SFX/Ambience in the menu: " + offenders);
        Assert.AreEqual(0f, CrackleVolume(), "no lantern, no crackle");
    }

    [UnityTest]
    public IEnumerator IslandKeepsLanternCrackle()
    {
        yield return SceneManager.LoadSceneAsync("Island1");
        yield return new WaitForSecondsRealtime(1f);

        Assert.Greater(CrackleVolume(), 0f, "full-fuel lantern crackles on the island");
    }
}
}

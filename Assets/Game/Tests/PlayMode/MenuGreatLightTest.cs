using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// The menu's Great Light tower stays dark until Island 5 is won, then its lamp and beam come on.
public class MenuGreatLightTest
{
    const string Won5Key = "LanternKeeperWon_island5";
    const string GraphicsKey = "LanternKeeperGraphics";
    bool hadWon5;
    int savedWon5;
    int savedGraphics;

    [SetUp]
    public void SetUp()
    {
        hadWon5 = PlayerPrefs.HasKey(Won5Key);
        savedWon5 = PlayerPrefs.GetInt(Won5Key, 0);
        savedGraphics = PlayerPrefs.GetInt(GraphicsKey, 0);
    }

    [TearDown]
    public void TearDown()
    {
        if (hadWon5)
        {
            PlayerPrefs.SetInt(Won5Key, savedWon5);
        }
        else
        {
            PlayerPrefs.DeleteKey(Won5Key);
        }

        PlayerPrefs.SetInt(GraphicsKey, savedGraphics);
        PlayerPrefs.Save();
    }

    static IEnumerator Load()
    {
        AsyncOperation load = SceneManager.LoadSceneAsync("MainMenu");
        while (!load.isDone)
        {
            yield return null;
        }

        yield return null;
        yield return null;
    }

    static bool Lit()
    {
        Type type = Type.GetType("LanternKeeper.MenuGreatLight, Assembly-CSharp");
        Assert.IsNotNull(type, "MenuGreatLight type");
        UnityEngine.Object found = UnityEngine.Object.FindFirstObjectByType(type, FindObjectsInactive.Include);
        Assert.IsNotNull(found, "MenuGreatLight in MainMenu");
        PropertyInfo info = type.GetProperty("IsLit", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(info, "MenuGreatLight.IsLit");
        bool lit = (bool)info.GetValue(found);
        // The state is real: the beam is running in the scene exactly when lit.
        Type beamType = Type.GetType("LanternKeeper.LighthouseBeam, Assembly-CSharp");
        UnityEngine.Object beam = UnityEngine.Object.FindFirstObjectByType(beamType, FindObjectsInactive.Include);
        Assert.IsNotNull(beam, "the menu's LighthouseBeam");
        Assert.AreEqual(lit, ((Behaviour)beam).isActiveAndEnabled, "the beam must be active exactly when lit");
        return lit;
    }

    [UnityTest]
    public IEnumerator MenuLighthouseLitOnlyAfterFinale()
    {
        PlayerPrefs.DeleteKey(Won5Key);
        yield return Load();
        Assert.IsFalse(Lit(), "dark before Island 5 is won");

        PlayerPrefs.SetInt(Won5Key, 1);
        yield return Load();
        Assert.IsTrue(Lit(), "lit after Island 5 is won");
    }
}
}

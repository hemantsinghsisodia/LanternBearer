using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// The win and lose panels, the intro card and the toasts running on Island1. ResultPanel, IntroCard and Toasts are used
// directly (namespace LanternKeeper, assembly LanternKeeper.Hud); GameManager, Lantern and PauseScreen are reached by reflection (this assembly can't reference Assembly-CSharp). The tests touch three Island1 PlayerPrefs keys and restore them.
public class ResultPanelTest
{
    static readonly string[] Keys =
    {
        "LanternKeeperIntro_island1", "LanternKeeperBest_island1", "LanternKeeperWon_island1"
    };

    readonly List<string> savedKeys = new List<string>();
    readonly List<float> savedValues = new List<float>();

    [SetUp]
    public void SetUp()
    {
        savedKeys.Clear();
        savedValues.Clear();
        for (int i = 0; i < Keys.Length; i++)
        {
            if (PlayerPrefs.HasKey(Keys[i]))
            {
                savedKeys.Add(Keys[i]);
                savedValues.Add(PlayerPrefs.GetFloat(Keys[i], PlayerPrefs.GetInt(Keys[i], 0)));
            }
        }
        // Intro seen, so Island1 starts running instead of holding the first-visit card.
        PlayerPrefs.SetInt(Keys[0], 1);
        PlayerPrefs.DeleteKey(Keys[1]);
        PlayerPrefs.DeleteKey(Keys[2]);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        for (int i = 0; i < Keys.Length; i++)
        {
            PlayerPrefs.DeleteKey(Keys[i]);
        }
        for (int i = 0; i < savedKeys.Count; i++)
        {
            if (savedKeys[i] == Keys[0] || savedKeys[i] == Keys[2])
            {
                PlayerPrefs.SetInt(savedKeys[i], (int)savedValues[i]);
            }
            else
            {
                PlayerPrefs.SetFloat(savedKeys[i], savedValues[i]);
            }
        }
        PlayerPrefs.Save();
    }

    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    static UnityEngine.Object Find(string typeName)
    {
        UnityEngine.Object found = UnityEngine.Object.FindFirstObjectByType(GameType(typeName), FindObjectsInactive.Include);
        Assert.IsNotNull(found, typeName + " in scene");
        return found;
    }

    static IEnumerator LoadIsland1()
    {
        AsyncOperation load = SceneManager.LoadSceneAsync("Island1");
        while (!load.isDone)
        {
            yield return null;
        }
        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }
    }

    static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return null;
        }
    }

    static object Call(UnityEngine.Object target, string method, params object[] args)
    {
        return target.GetType().GetMethod(method).Invoke(target, args);
    }

    static T Get<T>(UnityEngine.Object target, string property)
    {
        return (T)target.GetType().GetProperty(property).GetValue(target);
    }

    static GameObject Selected()
    {
        return EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator WinPanelShowsAndFocusesNext()
    {
        yield return LoadIsland1();
        ResultPanel panel = UnityEngine.Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
        Assert.IsNotNull(panel, "the HUD carries a result panel");
        Assert.IsFalse(panel.IsShowing, "hidden to start");
        panel.ShowWin(65f, 70f, true, 3, 3, true);
        yield return Frames(2);
        Assert.IsTrue(panel.IsShowing);
        Assert.IsTrue(panel.NextButton.gameObject.activeInHierarchy, "Next island is offered");
        Assert.AreEqual(panel.NextButton.gameObject, Selected(), "the first (primary) button is focused");
        Assert.IsTrue(panel.NextButton.Primary);
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator LastIslandHidesNext()
    {
        yield return LoadIsland1();
        ResultPanel panel = UnityEngine.Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
        panel.ShowWin(65f, 65f, false, 3, 3, false);
        yield return Frames(2);
        Assert.IsFalse(panel.NextButton.gameObject.activeInHierarchy, "no Next island on the last island");
        Assert.AreEqual(panel.RetryButton.gameObject, Selected(), "Retry takes focus instead");
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator LosePanelRetryRestarts()
    {
        yield return LoadIsland1();
        ResultPanel panel = UnityEngine.Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
        panel.ShowLose();
        yield return Frames(2);
        Assert.IsTrue(panel.IsShowing);
        Assert.AreEqual(panel.RetryButton.gameObject, Selected(), "Retry is focused on the lose panel");
        Assert.IsFalse(panel.NextButton.gameObject.activeInHierarchy, "no Next island after losing");
        GameObject marker = new GameObject("RestartMarker");
        panel.RetryButton.onClick.Invoke();
        float waited = 0f;
        while (marker != null && waited < 30f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        Assert.IsTrue(marker == null, "Retry reloads the scene");
        yield return Frames(3);
        Assert.AreEqual("Island1", SceneManager.GetActiveScene().name);
    }

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator MenuButtonLoadsMainMenu()
    {
        yield return LoadIsland1();
        ResultPanel panel = UnityEngine.Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
        panel.ShowLose();
        yield return Frames(2);
        panel.MenuButton.onClick.Invoke();
        float waited = 0f;
        while (SceneManager.GetActiveScene().name != "MainMenu" && waited < 30f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator ResultWinsOverToastAndPause()
    {
        yield return LoadIsland1();
        UnityEngine.Object manager = Find("GameManager");
        ResultPanel panel = UnityEngine.Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
        Toasts toasts = UnityEngine.Object.FindFirstObjectByType<Toasts>(FindObjectsInactive.Include);
        IntroCard card = UnityEngine.Object.FindFirstObjectByType<IntroCard>(FindObjectsInactive.Include);

        // The intro card up, as on a first visit (the flag only; the game keeps running so the win can finish).
        manager.GetType().GetField("introLines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(manager, new[] { "Light every beacon." });
        manager.GetType().GetField("introShowing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(manager, true);
        toasts.Show("first", ToastKind.Info);
        toasts.Show("second", ToastKind.Tide);
        toasts.Show("third", ToastKind.LogPage);
        yield return Frames(3);
        Assert.IsTrue(card.IsShowing, "the intro card shows");
        Assert.IsTrue(toasts.IsShowing, "a toast shows");

        Call(manager, "Win");
        float waited = 0f;
        while (!Get<bool>(manager, "Won") && waited < 60f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        Assert.IsTrue(Get<bool>(manager, "Won"), "the round was won");
        yield return Frames(3);

        Assert.IsTrue(panel.IsShowing, "the win panel is up");
        Assert.IsTrue(panel.IsWin);
        GameObject selected = Selected();
        Assert.IsNotNull(selected, "a button has focus");
        Assert.IsTrue(selected.transform.IsChildOf(panel.transform), "focus is inside the result panel");
        Assert.IsFalse(toasts.IsShowing, "toasts are cleared");
        Assert.AreEqual(0, toasts.QueuedCount, "no toast is left in the queue");
        Assert.IsFalse(card.IsShowing, "the intro card is gone");

        Call(manager, "Pause");
        Assert.IsFalse(Get<bool>(manager, "IsPaused"), "the game cannot pause once the round is over");
        UnityEngine.Object pause = Find("PauseScreen");
        Assert.IsFalse(Get<bool>(pause, "IsShowing"), "the pause menu stays closed");
        yield return Frames(2);
        Assert.IsTrue(panel.IsShowing, "the result panel stays on top");
    }
}
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LanternKeeper.Tests
{
public class ThemedButtonSoundTests
{
    GameObject go;
    readonly List<string> cues = new List<string>();

    void OnCue(string cue)
    {
        cues.Add(cue);
    }

    [SetUp]
    public void SetUp()
    {
        cues.Clear();
        UiSound.Requested += OnCue;
    }

    [TearDown]
    public void TearDown()
    {
        UiSound.Requested -= OnCue;
        if (go != null)
        {
            Object.DestroyImmediate(go);
        }

        Undo.ClearAll();
    }

    ThemedButton SelfClosing(bool back)
    {
        go = new GameObject("SelfClosingButton");
        ThemedButton button = go.AddComponent<ThemedButton>();
        button.SetBack(back);
        button.onClick.AddListener(() => go.SetActive(false));
        return button;
    }

    [Test]
    public void ClickOnSelfClosingButtonStillPlaysClick()
    {
        ThemedButton button = SelfClosing(false);
        button.OnPointerClick(new PointerEventData(null));
        Assert.IsFalse(go.activeSelf);
        CollectionAssert.AreEqual(new[] { SoundCues.UiClick }, cues);
    }

    [Test]
    public void SubmitOnSelfClosingBackButtonStillPlaysBack()
    {
        ThemedButton button = SelfClosing(true);
        button.OnSubmit(null);
        Assert.IsFalse(go.activeSelf);
        CollectionAssert.AreEqual(new[] { SoundCues.UiBack }, cues);
    }

    [Test]
    public void HoverAndSelectionPlayNoSound()
    {
        ThemedButton button = SelfClosing(false);
        button.OnPointerEnter(new PointerEventData(null));
        button.OnSelect(null);
        CollectionAssert.IsEmpty(cues);
    }
}
}

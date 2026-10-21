using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class TouchWordingTests
{
    [Test]
    public void KeyboardWordingIsUnchanged()
    {
        Assert.AreEqual("E  Light beacon (-15)", TouchWording.BeaconLight(false, 15));
        Assert.AreEqual("E  Light the great lamp", TouchWording.GreatLampLight(false));
        Assert.AreEqual("Press any key to begin", TouchWording.CardHint(false, true));
        Assert.AreEqual("Press any key to go back", TouchWording.CardHint(false, false));
        Assert.AreEqual("Press any key to skip", TouchWording.CreditsHint(false));
    }

    [Test]
    public void TouchWordingUsesTap()
    {
        Assert.AreEqual("Tap  Light (-15)", TouchWording.BeaconLight(true, 15));
        Assert.AreEqual("Tap  Light the great lamp", TouchWording.GreatLampLight(true));
        Assert.AreEqual("Tap to continue", TouchWording.CardHint(true, true));
        Assert.AreEqual("Tap to continue", TouchWording.CardHint(true, false));
        Assert.AreEqual("Tap to skip", TouchWording.CreditsHint(true));
    }

    [Test]
    public void ControlsListSwitchesWithTheMode()
    {
        string[] keyboard = { "a", "b", "c" };
        Assert.AreSame(keyboard, TouchWording.Controls(false, keyboard));
        Assert.AreSame(TouchWording.TouchBindings, TouchWording.Controls(true, keyboard));
        Assert.AreEqual(6, TouchWording.TouchBindings.Length);
        StringAssert.Contains("left stick", TouchWording.TouchBindings[0]);
        StringAssert.Contains("drag", TouchWording.TouchBindings[1]);
        StringAssert.Contains("Sprint", TouchWording.TouchBindings[4]);
        StringAssert.Contains("toggle", TouchWording.TouchBindings[4]);
        StringAssert.Contains("Pause", TouchWording.TouchBindings[5]);
        Assert.AreEqual(TouchWording.TouchBindings.Length, TouchWording.TouchControlHints.Length);
    }

    [Test]
    public void ControlRowsAndHintsFollowTheMode()
    {
        Assert.IsTrue(TouchWording.ControlRowVisible(false, 8));
        Assert.IsTrue(TouchWording.ControlRowVisible(true, 5));
        Assert.IsFalse(TouchWording.ControlRowVisible(true, 6));
        Assert.AreEqual("kb", TouchWording.ControlHint(false, 2, "kb"));
        Assert.AreEqual(TouchWording.TouchControlHints[2], TouchWording.ControlHint(true, 2, "kb"));
        Assert.AreEqual("", TouchWording.ControlHint(true, 7, "kb"));
    }

    [Test]
    public void PortraitPausesOnlyWhilePlayingOnAnIsland()
    {
        Assert.IsTrue(PortraitPause.ShouldPause(true, true, false, false, false, false, false));
        Assert.IsFalse(PortraitPause.ShouldPause(false, true, false, false, false, false, false), "landscape never pauses");
        Assert.IsFalse(PortraitPause.ShouldPause(true, false, false, false, false, false, false), "not on an island (menu)");
        Assert.IsFalse(PortraitPause.ShouldPause(true, true, true, false, false, false, false), "already paused");
        Assert.IsFalse(PortraitPause.ShouldPause(true, true, false, true, false, false, false), "the intro card already pauses");
        Assert.IsFalse(PortraitPause.ShouldPause(true, true, false, false, true, false, false), "round over");
        Assert.IsFalse(PortraitPause.ShouldPause(true, true, false, false, false, true, false), "dawn playing");
        Assert.IsFalse(PortraitPause.ShouldPause(true, true, false, false, false, false, true), "dying");
    }

    [Test]
    public void ControlIdParsesBackToAnIndex()
    {
        int index;
        Assert.IsTrue(SettingsHints.TryControlIndex(SettingsHints.ControlId(4), out index));
        Assert.AreEqual(4, index);
        Assert.IsFalse(SettingsHints.TryControlIndex("brightness", out index));
        Assert.IsFalse(SettingsHints.TryControlIndex(null, out index));
    }
}
}

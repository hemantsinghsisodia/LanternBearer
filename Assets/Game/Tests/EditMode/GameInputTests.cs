using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class GameInputTests
{
    int frame;
    float time;

    [SetUp]
    public void SetUp()
    {
        GameInput.ResetForTests();
        frame = 100;
        time = 10f;
        GameInput.FrameSource = () => frame;
        GameInput.TimeSource = () => time;
    }

    [TearDown]
    public void TearDown()
    {
        GameInput.ResetForTests();
    }

    [Test]
    public void NothingPressedByDefault()
    {
        Assert.IsFalse(GameInput.InteractPressed());
        Assert.IsFalse(GameInput.PausePressed());
        Assert.IsFalse(GameInput.JumpPressed());
        Assert.IsFalse(GameInput.SprintHeld());
    }

    [Test]
    public void InteractVisibleToAllReadersSameFrameThenClears()
    {
        GameInput.PressInteract();
        Assert.IsTrue(GameInput.InteractPressed());
        Assert.IsTrue(GameInput.InteractPressed());
        frame++;
        Assert.IsFalse(GameInput.InteractPressed());
        frame++;
        Assert.IsFalse(GameInput.InteractPressed());
    }

    [Test]
    public void PauseVisibleSameFrameThenClears()
    {
        GameInput.PressPause();
        Assert.IsTrue(GameInput.PausePressed());
        Assert.IsTrue(GameInput.PausePressed());
        frame++;
        Assert.IsFalse(GameInput.PausePressed());
    }

    [Test]
    public void JumpVisibleSameFrameThenClears()
    {
        GameInput.PressJump();
        Assert.IsTrue(GameInput.JumpPressed());
        frame++;
        Assert.IsFalse(GameInput.JumpPressed());
    }

    [Test]
    public void SetterAfterReaderInSameFrameIsSeenNextFrameOnce()
    {
        Assert.IsFalse(GameInput.JumpPressed());
        GameInput.PressJump();
        frame++;
        Assert.IsTrue(GameInput.JumpPressed());
        Assert.IsTrue(GameInput.JumpPressed());
        frame++;
        Assert.IsFalse(GameInput.JumpPressed());
    }

    [Test]
    public void LateUpdateSetterIsSeenNextFrame()
    {
        // Update readers (nothing), then LateUpdate sets, then next frame's Update readers.
        Assert.IsFalse(GameInput.InteractPressed());
        GameInput.PressInteract();
        frame++;
        time += 0.016f;
        Assert.IsTrue(GameInput.InteractPressed());
    }

    [Test]
    public void UnreadPressExpiresAfterLifetime()
    {
        GameInput.PressInteract();
        frame += 3;
        time += GameInput.PressLifetime + 0.01f;
        Assert.IsFalse(GameInput.InteractPressed());
        frame++;
        Assert.IsFalse(GameInput.InteractPressed());
    }

    [Test]
    public void UnreadPressWithinLifetimeStillFires()
    {
        GameInput.PressInteract();
        frame += 3;
        time += GameInput.PressLifetime - 0.05f;
        Assert.IsTrue(GameInput.InteractPressed());
    }

    [Test]
    public void PressesAreIndependent()
    {
        GameInput.PressJump();
        Assert.IsFalse(GameInput.InteractPressed());
        Assert.IsFalse(GameInput.PausePressed());
    }

    [Test]
    public void TouchSprintHeldUntilReleased()
    {
        GameInput.SetTouchSprint(true);
        Assert.IsTrue(GameInput.SprintHeld());
        frame += 5;
        Assert.IsTrue(GameInput.SprintHeld());
        GameInput.SetTouchSprint(false);
        Assert.IsFalse(GameInput.SprintHeld());
    }

    [Test]
    public void SecondPressAfterConsumeIsSeenAgain()
    {
        GameInput.PressInteract();
        Assert.IsTrue(GameInput.InteractPressed());
        frame++;
        GameInput.PressInteract();
        Assert.IsTrue(GameInput.InteractPressed());
    }
}

public class TouchModeTests
{
    [TearDown]
    public void TearDown()
    {
        TouchMode.SetActiveForTests(false);
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(true, true, true)]
    public void InitialTruthTable(bool mobile, bool browser, bool expected)
    {
        Assert.AreEqual(expected, TouchMode.Initial(mobile, browser));
    }

    // current, touched, key, mouse(no recent touch), expected
    [TestCase(false, false, false, false, false)]
    [TestCase(true, false, false, false, true)]
    [TestCase(false, true, false, false, true)]
    [TestCase(true, true, false, false, true)]
    [TestCase(true, false, true, false, false)]
    [TestCase(true, false, false, true, false)]
    [TestCase(false, false, true, false, false)]
    [TestCase(false, false, false, true, false)]
    [TestCase(true, true, true, false, true)]
    [TestCase(true, true, false, true, true)]
    public void NextTruthTable(bool current, bool touched, bool key, bool mouse, bool expected)
    {
        Assert.AreEqual(expected, TouchMode.Next(current, touched, key, mouse));
    }

    [Test]
    public void MouseMoveRightAfterTouchIsIgnored()
    {
        Assert.IsFalse(TouchMode.MouseMovedWithoutRecentTouch(5f, 10.2f, 10f));
        Assert.IsTrue(TouchMode.MouseMovedWithoutRecentTouch(5f, 10.6f, 10f));
        Assert.IsFalse(TouchMode.MouseMovedWithoutRecentTouch(0.2f, 20f, 10f));
    }

    [Test]
    public void LongDragKeepsGuardAlive()
    {
        // Finger goes down at t=10 and stays down for 3 s with compat mouse moves arriving the whole time.
        float last = TouchMode.UpdateLastTouchTime(-100f, 10f, true, true);
        for (float t = 10.1f; t <= 13f; t += 0.1f)
        {
            last = TouchMode.UpdateLastTouchTime(last, t, false, true);
            Assert.IsFalse(TouchMode.MouseMovedWithoutRecentTouch(4f, t, last), "t=" + t);
        }

        // Finger lifts at 13; moves within 0.5 s still ignored, later ones count.
        last = TouchMode.UpdateLastTouchTime(last, 13.3f, false, false);
        Assert.IsFalse(TouchMode.MouseMovedWithoutRecentTouch(4f, 13.4f, last));
        Assert.IsTrue(TouchMode.MouseMovedWithoutRecentTouch(4f, 13.7f, last));
    }

    [Test]
    public void SetActiveFiresOnChangedOnlyOnChange()
    {
        int calls = 0;
        System.Action handler = () => calls++;
        TouchMode.OnChanged += handler;
        try
        {
            TouchMode.SetActiveForTests(true);
            TouchMode.SetActiveForTests(true);
            TouchMode.SetActiveForTests(false);
        }
        finally
        {
            TouchMode.OnChanged -= handler;
        }

        Assert.AreEqual(2, calls);
        Assert.IsFalse(TouchMode.Active);
    }
}
}

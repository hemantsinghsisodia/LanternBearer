using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class VirtualStickTests
{
    [Test]
    public void InsideDeadZoneIsZero()
    {
        Assert.AreEqual(Vector2.zero, VirtualStick.Evaluate(new Vector2(VirtualStick.Radius * 0.11f, 0f), VirtualStick.Radius, VirtualStick.DeadZone));
    }

    [Test]
    public void JustPastDeadZoneIsSmallButNonZero()
    {
        Vector2 output = VirtualStick.Evaluate(new Vector2(VirtualStick.Radius * 0.13f, 0f), VirtualStick.Radius, VirtualStick.DeadZone);
        Assert.Greater(output.x, 0f);
        Assert.Less(output.magnitude, 0.1f);
    }

    [Test]
    public void FullPushIsOneAndClamped()
    {
        Vector2 output = VirtualStick.Evaluate(new Vector2(0f, VirtualStick.Radius * 3f), VirtualStick.Radius, VirtualStick.DeadZone);
        Assert.AreEqual(1f, output.magnitude, 1e-4f);
        Assert.AreEqual(1f, output.y, 1e-4f);
    }

    [Test]
    public void OutputKeepsDirection()
    {
        Vector2 output = VirtualStick.Evaluate(new Vector2(-60f, 60f), VirtualStick.Radius, VirtualStick.DeadZone);
        Assert.Less(output.x, 0f);
        Assert.Greater(output.y, 0f);
        Assert.AreEqual(-output.x, output.y, 1e-4f);
    }

    [Test]
    public void KnobOffsetIsClampedToRadius()
    {
        VirtualStick stick = new VirtualStick();
        stick.Down(new Vector2(300f, 300f));
        stick.Drag(new Vector2(300f + 500f, 300f));
        Assert.AreEqual(VirtualStick.Radius, stick.KnobOffset.magnitude, 1e-3f);
        Assert.AreEqual(new Vector2(300f, 300f), stick.Origin);
    }

    [Test]
    public void InactiveStickOutputsZero()
    {
        VirtualStick stick = new VirtualStick();
        Assert.AreEqual(Vector2.zero, stick.Output);
        stick.Down(Vector2.zero);
        stick.Drag(new Vector2(100f, 0f));
        Assert.Greater(stick.Output.x, 0.5f);
        stick.Up();
        Assert.AreEqual(Vector2.zero, stick.Output);
        Assert.IsFalse(stick.Active);
    }

    [Test]
    public void AutoSprintStartsAfterFourTenthsPastNinetyPercent()
    {
        VirtualStick stick = new VirtualStick();
        stick.Down(Vector2.zero);
        stick.Drag(new Vector2(VirtualStick.Radius * 0.95f, 0f));
        for (int i = 0; i < 3; i++)
        {
            stick.Tick(0.1f);
        }

        Assert.IsFalse(stick.AutoSprint, "0.3 s is not enough");
        stick.Tick(0.1f);
        stick.Tick(0.0001f);
        Assert.IsTrue(stick.AutoSprint, "0.4 s past 90% sprints");
    }

    [Test]
    public void EaseBelowNinetyPercentResetsTheTimer()
    {
        VirtualStick stick = new VirtualStick();
        stick.Down(Vector2.zero);
        stick.Drag(new Vector2(VirtualStick.Radius, 0f));
        stick.Tick(0.35f);
        stick.Drag(new Vector2(VirtualStick.Radius * 0.8f, 0f));
        stick.Tick(0.1f);
        stick.Drag(new Vector2(VirtualStick.Radius, 0f));
        stick.Tick(0.3f);
        Assert.IsFalse(stick.AutoSprint);
        stick.Tick(0.15f);
        Assert.IsTrue(stick.AutoSprint);
    }

    [Test]
    public void LiftingClearsAutoSprint()
    {
        VirtualStick stick = new VirtualStick();
        stick.Down(Vector2.zero);
        stick.Drag(new Vector2(VirtualStick.Radius, 0f));
        stick.Tick(0.5f);
        Assert.IsTrue(stick.AutoSprint);
        stick.Up();
        Assert.IsFalse(stick.AutoSprint);
    }
}

public class FingerClaimsTests
{
    const float Width = 1920f;

    [Test]
    public void LeftHalfIsStickRightHalfIsLook()
    {
        FingerClaims claims = new FingerClaims();
        Assert.AreEqual(FingerClaims.Region.Stick, claims.Begin(1, new Vector2(300f, 400f), Width, false));
        Assert.AreEqual(FingerClaims.Region.Look, claims.Begin(2, new Vector2(1500f, 400f), Width, false));
    }

    [Test]
    public void AFingerKeepsItsRegionWhenItCrossesTheMiddle()
    {
        FingerClaims claims = new FingerClaims();
        claims.Begin(1, new Vector2(300f, 400f), Width, false);
        claims.Move(1, new Vector2(1700f, 400f));
        Assert.AreEqual(FingerClaims.Region.Stick, claims.RegionOf(1));
        Assert.IsFalse(FingerClaims.TurnsCamera(claims.RegionOf(1)));
        claims.Begin(2, new Vector2(1500f, 400f), Width, false);
        claims.Move(2, new Vector2(100f, 400f));
        Assert.AreEqual(FingerClaims.Region.Look, claims.RegionOf(2));
        Assert.IsTrue(FingerClaims.TurnsCamera(claims.RegionOf(2)));
    }

    [Test]
    public void AButtonTapNeverTurnsTheCamera()
    {
        FingerClaims claims = new FingerClaims();
        Assert.AreEqual(FingerClaims.Region.Button, claims.Begin(1, new Vector2(1700f, 200f), Width, true));
        claims.Move(1, new Vector2(1500f, 500f));
        Assert.AreEqual(FingerClaims.Region.Button, claims.RegionOf(1));
        Assert.IsFalse(FingerClaims.TurnsCamera(claims.RegionOf(1)));
    }

    [Test]
    public void ButtonOnTheLeftHalfStillWinsOverTheStick()
    {
        FingerClaims claims = new FingerClaims();
        Assert.AreEqual(FingerClaims.Region.Button, claims.Begin(1, new Vector2(100f, 100f), Width, true));
    }

    [Test]
    public void SecondStickFingerIsIgnoredUntilTheFirstLifts()
    {
        FingerClaims claims = new FingerClaims();
        claims.Begin(1, new Vector2(300f, 400f), Width, false);
        Assert.AreEqual(FingerClaims.Region.Ignored, claims.Begin(2, new Vector2(400f, 500f), Width, false));
        claims.End(1);
        Assert.AreEqual(FingerClaims.Region.Stick, claims.Begin(3, new Vector2(400f, 500f), Width, false));
    }

    [Test]
    public void MoveReturnsTheDeltaSinceTheLastPosition()
    {
        FingerClaims claims = new FingerClaims();
        claims.Begin(1, new Vector2(1500f, 400f), Width, false);
        Assert.AreEqual(new Vector2(10f, -5f), claims.Move(1, new Vector2(1510f, 395f)));
        Assert.AreEqual(new Vector2(5f, 5f), claims.Move(1, new Vector2(1515f, 400f)));
        Assert.AreEqual(Vector2.zero, claims.Move(9, new Vector2(1f, 1f)));
    }

    [Test]
    public void EndedFingerCanBeClaimedAgainAsNew()
    {
        FingerClaims claims = new FingerClaims();
        claims.Begin(1, new Vector2(1500f, 400f), Width, false);
        claims.End(1);
        Assert.AreEqual(FingerClaims.Region.None, claims.RegionOf(1));
        Assert.AreEqual(FingerClaims.Region.Stick, claims.Begin(1, new Vector2(100f, 400f), Width, false));
    }

    [Test]
    public void ReleaseExceptDropsFingersThatAreGone()
    {
        FingerClaims claims = new FingerClaims();
        claims.Begin(1, new Vector2(300f, 400f), Width, false);
        claims.Begin(2, new Vector2(1500f, 400f), Width, false);
        List<int> released = new List<int>();
        claims.ReleaseExcept(new List<int> { 2 }, released);
        CollectionAssert.AreEqual(new[] { 1 }, released);
        Assert.AreEqual(FingerClaims.Region.None, claims.RegionOf(1));
        Assert.AreEqual(FingerClaims.Region.Look, claims.RegionOf(2));
        Assert.AreEqual(FingerClaims.Region.Stick, claims.Begin(3, new Vector2(300f, 400f), Width, false));
    }

    [Test]
    public void IgnoredFingerDrivesNothing()
    {
        FingerClaims claims = new FingerClaims();
        claims.Ignore(5, new Vector2(1500f, 400f));
        Assert.AreEqual(FingerClaims.Region.Ignored, claims.RegionOf(5));
        Assert.AreEqual(FingerClaims.Region.Ignored, claims.Begin(5, new Vector2(100f, 100f), Width, true));
        Assert.IsFalse(FingerClaims.TurnsCamera(FingerClaims.Region.Ignored));
    }

    [Test]
    public void LookDegreesScaleWithDpi()
    {
        Vector2 atReference = FingerClaims.LookDegrees(new Vector2(100f, 0f), FingerClaims.ReferenceDpi);
        Assert.AreEqual(15f, atReference.x, 1e-3f);
        Vector2 dense = FingerClaims.LookDegrees(new Vector2(100f, 0f), FingerClaims.ReferenceDpi * 2f);
        Assert.AreEqual(7.5f, dense.x, 1e-3f);
        Vector2 unknown = FingerClaims.LookDegrees(new Vector2(100f, 0f), 0f);
        Assert.AreEqual(15f, unknown.x, 1e-3f);
    }
}
}

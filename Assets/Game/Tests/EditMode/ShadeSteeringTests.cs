using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class ShadeSteeringTests
{
    // A wall straight ahead (+X travel) whose normal faces back at the Shade, tilted slightly so the tangent sign is stable.
    static readonly Vector3 Forward = Vector3.right;

    static Vector3 NormalFacing(float yawDegrees)
    {
        return Quaternion.AngleAxis(yawDegrees, Vector3.up) * Vector3.left;
    }

    [Test]
    public void SidePersistsWhileTheHitContinues()
    {
        ShadeSteering steer = new ShadeSteering();
        steer.Reset(Forward);
        steer.Avoid(Forward, NormalFacing(20f), false, 0.016f);
        int first = steer.Side;
        Assert.AreNotEqual(0, first);
        // The wall normal now swings across the perpendicular: the old rule flipped the side here.
        for (int i = 0; i < 30; i++)
        {
            float yaw = i % 2 == 0 ? -20f : 20f;
            steer.Avoid(Forward, NormalFacing(yaw), false, 0.016f);
            Assert.AreEqual(first, steer.Side);
        }
    }

    [Test]
    public void SideSwitchesOnlyWhenTheChosenSideIsBlocked()
    {
        ShadeSteering steer = new ShadeSteering();
        steer.Reset(Forward);
        steer.Avoid(Forward, NormalFacing(20f), false, 0.016f);
        int first = steer.Side;
        steer.Avoid(Forward, NormalFacing(20f), false, 0.016f);
        Assert.AreEqual(first, steer.Side);
        steer.Avoid(Forward, NormalFacing(20f), true, 0.016f);
        Assert.AreEqual(-first, steer.Side);
    }

    [Test]
    public void SideClearsAfterTheObstacleIsGone()
    {
        ShadeSteering steer = new ShadeSteering();
        steer.Reset(Forward);
        steer.Avoid(Forward, NormalFacing(20f), false, 0.016f);
        Assert.AreNotEqual(0, steer.Side);
        steer.NoHit(0.3f);
        Assert.AreNotEqual(0, steer.Side);
        steer.NoHit(0.15f);
        Assert.AreEqual(0, steer.Side);
    }

    [Test]
    public void StuckFiresAfterOneAndAHalfSecondsWithoutMovement()
    {
        ShadeSteering steer = new ShadeSteering();
        steer.Reset(Forward);
        Vector3 spot = new Vector3(5f, 0f, 5f);
        bool fired = false;
        float t = 0f;
        for (int i = 0; i < 200 && !fired; i++)
        {
            fired = steer.TickStuck(spot + Vector3.right * 0.05f * (i % 2), 0.01f, true);
            t += 0.01f;
        }

        Assert.IsTrue(fired);
        Assert.AreEqual(1.5f, t, 0.03f);
    }

    [Test]
    public void StuckDoesNotFireWhenTheShadeIsMoving()
    {
        ShadeSteering steer = new ShadeSteering();
        steer.Reset(Forward);
        for (int i = 0; i < 400; i++)
        {
            Vector3 position = new Vector3(i * 0.01f * 4.5f, 0f, 0f);
            Assert.IsFalse(steer.TickStuck(position, 0.01f, true));
        }
    }

    [Test]
    public void StuckDoesNotFireWhenNotEligible()
    {
        ShadeSteering steer = new ShadeSteering();
        steer.Reset(Forward);
        for (int i = 0; i < 400; i++)
        {
            Assert.IsFalse(steer.TickStuck(Vector3.zero, 0.01f, false));
        }
    }

    [Test]
    public void EscapeHeadsOffAxisThenReleases()
    {
        ShadeSteering steer = new ShadeSteering();
        steer.Reset(Forward);
        steer.BeginEscape(Forward, true, false);
        Assert.IsTrue(steer.Escaping);
        float angle = Vector3.Angle(Forward, steer.EscapeDirection);
        Assert.GreaterOrEqual(angle, 60f);
        Assert.LessOrEqual(angle, 90f);
        // Only the walkable side (here the left one, -90 degrees about up) is taken.
        Assert.Less(Vector3.Cross(Forward, steer.EscapeDirection).y, 0f);
        for (int i = 0; i < 40; i++)
        {
            steer.TickStuck(new Vector3(i, 0f, 0f), 0.02f, true);
        }

        Assert.IsTrue(steer.Escaping);
        for (int i = 0; i < 20; i++)
        {
            steer.TickStuck(new Vector3(40f + i, 0f, 0f), 0.02f, true);
        }

        Assert.IsFalse(steer.Escaping);
    }

    [Test]
    public void EscapePrefersTheWalkableSide()
    {
        ShadeSteering steer = new ShadeSteering();
        steer.Reset(Forward);
        steer.BeginEscape(Forward, false, true);
        Assert.Greater(Vector3.Cross(Forward, steer.EscapeDirection).y, 0f);
    }

    [Test]
    public void TurnObeysTheMaxTurnRate()
    {
        ShadeSteering steer = new ShadeSteering();
        steer.Reset(Vector3.right);
        Vector3 heading = steer.Turn(Vector3.forward, 0.1f);
        float turned = Vector3.Angle(Vector3.right, heading);
        Assert.AreEqual(ShadeSteering.MaxTurnDegreesPerSecond * 0.1f, turned, 0.5f);
        // A reversal takes several frames, never a snap.
        steer.Reset(Vector3.right);
        heading = steer.Turn(Vector3.left, 0.016f);
        Assert.Less(Vector3.Angle(Vector3.right, heading), ShadeSteering.MaxTurnDegreesPerSecond * 0.016f + 0.5f);
    }

    [Test]
    public void TurnReachesTheDesiredHeadingEventually()
    {
        ShadeSteering steer = new ShadeSteering();
        steer.Reset(Vector3.right);
        Vector3 heading = Vector3.right;
        for (int i = 0; i < 120; i++)
        {
            heading = steer.Turn(Vector3.left, 0.016f);
        }

        Assert.Less(Vector3.Angle(heading, Vector3.left), 0.5f);
        Assert.AreEqual(1f, heading.magnitude, 0.001f);
        Assert.AreEqual(0f, heading.y, 0.0001f);
    }

    [Test]
    public void ZeroDeltaTimeDoesNothing()
    {
        ShadeSteering steer = new ShadeSteering();
        steer.Reset(Vector3.right);
        Vector3 before = steer.Heading;
        Vector3 heading = steer.Turn(Vector3.forward, 0f);
        Assert.AreEqual(0f, Vector3.Angle(before, heading), 0.0001f);
        Assert.IsFalse(steer.TickStuck(Vector3.zero, 0f, true));
        steer.Avoid(Forward, NormalFacing(20f), false, 0f);
        steer.NoHit(0f);
        Assert.AreEqual(0f, Vector3.Angle(before, steer.Heading), 0.0001f);
    }
}
}

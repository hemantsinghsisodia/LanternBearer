using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class FinaleFlightPathTests
{
    const float Step = 0.05f;
    // The gameplay camera's vertical FOV and the widest picture the finale is shot for.
    const float VerticalFov = 58f;
    const float Aspect = 16f / 9f;

    static Vector3 FlameRelative { get { return new Vector3(0f, FinaleFlightPath.FlameHeight, 0f); } }

    // Degrees between the view direction and the direction to a point.
    static float AngleTo(float t, Vector3 point)
    {
        return Vector3.Angle(FinaleFlightPath.Forward(t), point - FinaleFlightPath.Position(t));
    }

    // True if the point is inside the view frustum's left/right/top/bottom at time t.
    static bool InFrame(float t, Vector3 point)
    {
        Quaternion look = Quaternion.LookRotation(FinaleFlightPath.Forward(t), Vector3.up);
        Vector3 local = Quaternion.Inverse(look) * (point - FinaleFlightPath.Position(t));
        if (local.z <= 0.1f)
        {
            return false;
        }

        float halfV = Mathf.Tan(VerticalFov * 0.5f * Mathf.Deg2Rad);
        return Mathf.Abs(local.y / local.z) <= halfV && Mathf.Abs(local.x / local.z) <= halfV * Aspect;
    }

    [Test]
    public void StartsOnTheLanternRoom()
    {
        Vector3 start = FinaleFlightPath.Position(0f);
        Assert.Less((start - FlameRelative).magnitude, 20f, "the ignition is close to the lantern room");
        Assert.Less(AngleTo(0f, FlameRelative), 2f, "and looks at it");
    }

    [Test]
    public void PathIsContinuous()
    {
        Vector3 prevPosition = FinaleFlightPath.Position(0f);
        Vector3 prevLook = FinaleFlightPath.LookTarget(0f);
        Vector3 prevForward = FinaleFlightPath.Forward(0f);
        for (float t = Step; t <= FinaleTimeline.Total + 0.0001f; t += Step)
        {
            Vector3 position = FinaleFlightPath.Position(t);
            Vector3 look = FinaleFlightPath.LookTarget(t);
            Vector3 forward = FinaleFlightPath.Forward(t);
            Assert.Less((position - prevPosition).magnitude / Step, 60f, "camera speed (m/s) stays flyable at t=" + t);
            Assert.Less((look - prevLook).magnitude / Step, 120f, "the aim point never jumps at t=" + t);
            Assert.Less(Vector3.Angle(forward, prevForward) / Step, 40f, "the camera never whips (deg/s) at t=" + t);
            prevPosition = position;
            prevLook = look;
            prevForward = forward;
        }
    }

    [Test]
    public void SpeedHasNoJerks()
    {
        // No sudden change of velocity: the acceleration stays modest along the whole path.
        Vector3 prevVelocity = (FinaleFlightPath.Position(Step) - FinaleFlightPath.Position(0f)) / Step;
        for (float t = 2f * Step; t <= FinaleTimeline.Total; t += Step)
        {
            Vector3 velocity = (FinaleFlightPath.Position(t) - FinaleFlightPath.Position(t - Step)) / Step;
            Assert.Less((velocity - prevVelocity).magnitude / Step, 40f, "acceleration (m/s^2) at t=" + t);
            prevVelocity = velocity;
        }
    }

    [Test]
    public void ComesToRestAtBothEnds()
    {
        Assert.Less((FinaleFlightPath.Position(Step) - FinaleFlightPath.Position(0f)).magnitude / Step, 1f);
        Assert.Less((FinaleFlightPath.Position(FinaleTimeline.Total) - FinaleFlightPath.Position(FinaleTimeline.Total - Step)).magnitude / Step, 1.5f);
    }

    [Test]
    public void PitchNeverSteeperThanTwentyDegreesDown()
    {
        for (float t = 0f; t <= FinaleTimeline.Total + 0.0001f; t += Step)
        {
            Assert.LessOrEqual(FinaleFlightPath.DownPitch(t), FinaleFlightPath.MaxDownPitch, "pitch at t=" + t);
        }
    }

    [Test]
    public void StaysAboveTheSea()
    {
        const float seaRelative = -40f;
        for (float t = 0f; t <= FinaleTimeline.Total; t += Step)
        {
            Assert.Greater(FinaleFlightPath.Position(t).y, seaRelative + 15f, "camera height at t=" + t);
        }
    }

    [Test]
    public void PassesEachIslandAtItsFlareTime()
    {
        for (int i = 0; i < 4; i++)
        {
            float t = FinaleTimeline.IslandFlareTime(i);
            Vector3 flare = FinaleFlightPath.FlarePoint(i);
            float distance = (flare - FinaleFlightPath.Position(t)).magnitude;
            Assert.GreaterOrEqual(distance, 80f, "island " + (i + 1) + " is not too close");
            Assert.LessOrEqual(distance, 150f, "island " + (i + 1) + " is not too far");
            Vector3 aim = flare + Vector3.up * (FinaleFlightPath.AimHeight * 0.5f);
            Assert.Less(AngleTo(t, aim), 12f, "island " + (i + 1) + " is centred-ish");
            Assert.IsTrue(InFrame(t, flare), "the beacon of island " + (i + 1) + " is in the frame");
            Assert.IsTrue(InFrame(t, flare + Vector3.up * 60f), "and so is its light column");
            float height = FinaleFlightPath.Position(t).y + 40f;
            Assert.GreaterOrEqual(height, 40f, "above the sea by 40 m or more");
            Assert.LessOrEqual(height, 80f, "and by 80 m or less");
        }
    }

    [Test]
    public void NoHardCutBetweenIslands()
    {
        // The camera keeps moving through the whole flyby (never stops), and the aim stays inside the chain.
        for (float t = FinaleTimeline.IslandsStart; t <= FinaleTimeline.SunriseStart; t += Step)
        {
            Vector3 velocity = (FinaleFlightPath.Position(t + Step) - FinaleFlightPath.Position(t)) / Step;
            Assert.Greater(velocity.magnitude, 3.5f, "the flyby keeps gliding at t=" + t);
        }
    }

    [Test]
    public void LampInFrameAtTheCheckpoints()
    {
        float[] checkpoints = { 3f, 15f, 50f, 64f };
        for (int i = 0; i < checkpoints.Length; i++)
        {
            Assert.IsTrue(InFrame(checkpoints[i], FlameRelative), "the lamp is in the frame at " + checkpoints[i] + " s");
        }
    }

    [Test]
    public void SunriseLooksAtTheSunWithTheLampLow()
    {
        for (float t = FinaleTimeline.HoldStart; t <= FinaleTimeline.Total; t += 1f)
        {
            float bearing = FinaleFlightPath.LookBearing(t);
            Assert.AreEqual(FinaleFlightPath.SunBearing, bearing, 12f, "the hold looks towards the sun at t=" + t);
            Vector3 position = FinaleFlightPath.Position(t);
            Vector3 toLamp = FlameRelative - position;
            Assert.Greater(toLamp.magnitude, 30f);
            Assert.Less(toLamp.magnitude, 120f, "the lighthouse is in the foreground");
            Quaternion look = Quaternion.LookRotation(FinaleFlightPath.Forward(t), Vector3.up);
            Vector3 local = Quaternion.Inverse(look) * toLamp;
            Assert.Less(local.y / local.z, -0.05f, "the lamp sits in the lower part of the frame at t=" + t);
        }
    }

    [Test]
    public void LookBearingIsContinuous()
    {
        float prev = FinaleFlightPath.LookBearing(0f);
        for (float t = Step; t <= FinaleTimeline.Total; t += Step)
        {
            float bearing = FinaleFlightPath.LookBearing(t);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(prev, bearing)) / Step, 90f, "the cloud window never jumps at t=" + t);
            prev = bearing;
        }
    }
}
}

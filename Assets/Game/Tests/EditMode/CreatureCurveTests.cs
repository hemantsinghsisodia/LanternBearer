using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class CreatureCurveTests
{
    static void AssertHex(string hex, string expected, string name)
    {
        Assert.AreEqual(expected, hex, name);
        Assert.AreEqual(expected, LookPalette.ToHex(LookPalette.FromHex(hex)), name + " roundtrip");
    }

    [Test]
    public void IntensityReachesFullBy1s()
    {
        Assert.AreEqual(0f, BeaconLightCurve.Intensity01(0f), 1e-6f);
        Assert.AreEqual(1f, BeaconLightCurve.Intensity01(1f), 1e-6f);
        Assert.AreEqual(1f, BeaconLightCurve.Intensity01(5f), 1e-6f);
        float prev = -1f;
        for (float t = 0f; t <= 1.5f; t += 0.01f)
        {
            float v = BeaconLightCurve.Intensity01(t);
            Assert.GreaterOrEqual(v, prev - 1e-6f, "monotonic at " + t);
            prev = v;
        }
    }

    [Test]
    public void RingStartsAt0_3AndReachesMaxBy1_2()
    {
        Assert.AreEqual(0f, BeaconLightCurve.RingRadius(0f, 8f), 1e-6f);
        Assert.AreEqual(0f, BeaconLightCurve.RingRadius(0.3f, 8f), 1e-6f);
        Assert.Greater(BeaconLightCurve.RingRadius(0.7f, 8f), 0f);
        Assert.Less(BeaconLightCurve.RingRadius(0.7f, 8f), 8f);
        Assert.AreEqual(8f, BeaconLightCurve.RingRadius(1.2f, 8f), 1e-5f);
        Assert.AreEqual(8f, BeaconLightCurve.RingRadius(3f, 8f), 1e-5f);
    }

    [Test]
    public void FlareIsEarlyOnly()
    {
        Assert.Greater(BeaconLightCurve.Flare(0.08f), 0.8f);
        Assert.AreEqual(0f, BeaconLightCurve.Flare(0.5f), 1e-6f);
        Assert.AreEqual(0f, BeaconLightCurve.Flare(0.3f), 1e-6f);
        float peakT = 0f;
        float peak = -1f;
        for (float t = 0f; t <= 0.3f; t += 0.005f)
        {
            float v = BeaconLightCurve.Flare(t);
            if (v > peak)
            {
                peak = v;
                peakT = t;
            }
        }

        Assert.LessOrEqual(peakT, 0.15f);
    }

    [Test]
    public void EmbersWindow()
    {
        Assert.AreEqual(0f, BeaconLightCurve.EmberRate01(0.05f), 1e-6f);
        Assert.Greater(BeaconLightCurve.EmberRate01(0.12f), 0f);
        Assert.Greater(BeaconLightCurve.EmberRate01(0.8f), 0f);
        Assert.Greater(BeaconLightCurve.EmberRate01(1.45f), 0f);
        Assert.AreEqual(0f, BeaconLightCurve.EmberRate01(1.6f), 1e-6f);
        Assert.AreEqual(1.5f, BeaconLightCurve.Duration, 1e-6f);
    }

    [Test]
    public void FlapAngleBounded()
    {
        for (int p = 0; p < 50; p++)
        {
            float phase = p * 0.731f;
            for (float t = 0f; t < 10f; t += 0.003f)
            {
                float glide = MothFlap.GlideFactor(t, phase);
                Assert.GreaterOrEqual(glide, 0f);
                Assert.LessOrEqual(glide, 1f);
                float a = MothFlap.Angle(t, phase, MothFlap.MaxHz, glide);
                Assert.LessOrEqual(Mathf.Abs(a), 55f + 1e-3f);
                a = MothFlap.Angle(t, phase, MothFlap.MinHz, 0f);
                Assert.LessOrEqual(Mathf.Abs(a), 55f + 1e-3f);
            }
        }
    }

    [Test]
    public void FlapRateInRange()
    {
        Assert.AreEqual(14f, MothFlap.MinHz);
        Assert.AreEqual(20f, MothFlap.MaxHz);
        foreach (float hz in new[] { MothFlap.MinHz, MothFlap.MaxHz })
        {
            int crossings = 0;
            float prev = MothFlap.Angle(0f, 0f, hz, 0f);
            for (float t = 0.0005f; t <= 1f; t += 0.0005f)
            {
                float a = MothFlap.Angle(t, 0f, hz, 0f);
                if (prev < 0f && a >= 0f)
                {
                    crossings++;
                }

                prev = a;
            }

            Assert.AreEqual(hz, crossings, 1.01f, "upward zero crossings per second at " + hz);
        }
    }

    [Test]
    public void GlideIsDeterministicAndAboutOneSecondEveryThreeToSix()
    {
        Assert.AreEqual(MothFlap.GlideFactor(7.3f, 1.2f), MothFlap.GlideFactor(7.3f, 1.2f));
        float glideTime = 0f;
        const float dt = 0.01f;
        const float total = 120f;
        for (float t = 0f; t < total; t += dt)
        {
            if (MothFlap.GlideFactor(t, 0.4f) > 0.5f)
            {
                glideTime += dt;
            }
        }

        float fraction = glideTime / total;
        Assert.Greater(fraction, 1f / 8f);
        Assert.Less(fraction, 1f / 2.5f);
    }

    [Test]
    public void SignalColoursExact()
    {
        AssertHex(LookPalette.DrainViolet, "B48CFF", "drain violet");
        AssertHex(LookPalette.LightningBlue, "BCD2FF", "lightning blue");
        AssertHex(LookPalette.LanternAmber, "FFB15C", "lantern amber");
        AssertHex(LookPalette.GlowCore, "FFD38A", "glow core");
    }
}
}

using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class LanternFlameMappingTests
{
    const float ColourTolerance = 1f / 255f;

    [Test]
    public void HeightEndpointsAndClamp()
    {
        Assert.AreEqual(0.45f, LanternFlameMapping.Height(0f), 1e-5f);
        Assert.AreEqual(1f, LanternFlameMapping.Height(1f), 1e-5f);
        Assert.AreEqual(0.45f, LanternFlameMapping.Height(-1f), 1e-5f);
        Assert.AreEqual(1f, LanternFlameMapping.Height(2f), 1e-5f);
    }

    [Test]
    public void HeightIsMonotonic()
    {
        float previous = LanternFlameMapping.Height(0f);
        for (int i = 1; i <= 10; i++)
        {
            float h = LanternFlameMapping.Height(i / 10f);
            Assert.GreaterOrEqual(h, previous, "height dropped at sample " + i);
            previous = h;
        }
    }

    [Test]
    public void FlameColourEndpoints()
    {
        Color full = LanternFlameMapping.FlameColour(1f);
        Color empty = LanternFlameMapping.FlameColour(0f);
        Assert.AreEqual(0xFF / 255f, full.r, ColourTolerance);
        Assert.AreEqual(0xD3 / 255f, full.g, ColourTolerance);
        Assert.AreEqual(0x8A / 255f, full.b, ColourTolerance);
        Assert.AreEqual(0xE8 / 255f, empty.r, ColourTolerance);
        Assert.AreEqual(0x50 / 255f, empty.g, ColourTolerance);
        Assert.AreEqual(0x2A / 255f, empty.b, ColourTolerance);
        Assert.AreEqual(1f, full.a, 1e-5f);
        Assert.AreEqual(1f, empty.a, 1e-5f);
    }

    [Test]
    public void HaloScaleAtEmpty()
    {
        Assert.AreEqual(0.55f, LanternFlameMapping.HaloScale(0f), 1e-5f);
        Assert.AreEqual(1f, LanternFlameMapping.HaloScale(1f), 1e-5f);
    }
}
}

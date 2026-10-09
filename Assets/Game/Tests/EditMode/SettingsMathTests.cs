using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class SettingsMathTests
{
    [Test]
    public void ZeroVolumeIsSilent()
    {
        Assert.AreEqual(-80f, SettingsMath.VolumeToDb(0f));
    }

    [Test]
    public void FullVolumeIsZeroDb()
    {
        Assert.AreEqual(0f, SettingsMath.VolumeToDb(1f));
    }

    [Test]
    public void HalfVolume()
    {
        Assert.AreEqual(-6.02f, SettingsMath.VolumeToDb(0.5f), 0.01f);
    }

    [Test]
    public void BrightnessClamps()
    {
        Assert.AreEqual(1f, SettingsMath.BrightnessToEv(2f));
        Assert.AreEqual(-1f, SettingsMath.BrightnessToEv(-3f));
    }

    [Test]
    public void FlashCap()
    {
        Assert.AreEqual(0.35f, SettingsMath.FlashCap(true));
        Assert.AreEqual(1f, SettingsMath.FlashCap(false));
    }

    [Test]
    public void PulseHalved()
    {
        Assert.AreEqual(2f, SettingsMath.LowFuelPulseRate(4f, true));
        Assert.AreEqual(4f, SettingsMath.LowFuelPulseRate(4f, false));
    }

    [Test]
    public void MothPulsePeriodHalvesRateWithReduceFlashing()
    {
        Assert.AreEqual(1f, SettingsMath.MothPulsePeriod(false), "about 1 Hz");
        Assert.AreEqual(2f, SettingsMath.MothPulsePeriod(true), "half the rate with Reduce flashing");
    }

    [Test]
    public void RenderScaleOptionsAndLabels()
    {
        CollectionAssert.AreEqual(new[] { 1f, 1.25f, 1.5f, 2f }, SettingsMath.RenderScales);
        CollectionAssert.AreEqual(new[] { "100%", "125%", "150%", "200% (4K at 1080p)" }, SettingsMath.RenderScaleLabels());
        Assert.AreEqual(2, SettingsMath.RenderScaleIndex(1.5f));
        Assert.AreEqual(0, SettingsMath.RenderScaleIndex(0.2f));
        Assert.AreEqual(3, SettingsMath.RenderScaleIndex(7f));
    }

    [Test]
    public void EffectiveRenderScaleIsPresetTimesSettingClamped()
    {
        Assert.AreEqual(1f, SettingsMath.EffectiveRenderScale(1f, 1f), 0.0001f);
        Assert.AreEqual(1.25f, SettingsMath.EffectiveRenderScale(1f, 1.25f), 0.0001f);
        Assert.AreEqual(1f, SettingsMath.EffectiveRenderScale(0.5f, 2f), 0.0001f, "Low x 200%");
        Assert.AreEqual(0.5f, SettingsMath.EffectiveRenderScale(0.5f, 1f), 0.0001f);
        Assert.AreEqual(2f, SettingsMath.EffectiveRenderScale(1.5f, 2f), 0.0001f, "clamped to 2");
        Assert.AreEqual(0.5f, SettingsMath.EffectiveRenderScale(0.2f, 1f), 0.0001f, "clamped to 0.5");
    }
}
}

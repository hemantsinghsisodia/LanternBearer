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
}
}

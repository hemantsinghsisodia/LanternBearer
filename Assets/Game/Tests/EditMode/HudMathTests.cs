using NUnit.Framework;

namespace LanternKeeper.Tests
{
public class HudMathTests
{
    [Test]
    public void FlameScaleAndRedness()
    {
        HudMath.FlameLook full = HudMath.Flame(1f, 0f, false);
        Assert.AreEqual(1f, full.Scale, 1e-4f);
        Assert.AreEqual(0f, full.Redness, 1e-4f);

        Assert.AreEqual(0f, HudMath.Flame(0.30f, 0f, false).Redness, 1e-4f);
        Assert.AreEqual(0f, HudMath.Flame(0.6f, 0f, false).Redness, 1e-4f);

        HudMath.FlameLook empty = HudMath.Flame(0f, 0f, false);
        Assert.AreEqual(1f, empty.Redness, 1e-4f);
        Assert.AreEqual(0.35f, empty.Scale, 1e-4f);

        Assert.AreEqual(0.5f, HudMath.Flame(0.15f, 0f, false).Redness, 1e-4f);
    }

    [Test]
    public void PulseOnlyBelow15AndHalvedWhenReduced()
    {
        Assert.AreEqual(0f, HudMath.Flame(0.15f, 0.125f, false).Pulse);
        Assert.AreEqual(0f, HudMath.Flame(0.5f, 0.125f, false).Pulse);

        // Normal: 2 Hz, period 0.5 s.
        float a = HudMath.Flame(0.10f, 0.1f, false).Pulse;
        float b = HudMath.Flame(0.10f, 0.6f, false).Pulse;
        Assert.AreEqual(a, b, 1e-3f);
        Assert.AreEqual(1f, HudMath.Flame(0.10f, 0.125f, false).Pulse, 1e-3f);
        Assert.AreEqual(0f, HudMath.Flame(0.10f, 0.375f, false).Pulse, 1e-3f);

        // Reduced flashing: 1 Hz, period 1 s, so t = 0.375 is not yet at the minimum.
        Assert.AreEqual(HudMath.Flame(0.10f, 0.1f, true).Pulse, HudMath.Flame(0.10f, 1.1f, true).Pulse, 1e-3f);
        Assert.AreEqual(1f, HudMath.Flame(0.10f, 0.25f, true).Pulse, 1e-3f);
        Assert.AreEqual(0f, HudMath.Flame(0.10f, 0.75f, true).Pulse, 1e-3f);
    }

    [Test]
    public void DrainIconsFromState()
    {
        HudMath.DrainIconsView idle = HudMath.DrainIcons(new HudMath.DrainState { DrainRelative = 1f });
        Assert.IsFalse(idle.Moth);
        Assert.IsFalse(idle.Shield);
        Assert.IsFalse(idle.Ember);
        Assert.IsFalse(idle.Multiplier);

        HudMath.DrainIconsView moths = HudMath.DrainIcons(new HudMath.DrainState { MothsDraining = 3, DrainRelative = 1f });
        Assert.IsTrue(moths.Moth);
        Assert.AreEqual(3, moths.MothCount);

        HudMath.DrainIconsView sprint = HudMath.DrainIcons(new HudMath.DrainState { Sprinting = true, DrainRelative = 1.5f });
        Assert.IsTrue(sprint.Ember);
        Assert.IsTrue(sprint.Multiplier);
        Assert.AreEqual("×1.5", sprint.MultiplierText);

        // Safe light: no moths, shield only, ember and multiplier hidden.
        HudMath.DrainIconsView safe = HudMath.DrainIcons(new HudMath.DrainState { MothsDraining = 0, InSafeLight = true, Sprinting = true, DrainRelative = 2f });
        Assert.IsFalse(safe.Moth);
        Assert.IsTrue(safe.Shield);
        Assert.IsFalse(safe.Ember);
        Assert.IsFalse(safe.Multiplier);

        Assert.IsFalse(HudMath.DrainIcons(new HudMath.DrainState { DrainRelative = 1.05f }).Multiplier);
    }

    [Test]
    public void FlashCoalescesRapidChanges()
    {
        var flash = new HudMath.FuelFlash();
        for (int i = 0; i < 10; i++)
        {
            flash.Add(-1f, i * 0.05f);
        }

        Assert.AreEqual("−10", flash.Text);
        Assert.IsTrue(flash.Visible(0.45f + 0.9f));
        Assert.IsFalse(flash.Visible(0.45f + 1.0f));

        // A change more than 1 s after the last starts a new total.
        flash.Add(-3f, 2f);
        Assert.AreEqual("−3", flash.Text);
    }

    [Test]
    public void FlashSignFormatting()
    {
        var flash = new HudMath.FuelFlash();
        flash.Add(-12f, 0f);
        Assert.AreEqual("−12", flash.Text);

        var gain = new HudMath.FuelFlash();
        gain.Add(5f, 0f);
        Assert.AreEqual("+5", gain.Text);

        var round = new HudMath.FuelFlash();
        round.Add(4.6f, 0f);
        Assert.AreEqual("+5", round.Text);

        Assert.IsFalse(new HudMath.FuelFlash().Visible(0f));
    }
}
}

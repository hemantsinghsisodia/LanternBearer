using UnityEngine;

namespace LanternKeeper
{
// Pure HUD maths: flame look from fuel, drain icon visibility, fuel change flash. No scene access.
public static class HudMath
{
    public struct FlameLook
    {
        public float Scale, Glow, Redness, Pulse;
    }

    // Scale 0.35..1 from fuel. Redness 0 at 30% and above, 1 at empty. Pulse only below 15%: 2 Hz sine, 1 Hz when reduced.
    public static FlameLook Flame(float fuel01, float time, bool reduceFlashing)
    {
        float fuel = float.IsNaN(fuel01) ? 0f : Mathf.Clamp01(fuel01);
        FlameLook look = new FlameLook();
        look.Scale = 0.35f + 0.65f * fuel;
        look.Glow = fuel;
        look.Redness = Mathf.Clamp01(1f - fuel / 0.30f);
        if (fuel < 0.15f)
        {
            float hz = reduceFlashing ? 1f : 2f;
            look.Pulse = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * hz * time);
        }

        return look;
    }

    public struct DrainState
    {
        public int MothsDraining;
        public bool InSafeLight;
        public bool Sprinting;
        public float DrainRelative;
    }

    public struct DrainIconsView
    {
        public bool Moth;
        public int MothCount;
        public bool Shield;
        public bool Ember;
        public bool Multiplier;
        public string MultiplierText;
    }

    public static DrainIconsView DrainIcons(DrainState s)
    {
        DrainIconsView v = new DrainIconsView();
        v.Moth = s.MothsDraining > 0;
        v.MothCount = s.MothsDraining;
        v.Shield = s.InSafeLight;
        v.Ember = s.Sprinting && !s.InSafeLight;
        v.Multiplier = s.DrainRelative > 1.05f && !s.InSafeLight;
        v.MultiplierText = v.Multiplier ? MultiplierString(s.DrainRelative) : string.Empty;
        return v;
    }

    static float cachedMultiplier = float.NaN;
    static string cachedMultiplierText = string.Empty;

    // Rebuilt only when the rounded (one decimal) multiplier changes, so the per-frame HUD update does not allocate.
    static string MultiplierString(float relative)
    {
        float rounded = Mathf.Round(relative * 10f) / 10f;
        if (rounded != cachedMultiplier)
        {
            cachedMultiplier = rounded;
            cachedMultiplierText = "×" + relative.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }
        return cachedMultiplierText;
    }

    // mm:ss, or --:-- for a negative (unset) time. The one place the timer / best-time format lives.
    public static string FormatTime(float seconds)
    {
        if (seconds < 0f)
        {
            return "--:--";
        }
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
    }

    // Coalesces fuel changes made within 1 s of each other into one running total, shown for 1 s after the last change.
    public class FuelFlash
    {
        const float Window = 1f;
        const float MaxTotal = 99999f;

        float total;
        float lastTime = float.NegativeInfinity;

        public void Add(float delta, float now)
        {
            if (float.IsNaN(delta))
            {
                return;
            }

            if (now - lastTime >= Window)
            {
                total = 0f;
            }

            total = Mathf.Clamp(total + delta, -MaxTotal, MaxTotal);
            lastTime = now;
        }

        public bool Visible(float now)
        {
            return now - lastTime < Window;
        }

        // 1 right after a change, falling to 0 as the window ends.
        public float Fade(float now)
        {
            return Mathf.Clamp01(1f - (now - lastTime) / Window);
        }

        public float Total { get { return total; } }
        public bool IsNegative { get { return total < 0f; } }
        public bool RoundsToZero { get { return Mathf.RoundToInt(total) == 0; } }

        public string Text
        {
            get
            {
                int n = Mathf.RoundToInt(total);
                return n < 0 ? "−" + (-n).ToString() : "+" + n.ToString();
            }
        }
    }
}
}

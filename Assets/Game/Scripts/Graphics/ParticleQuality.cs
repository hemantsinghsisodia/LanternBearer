using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LanternKeeper
{
// Scales particle counts from the values captured the first time each system is seen.
// A multiplier of 1 restores that baseline and does not write the modules again once they match.
public static class ParticleQuality
{
    sealed class Tracked
    {
        public ParticleSystem system;
        public int maxParticles;
        public ParticleSystem.MinMaxCurve rateOverTime;
        public ParticleSystem.Burst[] bursts;
        public bool changed;
    }

    static readonly List<Tracked> tracked = new List<Tracked>(32);
    static ParticleSystem.Burst[] scratch;

    public static float Multiplier
    {
        get
        {
            GraphicsProfile profile = GraphicsQuality.Profile;
            if (profile == null || profile.particleCountMultiplier <= 0f)
            {
                return 1f;
            }

            return profile.particleCountMultiplier;
        }
    }

    public static float ScaleRate(float baseline)
    {
        float multiplier = Multiplier;
        if (Mathf.Approximately(multiplier, 1f))
        {
            return baseline;
        }

        return baseline * multiplier;
    }

    public static int ScaleCount(int baseline)
    {
        float multiplier = Multiplier;
        if (baseline <= 0 || Mathf.Approximately(multiplier, 1f))
        {
            return baseline;
        }

        int scaled = Mathf.RoundToInt(baseline * multiplier);
        return scaled < 1 ? 1 : scaled;
    }

    public static void ApplyTo(ParticleSystem system)
    {
        if (!Application.isPlaying || system == null)
        {
            return;
        }

        Tracked item = Track(system);
        Apply(item, Multiplier);
    }

    public static void Reapply(float multiplier)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (multiplier <= 0f)
        {
            multiplier = 1f;
        }

        for (int i = 0; i < tracked.Count; i++)
        {
            Apply(tracked[i], multiplier);
        }
    }

    public static void AppendNamed(StringBuilder text, string particleName, int maxMatches)
    {
        int found = 0;
        for (int i = 0; i < tracked.Count && found < maxMatches; i++)
        {
            ParticleSystem system = tracked[i].system;
            if (system == null || system.name != particleName)
            {
                continue;
            }

            text.Append(" | ");
            text.Append(Describe(system));
            found++;
        }
    }

    public static string Describe(ParticleSystem system)
    {
        if (system == null)
        {
            return "null";
        }

        ParticleSystem.MainModule main = system.main;
        ParticleSystem.EmissionModule emission = system.emission;
        string parent = system.transform.parent != null ? system.transform.parent.name : "-";
        string burst = "-";
        int burstCount = emission.burstCount;
        if (burstCount > 0)
        {
            EnsureScratch(burstCount);
            emission.GetBursts(scratch);
            burst = CurveText(scratch[0].count);
            if (burstCount > 1)
            {
                burst = burst + "x" + burstCount.ToString();
            }
        }

        return system.name + "@" + parent
            + " rate=" + CurveText(emission.rateOverTime)
            + " max=" + main.maxParticles.ToString()
            + " burst=" + burst;
    }

    static Tracked Track(ParticleSystem system)
    {
        for (int i = 0; i < tracked.Count; i++)
        {
            if (tracked[i].system == system)
            {
                return tracked[i];
            }
        }

        ParticleSystem.MainModule main = system.main;
        ParticleSystem.EmissionModule emission = system.emission;
        int burstCount = emission.burstCount;
        ParticleSystem.Burst[] bursts = null;
        if (burstCount > 0)
        {
            bursts = new ParticleSystem.Burst[burstCount];
            emission.GetBursts(bursts);
        }

        Tracked item = new Tracked();
        item.system = system;
        item.maxParticles = main.maxParticles;
        item.rateOverTime = emission.rateOverTime;
        item.bursts = bursts;
        item.changed = false;
        tracked.Add(item);
        return item;
    }

    static void Apply(Tracked item, float multiplier)
    {
        if (item == null || item.system == null)
        {
            return;
        }

        bool neutral = multiplier >= 0.999f && multiplier <= 1.001f;
        if (neutral)
        {
            if (!item.changed)
            {
                return;
            }

            Write(item, item.maxParticles, item.rateOverTime, item.bursts, false, 1f);
            item.changed = false;
            return;
        }

        int maxParticles = item.maxParticles;
        if (maxParticles > 0)
        {
            maxParticles = Mathf.RoundToInt(item.maxParticles * multiplier);
            if (maxParticles < 1)
            {
                maxParticles = 1;
            }
        }

        ParticleSystem.MinMaxCurve rate = ScaleCurve(item.rateOverTime, multiplier);
        Write(item, maxParticles, rate, item.bursts, true, multiplier);
        item.changed = true;
    }

    static void Write(
        Tracked item,
        int maxParticles,
        ParticleSystem.MinMaxCurve rate,
        ParticleSystem.Burst[] bursts,
        bool scaleBursts,
        float multiplier)
    {
        ParticleSystem.MainModule main = item.system.main;
        if (main.maxParticles != maxParticles)
        {
            main.maxParticles = maxParticles;
        }

        ParticleSystem.EmissionModule emission = item.system.emission;
        emission.rateOverTime = rate;
        if (bursts == null || bursts.Length == 0)
        {
            return;
        }

        EnsureScratch(bursts.Length);
        for (int i = 0; i < bursts.Length; i++)
        {
            ParticleSystem.Burst burst = bursts[i];
            if (scaleBursts)
            {
                burst.count = ScaleCurve(burst.count, multiplier);
            }

            scratch[i] = burst;
        }

        emission.SetBursts(scratch, bursts.Length);
    }

    static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve curve, float multiplier)
    {
        if (curve.mode == ParticleSystemCurveMode.TwoConstants)
        {
            curve.constantMin *= multiplier;
            curve.constantMax *= multiplier;
        }
        else if (curve.mode == ParticleSystemCurveMode.Constant)
        {
            curve.constant *= multiplier;
        }
        else
        {
            curve.curveMultiplier *= multiplier;
        }

        return curve;
    }

    static string CurveText(ParticleSystem.MinMaxCurve curve)
    {
        if (curve.mode == ParticleSystemCurveMode.TwoConstants)
        {
            return curve.constantMin.ToString("0.###") + "-" + curve.constantMax.ToString("0.###");
        }

        if (curve.mode == ParticleSystemCurveMode.Constant)
        {
            return curve.constant.ToString("0.###");
        }

        return "curve x" + curve.curveMultiplier.ToString("0.###");
    }

    static void EnsureScratch(int count)
    {
        if (scratch == null || scratch.Length < count)
        {
            scratch = new ParticleSystem.Burst[count];
        }
    }
}
}

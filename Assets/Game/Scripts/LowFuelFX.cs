using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LanternKeeper
{
public class LowFuelFX : MonoBehaviour
{
    [SerializeField] Volume volume;
    [SerializeField] Lantern lantern;

    Vignette vignette;
    ColorAdjustments color;
    bool ready;
    bool resolved;
    float edgePulse;
    static bool loggedFallback;

    void Awake()
    {
        Resolve();
    }

    void Resolve()
    {
        if (resolved)
        {
            return;
        }

        bool missing = volume == null || lantern == null;
        resolved = true;
        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

        if (missing && !loggedFallback)
        {
            loggedFallback = true;
            Debug.LogWarning("LowFuelFX references were not wired. Resolved once.", this);
        }

        Bind();
    }

    public static bool ProfileAlive(VolumeProfile profile)
    {
        if (profile.components == null || profile.components.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < profile.components.Count; i++)
        {
            if (profile.components[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    void Bind()
    {
        if (volume == null)
        {
            return;
        }

        VolumeProfile shared = volume.sharedProfile;
        if (shared == null || !ProfileAlive(shared))
        {
            return;
        }

        VolumeProfile runtime = volume.profile;
        if (runtime == null)
        {
            return;
        }

        runtime.TryGet(out vignette);
        runtime.TryGet(out color);
        ready = vignette != null;
    }

    // A Shade steal darkens the screen edge for a moment on top of the fuel vignette.
    public void PulseEdge(float strength)
    {
        edgePulse = Mathf.Max(edgePulse, Mathf.Clamp01(strength));
    }

    void Update()
    {
        if (!ready || lantern == null || vignette == null)
        {
            return;
        }

        float fuel = lantern.FuelNormalized;
        float amount = Mathf.Lerp(0.55f, 0.18f, fuel);
        if (fuel < 0.2f)
        {
            amount += Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.1f;
        }

        amount += lantern.ProximityDim * 0.22f;
        if (edgePulse > 0f)
        {
            amount += edgePulse * 0.4f;
            edgePulse = Mathf.MoveTowards(edgePulse, 0f, Time.deltaTime / 0.8f);
        }

        // Released at full fuel so the look volume's vignette shows; otherwise the extra darkening adds to it.
        float extra = Mathf.Max(0f, amount - LookMapping.FullFuelVignette);
        vignette.intensity.overrideState = extra > 0.001f;
        vignette.intensity.value = Mathf.Clamp01(LookMapping.LookVignette + extra);
        if (color != null)
        {
            // At full fuel the override is released so the look volume's grade shows through.
            float saturation = Mathf.Lerp(-45f, 0f, fuel);
            color.saturation.overrideState = Mathf.Abs(saturation) > 0.01f;
            color.saturation.value = saturation;
        }
    }
}
}

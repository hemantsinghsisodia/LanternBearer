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
    float pulsePhase;
    float mothClock;
    bool tinted;
    Color pulseColour;
    static bool loggedFallback;
    const float MothPulseStrength = 0.45f;
    static readonly Color DrainViolet = LookPalette.FromHex(LookPalette.DrainViolet);

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

    public float EdgePulseAmount => edgePulse;
    // How many moth drain pulses have been requested (tests read it).
    public int MothPulsesRequested { get; private set; }

    // A Shade steal darkens the screen edge for a moment on top of the fuel vignette.
    public void PulseEdge(float strength)
    {
        float clamped = Mathf.Clamp01(strength);
        if (clamped >= edgePulse)
        {
            tinted = false;
        }

        edgePulse = Mathf.Max(edgePulse, clamped);
    }

    // The same pulse, tinted: the vignette takes this colour until the pulse has faded.
    // A weaker tint never recolours a stronger pulse that is still decaying.
    public void PulseEdge(float strength, Color colour)
    {
        float clamped = Mathf.Clamp01(strength);
        if (clamped >= edgePulse)
        {
            tinted = true;
            pulseColour = colour;
        }

        edgePulse = Mathf.Max(edgePulse, clamped);
    }

    // While moths drain the lantern the screen edge pulses drain violet, about once a second (every other second with
    // Reduce flashing). The Update that follows applies it.
    void UpdateMothPulse()
    {
        if (lantern.MothsDraining <= 0)
        {
            mothClock = 0f;
            return;
        }

        mothClock -= Time.deltaTime;
        if (mothClock > 0f)
        {
            return;
        }

        mothClock = SettingsMath.MothPulsePeriod(UserSettings.ReduceFlashing);
        MothPulsesRequested++;
        PulseEdge(MothPulseStrength, DrainViolet);
    }

    void Update()
    {
        if (!ready || lantern == null || vignette == null)
        {
            return;
        }

        UpdateMothPulse();
        float fuel = lantern.FuelNormalized;
        float amount = Mathf.Lerp(0.55f, 0.18f, fuel);
        if (fuel < 0.2f)
        {
            // Reduce flashing halves the pulse rate. The phase is accumulated so a rate change does not jump.
            pulsePhase += Time.deltaTime * SettingsMath.LowFuelPulseRate(6f, UserSettings.ReduceFlashing);
            amount += Mathf.Abs(Mathf.Sin(pulsePhase)) * 0.1f;
        }

        amount += lantern.ProximityDim * 0.22f;
        bool tintNow = edgePulse > 0f && tinted;
        if (edgePulse > 0f)
        {
            amount += edgePulse * 0.4f;
            edgePulse = Mathf.MoveTowards(edgePulse, 0f, Time.deltaTime / 0.8f);
        }

        // The tint is overridden only while a tinted pulse is on screen; afterwards the look volume's vignette colour shows again.
        vignette.color.overrideState = tintNow;
        if (tintNow)
        {
            vignette.color.value = pulseColour;
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

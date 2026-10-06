using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LanternKeeper
{
// Turns a look volume's features on or off for the graphics preset. Low has no post-processing, Medium halves the bloom
// and only Ultra gets film grain. Writes go to the runtime clone of the profile, so the saved asset is never touched.
[RequireComponent(typeof(Volume))]
public class LookVolumeQuality : MonoBehaviour
{
    const float MediumBloomScale = 0.5f;

    Volume volume;
    Bloom bloom;
    FilmGrain grain;
    float baseBloomIntensity;
    ColorAdjustments exposure;
    float baseExposure;
    bool bound;
    bool counted;
    static int activeCount;

    // True while at least one look volume is switched on, so exposure (and with it the player's brightness) is applied.
    // Where it is false (the Low preset, or a scene with no look volume) UserSettingsApplier scales the lights instead.
    public static bool ExposureActive
    {
        get { return activeCount > 0; }
    }

    public bool VolumeOn
    {
        get { return volume != null && volume.enabled; }
    }

    public bool GrainOn
    {
        get { return grain != null && grain.active; }
    }

    void OnEnable()
    {
        GraphicsQuality.QualityChanged += HandleQualityChanged;
        UserSettings.Changed += ApplyUserExposure;
        Apply(GraphicsQuality.Profile);
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= HandleQualityChanged;
        UserSettings.Changed -= ApplyUserExposure;
        SetCounted(false);
    }

    void SetCounted(bool on)
    {
        if (on != counted)
        {
            counted = on;
            activeCount = Mathf.Max(0, activeCount + (on ? 1 : -1));
        }
    }

    // The look's authored exposure plus the player's brightness setting. Only the runtime clone is written.
    void ApplyUserExposure()
    {
        Bind();
        if (bound && exposure != null)
        {
            exposure.postExposure.value = baseExposure + UserSettingsApplier.UserEv;
        }
    }

    void HandleQualityChanged(GraphicsProfile profile)
    {
        Apply(profile);
    }

    void Bind()
    {
        if (bound)
        {
            return;
        }

        volume = GetComponent<Volume>();
        VolumeProfile shared = volume != null ? volume.sharedProfile : null;
        if (shared == null || !LowFuelFX.ProfileAlive(shared))
        {
            return;
        }

        Bloom sharedBloom;
        if (shared.TryGet(out sharedBloom))
        {
            baseBloomIntensity = sharedBloom.intensity.value;
        }

        ColorAdjustments sharedColor;
        if (shared.TryGet(out sharedColor))
        {
            baseExposure = sharedColor.postExposure.value;
        }

        VolumeProfile runtime = volume.profile;
        runtime.TryGet(out bloom);
        runtime.TryGet(out grain);
        runtime.TryGet(out exposure);
        bound = true;
    }

    public void Apply(GraphicsProfile profile)
    {
        Bind();
        if (volume == null)
        {
            return;
        }

        ApplyUserExposure();
        bool post = profile == null || profile.postProcessing != GraphicsPostProcessing.Off;
        bool reduced = profile != null && profile.postProcessing == GraphicsPostProcessing.Lighter;
        if (bound)
        {
            if (bloom != null)
            {
                bloom.intensity.value = reduced ? baseBloomIntensity * MediumBloomScale : baseBloomIntensity;
            }

            if (grain != null)
            {
                grain.active = post && GraphicsQuality.Current == GraphicsLevel.Ultra;
            }
        }

        volume.enabled = post;
        SetCounted(post && bound && exposure != null);
    }
}
}

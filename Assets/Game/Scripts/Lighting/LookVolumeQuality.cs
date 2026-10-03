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
    bool bound;

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
        Apply(GraphicsQuality.Profile);
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= HandleQualityChanged;
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

        VolumeProfile runtime = volume.profile;
        runtime.TryGet(out bloom);
        runtime.TryGet(out grain);
        bound = true;
    }

    public void Apply(GraphicsProfile profile)
    {
        Bind();
        if (volume == null)
        {
            return;
        }

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
    }
}
}

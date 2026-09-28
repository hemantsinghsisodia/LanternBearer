using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LanternKeeper
{
public class LowFuelFX : MonoBehaviour
{
    [SerializeField] Volume volume;

    Vignette vignette;
    ColorAdjustments color;
    Lantern lantern;
    bool ready;

    void Awake()
    {
        Bind();
    }

    static bool ProfileAlive(VolumeProfile profile)
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
            volume = FindAnyObjectByType<Volume>();
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

    void Update()
    {
        if (!ready)
        {
            Bind();
        }

        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

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

        vignette.intensity.Override(Mathf.Clamp01(amount));
        if (color != null)
        {
            color.saturation.Override(Mathf.Lerp(-45f, 0f, fuel));
        }
    }
}
}

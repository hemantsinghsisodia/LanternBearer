using UnityEngine;

namespace LanternKeeper
{
// Per-island runtime half of the moon rim: feeds the feature the graphics preset's rim quality and, on moonless islands,
// brightens the rim with each lightning flash.
[DefaultExecutionOrder(-50)]
public class MoonRimSettings : MonoBehaviour
{
    static readonly int BoostId = Shader.PropertyToID("_LKMoonRimBoost");

    void OnEnable()
    {
        GraphicsQuality.QualityChanged += HandleQualityChanged;
        GraphicsProfile profile = GraphicsQuality.Profile;
        if (profile != null)
        {
            MoonRimFeature.SetQuality(profile.moonRimQuality);
        }
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= HandleQualityChanged;
        Shader.SetGlobalFloat(BoostId, 0f);
    }

    void HandleQualityChanged(GraphicsProfile profile)
    {
        if (profile != null)
        {
            MoonRimFeature.SetQuality(profile.moonRimQuality);
        }
    }

    void Update()
    {
        LookApplier look = LookApplier.Current;
        bool moonless = look != null && look.Profile != null && !look.Profile.moonOn;
        Shader.SetGlobalFloat(BoostId, moonless ? Lightning.CurrentFlash : 0f);
    }
}
}

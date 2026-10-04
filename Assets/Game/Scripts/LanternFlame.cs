using UnityEngine;

namespace LanternKeeper
{
// Flame quad and glass glow of the storm lantern. Height, colour and glow follow fuel; the flame guts with the death light.
public class LanternFlame : MonoBehaviour
{
    [SerializeField] Lantern lantern;
    [SerializeField] Transform flame;
    [SerializeField] Renderer flameRenderer;
    [SerializeField] Renderer glassRenderer;
    [SerializeField] LanternFlicker flicker;
    // Authored scale of the flame quad. Stays zero until first use, then caches the transform's scale.
    [SerializeField] Vector3 baseScale;

    const float GlassAlpha = 0.35f;
    const float DeathFadeStart = 0.35f;
    static readonly Color glassColor = LookPalette.FromHex(LookPalette.GlowCore);
    static readonly Color coreFull = new Color(1f, 0.96f, 0.7f, 1f);
    static readonly int MidId = Shader.PropertyToID("_Mid");
    static readonly int CoreId = Shader.PropertyToID("_Core");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    MaterialPropertyBlock flameBlock;
    MaterialPropertyBlock glassBlock;
    Light sourceLight;

    void Awake()
    {
        if (lantern == null)
        {
            lantern = GetComponent<Lantern>();
        }

        if (flicker == null)
        {
            flicker = GetComponent<LanternFlicker>();
        }
    }

    void LateUpdate()
    {
        if (lantern == null)
        {
            return;
        }

        float death = lantern.DeathLightActive ? Mathf.Clamp01(lantern.DeathLight / DeathFadeStart) : 1f;
        Apply(lantern.FuelNormalized, death, FlickerFactor());
    }

    public void ApplyFuel(float fuel01)
    {
        Apply(fuel01, 1f, 1f);
    }

    void Apply(float fuel01, float gutter, float flickerFactor)
    {
        fuel01 = Mathf.Clamp01(fuel01);
        if (flame != null)
        {
            if (baseScale == Vector3.zero)
            {
                baseScale = flame.localScale;
            }

            Vector3 scale = baseScale;
            scale.y *= LanternFlameMapping.Height(fuel01) * gutter;
            flame.localScale = scale;
        }

        if (flameRenderer != null)
        {
            if (flameBlock == null)
            {
                flameBlock = new MaterialPropertyBlock();
            }

            Color mid = LanternFlameMapping.FlameColour(fuel01);
            flameBlock.SetColor(MidId, mid);
            flameBlock.SetColor(CoreId, Color.Lerp(mid, coreFull, fuel01));
            flameRenderer.SetPropertyBlock(flameBlock);
        }

        if (glassRenderer != null)
        {
            if (glassBlock == null)
            {
                glassBlock = new MaterialPropertyBlock();
            }

            float alpha = Mathf.Clamp01(GlassAlpha * fuel01 * flickerFactor * gutter);
            glassBlock.SetColor(BaseColorId, new Color(glassColor.r, glassColor.g, glassColor.b, alpha));
            glassRenderer.SetPropertyBlock(glassBlock);
        }
    }

    // Light intensity against its baseline, so the glass breathes with the flame light.
    float FlickerFactor()
    {
        if (flicker == null || lantern == null || lantern.DeathLightActive)
        {
            return 1f;
        }

        if (sourceLight == null)
        {
            sourceLight = lantern.GetComponentInChildren<Light>();
        }

        if (sourceLight == null || !sourceLight.enabled)
        {
            return 1f;
        }

        float baseline = Mathf.Max(0.01f, lantern.BaseIntensity * lantern.ProximityScale);
        return Mathf.Clamp(sourceLight.intensity / baseline, 0.5f, 1.3f);
    }
}
}

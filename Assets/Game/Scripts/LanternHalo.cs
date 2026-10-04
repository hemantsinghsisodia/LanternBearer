using UnityEngine;

namespace LanternKeeper
{
// Soft additive glow quad around the lantern. Faces the camera; alpha follows fuel and the flicker.
public class LanternHalo : MonoBehaviour
{
    [SerializeField] Lantern lantern;
    [SerializeField] Light sourceLight;
    [SerializeField] Renderer haloRenderer;
    // Glow core colour (#FFD38A) and quad size in metres; constants so a rebuild always applies them.
    static readonly Color color = LookPalette.FromHex(LookPalette.GlowCore);
    // Kept under the glass tint: a hotter halo behind the panes whitens them.
    const float maxAlpha = 0.36f;
    const float size = 0.78f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    MaterialPropertyBlock block;
    Camera cachedCamera;

    void Awake()
    {
        if (lantern == null)
        {
            lantern = GetComponentInParent<Lantern>();
        }

        if (sourceLight == null && lantern != null)
        {
            sourceLight = lantern.GetComponentInChildren<Light>();
        }

        if (haloRenderer == null)
        {
            haloRenderer = GetComponent<Renderer>();
        }

        block = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        if (haloRenderer == null || lantern == null)
        {
            return;
        }

        float alpha = 0f;
        if (sourceLight != null && sourceLight.enabled)
        {
            if (lantern.DeathLightActive)
            {
                alpha = Mathf.Clamp01(lantern.DeathLight / 4f);
            }
            else
            {
                float baseline = Mathf.Max(0.01f, lantern.BaseIntensity * lantern.ProximityScale);
                float flicker = Mathf.Clamp(sourceLight.intensity / baseline, 0.5f, 1.3f);
                alpha = lantern.FuelNormalized * flicker;
            }
        }

        Apply(lantern.FuelNormalized, alpha);
    }

    // Static look for a given fuel at full flicker (the close-up capture runs without a running Lantern).
    public void ApplyFuel(float fuel01)
    {
        Apply(fuel01, Mathf.Clamp01(fuel01));
    }

    void Apply(float fuel01, float alpha)
    {
        // Awake has not run when the close-up capture drives this in edit mode.
        if (haloRenderer == null)
        {
            haloRenderer = GetComponent<Renderer>();
        }

        if (haloRenderer == null)
        {
            return;
        }

        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera != null)
        {
            // Cull Off, so the quad only has to be parallel to the camera plane.
            transform.rotation = Quaternion.LookRotation(cachedCamera.transform.forward, cachedCamera.transform.up);
        }

        transform.localScale = Vector3.one * (size * LanternFlameMapping.HaloScale(fuel01));
        haloRenderer.enabled = alpha > 0.002f;
        block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha) * maxAlpha));
        haloRenderer.SetPropertyBlock(block);
    }
}
}

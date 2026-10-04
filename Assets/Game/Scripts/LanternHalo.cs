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
    const float maxAlpha = 0.7f;
    const float size = 0.9f;

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

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera != null)
        {
            // Cull Off, so the quad only has to be parallel to the camera plane.
            transform.rotation = Quaternion.LookRotation(cachedCamera.transform.forward, cachedCamera.transform.up);
        }

        transform.localScale = Vector3.one * (size * LanternFlameMapping.HaloScale(lantern.FuelNormalized));

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

        haloRenderer.enabled = alpha > 0.002f;
        block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha) * maxAlpha));
        haloRenderer.SetPropertyBlock(block);
    }
}
}

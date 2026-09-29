using UnityEngine;

namespace LanternKeeper
{
public class LanternFlicker : MonoBehaviour
{
    [SerializeField] Light targetLight;
    [SerializeField] Lantern lantern;

    void Awake()
    {
        if (targetLight == null)
        {
            targetLight = GetComponentInChildren<Light>();
        }

        if (lantern == null)
        {
            lantern = GetComponent<Lantern>();
        }
    }

    void LateUpdate()
    {
        if (targetLight == null || !targetLight.enabled || lantern == null)
        {
            return;
        }

        if (lantern.DeathLightActive)
        {
            targetLight.intensity = lantern.DeathLight;
            targetLight.enabled = lantern.DeathLight > 0.02f;
            return;
        }

        float fuel = lantern.FuelNormalized;
        float amp = Mathf.Lerp(0.05f, 0.28f, 1f - fuel);
        float speed = 6f;
        if (fuel < 0.2f)
        {
            amp = 0.5f;
            speed = 11f;
        }

        float noise = Mathf.PerlinNoise(Time.time * speed, 0.37f);
        float wobble = 1f - amp + (amp * noise * 2f);
        targetLight.intensity = Mathf.Max(0.05f, lantern.BaseIntensity * wobble * lantern.ProximityScale);
    }
}
}

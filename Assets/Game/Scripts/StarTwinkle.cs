using UnityEngine;

namespace LanternKeeper
{
public class StarTwinkle : MonoBehaviour
{
    ParticleSystem system;
    ParticleSystem.Particle[] particles;
    float fade = 1f;

    void Awake()
    {
        system = GetComponent<ParticleSystem>();
    }

    public void SetFade(float value)
    {
        fade = Mathf.Clamp01(value);
    }

    void LateUpdate()
    {
        if (system == null)
        {
            return;
        }

        int count = system.particleCount;
        if (count <= 0)
        {
            return;
        }

        if (particles == null || particles.Length < count)
        {
            particles = new ParticleSystem.Particle[Mathf.Max(count, 16)];
        }

        int alive = system.GetParticles(particles);
        for (int i = 0; i < alive; i++)
        {
            float seed = particles[i].randomSeed * 0.0001f;
            float twinkle = 0.3f + 0.7f * Mathf.PerlinNoise(seed, Time.time * 0.65f);
            byte alpha = (byte)Mathf.Clamp(twinkle * fade * 255f, 0f, 255f);
            Color32 color = particles[i].startColor;
            particles[i].startColor = new Color32(color.r, color.g, color.b, alpha);
        }

        system.SetParticles(particles, alive);
    }
}
}

using UnityEngine;

namespace LanternKeeper
{
public class LighthouseBeam : MonoBehaviour
{
    [SerializeField] Transform beam;
    [SerializeField] Light lamp;
    [SerializeField] Renderer lampGlow;
    [SerializeField] float degreesPerSecond = 14f;
    [SerializeField] float blinkPeriod = 2.6f;

    MaterialPropertyBlock block;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Update()
    {
        if (beam != null)
        {
            beam.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }

        float period = Mathf.Max(0.4f, blinkPeriod);
        float phase = Mathf.Repeat(Time.time, period) / period;
        bool on = phase < 0.16f || (phase > 0.26f && phase < 0.38f);
        if (lamp != null)
        {
            lamp.intensity = on ? 6.5f : 0.2f;
        }

        if (lampGlow == null)
        {
            return;
        }

        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        Color color = on ? new Color(3.4f, 2.5f, 1.55f, 1f) : new Color(0.35f, 0.26f, 0.14f, 1f);
        lampGlow.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        lampGlow.SetPropertyBlock(block);
    }
}
}

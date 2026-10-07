using UnityEngine;

namespace LanternKeeper
{
// Per-moth settings for the MothWing shader: a random flap phase and rate (MothFlap.MinHz..MaxHz) set through a
// MaterialPropertyBlock, so every moth beats out of step. It also publishes the Low graphics preset to the shader
// as the global float _LKMothLow (1 on Low: a plain sine flap with no glide). The flap itself runs in the shader.
public class MothVisual : MonoBehaviour
{
    public const string LowGlobalName = "_LKMothLow";

    static readonly int PhaseId = Shader.PropertyToID("_Phase");
    static readonly int FlapHzId = Shader.PropertyToID("_FlapHz");
    static readonly int LowId = Shader.PropertyToID(LowGlobalName);
    static MaterialPropertyBlock block;

    [SerializeField] Renderer wingRenderer;

    float phase;
    float flapHz = 17f;
    bool configured;

    public float Phase => phase;
    public float FlapHz => flapHz;
    public Renderer WingRenderer => wingRenderer;

    public void Configure(float newPhase, float newFlapHz)
    {
        phase = newPhase;
        flapHz = Mathf.Clamp(newFlapHz, MothFlap.MinHz, MothFlap.MaxHz);
        configured = true;
        Apply();
    }

    // Writes the Low flag to the shader global. Called whenever the preset changes.
    public static void PublishQuality()
    {
        Shader.SetGlobalFloat(LowId, GraphicsQuality.Current == GraphicsLevel.Low ? 1f : 0f);
    }

    void Awake()
    {
        if (!configured)
        {
            configured = true;
            phase = Random.value * 2f * Mathf.PI;
            flapHz = Random.Range(MothFlap.MinHz, MothFlap.MaxHz);
        }

        Apply();
    }

    void OnEnable()
    {
        GraphicsQuality.QualityChanged += OnQuality;
        PublishQuality();
        Apply();
    }

    void OnDisable()
    {
        GraphicsQuality.QualityChanged -= OnQuality;
    }

    void OnQuality(GraphicsProfile profile)
    {
        PublishQuality();
    }

    void Apply()
    {
        if (wingRenderer == null)
        {
            wingRenderer = FindWingRenderer();
        }

        if (wingRenderer == null)
        {
            return;
        }

        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        wingRenderer.GetPropertyBlock(block);
        block.SetFloat(PhaseId, phase);
        block.SetFloat(FlapHzId, flapHz);
        wingRenderer.SetPropertyBlock(block);
    }

    Renderer FindWingRenderer()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].sharedMaterial;
            if (material != null && material.shader != null && material.shader.name == "LanternKeeper/MothWing")
            {
                return renderers[i];
            }
        }

        return null;
    }
}
}

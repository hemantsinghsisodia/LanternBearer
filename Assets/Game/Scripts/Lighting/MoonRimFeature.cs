using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace LanternKeeper
{
// Always-on moon rim: pale edges and moon-facing slopes reconstructed from the camera depth texture, added to the colour
// after the skybox and before post-processing. On Low it renders at half resolution and upsamples. It is never off.
public class MoonRimFeature : ScriptableRendererFeature
{
    const string ShaderName = "Hidden/LanternKeeper/MoonRim";
    const float MinStrength = 0.0005f;

    [SerializeField] Shader shader;

    static bool qualitySet;
    static LookMapping.MoonRimQuality quality = LookMapping.MoonRimQualityFor(1);

    static readonly int StrengthId = Shader.PropertyToID("_LKMoonRimStrength");
    static readonly int TexelId = Shader.PropertyToID("_RimTexel");
    static readonly int WidthId = Shader.PropertyToID("_RimWidth");
    static readonly int SimpleId = Shader.PropertyToID("_RimSimple");
    static readonly int EdgeWeightId = Shader.PropertyToID("_RimEdgeWeight");
    static readonly int GainId = Shader.PropertyToID("_RimGain");

    Material material;
    MoonRimPass pass;

    // Called by MoonRimSettings when the graphics preset changes.
    public static void SetQuality(int moonRimQuality)
    {
        quality = LookMapping.MoonRimQualityFor(moonRimQuality);
        qualitySet = true;
    }

    public override void Create()
    {
        if (shader == null)
        {
            shader = Shader.Find(ShaderName);
        }

        material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
        pass = new MoonRimPass();
        // URP copies the depth texture at AfterRenderingSkybox, so the rim runs just after it.
        pass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 1;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        CameraType type = renderingData.cameraData.cameraType;
        if (material == null || (type != CameraType.Game && type != CameraType.SceneView))
        {
            return;
        }

        // Island looks reset the strength to 0 when they go away (menu) and DawnSequence fades it out, so no look means no rim.
        if (Shader.GetGlobalFloat(StrengthId) < MinStrength)
        {
            return;
        }

        if (!qualitySet)
        {
            GraphicsProfile profile = GraphicsQuality.Profile;
            if (profile != null)
            {
                quality = LookMapping.MoonRimQualityFor(profile.moonRimQuality);
            }
        }

        pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        pass.Setup(material, quality);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
    }

    class MoonRimPass : ScriptableRenderPass
    {
        class PassData
        {
            public Material material;
            public TextureHandle source;
            public int passIndex;
            public Vector4 texel;
            public float width;
            public float simple;
            public float gain;
        }

        Material material;
        LookMapping.MoonRimQuality settings;

        public MoonRimPass()
        {
            profilingSampler = new ProfilingSampler("Moon Rim");
        }

        public void Setup(Material rimMaterial, LookMapping.MoonRimQuality rimQuality)
        {
            material = rimMaterial;
            settings = rimQuality;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            UniversalCameraData camera = frameData.Get<UniversalCameraData>();
            if (!resources.cameraDepthTexture.IsValid())
            {
                return;
            }

            Vector4 texel = new Vector4(1f / camera.cameraTargetDescriptor.width, 1f / camera.cameraTargetDescriptor.height, 0f, 0f);
            float simple = settings.simpleEdge ? 1f : 0f;
            float width = settings.simpleEdge ? 2f : 1.2f;
            float gain = settings.gain;

            if (settings.resolutionScale >= 0.999f)
            {
                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("Moon Rim", out PassData data, profilingSampler))
                {
                    data.material = material;
                    data.passIndex = 0;
                    data.source = resources.cameraDepthTexture;
                    data.texel = texel;
                    data.width = width;
                    data.simple = simple;
                    data.gain = gain;
                    builder.UseTexture(resources.cameraDepthTexture);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (PassData d, RasterGraphContext context) => DrawRim(d, context));
                }

                return;
            }

            RenderTextureDescriptor descriptor = camera.cameraTargetDescriptor;
            descriptor.width = Mathf.Max(1, Mathf.RoundToInt(descriptor.width * settings.resolutionScale));
            descriptor.height = Mathf.Max(1, Mathf.RoundToInt(descriptor.height * settings.resolutionScale));
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32;
            TextureHandle half = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_MoonRimHalf", false);

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("Moon Rim (half res)", out PassData data, profilingSampler))
            {
                data.material = material;
                data.passIndex = 1;
                data.source = resources.cameraDepthTexture;
                data.texel = texel;
                data.width = width;
                data.simple = simple;
                data.gain = gain;
                builder.UseTexture(resources.cameraDepthTexture);
                builder.SetRenderAttachment(half, 0);
                builder.SetRenderFunc(static (PassData d, RasterGraphContext context) => DrawRim(d, context));
            }

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("Moon Rim (upsample)", out PassData data, profilingSampler))
            {
                data.material = material;
                data.source = half;
                data.passIndex = 2;
                builder.UseTexture(half);
                builder.SetRenderAttachment(resources.activeColorTexture, 0);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, d.passIndex);
                });
            }
        }

        static void DrawRim(PassData data, RasterGraphContext context)
        {
            data.material.SetVector(TexelId, data.texel);
            data.material.SetFloat(WidthId, data.width);
            data.material.SetFloat(SimpleId, data.simple);
            data.material.SetFloat(EdgeWeightId, 0.2f);
            data.material.SetFloat(GainId, data.gain);
            // Blitter sets _BlitScaleBias, which the fullscreen vertex shader needs; the depth source itself is read via _CameraDepthTexture.
            Blitter.BlitTexture(context.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, data.passIndex);
        }
    }
}
}

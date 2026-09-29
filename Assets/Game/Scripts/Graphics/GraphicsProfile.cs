using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LanternKeeper
{
public enum GraphicsLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Ultra = 3
}

public enum GraphicsPostProcessing
{
    Off = 0,
    Lighter = 1,
    Full = 2
}

// One preset. Later appliers read these fields. Medium matches today's look.
public class GraphicsProfile : ScriptableObject
{
    public GraphicsLevel level;

    [Header("Rendering")]
    public float renderScale;
    // URP MsaaQuality stored value: 1 = Disabled, 2 = 2x, 4 = 4x, 8 = 8x.
    public int msaaSampleCount;
    public bool supportsHdr;
    public AntialiasingMode cameraAntialiasing;
    public GraphicsPostProcessing postProcessing;

    [Header("Shadows and lights")]
    public float shadowDistance;
    public int shadowCascadeCount;
    public int mainLightShadowmapResolution;
    public bool supportsSoftShadows;
    // URP SoftShadowQuality: 0 UsePipelineSettings, 1 Low, 2 Medium, 3 High.
    public int softShadowQuality;
    public int additionalLightsPerObject;
    // URP asset flag. Medium stays on, matching today's PC_RPAsset.
    public bool additionalLightShadowsSupported;
    // Lantern and beacon shadows. Applied later by LightQuality. Off on Low and Medium.
    public bool pointLightShadows;
    // 0 means no cap. Low keeps only the nearest few moth and firefly glow lights.
    public int glowLightCap;

    [Header("Terrain, grass and trees")]
    public float grassDrawDistance;
    public float grassDensity;
    public float terrainPixelError;
    public float terrainBasemapDistance;
    public float treeLodBias;
    public bool grassBending;

    [Header("QualitySettings")]
    // Today's Standalone (PC) lodBias is 2, so these are 1 / 2 / 3 / 4, not 0.7 / 1 / 1.5 / 2.
    public float lodBias;

    [Header("Water")]
    public bool waterRipples;
    public bool waterShoreFoam;
    public bool waterDepthTexture;
    // Today's water material _Glitter is 0.55.
    public float waterGlint;
    public bool waterDetailLayer;
    // 1 matches today's ripple range. High and Ultra widen the near field.
    public float waterRippleRange;
    // Today's _FoamDepth is 1.6.
    public float waterFoamReach;
    public bool waterReflectionProbe;

    [Header("Keeper")]
    public bool keeperRim;
    public bool chestFillLight;
    public bool keeperSelfShadow;
    public bool keeperAlwaysAnimate;
    public bool lanternSoftShadowOnKeeper;

    [Header("Horizon")]
    public bool distantMountains;
    public int hazeLayers;
    public bool extraRidges;
    public bool finerHaze;
    // Today's gameplay cameras use a far clip of 900.
    public float cameraFarPlane;

    [Header("Particles")]
    public float particleCountMultiplier;
}
}

Shader "Hidden/LanternKeeper/MoonRim"
{
    // Moon rim reconstructed from the camera depth texture: a soft pale edge where a surface meets farther depth,
    // plus a gentle lift on slopes that face the moon. The direct and upsample passes test stencil bit 8 (written by KeeperLit, BeaconGlass, BeaconIron and Shade), so those are excluded on both Ultra and Low. Pass 0 composites straight onto the camera colour; passes 1 and 2
    // render the same rim to a half-resolution target and upsample it (Low preset).
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        ENDHLSL

        Pass
        {
            Name "MoonRimDirect"
            Blend One One
            // Skips pixels that wrote stencil bit 8 (URP user bit; 32 is Deferred MaterialLit). Writers of bit 8: KeeperLit (the keeper and its lantern iron), BeaconGlass, BeaconIron and Shade; each has its own rim.
            Stencil
            {
                Ref 8
                ReadMask 8
                Comp NotEqual
                Pass Keep
            }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragRim
            #include "MoonRimCommon.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MoonRimHalfRes"
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragRim
            #include "MoonRimCommon.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MoonRimUpsample"
            Blend One One
            // Skips pixels that wrote stencil bit 8 (URP user bit; 32 is Deferred MaterialLit). Writers of bit 8: KeeperLit (the keeper and its lantern iron), BeaconGlass, BeaconIron and Shade; each has its own rim.
            Stencil
            {
                Ref 8
                ReadMask 8
                Comp NotEqual
                Pass Keep
            }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpsample
            #include "MoonRimCommon.hlsl"
            ENDHLSL
        }
    }
}

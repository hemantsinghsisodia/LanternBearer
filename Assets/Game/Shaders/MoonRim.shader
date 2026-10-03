Shader "Hidden/LanternKeeper/MoonRim"
{
    // Moon rim reconstructed from the camera depth texture: a soft pale edge where a surface meets farther depth,
    // plus a gentle lift on slopes that face the moon. Pass 0 composites straight onto the camera colour; passes 1 and 2
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
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpsample
            #include "MoonRimCommon.hlsl"
            ENDHLSL
        }
    }
}

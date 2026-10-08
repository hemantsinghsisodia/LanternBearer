#ifndef LK_CREATURE_COMMON
#define LK_CREATURE_COMMON

// Shared by the forward lit creature shaders (MothWing, BeaconIron). Include after Lighting.hlsl.

// The InputData the clustered additional-light loop macros read.
InputData CreatureInputData(float3 positionWS, float3 normalWS, float3 viewDirectionWS, float4 positionCS)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirectionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    return inputData;
}

#endif

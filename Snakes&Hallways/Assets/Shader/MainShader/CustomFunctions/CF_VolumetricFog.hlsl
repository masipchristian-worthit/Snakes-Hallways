#ifndef CF_VOLUMETRIC_FOG_INCLUDED
#define CF_VOLUMETRIC_FOG_INCLUDED

// Distance fog with light dispersal:
//  - NEAR the camera (d < StartDistance): no fog (clean view).
//  - FAR (d > FullDistance): full fog.
//  - Bright (lit) pixels burn through fog so torches open clearings.
// Height bias makes the fog hug the floor.

void VolumetricFog_float(
    UnityTexture2D NoiseTex,
    UnitySamplerState SS,
    float3 WorldPos,
    float3 LitColor,           // pass in the already-lit color so luminance drives dispersal
    float3 FogColor,
    float  StartDistance,
    float  FullDistance,
    float  HeightBias,
    float  Strength,
    float  LightDispersal,
    out float  FogFactor,
    out float3 FogColorOut)
{
    float d = distance(WorldPos, _WorldSpaceCameraPos);

    float distFactor = saturate((d - StartDistance) /
                                max(FullDistance - StartDistance, 1e-4));
    distFactor = distFactor * distFactor;

    float heightF = saturate(1.0 - WorldPos.y * HeightBias);

    float2 nuv = WorldPos.xz * 0.1 + _Time.y * 0.02;
    float  n   = SAMPLE_TEXTURE2D(NoiseTex, SS, nuv).r * 0.3 + 0.7;

    float luma       = dot(LitColor, float3(0.299, 0.587, 0.114));
    float dispersion = saturate(luma * LightDispersal);

    FogFactor   = saturate(distFactor * heightF * n * Strength * (1.0 - dispersion));
    FogColorOut = FogColor;
}

#endif

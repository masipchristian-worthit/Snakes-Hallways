#ifndef MAINSHADER_INCLUDES_INCLUDED
#define MAINSHADER_INCLUDES_INCLUDED

// =====================================================================
// MainShader - shared HLSL library
// Medieval / dirty / oppressive horror environment shader for URP 17.2+
// =====================================================================

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float  _NormalStrength;
    float  _Metallic;
    float  _Smoothness;
    float  _OcclusionStrength;
    float4 _EmissionColor;
    float  _TriplanarScale;

    float  _EdgeIntensity;
    float  _EdgeWidth;
    float  _BlendSoftness;
    float  _SeamThreshold;
    float  _ModularSeamInfluence;
    float  _EdgeVisibilityDistance;
    float  _EdgeFadeRange;

    float  _SeamDarkenAmount;
    float4 _SeamDarkenColor;
    float  _SeamDissolveAmount;

    float  _NoiseScale;
    float  _NoiseContrast;
    float  _NoiseBreakup;

    float4 _GrimeColor;
    float  _GrimeIntensity;
    float  _GrimeDirectionality;
    float  _WallGrimeBoost;
    float  _FloorGrimeBoost;
    float  _GrimeScale;

    float4 _ShadowTint;
    float  _ShadowIntensity;
    float  _Saturation;
    float  _Contrast;
    float  _AmbientDarkness;
    float  _AmbientLift;

    float  _FresnelStrength;
    float  _FresnelFalloff;
    float4 _FresnelColor;

    float4 _FogColor;
    float  _FogStartDistance;
    float  _FogFullDistance;
    float  _FogHeightBias;
    float  _FogStrength;
    float  _FogLightDispersal;
CBUFFER_END

TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
TEXTURE2D(_NormalMap);          SAMPLER(sampler_NormalMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_EmissionMap);        SAMPLER(sampler_EmissionMap);
TEXTURE2D(_NoiseTexture);       SAMPLER(sampler_NoiseTexture);
TEXTURE2D(_GrimeMap);           SAMPLER(sampler_GrimeMap);

// ---------- Helpers ----------

float Luminance01(float3 rgb)
{
    return dot(rgb, float3(0.299, 0.587, 0.114));
}

// Triplanar albedo
float4 SampleTriplanar(TEXTURE2D_PARAM(tex, ss), float3 wpos, float3 wnrm, float scale, float sharpness)
{
    float3 p = wpos * scale;
    float3 n = pow(abs(wnrm), sharpness);
    n /= max(dot(n, 1.0.xxx), 1e-5);
    float4 x = SAMPLE_TEXTURE2D(tex, ss, p.yz);
    float4 y = SAMPLE_TEXTURE2D(tex, ss, p.xz);
    float4 z = SAMPLE_TEXTURE2D(tex, ss, p.xy);
    return x * n.x + y * n.y + z * n.z;
}

// Triplanar normal (whiteout blend)
float3 SampleTriplanarNormal(TEXTURE2D_PARAM(tex, ss), float3 wpos, float3 wnrm, float scale, float sharpness)
{
    float3 p = wpos * scale;
    float3 n = pow(abs(wnrm), sharpness);
    n /= max(dot(n, 1.0.xxx), 1e-5);

    float3 nx = UnpackNormal(SAMPLE_TEXTURE2D(tex, ss, p.yz));
    float3 ny = UnpackNormal(SAMPLE_TEXTURE2D(tex, ss, p.xz));
    float3 nz = UnpackNormal(SAMPLE_TEXTURE2D(tex, ss, p.xy));

    nx = float3(nx.xy + wnrm.zy, abs(nx.z) * wnrm.x);
    ny = float3(ny.xy + wnrm.xz, abs(ny.z) * wnrm.y);
    nz = float3(nz.xy + wnrm.xy, abs(nz.z) * wnrm.z);

    return normalize(nx.zyx * n.x + ny.xzy * n.y + nz.xyz * n.z);
}

// Curvature seam (fwidth of GEOMETRIC world normal). Coplanar pieces => 0.
void ComputeSeamMasks(float3 worldNormalGeom, out float seamMask, out float seamSoft)
{
    float raw    = length(fwidth(worldNormalGeom));
    float scaled = saturate(raw * _EdgeIntensity);
    float shaped = pow(scaled, _EdgeWidth);
    seamMask = smoothstep(0.0, max(_BlendSoftness, 1e-4), shaped);
    seamSoft = smoothstep(0.0, 1.2,                      shaped);
}

void ComputeFloorWallMask(float3 wn, out float floorM, out float wallM, out float ceilM)
{
    float ny = wn.y;
    floorM = smoothstep( 0.70,  0.95,  ny);
    ceilM  = smoothstep(-0.95, -0.70, -ny);
    wallM  = saturate(1.0 - floorM - ceilM);
}

float ComputeDistanceFade(float3 wpos)
{
    float d = distance(wpos, _WorldSpaceCameraPos);
    return 1.0 - smoothstep(_EdgeVisibilityDistance, _EdgeVisibilityDistance + max(_EdgeFadeRange, 1e-4), d);
}

float StylizedNoiseTriplanar(float3 wpos, float3 wn)
{
    float3 p = wpos * _NoiseScale;
    float3 n = abs(wn);
    n /= max(dot(n, 1.0.xxx), 1e-5);
    float nx = SAMPLE_TEXTURE2D(_NoiseTexture, sampler_NoiseTexture, p.yz).r;
    float ny = SAMPLE_TEXTURE2D(_NoiseTexture, sampler_NoiseTexture, p.xz).r;
    float nz = SAMPLE_TEXTURE2D(_NoiseTexture, sampler_NoiseTexture, p.xy).r;
    float v  = nx * n.x + ny * n.y + nz * n.z;
    return saturate((v - 0.5) * _NoiseContrast + 0.5);
}

float3 ApplyGrime(float3 albedo, float3 wpos, float3 wn, float seamSoft, float floorM, float wallM)
{
    float g = SampleTriplanar(TEXTURE2D_ARGS(_GrimeMap, sampler_GrimeMap), wpos, wn, _GrimeScale, 4.0).r;
    // Threshold the grime sample so only the upper portion of the texture
    // reads as dirt -> no muddy noise across clean surfaces.
    g = saturate((g - 0.45) / 0.55);
    g = g * g;
    float gravity  = saturate(dot(wn, float3(0, -1, 0)) * 0.5 + 0.5);
    float dirW     = lerp(1.0, gravity, _GrimeDirectionality);
    float surfaceW = floorM * _FloorGrimeBoost + wallM * _WallGrimeBoost;
    // Grime concentrates strongly at seams, almost nothing on flat surfaces.
    float seamC    = seamSoft * 0.85 + 0.08;
    float amount   = saturate(g * _GrimeIntensity * surfaceW * dirW * seamC);
    return lerp(albedo, _GrimeColor.rgb, amount);
}

float3 ApplyColorGrade(float3 rgb)
{
    float luma       = Luminance01(rgb);
    // Only deep shadows get tinted, mid-tones keep their detail.
    float shadowMask = 1.0 - smoothstep(0.05, 0.35, luma);
    float3 tinted    = lerp(rgb, _ShadowTint.rgb, shadowMask * _ShadowIntensity);
    tinted = lerp(luma.xxx, tinted, _Saturation);
    tinted = (tinted - 0.5) * _Contrast + 0.5;
    tinted *= (1.0 - _AmbientDarkness);
    // Lift deep blacks so unlit areas are still readable (gamma-style).
    tinted = tinted + _AmbientLift * (1.0 - tinted);
    return max(tinted, 0.0);
}

// Volumetric fog: NEAR camera = clean, FAR from camera = foggy.
// Bright (lit) pixels burn through fog so torches dispel it locally.
void ApplyVolumetricFog(inout float3 rgb, float3 wpos)
{
    // 1. Distance: 0 at camera, ramps from _FogStartDistance to _FogFullDistance.
    float d         = distance(wpos, _WorldSpaceCameraPos);
    float distFactor = saturate((d - _FogStartDistance) /
                                max(_FogFullDistance - _FogStartDistance, 1e-4));
    distFactor = distFactor * distFactor;            // softer ramp near, fuller far

    // 2. Height bias (more fog low to the ground).
    float heightF = saturate(1.0 - wpos.y * _FogHeightBias);

    // 3. Very subtle temporal noise (8% variation max - was 30% which caused
    //    visible cells/arcs on floors). Larger scale -> softer patches.
    float2 nuv = wpos.xz * 0.03 + _Time.y * 0.015;
    float  n   = SAMPLE_TEXTURE2D(_NoiseTexture, sampler_NoiseTexture, nuv).r * 0.08 + 0.92;

    // 4. Light dispersal: bright surfaces (lit by torches, sun) clear fog.
    float lit          = Luminance01(rgb);
    float dispersion   = saturate(lit * _FogLightDispersal);

    float fog = saturate(distFactor * heightF * n * _FogStrength * (1.0 - dispersion));
    rgb = lerp(rgb, _FogColor.rgb, fog);
}

#endif

#ifndef CF_COLOR_GRADE_INCLUDED
#define CF_COLOR_GRADE_INCLUDED

// Sepia color grade with deep-black lift.
//  - Shadow tint affects ONLY very dark pixels (luma < 0.35) so mid-tones
//    keep their texture detail and the scene remains readable.
//  - Ambient Lift raises deep blacks so unlit areas are not pure black.

void ColorGrade_float(
    float3 Albedo,
    float3 ShadowTint,
    float  ShadowIntensity,
    float  Saturation,
    float  Contrast,
    float  AmbientDarkness,
    float  AmbientLift,
    out float3 Result)
{
    float luma = dot(Albedo, float3(0.299, 0.587, 0.114));

    float shadowMask = 1.0 - smoothstep(0.05, 0.35, luma);
    float3 tinted    = lerp(Albedo, ShadowTint, shadowMask * ShadowIntensity);

    tinted = lerp(luma.xxx, tinted, Saturation);
    tinted = (tinted - 0.5) * Contrast + 0.5;
    tinted *= (1.0 - AmbientDarkness);
    tinted = tinted + AmbientLift * (1.0 - tinted);

    Result = max(tinted, 0.0);
}

#endif

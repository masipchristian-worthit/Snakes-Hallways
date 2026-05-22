#ifndef CF_GRIME_ACCUMULATION_INCLUDED
#define CF_GRIME_ACCUMULATION_INCLUDED

// Layered grime. Concentrates near seams (SeamSoft), accumulates on floors,
// drips down walls (gravity-based directionality). The grime sample itself
// is triplanar so it doesn't stretch on rotated modules.

void GrimeAccumulation_float(
    float4  Albedo,
    UnityTexture2D GrimeMap,
    UnitySamplerState SS,
    float3  GrimeColor,
    float   Intensity,
    float   Directionality,
    float   FloorMask,
    float   WallMask,
    float   FloorBoost,
    float   WallBoost,
    float   SeamSoft,
    float3  WorldPos,
    float3  WorldNormal,
    float   Scale,
    out float4 Result)
{
    float3 p = WorldPos * Scale;
    float3 n = abs(WorldNormal);
    n /= max(dot(n, 1.0.xxx), 1e-5);

    float gx = SAMPLE_TEXTURE2D(GrimeMap, SS, p.yz).r;
    float gy = SAMPLE_TEXTURE2D(GrimeMap, SS, p.xz).r;
    float gz = SAMPLE_TEXTURE2D(GrimeMap, SS, p.xy).r;
    float g  = gx * n.x + gy * n.y + gz * n.z;

    // Gravity-driven directionality: dot(WorldNormal, down) is high where the
    // surface faces upward (floors get more) -> blended with a baseline so
    // walls still receive some grime.
    float gravity = saturate(dot(WorldNormal, float3(0, -1, 0)) * 0.5 + 0.5);
    float dirW    = lerp(1.0, gravity, Directionality);

    float surfaceW       = FloorMask * FloorBoost + WallMask * WallBoost;
    float seamConcentr   = SeamSoft * 0.7 + 0.3;

    float amount = saturate(g * Intensity * surfaceW * dirW * seamConcentr);

    Result      = Albedo;
    Result.rgb  = lerp(Albedo.rgb, GrimeColor, amount);
}

#endif

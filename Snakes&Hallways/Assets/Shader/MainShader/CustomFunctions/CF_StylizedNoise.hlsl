#ifndef CF_STYLIZED_NOISE_INCLUDED
#define CF_STYLIZED_NOISE_INCLUDED

// World-space triplanar noise sampling. Keeps the noise stable when the
// camera or the object moves, and avoids stretching on rotated modules.
// Contrast is biased around 0.5 so 1.0 = pass-through.

void StylizedNoise_float(
    UnityTexture2D NoiseTex,
    UnitySamplerState SS,
    float3 WorldPos,
    float3 WorldNormal,
    float Scale,
    float Contrast,
    out float Result)
{
    float3 p = WorldPos * Scale;
    float3 n = abs(WorldNormal);
    n /= max(dot(n, 1.0.xxx), 1e-5);

    float nx = SAMPLE_TEXTURE2D(NoiseTex, SS, p.yz).r;
    float ny = SAMPLE_TEXTURE2D(NoiseTex, SS, p.xz).r;
    float nz = SAMPLE_TEXTURE2D(NoiseTex, SS, p.xy).r;

    float v = nx * n.x + ny * n.y + nz * n.z;
    v = saturate((v - 0.5) * Contrast + 0.5);
    Result = v;
}

#endif

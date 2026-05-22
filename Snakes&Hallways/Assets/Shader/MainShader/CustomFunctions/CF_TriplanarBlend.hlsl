#ifndef CF_TRIPLANAR_BLEND_INCLUDED
#define CF_TRIPLANAR_BLEND_INCLUDED

// Triplanar albedo sampling. Projects the texture along three world axes
// and blends by the world normal. Eliminates UV stretching on rotated /
// non-uniformly scaled modular pieces.
// Cost: 3 texture samples.

void TriplanarAlbedo_float(
    UnityTexture2D Tex,
    UnitySamplerState SS,
    float3 WorldPos,
    float3 WorldNormal,
    float Scale,
    float BlendSharpness,
    out float4 Result)
{
    float3 p = WorldPos * Scale;
    float3 n = abs(WorldNormal);
    n = pow(n, BlendSharpness);
    n /= max(dot(n, 1.0.xxx), 1e-5);

    float4 x = SAMPLE_TEXTURE2D(Tex, SS, p.yz);
    float4 y = SAMPLE_TEXTURE2D(Tex, SS, p.xz);
    float4 z = SAMPLE_TEXTURE2D(Tex, SS, p.xy);

    Result = x * n.x + y * n.y + z * n.z;
}

// Triplanar normal sampling using Reoriented Normal Mapping (UDN simplification).
// Returns a tangent-space-like normal that can feed Normal Strength node.
void TriplanarNormal_float(
    UnityTexture2D Tex,
    UnitySamplerState SS,
    float3 WorldPos,
    float3 WorldNormal,
    float Scale,
    float BlendSharpness,
    out float3 Result)
{
    float3 p = WorldPos * Scale;
    float3 n = abs(WorldNormal);
    n = pow(n, BlendSharpness);
    n /= max(dot(n, 1.0.xxx), 1e-5);

    float3 nx = UnpackNormal(SAMPLE_TEXTURE2D(Tex, SS, p.yz));
    float3 ny = UnpackNormal(SAMPLE_TEXTURE2D(Tex, SS, p.xz));
    float3 nz = UnpackNormal(SAMPLE_TEXTURE2D(Tex, SS, p.xy));

    // Whiteout blend
    nx = float3(nx.xy + WorldNormal.zy, abs(nx.z) * WorldNormal.x);
    ny = float3(ny.xy + WorldNormal.xz, abs(ny.z) * WorldNormal.y);
    nz = float3(nz.xy + WorldNormal.xy, abs(nz.z) * WorldNormal.z);

    Result = normalize(nx.zyx * n.x + ny.xzy * n.y + nz.xyz * n.z);
}

#endif

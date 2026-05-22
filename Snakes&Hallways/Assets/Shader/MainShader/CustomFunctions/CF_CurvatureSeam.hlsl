#ifndef CF_CURVATURE_SEAM_INCLUDED
#define CF_CURVATURE_SEAM_INCLUDED

// Produces two seam masks from world-normal derivatives:
//  - SeamMask:     tight, used for darkening the join
//  - SeamMaskSoft: wide halo, used for grime accumulation around the join
//
// Both are 0 on coplanar surfaces (no false seams between same-plane modules).

void CurvatureSeam_float(
    float3 WorldNormal,
    float EdgeIntensity,
    float EdgeWidth,
    float BlendSoftness,
    out float SeamMask,
    out float SeamMaskSoft)
{
    float raw = length(fwidth(WorldNormal));
    float scaled = saturate(raw * EdgeIntensity);
    float shaped = pow(scaled, EdgeWidth);

    SeamMask     = smoothstep(0.0, max(BlendSoftness, 1e-4), shaped);
    SeamMaskSoft = smoothstep(0.0, 1.2,                      shaped);
}

#endif

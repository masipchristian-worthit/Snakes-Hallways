#ifndef CF_FWIDTH_NORMAL_INCLUDED
#define CF_FWIDTH_NORMAL_INCLUDED

// Detects geometric edges via per-pixel derivatives of the WORLD normal.
// Two coplanar quads (e.g. two 2x2 walls side by side) have identical
// world normals at the seam => fwidth ~ 0 => no mask. Only real rotation
// changes (corners, floor/wall transitions, rotated modules) trigger the mask.
//
// IMPORTANT: pass the GEOMETRIC interpolated world normal here.
// Do NOT pass the perturbed (normal-mapped) normal, or texture detail
// will produce false seams on flat continuous walls.

void FwidthNormal_float(float3 WorldNormal, out float Result)
{
    float3 dN = fwidth(WorldNormal);
    Result = length(dN);
}

void FwidthNormal_half(half3 WorldNormal, out half Result)
{
    half3 dN = fwidth(WorldNormal);
    Result = length(dN);
}

#endif

#ifndef CF_FLOOR_WALL_MASK_INCLUDED
#define CF_FLOOR_WALL_MASK_INCLUDED

// Per-pixel classification of the surface as Floor / Wall / Ceiling
// based on the world normal's vertical component.
// Sum of the three outputs is approximately 1.

void FloorWallMask_float(
    float3 WorldNormal,
    out float FloorMask,
    out float WallMask,
    out float CeilingMask)
{
    float ny = WorldNormal.y;
    FloorMask   = smoothstep( 0.70,  0.95,  ny);
    CeilingMask = smoothstep(-0.95, -0.70, -ny);
    WallMask    = saturate(1.0 - FloorMask - CeilingMask);
}

#endif

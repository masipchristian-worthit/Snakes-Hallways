#ifndef CF_DISTANCE_FADE_INCLUDED
#define CF_DISTANCE_FADE_INCLUDED

// 1.0 inside [0, VisibilityDistance], smoothly fades to 0 over FadeRange.
// Used to keep seam/grime effects in the close-up first-person window
// (0-3m strong, ~8-11m gone) so distant geometry stays clean.

void DistanceFade_float(
    float3 WorldPos,
    float  VisibilityDistance,
    float  FadeRange,
    out float Weight)
{
    float d = distance(WorldPos, _WorldSpaceCameraPos);
    Weight = 1.0 - smoothstep(VisibilityDistance, VisibilityDistance + max(FadeRange, 1e-4), d);
}

#endif

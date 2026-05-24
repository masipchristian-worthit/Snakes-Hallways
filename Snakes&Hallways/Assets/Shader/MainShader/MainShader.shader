Shader "Custom/MainShader"
{
    // =================================================================
    // MainShader v2.0 - minimal URP/Lit-compatible shader with optional
    // modular seam darkening at geometric edges (fwidth of world normal).
    // Unity 6 / URP 17.2+ / Forward+
    //
    // Default behavior == URP/Lit. Seam darkening is opt-in via toggle.
    // No color grade, no volumetric fog, no grime, no fresnel.
    // =================================================================
    Properties
    {
        [MainTexture] _BaseMap          ("Base Map",          2D)         = "white" {}
        [MainColor]   _BaseColor        ("Base Color",        Color)      = (1,1,1,1)
                      _NormalMap        ("Normal Map",        2D)         = "bump"  {}
                      _NormalStrength   ("Normal Strength",   Range(0,2)) = 1.0

        [Header(Metallic and Smoothness)]
                      _MetallicGlossMap ("Metallic Map (R metallic A smooth)", 2D) = "white" {}
                      _Metallic         ("Metallic",          Range(0,1)) = 0.0
                      _Smoothness       ("Smoothness",        Range(0,1)) = 0.4

        [Header(Occlusion and Curvature)]
                      _OcclusionMap     ("Ambient Occlusion Map (G)", 2D) = "white" {}
                      _OcclusionStrength("Occlusion Strength",Range(0,1)) = 1.0
                      _CurvatureMap     ("Curvature Map (R)", 2D)         = "white" {}
                      _CurvatureStrength("Curvature Strength",Range(0,1)) = 0.0

        [Header(Mask Map  R metal G AO B detail A smooth)]
                      _MaskMap          ("Mask Map",          2D)         = "white" {}
                      _MaskMapStrength  ("Mask Map Influence",Range(0,1)) = 0.0

        [Header(Emission)]
        [HDR]         _EmissionColor    ("Emission Color",    Color)      = (0,0,0,1)
        [NoScaleOffset] _EmissionMap    ("Emission Map",      2D)         = "white" {}

        [Header(Stylized Triplanar AO  neutral darkening)]
                      _StylizedAOMap      ("AO Overlay Map",          2D)           = "black" {}
                      _StylizedAOStrength ("AO Overlay Strength",     Range(0,1))   = 0.0
                      _StylizedAOContrast ("AO Overlay Contrast",     Range(0.1,3)) = 1.20
                      _StylizedAOScale    ("AO Overlay Scale world",  Float)        = 0.35

        [Header(Screen Space Fake AO)]
                      _FakeAOStrength          ("Fake AO Strength",            Range(0,3))   = 1.0
                      _FakeAONormalSensitivity ("Normal Edge Sensitivity",     Range(0,10))  = 2.5
                      _FakeAODepthSensitivity  ("Depth Edge Sensitivity",      Range(0,10))  = 1.0
                      _FakeAOContrast          ("Fake AO Contrast",            Range(0.1,4)) = 1.5
                      _FakeAOTint              ("Fake AO Tint",                Color)        = (0,0,0,1)

        [Header(Three Level Distance System)]
                      _Level1Distance          ("Level 1 End Base m",          Float)        = 3.0
                      _Level2Distance          ("Level 2 End Intermediate m",  Float)        = 8.0
                      _LevelBlend              ("Level Transition Blend m",    Range(0.1,5)) = 1.5

        [Header(AO Per Level Multipliers)]
                      _AOLevel1Mul             ("AO Base 0 to L1",             Range(0,3))   = 0.6
                      _AOLevel2Mul             ("AO Intermediate L1 to L2",    Range(0,5))   = 1.8
                      _AOLevel3Mul             ("AO Total L2 plus",            Range(0,8))   = 4.0

        [Header(Dither Per Level Multipliers)]
                      _DitherLevel1Mul         ("Dither Base",                 Range(0,3))   = 1.0
                      _DitherLevel2Mul         ("Dither Intermediate",         Range(0,3))   = 1.7
                      _DitherLevel3Mul         ("Dither Total",                Range(0,4))   = 2.6

        [Header(Shader Fog Level 3)]
                      _Level3FogStrength       ("Fog Strength at Total",       Range(0,1))   = 0.85
                      _Level3FogColor          ("Fog Color",                   Color)        = (0.02,0.02,0.02,1)
                      _Level3FogStart          ("Fog Start Offset m",          Float)        = 0.0

        [Header(Shadow Accumulation)]
                      _ShadowAOBoost           ("Shadow AO Boost",             Range(1,5))   = 1.5
                      _ShadowAOThreshold       ("Shadow Luma Threshold",       Range(0,1))   = 0.35

        [Header(Dither and PSX Palette)]
                      _DitherStrength          ("AO Dither Strength",          Range(0,1))   = 0.15
                      _DitherScale             ("Dither Pixel Scale",          Range(1,8))   = 1.0
                      _HighlightDither         ("Highlight Granulate",         Range(0,1))   = 0.15
                      _HighlightThreshold      ("Highlight Luma Threshold",    Range(0,2))   = 0.85
                      _PaletteSteps            ("Palette Steps per Channel",   Range(2,64))  = 48
                      _PaletteSaturation       ("Palette Saturation",          Range(0,1.5)) = 1.00

        [Header(Semicartoon Tetrico  Normal Driven Rim and Dither)]
                      _NormalRimStrength       ("Normal Rim Strength",         Range(0,2))   = 0.5
                      _NormalRimPower          ("Normal Rim Power",            Range(0.5,8)) = 3.0
                      _NormalRimColor          ("Normal Rim Color",            Color)        = (0,0,0,1)
                      _NormalDitherStrength    ("Normal Dither Strength",      Range(0,2))   = 0.5

        [Header(Highlight Softness  Reinhard rolloff)]
                      _HighlightSoftness       ("Highlight Softness",          Range(0,1))   = 0.10

        [Header(Ambient Lift  unlit visibility floor  OFF by default)]
                      _AmbientLift             ("Ambient Lift",                Range(0,1))   = 0.0
                      _AmbientLiftFadeDistance ("Ambient Lift Fade Distance m",Float)        = 8.0
                      _AmbientLiftTint         ("Ambient Lift Tint",           Color)        = (1,1,1,1)

        [Header(Exposure and Burn Control)]
                      _Exposure                ("Exposure",                    Range(0,3))   = 1.0
                      _MaxBrightness           ("Max Brightness Clamp",        Range(0.5,8)) = 8.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"            = "Opaque"
            "RenderPipeline"        = "UniversalPipeline"
            "Queue"                 = "Geometry"
            "IgnoreProjector"       = "True"
            "UniversalMaterialType" = "Lit"
        }
        LOD 300

        // =========================================================
        // FORWARD LIT PASS
        // =========================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   LitPassVertex
            #pragma fragment LitPassFragment

            // URP keywords - mirror of URP/Lit.shader (17.2)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _NormalStrength;
                float  _Metallic;
                float  _Smoothness;
                float  _OcclusionStrength;
                float  _CurvatureStrength;
                float  _MaskMapStrength;
                float4 _EmissionColor;

                float  _StylizedAOStrength;
                float  _StylizedAOContrast;
                float  _StylizedAOScale;

                float  _FakeAOStrength;
                float  _FakeAONormalSensitivity;
                float  _FakeAODepthSensitivity;
                float  _FakeAOContrast;
                float4 _FakeAOTint;

                float  _Level1Distance;
                float  _Level2Distance;
                float  _LevelBlend;
                float  _AOLevel1Mul;
                float  _AOLevel2Mul;
                float  _AOLevel3Mul;
                float  _DitherLevel1Mul;
                float  _DitherLevel2Mul;
                float  _DitherLevel3Mul;
                float  _Level3FogStrength;
                float4 _Level3FogColor;
                float  _Level3FogStart;

                float  _ShadowAOBoost;
                float  _ShadowAOThreshold;

                float  _DitherStrength;
                float  _DitherScale;
                float  _HighlightDither;
                float  _HighlightThreshold;
                float  _PaletteSteps;
                float  _PaletteSaturation;

                float  _NormalRimStrength;
                float  _NormalRimPower;
                float4 _NormalRimColor;
                float  _NormalDitherStrength;
                float  _HighlightSoftness;

                float  _AmbientLift;
                float  _AmbientLiftFadeDistance;
                float4 _AmbientLiftTint;

                float  _Exposure;
                float  _MaxBrightness;
            CBUFFER_END

            TEXTURE2D(_BaseMap);          SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);        SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_OcclusionMap);     SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_CurvatureMap);     SAMPLER(sampler_CurvatureMap);
            TEXTURE2D(_MaskMap);          SAMPLER(sampler_MaskMap);
            TEXTURE2D(_EmissionMap);      SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_StylizedAOMap);    SAMPLER(sampler_StylizedAOMap);

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                float2 lightmapUV   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS               : SV_POSITION;
                float3 positionWS               : TEXCOORD0;
                float3 normalWS                 : TEXCOORD1;
                float4 tangentWS                : TEXCOORD2;
                float2 uv                       : TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 4);
                float  fogFactor                : TEXCOORD5;
                float3 viewDirWS                : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ============================================================
            // 8x8 Bayer matrix for ordered dithering (PSX / Buckshot style)
            // Values normalized to [0, 1).
            // ============================================================
            static const float kBayer8x8[64] =
            {
                 0.0/64.0, 32.0/64.0,  8.0/64.0, 40.0/64.0,  2.0/64.0, 34.0/64.0, 10.0/64.0, 42.0/64.0,
                48.0/64.0, 16.0/64.0, 56.0/64.0, 24.0/64.0, 50.0/64.0, 18.0/64.0, 58.0/64.0, 26.0/64.0,
                12.0/64.0, 44.0/64.0,  4.0/64.0, 36.0/64.0, 14.0/64.0, 46.0/64.0,  6.0/64.0, 38.0/64.0,
                60.0/64.0, 28.0/64.0, 52.0/64.0, 20.0/64.0, 62.0/64.0, 30.0/64.0, 54.0/64.0, 22.0/64.0,
                 3.0/64.0, 35.0/64.0, 11.0/64.0, 43.0/64.0,  1.0/64.0, 33.0/64.0,  9.0/64.0, 41.0/64.0,
                51.0/64.0, 19.0/64.0, 59.0/64.0, 27.0/64.0, 49.0/64.0, 17.0/64.0, 57.0/64.0, 25.0/64.0,
                15.0/64.0, 47.0/64.0,  7.0/64.0, 39.0/64.0, 13.0/64.0, 45.0/64.0,  5.0/64.0, 37.0/64.0,
                63.0/64.0, 31.0/64.0, 55.0/64.0, 23.0/64.0, 61.0/64.0, 29.0/64.0, 53.0/64.0, 21.0/64.0
            };

            float Bayer8x8(uint2 p)
            {
                uint x = p.x & 7u;
                uint y = p.y & 7u;
                return kBayer8x8[y * 8u + x];
            }

            Varyings LitPassVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.normalWS   = vni.normalWS;
                OUT.tangentWS  = float4(vni.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.viewDirWS  = GetWorldSpaceViewDir(vpi.positionWS);
                OUT.fogFactor  = ComputeFogFactor(vpi.positionCS.z);

                OUTPUT_LIGHTMAP_UV(IN.lightmapUV, unity_LightmapST, OUT.lightmapUV);
                OUTPUT_SH(OUT.normalWS, OUT.vertexSH);
                return OUT;
            }

            half4 LitPassFragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 wpos = IN.positionWS;
                float3 wnG  = normalize(IN.normalWS);

                float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float3 albedo     = baseSample.rgb * _BaseColor.rgb;
                float  alpha      = baseSample.a   * _BaseColor.a;

                // ===== STYLIZED TRIPLANAR AO (neutral darkening, no tint) =====
                // Bright pixels in the overlay map = MORE darkening (cavity / grime).
                // Default texture "black" = no contribution.
                {
                    float3 p = wpos * _StylizedAOScale;
                    float3 nAbs = abs(wnG);
                    nAbs /= max(dot(nAbs, 1.0.xxx), 1e-5);
                    float ax = SAMPLE_TEXTURE2D(_StylizedAOMap, sampler_StylizedAOMap, p.yz).r;
                    float ay = SAMPLE_TEXTURE2D(_StylizedAOMap, sampler_StylizedAOMap, p.xz).r;
                    float az = SAMPLE_TEXTURE2D(_StylizedAOMap, sampler_StylizedAOMap, p.xy).r;
                    float ao = ax * nAbs.x + ay * nAbs.y + az * nAbs.z;
                    ao = pow(saturate(ao), _StylizedAOContrast);
                    albedo *= lerp(1.0, 1.0 - ao, _StylizedAOStrength);
                }

                // --- Normal map ---
                float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv),
                                                   _NormalStrength);
                float sgn        = IN.tangentWS.w;
                float3 bitangent = sgn * cross(wnG, IN.tangentWS.xyz);
                float3 normalWS  = normalize(TransformTangentToWorld(
                                       normalTS, float3x3(IN.tangentWS.xyz, bitangent, wnG)));

                // --- Metallic / Smoothness from map (R = metallic, A = smoothness) ---
                float4 mg = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, IN.uv);

                // --- Mask Map (HDRP-style: R metal, G AO, B detail, A smooth) ---
                //     _MaskMapStrength = 0 means ignore (default white texture has no effect anyway).
                float4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, IN.uv);
                float maskMetal  = lerp(1.0, mask.r, _MaskMapStrength);
                float maskAO     = lerp(1.0, mask.g, _MaskMapStrength);
                float maskSmooth = lerp(1.0, mask.a, _MaskMapStrength);

                // --- Occlusion (G) and Curvature (R) ---
                float occ  = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, IN.uv).g;
                float curv = SAMPLE_TEXTURE2D(_CurvatureMap, sampler_CurvatureMap, IN.uv).r;
                float occlusion = lerp(1.0, occ, _OcclusionStrength);
                occlusion *= lerp(1.0, curv, _CurvatureStrength);
                occlusion *= maskAO;

                // --- Pack SurfaceData ---
                SurfaceData sd = (SurfaceData)0;
                sd.albedo      = albedo;
                sd.metallic    = mg.r * _Metallic   * maskMetal;
                sd.smoothness  = mg.a * _Smoothness * maskSmooth;
                sd.normalTS    = normalTS;
                sd.emission    = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).rgb
                                 * _EmissionColor.rgb;
                sd.occlusion   = occlusion;
                sd.alpha       = alpha;

                // --- Pack InputData ---
                InputData inputData            = (InputData)0;
                inputData.positionWS           = wpos;
                inputData.normalWS             = normalWS;
                inputData.viewDirectionWS      = SafeNormalize(IN.viewDirWS);
            #if defined(_MAIN_LIGHT_SHADOWS)
                inputData.shadowCoord          = TransformWorldToShadowCoord(wpos);
            #else
                inputData.shadowCoord          = float4(0,0,0,0);
            #endif
                inputData.fogCoord             = IN.fogFactor;
                inputData.vertexLighting       = 0;
                inputData.bakedGI              = SAMPLE_GI(IN.lightmapUV, IN.vertexSH, normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask           = SAMPLE_SHADOWMASK(IN.lightmapUV);

                float camDist = length(_WorldSpaceCameraPos - wpos);

                half4 color = UniversalFragmentPBR(inputData, sd);

                // ============================================================
                // EXPOSURE  (linear lighting multiplier; affects everything
                // downstream including emission. Set < 1 to dim, > 1 to brighten.)
                // ============================================================
                color.rgb *= _Exposure;

                // ============================================================
                // NORMAL RIM (semicartoon Lethal/Inscryption silhouette)
                //   Fresnel based on world normal vs view: silhouettes get
                //   tinted toward _NormalRimColor. Slider _NormalRimStrength
                //   controls intensity (0 = off).
                // ============================================================
                if (_NormalRimStrength > 0.0)
                {
                    float ndv  = saturate(1.0 - saturate(dot(normalWS, SafeNormalize(IN.viewDirWS))));
                    float rim  = pow(ndv, _NormalRimPower) * _NormalRimStrength;
                    color.rgb  = lerp(color.rgb, _NormalRimColor.rgb, saturate(rim));
                }

                // ============================================================
                // SCREEN-SPACE FAKE AO  (Buckshot/Lethal/REPO style)
                //   Detects normal discontinuities (corners, joints) via
                //   ddx/ddy of world normal, and depth gaps (mesh silhouettes
                //   against farther geometry) via ddx/ddy of camera distance.
                //   Output is multiplied by distance ramp, shadow boost and
                //   dithered with an 8x8 Bayer matrix.
                // ============================================================

                // --- Geometric edge signal ---
                //   IMPORTANT: use wnG (unperturbed geometric normal), not the
                //   normal-mapped normalWS. Otherwise every texture detail
                //   (brick cracks, stone bumps) registers as a "corner" and
                //   the whole surface darkens to mud.
                float3 nDx = ddx(wnG);
                float3 nDy = ddy(wnG);
                float normCurv = (length(nDx) + length(nDy)) * _FakeAONormalSensitivity;

                float depthGrad = length(float2(ddx(camDist), ddy(camDist)));
                float depthEdge = saturate(depthGrad / max(camDist, 0.01)) * _FakeAODepthSensitivity;

                float aoRaw = pow(saturate(normCurv + depthEdge), _FakeAOContrast);

                // ============================================================
                // THREE LEVEL DISTANCE SYSTEM
                //   Level 1 (0 .. L1End)        : Base subtle AO, light dither.
                //   Level 2 (L1End .. L2End)    : Intermediate AO amplified.
                //   Level 3 (L2End ..)          : Total - heavy AO + fog.
                //   Smoothstep transitions so gradients are most felt right
                //   before crossing into the next level (per user request).
                // ============================================================
                float blend = max(_LevelBlend, 0.01);
                float t12 = smoothstep(_Level1Distance - blend, _Level1Distance + blend, camDist);
                float t23 = smoothstep(_Level2Distance - blend, _Level2Distance + blend, camDist);

                // Per-level multipliers, smoothly blended.
                float aoLevelMul     = lerp(_AOLevel1Mul,
                                            lerp(_AOLevel2Mul, _AOLevel3Mul, t23), t12);
                float ditherLevelMul = lerp(_DitherLevel1Mul,
                                            lerp(_DitherLevel2Mul, _DitherLevel3Mul, t23), t12);

                // Shadow accumulation: dark areas eat more AO.
                float luma       = dot(color.rgb, float3(0.299, 0.587, 0.114));
                float shadowMask = 1.0 - smoothstep(0.0, _ShadowAOThreshold, luma);
                float shadowMul  = lerp(1.0, _ShadowAOBoost, shadowMask);

                float ao = aoRaw * _FakeAOStrength * aoLevelMul * shadowMul;

                // --- Bayer dither the AO mask (scaled by level) ---
                uint2 pixCoord = uint2(IN.positionCS.xy / max(_DitherScale, 1.0));
                float bayer    = Bayer8x8(pixCoord);
                ao = saturate(ao + (bayer - 0.5) * _DitherStrength * ditherLevelMul);

                // Normal-driven dither: areas with high normal variation
                // (curvature, creases, silhouettes) get extra bayer-based
                // granulation. Independent of light/shadow.
                float normalDither = bayer * saturate(normCurv) * _NormalDitherStrength;
                ao = saturate(ao + normalDither);

                // --- Apply darkening to lighting (preserve emission) ---
                {
                    float3 lit = max(color.rgb - sd.emission, 0.0);
                    lit = lerp(lit, _FakeAOTint.rgb, ao);
                    color.rgb = lit + sd.emission;
                }

                // ============================================================
                // HIGHLIGHT GRANULATION (scaled by per-level dither multiplier)
                //   Dithers bright pixels (light highlights) for PSX feel.
                // ============================================================
                {
                    float litLuma = dot(color.rgb, float3(0.299, 0.587, 0.114));
                    float hMask   = saturate((litLuma - _HighlightThreshold) * 2.0);
                    float hD      = (bayer - 0.5) * _HighlightDither * hMask * ditherLevelMul;
                    color.rgb = saturate(color.rgb + hD);
                }

                // ============================================================
                // HIGHLIGHT SOFTNESS (Reinhard rolloff on lighting only)
                // ============================================================
                if (_HighlightSoftness > 0.0)
                {
                    float3 lit2 = max(color.rgb - sd.emission, 0.0);
                    float3 soft = lit2 / (1.0 + lit2 * _HighlightSoftness);
                    color.rgb   = soft + sd.emission;
                }

                // ============================================================
                // PSX PALETTE QUANTIZATION
                //   Reduce saturation then quantize per channel with bayer
                //   dithering so gradients stay smooth despite low step count.
                // ============================================================
                {
                    float palLuma = dot(color.rgb, float3(0.299, 0.587, 0.114));
                    color.rgb = lerp(palLuma.xxx, color.rgb, _PaletteSaturation);

                    float pSteps = max(_PaletteSteps, 2.0);
                    color.rgb = floor(color.rgb * pSteps + (bayer - 0.5)) / pSteps;
                }

                // ============================================================
                // MAX BRIGHTNESS CLAMP  (anti-burn ceiling)
                //   Hard ceiling per-channel. Combined with _HighlightSoftness
                //   (Reinhard) above, gives both smooth rolloff and a hard cap.
                //   Set higher to allow more burn; lower to flatten highlights.
                // ============================================================
                color.rgb = min(color.rgb, _MaxBrightness.xxx);

                // ============================================================
                // AMBIENT LIFT  (off by default; uses ALBEDO to preserve color)
                //   Lifts dark/unlit pixels back to a fraction of their own
                //   albedo color instead of toward gray/white. This way
                //   bricks stay brick-colored, stone stays stone-colored.
                //   Fades to 0 at _AmbientLiftFadeDistance so far pixels can
                //   stay pitch black.
                //   Applied LAST so palette quantization does not affect it.
                // ============================================================
                if (_AmbientLift > 0.0)
                {
                    float liftFade = saturate(1.0 - camDist /
                                               max(_AmbientLiftFadeDistance, 0.01));
                    float3 liftCol = sd.albedo * _AmbientLiftTint.rgb *
                                     (_AmbientLift * liftFade);
                    color.rgb = max(color.rgb, liftCol);
                }

                // ============================================================
                // LEVEL 3 SHADER FOG  (everything becomes indistinguishable)
                //   Smooth fog that ramps up entering the Total level (L2+).
                //   Independent of URP fog so it can dominate without
                //   touching the rest of the scene.
                //   Mixed BEFORE URP MixFog so URP fog adds on top if active.
                // ============================================================
                if (_Level3FogStrength > 0.0)
                {
                    float fogT = saturate((camDist - (_Level2Distance + _Level3FogStart)) /
                                          max(_LevelBlend * 3.0, 0.01));
                    fogT = smoothstep(0.0, 1.0, fogT);
                    color.rgb = lerp(color.rgb, _Level3FogColor.rgb,
                                     fogT * _Level3FogStrength);
                }

                color.rgb = MixFog(color.rgb, IN.fogFactor);
                return color;
            }
            ENDHLSL
        }

        // =========================================================
        // SHADOW CASTER
        // =========================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 GetShadowPositionHClip(A IN)
            {
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            V ShadowPassVertex(A IN)
            {
                V OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = GetShadowPositionHClip(IN);
                return OUT;
            }

            half4 ShadowPassFragment(V IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // =========================================================
        // DEPTH ONLY
        // =========================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V DepthOnlyVertex(A IN)
            {
                V OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
            half4 DepthOnlyFragment(V IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // =========================================================
        // DEPTH NORMALS (for SSAO / Decals)
        // =========================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V DepthNormalsVertex(A IN)
            {
                V OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }
            half4 DepthNormalsFragment(V IN) : SV_Target
            {
                return half4(normalize(IN.normalWS) * 0.5 + 0.5, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

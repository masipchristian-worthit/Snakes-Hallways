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
                      _EmissionMap      ("Emission Map",      2D)         = "black" {}

        [Header(Stylized Triplanar AO  neutral darkening)]
                      _StylizedAOMap      ("AO Overlay Map",          2D)           = "black" {}
                      _StylizedAOStrength ("AO Overlay Strength",     Range(0,1))   = 0.0
                      _StylizedAOContrast ("AO Overlay Contrast",     Range(0.1,3)) = 1.20
                      _StylizedAOScale    ("AO Overlay Scale world",  Float)        = 0.35

        [Header(Scene Extremes AO  distance darken)]
                      _AOExtremesStrength ("AO Extremes Strength",    Range(0,1))   = 0.45
                      _AOStartDistance    ("AO Start Distance m",     Float)        = 4.0
                      _AOEndDistance      ("AO End Distance m",       Float)        = 14.0

        [Header(Highlight Softness  Reinhard rolloff)]
                      _HighlightSoftness  ("Highlight Softness",      Range(0,1))   = 0.15
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
                float  _AOExtremesStrength;
                float  _AOStartDistance;
                float  _AOEndDistance;
                float  _HighlightSoftness;
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

                // ===== SCENE EXTREMES AO =====
                // Darkens the lit result with distance from camera (neutral, no tint).
                // Produces the "player halo of light" horror feel without burning silhouettes.
                float camDist = length(_WorldSpaceCameraPos - wpos);
                float aoFade  = saturate((camDist - _AOStartDistance) /
                                          max(_AOEndDistance - _AOStartDistance, 1e-3));
                float aoMul   = lerp(1.0, 1.0 - _AOExtremesStrength, aoFade);

                // Apply to albedo and baked GI so direct + indirect lighting both fade.
                // Emission stays untouched (handled inside UniversalFragmentPBR via sd.emission).
                sd.albedo            *= aoMul;
                inputData.bakedGI    *= aoMul;

                half4 color = UniversalFragmentPBR(inputData, sd);

                // ===== HIGHLIGHT SOFTNESS (Reinhard rolloff on lighting only) =====
                // Subtract emission, soften, re-add emission so torches keep punch.
                if (_HighlightSoftness > 0.0)
                {
                    float3 lit   = max(color.rgb - sd.emission, 0.0);
                    float3 soft  = lit / (1.0 + lit * _HighlightSoftness);
                    color.rgb    = soft + sd.emission;
                }

                color.rgb   = MixFog(color.rgb, IN.fogFactor);
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

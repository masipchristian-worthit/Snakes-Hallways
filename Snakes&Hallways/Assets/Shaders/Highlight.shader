// Pinta el objeto con un color sólido y lo renderiza POR ENCIMA de cualquier
// otra geometría (ZTest Always) → efecto rayos X. Doble pass:
//   - Pass 1: fill plano por encima de todo, color principal con alpha bajo.
//   - Pass 2: outline opaco (escala los normales un poco hacia afuera).
//
// Uso: HighlightController instancia un Material con este shader por categoría
// (Pickup/Portal/Enemy) y lo swappea sobre el material array del Renderer
// mientras dura el resaltado.
Shader "SH/Highlight"
{
    Properties
    {
        _Color ("Tint", Color) = (1,0.85,0.2,0.55)
        _OutlineColor ("Outline", Color) = (1,1,1,1)
        _OutlineWidth ("Outline width", Range(0,0.05)) = 0.012
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "IgnoreProjector"="True" }

        // ── Outline (se dibuja antes, expandiendo el mesh) ────────────────
        Pass
        {
            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _OutlineColor; float _OutlineWidth;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; };
            v2f vert(appdata v){
                v2f o;
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                float3 n = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                wp.xyz += n * _OutlineWidth;
                o.pos = mul(UNITY_MATRIX_VP, wp);
                return o;
            }
            half4 frag(v2f i):SV_Target { return _OutlineColor; }
            ENDHLSL
        }

        // ── Fill XRay (encima de todo) ─────────────────────────────────────
        Pass
        {
            Cull Back
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };
            v2f vert(appdata v){ v2f o; o.pos = UnityObjectToClipPos(v.vertex); return o; }
            half4 frag(v2f i):SV_Target { return _Color; }
            ENDHLSL
        }
    }
    Fallback Off
}

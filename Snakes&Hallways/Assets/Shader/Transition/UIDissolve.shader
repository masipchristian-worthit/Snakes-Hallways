Shader "UI/Dissolve"
{
    // UI-compatible dissolve shader used by SceneTransition.
    // _Cutoff = 0 → image fully visible (black overlay).
    // _Cutoff = 1 → image fully dissolved (scene visible).
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color      ("Tint",        Color)        = (0,0,0,1)
        _NoiseTex   ("Noise (R)",   2D)           = "white" {}
        _Cutoff     ("Cutoff",      Range(0,1))   = 0
        _EdgeWidth  ("Edge Width",  Range(0,0.5)) = 0.08
        _EdgeColor  ("Edge Color",  Color)        = (1.0, 0.45, 0.1, 1)
        _EdgeIntensity ("Edge Intensity", Range(0,4)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "Queue"="Overlay"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            float4    _NoiseTex_ST;
            float4    _Color;
            float     _Cutoff;
            float     _EdgeWidth;
            float4    _EdgeColor;
            float     _EdgeIntensity;

            v2f vert(appdata IN)
            {
                v2f OUT;
                OUT.pos   = UnityObjectToClipPos(IN.vertex);
                OUT.uv    = TRANSFORM_TEX(IN.uv, _NoiseTex);
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float n = tex2D(_NoiseTex, IN.uv).r;

                // visible where noise > cutoff
                float visible = smoothstep(_Cutoff, _Cutoff + 0.005, n);

                // edge band right above the cutoff line
                float edge = (1.0 - smoothstep(_Cutoff, _Cutoff + _EdgeWidth, n)) * visible;

                fixed4 baseCol = _Color * IN.color;
                fixed3 rgb = lerp(baseCol.rgb, _EdgeColor.rgb, saturate(edge * _EdgeIntensity * _EdgeColor.a));

                return fixed4(rgb, baseCol.a * visible);
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}

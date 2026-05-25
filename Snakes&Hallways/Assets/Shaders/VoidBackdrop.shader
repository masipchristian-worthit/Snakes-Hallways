// Pinta de negro plano cualquier pixel "del fondo" sin necesitar tocar la cámara
// ni el skybox. Se aplica sobre un cubo gigantesco invertido (cull front) que
// envuelve todo el mapa: como mira hacia DENTRO, solo se ve cuando la cámara
// está dentro del cubo y mira a un hueco entre módulos. Detrás de la geometría
// real del mapa NO se ve. Funciona en Built-in y URP (Unlit no toca lighting).
//
// Uso:
//   1. Crea un Cube en escena.
//   2. Escálalo enorme (p.ej. 500/500/500) englobando todo el nivel.
//   3. Crea un Material con este shader (Shader = "SH/VoidBackdrop").
//      Color por defecto negro; ajustable si quieres "gris muy oscuro".
//   4. Asígnaselo al cubo. Quítale el collider.
//   5. Ponlo en una layer "VoidBackdrop" y asegúrate que la cámara la renderiza.
//
// Notas:
//   - Cull Front + ZWrite On: el cubo escribe Z pero solo desde dentro.
//     Cualquier geometría real lo tapará porque su Z será más cercano.
//   - Queue "Background"+1 para asegurar que el resto del mundo pinta encima.
//   - No usa fog ni recibe sombras ni reflejos.
Shader "SH/VoidBackdrop"
{
    Properties
    {
        _Color ("Color", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background+1" "IgnoreProjector"="True" }
        Cull Front
        ZWrite On
        ZTest LEqual
        Lighting Off
        Fog { Mode Off }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); return o; }
            half4 frag (v2f i) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
    Fallback Off
}

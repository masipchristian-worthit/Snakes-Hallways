# MainShader — Documentación local

## Qué es
Framework de shading de entorno para Snakes&Hallways: un drop-in de URP/Lit
con detección automática de costuras modulares, mugre direccional, color
grade sepia y niebla volumétrica cercana.

## Archivos

```
MainShader/
├── MainShader.shader              ← shader principal (ShaderLab + HLSL)
├── MainShader_Includes.hlsl       ← librería compartida por los passes
│
├── CustomFunctions/               ← .hlsl reutilizables (Shader Graph)
│   ├── CF_FwidthNormal.hlsl
│   ├── CF_CurvatureSeam.hlsl
│   ├── CF_TriplanarBlend.hlsl
│   ├── CF_FloorWallMask.hlsl
│   ├── CF_StylizedNoise.hlsl
│   ├── CF_GrimeAccumulation.hlsl
│   ├── CF_DistanceFade.hlsl
│   ├── CF_VolumetricFog.hlsl
│   └── CF_ColorGrade.hlsl
│
├── Subgraphs/
│   └── _README_SUBGRAPHS.md       ← cómo crear los subgraphs en Unity
│
├── Noise/                         ← coloca aquí las texturas de ruido
│   └── (T_NoiseTile_Worley_01.png — debes generarla, ver más abajo)
│
└── Documentation/
    ├── README.md                  ← este archivo
    └── InspectorReference.md
```

## Texturas que necesitas crear o importar

| Slot           | Requisitos                         | Fuente sugerida                |
|----------------|------------------------------------|--------------------------------|
| `_NoiseTexture`| 512², R8, tileable, Worley/Voronoi | substance / GIMP / Krita      |
| `_GrimeMap`    | 1024², R8 o RGBA8, tileable        | textures.com, dirt overlays   |
| `_BaseMap`     | tu textura de piedra/madera        | tu pipeline                    |
| `_NormalMap`   | normal map del base                | tu pipeline                    |

## Cómo aplicar el shader
1. En cualquier material existente, cambia el shader a `Custom/MainShader`.
2. Los slots de textura están alineados con la convención URP/Lit, así que
   tus texturas existentes se mantienen.
3. Por defecto vienen los valores Inscryption-sepia. Toca el inspector si
   necesitas otro mood.

## Toggles
- **Triplanar Sampling**: actívalo en piezas con escalado no uniforme o
  UVs rotas. Coste: +6 samples.
- **Grime**: capa de mugre direccional. ON por defecto.
- **Volumetric Fog**: niebla densa cercana. ON por defecto.
- **Detail Noise**: ruido que disuelve la costura. ON por defecto.
- **Fresnel**: realce sutil en muros. ON por defecto.

Ver `InspectorReference.md` para descripción de cada propiedad.

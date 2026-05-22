# Subgraphs — instrucciones de creación

Los archivos `.shadergraph` y `.shadersubgraph` de Unity 6 / URP 17 son JSON
con GUIDs internos generados por el editor. Crearlos a mano fuera del editor
produce archivos que Unity puede no abrir correctamente.

**Por eso `MainShader.shader` está implementado como ShaderLab/HLSL puro:
es 1:1 equivalente al Shader Graph descrito y funciona sin nada más.**

Si en algún momento quieres la versión visual en Shader Graph, sigue esta
correspondencia 1:1 con los archivos `.hlsl` en `../CustomFunctions/`:

| Subgraph a crear              | Custom Function HLSL                   | Función           |
|-------------------------------|----------------------------------------|-------------------|
| `SG_TriplanarSampler`         | `CF_TriplanarBlend.hlsl`               | `TriplanarAlbedo_float` |
| `SG_TriplanarNormal`          | `CF_TriplanarBlend.hlsl`               | `TriplanarNormal_float` |
| `SG_CurvatureSeamMask`        | `CF_CurvatureSeam.hlsl`                | `CurvatureSeam_float`   |
| `SG_FloorWallMask`            | `CF_FloorWallMask.hlsl`                | `FloorWallMask_float`   |
| `SG_StylizedNoise`            | `CF_StylizedNoise.hlsl`                | `StylizedNoise_float`   |
| `SG_GrimeAccumulation`        | `CF_GrimeAccumulation.hlsl`            | `GrimeAccumulation_float` |
| `SG_DistanceFade`             | `CF_DistanceFade.hlsl`                 | `DistanceFade_float`    |
| `SG_VolumetricFog`            | `CF_VolumetricFog.hlsl`                | `VolumetricFog_float`   |
| `SG_ColorGrade`               | `CF_ColorGrade.hlsl`                   | `ColorGrade_float`      |

## Cómo crear cada subgraph en Unity
1. `Create > Shader Graph > Sub Graph`. Nómbralo según la tabla.
2. Añade un nodo **Custom Function**.
3. En el inspector del nodo: `Source = File`, `File = <ruta al .hlsl correspondiente>`,
   `Name = <nombre de la función>_float`.
4. Crea los **Inputs** y **Outputs** del subgraph que coincidan exactamente
   con la firma de la función HLSL (orden y tipos).
5. Conecta inputs → Custom Function → outputs.

## Para el Shader Graph principal
1. `Create > Shader Graph > URP > Lit Shader Graph`. Nómbralo `MainShader_Graph`.
2. Añade todas las Properties listadas en la sección 7.1 del README principal.
3. Añade los Keywords: `_TRIPLANAR_ON`, `_GRIME_ON`, `_VOLUMETRICFOG_ON`,
   `_DETAIL_NOISE_ON`, `_FRESNEL_ON`.
4. Replica la cascada de bloques descrita en la sección 7.2 usando los
   subgraphs anteriores.

> NOTA: la versión ShaderLab (`MainShader.shader`) está pensada para uso
> en producción. La versión Shader Graph es opcional, solo para artistas
> que prefieran editar visualmente.

# READMECLAUDE — Contexto persistente para Claude

> **Propósito de este archivo:** servir como memoria de proyecto para futuras
> sesiones de Claude que trabajen en el sistema de shading del juego. Resume
> todas las decisiones técnicas y artísticas tomadas en la sesión donde se
> diseñó e implementó `MainShader`.
>
> **Si abres este archivo en una sesión nueva: léelo entero antes de tocar
> el shader.** Contiene el "por qué" detrás de cada decisión.

---

## 🚀 QUICK START — Implementación en 5 minutos

```
┌─────────────────────────────────────────────────────────────────┐
│  PASO 1 · Refrescar Unity                                       │
├─────────────────────────────────────────────────────────────────┤
│  Ctrl + R                                                       │
│  → Unity compila el shader y los 2 scripts de Editor.           │
│  → Comprueba Console: 0 errores rojos.                          │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  PASO 2 · Crear ShaderManager (singleton)                       │
├─────────────────────────────────────────────────────────────────┤
│  · Crea un GameObject vacío en escena: "ShaderManager"           │
│  · Añade el componente: Assets/Scripts/Managers/ShaderManager.cs │
│  · Inspector ▸ botón "Auto-Fill From Scene"                     │
│  → Busca y lista todos los materiales que usan Custom/MainShader │
│  → Permite tuning global de: AO Extremes, Highlight Softness     │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  PASO 3 · Crear material                                        │
├─────────────────────────────────────────────────────────────────┤
│  Click derecho en Assets/Shader/MainShader/                     │
│  → Create ▸ Material  →  nombre: M_MainShader_Default           │
│  → Inspector ▸ Shader  →  Custom/MainShader                     │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  PASO 4 · Asignar texturas (PBR estándar)                       │
├─────────────────────────────────────────────────────────────────┤
│   Base Map ....................... tu piedra/madera              │
│   Base Color ..................... ajusta tono base              │
│   Normal Map ..................... su normal map                 │
│   Metallic / Smoothness Map ...... R=metallic, A=smooth         │
│   Occlusion / Curvature Map ...... AO (G) y curvature (R)       │
│   Mask Map (opcional) ............ HDRP-style RGBA              │
│   Emission Map (opcional) ........ brillo                       │
│                                                                 │
│   El resto: defaults URP puro (SIN filtro sepia).               │
│   Scene Extremes AO activo por defecto (4-14m).                 │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  PASO 5 · Aplicar y validar                                     │
├─────────────────────────────────────────────────────────────────┤
│  Arrastra el material a un muro/suelo modular.                  │
│                                                                 │
│  ✓ TEST coplanar:                                               │
│      [▓▓▓▓][▓▓▓▓]   ← 2 muros 2×2 juntos → colores naturales    │
│                                                                 │
│  ✓ TEST distancia (Scene Extremes AO):                          │
│      0m ━━━ legible ━━ 4m inicio fade ━━ 14m saturation        │
│      → halo de luz alrededor jugador, extremos oscurecidos      │
│                                                                 │
│  ✓ TEST highlights:                                             │
│      Superficies brillantes sin burn visual → Reinhard rolloff  │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  ✅ LISTO. Migra el resto de materiales cambiándoles el shader. │
│  Los slots URP/Lit se conservan automáticamente (drop-in).      │
└─────────────────────────────────────────────────────────────────┘
```

### ⚠️ Si algo falla

| Síntoma                                    | Solución                                       |
|--------------------------------------------|------------------------------------------------|
| Error de compilación al refrescar          | Pegar mensaje exacto de Console                |
| Material magenta                           | URP < 17.2 — actualizar package                |
| Demasiado oscuro en extremos                | Bajar `_AOExtremesStrength` a 0.2–0.3           |
| No hay oscurecimiento en distancia         | Subir `_AOExtremesStrength` a 0.6–0.8           |
| Highlights aún queman                      | Subir `_HighlightSoftness` a 0.3–0.5            |
| Hay filtro sepia en materiales viejos      | Reset material o reasignar shader                |
| Properties `_EdgeAOStrength` legacy error  | Ignorar (ya no existen en v2.6)                 |

---

## 1. Proyecto

- **Juego:** Snakes & Hallways
- **Género:** Horror first-person medieval estilizado
- **Engine:** Unity `6000.2.10f1`
- **Render Pipeline:** URP `17.2.0` en Forward+
- **Plataformas objetivo:** PC medio / Steam Deck
- **Referencias visuales declaradas:** R.E.P.O., Fear & Hunger, Inscryption,
  Lethal Company, Clover Pit, Buckshot Roulette, Darkest Dungeon.

## 2. Problema que resuelve `MainShader`

Las escenas son **modulares**: el level design ensambla suelos y muros desde
piezas independientes (M_Suelo_2x2, M_Muro_2x2, columnas, etc.). Esto deja
visibles tres problemas:
1. Costuras feas entre módulos contiguos.
2. Discontinuidades de iluminación entre piezas.
3. Bordes lineales que rompen el mood de "catacumba erosionada".

`MainShader` es un **framework drop-in de URP/Lit** que detecta y enmascara
estas costuras de forma **localizada** (sin contaminar muros lisos continuos
ni añadir outlines genéricos toon).

## 3. Decisiones de diseño (Test del usuario + iteraciones)

| Bloque | Decisión (v2.6) |
|---|---|
| Arquitectura | Framework drop-in URP/Lit, PBR puro + extras opcionales |
| Rendimiento | Steam Deck-compatible, Forward+ ready |
| Tratamiento de costura | **Abandoned:** fwidth-based detection fue invisible. Pivotado a Scene Extremes AO. |
| Oscurecimiento | **Scene Extremes AO:** distance-based, no seam-specific. Halo de luz alrededor jugador. |
| Mood | **Horror atmosférico, sin quema:** PBR neutro + AO en extremos + Reinhard rolloff. |
| Estilización | PBR estándar (sin tint/filtro). Modular + extensible. |
| Paleta | **Neutral:** user-determined vía texturas y BaseColor. SIN sepia por defecto. |
| Compresión de highlights | **Reinhard rolloff** en lighting (emission untouched). Anti-burn. |
| Curvature map | **Nuevo (v2.5):** multiplica occlusion en cavidades. |
| Metallic map | **Nuevo (v2.5):** estándar URP (R=metallic, A=smoothness). |
| Mask map | **Nuevo (v2.5):** HDRP-style (R=metal, G=AO, B=detail, A=smooth) para control packed. |
| ShaderManager | **Nuevo (v2.5):** Singleton para tuning global. Auto-fill desde escena. |

### Nota: cambio de estrategia (v2.4–v2.6)

**v2.1–v2.3** persiguió la detección geométrica de costuras:
> "Las paredes 2×2 coplanares NO DEBEN tener costuras, solo en esquinas."

Estrategia: `fwidth(WorldNormal)` en normal geométrica → resultado invisible en mallas smooth-shaded (FBX típicos). Abandonada.

**v2.4–v2.6** pivotó a estrategia de **Scene Extremes AO**:
- No intenta detectar costuras (detalles micro).
- Oscurece según **distancia desde cámara** → halo de luz horror.
- Neutral (sin tint) → user's colors untouched.
- Robusta: funciona en cualquier malla, sin dependencias geométricas.

**Resultado:** el problema de costuras se resuelve ahora via:
1. **Material continuidad:** texturas/normal maps alineadas.
2. **Geometric smoothing:** smoothing groups correctos en FBX.
3. **AO baked / curvature map:** detalle local via texturas.
4. **Scene Extremes AO:** oscurecimiento global con distancia (no seam-specific).

## 4. Estado actual de implementación (v2.6)

| Componente | Estado | Ruta |
|---|---|---|
| Shader principal (ShaderLab+HLSL) | ✅ v2.6 (Scene Extremes AO + Reinhard) | `Assets/Shader/MainShader/MainShader.shader` |
| ShaderManager singleton | ✅ Nuevo (v2.5) | `Assets/Scripts/Managers/ShaderManager.cs` |
| Slots PBR completos | ✅ BaseMap, NormalMap, MetallicGlossMap, OcclusionMap, CurvatureMap, MaskMap, EmissionMap | MainShader properties |
| Librería HLSL compartida | ✅ Legacy (v2.0, no usado en v2.6) | `Assets/Shader/MainShader/MainShader_Includes.hlsl` |
| Custom Functions HLSL | ✅ Legacy (disponible para Shader Graph) | `Assets/Shader/MainShader/CustomFunctions/*.hlsl` |
| Documentación local | ✅ README + code comments | `Assets/Shader/MainShader/Documentation/` y README.md |
| Generadores de textura (Editor) | ✅ Worley + Grime, vía menú `Tools/MainShader` | `Assets/Shader/MainShader/Editor/` |
| Subgraphs Shader Graph | ⚠️ Pendientes — guía de creación | `Assets/Shader/MainShader/Subgraphs/_README_SUBGRAPHS.md` |
| Texturas de ruido / mugre | ⚠️ Opcional; generar vía menú | `Assets/Shader/MainShader/Noise/` |

### Por qué no hay archivos `.shadergraph` / `.shadersubgraph`
Los `.shadergraph` de Unity 6 / URP 17 son JSON con GUIDs internos. Crearlos
fuera del editor produce archivos frágiles que Unity puede no abrir.
**La versión `.shader` implementa exactamente la misma lógica y es 1:1
equivalente**, así que se priorizó esa. Si el usuario quiere la versión
visual en Shader Graph, los `.hlsl` están listos para enchufarse a nodos
**Custom Function** según la tabla en `Subgraphs/_README_SUBGRAPHS.md`.

## 5. Arquitectura del shader

```
MainShader.shader
│
├── Properties (~40)              ← inspector controls agrupados por header
│
├── ForwardLit pass               ← PBR + capas custom
│   1. Sample base (UV o triplanar según _TRIPLANAR_ON)
│   2. Sample normal (UV o triplanar)
│   3. Computar máscaras modulares:
│        - SeamMask (fwidth de normal GEOMÉTRICA — coplanar-safe)
│        - SeamMaskSoft (versión amplia para grime)
│        - FloorMask/WallMask/CeilingMask (dot con UP)
│        - ProximityWeight (fade por distancia)
│   4. Ruido world-space triplanar
│   5. Disolución de costura (ruido rompe la silueta)
│   6. Oscurecimiento de costura (lerp a _SeamDarkenColor)
│   7. Grime (triplanar, direccional por gravedad, masked por suelo/muro)
│   8. Fresnel sutil (solo en muros)
│   9. Pack SurfaceData + InputData
│  10. UniversalFragmentPBR (lighting URP estándar)
│  11. Color grade (sepia shadow tint + saturation/contrast/ambient dark)
│  12. Volumetric fog cercana (proximidad + height + ruido temporal)
│  13. URP fog nativa (MixFog)
│
├── ShadowCaster pass             ← sombras direccionales y puntuales
├── DepthOnly pass                ← para post-process URP
├── DepthNormals pass             ← para SSAO, Decals
└── Meta pass                     ← para lightmap baking
```

## 6. Compatibilidad

| Feature URP | Soportada |
|---|---|
| Normal maps | ✅ |
| Metallic / Smoothness | ✅ |
| Emission | ✅ |
| Lightmaps (baked GI) | ✅ Meta pass |
| Realtime shadows (main + additional) | ✅ |
| Additional lights (Forward+) | ✅ |
| Reflection probes (incl. blending + box projection) | ✅ |
| URP Fog | ✅ MixFog |
| SSAO | ✅ DepthNormals pass + `_SCREEN_SPACE_OCCLUSION` |
| Decals | ✅ DepthNormals pass |
| GPU Instancing | ✅ `multi_compile_instancing` |
| DOTS Instancing | ✅ `DOTS_INSTANCING_ON` |
| Forward+ | ✅ `_FORWARD_PLUS` keyword propagada |
| SRP Batcher | ✅ CBUFFER UnityPerMaterial |
| Shadowmask | ✅ `SHADOWS_SHADOWMASK` |
| Adaptive Probe Volumes | Heredado de URP (probar) |

## 7. Estructura de carpetas

```
Assets/
├── READMECLAUDE/                  ← ESTE archivo
│   └── README.md
│
└── Shader/MainShader/
    ├── MainShader.shader          ← shader principal
    ├── MainShader_Includes.hlsl
    │
    ├── CustomFunctions/
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
    │   └── _README_SUBGRAPHS.md   ← guía para crear los SG opcionales
    │
    ├── Editor/                    ← generadores de textura (menú Tools)
    │   ├── WorleyNoiseGenerator.cs
    │   └── GrimeTextureGenerator.cs
    │
    ├── Noise/                     ← texturas generadas (output)
    │   ├── T_NoiseTile_Worley_01.png   (generada por menú)
    │   └── T_GrimeAtlas_01.png         (generada por menú)
    │
    └── Documentation/
        ├── README.md
        └── InspectorReference.md
```

## 8. Convenciones de nombres

| Prefijo | Tipo |
|---|---|
| `MainShader` / `MainShader_<Variant>` | Shaders |
| `SG_<Function>` | Subgraphs (cuando se creen) |
| `CF_<Function>` | Custom Functions HLSL |
| `T_<Category>_<Name>_<Version>` | Texturas |
| `M_<Shader>_<Variant>` | Materiales |
| `_PascalCase` | Properties (con underscore por convención URP) |
| `_FEATURE_ON` | Keywords |

## 9. Defaults artísticos (mood Inscryption sepia)

```
Edge Intensity ............ 4.0
Edge Width ................ 1.5
Blend Softness ............ 0.6
Edge Visibility Distance .. 8.0 m
Edge Fade Range ........... 3.0 m
Seam Darken Amount ........ 0.45
Seam Dissolve Amount ...... 0.35
Noise Scale ............... 3.5
Noise Contrast ............ 0.65   (obvio, R.E.P.O./Lethal)
Noise Breakup ............. 0.50
Grime Intensity ........... 0.55
Grime Directionality ...... 0.70
Floor Grime Boost ......... 1.3
Wall Grime Boost .......... 1.0
Shadow Intensity .......... 0.25
Saturation ................ 0.95
Contrast .................. 1.0
Ambient Darkness .......... 0.0
Ambient Lift .............. 0.06   (levanta negros, NUEVO en v1.1)
Fresnel Strength .......... 0.15
Fog Start Distance ........ 2.0 m  (NUEVO: a partir de aquí hay niebla)
Fog Full Distance ......... 18.0 m (NUEVO: aquí satura la niebla)
Fog Height Bias ........... 0.3
Fog Strength .............. 0.55
Fog Light Dispersal ....... 2.0    (NUEVO: la luz despeja la niebla)
```

Colores clave (todos sepia/marrón):
- `_BaseColor`        = `#A89684`
- `_SeamDarkenColor`  = `#2A1A10`
- `_GrimeColor`       = `#1F1308`
- `_ShadowTint`       = `#3B2415`
- `_FresnelColor`     = `#5C4A38`
- `_FogColor`         = `#3D2818`

## 10. Performance tiers

| Tier | Triplanar | Grime | VolFog | DetailNoise | Coste rel. |
|---|---|---|---|---|---|
| Low (Steam Deck min) | OFF | OFF | OFF | OFF | 1.0× |
| **Mid (recomendado, default)** | OFF | ON | ON | ON | ~1.35× |
| High (PC) | ON | ON | ON | ON | ~2.1× |

## 11. Validaciones obligatorias antes de shipear cambios

1. **Test coplanar:** colocar 4 quads 2×2 coplanares en grid → no debe verse
   línea entre ellos. Si aparece, la implementación está rota.
2. **Test esquina:** dos quads en ángulo 90° → debe verse oscurecimiento +
   disolución por ruido en la junta.
3. **Test distancia:** caminar alejándose de un muro → costura debe
   desvanecerse entre 8–11 m.
4. **Test toggles:** activar/desactivar `_GRIME_ON`, `_VOLUMETRICFOG_ON`,
   `_DETAIL_NOISE_ON` por separado para verificar contribución de cada capa.
5. **Test SSAO:** Activar SSAO en URP Renderer → debe combinarse, no
   sobreescribirse.
6. **Test instancing:** material con `Enable GPU Instancing` ON → frame
   debugger debe mostrar batches instanciadas.

## 12. Trampas conocidas

### 🚨🚨🚨 ShaderLab `[Header(...)]` — SOLO LETRAS, DÍGITOS Y ESPACIOS 🚨🚨🚨

**Esta regla la he incumplido DOS veces ya (v1.3 y v2.2). Si vas a tocar
las Properties, RELEELO antes.**

El texto dentro de `[Header(...)]` SOLO admite letras, dígitos y espacios.
NUNCA uses guiones (`-`), igual (`=`), guiones bajos (`_`), comas,
paréntesis anidados ni ninguna otra puntuación. El parser de ShaderLab
falla con:
> `Parse error: syntax error, unexpected $undefined, expecting TVAL_ID or TVAL_VARREF`

Y el shader queda magenta sin pista clara de la causa (el `line N` del
error sí señala la línea del Header malo).

```
✅ [Header(Seam Darkening)]
✅ [Header(Volumetric Fog)]
✅ [Header(Surface Textures)]
❌ [Header(Seam Darkening - geometric edges)]    ← guion
❌ [Header(Toggles = OFF by default)]            ← igual
❌ [Header(Color Grade (optional))]              ← paréntesis
❌ [Header(Fog, Vignette and Grain)]             ← coma
❌ [Header(set Amount to 0 to disable)]          ← "0" en sí está bien,
                                                    pero los guiones no
```

**Antes de cualquier commit del shader, ejecuta:**
```
grep -nE "\[Header\([^)]*[^a-zA-Z0-9 )]" MainShader.shader
```
Si devuelve algo, hay un Header sucio que reventará el shader.
(El `)` extra en la clase de exclusión evita falso positivo al match del
paréntesis de cierre del propio `Header(...)`.)

### ⚠️ URP 17 — `_FORWARD_PLUS` está deprecado
URP 17.2 ha renombrado el keyword:
- ❌ Viejo: `#pragma multi_compile _ _FORWARD_PLUS`
- ✅ Nuevo: `#pragma multi_compile _ _CLUSTER_LIGHT_LOOP`

El viejo aún funciona pero da warning que ralentiza compilación.



- **No usar normal map en `fwidth`:** se romperían los muros lisos. El
  shader usa `wnG` (normal geométrica) explícitamente para esto.
- **Doble niebla:** si `_FogStrength` es alta a la vez que la niebla URP
  nativa, el fondo se aplasta. El `_EdgeVisibilityDistance` ya limita el
  efecto cerca del jugador.
- **Triplanar y normal maps:** el triplanar normal usa whiteout blend que
  puede dar resultados ligeramente diferentes al UV-mapped normal. Para
  hero assets, mejor UV.
- **Shader Graph manual:** si en algún momento se crea la versión SG, hay
  que asegurarse que el `Normal Vector` node esté en space **World** y NO
  use la versión perturbada.

## 13. Cómo extender el sistema (ideas futuras)

1. **`MainShader_Debug.shader`** — variante con keyword `_DEBUG_VIEW` que
   pinta directamente las máscaras en albedo para tuning.
2. **Wetness pass** — canal de humedad que oscurece + sube smoothness en
   zonas bajas (charcos en juntas). Reutilizar `FloorMask`.
3. **Vertex paint opcional** — usar el canal alpha del vertex color para
   reforzar costuras manualmente: `SeamMask = max(curvature, vertexAlpha)`.
4. **Height blending** — para transiciones suelo→muro con dos materiales.
5. **Flicker emisivo sincronizado** — `_GlobalFlickerTime` compartido entre
   antorchas para titilar coordinadas.
6. **POM/Parallax suave** en surfaces close-up tier High.
7. **GPU-procedural decals** — escribir huellas en mugre vía RenderTexture
   compartida.

## 14. Reglas de colaboración con el usuario (memoria)

- **Idioma:** responder en **español** aunque el prompt esté en inglés.
- **Tono:** el usuario es desarrollador del juego, no necesita
  explicaciones básicas de Unity. Sí valora la justificación técnica de
  decisiones (por qué `fwidth`, por qué triplanar opcional, etc.).
- **Estilo de entrega:** prefiere documentos exhaustivos con secciones
  numeradas, tablas y código real funcional. No vale "ejemplo simplificado".

## 15. Si te toca continuar el trabajo en una sesión futura

1. **Lee este archivo entero antes de tocar nada.**
2. **Verifica el estado real con `git status` y `ls`** — los archivos
   listados aquí pueden haber cambiado.
3. **No re-arquitectures sin discutirlo:** el diseño está validado por el
   usuario tras un test de 12 preguntas.
4. **No introduzcas dependencias nuevas** (third-party packages, Shader
   Graph extensions) sin preguntar.
5. **Si tocas `MainShader.shader`, mantén compatibilidad con todas las
   keywords URP listadas en la sección 6.**
6. **Mantén la nota crítica de paredes coplanares** (sección 3) — es un
   requisito explícito del usuario, no una sugerencia.

---

## 16. Changelog

### v3.2 — 2026-05-23 (Ambient Lift OFF by default + uses albedo, no gray wash)
**El usuario reportó (segunda captura) que aún se veía gris pálido en
zonas oscuras. Causa: el screen-blend con `_AmbientLiftTint = white`
seguía introduciendo color blanco en pixels negros → gris.**

**Doble fix:**

**1. Default OFF (`_AmbientLift = 0.0`):**
- El shader sale **completamente sin lift** por defecto.
- Las zonas oscuras se quedan oscuras como en URP/Lit puro.
- No hay wash gris a menos que el usuario lo active expresamente.

**2. Fórmula cambiada para usar ALBEDO en vez de blanco:**
```hlsl
// ANTES (v3.1): screen blend con color del tint
liftRGB = _AmbientLift * _AmbientLiftTint.rgb * liftFade;
color = 1 - (1-color) * (1-liftRGB);  // introduce gris/blanco

// AHORA (v3.2): max() vs albedo * tint * amount
liftCol = sd.albedo * _AmbientLiftTint.rgb * (_AmbientLift * liftFade);
color = max(color, liftCol);  // preserva identidad cromática
```

Esto hace que cuando el usuario active el lift (por ejemplo a 0.30):
- Una pared de ladrillo rojo (albedo = (0.5, 0.2, 0.15)) tendrá un floor
  en (0.15, 0.06, 0.045) — **rojo oscuro**, no gris.
- Una piedra clara (albedo = (0.6, 0.55, 0.5)) tendrá floor en
  (0.18, 0.165, 0.15) — **piedra apagada**, no gris.
- El tint blanco neutro deja el albedo pasar tal cual.
- Si el usuario quiere desaturar puede usar tint gris o sepia.

**Comportamiento esperado v3.2:**
- Default: shader puro URP/Lit + AO + dither sutil. **Sin lift, sin gris.**
- Si activas lift → ves color natural del material en zonas oscuras.
- Sigue desvaneciéndose con distancia (default 8m).

### v3.1 — 2026-05-23 (Ambient Lift: screen-blend, post-palette)
**El usuario reportó (con captura) que el lift via `max()` creaba un wash
gris/rosa plano en superficies oscuras, que combinado con dither + palette
quantization producía un grano artificial muy visible.**

**Fix triple:**

**1. Cambio de `max()` a screen blend:**
```hlsl
// ANTES (v3.0): hard floor que pintaba pixels oscuros con lift color
color.rgb = max(color.rgb, liftRGB);

// AHORA (v3.1): screen blend - soft, sin cutoff
color.rgb = 1 - (1 - color.rgb) * (1 - liftRGB);
```
Esto:
- Píxel oscuro (luma ~0) → recibe ~lift completo, visible.
- Píxel medio (luma 0.5) → recibe ~50% del lift.
- Píxel brillante (luma 1) → recibe 0 lift.
- **Suave, sin "suelo plano" gris.**

**2. Lift movido a final del pipeline (post-palette):**
```
PBR → ×Exposure → FakeAO → HighlightGranulate → Reinhard
    → Palette quant → MaxBright → AmbientLift (NUEVA posición) → Fog
```
Aplicar el lift DESPUÉS de la cuantización evita que el lift se cuantice
en pocos steps, lo que generaba el grano visible.

**3. Defaults más sutiles:**
| Property | v3.0 | v3.1 |
|---|---|---|
| `_AmbientLift` | 0.12 | **0.10** |
| `_DitherStrength` | 0.30 | **0.15** |
| `_HighlightDither` | 0.30 | **0.15** |
| `_PaletteSteps` | 32 | **48** (menos blocky) |

Esto reduce el grano de fondo. Si el usuario quiere mood más blocky
puede subir manualmente en el ShaderManager.

**Comportamiento esperado v3.1:**
- Zonas oscuras se levantan suavemente sin pintar gris.
- Texturas dark visible pero conservando carácter.
- Píxeles iluminados no cambian.
- Lift sigue desvaneciéndose con distancia (default 8m).

### v3.0 — 2026-05-23 (Ambient Lift: unlit pixels visibles cerca, negro lejos)
**El usuario reportó que las zonas no iluminadas seguían quedándose
completamente negras. Pidió un punto intermedio editable: visible cerca
para poder trabajar en scene view, negro solo a partir de cierta distancia
(~8m).**

**Nuevo: Ambient Lift (visibility floor)**
- `_AmbientLift` (Range 0-1, default 0.12): brillo mínimo que se aplica a
  zonas oscuras (PBR cerca de 0 o muy oscurecidas por Fake AO).
- `_AmbientLiftFadeDistance` (Float, default 8.0 m): a partir de esta
  distancia el lift llega a 0 → los pixels pueden ser negros puros.
- `_AmbientLiftTint` (Color, default white): tinta del lift. Cambia a
  marrón/azul oscuro si quieres color en las sombras.

**Cómo funciona:**
```hlsl
float liftFade = saturate(1.0 - camDist / _AmbientLiftFadeDistance);
float3 liftRGB = _AmbientLift * _AmbientLiftTint.rgb * liftFade;
color.rgb = max(color.rgb, liftRGB);
```

Usa `max()` per-channel:
- Pixel iluminado (luma > lift) → unchanged.
- Pixel oscuro cerca → lifted a `_AmbientLift` (visible gris).
- Pixel oscuro lejos (> fadeDistance) → lift = 0 → puede ser negro puro.

**Posición en pipeline:**
```
PBR → ×Exposure → FakeAO → AmbientLift (NEW)
    → HighlightGranulate → Reinhard → Palette → MaxBright → Fog
```

Aplicado DESPUÉS de Fake AO para que las zonas oscurecidas por AO también
se rescaten cerca; pero ANTES de palette quantization para que el lift
respete la cuantización.

**ShaderManager:**
- `ambientLift` (Range 0-1, default 0.12).
- `ambientLiftFadeDistance` (default 8.0 m, matchear viewMaxDistance si quieres
  que el lift se desvanezca antes del AO máximo).
- `ambientLiftTint` (Color, default white).
- Independiente de `masterVisibility` (no es un "efecto estilizado", es
  visibility helper).

**Recomendaciones de tuning:**
- Para scene view confortable: `ambientLift = 0.12-0.18`, `fadeDistance = 8`.
- Para gameplay horror puro: `ambientLift = 0.05`, `fadeDistance = 4`.
- Para tono cálido en sombras: `ambientLiftTint = (1, 0.85, 0.6)` (sepia).
- Para tono frío tipo Lethal: `ambientLiftTint = (0.7, 0.85, 1)` (azul).

### v2.9 — 2026-05-23 (visibility fix: AO usa normal geométrica + falloff power)
**El usuario reportó que en el viewport todo estaba demasiado oscuro,
incluso a corta distancia. Causa raíz triple:**

1. **Bug grave:** el AO usaba `ddx(normalWS)` — pero `normalWS` ya incluye
   la perturbación del normal map. Cada grieta de ladrillo, cada bump de
   piedra, cada detalle de textura registraba como "edge" → la superficie
   entera se oscurecía a fango.
2. **Distance ramp lineal:** con `_AONearStrength = 0.35`, AO ya aplicaba
   un 35% incluso pegado al jugador. Sumado al fix #1, scene irrespirable.
3. **Defaults muy agresivos** en palette saturation (0.80), steps (18),
   dither (0.55), shadow boost (2.4).

**Fixes:**

**A. AO usa `wnG` (geometric normal sin perturbar):**
```hlsl
// ANTES (mal):
float3 nDx = ddx(normalWS);  // incluye normal map → todo es edge
// AHORA:
float3 nDx = ddx(wnG);        // solo mesh corners reales
```
Resultado: los AO ahora SÓLO aparecen en uniones reales de geometría
(esquinas, juntas entre meshes), no en cada bump de textura.

**B. Nueva property `_AOFalloffPower` (default 2.5):**
```hlsl
distT = pow(distT, _AOFalloffPower);
```
Power curve sobre el ramp de distancia. Con power 2.5:
- A 25% del rango → AO al 4% (invisible).
- A 50% del rango → AO al 18%.
- A 75% del rango → AO al 47%.
- A 100% del rango → AO al 100%.

Esto cumple el requisito del usuario: "que solo se vea muy oscuro con
la distancia máxima establecida".

**C. Defaults muy permisivos (mejor visibilidad por defecto):**
| Property | v2.8 | v2.9 |
|---|---|---|
| `_FakeAOStrength` | 1.6 | **1.0** |
| `_FakeAONormalSensitivity` | 4.0 | **2.5** |
| `_FakeAODepthSensitivity` | 2.5 | **1.0** |
| `_AONearStrength` | 0.35 | **0.0** ← cero AO cerca |
| `_AOFarStrength` | 1.5 | **1.0** |
| `_AOFalloffPower` | — | **2.5** (NUEVO) |
| `_ShadowAOBoost` | 2.4 | **1.5** |
| `_PaletteSaturation` | 0.80 | **1.00** ← sin desaturación default |
| `_PaletteSteps` | 18 | **32** ← más colores |
| `_DitherStrength` | 0.55 | **0.30** |
| `_HighlightDither` | 0.55 | **0.30** |
| `_MaxBrightness` | 4.0 | **8.0** ← cap permisivo |

**ShaderManager.cs:**
- `aoFalloffPower` añadido (default 2.5).
- Todos los defaults sincronizados con los del shader.
- Tooltips clarifican qué hace cada slider.

**Comportamiento esperado en v2.9:**
- En el viewport, paseando cerca de muros: **color limpio, visible**.
- A media distancia: AO empieza a notarse en esquinas reales.
- Cerca del max distance: AO fuerte, look horror.
- Las texturas NO se ven oscurecidas por su propio detalle.

### v2.8 — 2026-05-23 (Burn Control + Master Visibility)
**El usuario pidió control de quemado en el shader y maestro de visibilidad
en el ShaderManager para escalar todo de golpe.**

**Shader — nuevas propiedades:**
- `_Exposure` (Range 0-3, default 1.0): multiplicador linear post-PBR
  aplicado antes de todos los efectos downstream. Afecta también a
  emission. <1 dim, >1 brighten.
- `_MaxBrightness` (Range 0.5-8, default 4.0): techo hard per-channel al
  final del pipeline. Combinado con `_HighlightSoftness` (Reinhard), da
  soft rolloff + hard cap. Sube para permitir más burn, baja para
  aplastar highlights.

**Posición en el pipeline:**
```
PBR → ×Exposure → FakeAO → Highlight Granulate → Reinhard
    → Palette quant → MIN(color, MaxBrightness) → Fog
```

**ShaderManager — Master Controls:**
- `masterVisibility` (0-1): multiplicador global que escala
  `fakeAOStrength`, `ditherStrength`, `highlightDither`, `stylizedAOStrength`,
  y lerps `paletteSteps`→64 + `paletteSaturation`→1 hacia "sin efecto".
  En 0 = el shader se comporta como URP/Lit puro.
- Toggles individuales: `fakeAOEnabled`, `ditherEnabled`, `paletteEnabled`,
  `highlightGranulateEnabled`, `stylizedTriplanarEnabled`. Cada uno permite
  desactivar una capa específica sin perder sus parámetros guardados.
- Botones nuevos en inspector: **Pure URP (0)**, **Subtle (0.4)**, **Full (1.0)**
  para alternar visibilidad de un click.
- `exposure` y `maxBrightness` mirrored desde el shader.

**Implementación clave:**
```csharp
float effFakeAO = fakeAOEnabled ? fakeAOStrength * masterVisibility : 0f;
// Palette es diferente porque "off" no es 0 sino 64 steps + sat 1.
float effPaletteSteps = paletteEnabled ? Mathf.Lerp(64, paletteSteps, v) : 64;
float effPaletteSat   = paletteEnabled ? Mathf.Lerp(1, paletteSaturation, v) : 1;
```

### v2.7 — 2026-05-23 (Screen-Space Fake AO + Dither + PSX palette)
**El usuario reportó que el AO no se veía y mostró referencias visuales:**
Buckshot Roulette, Lethal Company, R.E.P.O. — look low-fi con dithering,
paleta reducida, sombras marcadas en uniones de mallas, halo de luz que se
oscurece con la distancia.

**Reemplazado por completo el sistema de AO. Borrado Scene Extremes AO.**

**Nuevo: Screen-Space Fake AO**
- **Detección geométrica vía derivadas de pantalla:**
  - `ddx/ddy(normalWS)` → detecta cambios bruscos de normal (corners, uniones).
  - `ddx/ddy(camDist)` → detecta saltos de profundidad (silhouettes entre
    meshes distintos, donde la geometría se separa).
  - Combinados, falsean AO en TODAS las uniones — incluyendo entre objetos
    separados, lo que pedía el usuario.
- **Distance ramp:** define `_ViewMinDistance` y `_ViewMaxDistance`. Cerca
  de min = AO casi invisible, cerca de max = AO al máximo. Halo de luz
  estilo Lethal Company.
- **Shadow accumulation:** detecta luma baja → multiplica AO. Las sombras
  se vuelven MÁS oscuras que las zonas iluminadas, dramatizando el contraste.
- Aplica DESPUÉS de PBR para tener acceso a luma iluminada.
- Preserva emission (no se ataca).

**Nuevo: Bayer 8x8 Dithering**
- Matriz Bayer 8x8 ordered en screen-space.
- Modula tanto el AO como las quantizaciones de palette.
- `_DitherScale` (1-8) controla "blockiness":
  - 1 = per-pixel (clásico PSX),
  - 4-8 = bloques grandes tipo Buckshot Roulette.

**Nuevo: PSX Palette Quantization**
- `_PaletteSteps` (2-64, default 18) → niveles por canal RGB.
- `_PaletteSaturation` (0-1.5, default 0.8) → reduce vibración.
- Combinado con dither → gradientes suaves pese a poca paleta.

**Nuevo: Highlight Granulation**
- Píxeles con luma > `_HighlightThreshold` reciben dither extra.
- Granula los extremos de luz tipo lámparas/antorchas (referencia Buckshot).

**Properties shader v2.7:**
```
[Screen Space Fake AO]
_FakeAOStrength            1.6   (0-3)
_FakeAONormalSensitivity   4.0   (0-10)  cuánto pesa normal change
_FakeAODepthSensitivity    2.5   (0-10)  cuánto pesa depth jump
_FakeAOContrast            1.8   (0.1-4)
_FakeAOTint                #000000        color de oscurecimiento

[Distance Driven Intensity]
_ViewMinDistance           2.0 m
_ViewMaxDistance           18.0 m
_AONearStrength            0.35  (multiplier cerca)
_AOFarStrength             1.5   (multiplier lejos)

[Shadow Accumulation]
_ShadowAOBoost             2.4   (1-5)  boost en zonas oscuras
_ShadowAOThreshold         0.45  (luma cutoff)

[Dither + PSX Palette]
_DitherStrength            0.55
_DitherScale               1.0   (1-8 blockiness)
_HighlightDither           0.55  (granula highlights)
_HighlightThreshold        0.75  (luma cutoff)
_PaletteSteps              18    (per channel)
_PaletteSaturation         0.80

[Highlight Softness]
_HighlightSoftness         0.18  (Reinhard rolloff)
```

**ShaderManager.cs actualizado:**
- Quitados: aoExtremesStrength, aoStartDistance, aoEndDistance.
- Añadidos: fakeAOStrength, fakeAONormalSensitivity, fakeAODepthSensitivity,
  viewMinDistance, viewMaxDistance, aoNearStrength, aoFarStrength,
  shadowAOBoost, shadowAOThreshold, ditherStrength, ditherScale,
  highlightDither, highlightThreshold, paletteSteps, paletteSaturation.

**Pipeline post-PBR (resumen del flow):**
1. UniversalFragmentPBR → color iluminado.
2. Compute Fake AO (ddx normales + ddx depth).
3. Multiply AO * distance ramp * shadow boost.
4. Bayer dither el AO.
5. Lerp color → _FakeAOTint con peso AO.
6. Highlight granulation (dither extra en píxeles brillantes).
7. Reinhard highlight softness.
8. Palette quantization (saturación reducida + quantización per-channel
   con dither).
9. MixFog URP nativa.

### v2.6 — 2026-05-22 (recalibración: Scene Extremes AO + anti-burn)
**El usuario reportó dos problemas:**
1. La aproximación Edge AO fresnel "quemaba la vista" (tiñía silhouettes en sepia).
2. Quería que las sombras/oscurecimiento respondieran a los "extremos del escenario", no a silhouettes locales.

**Reemplazado el sistema de 3 capas (Edge AO + Downward Bias + Stylized AO tinted):**

**Quitado:**
- `_EdgeAOStrength`, `_EdgeAOPower` (fresnel silhouette darkening).
- `_DownwardBias` (downward-facing surface darkening).
- `_StylizedAOTint` color sepia (eliminaba el filtro marrón).

**Añadido:**
- **Scene Extremes AO** (distance-based):
  - Oscurece según distancia desde cámara (produce halo de luz tipo Lethal Company).
  - `_AOStartDistance` (4 m) → `_AOEndDistance` (14 m): rango de fade.
  - `_AOExtremesStrength` (0.45): intensidad máxima al final.
  - Multiplica `sd.albedo` e `inputData.bakedGI` (afecta directo + indirecto, NOT emission).
  - Neutral en luminancia (sin teñir).
  
- **Highlight Softness** (Reinhard rolloff):
  - Anti-burn: `color = color / (1 + color * softness)` aplicado solo al lighting.
  - `_HighlightSoftness` (0.15 default): comprime highlights sin matar contraste.
  - Emission preservada intacta.

**Stylized Triplanar AO:**
- Ahora oscurece sin tint: `albedo *= lerp(1, 1-ao, strength)`.
- Sigue siendo world-space triplanar, sin UV stretch.
- Default OFF (`_StylizedAOStrength = 0`).

**Resultado visual:**
- Ningún filtro rosa/sepia por defecto.
- Superficies legibles y coloreadas cerca.
- Oscurecimiento progresivo en los extremos → atmósfera oppresiva sin quema.
- Highlights suave rolloff.

**ShaderManager.cs:**
- Quitados sliders: `edgeAOStrength`, `downwardBias`.
- Añadidos: `aoExtremesStrength`, `aoStartDistance`, `aoEndDistance`, `highlightSoftness`.
- Auto-fill + apply to all mantiene funcionalidad.

### v2.4 — 2026-05-21 (pivote a fake AO estilizado)
**Tras varios intentos, la detección geométrica de costuras (fwidth de
normal o face normal) no producía nada visible en las mallas modulares
del usuario.** Probable causa: smoothing groups suaves + viewing
angles + Unity material refresh issues. Pivotamos completamente.

**Reemplazado el seam darkening por un sistema de FAKE AO ESTILIZADO**
inspirado en Inscryption / Lethal Company / Buckshot Roulette. No
depende de la geometría — funciona en cualquier malla.

**Tres capas de oscurecimiento, todas controlables:**

1. **Triplanar AO overlay (textura)**
   - Sample triplanar world-space del `_StylizedAOMap`.
   - Píxel brillante en la textura = más oscurecido en el final.
   - Funciona con cualquier mesh, sin UV stretching.
   - Default texture: `"black"` → no contribuye hasta que el usuario
     asigne `T_GrimeAtlas_01` (o cualquier grunge tileable).
   - Propiedades: Strength (0.7), Contrast (1.2), Scale (0.35), Tint.

2. **Edge AO (estilo Inscryption / Buckshot)**
   - Fresnel-style: las silhouettes (ángulos de visión rasantes) se
     tiñen de oscuro. Da ese feel "card depth" pintado.
   - Propiedades: Strength (0.45), Power (3.5).

3. **Downward Bias**
   - Caras orientadas hacia abajo (techos, undersides) reciben menos
     ambient bounce → más oscuras. Sutil moodiness.
   - Propiedad: Bias (0.20).

**Quitadas todas las propiedades de seam darkening**
(`_SeamIntensity`, `_SeamWidthPower`, `_SeamDarkenAmount`, etc.).

**Cómo usarlo:**
1. Refrescar Unity.
2. En el material asignar `T_GrimeAtlas_01` al slot `AO Overlay Map`.
   (Si no la tienes generada: `Tools > MainShader > Generate Grime Texture`.)
3. Ajustar `AO Overlay Strength` al gusto (sube a 1.0 para super sucio).

### v2.3 — 2026-05-21 (REINCIDENCIA Header + FORWARD_PLUS deprecation)
**Magenta otra vez. Causa: REINCIDÍ en el bug del Header con guiones.**
Una iteración después de documentar "nunca caracteres especiales en Header",
añadí `[Header(Seam Darkening - set Amount to 0 to disable)]`. Eliminado.

También arreglado warning de URP 17:
- `_FORWARD_PLUS` → `_CLUSTER_LIGHT_LOOP` (el keyword fue renombrado).

La sección "Trampas conocidas" del README está reforzada con asteriscos
y un comando grep para validar antes de commit.

### v2.2 — 2026-05-21 (seam sin keyword + Volume profile horror)
**Dos problemas reportados:**
1. Seam Darkening seguía sin verse aunque el toggle estuviera ON.
2. Sin Global Volume (post-process URP) la escena quedaba "muy mal".

**Fix 1 — Seam siempre activo, sin keyword:**
El `[Toggle(_SEAM_ON)]` + `#pragma shader_feature_local` + `#ifdef` introducía
problemas de refresco del material o variantes sin compilar. Eliminado el
keyword por completo. El bloque de seam ahora se ejecuta SIEMPRE y se
controla 100% por `Seam Darken Amount`:
- `Amount = 0` → desactivado.
- `Amount > 0` → visible.

Defaults subidos a `_SeamIntensity = 18`, `_SeamWidthPower = 0.30`,
`_SeamFadeDistance = 25 m` para que sea muy visible por defecto.

**Fix 2 — Editor script `CreateHorrorVolume.cs`:**
Nueva utilidad en `Tools > MainShader > Create Horror Global Volume`.
De un click crea/actualiza:
- `Assets/Shader/MainShader/Volume/VP_Horror_Default.asset` (VolumeProfile)
- Un GameObject `GlobalVolume_Horror` con componente Volume global

El perfil viene configurado con:
- Tonemapping Neutral
- Bloom warm (tint #FFDBB3, intensity 0.35, threshold 0.9, scatter 0.75)
- Vignette negro (intensity 0.35, smoothness 0.42)
- Color Adjustments (contrast +12, saturation -8, filter cálido)
- White Balance (temperature +8, tint -4 — cálido sutil)
- Shadows/Midtones/Highlights (warm tint)
- Film Grain Thin1 (intensity 0.18)

**Importante post-creación:**
- En la **Camera**: marcar `Rendering > Post Processing`.
- En el **URP Renderer Asset**: confirmar que `Post-processing` esté ON.

Sin esos dos checks el perfil no se aplica.

### v2.1 — 2026-05-21 (seam visible de verdad)
**El usuario reportó que con `Enable Seam Darkening` ON y los valores ajustados
no veía absolutamente nada. Diagnóstico:**

1. **`fwidth(interpolatedNormal)` = 0 en mallas smooth-shaded.**
   Los FBX modulares típicos vienen importados con smoothing groups que
   suavizan TODAS las normales, haciendo que la normal interpolada del
   vértice sea continua → `fwidth` no detecta nada. El usuario podía
   tener costuras geométricas reales (esquinas de 90°) que simplemente
   eran invisibles para esta detección.

2. **Los defaults eran demasiado tímidos** incluso si la detección
   funcionaba: `_SeamIntensity = 3` daba una franja de 1 pixel apenas
   perceptible.

**Fix:**

- Cambio del signal a **face normal derivada de la posición**:
  ```hlsl
  float3 faceN = normalize(cross(ddy(wpos), ddx(wpos)));
  float curv = length(fwidth(faceN)) * _SeamIntensity;
  ```
  Esto deriva la normal geométrica real desde las derivadas de posición
  en pantalla, **independiente del smoothing del FBX**. Dos quads
  coplanares siguen dando 0 (face normal idéntica), pero esquinas reales
  se detectan siempre, sin importar cómo esté shadeada la malla.

- **Nueva propiedad `_SeamWidthPower`** (default 0.35). Aplica `pow(curv, p)`
  para ensanchar visualmente la franja de 1 pixel a un halo perceptible.
  Valores bajos (0.1-0.3) = franja gruesa, valores altos (1-2) = franja fina.

- **Defaults subidos para que se VEA:**
  - `_SeamIntensity` 3.0 → **15.0** (rango 0-50)
  - `_SeamDarkenAmount` 0.25 → **0.85**
  - `_SeamFadeDistance` 8 m → **20 m**
  - `_SeamWidthPower` nuevo, **0.35**

**Trampa documentada:** si en el futuro alguien añade detección por
normal interpolada, recordar que NO funciona en mallas smooth-shaded.
Siempre derivar la face normal desde `cross(ddy(wpos), ddx(wpos))` para
ser robusto.

### v2.0 — 2026-05-21 (RESET — minimal rewrite)
**El usuario reportó magenta completo (fallback error de Unity) tras v1.3.
El shader v1.x se había vuelto demasiado complejo (6 keywords, CBUFFER con
40 campos, include externo, 5 passes con muchas funciones helper). Algo
no compilaba en URP 17.2 y debuggear a ciegas era inviable.**

Decisión: **reescritura completa, mínima y auto-contenida.**

**Lo que QUEDA en v2.0:**
- ForwardLit + ShadowCaster + DepthOnly + DepthNormals passes.
- Standard URP/Lit behavior (BaseMap, NormalMap, Metallic, Smoothness,
  Occlusion, Emission, lightmaps, sombras, additional lights, SSAO, fog).
- Forward+ keywords completos (incluye `_LIGHT_LAYERS`, `_LIGHT_COOKIES`,
  `USE_LEGACY_LIGHTMAPS`, `LOD_FADE_CROSSFADE` que faltaban en v1.x).
- **Una sola feature custom:** seam darkening al ángulo (`_SEAM_ON` toggle,
  ON por defecto). Usa `fwidth(worldNormal)` → coplanar-safe.
- Inspector simplificado a ~12 propiedades.

**Lo que SE QUITÓ (puede volver más tarde):**
- Color grade
- Volumetric fog
- Grime accumulation
- Fresnel
- Triplanar sampling
- Detail noise dissolution
- Meta pass (lightmap baking — añadir si hace falta rebake)
- Include externo `MainShader_Includes.hlsl`

**Archivos que quedan pero ya NO se usan en v2.0:**
- `MainShader_Includes.hlsl` (obsoleto, conservar como referencia para
  re-añadir features)
- `CustomFunctions/*.hlsl` (siguen siendo válidos para Shader Graph)

**Inspector v2.0:**
```
Base Map, Base Color, Normal Map, Normal Strength,
Metallic, Smoothness, Occlusion Strength, Occlusion Map,
Emission Color, Emission Map

[Seam Darkening]
Enable Seam Darkening (toggle) ........ ON
Seam Intensity ........................ 3.0
Seam Darken Amount .................... 0.25
Seam Darken Color ..................... black
Seam Fade Distance (m) ................ 8.0
```

**Filosofía v2.0:** "compilar primero, decorar después." Cuando el usuario
confirme que v2.0 carga sin magenta, podemos re-añadir features UNA por
UNA en versiones siguientes, validando cada paso.

### v1.3 — 2026-05-21 (REVERT)
**El usuario reportó "filtro rosado" persistente y pérdida total de
legibilidad. Decisión: revertir todos los efectos de pantalla por defecto.**

El shader por defecto ahora es **URP/Lit normal + detección de costura
sutil**. Todo lo demás es opt-in vía toggle.

| Toggle | v1.2 default | **v1.3 default** |
|---|---|---|
| Triplanar Sampling | OFF | OFF |
| Grime | **ON** | **OFF** |
| Volumetric Fog | **ON** | **OFF** |
| Color Grade (nuevo) | (no existía) | **OFF** |
| Detail Noise (seam dissolve) | ON | ON |
| Fresnel | ON | **OFF** |

**Nuevo keyword `_COLORGRADE_ON`.** Antes el color grade se aplicaba siempre.
Ahora está envuelto en `#ifdef _COLORGRADE_ON` y por defecto **no se ejecuta**
— el PBR sale tal cual de `UniversalFragmentPBR`.

**Defaults pacificados** (por si el usuario activa los toggles):
- `_ShadowIntensity` 0.12 → 0.0
- `_Saturation` 0.95 → 1.0
- `_AmbientLift` 0.05 → 0.0
- `_FogStrength` 0.35 → 0.0
- `_FogStartDistance` 3 m → 5 m
- `_FogFullDistance` 22 m → 30 m
- `_SeamDarkenAmount` 0.35 → 0.20
- `_SeamDarkenColor` marrón medio → casi negro neutro (`#1A1410`)
- `_SeamDissolveAmount` 0.25 → 0.15

**Filosofía v1.3:** el shader es invisible por defecto excepto en costuras
reales. El artista activa explícitamente cada capa (color grade, niebla,
grime) en los materiales donde la quiera.

### v1.2 — 2026-05-21
**Fixes tras segundo test (filtro rosado + ruido visible en suelos):**

1. **Niebla volumétrica ya no proyecta "cells" en el suelo.**
   El sample del `_NoiseTexture` modulaba la niebla en un 30 % (`* 0.3 + 0.7`),
   lo que con la textura Worley creaba arcos/células visibles en las zonas
   de niebla parcial. Reducido a **8 % (`* 0.08 + 0.92`)** y el sampling
   pasa de `xz * 0.1` (tile 10 m) a `xz * 0.03` (tile ~33 m) → patches
   mucho más grandes y suaves.

2. **Grime ya no contamina superficies coplanares limpias.**
   - Baseline cuando `seamSoft = 0`: **0.30 → 0.08** (de 30 % a 8 %).
   - El sample de grime ahora pasa por threshold `(g - 0.45) / 0.55` y luego
     `g²` → solo los blobs más oscuros del mapa producen mugre real,
     el resto se queda limpio.
   - `_GrimeIntensity` 0.55 → **0.30**, `_FloorGrimeBoost` 1.3 → **1.0**,
     `_WallGrimeBoost` 1.0 → **0.8**, `_GrimeScale` 1.0 → **0.6** (patches
     más grandes, menos repetición visible).

3. **Filtro rosado eliminado.**
   - `_FogColor` `#3D2818` (marrón rojizo) → **`#4A4138`** (gris-marrón
     neutro). Ya no contamina las luces rojas/cálidas de antorchas.
   - `_ShadowTint` `#3B2415` (sepia rojizo) → **`#3B3228`** (sepia neutro).
   - `_ShadowIntensity` 0.25 → **0.12** (mitad de tinte).
   - `_FogStrength` 0.55 → **0.35**, `_FogStartDistance` 2 m → **3 m**,
     `_FogFullDistance` 18 m → **22 m** (la niebla aparece más tarde y
     menos intensa).
   - `_FogLightDispersal` 2.0 → **2.5** (luz despeja más).

4. **Costuras más selectivas.**
   - `_SeamThreshold` 0.15 → **0.25**: pequeños cambios de normal por
     interpolación entre quads no disparan máscara.
   - `_SeamDarkenAmount` 0.45 → **0.35**.
   - `_SeamDissolveAmount` 0.35 → **0.25**.
   - `_NoiseContrast` 0.65 → **0.35**, `_NoiseBreakup` 0.50 → **0.30**.
   - `_EdgeVisibilityDistance` 8 m → **6 m** (efecto solo en el muy cerca).

**Regla mnemotécnica del nuevo balance:** *clean by default, dirty by choice*.
Los defaults producen un look limpio y legible — el artista sube los valores
de grime/seam/noise donde quiera ensuciar.

### v1.1 — 2026-05-21
**Fixes tras primer test en escena real (catacumba con antorchas):**

1. **Niebla invertida arreglada.**
   Antes: la niebla era más densa cerca de la cámara (bug — `proximity = 1 - d/r`).
   Ahora: 0 niebla cerca, ramps up entre `_FogStartDistance` (2 m) y
   `_FogFullDistance` (18 m). Se eliminaron `_FogDensityRadius`.

2. **Light dispersal añadido.**
   Nueva propiedad `_FogLightDispersal` (default 2.0). Las superficies
   iluminadas (antorchas, luces, sun) **abren claros** en la niebla porque
   la dispersión se calcula desde la luminancia del píxel ya iluminado:
   `dispersion = saturate(luma * _FogLightDispersal)` y
   `fog *= (1 - dispersion)`.

3. **Color grade aligerado.**
   El default v1.0 aplastaba todo lo no-iluminado a negro:
   - `_ShadowIntensity` 0.50 → **0.25**
   - `_Saturation` 0.80 → **0.95**
   - `_Contrast` 1.12 → **1.0**
   - `_AmbientDarkness` 0.30 → **0.0**
   - Umbral de shadow tint movido: ahora solo afecta luma < 0.35 (antes 0.5),
     preservando mid-tones.

4. **`_AmbientLift` añadido (default 0.06).**
   Levanta los negros profundos con la fórmula gamma `c + lift * (1 - c)`,
   asegurando que las zonas sin luz directa **siguen siendo legibles**
   sin contaminar las luces.

**API cambiada (afecta a `CF_VolumetricFog.hlsl` y `CF_ColorGrade.hlsl`):**
- `VolumetricFog_float` ahora toma `LitColor`, `StartDistance`, `FullDistance`,
  `LightDispersal` (y ya no toma `DensityRadius`).
- `ColorGrade_float` toma un parámetro extra `AmbientLift`.

### v1.0 — 2026-05-21
Creación inicial de `MainShader` con 9 Custom Functions, 2 generadores de
textura, documentación y README maestro.

---

_Última actualización: 2026-05-23 v3.2 — Ambient Lift OFF por defecto + usa albedo (no más gris)._

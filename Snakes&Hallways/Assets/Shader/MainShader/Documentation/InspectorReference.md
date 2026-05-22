# MainShader — Inspector Reference

## Surface Textures
| Propiedad | Default | Función |
|---|---|---|
| Base Map | white | Albedo principal |
| Base Color | `#A89684` | Tinte de albedo, piedra cálida |
| Normal Map | bump | Detalle normal |
| Normal Strength | 1.0 | Intensidad del normal |
| Metallic Map | white | R=Metallic, A=Smoothness |
| Metallic | 0.0 | Multiplicador metallic |
| Smoothness | 0.35 | Multiplicador smoothness (piedra húmeda) |
| Occlusion Map | white | Canal G = AO |
| Occlusion Strength | 1.0 | Amplificación de AO |
| Emission Map | black | Para antorchas, runas, etc. |
| Emission Color (HDR) | black | Color emisivo |
| Triplanar Scale | 0.5 | Frecuencia del triplanar (1/m) |

## Seam Edge Detection
| Propiedad | Default | Función |
|---|---|---|
| Edge Intensity | 4.0 | Cuánto amplifica el fwidth |
| Edge Width | 1.5 | Exponente, controla dureza |
| Blend Softness | 0.6 | Suavidad de la transición |
| Seam Threshold | 0.15 | Corte mínimo para que la costura aparezca |
| Modular Seam Influence | 1.0 | Multiplicador global de la máscara |
| Edge Visibility Distance | 8.0 m | Distancia máxima donde se ve el efecto |
| Edge Fade Range | 3.0 m | Anchura del fade |

## Seam Treatment
| Propiedad | Default | Función |
|---|---|---|
| Seam Darken Amount | 0.45 | Cuánto oscurece la junta |
| Seam Darken Color | `#2A1A10` | Color de la sombra de junta (marrón muy oscuro) |
| Seam Dissolve Amount | 0.35 | Cuánto rompe la silueta el ruido |

## Noise
| Propiedad | Default | Función |
|---|---|---|
| Noise Texture | gray | Worley/Voronoi tileable |
| Noise Scale (world) | 3.5 | Frecuencia mundo del ruido |
| Noise Contrast | 0.65 | Visibilidad del ruido (0.4=sutil, 1.0=brutal) |
| Noise Breakup | 0.50 | Mezcla entre 1 y el ruido para disolver |

## Grime
| Propiedad | Default | Función |
|---|---|---|
| Grime Map | gray | Capa de mugre tileable |
| Grime Color | `#1F1308` | Color de la mugre (hollín de antorcha) |
| Grime Intensity | 0.55 | Cuánta mugre |
| Grime Directionality | 0.70 | 0=uniforme, 1=solo abajo (escurrido gravedad) |
| Floor Grime Boost | 1.3 | Multiplicador en suelos |
| Wall Grime Boost | 1.0 | Multiplicador en muros |
| Grime Scale (world) | 1.0 | Frecuencia mundo |

## Color Grade
| Propiedad | Default | Función |
|---|---|---|
| Shadow Tint | `#3B2415` | Tinte sepia para sombras profundas (luma < 0.35) |
| Shadow Intensity | 0.25 | Cuánto te tiñe las sombras |
| Saturation | 0.95 | Desaturación global (1.0 = neutro) |
| Contrast | 1.0 | Contraste (1.0 = neutro) |
| Ambient Darkness | 0.0 | Multiplicador de oscuridad global (0 = no oscurece) |
| Ambient Lift | 0.06 | Levanta los negros profundos — sube para más visibilidad |

## Fresnel
| Propiedad | Default | Función |
|---|---|---|
| Fresnel Strength | 0.15 | Intensidad del rim (sutil) |
| Fresnel Falloff | 4.0 | Exponente, mayor=más fino |
| Fresnel Color | `#5C4A38` | Color del rim (musgo iluminado) |

## Volumetric Fog
| Propiedad | Default | Función |
|---|---|---|
| Fog Color | `#3D2818` | Niebla cálida polvorienta |
| Fog Start Distance (m) | 2.0 | Distancia desde la cámara donde EMPIEZA a aparecer niebla (no hay niebla más cerca) |
| Fog Full Distance (m) | 18.0 | Distancia donde la niebla alcanza intensidad máxima |
| Fog Height Bias | 0.3 | >0 = más denso abajo (niebla rastrera) |
| Fog Strength | 0.55 | Mezcla final |
| Light Dispels Fog | 2.0 | Cuánto despeja la niebla la luz (antorchas abren claros). 0 = no despeja, 5 = despeja agresivo |

## Feature Toggles (keywords)
| Toggle | Default | Coste |
|---|---|---|
| Triplanar Sampling | OFF | +6 samples |
| Grime | ON | +3 samples |
| Volumetric Fog | ON | +1 sample |
| Detail Noise | ON | +3 samples |
| Fresnel | ON | ALU only |

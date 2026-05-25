# Snakes & Hallways — Gameplay v0.8 (2026-05-25)

## Cambios principales

### 1. GameManager — Pickups con obligatorio

**Nuevo:**
- Slot `Mandatory Pickup` en Inspector (Transform/Pickup).
- Lista persistente `AllScenePickups` expuesta como propiedad.
- `SetupScenePickups()` mantiene el mandatorio **siempre activo** y fuera del shuffle.
- Apaga aleatoriamente el resto hasta matchear `PickupsRequired` de la dificultad.

**Setup:**
```
GameManager Inspector
├── Mandatory Pickup → arrastra el Pickup obligatorio aquí
```

**Comportamiento:**
- Si el mandatorio está asignado y en la escena, queda ON + cuenta en `PickupsRequired`.
- Si no existe la escena lo advierta con warning.
- Los demás se shufflan: primeros N se encienden, resto OFF.

---

### 2. WinCollider — Portal apagado hasta completar

**Nuevo:**
- Slot `Portal Visual` (GameObject root con mesh/VFX).
- Slot `Portal Loop Source` (AudioSource para `PortalIdle` loop).
- Portal arranca **desactivo** (GameObject + Collider).
- Se activa automáticamente cuando `PickupsCollected >= PickupsRequired`.
- Al activarse: `PlaySFX(PortalActivate)` + `StartLoop(PortalIdle)`.
- Al cruzar: `PlaySFX(PortalCross)` + `TriggerWin()` + fade a `SCN_EndScene`.

**Setup:**
```
WinCollider GameObject
├── Collider (Is Trigger = ON)
├── Portal Visual → arrastra aquí el root del mesh/VFX del portal
├── WinCollider script
│   ├── Portal Loop Source → crea auto-AudioSource si está vacío
│   ├── Portal Idle Fade In = 0.8s
│   ├── Portal Idle Volume = 0.8
│   └── Require All Pickups = ON (default)
```

**Sonidos ya mapeados:**
- `SFXId.PortalActivate` (cuando se completan pickups).
- `SFXId.PortalIdle` (loop ambiente).
- `SFXId.PortalCross` (al cruzar).

---

### 3. EnemyAIBase — 5 mejoras profesionales

#### a) Respawn forzado a altura Y=7.15

**Campo nuevo:** `forcedRespawnY = 7.15f` (altura del navmesh superior).

**Cómo:**
- Samplea XZ alrededor del player centrado a Y=7.15.
- Escoge el punto del NavMesh más cercano a `forcedRespawnDistance`.
- El `Warp` queda fijado a `(hit.x, 7.15, hit.z)` — siempre cae en la pasarela.
- La frecuencia sigue escalando por dificultad + pickups vía `DifficultyManager.GetRuntimeSettings()`.

**Campos relacionados:**
```
forcedRespawnY = 7.15
forcedRespawnVerticalTolerance = 2m (rango vertical al samplear NavMesh)
noSightRespawnSeconds = escalado por dificultad
farFromPlayerDistance = escalado por dificultad
forcedRespawnCooldown = 8s (entre respawns)
```

#### b) Lead targeting (predicción de movimiento)

**Campos nuevos:**
```
chaseLeadTime = 0.35s
chaseMaxLeadDistance = 4m
```

**Cómo:**
- Durante `Chase`, estima `playerVelocity` frame a frame.
- Apunta a `player.position + playerVelocity * chaseLeadTime`.
- Solo si hay LoS (no predice si ya lo perdió).
- Clamp a `chaseMaxLeadDistance` para evitar sobre-anticipación.

**Resultado:** El minotauro anticipa giros en vez de seguir ciegamente al pie.

#### c) Espiral de búsqueda tras perder LoS

**Campos nuevos:**
```
spiralSearchPoints = 4 (waypoints)
spiralMinRadius = 3m
spiralMaxRadius = 9m
```

**Cómo:**
- Al expirar `chaseMemoryTimer`, en vez de ir directo al último punto conocido, genera una espiral.
- Distribución con ángulo dorado (137.5°) para evitar simetrías.
- Visita cada waypoint rápido (0.4s dwell) y pasa al siguiente.
- Si ninguno es válido cae back al comportamiento legacy (ir directo al last known).

**Resultado:** Búsqueda más orgánica tipo *Alien Isolation*.

#### d) Aceleración suave de velocidad

**Campo nuevo:**
```
speedAccel = 6 m/s² (aceleración)
```

**Cómo:**
- En `SetState`, asigna `targetSpeed` (no directo a `Agent.speed`).
- `TickSpeedSmoothing()` cada frame: `Agent.speed = MoveTowards(speed, targetSpeed, accel * dt)`.
- Si `speedAccel = 0` salta instantáneo (legacy).

**Resultado:** Transiciones patrol ↔ chase suaves, con peso/inercia visual.

#### e) Stalking proximity audio

**Campos nuevos:**
```
stalkAudioDistance = 8m
stalkAudioMaxBoost = 2.2x
```

**Cómo:**
- Si el breath está activo y el jugador está a <8m **sin LoS**:
  - Modula el volumen del `MinotaurIdleBreath` hasta 2.2× el volumen base.
  - Suavizado exponencial para evitar saltos audibles.
- Si el jugador SÍ tiene LoS, breath al volumen base (no oculto).

**Resultado:** Horror inmersivo de "sé que está cerca pero no lo veo".

---

## Setup final en Editor

### GameManager
```
[GameObject] GameManager
├── GameManager script
│   ├── Timer
│   │   ├── Match Time = 600s (fallback si no hay DifficultyManager)
│   │   └── Current Time (serialized, editable en runtime)
│   ├── Refs
│   │   └── Player = auto-resuelve con tag "Player"
│   ├── Pickups ← NUEVO
│   │   └── Mandatory Pickup = [arrastra aquí el Pickup obligatorio]
```

### WinCollider
```
[GameObject] Portal / WinCollider
├── Collider (Is Trigger = ON)
├── WinCollider script
│   ├── Scene
│   │   ├── End Scene Name = "SCN_EndScene"
│   │   └── Fade Time = 1.5s
│   ├── Behaviour
│   │   ├── Trigger Win State = ON
│   │   └── Require All Pickups = ON
│   ├── Portal Visual / Audio ← NUEVO
│   │   ├── Portal Visual = [arrastra mesh/VFX root aquí]
│   │   ├── Portal Loop Source = [auto-crea si está vacío]
│   │   ├── Portal Idle Fade In = 0.8s
│   │   └── Portal Idle Volume = 0.8
├── [si hay] Mesh Renderer (visual del portal)
├── [si hay] Light, Particle System, etc.
```

### Enemy (Minotauro)
```
[GameObject] Enemy
├── EnemyAIBase script
│   ├── Forced Respawn ← NUEVOS CAMPOS
│   │   ├── forcedRespawnY = 7.15 (altura del NavMesh superior)
│   │   ├── forcedRespawnVerticalTolerance = 2
│   │   ├── forcedRespawnTolerance = 6m
│   │   ├── forcedRespawnSamples = 12
│   │   └── forcedRespawnCooldown = 8s
│   ├── Chase — Lead Targeting ← NUEVOS
│   │   ├── chaseLeadTime = 0.35s
│   │   └── chaseMaxLeadDistance = 4m
│   ├── Search — Spiral ← NUEVOS
│   │   ├── spiralSearchPoints = 4
│   │   ├── spiralMinRadius = 3m
│   │   └── spiralMaxRadius = 9m
│   ├── Speed Smoothing ← NUEVO
│   │   └── speedAccel = 6 m/s²
│   ├── Stalking Audio ← NUEVO
│   │   ├── stalkAudioDistance = 8m
│   │   └── stalkAudioMaxBoost = 2.2
│   └── [resto de campos igual]
```

---

## Flujo en runtime

### Setup inicial (BeginRun)
1. `GameManager.SetupScenePickups()` → busca **todos** los Pickup (incluso inactivos en editor).
2. Si hay `mandatoryPickup` asignado y existe en escena → se queda **ON**, fuera del shuffle.
3. Resto de pickups: shuffle Fisher-Yates, activa N primeros, desactiva el resto.
4. `PickupsActiveInScene` = N de pickups activos (incluyendo mandatorio).

### Durante gameplay
- **Pickups:** Cuando el jugador recolecta uno, `RegisterPickup()` suma contador + emite `OnPickupCountChanged`.
- **WinCollider:** Escucha `OnPickupCountChanged`. Cuando `collected >= required`, activa `portalVisual`, habilita el `Collider`, reproduce SFX/loop.

### Minotauro durante Chase
- **Cada frame:** Estima `playerVelocity` y suaviza `Agent.speed`.
- **Lead targeting:** Si tiene LoS, apunta a `player.position + velocity * 0.35s` (clamped a 4m).
- **Stalking audio:** Si el jugador está a <8m sin LoS, sube el volumen de su respiración hasta 2.2×.

### Minotauro pierde LoS
- Cuenta `chaseMemoryTimer` (escalado por distancia + dificultad).
- Al expirar (p. ej. 5s en Medium):
  - Llama `BeginSpiralSearch(lastKnownPos)`.
  - Genera 4 waypoints en patrón espiral (radio 3-9m).
  - Investiga cada uno (~0.4s) y pasa al siguiente.
  - Tras la espiral, vuelve a Patrol.

### Forced respawn
- **Trigger:** Sin LoS durante `noSightRespawnSeconds` O a >distancia durante `farFromPlayerSeconds`.
- **Muestreo:** Samplea XZ alrededor del player (12 puntos), centrado a Y=7.15.
- **Warp:** Va al punto más cercano a `forcedRespawnDistance`, pero **siempre a Y=7.15**.
- **Post-respawn:** Si está facing al jugador, entra directo a Chase; si no, vuelve a Patrol.

---

## Testing checklist

- [ ] Pickup mandatorio no se apaga nunca (incluso si dificultad pide más pickups).
- [ ] Portal arranca invisible/desactivo.
- [ ] Portal se activa (mesh visible + collider activo + SFX + loop) al completar pickups.
- [ ] Minotauro apunta adelante del jugador en chase (lead targeting visible).
- [ ] Minotauro busca en espiral tras perder LoS (no va directo al último punto).
- [ ] Velocidad del minotauro transiciona suave entre patrol y chase (no saltos).
- [ ] Respiración del minotauro sube de volumen cuando está cerca sin LoS.
- [ ] Respawn forzado ocurre a pasarela superior (Y≈7.15), no al piso inferior.

---

## Notas finales

- Todos los defaults están ajustados para **Medium** como baseline.
- `DifficultyManager.GetRuntimeSettings()` escala automáticamente según pickups recogidos.
- Los SFX del portal (`PortalActivate`, `PortalIdle`, `PortalCross`) **ya existen** en `Assets/Audio/SFX/`.
- Si cambias `forcedRespawnY`, recuerda que debe coincidir con la **cota real del NavMesh superior** del mapa para evitar que caiga por debajo.

---

_Última actualización: 2026-05-25 — Gameplay v0.8_

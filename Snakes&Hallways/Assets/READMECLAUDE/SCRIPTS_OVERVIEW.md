# Snakes & Hallways — Resumen de Scripts

Resumen de TODOS los scripts bajo `Assets/Scripts/` (estado a fecha **2026-05-24**).

---

## 📁 Enemy — IA del Minotauro

### `EnemyAIBase.cs`
IA "ciega" del enemigo. NavMeshAgent con estados `Idle / Patrol / Investigate / Alert / Chase / Attacking / Stunned`.

**Patrullaje:**
- Si `patrolPoints` está vacío → **wander inteligente por NavMesh**: muestrea `wanderSampleCount` puntos aleatorios en `wanderRadius`, elige el de mejor score (distancia mínima + sesgo opcional hacia el jugador vía `WanderBiasToPlayer`). **No cambia de destino hasta llegar.**
- Si hay `patrolPoints` → ruta fija de toda la vida.

**Visión realista:**
- LoS con el jugador → **interrumpe cualquier estado** y entra a Chase inmediatamente. No usa thresholds para entrar a Alert/Chase.
- Mientras lo ve, refresca `chaseMemoryTimer` con un valor escalado por distancia (`ComputeMemoryDuration`):
  - Distancia ≤ `closeMemoryDistance` (4 m) → memoria × `closeMemoryMultiplier` (1.6).
  - Distancia ≥ `farMemoryDistance` (18 m) → memoria × `farMemoryMultiplier` (0.4).
- Al perder LoS, sigue persiguiendo hasta agotar `chaseMemoryTimer`, luego pasa a `Investigate` del último punto conocido y de ahí a `Patrol`.

**Combate:**
- `safetyDistance` (1.6 m) — el agente apunta a un punto antes del jugador para no clipearlo.
- `attackRange` para entrar a `Attacking`. Solo si `attackCooldownTimer <= 0` y `postAttackWalkTimer <= 0`.
- En `Attacking`: para al agente, llama `enemyAnim.TriggerAttack()` (dispara UnityEvent `OnAttack` que reproduce la animación), encara al jugador.
- El daño efectivo lo aplica el AnimationEvent `AttackFrame` → `OnAttackFrame()` → `attackCollider.OpenWindow()`.
- `NotifyAttackLanded()` lo llama `EnemyAttack` al impactar: arranca `attackCooldownTimer` y `postAttackWalkTimer` (de `DifficultyManager`), fuerza salida a `Patrol`.

**Hints e investigación:**
- `ReceiveHint(pos, radius)` desde `EnemyAIInteligent`. Tira un dado contra `DifficultySettings.hintIgnoreChance` — si falla, ignora la pista.
- `ForceKnownPlayer(pos)`, `Teleport(pos)` — usados por el director.

**Animator:**
- Solo dos bools: `Patrolling` / `Chasing`. **Sin triggers de Turn ni Alert** (giro gradual continuo).
- Los bools se ponen a `false` cuando el agente no se mueve realmente (chequeo de `Agent.velocity` + `isStopped`) para que no quede plantado en Run idle.

### `EnemyAIInteligent.cs` (singleton)
Capa "Director". Sabe SIEMPRE dónde está el jugador y alimenta pistas a la IA ciega.

- Auto-wiring de `blindAI`: busca primero en su GameObject, luego en escena.
- Cada frame ajusta `blindAI.WanderBiasToPlayer = esc * 0.9f` → a más dificultad, más cerca del jugador elige sus puntos de patrullaje.
- Frecuencia de pistas: `baseHintInterval` lerp a `minHintInterval` por escalación, modulado por `DifficultySettings.hintFrequencyMul`.
- Omnisciencia ocasional (`Random.value < diff.omniscience * 0.05f`) → `ForceKnownPlayer`.
- Gestiona spawns dentro de `Room` y reposicionamiento post-room (`PostRoomRespawn`).
- Hace check de visibilidad de cámara para no teleportarse delante del jugador.

### `EnemyAnimator.cs`
Wrapper del Animator. Diseño basado en **UnityEvents** + **Animation Events** (sin triggers internos).

**Animator parameters esperados:** `Patrolling` (bool), `Chasing` (bool). Nada más.

**UnityEvents públicos** (enganchables en inspector):
- `OnAttack` → engánchalo a `Animator.Play("Attack")` para que reproduzca el clip de ataque.
- `OnAlert` → opcional (reproduce `MinotaurNeigh` por código, lo demás opcional).
- `OnFootstepWalk` / `OnFootstepRun` → opcionales (shake + SFX se hacen por código).

**Animation Events a colocar en los clips:**
- `AttackFrame()` en el frame del impacto del clip de ataque → `EnemyAIBase.OnAttackFrame()` → abre la hitbox.
- `FootstepWalk()` en cada pisada del clip de Walk → `CameraShake.Shake` + `AudioManager.PlaySFX(walkStepSfx, pos)`.
- `FootstepRun()` en cada pisada del clip de Run → idem con `runStepShake` / `runStepSfx`.

**Configurable en inspector:**
- `walkStepShake = 0.15`, `runStepShake = 0.45`, `stepShakeDuration = 0.18`.
- `walkStepSfx = SFXId.MinotaurStep`, `runStepSfx = SFXId.MinotaurStep` (cambiables).
- `gradualTurnSpeed = 180°/s` — única velocidad de giro (sin animaciones de Turn 90/180).

### `EnemyAttack.cs`
Hitbox con tag `EnemyAttack`. Vive en un hijo del enemigo con `Collider` (BoxCollider típico). Trigger, deshabilitado al inicio.
- `OpenWindow()` abre el collider durante `windowDuration` (0.35 s).
- `OnTriggerEnter` con Player → `PlayerHealth.TakeDamage(damage)`, fallback a `GameManager.TriggerGameOver` si no hay `PlayerHealth`.
- **Tras impactar:** llama a `EnemyAIBase.NotifyAttackLanded()` (cooldown + post-attack walk) y cierra el collider inmediatamente (un golpe por ventana).

### `EnemyDetection.cs`
Cono de visión + ocluder raycast. Lógica pura, no renderiza.

- `Eye` Transform: si se deja vacío, **autocrea un hijo `Eye`** en `(0, 1.6, 0.2)` con `forward = +Z`. `OnValidate` lo enlaza automáticamente si encuentra un hijo llamado "Eye".
- Calcula `Score (0..1)`, `Visibility (None/Backside/Frontside)`, `HasLineOfSight`.
- Multiplicadores por estado del jugador: crouch, walk, sprint, idle, lampOn.
- Canal estático `NoiseHeard` que recibe ruidos del jugador.
- **Gizmos siempre visibles**: esfera + flecha cyan en el eye (incluso sin seleccionar). Al seleccionar muestra cono + esfera de `viewDistance`. El cono rota alrededor de `eye.up` (no `Vector3.up`), así que pitch/roll del Eye se reflejan correctamente.

### `EnemyCameraView.cs`
Cámara renderizada del enemigo (la que ves al pulsar **C**). **No interviene en la IA.**
- Si `spyCamera` vacío → autocrea un hijo `EnemySpyCamera` con offset `(0, 1.6, 0)`, FOV 75°.
- Si `manageAudioListener = true` (default) → añade un `AudioListener` propio que se enciende/apaga con la cámara.
- `SetActive(bool)` activa/desactiva el **GameObject** (no solo el componente) + `Camera.enabled` + `AudioListener.enabled`. Esto soporta el caso de que la cámara esté apagada en escena por defecto.

---

## 📁 GamePlay

### `Interactable.cs`
Componente con `UnityEvent onInteract` + `prompt`. Lo dispara `PlayerController.TryInteract()` con un SphereCast.

### `Pickup.cs`
Coleccionable de trigger. Al tocar al jugador llama a `GameManager.RegisterPickup()` y se desactiva.

### `Portal.cs`
Frame visible cuando está seleccionado por `PortalManager`. `Activate()` lo enciende cuando se completan los pickups. Cruzarlo dispara `GameManager.TriggerWin()`.

### `Room.cs`
Zona trigger que notifica a `EnemyAIInteligent` cuando el player entra/sale, y expone `interiorSpawnPoints` para teleports.

---

## 📁 Managers

### `GameManager.cs` (singleton)
Estado global del juego: `Playing / Paused / GameOver / Win`.

- **Timer:** `matchTime` (inicial) + `currentTime` (serialized — editable en runtime desde inspector).
- `TimeRemaining` ahora es get/set sobre `currentTime` para que el inspector muestre la cuenta atrás en vivo.
- En `Start` busca un `PlayerHealth` en escena y engancha `OnDied += TriggerGameOver`.
- `TriggerGameOver()` → `SceneTransition.EnsureInstance().FadeAndLoad(gameOverScene, deathFadeTime)`. `gameOverScene` por defecto **`SCN_DeathScene`**.
- `TriggerWin()` similar con `winScene`.
- `Pause(bool)` cambia `Time.timeScale`.

### `DifficultyManager.cs` (singleton DontDestroyOnLoad)
4 niveles (`Easy / Medium / Hard / Impossible`) con `DifficultySettings`:

| Campo | Easy | Medium | Hard | Impossible |
|---|---|---|---|---|
| pickupsRequired | 4 | 6 | 8 | 10 |
| baseAggression | 0.1 | 0.35 | 0.6 | 1.0 |
| omniscience | 0 | 0.1 | 0.35 | 1.0 |
| roomSpawnChance | 0.05 | 0.2 | 0.35 | 0.6 |
| postRoomSpawnDistance | 60 | 35 | 18 | 8 |
| soundReactivity | 0 | 0.5 | 0.85 | 1.0 |
| chaseSpeedMul | 0.85 | 1.0 | 1.15 | 1.3 |
| hintFrequencyMul | 0.5 | 1.0 | 1.6 | 2.5 |
| **chaseMemorySeconds** | 1.5 | 3 | 5 | 8 |
| **hintIgnoreChance** | 0.7 | 0.4 | 0.15 | 0 |
| **attackCooldown** | 4 | 3 | 2 | 1 |
| **postAttackWalkSeconds** | 6 | 4 | 2 | 0.5 |

- `GetEscalation(collected, total)` mezcla dificultad base + progreso de pickups.

### `DifficultyButton.cs`
Botón UI que aplica una dificultad. Refresca el resaltado del botón seleccionado entre todos los `DifficultyButton` activos.

### `DefeatManager.cs` (singleton)
Pantalla de derrota in-game. Escucha `PlayerHealth.OnDied` y `GameManager.OnStateChanged == GameOver`. Hace fade-in sobre `Time.unscaledDeltaTime` y muestra botón "Main Menu". **NOTA:** Tras el cambio en `GameManager.TriggerGameOver`, ahora se hace fade y carga directa de `SCN_DeathScene`. Si quieres mantener `DefeatManager` in-game, hay que decidir cuál prevalece.

### `PickupManager.cs` (singleton)
Spawnea o activa pickups. Dos modos:
- Instanciar `pickupPrefab` en N puntos aleatorios de `candidatePoints`.
- Activar N pickups ya colocados en escena de `existingPickups`.
- N viene de `DifficultyManager.GetSettings().pickupsRequired`.

### `PortalManager.cs` (singleton)
Lista de portales candidatos. Elige uno aleatoriamente al `Start()`, oculta los demás, lo activa cuando se reciben suficientes pickups.

### `AudioManager.cs` (singleton DontDestroyOnLoad)
- Enums `SFXId` (incluye `MinotaurStep`, `MinotaurNeigh`, `MinotaurCharge`, `MinotaurDetect`, `MinotaurIdleBreath`...) y `MusicId`.
- Pool de `AudioSource` para SFX 3D/2D, dos sources para música con crossfade.
- Métodos `PlaySFX(id, pos, vol)`, `PlaySFX2D(id, vol)`, `PlayMusic(id, fade)`.

### `UIManager.cs` (singleton)
HUD + Pausa con paneles que entran deslizando desde la izquierda.
- Texto de pickups (`X/Y`), timer (`MM:SS`).
- `TogglePause(bool)` engancha con `GameManager.Pause` y gestiona cursor.

### `ShaderManager.cs` (singleton opcional DontDestroyOnLoad)
Controla globalmente propiedades del shader `Custom/MainShader`. Auto-fill desde escena en editor.

### `SpyCamController.cs` (singleton) — **rehecho**
Cámara espía del enemigo (tecla **C**).

- **Auto-wire:** busca `playerCamera` (Camera.main), `EnemyCameraView`, `EnemyDetection` y el Player por tag.
- Toggle con C → `TryEnter` / `Exit`. `IsActive` y `IsTransitioning` son leídos por `PlayerController` para bloquear input.
- **`CanEnter`:** falla si el enemigo te ve o estás a menos de `minSafeDistance`.
- **Auto-exit:** durante el modo espía, si te ven o entras a menos de `autoExitDistance` → sale automáticamente.
- **Fade:** usa `SceneTransition.EnsureInstance()` por defecto (toggle `useSceneTransitionFade`). Si no funciona o se desactiva, crea su **propio overlay Canvas** en `EnsureFallbackOverlay`: Canvas Screen Space Overlay con `sortingOrder = 32760` y una Image negra a pantalla completa. **Garantiza siempre el fade.**
- Gestiona `AudioListener` del player + del enemyView de forma exclusiva (uno solo activo a la vez).

### `CameraShake.cs` (singleton) — **nuevo**
Singleton de shake de cámara. `DontDestroyOnLoad`.

- **API estática:** `CameraShake.Shake(sourcePos, baseIntensity, duration)` y `CameraShake.ShakeUniform(intensity, duration)`.
- Aplica shake (Perlin noise) sobre la cámara `isActiveAndEnabled` con mayor `depth`. Refresca baseline cada `LateUpdate`.
- Intensidad por distancia: `minDistance` (1.5 m → 100%) a `maxDistance` (25 m → 0%), con `falloffPower` (default 2 = cuadrática).
- Sistema de "trauma" 0–1 con decay. Repetir shakes acumula.
- Si no hay instancia en escena se autocrea (`EnsureInstance`).

---

## 📁 Player

### `PlayerController.cs`
- Movimiento Rigidbody con `walk/sprint/crouch`, stamina con cooldown.
- Look con `camHolder` (yaw player, pitch camHolder).
- Crouch suave que cambia sólo `CapsuleCollider.height`.
- Head bob con amplitudes/frecuencias por estado, modo "tired".
- Footsteps con intervalo por estado, emite `EnemyDetection.NotifyNoise`.
- Interacción por SphereCast (`Interactable`).
- Linterna (`lampObject`) toggle con tecla F.
- Animator con bools Walking/Running/Crouching y triggers Draw/ReverseDraw.
- `ToggleHand()` con input `OnToggleHand` (Tab) → alterna sacar/guardar la mano.
- `OnSwitchCamera` (C) → `SpyCamController.Instance?.Toggle()`. `InputBlocked` mientras la spy cam está activa o en transición.

### `PlayerHealth.cs`
- HP con `TakeDamage(amount)` + i-frames cortos para evitar multi-hit.
- Regeneración: tras `regenDelay` (60s por defecto) sin recibir daño, regenera `regenRate` HP/s hasta el máximo.
- Eventos `OnDamaged / OnHealthChanged / OnDied`.
- `GameManager.Start` lo engancha automáticamente para llamar `TriggerGameOver` al morir.

### `INP_Player.inputactions`
Action map único `Gameplay`. Actions: Move, Look, Jump, Shoot, Crouch, Sprint, Reload, Pause (Esc), Interact (E), Lamp (F), ToggleHand (Tab), **SwitchCamera (C)**.

---

## 📁 Scene-Related

### `SceneTransition.cs` (singleton DontDestroyOnLoad) — **rehecho**

Canvas Screen Space Overlay con Image a pantalla completa. Hace fade entre escenas y dentro de escena (cambio de cámaras).

**Auto-bootstrap:** `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` crea uno automáticamente al cargar cualquier escena si no hay uno manual. `EnsureInstance()` lo fuerza también desde código.

**Dropdown `Variant` en inspector** (enum `TransitionVariant`):
- `EntryDissolveIn` — arranca opaco, se disuelve a transparente en Start. **Default.**
- `ExitFadeToBlack` — arranca transparente, se opaca en Start.
- `Manual` — no hace nada en Start; solo responde a llamadas explícitas.

**Checkbox `Force Plain Alpha`** — ignora el shader `UI/Dissolve` y usa un alpha fade plano. Útil si el dissolve falla por shader no incluido en build o setup raro. **100% nativo Unity UI.**

**Canvas forzado a `ScreenSpaceOverlay` con `sortingOrder = 32760`** en Awake. Funciona aunque las cámaras del juego se desactiven (sigue renderizando).

**API:**
- `FadeAndLoad(scene, t)` — fade a negro + carga escena.
- `FadeAndLoadWithSpawn(scene, spawnId, t)` — fade + carga + coloca al player en `SpawnPoint` con ese id.
- `WarpInScene(spawnId, t)` — teleport in-scene con fade.
- `FadeAction(System.Action mid, fadeIn, hold, fadeOut)` — fade a negro → ejecuta callback → fade a transparente. Lo usa `SpyCamController` para los cambios de cámara.
- `FadeToBlack(t)` / `FadeFromBlack(t)` — corutinas directas.

### `SpawnPoint.cs`
Marcador de spawn con `spawnId`. Se registra en `SceneTransition` y resuelve cuando se carga una escena con `FadeAndLoadWithSpawn`.

---

## 📁 UI

### `TimerDisplay.cs` — **nuevo**
Componente que va sobre un `TMP_Text`. Muestra `GameManager.TimeRemaining` en formato `mm:ss`.
- Se suscribe a `GameManager.OnTimerChanged` (sin polling).
- Color rojo cuando quedan menos de `lowTimeThreshold` (30 s por defecto).
- Fallback `--:--` si `GameManager.Instance` aún no existe.

### `SettingsUIPanel.cs`
Panel de settings (sliders de volumen, etc.).

---

## 📁 VFX_Related

### `DamageVignette.cs`
`Image` UI con 3 estados (amarillo/naranja/rojo) que se activan por HP. Pulse de alpha sinusoidal + flash al recibir daño. Se autoenlaza a `PlayerHealth` buscando el tag `Player`.

---

# 🔧 Cómo montar el minotauro

```
Enemy (root, tag Enemy, layer Enemy)
├── NavMeshAgent
├── Animator (parámetros: Patrolling, Chasing)
├── EnemyAIBase
├── EnemyDetection (auto-crea Eye si vacío)
├── EnemyAnimator (UnityEvents OnAttack, OnFootstepWalk/Run)
├── EnemyAIInteligent (singleton, auto-wire de blindAI)
├── EnemyCameraView (auto-crea Camera + AudioListener)
├── Eye (hijo, Transform — apunta hacia delante)
├── Mesh / Rig
└── AttackHitbox (hijo, layer EnemyAttack)
    ├── BoxCollider (gestionado por el script: trigger, off al inicio)
    └── EnemyAttack
```

**Capas / física:** Player ↔ Enemy desactivado en Layer Collision Matrix (Edit → Project Settings → Physics). Player ↔ EnemyAttack activado (es trigger).

**Animator del enemigo:**
- States: `Idle`, `Walk`, `Run`, `Attack`, etc.
- Transiciones por bool `Patrolling` (Idle ↔ Walk) y `Chasing` (Walk ↔ Run).
- AnimationEvents en clips:
  - `Attack` → `AttackFrame()` en frame del impacto.
  - `Walk` → `FootstepWalk()` en cada pisada.
  - `Run` → `FootstepRun()` en cada pisada.

**UnityEvents en EnemyAnimator (inspector):**
- `OnAttack` → `Animator.Play("Attack")` (apuntando a su propio Animator).
- Resto: vacíos por defecto.

---

# 🎮 Loop de juego

1. **Inicio escena:** `SceneTransition` (variant `EntryDissolveIn`) disuelve la pantalla negra.
2. **Gameplay:** `GameManager.currentTime` cuenta atrás. `EnemyAIInteligent` da hints al `EnemyAIBase`, que patrulla por NavMesh.
3. **Detección:** si el enemigo te ve → interrumpe y persigue. Si lo rompes, te recuerda según distancia × dificultad.
4. **Combate:** ataque por AnimationEvent → `EnemyAttack.OpenWindow()` → `PlayerHealth.TakeDamage()`. Tras impactar, cooldown + walk forzado durante X segundos.
5. **Pickups:** recoges N (según dificultad) → `PortalManager` activa el portal → cruzas → `TriggerWin()` → `Win` scene.
6. **Muerte:** HP a 0 o timer a 0 → `TriggerGameOver` → fade a negro → `SCN_DeathScene`.
7. **Modo espía (C):** si no te ven y estás lejos → fade dissolve → cámara del enemigo. Si te ven o te acercas → fade de vuelta automático.

---

# 🔴 Qué falta hacer / pulir

## 🚨 Críticos
1. **`SCN_DeathScene` en Build Settings.** Si no está añadida, `TriggerGameOver` lanza error al cargar.
2. **Layers `Enemy` y `EnemyAttack`** creadas + Layer Collision Matrix configurada Player ↔ Enemy = off.
3. **Always Included Shaders** debe incluir `UI/Dissolve` para builds (Project Settings → Graphics). Alternativa: marcar `Force Plain Alpha` en SceneTransition.

## 🟠 Loop
4. **`VictoryManager`** análogo al Defeat, si quieres pantalla in-game para Win.
5. **MainMenu** con `DifficultyButton`s.

## 🟡 UI / HUD
6. **Barra de vida** enganchada a `PlayerHealth.OnHealthChanged`.
7. **Indicador de stamina** en HUD.
8. **Crosshair / prompt de interacción** con `InteractionPrompt`.

## 🟢 Pulido
9. **Llenar SFXEntries** del `AudioManager` (faltan clips para varios IDs).
10. **Steps por superficie** — actualmente todo es `PlayerStepStone`.
11. **Feedback de pickup recogido** (VFX, flash, screen shake — el sistema ya está).
12. **`DefeatManager` vs `SCN_DeathScene`** — decidir si quieres pantalla in-game o escena dedicada. Hoy `GameManager.TriggerGameOver` carga la escena directamente y `DefeatManager` queda huérfano salvo que lo enganches manualmente.

---

# 📋 Changelog de esta sesión (2026-05-24)

**IA del enemigo:**
- Patrullaje por NavMesh con elección inteligente (sesgo por dificultad).
- Visión realista: LoS interrumpe todo, memoria escalada por distancia + dificultad.
- Safety distance para no clipear al jugador.
- Cooldown de ataque + post-attack walk forzado (lento, sin atacar) durante X s.
- `hintIgnoreChance` para que la dificultad fácil ignore más pistas.
- Animator simplificado: sin Turn triggers, sin Alert trigger. Solo bools Patrolling/Chasing.
- UnityEvents `OnAttack`, `OnAlert`, `OnFootstepWalk`, `OnFootstepRun`.
- Animation Events `AttackFrame`, `FootstepWalk`, `FootstepRun` con shake + SFX.
- Gizmo del cono usa `eye.up` (sigue pitch/roll).
- `EnemyCameraView` gestiona GameObject + AudioListener.
- `EnemyDetection` autocrea hijo `Eye` si no asignado.

**Cámaras:**
- `CameraShake` nuevo: singleton, falloff por distancia, Perlin noise.
- `SpyCamController` rehecho: usa `SceneTransition` + fallback overlay propio.

**Transiciones:**
- `SceneTransition` con dropdown `Variant`, `Force Plain Alpha`, auto-bootstrap.
- `FadeAction` API para callbacks in-scene.

**Dificultad:**
- 4 nuevos campos: `chaseMemorySeconds`, `hintIgnoreChance`, `attackCooldown`, `postAttackWalkSeconds`.

**Sistema de juego:**
- `GameManager.gameOverScene = "SCN_DeathScene"`, hooks a `PlayerHealth.OnDied`.
- `GameManager.currentTime` serialized editable en runtime.
- `TimerDisplay` UI nuevo para mostrar el tiempo.

# Snakes & Hallways — Resumen de Scripts

Resumen de TODOS los scripts bajo `Assets/Scripts/` (estado a fecha 2026-05-22).

---

## 📁 Enemy — IA del Minotauro

### `EnemyAIBase.cs`
IA "ciega" del enemigo. NavMeshAgent con estados `Idle / Patrol / Investigate / Alert / Chase / Attacking / Stunned`.
- Patrulla entre `patrolPoints`, investiga ruidos (`EnemyDetection.NoiseHeard`), entra en alerta o persecución cuando `EnemyDetection.Score` supera los umbrales.
- Si te ve por la espalda, hay un % (`chargeFromBackChance`) de cargar directamente sin rugir.
- Recibe pistas aproximadas (`ReceiveHint`) y posiciones forzadas (`ForceKnownPlayer`) desde `EnemyAIInteligent`.
- Llama a `EnemyAttack.OpenWindow()` desde un AnimationEvent (`OnAttackFrame`).
- Lee dificultad de `DifficultyManager` para velocidad de carga y escalado.

### `EnemyAIInteligent.cs` (singleton)
Capa "Director". Sabe SIEMPRE dónde está el jugador y alimenta pistas a la IA ciega.
- Frecuencia de pistas regulada por `hintFrequencyMul` × escalada de pickups.
- Probabilidad de omnisciencia (`omniscience`) — en Impossible siempre conoce posición.
- Gestiona spawns dentro de `Room` y reposicionamiento post-room (`PostRoomRespawn`).
- Hace check de visibilidad de cámara para no teleportarse delante del jugador.

### `EnemyAnimator.cs`
Wrapper del Animator del enemigo. Maneja:
- Bools `Patrolling` / `Chasing`, triggers `Attack` / `Alert`.
- Giros: gradual (<90°) sin animación, animaciones `Turn 90 / Turn -90 / Turn 180` con bloqueo de locomoción.
- AnimationEvent `AttackFrame()` que llama a `EnemyAIBase.OnAttackFrame()`.

### `EnemyAttack.cs`
Hitbox con tag `EnemyAttack`. Se enciende durante `windowDuration` cuando se llama a `OpenWindow()`.
- ⚠️ Actualmente llama a `GameManager.TriggerGameOver()` directamente — **bypassa `PlayerHealth`**.

### `EnemyDetection.cs`
Cono de visión + ocluder raycast. Calcula `Score (0..1)` y `Visibility (None/Backside/Frontside)`.
- Multiplicadores por estado del jugador: crouch, walk, sprint, idle, lampOn.
- Canal estático `NoiseHeard` que recibe ruidos del jugador.

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
- Cuenta atrás `matchTime`, eventos `OnTimerChanged / OnPickupCountChanged / OnStateChanged`.
- `RegisterPickup()` — incrementa contador y activa portal cuando se alcanza `PickupsRequired` (definido por dificultad).
- `TriggerGameOver()` ahora deja la pantalla al `DefeatManager` si existe; si no, carga la escena de GameOver.
- `Pause(bool)` cambia `Time.timeScale`.

### `DifficultyManager.cs` (singleton DontDestroyOnLoad)
4 niveles (`Easy / Medium / Hard / Impossible`) con bloque `DifficultySettings`: pickups requeridos, agresividad, omnisciencia, prob. de spawn en room, reactividad al sonido, multiplicador de velocidad de carga, frecuencia de pistas.
- `GetEscalation(collected, total)` mezcla dificultad base + progreso de pickups.

### `DifficultyButton.cs`  *(nuevo)*
Botón UI que aplica una dificultad. Refresca el resaltado del botón seleccionado entre todos los `DifficultyButton` activos.

### `DefeatManager.cs`  *(nuevo, singleton)*
Pantalla de derrota. Escucha `PlayerHealth.OnDied` y `GameManager.OnStateChanged == GameOver` (fin de timer). Hace fade-in sobre `Time.unscaledDeltaTime` y muestra botón "Main Menu".

### `PickupManager.cs` (singleton)
Spawnea o activa pickups. Soporta dos modos:
- Instanciar `pickupPrefab` en N puntos aleatorios de `candidatePoints`.
- Activar N pickups ya colocados en escena de la lista `existingPickups`.
- N viene de `DifficultyManager.GetSettings().pickupsRequired`.

### `PortalManager.cs` (singleton)
Lista de portales candidatos. Elige uno aleatoriamente al `Start()`, oculta los demás, lo activa cuando se reciben suficientes pickups.

### `AudioManager.cs` (singleton DontDestroyOnLoad)
- Enums `SFXId` y `MusicId`.
- Pool de `AudioSource` para SFX 3D/2D, dos sources para música con crossfade.
- Métodos `PlaySFX(id, pos, vol)`, `PlaySFX2D(id, vol)`, `PlayMusic(id, fade)`.
- Librerías serializadas con variantes de clip y pitch random.

### `UIManager.cs` (singleton)
HUD + Pausa con paneles que entran deslizando desde la izquierda (coroutine sin DOTween).
- Texto de pickups (`X/Y`), timer (`MM:SS`).
- `TogglePause(bool)` engancha con `GameManager.Pause` y gestiona cursor.
- Botones Resume / Options / MainMenu / Quit + hook hover de UI sonido.

### `ShaderManager.cs` (singleton opcional DontDestroyOnLoad)
Controla globalmente propiedades del shader `Custom/MainShader` para todos los materiales registrados (AO estilizado, AO por distancia, suavizado de highlights, fuerza de normales/oclusión/curvatura). Auto-fill desde escena en editor.

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
- `noneStateMeshes` se ocultan cuando el Animator está en estado `None`.
- *(añadido)* Dos `AudioSource` locales auto-generados (`stepsSource`, `handEyeSource`) con arrays de clips locales opcionales para pasos y mano/ojo.
- *(añadido)* `ToggleHand()` con input `OnToggleHand` (Tab) → alterna sacar/guardar la mano. `HandDrawn` pública.

### `PlayerHealth.cs`  *(nuevo)*
- HP con `TakeDamage(amount)` + i-frames cortos para evitar multi-hit.
- Regeneración: tras `regenDelay` (60s por defecto) sin recibir daño, regenera `regenRate` HP/s hasta el máximo.
- Eventos `OnDamaged / OnHealthChanged / OnDied`.

### `INP_Player.inputactions`
Action map único `Gameplay`. Actions: Move, Look, Jump, Shoot, Crouch, Sprint, Reload, Pause (Esc), Interact (E), Lamp (F), **ToggleHand (Tab)** *(nuevo)*. Bindings teclado + gamepad genérico.

---

## 📁 Scene-Related

### `SceneTransition.cs` (singleton DontDestroyOnLoad)
Canvas con `Image` negra a pantalla completa. `FadeAndLoad(scene, t)` y `FadeFromBlack(t)` con `unscaledDeltaTime`.

---

## 📁 VFX_Related

### `DamageVignette.cs`  *(nuevo)*
`Image` UI con 3 estados (amarillo/naranja/rojo) que se activan por HP. Pulse de alpha sinusoidal + flash al recibir daño. Se autoenlaza a `PlayerHealth` buscando el tag `Player`.

---

# 🔴 Qué falta hacer (prioridad alta → baja)

## 🚨 Bugs / inconsistencias críticas
1. **`EnemyAttack` no usa `PlayerHealth`.** Llama directamente a `GameManager.TriggerGameOver()`. Hay que cambiarlo a `playerHealth.TakeDamage(damageAmount)` para que la viñeta de daño, las i-frames y la regeneración funcionen. Si el HP llega a 0, `PlayerHealth.OnDied` → `DefeatManager` ya engancha. Así un golpe ya no es one-shot salvo que quieras (campo `damage` configurable).
2. **`PlayerHealth` NO está en el Player.prefab todavía.** Hay que añadir el componente al prefab (o instanciarlo por código en `PlayerController.Awake`).
3. **`DamageVignette` necesita un `Canvas` UI con un `Image`** en la escena, colocado y enlazado. No existe todavía.

## 🟠 Loop de juego incompleto
4. **`VictoryManager` (espejo del Defeat).** Cuando `GameManager.TriggerWin()`, hoy se carga la escena `Win`. Si quieres pantalla in-game como en la derrota, falta crearlo.
5. **Escena `MainMenu`** con botones Play / Quit + los `DifficultyButton`. No existe (sólo está la referencia por nombre).
6. **Escenas `GameOver` y `Win`** referenciadas en `GameManager` — si dependes sólo de `DefeatManager` puedes borrar esa dependencia. Si no, hay que crearlas.

## 🟡 UI / HUD
7. **Barra de vida** enganchada a `PlayerHealth.OnHealthChanged` en `UIManager`. Hoy se ven pickups y timer pero no HP.
8. **Indicador de stamina** (`PlayerController.IsTired`) en HUD.
9. **Crosshair / prompt de interacción** ("Press E to interact") cuando el SphereCast detecta un `Interactable`.
10. **Compass / radar al portal** opcional, para los últimos minutos.

## 🟢 Persistencia y settings
11. **Guardar dificultad** en `DifficultyManager.SetDifficulty` con `PlayerPrefs.SetInt`.
12. **AudioMixer** con buses Master/SFX/Music/UI + sliders en pausa. Hoy `AudioManager` referencia los grupos pero no hay panel de opciones.
13. **Sensibilidad / inversión de eje** en opciones (`PlayerController.sensitivity`).

## 🔵 Audio
14. **Llenar las `SFXEntry`** del `AudioManager` para los IDs ya enumerados (faltan probablemente la mayoría de clips).
15. **Steps por tipo de superficie** (stone, grass, wood...) — hoy `SFXId.PlayerStepStone` es único. Si vas a tener materiales variados, conviene un detector tipo `PhysicMaterial` que decida el clip.
16. **Sonidos de eye/hand**: ya tienes el `handEyeSource` en el Player y enums `EyeViscous / EyeZoom / EyeProximity`, pero falta el script que dispare estos según proximidad del enemigo. Recomendado: nuevo `EyeProximityCue.cs` que mire la distancia/ángulo al `EnemyAIBase` y reproduzca clips en `handEyeSource`.

## ⚪ Calidad / pulido
17. **`PortalManager` no avisa visualmente cuando se activa** (sólo enciende objetos). Una flash de pantalla + cue de audio fuerte ayudaría.
18. **`Pickup` carece de feedback fuerte** al recogerse (VFX, light flash, screen shake). Hoy sólo sonido.
19. **`SceneTransition.FadeFromBlack`** no se llama en ningún sitio. Falta enganche en `Start` de la escena de juego o un manager de boot.
20. **No hay sistema de save/checkpoint**. Para una run de 10 min puede no hacer falta, pero si la run muere y vuelves al menú pierdes la dificultad y los settings (ver #11).
21. **`PlayerController.OnShoot` y `OnReload`** son stubs — si vas a tener arma, falta `GunSystem` (el `UIManager.cs` del Desktop habla de él).
22. **CarouselUI / inventario del Desktop NO está adaptado.** Si vas por dirección Slenderman puro no hace falta; si quieres inventario tipo RE4, hay trabajo gordo ahí.

## 📦 Esenciales que tampoco están
23. **Layers / Tags** correctos: confirmar que existen `Player`, `EnemyAttack`, `Room`, `Interactable`.
24. **NavMesh horneado** en la escena principal — `EnemyAIBase` lo necesita sí o sí.
25. **Una escena de tutorial / intro corta** (el Desktop tenía `IntroDialogueManager` que no porté; podría adaptarse fácil si quieres).
26. **`WorldDialogueManager` adaptado** — los `Pickup` podrían disparar líneas de diálogo al recogerse para narrativa.

---

# Sugerencia de orden si quieres seguir
1. Añadir `PlayerHealth` al prefab del Player + reemplazar la llamada de `EnemyAttack` por `TakeDamage` con campo `damage` configurable.
2. Crear Canvas de HUD con la Image de viñeta de daño y enganchar `DamageVignette`.
3. Crear Canvas de Defeat con `DefeatManager` enganchado.
4. Crear escena `MainMenu` con `DifficultyButton`s.
5. Barra de vida en `UIManager`.
6. Llenar la librería de `AudioManager`.

Lo demás es pulido — el loop ya funciona en cuanto los puntos 1–4 estén en escena.

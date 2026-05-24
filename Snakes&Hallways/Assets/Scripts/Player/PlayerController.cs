using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    #region Inspector — Look & Camera
    [Header("Look & Camera")]
    [SerializeField] GameObject camHolder;
    [SerializeField] Transform cameraTransform;
    [SerializeField] float sensitivity = 0.1f;
    public float MouseSensitivity { get => sensitivity; set => sensitivity = Mathf.Clamp(value, 0.01f, 2f); }
    #endregion

    #region Inspector — Movement
    [Header("Movement — Speeds")]
    [SerializeField] float walkSpeed = 5f;
    [SerializeField] float sprintSpeed = 8f;
    [SerializeField] float crouchSpeed = 3f;

    [Header("Movement — Feel (accel / braking)")]
    [Tooltip("Aceleración horizontal en suelo cuando hay input (u/s²).")]
    [SerializeField] float groundAccel = 65f;
    [Tooltip("Frenado horizontal en suelo cuando NO hay input (u/s²). Sube para parar en seco.")]
    [SerializeField] float groundBrake = 55f;
    [Tooltip("Aceleración horizontal en aire (control aéreo limitado).")]
    [SerializeField] float airAccel = 14f;
    [Tooltip("Frenado horizontal en aire sin input (drag muy bajo).")]
    [SerializeField] float airBrake = 2f;
    [Tooltip("Snap del cambio de dirección. Más alto = giros más bruscos / responsivos.")]
    [SerializeField] float turnSharpness = 14f;
    [Tooltip("Tope legacy de cambio de velocidad por step (anti-explosión física).")]
    [SerializeField] float maxForce = 1f;
    #endregion

    #region Inspector — Jump & GroundCheck
    [Header("Jump")]
    [SerializeField] float jumpForce = 5f;
    [Tooltip("Tiempo tras dejar el suelo en el que aún se puede saltar.")]
    [SerializeField] float coyoteTime = 0.12f;
    [Tooltip("Tiempo durante el que el último pulso de salto sigue contando si tocas suelo.")]
    [SerializeField] float jumpBufferTime = 0.15f;
    [Tooltip("Si sueltas salto subiendo, se recorta la velocidad Y por este factor (variable jump height).")]
    [Range(0f, 1f)][SerializeField] float jumpCutMultiplier = 0.5f;
    [Tooltip("Si está activo, el salto consume stamina y exige stamina mínima.")]
    [SerializeField] bool jumpUsesStamina = true;
    [SerializeField] float jumpStaminaCost = 1.2f;
    [SerializeField] float jumpStaminaMin = 0.5f;
    [Tooltip("Salto extra-bajo si estás cansado.")]
    [Range(0.3f, 1f)][SerializeField] float tiredJumpMultiplier = 0.7f;
    [Tooltip("Multiplicador de gravedad cuando vas cayendo (vy<0). 1 = gravedad normal, 2-3 = caída más rápida y peso 'profesional'.")]
    [Range(1f, 5f)][SerializeField] float fallMultiplier = 2.2f;
    [Tooltip("Si está activo, autoengancha la acción Jump del PlayerInput por código en Awake (recomendado: el prefab actual tiene la binding vacía).")]
    [SerializeField] bool autoWireJumpAction = true;

    [Header("Ground Check")]
    [SerializeField] Transform groundCheck;
    [SerializeField] float groundCheckRadius = 0.3f;
    [SerializeField] LayerMask groundLayer;
    #endregion

    #region Inspector — Crouch
    [Header("Crouch")]
    [SerializeField] float standHeight = 1.7f;
    [SerializeField] float crouchHeight = 1.0f;
    [SerializeField] float crouchLerpSpeed = 8f;
    [SerializeField] CapsuleCollider playerCollider;
    #endregion

    #region Inspector — Stamina
    [Header("Stamina (Sprint & Jump)")]
    [SerializeField] float maxStamina = 5f;
    [SerializeField] float staminaRegenRate = 1f;
    [SerializeField] float staminaDrainRate = 1f;
    [SerializeField] float staminaCooldownAfterEmpty = 2f;
    #endregion

    #region Inspector — Head Bob
    [Header("Head Bob")]
    [SerializeField] float bobWalkAmp = 0.09f;
    [SerializeField] float bobWalkFreq = 10f;
    [SerializeField] float bobSprintAmp = 0.18f;
    [SerializeField] float bobSprintFreq = 16f;
    [SerializeField] float bobCrouchAmp = 0.04f;
    [SerializeField] float bobCrouchFreq = 5f;
    [SerializeField] float bobTiredAmp = 0.2f;
    [SerializeField] float bobTiredFreq = 7f;
    [SerializeField] float bobReturnSpeed = 8f;
    #endregion

    #region Inspector — Arm Sway / Overlap
    [Header("Arm Sway — Root")]
    [Tooltip("Transform raíz del brazo/arma. Suele ser un hijo del CamHolder. Se le aplican offsets locales sobre su rest pose.")]
    [SerializeField] Transform armRoot;
    [Tooltip("Si está activo, se autocaptura armRestLocalPos/Euler en Awake con la pose inicial del armRoot.")]
    [SerializeField] bool autoCaptureArmRest = true;
    [SerializeField] Vector3 armRestLocalPos;
    [SerializeField] Vector3 armRestLocalEuler;

    [Header("Arm Sway — Mouse / Movement Overlap")]
    [Tooltip("Cuánto se desplaza el brazo lateralmente al mover el ratón / strafe.")]
    [SerializeField] float swayPosAmount = 0.02f;
    [Tooltip("Cuánto se inclina el brazo al mover el ratón (overlap, en grados).")]
    [SerializeField] float swayRotAmount = 5f;
    [Tooltip("Suavizado del sway: más alto = sigue más a la cámara, más bajo = más lag/overlap.")]
    [SerializeField] float swayLerpSpeed = 9f;
    [Tooltip("Empuje hacia adelante/atrás cuando el jugador acelera o frena.")]
    [SerializeField] float swayAccelKick = 0.04f;

    [Header("Arm Sway — Walk Bob")]
    [SerializeField] float armBobAmpWalk = 0.012f;
    [SerializeField] float armBobAmpSprint = 0.022f;
    [SerializeField] float armBobAmpCrouch = 0.006f;
    [SerializeField] float armBobRotAmp = 2.5f;

    [Header("Arm Sway — Jump / Land Feedback")]
    [Tooltip("Cuánto baja el brazo al despegar.")]
    [SerializeField] float armJumpDip = 0.07f;
    [Tooltip("Cuánto sube/golpea el brazo al aterrizar.")]
    [SerializeField] float armLandKick = 0.10f;
    [Tooltip("Decaimiento del impulso vertical del brazo (más alto = se reposa antes).")]
    [SerializeField] float armImpulseDamp = 10f;
    #endregion

    #region Inspector — Interaction
    [Header("Interaction")]
    [SerializeField] float interactRange = 2.5f;
    [SerializeField] float interactRadius = 0.25f;
    [SerializeField] LayerMask interactMask = ~0;
    #endregion

    #region Inspector — Animator
    [Header("Animator (Main / Armature)")]
    [Tooltip("Animator principal del Gauntlet (Armature). Si se deja vacío se intenta GetComponent<Animator>() en este GameObject.")]
    [SerializeField] Animator anim;

    [Header("Animator None-State Mesh")]
    [Tooltip("Mesh that's hidden while AC_Player is in 'None' state (hands/weapon).")]
    [SerializeField] Renderer[] noneStateMeshes;
    [SerializeField] string noneStateName = "None";
    [SerializeField] int animatorLayer = 0;

    [Header("Secondary Animator (Sphere / Eye)")]
    [Tooltip("Animator del ojo (Sphere). Debe tener los MISMOS parámetros (Draw, ReverseDraw) que el principal. Los triggers se disparan en ambos a la vez desde AnimationHandle().")]
    [SerializeField] Animator sphereAnim;
    #endregion

    #region Inspector — Lamp
    [Header("Lamp")]
    [SerializeField] GameObject lampObject;
    public bool LampOn { get; private set; }
    #endregion

    #region Inspector — SFX / Audio
    [Header("SFX timings")]
    [SerializeField] float stepIntervalWalk = 0.5f;
    [SerializeField] float stepIntervalSprint = 0.32f;
    [SerializeField] float stepIntervalCrouch = 0.75f;

    [Header("Audio Sources (locales del player)")]
    [Tooltip("AudioSource de los PIES (PlayerStepStone alternado). Si está vacío se crea uno en Awake.")]
    [SerializeField] AudioSource stepsSource;
    [Tooltip("AudioSource de la MANO (respiración loopeada + EyeViscous). Si está vacío se crea uno en Awake.")]
    [SerializeField] AudioSource handEyeSource;

    [Header("Respiración (sprint)")]
    [Tooltip("Tiempo de fade-in al empezar a correr.")]
    [SerializeField] float breathFadeIn = 0.4f;
    [Tooltip("Tiempo de fade-out al dejar de correr.")]
    [SerializeField] float breathFadeOut = 0.8f;
    [Tooltip("Multiplicador de volumen aplicado al loop de respiración.")]
    [Range(0f, 1f)][SerializeField] float breathVolume = 1f;
    #endregion

    #region Inspector — Hand
    [Header("Hand toggle")]
    [Tooltip("Si está activo, el jugador tiene la mano sacada (Draw). TAB la guarda/saca.")]
    [SerializeField] bool startWithHandDrawn = false;
    public bool HandDrawn { get; private set; }
    #endregion

    // ── State ────────────────────────────────────────────────────────────────
    Rigidbody rb;
    Vector2 moveInput;
    Vector2 lookInput;
    float lookRotation;

    bool isGrounded;
    public bool IsCrouching { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsMoving => moveInput.sqrMagnitude > 0.01f && isGrounded;

    float stamina;
    float staminaCooldownTimer;

    Vector3 cameraLocalStart;
    float bobTimer;

    float stepTimer;
    int stepVariantToggle; // alterna PlayerStepStone (0) / PlayerStepStone2 (1)
    bool wasGrounded;
    bool breathLoopActive;

    float colliderInitialCenterY;

    // Jump timers
    float coyoteTimer;
    float jumpBufferTimer;
    UnityEngine.InputSystem.InputAction wiredJumpAction; // referencia para desuscribir en OnDisable

    // Movement derived
    Vector3 prevPlanarVel;
    Vector3 planarAccel;
    float landImpulse;       // vertical kick on arm
    float airTimer;          // time since left ground
    float lastFallSpeed;     // |y velocity| at moment of landing

    // Arm sway derived
    Vector3 armCurrentPosOffset;
    Vector3 armCurrentEulerOffset;

    // Animator triggers (los controllers solo tienen Draw / ReverseDraw)
    static readonly int HashDraw        = Animator.StringToHash("Draw");
    static readonly int HashReverseDraw = Animator.StringToHash("ReverseDraw");

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (anim == null) anim = GetComponent<Animator>();
        if (anim == null) Debug.LogWarning("PlayerController: Animator principal no asignado y no se encontró en el GameObject. Asigna el Animator del GuanteBaked.", this);
        if (cameraTransform == null && camHolder != null)
        {
            var cam = camHolder.GetComponentInChildren<Camera>();
            cameraTransform = cam != null ? cam.transform : camHolder.transform;
            if (cam == null)
                Debug.LogWarning("PlayerController: Camera Transform not assigned and no Camera found under CamHolder. Head bob may not work correctly.", this);
        }
        if (cameraTransform) cameraLocalStart = cameraTransform.localPosition;
        stamina = maxStamina;
        if (playerCollider)
        {
            colliderInitialCenterY = playerCollider.center.y;
            standHeight = playerCollider.height;
        }

        if (armRoot && autoCaptureArmRest)
        {
            armRestLocalPos = armRoot.localPosition;
            armRestLocalEuler = armRoot.localEulerAngles;
        }

        EnsureAudioSources();
        TryAutoWireJumpAction();
    }

    /// <summary>
    /// El Player.prefab tiene el PlayerInput configurado como "Invoke Unity Events" pero la
    /// acción Jump quedó SIN PersistentCall hacia OnJump (lista vacía en el Inspector).
    /// Para evitar romper el Inspector wiring del resto de acciones (Move/Look/Crouch/etc.,
    /// que sí están bien), nos enganchamos a Jump por código aquí. Si el usuario añade
    /// también el PersistentCall en el Inspector, OnJump se llamaría DOS veces — para evitarlo
    /// se puede desactivar autoWireJumpAction en el componente.
    /// </summary>
    void TryAutoWireJumpAction()
    {
        if (!autoWireJumpAction) return;
        var pi = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (pi == null || pi.actions == null) return;
        var act = pi.actions.FindAction("Jump", throwIfNotFound: false);
        if (act == null) return;
        wiredJumpAction = act;
        act.performed += OnJump;
        act.canceled  += OnJump;
    }

    void OnDestroy()
    {
        if (wiredJumpAction != null)
        {
            wiredJumpAction.performed -= OnJump;
            wiredJumpAction.canceled  -= OnJump;
            wiredJumpAction = null;
        }
    }

    void EnsureAudioSources()
    {
        if (stepsSource == null)
        {
            var go = new GameObject("AS_Steps");
            go.transform.SetParent(transform, false);
            stepsSource = go.AddComponent<AudioSource>();
            stepsSource.playOnAwake = false;
            stepsSource.spatialBlend = 1f;
        }
        if (handEyeSource == null)
        {
            var go = new GameObject("AS_HandEye");
            go.transform.SetParent(transform, false);
            handEyeSource = go.AddComponent<AudioSource>();
            handEyeSource.playOnAwake = false;
            handEyeSource.spatialBlend = 0.5f;
        }
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (lampObject) lampObject.SetActive(LampOn);
        HandDrawn = startWithHandDrawn;
    }

    void Update()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);

        // Landing: detect & store impact for feedback
        if (!wasGrounded && isGrounded)
        {
            lastFallSpeed = Mathf.Abs(rb.linearVelocity.y);
            // arm landing kick proportional to fall (clamped)
            landImpulse += Mathf.Clamp(lastFallSpeed * 0.04f, 0f, armLandKick);
            AudioManager.Instance?.PlaySFX(SFXId.PlayerFall, transform.position);
            airTimer = 0f;
        }
        wasGrounded = isGrounded;

        // Coyote / jump buffer timers
        if (isGrounded) coyoteTimer = coyoteTime;
        else { coyoteTimer -= Time.deltaTime; airTimer += Time.deltaTime; }
        if (jumpBufferTimer > 0f) jumpBufferTimer -= Time.deltaTime;

        // Try buffered jump
        if (jumpBufferTimer > 0f && coyoteTimer > 0f) TryConsumeJump();

        StaminaTick();
        HeadBob();
        ArmSway();
        FootstepTick();
        UpdateNoneStateMesh();

        // decay arm impulse
        landImpulse = Mathf.MoveTowards(landImpulse, 0f, armImpulseDamp * Time.deltaTime * 0.1f);
    }

    void FixedUpdate()
    {
        Movement();
        ApplyJumpGravityTuning();
    }

    /// <summary>
    /// "Hyper-jump feel" estándar AAA: gravedad extra al caer (fallMultiplier) para
    /// que el arco del salto no se sienta flotante. Combinado con el JumpCut (variable
    /// jump height) en la subida da una sensación responsive de plataforma moderna.
    /// </summary>
    void ApplyJumpGravityTuning()
    {
        if (rb == null) return;
        var v = rb.linearVelocity;
        // Solo modificamos la gravedad cuando vamos cayendo. En la subida ApplyJumpCut
        // ya hace su trabajo al soltar el botón.
        if (v.y < 0f && fallMultiplier > 1f)
        {
            v += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
            rb.linearVelocity = v;
        }
    }

    void LateUpdate() { CameraLook(); }

    #region Movement / Look
    void CameraLook()
    {
        if (InputBlocked) return;
        transform.Rotate(Vector3.up * lookInput.x * sensitivity);
        lookRotation += -lookInput.y * sensitivity;
        lookRotation = Mathf.Clamp(lookRotation, -90f, 90f);
        camHolder.transform.localEulerAngles = new Vector3(lookRotation, 0f, 0f);
    }

    void Movement()
    {
        Vector3 currentVel = rb.linearVelocity;
        Vector3 currentPlanar = new Vector3(currentVel.x, 0f, currentVel.z);

        Vector2 effectiveMove = InputBlocked ? Vector2.zero : moveInput;
        Vector3 wishDirLocal = new Vector3(effectiveMove.x, 0f, effectiveMove.y);
        bool hasInput = wishDirLocal.sqrMagnitude > 0.0001f;
        if (wishDirLocal.sqrMagnitude > 1f) wishDirLocal.Normalize();

        float spd = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
        Vector3 targetPlanar = transform.TransformDirection(wishDirLocal) * spd;

        // Direction-aware accel/brake
        float accel = isGrounded ? groundAccel : airAccel;
        float brake = isGrounded ? groundBrake : airBrake;

        Vector3 newPlanar;
        if (hasInput)
        {
            // Lerp velocity toward target, sharper when changing direction (turnSharpness)
            float dirDot = currentPlanar.sqrMagnitude > 0.01f
                ? Vector3.Dot(currentPlanar.normalized, targetPlanar.normalized)
                : 1f;
            float turnBoost = Mathf.Lerp(1f, turnSharpness * 0.1f, 1f - Mathf.Clamp01(dirDot));
            newPlanar = Vector3.MoveTowards(currentPlanar, targetPlanar, accel * (1f + turnBoost) * Time.fixedDeltaTime);
        }
        else
        {
            // Braking — pull toward zero
            newPlanar = Vector3.MoveTowards(currentPlanar, Vector3.zero, brake * Time.fixedDeltaTime);
        }

        Vector3 velocityChange = newPlanar - currentPlanar;
        // legacy safety clamp so a misconfigured value never throws the player
        velocityChange = Vector3.ClampMagnitude(velocityChange, Mathf.Max(maxForce, accel * Time.fixedDeltaTime));
        velocityChange.y = 0f;
        rb.AddForce(velocityChange, ForceMode.VelocityChange);

        // store planar accel for arm sway feedback
        Vector3 newPlanarAfter = newPlanar;
        planarAccel = Vector3.Lerp(planarAccel, (newPlanarAfter - prevPlanarVel) / Mathf.Max(Time.fixedDeltaTime, 1e-4f), 0.5f);
        prevPlanarVel = newPlanarAfter;

        // Smooth crouch height
        if (playerCollider)
        {
            float targetH = IsCrouching ? crouchHeight : standHeight;
            playerCollider.height = Mathf.Lerp(playerCollider.height, targetH, Time.fixedDeltaTime * crouchLerpSpeed);
            var c = playerCollider.center; c.y = colliderInitialCenterY; playerCollider.center = c;
        }
    }
    #endregion

    #region Jump
    void TryConsumeJump()
    {
        if (jumpUsesStamina && stamina < jumpStaminaMin) return;

        float force = jumpForce * (IsTired ? tiredJumpMultiplier : 1f);

        // zero current vertical velocity for consistent jump height even when stepping off ledges
        var v = rb.linearVelocity; v.y = 0f; rb.linearVelocity = v;
        rb.AddForce(Vector3.up * force, ForceMode.Impulse);

        if (jumpUsesStamina)
        {
            stamina = Mathf.Max(0f, stamina - jumpStaminaCost);
            if (stamina <= 0.01f) staminaCooldownTimer = staminaCooldownAfterEmpty;
        }

        // consume timers
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;

        // arm dip for jump
        landImpulse -= armJumpDip;
    }

    void ApplyJumpCut()
    {
        var v = rb.linearVelocity;
        if (v.y > 0f) { v.y *= jumpCutMultiplier; rb.linearVelocity = v; }
    }
    #endregion

    #region Stamina
    void StaminaTick()
    {
        if (staminaCooldownTimer > 0f) staminaCooldownTimer -= Time.deltaTime;

        if (IsSprinting && IsMoving)
        {
            stamina -= staminaDrainRate * Time.deltaTime;
            if (stamina <= 0f)
            {
                stamina = 0f;
                IsSprinting = false;
                staminaCooldownTimer = staminaCooldownAfterEmpty;
            }
        }
        else
        {
            stamina = Mathf.Min(maxStamina, stamina + staminaRegenRate * Time.deltaTime);
        }

        // Loop de respiración: ON cuando se está esprintando y moviendo, OFF si no.
        bool wantsBreath = IsSprinting && IsMoving;
        if (wantsBreath && !breathLoopActive)
        {
            AudioManager.Instance?.StartLoop(SFXId.PlayerBreath, handEyeSource, breathFadeIn, breathVolume, spatial: false);
            breathLoopActive = true;
        }
        else if (!wantsBreath && breathLoopActive)
        {
            AudioManager.Instance?.StopLoop(handEyeSource, breathFadeOut);
            breathLoopActive = false;
        }
    }

    public bool IsTired => staminaCooldownTimer > 0f || stamina < maxStamina * 0.25f;
    #endregion

    #region Head Bob
    void HeadBob()
    {
        if (!cameraTransform) return;
        float amp = 0f, freq = 0f;
        if (IsMoving)
        {
            if (IsCrouching) { amp = bobCrouchAmp; freq = bobCrouchFreq; }
            else if (IsSprinting) { amp = bobSprintAmp; freq = bobSprintFreq; }
            else { amp = bobWalkAmp; freq = bobWalkFreq; }
            if (IsTired && !IsCrouching) { amp = bobTiredAmp; freq = bobTiredFreq; }
        }

        if (amp > 0f)
        {
            bobTimer += Time.deltaTime * freq;
            float y = Mathf.Sin(bobTimer) * amp;
            float x = Mathf.Cos(bobTimer * 0.5f) * amp * 0.5f;
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition,
                cameraLocalStart + new Vector3(x, y, 0f), Time.deltaTime * bobReturnSpeed);
        }
        else
        {
            bobTimer = 0f;
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, cameraLocalStart, Time.deltaTime * bobReturnSpeed);
        }
    }
    #endregion

    #region Arm Sway
    void ArmSway()
    {
        if (!armRoot) return;

        // Mouse-driven overlap: arm lags behind look direction.
        Vector3 targetPosOffset = new Vector3(
            -lookInput.x * swayPosAmount * 0.5f,
            -lookInput.y * swayPosAmount * 0.5f,
            0f);

        // Strafe / forward planar accel pushes the arm
        Vector3 localAccel = transform.InverseTransformDirection(planarAccel);
        targetPosOffset += new Vector3(
            -localAccel.x * swayAccelKick * 0.02f,
            0f,
            -localAccel.z * swayAccelKick * 0.02f);

        // Walking bob on the arm (figure-8)
        float armAmp = 0f;
        if (IsMoving)
        {
            if (IsCrouching) armAmp = armBobAmpCrouch;
            else if (IsSprinting) armAmp = armBobAmpSprint;
            else armAmp = armBobAmpWalk;
        }
        float bx = Mathf.Cos(bobTimer * 0.5f) * armAmp;
        float by = Mathf.Sin(bobTimer) * armAmp * 0.7f;
        targetPosOffset += new Vector3(bx, by, 0f);

        // Vertical impulse from jump/landing
        targetPosOffset.y += landImpulse;

        // Rotational overlap (pitch/yaw/roll)
        Vector3 targetEulerOffset = new Vector3(
            lookInput.y * swayRotAmount * 0.5f,
            -lookInput.x * swayRotAmount * 0.5f,
            -moveInput.x * swayRotAmount * 0.4f + Mathf.Sin(bobTimer) * armBobRotAmp * (IsMoving ? 1f : 0f));

        // Smooth toward targets (lerp = lag / overlap)
        armCurrentPosOffset = Vector3.Lerp(armCurrentPosOffset, targetPosOffset, Time.deltaTime * swayLerpSpeed);
        armCurrentEulerOffset = Vector3.Lerp(armCurrentEulerOffset, targetEulerOffset, Time.deltaTime * swayLerpSpeed);

        armRoot.localPosition = armRestLocalPos + armCurrentPosOffset;
        armRoot.localEulerAngles = armRestLocalEuler + armCurrentEulerOffset;
    }
    #endregion

    #region Footsteps
    void FootstepTick()
    {
        if (!IsMoving) { stepTimer = 0f; return; }
        float interval = IsCrouching ? stepIntervalCrouch : (IsSprinting ? stepIntervalSprint : stepIntervalWalk);
        stepTimer += Time.deltaTime;
        if (stepTimer >= interval)
        {
            stepTimer = 0f;
            float vol = IsCrouching ? 0.3f : 1f;
            // Alterna entre las dos variantes (PlayerStepStone / PlayerStepStone2)
            // reproducidas sobre el AudioSource dedicado a los pies.
            AudioManager.Instance?.PlaySFXVariant(SFXId.PlayerStepStone, stepVariantToggle, transform.position, vol, stepsSource);
            stepVariantToggle = 1 - stepVariantToggle;
            EnemyDetection.NotifyNoise(transform.position, IsSprinting ? 1f : (IsCrouching ? 0.15f : 0.5f));
        }
    }
    #endregion

    #region Animation
    /// <summary>
    /// Dispara el trigger indicado SIMULTÁNEAMENTE en el Animator de la mano y en el del ojo.
    /// Ambos AnimatorController comparten los parámetros (Draw, ReverseDraw) y la lógica de
    /// transiciones desde Any State, así que con un único disparo por frame se mantienen
    /// alineados sin necesidad de re-sync.
    /// </summary>
    void AnimationHandle(int triggerHash)
    {
        // Mano (Armature)
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.ResetTrigger(HashDraw);
            anim.ResetTrigger(HashReverseDraw);
            anim.SetTrigger(triggerHash);
        }

        // Ojo (Sphere) — mismo trigger, mismo frame.
        if (sphereAnim != null && sphereAnim.runtimeAnimatorController != null)
        {
            sphereAnim.ResetTrigger(HashDraw);
            sphereAnim.ResetTrigger(HashReverseDraw);
            sphereAnim.SetTrigger(triggerHash);
        }
    }

    public void DrawWeapon()
    {
        AnimationHandle(HashDraw);
        HandDrawn = true;
        // HandDraw SFX descartado.
    }
    public void StoreWeapon()
    {
        AnimationHandle(HashReverseDraw);
        HandDrawn = false;
        // HandStore SFX descartado.

        // Si guardamos la mano mientras estamos en la cámara enemiga, volvemos a la del player.
        if (SpyCamController.Instance != null && SpyCamController.Instance.IsActive)
            SpyCamController.Instance.Toggle();
    }
    public void ToggleHand()
    {
        if (HandDrawn) StoreWeapon();
        else DrawWeapon();
    }

    void UpdateNoneStateMesh()
    {
        if (noneStateMeshes == null || noneStateMeshes.Length == 0) return;
        if (anim == null || anim.runtimeAnimatorController == null) return;
        var info = anim.GetCurrentAnimatorStateInfo(animatorLayer);
        bool isNone = info.IsName(noneStateName);
        for (int i = 0; i < noneStateMeshes.Length; i++)
            if (noneStateMeshes[i]) noneStateMeshes[i].enabled = !isNone;
    }
    #endregion

    #region Actions
    void TryInteract()
    {
        var origin = camHolder.transform.position;
        var dir = camHolder.transform.forward;
        if (Physics.SphereCast(origin, interactRadius, dir, out var hit, interactRange, interactMask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.CompareTag("Interactable"))
            {
                var interact = hit.collider.GetComponentInParent<Interactable>();
                if (interact) interact.Interact();
            }
        }
    }

    void ToggleLamp()
    {
        LampOn = !LampOn;
        if (lampObject) lampObject.SetActive(LampOn);
    }
    #endregion

    #region Input Methods
    public void OnMove(InputAction.CallbackContext ctx) { moveInput = ctx.ReadValue<Vector2>(); }
    public void OnLook(InputAction.CallbackContext ctx) { lookInput = ctx.ReadValue<Vector2>(); }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            jumpBufferTimer = jumpBufferTime;
            // immediate try (coyoteTimer is fresh while grounded)
            if (coyoteTimer > 0f) TryConsumeJump();
        }
        else if (ctx.canceled)
        {
            ApplyJumpCut();
        }
    }

    public void OnCrouch(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        IsCrouching = !IsCrouching;
        if (IsCrouching) IsSprinting = false;
        // PlayerCrouch SFX descartado.
    }
    public void OnSprint(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && !IsCrouching && staminaCooldownTimer <= 0f && stamina > 0.1f) IsSprinting = true;
        if (ctx.canceled) IsSprinting = false;
    }
    public void OnPause(InputAction.CallbackContext ctx) { if (ctx.performed) UIManager.Instance?.TogglePause(); }
    public void OnInteract(InputAction.CallbackContext ctx) { if (ctx.performed) TryInteract(); }
    public void OnLamp(InputAction.CallbackContext ctx) { if (ctx.performed) ToggleLamp(); }
    public void OnReload(InputAction.CallbackContext ctx) { /* hook reload */ }
    public void OnShoot(InputAction.CallbackContext ctx) { /* hook shoot */ }
    public void OnToggleHand(InputAction.CallbackContext ctx) { if (ctx.performed) ToggleHand(); }

    /// <summary>
    /// Solo permite alternar a la cámara enemiga si el jugador tiene la mano sacada.
    /// Si ya está en cámara enemiga, siempre se puede volver (Toggle apaga).
    /// </summary>
    public void OnSwitchCamera(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        var spy = SpyCamController.Instance;
        if (spy == null) return;

        // Si ya estamos en la cámara enemiga, dejar volver siempre.
        if (spy.IsActive) { spy.Toggle(); return; }

        // Para ACTIVAR, exigir mano sacada.
        if (!HandDrawn) return;
        spy.Toggle();
    }
    #endregion

    bool InputBlocked => SpyCamController.Instance != null && (SpyCamController.Instance.IsActive || SpyCamController.Instance.IsTransitioning);

    #region Gizmos
    void OnDrawGizmosSelected()
    {
        if (camHolder == null) return;

        var origin = camHolder.transform.position;
        var dir    = camHolder.transform.forward;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin, dir * interactRange);
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawWireSphere(origin + dir * interactRange, interactRadius);

        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
    #endregion
}

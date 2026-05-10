using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    #region Inspector
    [Header("Movement & Look")]
    [SerializeField] GameObject camHolder;
    [SerializeField] Transform cameraTransform;
    [SerializeField] float walkSpeed = 5f;
    [SerializeField] float sprintSpeed = 8f;
    [SerializeField] float crouchSpeed = 3f;
    [SerializeField] float maxForce = 1f;
    [SerializeField] float sensitivity = 0.1f;

    [Header("Jump & GroundCheck")]
    [SerializeField] float jumpForce = 5f;
    [SerializeField] Transform groundCheck;
    [SerializeField] float groundCheckRadius = 0.3f;
    [SerializeField] LayerMask groundLayer;

    [Header("Crouch")]
    [SerializeField] float standHeight = 1.7f;
    [SerializeField] float crouchHeight = 1.0f;
    [SerializeField] float crouchLerpSpeed = 8f;
    [SerializeField] CapsuleCollider playerCollider;

    [Header("Sprint Cooldown")]
    [SerializeField] float maxStamina = 5f;
    [SerializeField] float staminaRegenRate = 1f;
    [SerializeField] float staminaDrainRate = 1f;
    [SerializeField] float staminaCooldownAfterEmpty = 2f;

    [Header("Head Bob")]
    [SerializeField] float bobWalkAmp = 0.05f;
    [SerializeField] float bobWalkFreq = 8f;
    [SerializeField] float bobSprintAmp = 0.09f;
    [SerializeField] float bobSprintFreq = 13f;
    [SerializeField] float bobCrouchAmp = 0.025f;
    [SerializeField] float bobCrouchFreq = 5f;
    [SerializeField] float bobTiredAmp = 0.11f;
    [SerializeField] float bobTiredFreq = 6f;
    [SerializeField] float bobReturnSpeed = 6f;

    [Header("Interaction")]
    [SerializeField] float interactRange = 2.5f;
    [SerializeField] float interactRadius = 0.25f;
    [SerializeField] LayerMask interactMask = ~0;

    [Header("Animator None-State Mesh")]
    [Tooltip("Mesh that's hidden while AC_Player is in 'None' state (hands/weapon).")]
    [SerializeField] Renderer[] noneStateMeshes;
    [SerializeField] string noneStateName = "None";
    [SerializeField] int animatorLayer = 0;

    [Header("Lamp")]
    [SerializeField] GameObject lampObject;
    public bool LampOn { get; private set; }

    [Header("SFX timings")]
    [SerializeField] float stepIntervalWalk = 0.5f;
    [SerializeField] float stepIntervalSprint = 0.32f;
    [SerializeField] float stepIntervalCrouch = 0.75f;
    #endregion

    Rigidbody rb;
    Animator anim;
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
    bool wasGrounded;

    // Animator triggers (use coroutines so we can both Set & Reset cleanly)
    static readonly int HashWalking   = Animator.StringToHash("Walking");
    static readonly int HashRunning   = Animator.StringToHash("Running");
    static readonly int HashCrouching = Animator.StringToHash("Crouching");
    static readonly int HashDraw      = Animator.StringToHash("Draw");
    static readonly int HashReverseDraw = Animator.StringToHash("ReverseDraw");

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        if (cameraTransform == null && camHolder != null) cameraTransform = camHolder.transform;
        if (cameraTransform) cameraLocalStart = cameraTransform.localPosition;
        stamina = maxStamina;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (lampObject) lampObject.SetActive(LampOn);
    }

    void Update()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);

        if (!wasGrounded && isGrounded)
            AudioManager.Instance?.PlaySFX(SFXId.PlayerFall, transform.position);
        wasGrounded = isGrounded;

        StaminaTick();
        AnimationHandle();
        HeadBob();
        FootstepTick();
        UpdateNoneStateMesh();
        Debug.DrawRay(camHolder.transform.position, camHolder.transform.forward * interactRange, Color.red);
    }

    void FixedUpdate() { Movement(); }

    void LateUpdate() { CameraLook(); }

    #region Movement / Look
    void CameraLook()
    {
        transform.Rotate(Vector3.up * lookInput.x * sensitivity);
        lookRotation += -lookInput.y * sensitivity;
        lookRotation = Mathf.Clamp(lookRotation, -90f, 90f);
        camHolder.transform.localEulerAngles = new Vector3(lookRotation, 0f, 0f);
    }

    void Movement()
    {
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 targetVelocity = new Vector3(moveInput.x, 0, moveInput.y);
        float spd = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
        targetVelocity *= spd;
        targetVelocity = transform.TransformDirection(targetVelocity);

        Vector3 velocityChange = targetVelocity - currentVelocity;
        velocityChange.y = 0f;
        velocityChange = Vector3.ClampMagnitude(velocityChange, maxForce);
        rb.AddForce(velocityChange, ForceMode.VelocityChange);

        // Smooth crouch height
        if (playerCollider)
        {
            float targetH = IsCrouching ? crouchHeight : standHeight;
            playerCollider.height = Mathf.Lerp(playerCollider.height, targetH, Time.fixedDeltaTime * crouchLerpSpeed);
            var c = playerCollider.center; c.y = playerCollider.height * 0.5f; playerCollider.center = c;
        }
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
                AudioManager.Instance?.PlaySFX(SFXId.PlayerBreathHard, transform.position);
            }
        }
        else
        {
            stamina = Mathf.Min(maxStamina, stamina + staminaRegenRate * Time.deltaTime);
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

    #region Footsteps
    void FootstepTick()
    {
        if (!IsMoving) { stepTimer = 0f; return; }
        float interval = IsCrouching ? stepIntervalCrouch : (IsSprinting ? stepIntervalSprint : stepIntervalWalk);
        stepTimer += Time.deltaTime;
        if (stepTimer >= interval)
        {
            stepTimer = 0f;
            AudioManager.Instance?.PlaySFX(SFXId.PlayerStepStone, transform.position, IsCrouching ? 0.3f : 1f);
            EnemyDetection.NotifyNoise(transform.position, IsSprinting ? 1f : (IsCrouching ? 0.15f : 0.5f));
        }
    }
    #endregion

    #region Animation
    void AnimationHandle()
    {
        anim.SetBool(HashWalking, IsMoving && !IsSprinting && !IsCrouching);
        anim.SetBool(HashRunning, IsMoving && IsSprinting && !IsCrouching);
        anim.SetBool(HashCrouching, IsCrouching);
    }

    IEnumerator FireTrigger(int triggerHash)
    {
        anim.ResetTrigger(triggerHash);
        anim.SetTrigger(triggerHash);
        yield return null;
    }

    public void DrawWeapon() { StartCoroutine(FireTrigger(HashDraw)); AudioManager.Instance?.PlaySFX(SFXId.HandDraw, transform.position); }
    public void StoreWeapon() { StartCoroutine(FireTrigger(HashReverseDraw)); AudioManager.Instance?.PlaySFX(SFXId.HandStore, transform.position); }

    void UpdateNoneStateMesh()
    {
        if (noneStateMeshes == null || noneStateMeshes.Length == 0) return;
        var info = anim.GetCurrentAnimatorStateInfo(animatorLayer);
        bool isNone = info.IsName(noneStateName);
        for (int i = 0; i < noneStateMeshes.Length; i++)
            if (noneStateMeshes[i]) noneStateMeshes[i].enabled = !isNone;
    }
    #endregion

    #region Actions
    void Jump()
    {
        if (!isGrounded) return;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

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
    public void OnJump(InputAction.CallbackContext ctx) { if (ctx.performed) Jump(); }
    public void OnCrouch(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        IsCrouching = !IsCrouching;
        if (IsCrouching) IsSprinting = false;
        AudioManager.Instance?.PlaySFX(SFXId.PlayerCrouch, transform.position);
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
    #endregion
}

using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class IronStormMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Tốc độ cơ bản dùng cho đi bộ và đi ngồi (chia sẻ chung một biến).")]
    [SerializeField] private float normalSpeed = 4f;

    [Tooltip("Tốc độ khi chạy nhanh của siêu chiến binh.")]
    [SerializeField] private float runSpeed = 7f;

    [Tooltip("Độ mượt khi tăng/giảm tốc ngang (nhỏ = phản hồi nhanh, lớn = trượt quán tính êm hơn).")]
    [SerializeField] private float movementSmoothTime = 0.08f;

    [Tooltip("Hệ số phanh khi thả phím di chuyển trên mặt đất (cao hơn = dừng gọn, không trôi lê thê).")]
    [SerializeField] private float groundStopDamping = 14f;

    [Tooltip("Hệ số điều khiển hướng khi đang ở trên không (0 = khóa hướng, 1 = lái đầy đủ).")]
    [Range(0f, 1f)]
    [SerializeField] private float airControlFactor = 0.75f;

    [Tooltip("Độ cao nhảy của nhân vật.")]
    [SerializeField] private float jumpHeight = 1.2f;

    [Tooltip("Gia tốc trọng lực kéo nhân vật xuống.")]
    [SerializeField] private float gravity = -9.81f;

    [Header("Dash (Né / Tốc Biến) Settings")]
    [Tooltip("Tốc độ lao đi khi thực hiện né/tốc biến.")]
    [SerializeField] private float dashSpeed = 25f;

    [Tooltip("Khoảng cách tối đa của cú tốc biến (mét).")]
    [SerializeField] private float dashDistance = 5f;

    [Tooltip("Thời gian hồi chiêu trước khi được dùng né tiếp theo (giây).")]
    [SerializeField] private float dashCooldown = 2f;

    [Tooltip("Cho phép thực hiện Dash khi đang nhảy / trên không.")]
    [SerializeField] private bool allowAirDash = true;

    [Tooltip("Cho phép thực hiện Dash khi đang bay bằng Jump Pack.")]
    [SerializeField] private bool allowJumpPackDash = true;

    [Tooltip("Hệ số duy trì vận tốc rơi khi Dash trên không (0 = giữ ngang, 0.3 = rơi nhẹ).")]
    [Range(0f, 1f)]
    [SerializeField] private float airDashVerticalCarry = 0.25f;

    [Header("Crouch Settings")]
    [Tooltip("Chiều cao mặc định của CharacterController khi đứng.")]
    [SerializeField] private float defaultHeight = 2.0f;

    [Tooltip("Chiều cao của CharacterController khi ngồi xuống.")]
    [SerializeField] private float crouchHeight = 1.0f;

    [Tooltip("Tốc độ chuyển đổi mượt mà giữa trạng thái đứng và ngồi.")]
    [SerializeField] private float crouchTransitionSpeed = 10f;

    [Header("Audio Settings")]
    [Tooltip("AudioSource phát tiếng bước chân.")]
    [SerializeField] private AudioSource footstepAudioSource;

    [Tooltip("File âm thanh tiếng bước chân.")]
    [SerializeField] private AudioClip footstepClip;

    [Range(0f, 1f)]
    [Tooltip("Thanh trượt điều chỉnh âm lượng tiếng bước chân (Từ 0 đến 1).")]
    [SerializeField] private float footstepVolume = 1.0f;

    [Tooltip("Độ biến thiên cao độ (pitch) ngẫu nhiên để tiếng bước chân nghe tự nhiên không bị nhàm chán.")]
    [Range(0.0f, 0.3f)]
    [SerializeField] private float pitchRandomness = 0.1f;

    [Tooltip("Khoảng thời gian giữa các bước chân khi đi bộ.")]
    [SerializeField] private float walkStepInterval = 0.5f;

    [Tooltip("Khoảng thời gian giữa các bước chân khi chạy.")]
    [SerializeField] private float runStepInterval = 0.3f;

    [Header("Input Action References")]
    [Tooltip("Nhận dữ liệu hướng đi từ bàn phím/Gamepad (Vector2).")]
    [SerializeField] private InputActionReference moveAction;

    [Tooltip("Nút kích hoạt hành động nhảy.")]
    [SerializeField] private InputActionReference jumpAction;

    [Tooltip("Nút kích hoạt hành động chạy nhanh.")]
    [SerializeField] private InputActionReference sprintAction;

    [Tooltip("Nút kích hoạt hành động ngồi xuống (giữ phím để ngồi).")]
    [SerializeField] private InputActionReference crouchAction;

    [Tooltip("Nút kích hoạt hành động né / tốc biến (gán phím V trên Input Action).")]
    [SerializeField] private InputActionReference dashAction;

    private CharacterController controller;
    private IronStormSlide slideScript;
    private Vector3 velocity;
    private Vector3 currentMoveVelocity;
    private Vector3 smoothVelocityRef;
    private Vector3 defaultCenter;
    private bool isGrounded;
    private float groundGraceTimer;
    private bool isCrouching;
    private float stepTimer;

    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection;

    public bool IsSprintHeldNow
    {
        get
        {
            if (sprintAction != null && sprintAction.action != null)
            {
                if (!sprintAction.action.enabled) sprintAction.action.Enable();
                if (sprintAction.action.IsPressed()) return true;
            }
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
                return true;
            return Input.GetKey(KeyCode.LeftShift);
        }
    }

    public bool IsRunning => IsSprintHeldNow && !isCrouching && !IsSliding && !isDashing;
    public bool IsCrouching => isCrouching;
    public bool IsSliding => slideScript != null && slideScript.IsSliding;
    public bool IsGrounded => isGrounded || groundGraceTimer > 0f;
    public bool IsDashing => isDashing;
    public bool CanDashNow => !isDashing && dashCooldownTimer <= 0f;
    public float AirDashVerticalCarry => airDashVerticalCarry;
    public float DefaultHeight => Mathf.Max(0.1f, defaultHeight);
    public float CrouchHeight => Mathf.Max(0.1f, crouchHeight);
    public float CurrentBodyHeight => controller != null ? Mathf.Max(0.1f, controller.height) : DefaultHeight;
    public float HeightRatio => Mathf.Clamp01(CurrentBodyHeight / DefaultHeight);
    public float HorizontalSpeed => new Vector3(currentMoveVelocity.x, 0f, currentMoveVelocity.z).magnitude;
    public float RunSpeed => runSpeed;
    public Vector3 DefaultCenter => defaultCenter;

    public Vector2 CurrentInputDirection
    {
        get
        {
            if (moveAction != null && moveAction.action != null)
            {
                if (!moveAction.action.enabled) moveAction.action.Enable();
                Vector2 val = moveAction.action.ReadValue<Vector2>();
                if (val.sqrMagnitude > 0.0025f) return val;
            }
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        slideScript = GetComponent<IronStormSlide>();
        if (controller != null)
        {
            defaultHeight = controller.height;
            defaultCenter = controller.center;
        }
    }

    private void OnEnable()
    {
        if (moveAction != null && moveAction.action != null) moveAction.action.Enable();
        if (jumpAction != null && jumpAction.action != null) jumpAction.action.Enable();
        if (sprintAction != null && sprintAction.action != null) sprintAction.action.Enable();
        if (crouchAction != null && crouchAction.action != null) crouchAction.action.Enable();
        if (dashAction != null && dashAction.action != null) dashAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null && moveAction.action != null) moveAction.action.Disable();
        if (jumpAction != null && jumpAction.action != null) jumpAction.action.Disable();
        if (sprintAction != null && sprintAction.action != null) sprintAction.action.Disable();
        if (crouchAction != null && crouchAction.action != null) crouchAction.action.Disable();
        if (dashAction != null && dashAction.action != null) dashAction.action.Disable();
    }

    private void Start()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();
        if (slideScript == null)
            slideScript = GetComponent<IronStormSlide>();

        if (controller != null)
        {
            defaultHeight = controller.height;
            defaultCenter = controller.center;
        }
    }

    public bool WasJumpTriggered()
    {
        if (jumpAction != null && jumpAction.action != null)
        {
            if (!jumpAction.action.enabled) jumpAction.action.Enable();
            if (jumpAction.action.triggered) return true;
        }
        return Input.GetKeyDown(KeyCode.Space);
    }

    public bool IsCrouchHeld()
    {
        if (crouchAction != null && crouchAction.action != null)
        {
            if (!crouchAction.action.enabled) crouchAction.action.Enable();
            if (crouchAction.action.IsPressed()) return true;
        }
        if (Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed)
            return true;
        return Input.GetKey(KeyCode.LeftControl);
    }

    public bool WasCrouchJustPressed()
    {
        if (crouchAction != null && crouchAction.action != null)
        {
            if (!crouchAction.action.enabled) crouchAction.action.Enable();
            if (crouchAction.action.WasPressedThisFrame()) return true;
        }
        if (Keyboard.current != null && Keyboard.current.leftCtrlKey.wasPressedThisFrame)
            return true;
        return Input.GetKeyDown(KeyCode.LeftControl);
    }

    public bool WasDashTriggered()
    {
        if (dashAction != null && dashAction.action != null)
        {
            if (!dashAction.action.enabled) dashAction.action.Enable();
            return dashAction.action.WasPressedThisFrame() || dashAction.action.triggered;
        }

        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            return true;
        }

        return Input.GetKeyDown(KeyCode.V);
    }

    public void SetCrouchStateFromSlide(bool crouch)
    {
        isCrouching = crouch;
    }

    public void OverrideHorizontalVelocityFromSlide(Vector3 newVelocity)
    {
        currentMoveVelocity = newVelocity;
        smoothVelocityRef = Vector3.zero;
    }

    public bool TryStepJumpPackDash(float deltaTime, Vector3 preferredDirection)
    {
        if (!allowJumpPackDash) return false;

        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - deltaTime);
        }

        if (!isDashing && WasDashTriggered() && dashCooldownTimer <= 0f)
        {
            if (slideScript != null)
                slideScript.CancelSlide();

            Vector3 dir = preferredDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
            {
                dir = transform.forward;
                dir.y = 0f;
            }

            isDashing = true;
            dashCooldownTimer = dashCooldown;
            dashTimer = dashDistance / Mathf.Max(0.1f, dashSpeed);
            dashDirection = dir.normalized;
        }

        if (!isDashing) return false;

        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (controller != null && controller.enabled)
        {
            controller.Move(dashDirection * dashSpeed * deltaTime);
        }

        dashTimer -= deltaTime;
        if (dashTimer <= 0f)
        {
            isDashing = false;
        }

        return true;
    }

    private void Update()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();
        if (controller == null || !controller.enabled)
            return;

        if (slideScript == null)
            slideScript = GetComponent<IronStormSlide>();

        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        if (dashCooldownTimer > 0f)
            dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - dt);

        isGrounded = controller.isGrounded;
        if (isGrounded)
        {
            groundGraceTimer = 0.12f;
            if (velocity.y < 0f)
            {
                velocity.y = -2f;
            }
        }
        else if (groundGraceTimer > 0f)
        {
            groundGraceTimer = Mathf.Max(0f, groundGraceTimer - dt);
        }

        bool groundedForMovement = isGrounded || groundGraceTimer > 0f;

        if (slideScript != null)
        {
            slideScript.NotifyRunJumpState(groundedForMovement, IsRunning);
        }

        Vector2 inputDir = CurrentInputDirection;
        if (inputDir.sqrMagnitude < 0.0025f)
        {
            inputDir = Vector2.zero;
        }

        Vector3 move = transform.right * inputDir.x + transform.forward * inputDir.y;
        move.y = 0f;
        move = Vector3.ClampMagnitude(move, 1f);

        bool canDashState = groundedForMovement || allowAirDash;
        if (canDashState && WasDashTriggered() && !isDashing && dashCooldownTimer <= 0f)
        {
            if (slideScript != null)
                slideScript.CancelSlide();

            isDashing = true;
            dashCooldownTimer = dashCooldown;
            dashTimer = dashDistance / Mathf.Max(0.1f, dashSpeed);
            dashDirection = move.sqrMagnitude > 0.01f ? move.normalized : transform.forward;
            currentMoveVelocity = dashDirection * dashSpeed;
            smoothVelocityRef = Vector3.zero;

            if (!groundedForMovement)
            {
                velocity.y *= airDashVerticalCarry;
            }
        }

        if (isDashing)
        {
            controller.Move(dashDirection * dashSpeed * dt);
            dashTimer -= dt;
            if (!groundedForMovement)
            {
                velocity.y += gravity * airDashVerticalCarry * dt;
                controller.Move(new Vector3(0f, velocity.y * dt, 0f));
            }
            if (dashTimer <= 0f)
            {
                isDashing = false;
                currentMoveVelocity = Vector3.zero;
                smoothVelocityRef = Vector3.zero;
            }
            return;
        }

        bool slidingNow = slideScript != null && slideScript.StepSlide(move, groundedForMovement, dt);
        bool crouchLockedBySlide = slidingNow || (slideScript != null && slideScript.IsCrouchLockedBySlide);

        if (crouchLockedBySlide)
        {
            isCrouching = false;
        }

        if (!slidingNow)
        {
            bool blockCrouchForSlide = crouchLockedBySlide ||
                                       (slideScript != null && slideScript.CanSlideNow(move, groundedForMovement));
            isCrouching = !blockCrouchForSlide && IsCrouchHeld();

            bool isRunning = IsRunning;
            float currentSpeed = isRunning ? runSpeed : normalSpeed;
            if (isCrouching)
            {
                currentSpeed *= 0.5f;
            }

            float effectiveSmoothTime = groundedForMovement
                ? movementSmoothTime
                : Mathf.Max(movementSmoothTime, movementSmoothTime / Mathf.Max(0.15f, airControlFactor));

            if (inputDir == Vector2.zero)
            {
                float dampingRate = groundedForMovement ? groundStopDamping : groundStopDamping * 0.25f;
                float blend = 1f - Mathf.Exp(-dampingRate * dt);
                currentMoveVelocity = Vector3.Lerp(currentMoveVelocity, Vector3.zero, blend);
                smoothVelocityRef = Vector3.zero;

                if (currentMoveVelocity.sqrMagnitude < 0.0004f)
                {
                    currentMoveVelocity = Vector3.zero;
                }
            }
            else
            {
                Vector3 targetMoveVelocity = move * currentSpeed;
                currentMoveVelocity = Vector3.SmoothDamp(
                    currentMoveVelocity,
                    targetMoveVelocity,
                    ref smoothVelocityRef,
                    effectiveSmoothTime,
                    Mathf.Infinity,
                    dt
                );
            }
        }

        float targetHeight = defaultHeight;
        if (slidingNow && slideScript != null)
            targetHeight = slideScript.SlideHeight;
        else if (isCrouching)
            targetHeight = crouchHeight;

        float heightBlend = 1f - Mathf.Exp(-crouchTransitionSpeed * dt);
        controller.height = Mathf.Lerp(controller.height, targetHeight, heightBlend);

        float heightDelta = defaultHeight - controller.height;
        controller.center = new Vector3(defaultCenter.x, defaultCenter.y - heightDelta * 0.5f, defaultCenter.z);

        if (currentMoveVelocity.sqrMagnitude > 0.000001f)
        {
            controller.Move(currentMoveVelocity * dt);
        }

        bool isMoving = inputDir.sqrMagnitude > 0.01f && groundedForMovement && !slidingNow;
        if (isMoving)
        {
            stepTimer -= dt;
            float currentInterval = IsRunning ? runStepInterval : walkStepInterval;
            if (isCrouching) currentInterval *= 1.5f;

            if (stepTimer <= 0f)
            {
                if (footstepAudioSource != null && footstepClip != null)
                {
                    float basePitch = isCrouching ? 0.8f : 1.0f;
                    footstepAudioSource.pitch = basePitch + UnityEngine.Random.Range(-pitchRandomness, pitchRandomness);
                    footstepAudioSource.PlayOneShot(footstepClip, footstepVolume);
                }
                stepTimer = currentInterval;
            }
        }
        else if (!slidingNow)
        {
            stepTimer = 0f;
        }

        if (WasJumpTriggered() && groundedForMovement && (!isCrouching || slidingNow))
        {
            if (slideScript != null)
                slideScript.CancelSlide();
            isCrouching = false;
            groundGraceTimer = 0f;
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * dt;
        controller.Move(new Vector3(0f, velocity.y * dt, 0f));
    }
}

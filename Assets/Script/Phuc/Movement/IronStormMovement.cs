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

    [Tooltip("Nút kích hoạt hành động ngồi xuống.")]
    [SerializeField] private InputActionReference crouchAction;

    [Tooltip("Nút kích hoạt hành động né / tốc biến (Gợi ý: Left Alt hoặc V).")]
    [SerializeField] private InputActionReference dashAction;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private bool isCrouching;
    private float stepTimer;

    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection;

    public bool IsRunning
    {
        get
        {
            bool sprintPressed = sprintAction != null && sprintAction.action != null
                ? sprintAction.action.IsPressed()
                : Input.GetKey(KeyCode.LeftShift);
            return sprintPressed && !isCrouching && !isDashing;
        }
    }

    public bool IsCrouching => isCrouching;
    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    public bool CanDashNow => !isDashing && dashCooldownTimer <= 0f;
    public float AirDashVerticalCarry => airDashVerticalCarry;

    public Vector2 CurrentInputDirection
    {
        get
        {
            if (moveAction != null && moveAction.action != null)
                return moveAction.action.ReadValue<Vector2>();
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
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
        controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            defaultHeight = controller.height;
        }
    }

    public bool WasJumpTriggered()
    {
        if (jumpAction != null && jumpAction.action != null)
            return jumpAction.action.triggered;
        return Input.GetKeyDown(KeyCode.Space);
    }

    public bool WasCrouchTriggered()
    {
        if (crouchAction != null && crouchAction.action != null)
            return crouchAction.action.triggered;
        return Input.GetKeyDown(KeyCode.LeftControl);
    }

    public bool WasDashTriggered()
    {
        if (dashAction != null && dashAction.action != null)
        {
            if (!dashAction.action.enabled) dashAction.action.Enable();
            if (dashAction.action.WasPressedThisFrame() || dashAction.action.triggered)
                return true;
        }

        if (Keyboard.current != null &&
            (Keyboard.current.leftAltKey.wasPressedThisFrame || Keyboard.current.vKey.wasPressedThisFrame))
        {
            return true;
        }

        return Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.V);
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

        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        Vector2 inputDir = CurrentInputDirection;
        Vector3 move = transform.right * inputDir.x + transform.forward * inputDir.y;
        move = Vector3.ClampMagnitude(move, 1f);

        bool canDashState = isGrounded || allowAirDash;
        if (canDashState && WasDashTriggered() && !isDashing && dashCooldownTimer <= 0f)
        {
            isDashing = true;
            dashCooldownTimer = dashCooldown;
            dashTimer = dashDistance / Mathf.Max(0.1f, dashSpeed);
            dashDirection = move.sqrMagnitude > 0.01f ? move.normalized : transform.forward;
            if (!isGrounded)
            {
                velocity.y *= airDashVerticalCarry;
            }
        }

        if (isDashing)
        {
            controller.Move(dashDirection * dashSpeed * Time.deltaTime);
            dashTimer -= Time.deltaTime;
            if (!isGrounded)
            {
                velocity.y += gravity * airDashVerticalCarry * Time.deltaTime;
                controller.Move(new Vector3(0f, velocity.y * Time.deltaTime, 0f));
            }
            if (dashTimer <= 0f)
            {
                isDashing = false;
            }
            return;
        }

        if (WasCrouchTriggered())
        {
            isCrouching = !isCrouching;
        }
        float targetHeight = isCrouching ? crouchHeight : defaultHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);

        bool isRunning = IsRunning;
        float currentSpeed = isRunning ? runSpeed : normalSpeed;
        if (isCrouching)
        {
            currentSpeed *= 0.5f;
        }

        controller.Move(move * currentSpeed * Time.deltaTime);

        bool isMoving = inputDir.sqrMagnitude > 0.01f && isGrounded;
        if (isMoving)
        {
            stepTimer -= Time.deltaTime;
            float currentInterval = isRunning ? runStepInterval : walkStepInterval;
            if (isCrouching) currentInterval *= 1.5f;

            if (stepTimer <= 0f)
            {
                if (footstepAudioSource != null && footstepClip != null)
                {
                    float basePitch = isCrouching ? 0.8f : 1.0f;
                    footstepAudioSource.pitch = basePitch + Random.Range(-pitchRandomness, pitchRandomness);
                    footstepAudioSource.PlayOneShot(footstepClip, footstepVolume);
                }
                stepTimer = currentInterval;
            }
        }
        else
        {
            stepTimer = 0f;
        }

        if (WasJumpTriggered() && isGrounded && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
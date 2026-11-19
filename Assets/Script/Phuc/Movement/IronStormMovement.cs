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

    // Các biến quản lý trạng thái tốc biến
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection;

    // Các thuộc tính cung cấp trạng thái cho Camera đọc
    public bool IsRunning => sprintAction.action.IsPressed() && !isCrouching && !isDashing;
    public bool IsCrouching => isCrouching;
    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    public Vector2 CurrentInputDirection => moveAction.action.ReadValue<Vector2>();

    private void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
        sprintAction.action.Enable();
        crouchAction.action.Enable();
        if (dashAction != null) dashAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
        jumpAction.action.Disable();
        sprintAction.action.Disable();
        crouchAction.action.Disable();
        if (dashAction != null) dashAction.action.Disable();
    }

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        defaultHeight = controller.height;
    }

    private void Update()
    {
        // Đếm ngược thời gian hồi chiêu né
        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        // Kiểm tra trạng thái chạm đất để reset lực rơi
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Đọc giá trị đầu vào từ New Input System và chuẩn hóa vector đi chéo
        Vector2 inputDir = moveAction.action.ReadValue<Vector2>();
        Vector3 move = transform.right * inputDir.x + transform.forward * inputDir.y;
        move = Vector3.ClampMagnitude(move, 1f);

        // Kích hoạt trạng thái né (Tốc biến) khi bấm phím và đã hết thời gian hồi
        if (dashAction != null && dashAction.action.triggered && !isDashing && dashCooldownTimer <= 0f)
        {
            isDashing = true;
            dashCooldownTimer = dashCooldown;
            dashTimer = dashDistance / dashSpeed;
            dashDirection = move.sqrMagnitude > 0.01f ? move.normalized : transform.forward;
        }

        // Nếu đang trong trạng thái tốc biến thì ưu tiên lướt đi và bỏ qua di chuyển thường
        if (isDashing)
        {
            controller.Move(dashDirection * dashSpeed * Time.deltaTime);
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
            }
            return;
        }

        // Xử lý chuyển đổi trạng thái ngồi và nội suy chiều cao mượt mà
        if (crouchAction.action.triggered)
        {
            isCrouching = !isCrouching;
        }
        float targetHeight = isCrouching ? crouchHeight : defaultHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);

        // Xác định tốc độ hiện tại dựa trên trạng thái chạy hoặc ngồi
        bool isRunning = IsRunning;
        float currentSpeed = isRunning ? runSpeed : normalSpeed;
        if (isCrouching)
        {
            currentSpeed *= 0.5f;
        }

        // Thực hiện di chuyển phương ngang
        controller.Move(move * currentSpeed * Time.deltaTime);

        // Xử lý âm thanh tiếng bước chân khi di chuyển
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
                    // Giữ nguyên cao độ chuẩn, chỉ tạo độ lệch ngẫu nhiên nhỏ (pitchRandomness) để không bị nhàm chán
                    float basePitch = isCrouching ? 0.8f : 1.0f;
                    footstepAudioSource.pitch = basePitch + Random.Range(-pitchRandomness, pitchRandomness);
                    // Phát âm thanh với mức âm lượng được chỉnh bằng slider (footstepVolume)
                    footstepAudioSource.PlayOneShot(footstepClip, footstepVolume);
                }
                stepTimer = currentInterval;
            }
        }
        else
        {
            stepTimer = 0f;
        }

        // Xử lý hành động nhảy
        if (jumpAction.action.triggered && isGrounded && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Tính toán và áp dụng trọng lực theo thời gian
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
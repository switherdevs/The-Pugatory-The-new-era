using UnityEngine;
using UnityEngine.InputSystem;

public class IronStormCamera : MonoBehaviour
{
    [Header("Mouse Settings")]
    [Tooltip("Độ nhạy của chuột khi xoay góc nhìn.")]
    [SerializeField] private float mouseSensitivity = 2f;

    [Tooltip("Giới hạn góc nhìn lên xuống tối đa (độ).")]
    [SerializeField] private float topClamp = 90f;

    [Tooltip("Giới hạn góc nhìn xuống dưới tối đa (độ).")]
    [SerializeField] private float bottomClamp = -90f;

    [Header("Recoil Settings (Độ giật & Hồi phục)")]
    [Tooltip("Tốc độ đá giật tức thời khi bóp cò (Càng cao giật càng đanh và gắt).")]
    [SerializeField] private float recoilSnappiness = 30f;
    [Tooltip("Tốc độ camera tự động kéo trả về vị trí ban đầu sau khi ngớt bắn (Số càng lớn hồi phục càng nhanh).")]
    [SerializeField] private float recoilReturnSpeed = 12f;
    [Tooltip("Giới hạn mức độ giật tối đa theo chiều dọc.")]
    [SerializeField] private float maxRecoilPitch = 15f;

    [Header("Movement Recoil Multipliers (Hệ số cộng dồn khi di chuyển/chạy)")]
    [Tooltip("Hệ số nhân độ giật khi vừa đi bộ vừa bắn.")]
    [SerializeField] private float walkRecoilMultiplier = 1.3f;
    [Tooltip("Hệ số nhân độ giật khi vừa chạy nhanh (Sprint) vừa bắn (rung mạnh hơn).")]
    [SerializeField] private float runRecoilMultiplier = 1.8f;

    [Header("Jump Pack Flight Shake & FOV Settings")]
    [Tooltip("Component Camera để chỉnh FOV khi bay (để trống sẽ tự lấy trên GameObject này).")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("Mức tăng FOV khi đang bay bằng Jump Pack (+15 mặc định).")]
    [SerializeField] private float flightFovIncrease = 15f;
    [Tooltip("Tốc độ chuyển đổi FOV khi bật/tắt bay.")]
    [SerializeField] private float fovTransitionSpeed = 10f;
    [Tooltip("Bật rung camera liên tục khi đang bay bằng Jump Pack.")]
    [SerializeField] private bool enableFlightShake = true;
    [Tooltip("Cường độ rung camera khi bay lơ lửng bình thường.")]
    [SerializeField] private float hoverShakeIntensity = 0.06f;
    [Tooltip("Cường độ rung camera khi giữ Shift bay nhanh.")]
    [SerializeField] private float boostShakeIntensity = 0.14f;
    [Tooltip("Tần số rung camera khi bay.")]
    [SerializeField] private float flightShakeFrequency = 24f;
    [Tooltip("Độ mượt khi chuyển cường độ rung lúc bật/tắt bay.")]
    [SerializeField] private float flightShakeSmoothness = 10f;

    [Header("Snake Cam (Head Bob) Settings")]
    [SerializeField] private bool enableSnakeCam = true;
    [Range(1f, 30f)]
    [SerializeField] private float transitionSmoothness = 15f;

    [Header("1. Snake Cam Idle (Đứng yên)")]
    [SerializeField] private float idleFrequency = 1.5f;
    [SerializeField] private float idleAmount = 0.02f;

    [Header("2. Snake Cam Movement & Jump")]
    [SerializeField] private float walkFrequency = 10f;
    [SerializeField] private float walkAmount = 0.05f;
    [SerializeField] private float runFrequency = 14f;
    [SerializeField] private float runAmount = 0.1f;
    [SerializeField] private float jumpSnakeAmount = 0.08f;

    [Header("Input Action Reference")]
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private Transform playerBody;

    private float xRotation = 0f;
    private float currentRecoilX = 0f;
    private float currentRecoilY = 0f;
    private float targetRecoilX = 0f;
    private float targetRecoilY = 0f;

    private float shakeTimer = 0f;
    private float shakeIntensity = 0f;
    private float bobTimer = 0f;
    private float flightShakeTimer = 0f;
    private float currentFlightShakeIntensity = 0f;

    private float baseFov = 60f;
    private bool isJumpPackFlying = false;
    private bool isJumpPackBoosting = false;

    private IronStormMovement movementRef;

    private float currentFrequency;
    private float currentAmount;

    private void OnEnable()
    {
        if (lookAction != null && lookAction.action != null)
        {
            lookAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (lookAction != null && lookAction.action != null)
        {
            lookAction.action.Disable();
        }

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = baseFov;
        }
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera != null)
        {
            baseFov = targetCamera.fieldOfView;
        }

        if (playerBody != null)
        {
            movementRef = playerBody.GetComponent<IronStormMovement>();
        }

        currentFrequency = idleFrequency;
        currentAmount = idleAmount;
    }

    private void Update()
    {
        // 1. Đọc giá trị chuột từ người chơi
        Vector2 mouseInput = lookAction != null && lookAction.action != null
            ? lookAction.action.ReadValue<Vector2>() * mouseSensitivity * 0.1f
            : Vector2.zero;

        xRotation -= mouseInput.y;
        xRotation = Mathf.Clamp(xRotation, bottomClamp, topClamp);

        // 2. XỬ LÝ ĐỘ GIẬT VÀ HỒI PHỤC VỀ 0
        targetRecoilX = Mathf.Lerp(targetRecoilX, 0f, recoilReturnSpeed * Time.deltaTime);
        targetRecoilY = Mathf.Lerp(targetRecoilY, 0f, recoilReturnSpeed * Time.deltaTime);

        currentRecoilX = Mathf.Lerp(currentRecoilX, targetRecoilX, recoilSnappiness * Time.deltaTime);
        currentRecoilY = Mathf.Lerp(currentRecoilY, targetRecoilY, recoilSnappiness * Time.deltaTime);

        float finalXRotation = xRotation - currentRecoilX;
        finalXRotation = Mathf.Clamp(finalXRotation, bottomClamp, topClamp);

        // 2b. Cập nhật FOV khi đang bay bằng Jump Pack (+15)
        if (targetCamera != null)
        {
            float desiredFov = isJumpPackFlying ? baseFov + flightFovIncrease : baseFov;
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, desiredFov, fovTransitionSpeed * Time.deltaTime);
        }

        // 3. Hiệu ứng rung camera (khi trúng đạn, v.v.)
        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimer > 0)
        {
            shakeOffset = (Vector3)Random.insideUnitCircle * shakeIntensity;
            shakeTimer -= Time.deltaTime;
        }

        // 3b. Hiệu ứng rung camera khi đang bay bằng Jump Pack
        Vector3 flightShakeOffset = Vector3.zero;
        float targetFlightShake = 0f;
        if (enableFlightShake && isJumpPackFlying)
        {
            targetFlightShake = isJumpPackBoosting ? boostShakeIntensity : hoverShakeIntensity;
        }

        currentFlightShakeIntensity = Mathf.Lerp(
            currentFlightShakeIntensity,
            targetFlightShake,
            flightShakeSmoothness * Time.deltaTime
        );

        if (currentFlightShakeIntensity > 0.0005f)
        {
            flightShakeTimer += Time.deltaTime * flightShakeFrequency;
            float shakeX = (Mathf.PerlinNoise(flightShakeTimer, 0f) - 0.5f) * 2f * currentFlightShakeIntensity;
            float shakeY = (Mathf.PerlinNoise(0f, flightShakeTimer) - 0.5f) * 2f * currentFlightShakeIntensity;
            flightShakeOffset = new Vector3(shakeX, shakeY, 0f);
        }

        // 4. Hiệu ứng Snake Cam (Head Bob)
        Vector3 snakeOffset = Vector3.zero;
        if (enableSnakeCam && movementRef != null && movementRef.enabled)
        {
            bool isGrounded = movementRef.IsGrounded;
            Vector2 inputDir = movementRef.CurrentInputDirection;
            bool isMoving = inputDir.sqrMagnitude > 0.01f && isGrounded;
            bool isRunning = movementRef.IsRunning;

            float targetFrequency = idleFrequency;
            float targetAmount = idleAmount;

            if (!isGrounded)
            {
                targetFrequency = walkFrequency;
                targetAmount = jumpSnakeAmount;
            }
            else if (isMoving)
            {
                targetFrequency = isRunning ? runFrequency : walkFrequency;
                targetAmount = isRunning ? runAmount : walkAmount;
            }

            currentFrequency = Mathf.Lerp(currentFrequency, targetFrequency, transitionSmoothness * Time.deltaTime);
            currentAmount = Mathf.Lerp(currentAmount, targetAmount, transitionSmoothness * Time.deltaTime);

            bobTimer += Time.deltaTime * currentFrequency;

            snakeOffset.y = Mathf.Sin(bobTimer) * currentAmount;
            snakeOffset.x = Mathf.Cos(bobTimer * 0.5f) * currentAmount * 0.5f;
        }

        Vector3 finalOffset = shakeOffset + flightShakeOffset + snakeOffset;

        // 5. Áp dụng góc quay cho Camera và Thân nhân vật
        transform.localRotation = Quaternion.Euler(finalXRotation, 0f, 0f) * Quaternion.Euler(finalOffset);

        if (playerBody != null)
        {
            float totalHorizontalLook = mouseInput.x + currentRecoilY;
            playerBody.Rotate(Vector3.up * totalHorizontalLook);
        }
    }

    /// <summary>
    /// Nhận trạng thái bay từ SpaceMarineJumpPack để bật/tắt rung cam và tăng FOV khi bay
    /// </summary>
    public void SetJumpPackFlying(bool flying, bool boosting)
    {
        isJumpPackFlying = flying;
        isJumpPackBoosting = flying && boosting;
    }

    public void AddRecoil(float recoilUp, float recoilSide)
    {
        float movementMultiplier = 1.0f;

        if (movementRef != null && movementRef.enabled && movementRef.IsGrounded)
        {
            Vector2 inputDir = movementRef.CurrentInputDirection;
            bool isMoving = inputDir.sqrMagnitude > 0.01f;
            bool isRunning = movementRef.IsRunning;

            if (isMoving)
            {
                movementMultiplier = isRunning ? runRecoilMultiplier : walkRecoilMultiplier;
            }
        }

        targetRecoilX += recoilUp * movementMultiplier;
        targetRecoilY += Random.Range(-recoilSide, recoilSide) * movementMultiplier;

        targetRecoilX = Mathf.Clamp(targetRecoilX, 0f, maxRecoilPitch * 1.5f);
    }

    public void TriggerCameraShake(float duration, float intensity)
    {
        shakeTimer = duration;
        shakeIntensity = intensity;
    }
}
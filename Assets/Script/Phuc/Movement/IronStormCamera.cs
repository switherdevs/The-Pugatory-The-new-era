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

    private float xRotation = 0f;          // Góc nhìn thuần của chuột
    private float currentRecoilX = 0f;     // Độ giật dọc hiện tại
    private float currentRecoilY = 0f;     // Độ giật ngang hiện tại
    private float targetRecoilX = 0f;      // Mục tiêu giật dọc
    private float targetRecoilY = 0f;      // Mục tiêu giật ngang

    private float shakeTimer = 0f;
    private float shakeIntensity = 0f;
    private float bobTimer = 0f;
    private IronStormMovement movementRef;

    private float currentFrequency;
    private float currentAmount;

    private void OnEnable()
    {
        lookAction.action.Enable();
    }

    private void OnDisable()
    {
        lookAction.action.Disable();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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
        Vector2 mouseInput = lookAction.action.ReadValue<Vector2>() * mouseSensitivity * 0.1f;

        // Xoay góc nhìn dọc thuần túy bằng chuột
        xRotation -= mouseInput.y;
        xRotation = Mathf.Clamp(xRotation, bottomClamp, topClamp);

        // 2. XỬ LÝ ĐỘ GIẬT VÀ HỒI PHỤC VỀ 0
        // Kéo dần targetRecoil về 0 khi người chơi không bắn
        targetRecoilX = Mathf.Lerp(targetRecoilX, 0f, recoilReturnSpeed * Time.deltaTime);
        targetRecoilY = Mathf.Lerp(targetRecoilY, 0f, recoilReturnSpeed * Time.deltaTime);

        // Nội suy mượt mà lực giật hiện tại
        currentRecoilX = Mathf.Lerp(currentRecoilX, targetRecoilX, recoilSnappiness * Time.deltaTime);
        currentRecoilY = Mathf.Lerp(currentRecoilY, targetRecoilY, recoilSnappiness * Time.deltaTime);

        // Tổng góc nhìn dọc = Góc nhìn chuột - Lực giật (giúp camera hất lên và tự động trả về đúng chỗ)
        float finalXRotation = xRotation - currentRecoilX;
        finalXRotation = Mathf.Clamp(finalXRotation, bottomClamp, topClamp);

        // 3. Hiệu ứng rung camera (khi trúng đạn, v.v.)
        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimer > 0)
        {
            shakeOffset = (Vector3)Random.insideUnitCircle * shakeIntensity;
            shakeTimer -= Time.deltaTime;
        }

        // 4. Hiệu ứng Snake Cam (Head Bob)
        Vector3 snakeOffset = Vector3.zero;
        if (enableSnakeCam && movementRef != null)
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

        Vector3 finalOffset = shakeOffset + snakeOffset;

        // 5. Áp dụng góc quay cho Camera và Thân nhân vật (Cộng thêm độ giật ngang currentRecoilY)
        transform.localRotation = Quaternion.Euler(finalXRotation, 0f, 0f) * Quaternion.Euler(finalOffset);

        float totalHorizontalLook = mouseInput.x + currentRecoilY;
        playerBody.Rotate(Vector3.up * totalHorizontalLook);
    }

    /// <summary>
    /// Nhận lệnh giật từ súng mỗi khi bắn (Tự động nhân hệ số cộng dồn nếu đang di chuyển hoặc chạy)
    /// </summary>
    public void AddRecoil(float recoilUp, float recoilSide)
    {
        float movementMultiplier = 1.0f;

        // Kiểm tra trạng thái chuyển động của nhân vật qua script movementRef
        if (movementRef != null && movementRef.IsGrounded)
        {
            Vector2 inputDir = movementRef.CurrentInputDirection;
            bool isMoving = inputDir.sqrMagnitude > 0.01f;
            bool isRunning = movementRef.IsRunning;

            if (isMoving)
            {
                // Nếu đang chạy nhanh -> Nhân hệ số chạy (Run Multiplier)
                // Nếu chỉ đi bộ -> Nhân hệ số đi bộ (Walk Multiplier)
                movementMultiplier = isRunning ? runRecoilMultiplier : walkRecoilMultiplier;
            }
        }

        // Cộng dồn lực giật đã được nhân hệ số trạng thái
        targetRecoilX += recoilUp * movementMultiplier;
        targetRecoilY += Random.Range(-recoilSide, recoilSide) * movementMultiplier;

        // Chặn không cho giật vượt quá giới hạn tối đa (mở rộng thêm chút để thích ứng khi chạy)
        targetRecoilX = Mathf.Clamp(targetRecoilX, 0f, maxRecoilPitch * 1.5f);
    }

    public void TriggerCameraShake(float duration, float intensity)
    {
        shakeTimer = duration;
        shakeIntensity = intensity;
    }
}
using UnityEngine;
using UnityEngine.InputSystem;

public class IronStormCamera : MonoBehaviour
{
    [Header("Mouse Settings (Góc nhìn chuột)")]
    [Tooltip("Hệ số nhân độ nhạy chuột")]
    [SerializeField] private float mouseSensitivity = 2f;
    [Tooltip("Giới hạn góc ngước lên tối đa (độ)")]
    [SerializeField] private float topClamp = 90f;
    [Tooltip("Giới hạn góc cúi xuống tối thiểu (độ)")]
    [SerializeField] private float bottomClamp = -90f;

    [Header("Crouch Height Sync (Hạ camera đúng theo chiều cao người chơi)")]
    [Tooltip("Tự động hạ localPosition.y của camera theo chiều cao hiện tại của người chơi khi ngồi/trượt")]
    [SerializeField] private bool syncCameraHeightWithBody = true;
    [Tooltip("Độ hạ thêm (mét) khi ngồi (0 = khớp đúng theo chiều cao CharacterController)")]
    [SerializeField] private float extraCrouchCameraDrop = 0f;
    [Tooltip("Tốc độ nội suy hạ/nâng chiều cao camera")]
    [SerializeField] private float cameraHeightSmoothSpeed = 12f;

    [Header("Anti-Distortion (Chống bóp méo object con)")]
    [Tooltip("Ép localScale của Transform camera về đều 3 trục để tránh object con bị méo")]
    [SerializeField] private bool enforceUniformCameraScale = true;
    [Tooltip("Giá trị Near Clip Plane an toàn nếu bật tùy chọn bên dưới")]
    [SerializeField] private float safeNearClipPlane = 0.1f;
    [Tooltip("Chỉ áp dụng safeNearClipPlane khi bật cờ này")]
    [SerializeField] private bool applySafeNearClipPlane = false;

    [Header("Recoil Settings (Độ giật & Hồi phục)")]
    [Tooltip("Tốc độ giật lên mục tiêu recoil (càng cao càng gắt)")]
    [SerializeField] private float recoilSnappiness = 30f;
    [Tooltip("Tốc độ tự hồi tâm recoil về 0")]
    [SerializeField] private float recoilReturnSpeed = 12f;
    [Tooltip("Ngưỡng trần Pitch tối đa cho recoil")]
    [SerializeField] private float maxRecoilPitch = 15f;

    [Header("Movement Recoil Multipliers (Hệ số giật theo trạng thái di chuyển)")]
    [SerializeField] private float walkRecoilMultiplier = 1.3f;
    [SerializeField] private float runRecoilMultiplier = 1.8f;

    [Header("Camera Shake Settings (Rung Rotation khi bắn / sự kiện)")]
    [Tooltip("Thời gian rung mặc định khi bắn")]
    [SerializeField] private float defaultShakeDuration = 0.35f;
    [Tooltip("Biên độ góc xoay khi rung")]
    [SerializeField] private float shakeAngleMultiplier = 6f;
    [Tooltip("Tần số nhiễu Perlin cho shake")]
    [SerializeField] private float shakeFrequency = 40f;
    [Tooltip("Cho phép nhấn phím H để test rung nhanh")]
    [SerializeField] private bool enableTestShakeKey = true;

    [Header("Camera References & FOV Settings (Liên quan targetCamera & weaponCamera)")]
    [Tooltip("Camera chính của người chơi (nếu để trống sẽ tự tìm trên object này hoặc Camera.main)")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("Camera riêng cho vũ khí")]
    [SerializeField] private Camera weaponCamera;
    [Tooltip("Có tăng FOV của targetCamera khi đang bay Jump Pack hay không")]
    [SerializeField] private bool affectMainCameraFovOnFlight = false;
    [Tooltip("Độ tăng FOV khi bay Jump Pack")]
    [SerializeField] private float flightFovIncrease = 10f;
    [Tooltip("Có mở rộng FOV của targetCamera khi đang trượt (Slide) hay không")]
    [SerializeField] private bool affectMainCameraFovOnSlide = true;
    [Tooltip("Độ mở rộng FOV khi đang trượt")]
    [SerializeField] private float slideFovIncrease = 12f;
    [Tooltip("Có mở rộng FOV của targetCamera khi đang đu dây (Zip-Line) hay không")]
    [SerializeField] private bool affectMainCameraFovOnZipLine = true;
    [Tooltip("Độ mở rộng FOV khi đang đu dây")]
    [SerializeField] private float zipLineFovIncrease = 14f;
    [Tooltip("Tốc độ nội suy chuyển đổi FOV mượt mà")]
    [SerializeField] private float fovTransitionSpeed = 10f;

    [Header("Jump Pack Flight Shake (Rung Rotation khi bay)")]
    [Tooltip("Bật/tắt rung góc nhìn khi đang bay Jump Pack")]
    [SerializeField] private bool enableFlightShake = true;
    [Tooltip("Cường độ rung góc khi giữ bay lơ lửng (hover)")]
    [SerializeField] private float hoverShakeIntensity = 0.15f;
    [Tooltip("Cường độ rung góc khi tăng tốc bay (boost)")]
    [SerializeField] private float boostShakeIntensity = 0.3f;
    [Tooltip("Tần số rung góc khi bay")]
    [SerializeField] private float flightShakeFrequency = 28f;
    [Tooltip("Độ mượt khi chuyển cường độ rung lúc bay")]
    [SerializeField] private float flightShakeSmoothness = 12f;

    [Header("Zip-Line Shake (Rung Rotation khi đu dây)")]
    [Tooltip("Bật/tắt rung góc nhìn khi đang đu dây")]
    [SerializeField] private bool enableZipLineShake = true;
    [Tooltip("Cường độ rung góc khi đang đu dây")]
    [SerializeField] private float zipLineShakeIntensity = 0.22f;
    [Tooltip("Tần số rung góc khi đu dây")]
    [SerializeField] private float zipLineShakeFrequency = 32f;
    [Tooltip("Độ mượt khi chuyển cường độ rung lúc đu dây")]
    [SerializeField] private float zipLineShakeSmoothness = 12f;

    [Header("Rotation Bob Settings (Chỉ rung Rotation khi Idle / Walk / Run / Jump)")]
    [Tooltip("Bật/tắt rung góc nhìn theo trạng thái đứng yên và di chuyển")]
    [SerializeField] private bool enableRotationBob = true;
    [Tooltip("Tốc độ chuyển đổi mượt mà giữa các trạng thái rung góc")]
    [Range(1f, 30f)]
    [SerializeField] private float bobTransitionSmoothness = 14f;
    [Tooltip("Hệ số nhân góc Roll (nghiêng trái/phải) so với biên độ rung góc")]
    [SerializeField] private float rollAngleMultiplier = 1.6f;
    [Tooltip("Hệ số nhân góc Yaw (xoay trái/phải) so với biên độ rung góc")]
    [SerializeField] private float yawAngleMultiplier = 0.9f;
    [Tooltip("Hệ số nhân góc Pitch (ngước/cúi nhẹ) so với biên độ rung góc")]
    [SerializeField] private float pitchAngleMultiplier = 1.0f;

    [Header("1. Rotation Bob - Idle (Đứng yên)")]
    [Tooltip("Tần số nhịp rung góc khi đứng yên")]
    [SerializeField] private float idleFrequency = 1.5f;
    [Tooltip("Biên độ rung góc (độ) khi đứng yên")]
    [SerializeField] private float idleAngleAmount = 0.12f;

    [Header("2. Rotation Bob - Move & Jump (Đi bộ / Chạy / Nhảy)")]
    [Tooltip("Tần số nhịp rung góc khi đi bộ")]
    [SerializeField] private float walkFrequency = 10f;
    [Tooltip("Biên độ rung góc (độ) khi đi bộ")]
    [SerializeField] private float walkAngleAmount = 0.45f;
    [Tooltip("Tần số nhịp rung góc khi chạy")]
    [SerializeField] private float runFrequency = 14f;
    [Tooltip("Biên độ rung góc (độ) khi chạy")]
    [SerializeField] private float runAngleAmount = 0.85f;
    [Tooltip("Biên độ rung góc (độ) khi đang trên không (nhảy / rơi)")]
    [SerializeField] private float jumpAngleAmount = 0.55f;

    [Header("Input & Body References")]
    [SerializeField] private InputActionReference lookAction;
    [Tooltip("Transform thân nhân vật (nhận xoay trục Y trái/phải)")]
    [SerializeField] private Transform playerBody;

    private float xRotation = 0f;
    private float currentRecoilX = 0f;
    private float currentRecoilY = 0f;
    private float targetRecoilX = 0f;
    private float targetRecoilY = 0f;

    private float shakeTimer = 0f;
    private float shakeDurationTotal = 0.35f;
    private float shakeIntensity = 0f;
    private float shakePhase = 0f;

    private float flightShakeTimer = 0f;
    private float currentFlightShakeIntensity = 0f;
    private bool isJumpPackFlying = false;
    private bool isJumpPackBoosting = false;

    private float zipLineShakeTimer = 0f;
    private float currentZipLineShakeIntensity = 0f;
    private bool isZipLining = false;

    private float bobPhase = 0f;
    private float currentBobFrequency = 1.5f;
    private float currentBobAmount = 0.12f;

    private float baseFov = 60f;
    private float baseWeaponFov = 60f;
    private bool hasCachedMainFov = false;
    private bool hasCachedWeaponFov = false;

    private float baseLocalY = 0f;
    private bool hasCachedBaseLocalY = false;

    private Vector3 initialCameraScale = Vector3.one;

    private IronStormMovement movementRef;

    private void Awake()
    {
        initialCameraScale = transform.localScale;
        baseLocalY = transform.localPosition.y;
        hasCachedBaseLocalY = true;

        ResolveCameraReferences();
        CacheBaseFovs();

        currentBobFrequency = idleFrequency;
        currentBobAmount = idleAngleAmount;
    }

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

        isZipLining = false;
        currentZipLineShakeIntensity = 0f;
        RestoreFovs();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        ResolveCameraReferences();
        CacheBaseFovs();

        if (!hasCachedBaseLocalY)
        {
            baseLocalY = transform.localPosition.y;
            hasCachedBaseLocalY = true;
        }

        if (enforceUniformCameraScale)
        {
            NormalizeScaleIfNeeded(transform);
        }

        ApplySafeNearClipIfEnabled();

        if (playerBody == null && transform.parent != null)
        {
            playerBody = transform.parent;
        }

        if (playerBody != null)
        {
            movementRef = playerBody.GetComponent<IronStormMovement>();
        }
        if (movementRef == null)
        {
            movementRef = GetComponentInParent<IronStormMovement>();
        }

        currentBobFrequency = idleFrequency;
        currentBobAmount = idleAngleAmount;
    }

    private void ResolveCameraReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }
    }

    private void CacheBaseFovs()
    {
        if (targetCamera != null && !hasCachedMainFov)
        {
            baseFov = targetCamera.fieldOfView;
            hasCachedMainFov = true;
        }

        if (weaponCamera != null && !hasCachedWeaponFov)
        {
            baseWeaponFov = weaponCamera.fieldOfView;
            hasCachedWeaponFov = true;
        }
    }

    private void RestoreFovs()
    {
        if (targetCamera != null && hasCachedMainFov)
        {
            targetCamera.fieldOfView = baseFov;
        }

        if (weaponCamera != null && hasCachedWeaponFov)
        {
            weaponCamera.fieldOfView = baseWeaponFov;
        }
    }

    private void ApplySafeNearClipIfEnabled()
    {
        if (!applySafeNearClipPlane) return;

        float clampedNear = Mathf.Max(0.01f, safeNearClipPlane);

        if (targetCamera != null)
        {
            targetCamera.nearClipPlane = clampedNear;
        }

        if (weaponCamera != null)
        {
            weaponCamera.nearClipPlane = clampedNear;
        }
    }

    private void NormalizeScaleIfNeeded(Transform t)
    {
        if (t == null) return;

        Vector3 s = t.localScale;
        bool isNonUniform = Mathf.Abs(s.x - s.y) > 0.0001f || Mathf.Abs(s.y - s.z) > 0.0001f;
        bool hasInvalidAxis = s.x <= 0f || s.y <= 0f || s.z <= 0f;

        if (isNonUniform || hasInvalidAxis)
        {
            float fallback = Mathf.Abs(initialCameraScale.y) > 0.0001f ? Mathf.Abs(initialCameraScale.y) : 1f;
            float uniform = Mathf.Max(0.001f, Mathf.Abs(s.y) > 0.0001f ? Mathf.Abs(s.y) : fallback);
            t.localScale = new Vector3(uniform, uniform, uniform);
        }
    }

    private Vector2 ReadLookInput()
    {
        Vector2 raw = Vector2.zero;

        if (lookAction != null && lookAction.action != null)
        {
            if (!lookAction.action.enabled)
            {
                lookAction.action.Enable();
            }
            raw = lookAction.action.ReadValue<Vector2>() * 0.1f;
        }

        if (raw.sqrMagnitude < 0.00001f && Mouse.current != null)
        {
            raw = Mouse.current.delta.ReadValue() * 0.05f;
        }

        if (raw.sqrMagnitude < 0.00001f)
        {
            raw = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }

        return raw * mouseSensitivity;
    }

    private void Update()
    {
        if (enableTestShakeKey && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            AddShootShake(1.5f);
        }
    }

    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        float unscaledDt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        if (enforceUniformCameraScale)
        {
            NormalizeScaleIfNeeded(transform);
        }

        UpdateCameraHeightWithBody(dt);

        Vector2 mouseInput = ReadLookInput();

        xRotation -= mouseInput.y;
        xRotation = Mathf.Clamp(xRotation, bottomClamp, topClamp);

        targetRecoilX = Mathf.Lerp(targetRecoilX, 0f, recoilReturnSpeed * dt);
        targetRecoilY = Mathf.Lerp(targetRecoilY, 0f, recoilReturnSpeed * dt);

        currentRecoilX = Mathf.Lerp(currentRecoilX, targetRecoilX, recoilSnappiness * dt);
        currentRecoilY = Mathf.Lerp(currentRecoilY, targetRecoilY, recoilSnappiness * dt);

        float finalXRotation = Mathf.Clamp(xRotation - currentRecoilX, bottomClamp, topClamp);

        UpdateCameraFovs(dt);

        float bobPitch = 0f;
        float bobYaw = 0f;
        float bobRoll = 0f;
        ComputeMovementRotationBob(dt, out bobPitch, out bobYaw, out bobRoll);

        float shakePitch = 0f;
        float shakeYaw = 0f;
        float shakeRoll = 0f;
        bool isShaking = shakeTimer > 0f && shakeIntensity > 0f;

        if (isShaking)
        {
            float ratio = shakeDurationTotal > 0.0001f ? Mathf.Clamp01(shakeTimer / shakeDurationTotal) : 0f;
            float currentAmp = shakeIntensity * ratio * ratio;

            shakePhase += unscaledDt * shakeFrequency;
            float nx = (Mathf.PerlinNoise(shakePhase, 1.37f) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(4.19f, shakePhase) - 0.5f) * 2f;
            float nz = (Mathf.PerlinNoise(shakePhase * 0.85f, 7.73f) - 0.5f) * 2f;

            shakePitch = ny * currentAmp * shakeAngleMultiplier;
            shakeYaw = nx * currentAmp * shakeAngleMultiplier;
            shakeRoll = nz * currentAmp * shakeAngleMultiplier * 0.45f;

            shakeTimer -= unscaledDt;
            if (shakeTimer <= 0f)
            {
                shakeTimer = 0f;
                shakeIntensity = 0f;
            }
        }

        float flightShakePitch = 0f;
        float flightShakeYaw = 0f;
        float targetFlightShake = 0f;

        if (enableFlightShake && isJumpPackFlying)
        {
            targetFlightShake = isJumpPackBoosting ? boostShakeIntensity : hoverShakeIntensity;
        }

        currentFlightShakeIntensity = Mathf.Lerp(
            currentFlightShakeIntensity,
            targetFlightShake,
            flightShakeSmoothness * dt
        );

        if (currentFlightShakeIntensity > 0.0005f)
        {
            flightShakeTimer += dt * flightShakeFrequency;
            flightShakeYaw = (Mathf.PerlinNoise(flightShakeTimer, 0f) - 0.5f) * 2f * currentFlightShakeIntensity;
            flightShakePitch = (Mathf.PerlinNoise(0f, flightShakeTimer) - 0.5f) * 2f * currentFlightShakeIntensity;
        }

        float zipLineShakePitch = 0f;
        float zipLineShakeYaw = 0f;
        float targetZipLineShake = (enableZipLineShake && isZipLining) ? Mathf.Max(0f, zipLineShakeIntensity) : 0f;

        currentZipLineShakeIntensity = Mathf.Lerp(
            currentZipLineShakeIntensity,
            targetZipLineShake,
            zipLineShakeSmoothness * dt
        );

        if (currentZipLineShakeIntensity > 0.0005f)
        {
            zipLineShakeTimer += dt * zipLineShakeFrequency;
            zipLineShakeYaw = (Mathf.PerlinNoise(zipLineShakeTimer, 3.11f) - 0.5f) * 2f * currentZipLineShakeIntensity;
            zipLineShakePitch = (Mathf.PerlinNoise(6.23f, zipLineShakeTimer) - 0.5f) * 2f * currentZipLineShakeIntensity;
        }

        float appliedPitch = Mathf.Clamp(
            finalXRotation + bobPitch + shakePitch + flightShakePitch + zipLineShakePitch,
            bottomClamp,
            topClamp
        );
        float appliedYaw = bobYaw + shakeYaw + flightShakeYaw + zipLineShakeYaw;
        float appliedRoll = bobRoll + shakeRoll;

        transform.localRotation = Quaternion.Euler(appliedPitch, appliedYaw, appliedRoll);

        if (playerBody != null)
        {
            float totalHorizontalLook = mouseInput.x + currentRecoilY;
            playerBody.Rotate(Vector3.up * totalHorizontalLook);
        }
    }

    private void UpdateCameraHeightWithBody(float dt)
    {
        if (!syncCameraHeightWithBody || !hasCachedBaseLocalY) return;
        if (movementRef == null)
        {
            if (playerBody != null) movementRef = playerBody.GetComponent<IronStormMovement>();
            if (movementRef == null) movementRef = GetComponentInParent<IronStormMovement>();
        }
        if (movementRef == null || !movementRef.enabled) return;

        // Khi ngồi hoặc trượt, chiều cao camera bám đúng theo tỉ lệ chiều cao hiện tại của người chơi
        float ratio = movementRef.HeightRatio;
        float crouchExtra = (movementRef.IsCrouching || movementRef.IsSliding)
            ? Mathf.Max(0f, extraCrouchCameraDrop) * (1f - ratio)
            : 0f;

        float targetLocalY = (baseLocalY * ratio) - crouchExtra;
        Vector3 localPos = transform.localPosition;
        localPos.y = Mathf.Lerp(localPos.y, targetLocalY, 1f - Mathf.Exp(-cameraHeightSmoothSpeed * dt));
        transform.localPosition = localPos;
    }

    private void ComputeMovementRotationBob(float dt, out float outPitch, out float outYaw, out float outRoll)
    {
        outPitch = 0f;
        outYaw = 0f;
        outRoll = 0f;

        if (!enableRotationBob) return;

        float targetFrequency = idleFrequency;
        float targetAmount = isJumpPackFlying ? 0f : idleAngleAmount;

        if (!isJumpPackFlying && movementRef != null && movementRef.enabled)
        {
            bool isGrounded = movementRef.IsGrounded;
            Vector2 inputDir = movementRef.CurrentInputDirection;
            bool isMoving = inputDir.sqrMagnitude > 0.01f && isGrounded;
            bool isRunning = movementRef.IsRunning;

            if (!isGrounded)
            {
                targetFrequency = walkFrequency;
                targetAmount = jumpAngleAmount;
            }
            else if (isMoving)
            {
                targetFrequency = isRunning ? runFrequency : walkFrequency;
                targetAmount = isRunning ? runAngleAmount : walkAngleAmount;
            }
            else
            {
                targetFrequency = idleFrequency;
                targetAmount = idleAngleAmount;
            }
        }

        currentBobFrequency = Mathf.Lerp(currentBobFrequency, targetFrequency, bobTransitionSmoothness * dt);
        currentBobAmount = Mathf.Lerp(currentBobAmount, targetAmount, bobTransitionSmoothness * dt);

        bobPhase += dt * currentBobFrequency;

        outPitch = Mathf.Sin(bobPhase * 2f) * currentBobAmount * pitchAngleMultiplier;
        outYaw = Mathf.Cos(bobPhase * 0.5f) * currentBobAmount * yawAngleMultiplier;
        outRoll = Mathf.Sin(bobPhase) * currentBobAmount * rollAngleMultiplier;
    }

    private void UpdateCameraFovs(float dt)
    {
        if (targetCamera != null)
        {
            CacheBaseFovs();
            bool isSlidingNow = movementRef != null && movementRef.enabled && movementRef.IsSliding;

            float desiredFov = baseFov;
            if (affectMainCameraFovOnFlight && isJumpPackFlying)
            {
                desiredFov += flightFovIncrease;
            }
            if (affectMainCameraFovOnSlide && isSlidingNow)
            {
                desiredFov += slideFovIncrease;
            }
            if (affectMainCameraFovOnZipLine && isZipLining)
            {
                desiredFov += zipLineFovIncrease;
            }

            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, desiredFov, fovTransitionSpeed * dt);
        }

        if (weaponCamera != null)
        {
            CacheBaseFovs();
            weaponCamera.fieldOfView = Mathf.Lerp(weaponCamera.fieldOfView, baseWeaponFov, fovTransitionSpeed * dt);
        }
    }

    public void SetJumpPackFlying(bool flying, bool boosting)
    {
        isJumpPackFlying = flying;
        isJumpPackBoosting = flying && boosting;
    }

    public void SetZipLineActive(bool active)
    {
        isZipLining = active;
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
        targetRecoilY += UnityEngine.Random.Range(-recoilSide, recoilSide) * movementMultiplier;

        targetRecoilX = Mathf.Clamp(targetRecoilX, 0f, maxRecoilPitch * 1.5f);
    }

    public void AddShootShake(float intensity)
    {
        TriggerCameraShake(defaultShakeDuration, Mathf.Max(0.25f, intensity));
    }

    public void TriggerCameraShake(float duration, float intensity)
    {
        if (duration <= 0f || intensity <= 0f) return;

        shakeDurationTotal = Mathf.Max(shakeTimer, duration);
        shakeTimer = Mathf.Max(shakeTimer, duration);
        shakeIntensity = Mathf.Max(shakeIntensity * 0.5f, intensity);
    }
}
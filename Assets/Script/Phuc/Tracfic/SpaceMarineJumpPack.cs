using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SpaceMarineJumpPack : MonoBehaviour
{
    [Header("New Input System References")]
    [Tooltip("Phím kích hoạt Jump Pack phóng lên hoặc hủy bay khi đang bay (mặc định C nếu để trống).")]
    [SerializeField] private InputActionReference toggleJumpPackAction;
    [Tooltip("Phím bấm 1 lần để bật/tắt trạng thái giữ bay khi đang trên không (mặc định Space nếu để trống).")]
    [SerializeField] private InputActionReference sustainHoverAction;
    [Tooltip("Vector2 di chuyển ngang khi đang trên không (mặc định WASD nếu để trống).")]
    [SerializeField] private InputActionReference moveAction;
    [Tooltip("Phím giữ để bay lướt nhanh khi đang trên không (mặc định Left Shift nếu để trống).")]
    [SerializeField] private InputActionReference boostAction;

    [Header("Script & Camera Integration")]
    [Tooltip("Script di chuyển mặt đất sẽ tạm tắt khi dùng Jump Pack trên không.")]
    [SerializeField] private IronStormMovement movementScript;
    [Tooltip("Script súng móc để nhường điều khiển khi đang đu dây hoặc kéo lên cột.")]
    [SerializeField] private GrappleHook grappleHookScript;
    [Tooltip("Script Camera để rung cam và tăng FOV khi bay.")]
    [SerializeField] private IronStormCamera cameraScript;
    [Tooltip("Transform dùng làm hệ quy chiếu hướng tới/sang ngang khi bay (để trống sẽ ưu tiên Camera.main hoặc chính object này).")]
    [SerializeField] private Transform moveReferenceTransform;

    [Header("Jump Launch & Hover Settings")]
    [Tooltip("Vận tốc nhảy vọt lên cao khi nhấn C từ mặt đất.")]
    [SerializeField] private float launchUpSpeed = 14f;
    [Tooltip("Vận tốc đẩy lên khi nhấn C giữa không trung (sau khi nhảy thường bằng Space).")]
    [SerializeField] private float midAirBoostUpSpeed = 11f;
    [Tooltip("Gia tốc trọng lực áp dụng khi đang bay nhưng chưa bật giữ bay.")]
    [SerializeField] private float airGravity = -20f;
    [Tooltip("Biên độ lơ lửng lên xuống nhẹ nhàng khi bật giữ bay.")]
    [SerializeField] private float hoverAmplitude = 0.6f;
    [Tooltip("Tốc độ dao động lơ lửng lên xuống khi bật giữ bay.")]
    [SerializeField] private float hoverFrequency = 2.5f;
    [Tooltip("Độ mượt khi chuyển sang trạng thái giữ bay.")]
    [SerializeField] private float hoverSmoothness = 14f;
    [Tooltip("Layer mặt đất để kiểm tra chạm đất.")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [Tooltip("Khoảng cách dò mặt đất bên dưới chân nhân vật.")]
    [SerializeField] private float groundCheckDistance = 0.25f;

    [Header("Air Movement & Boost Settings")]
    [Tooltip("Tốc độ di chuyển ngang khi đang trên không / bay.")]
    [SerializeField] private float airMoveSpeed = 8.5f;
    [Tooltip("Tốc độ di chuyển ngang nhanh khi giữ Shift.")]
    [SerializeField] private float boostMoveSpeed = 16f;
    [Tooltip("Độ mượt khi tăng/giảm tốc độ ngang.")]
    [SerializeField] private float moveSmoothness = 12f;

    [Header("Energy & UI Settings")]
    [Tooltip("Năng lượng tối đa của Jump Pack.")]
    [SerializeField] private float maxEnergy = 100f;
    [Tooltip("Năng lượng tiêu hao mỗi giây khi nhảy/di chuyển trên không bình thường.")]
    [SerializeField] private float baseDrainRate = 12f;
    [Tooltip("Hệ số nhân năng lượng tiêu hao khi đang bật giữ bay (càng cao càng tụt nhanh).")]
    [SerializeField] private float hoverDrainMultiplier = 2.5f;
    [Tooltip("Hệ số nhân năng lượng tiêu hao thêm khi vừa giữ bay vừa giữ Shift.")]
    [SerializeField] private float boostDrainMultiplier = 1.5f;
    [Tooltip("Năng lượng hồi phục mỗi giây khi không dùng Jump Pack.")]
    [SerializeField] private float energyRegenRate = 16f;
    [Tooltip("Thời gian chờ (giây) trước khi bắt đầu hồi năng lượng.")]
    [SerializeField] private float regenDelay = 1.0f;
    [Tooltip("Thanh Slider hiển thị năng lượng Jump Pack.")]
    [SerializeField] private Slider energySlider;

    [Header("VFX / Smoke Effect Settings")]
    [Tooltip("GameObject hiệu ứng khói phản lực bật khi kích hoạt Jump Pack.")]
    [SerializeField] private GameObject smokeEffectObject;
    [Tooltip("GameObject hiệu ứng khói mạnh hơn khi bay nhanh / giữ bay (tùy chọn).")]
    [SerializeField] private GameObject boostSmokeEffectObject;

    private CharacterController controller;
    private float currentEnergy;
    private float lastActiveTime;
    private float hoverTimer;
    private float launchGraceTimer;
    private float currentVerticalVelocity;
    private float currentHorizontalSpeed;
    private Vector3 currentHorizontalVelocity;

    private bool isJumpPackActive = false;
    private bool hasUsedJumpPackInAir = false;
    private bool isHovering = false;
    private bool isBoosting = false;

    private InputAction runtimeToggleAction;
    private InputAction runtimeHoverAction;
    private InputAction runtimeMoveAction;
    private InputAction runtimeBoostAction;
    private bool usingFallbackActions = false;

    public bool IsJumpPackActive => isJumpPackActive;

    private InputAction ActiveToggleAction => toggleJumpPackAction != null && toggleJumpPackAction.action != null ? toggleJumpPackAction.action : runtimeToggleAction;
    private InputAction ActiveHoverAction => sustainHoverAction != null && sustainHoverAction.action != null ? sustainHoverAction.action : runtimeHoverAction;
    private InputAction ActiveMoveAction => moveAction != null && moveAction.action != null ? moveAction.action : runtimeMoveAction;
    private InputAction ActiveBoostAction => boostAction != null && boostAction.action != null ? boostAction.action : runtimeBoostAction;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (movementScript == null)
        {
            movementScript = GetComponent<IronStormMovement>();
        }

        if (grappleHookScript == null)
        {
            grappleHookScript = GetComponentInChildren<GrappleHook>(true);
        }

        if (cameraScript == null)
        {
            cameraScript = GetComponentInChildren<IronStormCamera>();
        }

        currentEnergy = maxEnergy;
        currentHorizontalSpeed = airMoveSpeed;

        SetupFallbackActions();

        if (energySlider != null)
        {
            energySlider.minValue = 0f;
            energySlider.maxValue = maxEnergy;
            energySlider.value = currentEnergy;
        }

        SetEffects(false, false);
    }

    private void SetupFallbackActions()
    {
        if (toggleJumpPackAction == null || sustainHoverAction == null || moveAction == null || boostAction == null)
        {
            usingFallbackActions = true;
        }

        if (toggleJumpPackAction == null)
        {
            runtimeToggleAction = new InputAction("JumpPackLaunch", InputActionType.Button, "<Keyboard>/c");
        }

        if (sustainHoverAction == null)
        {
            runtimeHoverAction = new InputAction("SustainHover", InputActionType.Button, "<Keyboard>/space");
        }

        if (moveAction == null)
        {
            runtimeMoveAction = new InputAction("AirMove", InputActionType.Value);
            runtimeMoveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        }

        if (boostAction == null)
        {
            runtimeBoostAction = new InputAction("AirBoost", InputActionType.Button, "<Keyboard>/leftShift");
        }
    }

    private void OnEnable()
    {
        if (grappleHookScript == null)
        {
            grappleHookScript = GetComponentInChildren<GrappleHook>(true);
        }
        EnableAllActions();
    }

    private void OnDisable()
    {
        if (isJumpPackActive)
        {
            DeactivateJumpPack();
        }
        hasUsedJumpPackInAir = false;

        DisableAllActions();
    }

    private void OnDestroy()
    {
        if (usingFallbackActions)
        {
            runtimeToggleAction?.Dispose();
            runtimeHoverAction?.Dispose();
            runtimeMoveAction?.Dispose();
            runtimeBoostAction?.Dispose();
        }
    }

    private void EnableAllActions()
    {
        ActiveToggleAction?.Enable();
        ActiveHoverAction?.Enable();
        ActiveMoveAction?.Enable();
        ActiveBoostAction?.Enable();
    }

    private void DisableAllActions()
    {
        ActiveToggleAction?.Disable();
        ActiveHoverAction?.Disable();
        ActiveMoveAction?.Disable();
        ActiveBoostAction?.Disable();
    }

    private bool WasSpacePressed()
    {
        if (ActiveHoverAction != null && ActiveHoverAction.WasPressedThisFrame())
        {
            return true;
        }
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    }

    private bool WasLaunchKeyPressed()
    {
        if (ActiveToggleAction != null && ActiveToggleAction.WasPressedThisFrame())
        {
            return true;
        }
        return Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
    }

    private bool IsGroundedSafe()
    {
        if (launchGraceTimer > 0f) return false;

        if (controller != null && controller.isGrounded)
        {
            return true;
        }

        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        float checkDist = (controller != null ? controller.skinWidth + 0.08f : 0.12f) + groundCheckDistance;
        return Physics.Raycast(rayOrigin, Vector3.down, out _, checkDist, groundLayer, QueryTriggerInteraction.Ignore);
    }

    private void Update()
    {
        EnableAllActions();

        if (launchGraceTimer > 0f)
        {
            launchGraceTimer -= Time.deltaTime;
        }

        if (!isJumpPackActive && hasUsedJumpPackInAir && IsGroundedSafe())
        {
            hasUsedJumpPackInAir = false;
        }

        bool đangDuHoacKeoDay = grappleHookScript != null && grappleHookScript.IsSwingingOrPulling;
        if (đangDuHoacKeoDay)
        {
            if (isJumpPackActive)
            {
                DeactivateJumpPack();
            }
            UpdateUI();
            return;
        }

        if (isJumpPackActive)
        {
            if (movementScript != null && movementScript.enabled)
            {
                movementScript.enabled = false;
            }

            if (WasLaunchKeyPressed())
            {
                DeactivateJumpPack();
            }
            else
            {
                UpdateFlightStates();
                ApplyAirControllerMovement();
                DrainFlightEnergy();

                if (!isHovering && currentVerticalVelocity < 0f && IsGroundedSafe())
                {
                    DeactivateJumpPack();
                    hasUsedJumpPackInAir = false;
                }
            }
        }
        else
        {
            if (movementScript != null && !movementScript.enabled)
            {
                movementScript.enabled = true;
            }

            HandleLaunchInput();
            RegenerateEnergy();
        }

        UpdateUI();

        if (cameraScript != null)
        {
            cameraScript.SetJumpPackFlying(isJumpPackActive, isBoosting);
        }
    }

    private void HandleLaunchInput()
    {
        if (!WasLaunchKeyPressed()) return;

        if (hasUsedJumpPackInAir || currentEnergy <= 2f)
        {
            return;
        }

        if (IsGroundedSafe())
        {
            ActivateJumpPack(launchUpSpeed);
        }
        else
        {
            ActivateJumpPack(midAirBoostUpSpeed);
        }
    }

    private void ActivateJumpPack(float upwardSpeed)
    {
        isJumpPackActive = true;
        hasUsedJumpPackInAir = true;
        isHovering = false;
        isBoosting = false;
        hoverTimer = 0f;
        launchGraceTimer = 0.15f;

        currentVerticalVelocity = upwardSpeed;
        currentHorizontalSpeed = airMoveSpeed;
        currentHorizontalVelocity = Vector3.zero;

        if (movementScript != null)
        {
            movementScript.enabled = false;
        }

        SetEffects(true, false);
    }

    private void DeactivateJumpPack()
    {
        isJumpPackActive = false;
        isHovering = false;
        isBoosting = false;
        currentVerticalVelocity = 0f;
        currentHorizontalVelocity = Vector3.zero;
        lastActiveTime = Time.time;

        SetEffects(false, false);

        if (cameraScript != null)
        {
            cameraScript.SetJumpPackFlying(false, false);
        }

        if (movementScript != null && !movementScript.enabled)
        {
            movementScript.enabled = true;
        }
    }

    private void UpdateFlightStates()
    {
        if (WasSpacePressed())
        {
            if (!isHovering && currentEnergy > 0.2f)
            {
                isHovering = true;
                hoverTimer = 0f;
            }
            else
            {
                isHovering = false;
            }
        }

        if (currentEnergy <= 0.05f)
        {
            currentEnergy = 0f;
            isHovering = false;
            isBoosting = false;
        }

        bool holdingShift = (ActiveBoostAction != null && ActiveBoostAction.IsPressed())
                            || (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed);
        Vector2 moveInput = ActiveMoveAction != null ? ActiveMoveAction.ReadValue<Vector2>() : Vector2.zero;

        isBoosting = holdingShift && moveInput.sqrMagnitude > 0.01f && currentEnergy > 0.05f;

        SetEffects(true, isBoosting || isHovering);
    }

    private Vector3 GetHorizontalBasisForward()
    {
        Transform refT = moveReferenceTransform;
        if (refT == null && Camera.main != null)
        {
            refT = Camera.main.transform;
        }
        if (refT == null)
        {
            refT = transform;
        }

        Vector3 forward = refT.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }
        return forward.normalized;
    }

    private Vector3 LayHuongDashUuTien(Vector2 moveInput, Vector3 fallbackMoveDir)
    {
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 forward = GetHorizontalBasisForward();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x).normalized;
            Vector3 dir = right * moveInput.x + forward * moveInput.y;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                return dir.normalized;
            }
        }

        Vector3 fallback = fallbackMoveDir;
        fallback.y = 0f;
        if (fallback.sqrMagnitude > 0.001f)
        {
            return fallback.normalized;
        }

        Vector3 camForward = GetHorizontalBasisForward();
        return camForward.sqrMagnitude > 0.001f ? camForward : transform.forward.normalized;
    }

    private void ApplyAirControllerMovement()
    {
        if (controller == null || !controller.enabled) return;

        Vector2 moveInput = ActiveMoveAction != null ? ActiveMoveAction.ReadValue<Vector2>() : Vector2.zero;

        Vector3 forward = GetHorizontalBasisForward();
        Vector3 right = new Vector3(forward.z, 0f, -forward.x).normalized;

        Vector3 moveDir = right * moveInput.x + forward * moveInput.y;
        moveDir = Vector3.ClampMagnitude(moveDir, 1f);

        Vector3 huongDash = LayHuongDashUuTien(moveInput, moveDir);

        if (movementScript != null && movementScript.TryStepJumpPackDash(Time.deltaTime, huongDash))
        {
            currentHorizontalVelocity = moveDir.sqrMagnitude > 0.01f ? moveDir.normalized * airMoveSpeed : Vector3.zero;
            if (isHovering)
            {
                hoverTimer += Time.deltaTime * hoverFrequency;
                currentVerticalVelocity = Mathf.Sin(hoverTimer) * hoverAmplitude;
            }
            else
            {
                float heSoRoi = currentVerticalVelocity < 0f ? movementScript.AirDashVerticalCarry : 0.25f;
                currentVerticalVelocity += airGravity * heSoRoi * Time.deltaTime;
                controller.Move(new Vector3(0f, currentVerticalVelocity * Time.deltaTime, 0f));
            }
            return;
        }

        float targetSpeed = isBoosting ? boostMoveSpeed : airMoveSpeed;
        currentHorizontalSpeed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed, moveSmoothness * Time.deltaTime);

        Vector3 desiredHorizontalVel = moveDir * currentHorizontalSpeed;
        currentHorizontalVelocity = Vector3.Lerp(currentHorizontalVelocity, desiredHorizontalVel, moveSmoothness * Time.deltaTime);

        if (isHovering)
        {
            hoverTimer += Time.deltaTime * hoverFrequency;
            float targetHoverVel = Mathf.Sin(hoverTimer) * hoverAmplitude;
            currentVerticalVelocity = Mathf.Lerp(currentVerticalVelocity, targetHoverVel, hoverSmoothness * Time.deltaTime);
        }
        else
        {
            currentVerticalVelocity += airGravity * Time.deltaTime;
        }

        Vector3 finalVelocity = new Vector3(currentHorizontalVelocity.x, currentVerticalVelocity, currentHorizontalVelocity.z);
        controller.Move(finalVelocity * Time.deltaTime);
    }

    private void DrainFlightEnergy()
    {
        float drainRate = baseDrainRate;

        if (isHovering)
        {
            drainRate *= hoverDrainMultiplier;
        }

        if (isBoosting)
        {
            drainRate *= boostDrainMultiplier;
        }

        currentEnergy -= drainRate * Time.deltaTime;

        if (currentEnergy <= 0f)
        {
            currentEnergy = 0f;
            isHovering = false;
            isBoosting = false;
        }
    }

    private void RegenerateEnergy()
    {
        if (Time.time >= lastActiveTime + regenDelay && currentEnergy < maxEnergy)
        {
            currentEnergy += energyRegenRate * Time.deltaTime;
            currentEnergy = Mathf.Min(currentEnergy, maxEnergy);
        }
    }

    private void UpdateUI()
    {
        if (energySlider != null)
        {
            energySlider.value = currentEnergy;
        }
    }

    private void SetEffects(bool active, bool intense)
    {
        if (smokeEffectObject != null && smokeEffectObject.activeSelf != active)
        {
            smokeEffectObject.SetActive(active);
        }

        if (boostSmokeEffectObject != null && boostSmokeEffectObject.activeSelf != intense)
        {
            boostSmokeEffectObject.SetActive(intense);
        }
    }
}
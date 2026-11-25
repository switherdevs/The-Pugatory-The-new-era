using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(IronStormMovement))]
public class IronStormSlide : MonoBehaviour
{
    [Header("Slide Settings (Trượt bằng phím Alt)")]
    [Tooltip("Bật/tắt tính năng trượt.")]
    [SerializeField] private bool enableSlide = true;

    [Tooltip("Nút kích hoạt trượt (New Input System). Nếu để trống sẽ tự dùng phím Left Alt / Alt.")]
    [SerializeField] private InputActionReference slideAction;

    [Tooltip("Cho phép GIỮ phím trượt để tự động trượt khi đáp đất/đủ đà thay vì chỉ bắt khoảnh khắc vừa nhấn.")]
    [SerializeField] private bool allowHoldSlideToTrigger = true;

    [Tooltip("Bắt buộc phải nhả phím trượt ra rồi mới được kích hoạt trượt bằng cách giữ ở lần tiếp theo (chống trượt liên tục khi giữ phím).")]
    [SerializeField] private bool requireReleaseAfterHoldSlide = true;

    [Tooltip("Yêu cầu đang giữ phím chạy, hoặc có tốc độ cao, hoặc vừa tiếp đất sau đà chạy / Jump Pack.")]
    [SerializeField] private bool requireRunningToSlide = true;

    [Tooltip("Mặc định TẮT: chỉ được trượt sau khi chạm đất, không trượt khi đang trên không.")]
    [SerializeField] private bool allowAirSlide = false;

    [Tooltip("Cho phép trượt khi tốc độ cao mà không cần giữ phím Shift.")]
    [SerializeField] private bool allowHighSpeedSlideWithoutSprint = true;

    [Tooltip("Ngưỡng tốc độ ngang tối thiểu (m/s) để coi là chạy tốc độ cao (0 = tự dùng 85% runSpeed).")]
    [SerializeField] private float highSpeedSlideThreshold = 0f;

    [Tooltip("Tốc độ trượt đúng theo thông số điền (m/s).")]
    [SerializeField] private float slideSpeed = 11f;

    [Tooltip("Tốc độ dừng trượt tối thiểu (m/s).")]
    [SerializeField] private float minSlideSpeed = 2.4f;

    [Tooltip("Thời gian trượt tối đa (giây).")]
    [SerializeField] private float slideDuration = 0.75f;

    [Tooltip("Thời gian hồi trước khi trượt lần tiếp theo (giây).")]
    [SerializeField] private float slideCooldown = 0.4f;

    [Tooltip("Tốc độ giảm dần lực trượt mỗi giây ở mọi hình thức trượt (càng cao càng phanh nhanh).")]
    [SerializeField] private float slideDeceleration = 8f;

    [Tooltip("Hệ số nhân giảm tốc khi đang ở đà tốc độ cao (vượt slideSpeed) để phanh mượt theo thời gian.")]
    [SerializeField] private float highSpeedDecelerationMultiplier = 1.25f;

    [Tooltip("Mức độ lái nhẹ hướng trượt theo phím di chuyển (0 = khóa thẳng, 0.3 = lái nhẹ).")]
    [Range(0f, 1f)]
    [SerializeField] private float slideSteerControl = 0.25f;

    [Tooltip("Chiều cao CharacterController khi đang trượt.")]
    [SerializeField] private float slideHeight = 0.9f;

    [Header("Obstacle Overlap Stop (Quét va chạm collider khi trượt)")]
    [Tooltip("Dừng trượt và đứng lên ngay khi quét overlap thấy đụng collider vật cản.")]
    [SerializeField] private bool stopSlideOnColliderHit = true;

    [Tooltip("Bán kính quét overlap quanh/trước thân khi trượt (mét).")]
    [SerializeField] private float overlapRadius = 0.45f;

    [Tooltip("Khoảng cách dò overlap về phía trước khi trượt (mét).")]
    [SerializeField] private float overlapForwardProbe = 0.25f;

    [Tooltip("Layer mask quét collider khi trượt (Everything = quét mọi layer trừ chính nhân vật).")]
    [SerializeField] private LayerMask slideObstacleMask = ~0;

    [Header("Run -> Jump -> Slide Boost (Trượt nhanh & xa hơn sau khi tiếp đất từ đà chạy)")]
    [Tooltip("Bật tăng tốc và tầm trượt khi Chạy -> Nhảy -> Tiếp đất -> Trượt.")]
    [SerializeField] private bool enableRunJumpSlideBoost = true;

    [Tooltip("Hệ số nhân tốc độ trượt khi trượt ngay sau khi tiếp đất từ đà chạy.")]
    [SerializeField] private float jumpSlideSpeedMultiplier = 1.35f;

    [Tooltip("Hệ số nhân thời gian trượt khi trượt ngay sau khi tiếp đất từ đà chạy.")]
    [SerializeField] private float jumpSlideDurationMultiplier = 1.35f;

    [Tooltip("Thời gian lưu đà chạy-nhảy sau khi chạm đất để bấm/giữ trượt (giây).")]
    [SerializeField] private float postLandJumpSlideWindow = 0.22f;

    [Header("Jump Pack Fall -> Slide Boost (Trượt lực & tốc độ cao khi đáp từ Jump Pack)")]
    [Tooltip("Bật trượt lực & tốc độ cao khi bay hoặc nhảy bằng Jump Pack từ trên cao xuống và giữ phím trượt.")]
    [SerializeField] private bool enableJumpPackFallSlide = true;

    [Tooltip("Hệ số nhân tốc độ trượt mạnh khi đáp từ trên cao bằng Jump Pack xuống.")]
    [SerializeField] private float jumpPackFallSpeedMultiplier = 1.6f;

    [Tooltip("Hệ số nhân thời gian trượt dài hơn khi đáp từ trên cao bằng Jump Pack xuống.")]
    [SerializeField] private float jumpPackFallDurationMultiplier = 1.45f;

    [Tooltip("Thời gian lưu đà rơi Jump Pack sau khi chạm đất để người chơi kịp giữ/bấm trượt (giây).")]
    [SerializeField] private float postJumpPackLandSlideWindow = 0.35f;

    [Tooltip("Tốc độ rơi thẳng đứng tối thiểu (m/s, giá trị dương) để tính là rơi từ trên cao xuống.")]
    [SerializeField] private float minFallSpeedForHighPowerSlide = 3.5f;

    [Header("Anti-Tunneling (Chống xuyên tường khi trượt nhanh)")]
    [Tooltip("Quãng đường tối đa mỗi lần gọi controller.Move khi trượt (mét) để tránh xuyên tường.")]
    [SerializeField] private float maxSlideStepDistance = 0.12f;

    [Header("Slide Audio Settings")]
    [Tooltip("AudioSource phát tiếng trượt (nếu để trống sẽ tự tìm trên GameObject).")]
    [SerializeField] private AudioSource slideAudioSource;

    [Tooltip("File âm thanh khi trượt.")]
    [SerializeField] private AudioClip slideClip;

    [Range(0f, 1f)]
    [SerializeField] private float slideVolume = 1.0f;

    [Range(0f, 0.3f)]
    [SerializeField] private float pitchRandomness = 0.1f;

    private CharacterController controller;
    private IronStormMovement movement;
    private SpaceMarineJumpPack jumpPack;
    private readonly Collider[] overlapBuffer = new Collider[16];
    private bool isSliding;
    private float slideTimer;
    private float slideCooldownTimer;
    private float currentSlideSpeed;
    private Vector3 slideDirection;
    private bool blockedByHoldingSinceLastSlide;

    private bool wasAirborneFromRun;
    private float postLandBoostTimer;

    private bool wasDescendingFromJumpPack;
    private float peakFallSpeedFromJumpPack;
    private float postJumpPackLandTimer;
    private float savedJumpPackFallIntensity = 1f;

    public bool IsSliding => isSliding;
    public bool IsCrouchLockedBySlide => isSliding;
    public float SlideHeight => slideHeight;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        movement = GetComponent<IronStormMovement>();
        jumpPack = GetComponent<SpaceMarineJumpPack>();
        if (slideAudioSource == null)
        {
            slideAudioSource = GetComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        if (slideAction != null && slideAction.action != null)
        {
            slideAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (slideAction != null && slideAction.action != null)
        {
            slideAction.action.Disable();
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (!IsSlideHeld())
        {
            blockedByHoldingSinceLastSlide = false;
        }

        if (slideCooldownTimer > 0f)
        {
            slideCooldownTimer = Mathf.Max(0f, slideCooldownTimer - dt);
        }

        if (postLandBoostTimer > 0f)
        {
            postLandBoostTimer = Mathf.Max(0f, postLandBoostTimer - dt);
        }

        if (postJumpPackLandTimer > 0f)
        {
            postJumpPackLandTimer = Mathf.Max(0f, postJumpPackLandTimer - dt);
        }

        SyncJumpPackStateFromComponent(dt);
    }

    private void SyncJumpPackStateFromComponent(float dt)
    {
        if (!enableJumpPackFallSlide)
        {
            wasDescendingFromJumpPack = false;
            peakFallSpeedFromJumpPack = 0f;
            postJumpPackLandTimer = 0f;
            return;
        }

        if (jumpPack == null)
        {
            jumpPack = GetComponent<SpaceMarineJumpPack>();
        }

        if (jumpPack == null) return;

        bool groundedNow = controller != null && controller.isGrounded;
        float downwardSpeed = controller != null ? Mathf.Max(0f, -controller.velocity.y) : 0f;

        if (jumpPack.IsJumpPackActive)
        {
            if (downwardSpeed >= minFallSpeedForHighPowerSlide)
            {
                wasDescendingFromJumpPack = true;
                if (downwardSpeed > peakFallSpeedFromJumpPack)
                {
                    peakFallSpeedFromJumpPack = downwardSpeed;
                }
            }
            return;
        }

        if (!groundedNow && downwardSpeed >= minFallSpeedForHighPowerSlide && (wasDescendingFromJumpPack || jumpPack.enabled))
        {
            wasDescendingFromJumpPack = true;
            if (downwardSpeed > peakFallSpeedFromJumpPack)
            {
                peakFallSpeedFromJumpPack = downwardSpeed;
            }
        }
        else if (groundedNow && wasDescendingFromJumpPack)
        {
            float denom = Mathf.Max(minFallSpeedForHighPowerSlide, 14f);
            float ratio = Mathf.Clamp01((peakFallSpeedFromJumpPack - minFallSpeedForHighPowerSlide) / denom);
            savedJumpPackFallIntensity = Mathf.Lerp(1.05f, 1.5f, ratio);

            wasDescendingFromJumpPack = false;
            peakFallSpeedFromJumpPack = 0f;
            postJumpPackLandTimer = Mathf.Max(0.05f, postJumpPackLandSlideWindow);
        }
        else if (groundedNow && postJumpPackLandTimer <= 0f)
        {
            peakFallSpeedFromJumpPack = 0f;
        }
    }

    public void NotifyJumpPackDescentState(bool isJumpPackActive, bool groundedNow, float currentVerticalVelocity)
    {
        if (!enableJumpPackFallSlide)
        {
            wasDescendingFromJumpPack = false;
            peakFallSpeedFromJumpPack = 0f;
            postJumpPackLandTimer = 0f;
            return;
        }

        float downwardSpeed = Mathf.Max(0f, -currentVerticalVelocity);

        if (isJumpPackActive || !groundedNow)
        {
            if (downwardSpeed >= minFallSpeedForHighPowerSlide)
            {
                wasDescendingFromJumpPack = true;
                if (downwardSpeed > peakFallSpeedFromJumpPack)
                {
                    peakFallSpeedFromJumpPack = downwardSpeed;
                }
            }
        }
        else if (groundedNow && wasDescendingFromJumpPack)
        {
            float denom = Mathf.Max(minFallSpeedForHighPowerSlide, 14f);
            float ratio = Mathf.Clamp01((peakFallSpeedFromJumpPack - minFallSpeedForHighPowerSlide) / denom);
            savedJumpPackFallIntensity = Mathf.Lerp(1.05f, 1.5f, ratio);

            wasDescendingFromJumpPack = false;
            peakFallSpeedFromJumpPack = 0f;
            postJumpPackLandTimer = Mathf.Max(0.05f, postJumpPackLandSlideWindow);
        }
    }

    public bool WasSlideJustPressed()
    {
        if (slideAction != null && slideAction.action != null)
        {
            if (!slideAction.action.enabled) slideAction.action.Enable();
            return slideAction.action.WasPressedThisFrame() || slideAction.action.triggered;
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftAltKey.wasPressedThisFrame || Keyboard.current.altKey.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsSlideHeld()
    {
        if (slideAction != null && slideAction.action != null)
        {
            if (!slideAction.action.enabled) slideAction.action.Enable();
            if (slideAction.action.IsPressed())
            {
                return true;
            }
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftAltKey.isPressed || Keyboard.current.altKey.isPressed)
            {
                return true;
            }
        }

        return false;
    }

    public void NotifyRunJumpState(bool groundedNow, bool isRunningNow)
    {
        if (!enableRunJumpSlideBoost)
        {
            wasAirborneFromRun = false;
            postLandBoostTimer = 0f;
            return;
        }

        if (!groundedNow)
        {
            float speedThreshold = highSpeedSlideThreshold > 0f
                ? highSpeedSlideThreshold
                : (movement != null ? movement.RunSpeed * 0.85f : 5.5f);
            bool fastEnough = movement != null && movement.HorizontalSpeed >= speedThreshold;

            if (isRunningNow || fastEnough || (movement != null && movement.IsSprintHeldNow))
            {
                wasAirborneFromRun = true;
            }
        }
        else if (wasAirborneFromRun)
        {
            wasAirborneFromRun = false;
            postLandBoostTimer = Mathf.Max(0.05f, postLandJumpSlideWindow);
        }
    }

    private bool HasHighSpeedMomentum()
    {
        if (!allowHighSpeedSlideWithoutSprint || movement == null) return false;
        float threshold = highSpeedSlideThreshold > 0f
            ? highSpeedSlideThreshold
            : Mathf.Max(4.5f, movement.RunSpeed * 0.85f);
        return movement.HorizontalSpeed >= threshold;
    }

    public bool CanSlideNow(Vector3 moveDir, bool groundedForMovement)
    {
        if (!enableSlide) return false;
        if (movement == null) movement = GetComponent<IronStormMovement>();
        if (movement == null || isSliding || movement.IsCrouching || slideCooldownTimer > 0f)
            return false;

        if (!groundedForMovement && !allowAirSlide)
            return false;

        bool isHoldingSprint = movement.IsSprintHeldNow;
        bool hasHighSpeed = HasHighSpeedMomentum();
        bool hasJumpSlideBoost = enableRunJumpSlideBoost && postLandBoostTimer > 0f;
        bool hasJumpPackFallBoost = enableJumpPackFallSlide && postJumpPackLandTimer > 0f;

        bool slideJustPressed = WasSlideJustPressed();
        bool canTriggerByHold = allowHoldSlideToTrigger && IsSlideHeld() && (!requireReleaseAfterHoldSlide || !blockedByHoldingSinceLastSlide);
        bool triggerActive = slideJustPressed || (canTriggerByHold && (hasJumpPackFallBoost || hasJumpSlideBoost || hasHighSpeed || isHoldingSprint || !requireRunningToSlide));

        bool hasMoveInput = moveDir.sqrMagnitude > 0.01f || movement.CurrentInputDirection.sqrMagnitude > 0.01f;

        return triggerActive &&
               hasMoveInput &&
               (!requireRunningToSlide || isHoldingSprint || hasHighSpeed || hasJumpSlideBoost || hasJumpPackFallBoost);
    }

    public void CancelSlide()
    {
        isSliding = false;
        slideTimer = 0f;
    }

    private bool HasObstacleOverlapDuringSlide()
    {
        if (!stopSlideOnColliderHit) return false;

        float radius = Mathf.Max(0.05f, controller != null ? Mathf.Min(overlapRadius, controller.radius * 1.15f) : overlapRadius);
        float halfSpan = Mathf.Max(0.05f, slideHeight * 0.35f);
        Vector3 centerBase = transform.position + Vector3.up * Mathf.Max(radius + 0.05f, slideHeight * 0.5f) + slideDirection * Mathf.Max(0f, overlapForwardProbe);
        Vector3 point1 = centerBase + Vector3.down * halfSpan;
        Vector3 point2 = centerBase + Vector3.up * halfSpan;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            point1,
            point2,
            radius,
            overlapBuffer,
            slideObstacleMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];
            if (hit == null) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            return true;
        }

        return false;
    }

    public bool StepSlide(Vector3 moveDir, bool groundedForMovement, float dt)
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();
        if (movement == null)
            movement = GetComponent<IronStormMovement>();

        if (!enableSlide || movement == null)
        {
            isSliding = false;
            return false;
        }

        if (!isSliding && CanSlideNow(moveDir, groundedForMovement))
        {
            Vector3 candidateDir = moveDir.sqrMagnitude > 0.01f
                ? moveDir.normalized
                : new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            StartSlide(candidateDir);
        }

        if (!isSliding)
            return false;

        if (HasObstacleOverlapDuringSlide())
        {
            EndSlide(standUpImmediately: true);
            return false;
        }

        slideTimer -= dt;

        if (moveDir.sqrMagnitude > 0.01f && slideSteerControl > 0f)
        {
            slideDirection = Vector3.Slerp(slideDirection, moveDir.normalized, slideSteerControl * 6f * dt).normalized;
        }

        // Giam toc do theo thoi gian o moi hinh thuc truot (thuong, run-jump, jump pack fall)
        float targetMinSpeed = Mathf.Max(0f, minSlideSpeed);
        float decelMult = currentSlideSpeed > slideSpeed ? Mathf.Max(0.1f, highSpeedDecelerationMultiplier) : 1f;
        float decelRate = Mathf.Max(0f, slideDeceleration) * decelMult;
        currentSlideSpeed = Mathf.MoveTowards(currentSlideSpeed, targetMinSpeed, decelRate * dt);

        Vector3 totalStep = slideDirection * currentSlideSpeed * dt;
        float totalDist = totalStep.magnitude;
        float maxStep = Mathf.Max(0.02f, maxSlideStepDistance);

        if (controller != null && controller.enabled && totalDist > 0.0001f)
        {
            int steps = Mathf.CeilToInt(totalDist / maxStep);
            Vector3 singleStep = totalStep / steps;
            for (int i = 0; i < steps; i++)
            {
                controller.Move(singleStep);
                if (HasObstacleOverlapDuringSlide())
                {
                    EndSlide(standUpImmediately: true);
                    return false;
                }
            }
        }

        movement.OverrideHorizontalVelocityFromSlide(Vector3.zero);

        if (slideTimer <= 0f || (!groundedForMovement && !allowAirSlide) || currentSlideSpeed <= targetMinSpeed + 0.05f)
        {
            EndSlide(standUpImmediately: false);
            return false;
        }

        return true;
    }

    private void StartSlide(Vector3 dir)
    {
        if (requireReleaseAfterHoldSlide && IsSlideHeld())
        {
            blockedByHoldingSinceLastSlide = true;
        }

        bool boostedFromRunJump = enableRunJumpSlideBoost && postLandBoostTimer > 0f;
        bool boostedFromJumpPackFall = enableJumpPackFallSlide && postJumpPackLandTimer > 0f;
        float fallIntensity = boostedFromJumpPackFall ? Mathf.Clamp(savedJumpPackFallIntensity, 1f, 1.6f) : 1f;

        wasAirborneFromRun = false;
        postLandBoostTimer = 0f;
        wasDescendingFromJumpPack = false;
        peakFallSpeedFromJumpPack = 0f;
        postJumpPackLandTimer = 0f;
        savedJumpPackFallIntensity = 1f;

        float runJumpSpeedMult = boostedFromRunJump ? Mathf.Max(1f, jumpSlideSpeedMultiplier) : 1f;
        float runJumpDurationMult = boostedFromRunJump ? Mathf.Max(1f, jumpSlideDurationMultiplier) : 1f;

        float jumpPackSpeedMult = boostedFromJumpPackFall ? Mathf.Max(1f, jumpPackFallSpeedMultiplier * fallIntensity) : 1f;
        float jumpPackDurationMult = boostedFromJumpPackFall ? Mathf.Max(1f, jumpPackFallDurationMultiplier * fallIntensity) : 1f;

        float finalSpeedMult = Mathf.Max(runJumpSpeedMult, jumpPackSpeedMult);
        float finalDurationMult = Mathf.Max(runJumpDurationMult, jumpPackDurationMult);

        isSliding = true;
        slideTimer = slideDuration * finalDurationMult;
        slideCooldownTimer = slideCooldown;
        currentSlideSpeed = Mathf.Max(0f, slideSpeed * finalSpeedMult);

        slideDirection = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
        slideDirection.y = 0f;
        if (slideDirection.sqrMagnitude < 0.001f) slideDirection = Vector3.forward;
        slideDirection.Normalize();

        movement.SetCrouchStateFromSlide(false);
        movement.OverrideHorizontalVelocityFromSlide(Vector3.zero);

        if (slideAudioSource != null && slideClip != null)
        {
            float basePitch = boostedFromJumpPackFall ? 0.95f : (boostedFromRunJump ? 0.85f : 0.75f);
            slideAudioSource.pitch = basePitch + UnityEngine.Random.Range(-pitchRandomness * 0.5f, pitchRandomness * 0.5f);
            slideAudioSource.PlayOneShot(slideClip, slideVolume);
        }
    }

    private void EndSlide(bool standUpImmediately)
    {
        isSliding = false;
        bool crouchAfter = !standUpImmediately && movement.IsCrouchHeld();
        movement.SetCrouchStateFromSlide(crouchAfter);
        float exitSpeed = standUpImmediately ? 0f : Mathf.Min(currentSlideSpeed, crouchAfter ? 2f : 4f);
        movement.OverrideHorizontalVelocityFromSlide(slideDirection * exitSpeed);

        if (standUpImmediately && controller != null)
        {
            controller.height = movement.DefaultHeight;
            controller.center = movement.DefaultCenter;
        }
    }
}
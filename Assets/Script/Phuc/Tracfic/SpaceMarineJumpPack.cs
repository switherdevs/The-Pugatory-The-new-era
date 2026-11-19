using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class SpaceMarineJumpPack : MonoBehaviour
{
    [Header("Input Settings")]
    [Tooltip("Phím kích hoạt / tắt Jump Pack.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.C;

    [Header("Flight & Hover Settings (Lơ lửng mượt mà)")]
    [Tooltip("Lực đẩy lên ban đầu khi vừa bật Jump Pack.")]
    [SerializeField] private float liftForce = 18f;
    [Tooltip("Biên độ lơ lửng lên xuống (mét).")]
    [SerializeField] private float hoverAmplitude = 0.35f;
    [Tooltip("Tốc độ dao động lơ lửng lên xuống.")]
    [SerializeField] private float hoverFrequency = 2.5f;
    [Tooltip("Độ mượt khi giữ độ cao và lơ lửng.")]
    [SerializeField] private float hoverSmoothness = 6f;
    [Tooltip("Tốc độ di chuyển ngang khi đang bay trên không.")]
    [SerializeField] private float airMoveSpeed = 8f;

    [Header("Energy & UI Settings")]
    [Tooltip("Năng lượng tối đa của Jump Pack.")]
    [SerializeField] private float maxEnergy = 100f;
    [Tooltip("Năng lượng tiêu hao mỗi giây khi đang bay.")]
    [SerializeField] private float energyDrainRate = 22f;
    [Tooltip("Năng lượng hồi phục mỗi giây khi tắt Jump Pack.")]
    [SerializeField] private float energyRegenRate = 15f;
    [Tooltip("Thời gian trễ (giây) sau khi tắt/hết năng lượng mới bắt đầu hồi lại.")]
    [SerializeField] private float regenDelay = 1.0f;
    [Tooltip("Thanh Slider hiển thị năng lượng Jump Pack.")]
    [SerializeField] private Slider energySlider;

    [Header("VFX / Smoke Effect Settings")]
    [Tooltip("GameObject chứa hiệu ứng khói / tia lửa phản lực của Jump Pack.")]
    [SerializeField] private GameObject smokeEffectObject;

    private Rigidbody rb;
    private float currentEnergy;
    private float lastActiveTime;
    private float hoverTimer;
    private bool isFlying = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        currentEnergy = maxEnergy;

        if (energySlider != null)
        {
            energySlider.minValue = 0f;
            energySlider.maxValue = maxEnergy;
            energySlider.value = currentEnergy;
        }

        SetSmokeActive(false);
    }

    private void Update()
    {
        HandleToggleInput();
        HandleEnergy();
        UpdateUI();
    }

    private void FixedUpdate()
    {
        if (isFlying)
        {
            ApplyHoverFlight();
            ApplyAirMovement();
        }
    }

    /// <summary>
    /// Xử lý nhấn phím C để bật/tắt Jump Pack
    /// </summary>
    private void HandleToggleInput()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (!isFlying)
            {
                // Chỉ bật nếu còn năng lượng
                if (currentEnergy > 1f)
                {
                    ActivateJumpPack();
                }
            }
            else
            {
                DeactivateJumpPack();
            }
        }
    }

    /// <summary>
    /// Kích hoạt chế độ bay và bật hiệu ứng khói
    /// </summary>
    private void ActivateJumpPack()
    {
        isFlying = true;
        hoverTimer = 0f;
        SetSmokeActive(true);

        // Cho nhân vật một lực nhấc nhẹ lên khi khởi động
        rb.useGravity = false;
        Vector3 vel = rb.linearVelocity;
        vel.y = Mathf.Max(vel.y, 0f) + (liftForce * 0.25f);
        rb.linearVelocity = vel;
    }

    /// <summary>
    /// Tắt chế độ bay, trả lại trọng lực và ẩn hiệu ứng khói
    /// </summary>
    private void DeactivateJumpPack()
    {
        if (!isFlying) return;

        isFlying = false;
        lastActiveTime = Time.time;
        rb.useGravity = true;
        SetSmokeActive(false);
    }

    /// <summary>
    /// Tạo hiệu ứng lơ lửng lên xuống nhẹ nhàng, mượt mà bằng hàm Sin
    /// </summary>
    private void ApplyHoverFlight()
    {
        hoverTimer += Time.fixedDeltaTime * hoverFrequency;

        // Vận tốc dọc dao động nhẹ nhàng quanh mức 0 (lên xuống mượt mà)
        float targetVerticalVelocity = Mathf.Cos(hoverTimer) * hoverAmplitude * hoverFrequency;

        Vector3 currentVel = rb.linearVelocity;
        float smoothY = Mathf.Lerp(currentVel.y, targetVerticalVelocity, hoverSmoothness * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(currentVel.x, smoothY, currentVel.z);
    }

    /// <summary>
    /// Điều khiển di chuyển ngang (WASD / Arrow) mượt mà khi đang lơ lửng
    /// </summary>
    private void ApplyAirMovement()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        Vector3 moveDir = (transform.right * moveX + transform.forward * moveZ).normalized;
        Vector3 targetHorizontalVel = moveDir * airMoveSpeed;

        Vector3 currentVel = rb.linearVelocity;
        Vector3 smoothedHorizontal = Vector3.Lerp(
            new Vector3(currentVel.x, 0f, currentVel.z),
            targetHorizontalVel,
            8f * Time.fixedDeltaTime
        );

        rb.linearVelocity = new Vector3(smoothedHorizontal.x, currentVel.y, smoothedHorizontal.z);
    }

    /// <summary>
    /// Quản lý tiêu hao và hồi phục năng lượng
    /// </summary>
    private void HandleEnergy()
    {
        if (isFlying)
        {
            currentEnergy -= energyDrainRate * Time.deltaTime;
            if (currentEnergy <= 0f)
            {
                currentEnergy = 0f;
                DeactivateJumpPack();
            }
        }
        else
        {
            if (Time.time >= lastActiveTime + regenDelay && currentEnergy < maxEnergy)
            {
                currentEnergy += energyRegenRate * Time.deltaTime;
                currentEnergy = Mathf.Min(currentEnergy, maxEnergy);
            }
        }
    }

    /// <summary>
    /// Cập nhật giá trị lên UI Slider
    /// </summary>
    private void UpdateUI()
    {
        if (energySlider != null)
        {
            energySlider.value = currentEnergy;
        }
    }

    /// <summary>
    /// Bật / tắt GameObject hiệu ứng khói
    /// </summary>
    private void SetSmokeActive(bool active)
    {
        if (smokeEffectObject != null && smokeEffectObject.activeSelf != active)
        {
            smokeEffectObject.SetActive(active);
        }
    }

    private void OnDisable()
    {
        // Đảm bảo luôn trả lại trọng lực và tắt khói nếu script bị disable
        if (rb != null)
        {
            rb.useGravity = true;
        }
        SetSmokeActive(false);
    }
}
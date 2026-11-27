using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshPro))]
public class DamagePopup3D : MonoBehaviour
{
    [Header("Tham Chiếu TextMeshPro 3D")]
    [SerializeField] private TextMeshPro textMesh3D;

    [Header("Hiệu Ứng Pop-Up (Tùy Chỉnh)")]
    [Tooltip("Tổng thời gian hiển thị của popup (giây).")]
    [SerializeField] private float lifetime = 0.85f;

    [Tooltip("Tốc độ bay thẳng đứng lên trên của popup (m/s).")]
    [SerializeField] private float floatSpeed = 1.35f;

    [Tooltip("Tốc độ bay ngang nhẹ thêm vào nếu có.")]
    [SerializeField] private Vector3 horizontalDrift = new Vector3(0.25f, 0f, 0f);

    [Tooltip("Đường cong hiệu ứng pop-up phóng to / bật nảy theo tiến trình [0..1].")]
    [SerializeField] private AnimationCurve popScaleCurve = new AnimationCurve(
        new Keyframe(0f, 0.25f),
        new Keyframe(0.22f, 1.35f),
        new Keyframe(0.45f, 1.0f),
        new Keyframe(1f, 0.7f)
    );

    [Tooltip("Hệ số phóng đại thêm khi là sát thương chí mạng.")]
    [SerializeField] private float criticalScaleMultiplier = 1.3f;

    [Tooltip("Tỷ lệ thời gian (0..1) bắt đầu làm mờ dần trước khi biến mất.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float fadeOutStartRatio = 0.55f;

    [Header("Màu Sắc & Định Dạng Số")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color criticalColor = new Color(1f, 0.28f, 0.15f, 1f);
    [SerializeField] private string criticalSuffix = "!";
    [SerializeField] private bool roundToInteger = true;

    [Header("Hướng Về Camera (Billboard)")]
    [Tooltip("Tự động xoay mặt chữ về phía Camera chính.")]
    [SerializeField] private bool faceMainCamera = true;

    private Camera targetCamera;
    private Vector3 baseScale = Vector3.one;
    private Vector3 activeDrift;
    private Color activeColor;
    private float elapsed;
    private bool hasInitialized;

    private void Awake()
    {
        if (textMesh3D == null)
        {
            textMesh3D = GetComponent<TextMeshPro>();
        }

        baseScale = transform.localScale;
        if (baseScale.sqrMagnitude < 0.0001f)
        {
            baseScale = Vector3.one;
        }

        targetCamera = Camera.main;
        activeColor = normalColor;
    }

    public void HienThi(float damageAmount, bool isCritical = false)
    {
        if (textMesh3D == null)
        {
            textMesh3D = GetComponent<TextMeshPro>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        string numberText = roundToInteger && damageAmount >= 1f
            ? Mathf.RoundToInt(damageAmount).ToString()
            : damageAmount.ToString("0.#");

        if (textMesh3D != null)
        {
            textMesh3D.text = isCritical ? numberText + criticalSuffix : numberText;
        }

        activeColor = isCritical ? criticalColor : normalColor;
        float critMult = isCritical ? Mathf.Max(1f, criticalScaleMultiplier) : 1f;
        activeDrift = new Vector3(
            Random.Range(-horizontalDrift.x, horizontalDrift.x),
            Random.Range(-horizontalDrift.y, horizontalDrift.y),
            Random.Range(-horizontalDrift.z, horizontalDrift.z)
        );

        elapsed = 0f;
        hasInitialized = true;
        ApplyAnimationFrame(0f, critMult);
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;

        if (!hasInitialized)
        {
            hasInitialized = true;
            if (textMesh3D != null && string.IsNullOrEmpty(textMesh3D.text))
            {
                textMesh3D.text = "0";
            }
        }

        elapsed += dt;
        float dur = Mathf.Max(0.05f, lifetime);
        float progress = Mathf.Clamp01(elapsed / dur);

        transform.position += (Vector3.up * floatSpeed + activeDrift) * dt;
        ApplyAnimationFrame(progress, 1f);

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void LateUpdate()
    {
        if (!faceMainCamera) return;

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            Vector3 lookDir = transform.position - targetCamera.transform.position;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }
        }
    }

    private void ApplyAnimationFrame(float progress, float extraScaleMult)
    {
        float curveValue = popScaleCurve != null ? Mathf.Max(0f, popScaleCurve.Evaluate(progress)) : 1f;
        transform.localScale = baseScale * curveValue * extraScaleMult;

        if (textMesh3D != null)
        {
            float alpha = activeColor.a;
            if (progress > fadeOutStartRatio)
            {
                float fadeDenom = Mathf.Max(0.001f, 1f - fadeOutStartRatio);
                float fadeT = Mathf.Clamp01((progress - fadeOutStartRatio) / fadeDenom);
                alpha = Mathf.Lerp(activeColor.a, 0f, fadeT);
            }

            Color c = activeColor;
            c.a = alpha;
            textMesh3D.color = c;
        }
    }
}
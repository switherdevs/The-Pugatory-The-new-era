using UnityEngine;

public class CrosshairController : MonoBehaviour
{
    [Header("Crosshair Parts (RectTransforms)")]
    [SerializeField] private RectTransform topPart;
    [SerializeField] private RectTransform bottomPart;
    [SerializeField] private RectTransform leftPart;
    [SerializeField] private RectTransform rightPart;

    [Header("Spread Configuration")]
    [Tooltip("Khoảng cách cơ bản của tâm khi đứng yên (Ví dụ: 15 hoặc 150)")]
    [SerializeField] private float minSpread = 15f;
    [Tooltip("Khoảng cách giãn nở tối đa khi xả súng (Tâm sẽ vọt thẳng lên mức này khi bắn)")]
    [SerializeField] private float maxSpread = 200f;
    [Tooltip("Tốc độ thu hồi tâm về mức đứng yên (Số càng lớn thu về càng nhanh)")]
    [SerializeField] private float returnSpeed = 15f;

    private float currentSpread;

    private void Start()
    {
        currentSpread = minSpread;
    }

    private void Update()
    {
        // Khi không bắn (hoặc giữa các viên đạn), tâm sẽ tự động tụt dần về mức minSpread
        if (currentSpread > minSpread)
        {
            currentSpread -= returnSpeed * Time.deltaTime * 60f;
            currentSpread = Mathf.Max(currentSpread, minSpread);
        }

        // Cập nhật tọa độ thực tế lên 4 thanh UI
        UpdateCrosshairPositions();
    }

    /// <summary>
    /// Hàm nhận tín hiệu từ súng mỗi khi bắn
    /// </summary>
    public void SpreadCrosshair(float spreadAmount)
    {
        // VỌT THẲNG LÊN MAX: Khi bắn, tâm lập tức nhảy vọt lên mức tối đa (maxSpread) 
        // hoặc cộng dồn kịch trần ngay lập tức để người chơi thấy rõ độ bung nở.
        currentSpread = maxSpread;
    }

    private void UpdateCrosshairPositions()
    {
        if (topPart == null || bottomPart == null || leftPart == null || rightPart == null) return;

        // Gán trực tiếp giá trị currentSpread vào tọa độ anchoredPosition
        topPart.anchoredPosition = new Vector2(0f, currentSpread);
        bottomPart.anchoredPosition = new Vector2(0f, -currentSpread);
        leftPart.anchoredPosition = new Vector2(-currentSpread, 0f);
        rightPart.anchoredPosition = new Vector2(currentSpread, 0f);
    }
}
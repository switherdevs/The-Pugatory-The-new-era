using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class RandomSprite2D : MonoBehaviour
{
    [Header("Danh sách Sprite ngẫu nhiên")]
    [Tooltip("Kéo thả các Sprite bạn muốn random vào đây trong Inspector.")]
    [SerializeField] private Sprite[] spriteArray;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        // Tự động tìm và gán component SpriteRenderer có sẵn trên GameObject này
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        // Gọi hàm đổi sprite ngay khi đối tượng vừa được sinh ra (spawn)
        ApplyRandomSprite();
    }

    /// <summary>
    /// Hàm chọn và gán một sprite ngẫu nhiên từ mảng
    /// </summary>
    public void ApplyRandomSprite()
    {
        // Kiểm tra xem mảng có dữ liệu không để tránh lỗi NullReferenceException
        if (spriteArray != null && spriteArray.Length > 0)
        {
            // Random.Range với số nguyên (int) sẽ lấy từ min (bao gồm) đến max - 1 (loại trừ max)
            int randomIndex = Random.Range(0, spriteArray.Length);

            // Gán sprite ngẫu nhiên vừa chọn vào SpriteRenderer để hiển thị lên màn hình
            spriteRenderer.sprite = spriteArray[randomIndex];
        }
        else
        {
            Debug.LogWarning("Mảng Sprite đang trống! Hãy thêm ít nhất một Sprite vào Inspector.");
        }
    }
}
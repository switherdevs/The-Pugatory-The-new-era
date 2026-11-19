using UnityEngine;

public class SelfDestroy : MonoBehaviour
{
    public enum DestroyMode { DestroyImmediately, DestroyAfterTime }

    [Header("Destroy Settings")]
    [Tooltip("Chọn chế độ hủy: Hủy ngay lập tức hoặc hủy sau một khoảng thời gian.")]
    [SerializeField] private DestroyMode destroyMode = DestroyMode.DestroyAfterTime;

    [Tooltip("Thời gian chờ trước khi hủy (chỉ dùng khi chọn chế độ DestroyAfterTime).")]
    [SerializeField] private float lifeTime = 2.0f;

    private void Start()
    {
        if (destroyMode == DestroyMode.DestroyImmediately)
        {
            Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject, lifeTime);
        }
    }
}
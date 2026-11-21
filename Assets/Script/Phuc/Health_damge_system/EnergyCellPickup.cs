using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnergyCellPickup : MonoBehaviour
{
    [Header("Thiết lập Hồi Giáp")]
    [Tooltip("Lượng năng lượng giáp được hồi lại khi nhặt pin.")]
    [SerializeField] private float luongGiapHoi = 35f;

    [Tooltip("Chỉ cho phép Người chơi nhặt viên pin này.")]
    [SerializeField] private bool chiNguoiChoiMoiNhatDuoc = true;

    [Tooltip("Không cho nhặt nếu giáp của người chơi đã đầy.")]
    [SerializeField] private bool chiNhatKhiGiapChuaDay = true;

    [Header("Hiệu ứng xoay & Nhặt")]
    [Tooltip("Tốc độ tự xoay của viên pin (độ/giây, đặt 0 nếu không muốn xoay).")]
    [SerializeField] private float tocDoXoay = 90f;

    [Tooltip("Âm thanh phát ra khi nhặt pin (tùy chọn).")]
    [SerializeField] private AudioClip amThanhNhatPin;

    [Range(0f, 1f)]
    [SerializeField] private float amLuong = 0.9f;

    [Tooltip("Hiệu ứng sinh ra khi nhặt pin (tùy chọn).")]
    [SerializeField] private GameObject hieuUngNhatPin;

    private bool daDuocNhat = false;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Update()
    {
        if (daDuocNhat) return;

        if (Mathf.Abs(tocDoXoay) > 0.01f)
        {
            transform.Rotate(0f, tocDoXoay * Time.deltaTime, 0f, Space.World);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (daDuocNhat || other == null) return;

        HealthSystem heThongMau = other.GetComponentInParent<HealthSystem>();
        if (heThongMau == null || heThongMau.DaChet) return;

        if (chiNguoiChoiMoiNhatDuoc && heThongMau.Phe != HealthSystem.LoaiDoiTuong.NguoiChoi)
        {
            return;
        }

        if (chiNhatKhiGiapChuaDay && heThongMau.GiapHienTai >= heThongMau.GiapToiDa - 0.001f)
        {
            return;
        }

        daDuocNhat = true;
        heThongMau.HoiGiap(luongGiapHoi);

        if (hieuUngNhatPin != null)
        {
            Instantiate(hieuUngNhatPin, transform.position, Quaternion.identity);
        }

        if (amThanhNhatPin != null)
        {
            AudioSource.PlayClipAtPoint(amThanhNhatPin, transform.position, amLuong);
        }

        Destroy(gameObject);
    }
}
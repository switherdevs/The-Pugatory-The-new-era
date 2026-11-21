using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    public enum PheGaySatThuong
    {
        TuNguoiChoi, // Chỉ gây sát thương cho Quái vật
        TuQuaiVat,   // Chỉ gây sát thương cho Người chơi
        TatCa        // Gây sát thương cho cả hai (bẫy, nổ thùng xăng...)
    }

    [Header("Thiết lập Sát thương")]
    [Tooltip("Xác định nguồn sát thương này thuộc phe nào để không tự bắn trúng bản thân/đồng đội.")]
    [SerializeField] private PheGaySatThuong pheGaySatThuong = PheGaySatThuong.TuQuaiVat;

    [Tooltip("Lượng sát thương gây ra mỗi lần trúng.")]
    [SerializeField] private float satThuong = 15f;

    [Tooltip("Hủy đạn/vật thể ngay sau khi va chạm trúng mục tiêu (bật cho đạn bay, tắt cho bẫy gai/vùng lửa).")]
    [SerializeField] private bool huySauKhiVaCham = true;

    [Tooltip("Thời gian tự hủy nếu đạn bay ra ngoài khoảng không (giây). Đặt <= 0 nếu không tự hủy.")]
    [SerializeField] private float thoiGianTonTaiToiDa = 5f;

    [Tooltip("Thời gian giãn cách giữa 2 lần gây sát thương liên tục nếu mục tiêu đứng trong vùng bẫy (giây).")]
    [SerializeField] private float thoiGianHoiSatThuongLienTuc = 0.5f;

    [Tooltip("Hiệu ứng tia lửa/nổ khi đạn va chạm (tùy chọn).")]
    [SerializeField] private GameObject hieuUngVaCham;

    private Transform chuSoHuu;
    private float thoiDiemGaySatThuongTiepTheo = 0f;

    private void Start()
    {
        if (thoiGianTonTaiToiDa > 0f && huySauKhiVaCham)
        {
            Destroy(gameObject, thoiGianTonTaiToiDa);
        }
    }

    /// <summary>
    /// Nhận sát thương truyền từ RangedEnemyAI / súng qua SendMessage("SetDamage", value).
    /// </summary>
    public void SetDamage(float amount)
    {
        satThuong = amount;
    }

    /// <summary>
    /// Gán người bắn để đảm bảo đạn không bao giờ tự va chạm với chính người bắn.
    /// </summary>
    public void SetOwner(Transform ownerRoot)
    {
        chuSoHuu = ownerRoot;
    }

    private void OnTriggerEnter(Collider other)
    {
        XuLyVaCham(other.gameObject, other.ClosestPoint(transform.position));
    }

    private void OnTriggerStay(Collider other)
    {
        // Dùng cho vùng sát thương liên tục (bẫy lửa, độc...) khi không hủy sau va chạm
        if (!huySauKhiVaCham && Time.time >= thoiDiemGaySatThuongTiepTheo)
        {
            if (XuLyVaCham(other.gameObject, other.ClosestPoint(transform.position)))
            {
                thoiDiemGaySatThuongTiepTheo = Time.time + thoiGianHoiSatThuongLienTuc;
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 diemCham = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        XuLyVaCham(collision.gameObject, diemCham);
    }

    private bool XuLyVaCham(GameObject doiTuongVaCham, Vector3 diemVaCham)
    {
        if (doiTuongVaCham == null) return false;

        // Bỏ qua chính người bắn
        if (chuSoHuu != null && doiTuongVaCham.transform.root == chuSoHuu.root)
        {
            return false;
        }

        HealthSystem heThongMau = doiTuongVaCham.GetComponentInParent<HealthSystem>();
        bool daGaySatThuong = false;

        if (heThongMau != null)
        {
            if (!CoHopLeDeGaySatThuong(heThongMau))
            {
                return false;
            }

            heThongMau.NhanSatThuong(satThuong);
            daGaySatThuong = true;
        }

        if (hieuUngVaCham != null)
        {
            Instantiate(hieuUngVaCham, diemVaCham, Quaternion.identity);
        }

        if (huySauKhiVaCham)
        {
            Destroy(gameObject);
        }

        return daGaySatThuong;
    }

    private bool CoHopLeDeGaySatThuong(HealthSystem mucTieu)
    {
        if (mucTieu.DaChet) return false;

        switch (pheGaySatThuong)
        {
            case PheGaySatThuong.TuNguoiChoi:
                return mucTieu.Phe == HealthSystem.LoaiDoiTuong.QuaiVat;

            case PheGaySatThuong.TuQuaiVat:
                return mucTieu.Phe == HealthSystem.LoaiDoiTuong.NguoiChoi;

            case PheGaySatThuong.TatCa:
            default:
                return true;
        }
    }
}
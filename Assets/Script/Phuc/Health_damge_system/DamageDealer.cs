using System.Collections.Generic;
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

    [Header("Chế độ nhận va chạm (Collision & Trigger)")]
    [Tooltip("Cho phép gây sát thương thông qua OnCollisionEnter / OnCollisionStay (va chạm vật lý cứng).")]
    [SerializeField] private bool gaySatThuongQuaCollision = true;

    [Tooltip("Cho phép gây sát thương thông qua OnTriggerEnter / OnTriggerStay (vùng trigger / đạn trigger).")]
    [SerializeField] private bool gaySatThuongQuaTrigger = true;

    [Tooltip("Cho phép gây sát thương duy trì khi giữ va chạm (OnCollisionStay / OnTriggerStay) nếu không hủy sau va chạm.")]
    [SerializeField] private bool gaySatThuongKhiDangCham = true;

    private Transform chuSoHuu;
    private float thoiDiemGaySatThuongTiepTheo = 0f;
    private readonly Dictionary<int, float> thoiDiemTrúngTheoMucTieu = new Dictionary<int, float>();
    private bool daBiHuyHoacHoanTat = false;

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
        if (!gaySatThuongQuaTrigger || other == null) return;
        XuLyVaCham(other.gameObject, other.ClosestPoint(transform.position), laVaChamLienTuc: false);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!gaySatThuongQuaTrigger || !gaySatThuongKhiDangCham || huySauKhiVaCham || other == null) return;
        XuLyVaCham(other.gameObject, other.ClosestPoint(transform.position), laVaChamLienTuc: true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!gaySatThuongQuaCollision || collision == null) return;
        Vector3 diemCham = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        XuLyVaCham(collision.gameObject, diemCham, laVaChamLienTuc: false);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!gaySatThuongQuaCollision || !gaySatThuongKhiDangCham || huySauKhiVaCham || collision == null) return;
        Vector3 diemCham = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        XuLyVaCham(collision.gameObject, diemCham, laVaChamLienTuc: true);
    }

    private bool XuLyVaCham(GameObject doiTuongVaCham, Vector3 diemVaCham, bool laVaChamLienTuc)
    {
        if (daBiHuyHoacHoanTat || doiTuongVaCham == null) return false;

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

            int targetId = heThongMau.GetInstanceID();
            float thoiGianHoi = Mathf.Max(0.05f, thoiGianHoiSatThuongLienTuc);

            if (laVaChamLienTuc)
            {
                if (Time.time < thoiDiemGaySatThuongTiepTheo)
                {
                    return false;
                }

                if (thoiDiemTrúngTheoMucTieu.TryGetValue(targetId, out float nextTargetHitTime) && Time.time < nextTargetHitTime)
                {
                    return false;
                }
            }
            else
            {
                // Chống kích hoạt kép cả OnCollisionEnter và OnTriggerEnter cùng lúc trong 0.02s
                if (thoiDiemTrúngTheoMucTieu.TryGetValue(targetId, out float lastHitTime) && Time.time - lastHitTime < 0.02f)
                {
                    return false;
                }
            }

            heThongMau.NhanSatThuong(satThuong);
            daGaySatThuong = true;
            thoiDiemTrúngTheoMucTieu[targetId] = Time.time + thoiGianHoi;

            if (laVaChamLienTuc)
            {
                thoiDiemGaySatThuongTiepTheo = Time.time + thoiGianHoi;
            }
        }

        if (hieuUngVaCham != null)
        {
            Instantiate(hieuUngVaCham, diemVaCham, Quaternion.identity);
        }

        if (huySauKhiVaCham)
        {
            daBiHuyHoacHoanTat = true;
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
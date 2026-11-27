using UnityEngine;

// Bắt buộc GameObject gắn script này phải có Collider (dùng làm vùng hitbox cho từng bộ phận cơ thể quái)
[RequireComponent(typeof(Collider))]
public class EnemyBodyPartHitbox : MonoBehaviour
{
    // Phân loại vùng cơ thể để tính hệ số sát thương và hiệu ứng riêng biệt
    public enum LoaiVungCoThe
    {
        Chan,
        Than,
        Dau
    }

    [Header("Phân Loại Vùng Cơ Thể")]
    [Tooltip("Chọn vùng cơ thể tương ứng với Collider này: Chân, Thân hoặc Đầu.")]
    [SerializeField] private LoaiVungCoThe vungCoThe = LoaiVungCoThe.Than;

    [Header("Hệ Số Sát Thương Theo Vùng")]
    [Tooltip("Hệ số nhân sát thương khi bắn trúng Chân.")]
    [SerializeField] private float heSoSatThuongChan = 1.0f;

    [Tooltip("Hệ số nhân sát thương khi bắn trúng Thân.")]
    [SerializeField] private float heSoSatThuongThan = 1.0f;

    [Tooltip("Hệ số nhân sát thương khi bắn trúng Đầu (thường cao hơn để thưởng headshot).")]
    [SerializeField] private float heSoSatThuongDau = 2.0f;

    [Header("Hiệu Ứng Làm Chậm (Vùng Chân)")]
    [Tooltip("Tốc độ di chuyển còn lại khi trúng Chân (ví dụ 0.45 = giảm còn 45% tốc độ gốc).")]
    [Range(0.05f, 1.0f)]
    [SerializeField] private float heSoTocDoKhiTrungChan = 0.45f;

    [Tooltip("Thời gian quái bị làm chậm khi trúng Chân (tính bằng giây).")]
    [SerializeField] private float thoiGianLamCham = 2.5f;

    [Header("Hiệu Ứng Choáng (Đạn Gây Choáng & Đầu)")]
    [Tooltip("Bật nếu muốn đạn có cờ gây choáng làm choáng quái khi trúng Thân.")]
    [SerializeField] private bool choPhepDanChoangKhiTrungThan = true;

    [Tooltip("Bật nếu muốn trúng Đầu luôn gây choáng kể cả khi viên đạn không bật cờ gây choáng.")]
    [SerializeField] private bool choPhepChoangKhiTrungDau = true;

    [Tooltip("Thời gian quái bị choáng khi kích hoạt hiệu ứng choáng (tính bằng giây).")]
    [SerializeField] private float thoiGianChoang = 1.5f;

    [Header("Hiệu Ứng Hình Ảnh Khi Trúng Đạn")]
    [Tooltip("Prefab hiệu ứng (tia lửa, máu, tia sét...) sinh ra tại điểm va chạm.")]
    [SerializeField] private GameObject prefabHieuUngTrungVung;

    private HealthSystem heThongMau;
    private EnemyBodyStatusEffect boHieuUngTrangThai;
    private RangedEnemyAI aiDanhXa;
    private MeleeEnemyAI aiCanChien;

    // Thuộc tính đọc nhanh vùng cơ thể hiện tại từ bên ngoài nếu cần
    public LoaiVungCoThe VungCoThe => vungCoThe;

    private void Awake()
    {
        // Tìm các component chính trên object cha (thân chính của con quái)
        heThongMau = GetComponentInParent<HealthSystem>();
        boHieuUngTrangThai = GetComponentInParent<EnemyBodyStatusEffect>();

        // Nếu quái có HealthSystem nhưng chưa gắn EnemyBodyStatusEffect thì tự thêm vào object chứa HealthSystem
        if (boHieuUngTrangThai == null && heThongMau != null)
        {
            boHieuUngTrangThai = heThongMau.GetComponent<EnemyBodyStatusEffect>();
            if (boHieuUngTrangThai == null)
            {
                boHieuUngTrangThai = heThongMau.gameObject.AddComponent<EnemyBodyStatusEffect>();
            }
        }

        // Lấy tham chiếu AI (quái đánh xa hoặc quái cận chiến nếu có)
        aiDanhXa = GetComponentInParent<RangedEnemyAI>();
        aiCanChien = GetComponentInParent<MeleeEnemyAI>();
    }

    /// <summary>
    /// Hàm chính nhận đạn từ súng / viên đạn / raycast của người chơi.
    /// Không yêu cầu bắn liên tiếp: nếu viên đạn có laDanGayChoang = true thì sẽ gây choáng ngay khi trúng (tùy cấu hình vùng).
    /// </summary>
    public void NhanDan(float satThuongGoc, bool laDanGayChoang = false, Vector3 diemVaCham = default)
    {
        // Nếu quái đã chết thì không xử lý nhận đạn hoặc hiệu ứng nữa
        if (heThongMau != null && heThongMau.DaChet) return;

        // Đảm bảo sát thương gốc không bị số âm
        float satThuongAnToan = Mathf.Max(0f, satThuongGoc);
        float heSoVung = 1f;
        bool nenGayChoang = false;

        // Xử lý từng vùng cơ thể riêng biệt
        switch (vungCoThe)
        {
            case LoaiVungCoThe.Chan:
                heSoVung = Mathf.Max(0f, heSoSatThuongChan);

                // Trúng chân gây làm chậm ngay lập tức (không phụ thuộc FPS)
                if (boHieuUngTrangThai != null)
                {
                    boHieuUngTrangThai.ApDungLamCham(heSoTocDoKhiTrungChan, thoiGianLamCham);
                }

                // Nếu đạn có cờ gây choáng và trúng chân thì vẫn cho phép choáng
                nenGayChoang = laDanGayChoang;
                break;

            case LoaiVungCoThe.Than:
                heSoVung = Mathf.Max(0f, heSoSatThuongThan);

                // Trúng thân chỉ gây choáng khi viên đạn là đạn gây choáng VÀ cho phép trúng thân gây choáng
                nenGayChoang = laDanGayChoang && choPhepDanChoangKhiTrungThan;
                break;

            case LoaiVungCoThe.Dau:
                heSoVung = Mathf.Max(0f, heSoSatThuongDau);

                // Trúng đầu gây choáng nếu là đạn gây choáng HOẶC bật tùy chọn trúng đầu gây choáng
                nenGayChoang = laDanGayChoang || choPhepChoangKhiTrungDau;
                break;
        }

        // Áp dụng choáng nếu thỏa điều kiện của vùng và đạn
        if (nenGayChoang && boHieuUngTrangThai != null)
        {
            boHieuUngTrangThai.ApDungChoang(thoiGianChoang);
        }

        // Tính sát thương thực tế sau khi nhân hệ số vùng cơ thể
        float satThuongThucTe = Mathf.Max(0f, satThuongAnToan * heSoVung);

        // Trừ máu quái
        if (heThongMau != null && satThuongThucTe > 0f)
        {
            heThongMau.NhanSatThuong(satThuongThucTe);
        }

        // Thông báo sát thương cho AI để cập nhật trạng thái (ví dụ phát hiện người chơi, giật mình...)
        if (satThuongThucTe > 0f)
        {
            if (aiDanhXa != null)
            {
                aiDanhXa.NhanSatThuong(satThuongThucTe);
            }

            if (aiCanChien != null)
            {
                aiCanChien.NhanSatThuong(satThuongThucTe);
            }
        }

        // Sinh hiệu ứng tại điểm trúng đạn (nếu có truyền tọa độ hợp lệ thì dùng tọa độ đó, không thì lấy vị trí hitbox)
        if (prefabHieuUngTrungVung != null)
        {
            Vector3 viTriSinhHieuUng = diemVaCham.sqrMagnitude > 0.0001f ? diemVaCham : transform.position;
            Instantiate(prefabHieuUngTrungVung, viTriSinhHieuUng, Quaternion.identity);
        }
    }

    // Hàm phụ tương thích nếu script khác gọi NhanSatThuong(float) mặc định không phải đạn choáng
    public void NhanSatThuong(float satThuong)
    {
        NhanDan(satThuong, laDanGayChoang: false, transform.position);
    }

    // Hàm phụ tương thích chuẩn tiếng Anh TakeDamage(float) nếu hệ thống vũ khí khác gọi tên này
    public void TakeDamage(float luongSatThuong)
    {
        NhanDan(luongSatThuong, laDanGayChoang: false, transform.position);
    }
}
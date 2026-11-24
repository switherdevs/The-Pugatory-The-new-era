using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class HealthSystem : MonoBehaviour
{
    public enum LoaiDoiTuong
    {
        NguoiChoi,
        QuaiVat
    }

    [Header("1. Thiết lập Phe & Máu cơ bản")]
    [Tooltip("Chọn đối tượng này là Người chơi hay Quái vật.")]
    [SerializeField] private LoaiDoiTuong loaiDoiTuong = LoaiDoiTuong.QuaiVat;

    [Tooltip("Lượng máu tối đa.")]
    [SerializeField] private float mauToiDa = 100f;

    [Tooltip("Giáp giảm sát thương cố định khi hết năng lượng giáp (ví dụ: 5 nghĩa là trừ 5 sát thương, tối thiểu vẫn mất 1 máu).")]
    [SerializeField] private float giapPhongThu = 0f;

    [Header("2. Giáp Năng Lượng (Hấp thụ & Giới hạn mất tối đa 10% Máu)")]
    [Tooltip("Bật/tắt cơ chế giáp năng lượng (khi còn năng lượng giáp sẽ chỉ trừ tối đa 10% máu tối đa trở xuống tùy damage).")]
    [SerializeField] private bool dungGiapNangLuong = true;

    [Tooltip("Lượng năng lượng giáp tối đa.")]
    [SerializeField] private float giapToiDa = 100f;

    [Tooltip("Lượng năng lượng giáp khởi đầu (để < 0 sẽ tự lấy bằng Giáp Tối Đa).")]
    [SerializeField] private float giapKhoiDau = -1f;

    [Tooltip("Tỉ lệ máu tối đa bị mất khi đang có giáp năng lượng (mặc định 0.1 = 10% máu tối đa).")]
    [Range(0.01f, 1f)]
    [SerializeField] private float tiLeMauMatToiDaKhiCoGiap = 0.1f;

    [Tooltip("Mức sát thương chuẩn để chạm ngưỡng mất tối đa 10% máu (damage nhỏ hơn mức này sẽ mất ít hơn 10% máu).")]
    [SerializeField] private float nguongDamageDatTranGiap = 50f;

    [Tooltip("Hệ số tiêu hao năng lượng giáp theo sát thương nhận vào (ví dụ: 1 nghĩa là nhận 20 damage thì tốn 20 năng lượng giáp).")]
    [SerializeField] private float heSoTieuHaoGiap = 1f;

    [Header("3. Bất tử tạm thời & Hồi máu (Chủ yếu cho Người chơi)")]
    [Tooltip("Thời gian miễn nhiễm sát thương sau khi vừa trúng đòn (giây). Nên đặt 0 cho Quái để nhận đủ sát thương súng liên thanh.")]
    [SerializeField] private float thoiGianBatTuSauTrungDon = 0f;

    [Tooltip("Bật/tắt tự động hồi máu khi không giao tranh.")]
    [SerializeField] private bool tuDongHoiMau = false;

    [Tooltip("Thời gian chờ sau lần trúng đòn cuối cùng mới bắt đầu hồi máu (giây).")]
    [SerializeField] private float thoiGianChoHoiMau = 5f;

    [Tooltip("Lượng máu hồi phục mỗi giây.")]
    [SerializeField] private float tocDoHoiMau = 10f;

    [Header("4. Thanh máu & Thanh giáp UI (Tùy chọn)")]
    [Tooltip("Kéo Slider thanh máu của Player (trên HUD) hoặc thanh máu trên đầu Quái vào đây.")]
    [SerializeField] private Slider thanhMauSlider;

    [Tooltip("Kéo Slider thanh năng lượng giáp vào đây (tùy chọn).")]
    [SerializeField] private Slider thanhGiapSlider;

    [Tooltip("Tự động ẩn thanh máu của Quái khi đầy máu hoặc khi đã chết.")]
    [SerializeField] private bool anThanhMauKhiDay = true;

    [Header("5. Âm thanh & Hiệu ứng")]
    [SerializeField] private AudioSource nguonAmThanh;
    [SerializeField] private AudioClip amThanhTrungDon;
    [SerializeField] private AudioClip amThanhGiapChan;
    [SerializeField] private AudioClip amThanhHoiGiap;
    [SerializeField] private AudioClip amThanhChet;
    [Range(0f, 1f)]
    [SerializeField] private float amLuong = 0.9f;

    [Tooltip("Prefab hiệu ứng nổ/máu/tia lửa sinh ra khi bị hạ gục (tùy chọn).")]
    [SerializeField] private GameObject hieuUngKhiChet;

    [Tooltip("Thời gian trễ trước khi hủy GameObject quái sau khi chết (giây).")]
    [SerializeField] private float thoiGianHuyKhiChet = 0.15f;

    [Header("6. Sự kiện mở rộng (Events)")]
    public UnityEvent<float, float> onMauThayDoi; // (mauHienTai, mauToiDa)
    public UnityEvent<float, float> onGiapThayDoi; // (giapHienTai, giapToiDa)
    public UnityEvent onTrungDon;
    public UnityEvent onChet;

    private float mauHienTai;
    private float giapHienTai;
    private bool daChet = false;
    private float thoiDiemTrungDonCuoi = -999f;

    // Tham chiếu tự động tới AI để quái quay lại truy đuổi ngay khi bị bắn từ xa
    private MeleeEnemyAI meleeAI;
    private RangedEnemyAI rangedAI;

    // Các Property công khai để UI hoặc script khác đọc
    public LoaiDoiTuong Phe => loaiDoiTuong;
    public float MauHienTai => mauHienTai;
    public float MauToiDa => mauToiDa;
    public float TiLeMau => mauToiDa > 0f ? mauHienTai / mauToiDa : 0f;
    public float GiapHienTai => giapHienTai;
    public float GiapToiDa => giapToiDa;
    public float TiLeGiap => giapToiDa > 0f ? giapHienTai / giapToiDa : 0f;
    public bool CoGiapNangLuong => dungGiapNangLuong && giapHienTai > 0.001f;
    public bool DaChet => daChet;

    private void Awake()
    {
        mauToiDa = Mathf.Max(1f, mauToiDa);
        giapToiDa = Mathf.Max(0f, giapToiDa);

        mauHienTai = mauToiDa;
        giapHienTai = giapKhoiDau >= 0f
            ? Mathf.Clamp(giapKhoiDau, 0f, giapToiDa)
            : giapToiDa;

        if (nguonAmThanh == null)
        {
            nguonAmThanh = GetComponent<AudioSource>();
            if (nguonAmThanh == null)
            {
                nguonAmThanh = gameObject.AddComponent<AudioSource>();
                nguonAmThanh.playOnAwake = false;
            }
        }

        if (loaiDoiTuong == LoaiDoiTuong.QuaiVat)
        {
            meleeAI = GetComponent<MeleeEnemyAI>();
            rangedAI = GetComponent<RangedEnemyAI>();
        }

        KhoiTaoUI();
    }

    private void Update()
    {
        if (daChet) return;

        if (tuDongHoiMau && mauHienTai < mauToiDa && Time.time >= thoiDiemTrungDonCuoi + thoiGianChoHoiMau)
        {
            HoiMau(tocDoHoiMau * Time.deltaTime);
        }
    }

    /// <summary>
    /// Hàm nhận sát thương chuẩn (tương thích với SendMessage("TakeDamage", amount) từ mọi script vũ khí và quái).
    /// </summary>
    public void TakeDamage(float amount)
    {
        NhanSatThuong(amount);
    }

    /// <summary>
    /// Xử lý trừ giáp/máu, kích hoạt phản ứng AI, cập nhật UI và kiểm tra hạ gục.
    /// Khi còn năng lượng giáp: chỉ mất tối đa 10% máu tối đa trở xuống tùy theo sát thương.
    /// </summary>
    public void NhanSatThuong(float satThuongGoc)
    {
        if (daChet || satThuongGoc <= 0f) return;

        // Kiểm tra thời gian bất tử tạm thời (i-frames)
        if (thoiGianBatTuSauTrungDon > 0f && Time.time < thoiDiemTrungDonCuoi + thoiGianBatTuSauTrungDon)
        {
            return;
        }

        thoiDiemTrungDonCuoi = Time.time;
        float satThuongVaoMau = 0f;
        bool daChanBangGiap = false;

        if (dungGiapNangLuong && giapHienTai > 0.001f)
        {
            daChanBangGiap = true;

            // Mức mất máu tối đa theo cấu hình (mặc định 10% máu tối đa)
            float tranMauMat = mauToiDa * Mathf.Clamp01(tiLeMauMatToiDaKhiCoGiap);

            // Làm mượt đường cong sát thương khi trúng giáp để đạn súng không bị bóp quá nhỏ
            float nguongAnToan = Mathf.Max(0.01f, nguongDamageDatTranGiap);
            float tyLeDamage = Mathf.Clamp01(satThuongGoc / nguongAnToan);
            float heSoDamage = loaiDoiTuong == LoaiDoiTuong.QuaiVat
                ? Mathf.Sqrt(tyLeDamage)
                : tyLeDamage;

            float satThuongToiThieu = loaiDoiTuong == LoaiDoiTuong.QuaiVat
                ? Mathf.Min(satThuongGoc * 0.5f, tranMauMat)
                : 0f;

            satThuongVaoMau = Mathf.Max(satThuongToiThieu, Mathf.Min(satThuongGoc, tranMauMat * heSoDamage));
            satThuongVaoMau = Mathf.Clamp(satThuongVaoMau, 0f, tranMauMat);

            // Trừ năng lượng giáp (quái tiêu hao giáp mượt hơn để cảm nhận tụt giáp rõ)
            float heSoGiap = loaiDoiTuong == LoaiDoiTuong.QuaiVat
                ? Mathf.Max(1f, heSoTieuHaoGiap)
                : Mathf.Max(0f, heSoTieuHaoGiap);
            float luongGiapTieuHao = satThuongGoc * heSoGiap;
            giapHienTai = Mathf.Max(0f, giapHienTai - luongGiapTieuHao);
            onGiapThayDoi?.Invoke(giapHienTai, giapToiDa);
        }
        else
        {
            // Hết giáp năng lượng: trừ máu bình thường (có áp dụng giáp phòng thủ cơ bản)
            satThuongVaoMau = Mathf.Max(1f, satThuongGoc - giapPhongThu);
        }

        mauHienTai = Mathf.Clamp(mauHienTai - satThuongVaoMau, 0f, mauToiDa);

        // Báo cho AI biết vừa bị tấn công để truy đuổi ngay lập tức
        if (loaiDoiTuong == LoaiDoiTuong.QuaiVat)
        {
            if (meleeAI != null) meleeAI.NhanSatThuong(satThuongVaoMau);
            if (rangedAI != null) rangedAI.NhanSatThuong(satThuongVaoMau);
        }

        CapNhatUI();
        onTrungDon?.Invoke();
        onMauThayDoi?.Invoke(mauHienTai, mauToiDa);

        if (mauHienTai <= 0f)
        {
            XuLyKhiChet();
        }
        else if (daChanBangGiap && amThanhGiapChan != null)
        {
            PhatAmThanh(amThanhGiapChan);
        }
        else
        {
            PhatAmThanh(amThanhTrungDon);
        }
    }

    /// <summary>
    /// Hồi phục năng lượng giáp khi nhặt pin hoặc nhận buff giáp.
    /// </summary>
    public void HoiGiap(float luongGiapHoi)
    {
        if (daChet || luongGiapHoi <= 0f || giapToiDa <= 0f) return;

        dungGiapNangLuong = true;
        giapHienTai = Mathf.Clamp(giapHienTai + luongGiapHoi, 0f, giapToiDa);

        CapNhatUI();
        onGiapThayDoi?.Invoke(giapHienTai, giapToiDa);
        PhatAmThanh(amThanhHoiGiap);
    }

    /// <summary>
    /// Hồi phục một lượng máu nhất định (dùng cho bình máu, kỹ năng hoặc tự hồi).
    /// </summary>
    public void HoiMau(float luongMauHoi)
    {
        if (daChet || luongMauHoi <= 0f) return;

        mauHienTai = Mathf.Clamp(mauHienTai + luongMauHoi, 0f, mauToiDa);
        CapNhatUI();
        onMauThayDoi?.Invoke(mauHienTai, mauToiDa);
    }

    /// <summary>
    /// Hồi sinh lại đối tượng với đầy máu và giáp.
    /// </summary>
    public void HoiSinh()
    {
        daChet = false;
        mauHienTai = mauToiDa;
        giapHienTai = giapToiDa;
        CapNhatUI();
        onMauThayDoi?.Invoke(mauHienTai, mauToiDa);
        onGiapThayDoi?.Invoke(giapHienTai, giapToiDa);
    }

    private void XuLyKhiChet()
    {
        if (daChet) return;
        daChet = true;

        PhatAmThanh(amThanhChet);

        if (hieuUngKhiChet != null)
        {
            Instantiate(hieuUngKhiChet, transform.position + Vector3.up * 0.8f, Quaternion.identity);
        }

        if (thanhMauSlider != null && loaiDoiTuong == LoaiDoiTuong.QuaiVat)
        {
            thanhMauSlider.gameObject.SetActive(false);
        }

        onChet?.Invoke();

        if (loaiDoiTuong == LoaiDoiTuong.QuaiVat)
        {
            if (meleeAI != null) meleeAI.enabled = false;
            if (rangedAI != null) rangedAI.enabled = false;

            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Destroy(gameObject, Mathf.Max(0f, thoiGianHuyKhiChet));
        }
    }

    private void KhoiTaoUI()
    {
        if (thanhMauSlider != null)
        {
            thanhMauSlider.minValue = 0f;
            thanhMauSlider.maxValue = mauToiDa;
            thanhMauSlider.value = mauHienTai;

            if (loaiDoiTuong == LoaiDoiTuong.QuaiVat && anThanhMauKhiDay)
            {
                thanhMauSlider.gameObject.SetActive(false);
            }
        }

        if (thanhGiapSlider != null)
        {
            thanhGiapSlider.minValue = 0f;
            thanhGiapSlider.maxValue = giapToiDa;
            thanhGiapSlider.value = giapHienTai;
        }
    }

    private void CapNhatUI()
    {
        if (thanhMauSlider != null)
        {
            if (loaiDoiTuong == LoaiDoiTuong.QuaiVat && anThanhMauKhiDay && !daChet)
            {
                bool hienThi = mauHienTai < mauToiDa;
                if (thanhMauSlider.gameObject.activeSelf != hienThi)
                {
                    thanhMauSlider.gameObject.SetActive(hienThi);
                }
            }

            thanhMauSlider.maxValue = mauToiDa;
            thanhMauSlider.value = mauHienTai;
        }

        if (thanhGiapSlider != null)
        {
            thanhGiapSlider.maxValue = giapToiDa;
            thanhGiapSlider.value = giapHienTai;
        }
    }

    private void PhatAmThanh(AudioClip clip)
    {
        if (clip != null && nguonAmThanh != null)
        {
            nguonAmThanh.PlayOneShot(clip, amLuong);
        }
    }
}
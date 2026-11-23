using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class CuongNoSystem : MonoBehaviour
{
    [Header("1. Thanh UI & Điểm Cuồng Nộ")]
    [Tooltip("Thanh Slider hiển thị mức cuồng nộ (0 -> maxCuongNo).")]
    [SerializeField] private Slider thanhCuongNoSlider;

    [Tooltip("Mức cuồng nộ tối đa để kích hoạt trạng thái Cuồng Nộ.")]
    [SerializeField] private float maxCuongNo = 100f;

    [Tooltip("Điểm cuồng nộ cộng thêm mỗi khi hạ gục 1 con quái.")]
    [SerializeField] private float diemMoiLanGietQuai = 25f;

    [Tooltip("Hệ số quy đổi từ sát thương nhận vào sang điểm cuồng nộ (ví dụ 20 dmg * 0.8 = +16 điểm).")]
    [SerializeField] private float heSoNhanSatThuong = 0.8f;

    [Tooltip("Điểm cuồng nộ tối thiểu cộng mỗi lần bị trúng đòn.")]
    [SerializeField] private float diemToiThieuMoiLanBiDanh = 5f;

    [Header("2. Trạng Thái Khi Đầy Cuồng Nộ")]
    [Tooltip("Thời gian duy trì trạng thái Cuồng Nộ khi thanh đầy (giây thực tế).")]
    [SerializeField] private float thoiGianCuongNo = 8f;

    [Tooltip("Hệ số nhân tốc độ di chuyển của người chơi khi Cuồng Nộ (ví dụ 1.35 = tăng 35%).")]
    [SerializeField] private float heSoTangTocDo = 1.35f;

    [Tooltip("Mức Time.timeScale khi kích hoạt Cuồng Nộ (mặc định 0.75f).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float timeScaleCuongNo = 0.75f;

    [Tooltip("Hệ số nhân sát thương vũ khí khi Cuồng Nộ (ví dụ 1.5 = tăng 50% sát thương).")]
    [SerializeField] private float heSoBoostSatThuong = 1.5f;

    [Tooltip("Lượng sát thương cộng thêm cố định cho vũ khí khi Cuồng Nộ.")]
    [SerializeField] private float satThuongCongThem = 10f;

    [Header("3. Tham Chiếu Người Chơi & Hiệu Ứng")]
    [Tooltip("Script di chuyển của người chơi (tự tìm nếu để trống).")]
    [SerializeField] private IronStormMovement scriptDiChuyen;

    [Tooltip("Script Jump Pack của người chơi (tự tìm nếu để trống).")]
    [SerializeField] private SpaceMarineJumpPack scriptJumpPack;

    [Tooltip("HealthSystem của người chơi để tự cộng điểm khi nhận sát thương (tự tìm nếu để trống).")]
    [SerializeField] private HealthSystem mauNguoiChoi;

    [Tooltip("GameObject hiệu ứng VFX/Aura bật lên khi đang Cuồng Nộ (tùy chọn).")]
    [SerializeField] private GameObject hieuUngCuongNo;

    [SerializeField] private AudioSource nguonAmThanh;
    [SerializeField] private AudioClip amThanhKichHoatCuongNo;
    [Range(0f, 1f)]
    [SerializeField] private float amLuong = 0.9f;

    private float cuongNoHienTai = 0f;
    private bool dangCuongNo = false;
    private float thoiGianConLai = 0f;
    private int mauTruocDo = -1;

    // Lưu giá trị gốc để trả về chuẩn xác khi hết Cuồng Nộ
    private float normalSpeedGoc;
    private float runSpeedGoc;
    private float airMoveSpeedGoc;
    private float boostMoveSpeedGoc;
    private bool daLuuTocDoGoc = false;
    private float timeScaleTruocDo = 1f;

    public bool DangCuongNo => dangCuongNo;
    public float CuongNoHienTai => cuongNoHienTai;
    public float MaxCuongNo => maxCuongNo;

    private void Awake()
    {
        maxCuongNo = Mathf.Max(1f, maxCuongNo);

        if (scriptDiChuyen == null)
        {
            scriptDiChuyen = GetComponentInParent<IronStormMovement>();
            if (scriptDiChuyen == null) scriptDiChuyen = FindFirstObjectByType<IronStormMovement>();
        }

        if (scriptJumpPack == null)
        {
            scriptJumpPack = GetComponentInParent<SpaceMarineJumpPack>();
            if (scriptJumpPack == null) scriptJumpPack = FindFirstObjectByType<SpaceMarineJumpPack>();
        }

        if (mauNguoiChoi == null)
        {
            mauNguoiChoi = GetComponentInParent<HealthSystem>();
            if (mauNguoiChoi == null && scriptDiChuyen != null)
            {
                mauNguoiChoi = scriptDiChuyen.GetComponent<HealthSystem>();
            }
        }

        if (nguonAmThanh == null)
        {
            nguonAmThanh = GetComponent<AudioSource>();
            if (nguonAmThanh == null)
            {
                nguonAmThanh = gameObject.AddComponent<AudioSource>();
                nguonAmThanh.playOnAwake = false;
            }
        }

        LuuGiaTriGoc();
        DongBoMauBanDau();

        if (thanhCuongNoSlider != null)
        {
            thanhCuongNoSlider.minValue = 0f;
            thanhCuongNoSlider.maxValue = maxCuongNo;
            thanhCuongNoSlider.value = cuongNoHienTai;
        }

        if (hieuUngCuongNo != null)
        {
            hieuUngCuongNo.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (dangCuongNo)
        {
            KetThucCuongNo();
        }
    }

    private void Update()
    {
        TheoDoiSatThuongTuHealthSystem();

        if (dangCuongNo)
        {
            // Dùng unscaledDeltaTime để thời gian cuồng nộ đếm chuẩn ngay cả khi Time.timeScale = 0.75f
            thoiGianConLai -= Time.unscaledDeltaTime;
            cuongNoHienTai = Mathf.Clamp(
                (thoiGianConLai / Mathf.Max(0.01f, thoiGianCuongNo)) * maxCuongNo,
                0f,
                maxCuongNo
            );

            // Duy trì timeScale ở mức 0.75f trong suốt thời gian Cuồng Nộ
            if (!Mathf.Approximately(Time.timeScale, timeScaleCuongNo))
            {
                Time.timeScale = timeScaleCuongNo;
            }

            if (thoiGianConLai <= 0f)
            {
                KetThucCuongNo();
            }
        }

        CapNhatSlider();
    }

    /// <summary>
    /// Gọi hàm này mỗi khi người chơi hạ gục 1 con quái.
    /// </summary>
    public void CongDiemKhiGietQuai(float diemTuyChinh = -1f)
    {
        if (dangCuongNo) return;
        float cong = diemTuyChinh >= 0f ? diemTuyChinh : diemMoiLanGietQuai;
        ThemCuongNo(cong);
    }

    /// <summary>
    /// Gọi hàm này khi người chơi nhận sát thương.
    /// </summary>
    public void CongDiemKhiNhanSatThuong(float satThuongNhanVao)
    {
        if (dangCuongNo || satThuongNhanVao <= 0f) return;
        float cong = Mathf.Max(diemToiThieuMoiLanBiDanh, satThuongNhanVao * heSoNhanSatThuong);
        ThemCuongNo(cong);
    }

    /// <summary>
    /// Cộng trực tiếp điểm cuồng nộ và tự kích hoạt khi chạm ngưỡng tối đa.
    /// </summary>
    public void ThemCuongNo(float soDiem)
    {
        if (soDiem <= 0f || dangCuongNo) return;

        cuongNoHienTai = Mathf.Clamp(cuongNoHienTai + soDiem, 0f, maxCuongNo);
        CapNhatSlider();

        if (cuongNoHienTai >= maxCuongNo)
        {
            KichHoatCuongNo();
        }
    }

    /// <summary>
    /// Tính sát thương vũ khí sau khi áp dụng boost Cuồng Nộ.
    /// </summary>
    public float ApDungBoostSatThuong(float satThuongGoc)
    {
        if (!dangCuongNo) return satThuongGoc;
        return (satThuongGoc * Mathf.Max(1f, heSoBoostSatThuong)) + Mathf.Max(0f, satThuongCongThem);
    }

    private void KichHoatCuongNo()
    {
        dangCuongNo = true;
        cuongNoHienTai = maxCuongNo;
        thoiGianConLai = Mathf.Max(0.1f, thoiGianCuongNo);
        timeScaleTruocDo = Time.timeScale > 0.05f ? Time.timeScale : 1f;

        LuuGiaTriGoc();
        ApDungTocDoCuongNo(heSoTangTocDo);
        Time.timeScale = timeScaleCuongNo;

        if (hieuUngCuongNo != null)
        {
            hieuUngCuongNo.SetActive(true);
        }

        if (nguonAmThanh != null && amThanhKichHoatCuongNo != null)
        {
            nguonAmThanh.PlayOneShot(amThanhKichHoatCuongNo, amLuong);
        }

        CapNhatSlider();
    }

    private void KetThucCuongNo()
    {
        dangCuongNo = false;
        thoiGianConLai = 0f;
        cuongNoHienTai = 0f;

        ApDungTocDoCuongNo(1f);

        float scaleTraVe = timeScaleTruocDo > 0.05f ? timeScaleTruocDo : 1f;
        if (Mathf.Approximately(scaleTraVe, timeScaleCuongNo))
        {
            scaleTraVe = 1f;
        }
        Time.timeScale = scaleTraVe;

        if (hieuUngCuongNo != null)
        {
            hieuUngCuongNo.SetActive(false);
        }

        CapNhatSlider();
    }

    private void CapNhatSlider()
    {
        if (thanhCuongNoSlider == null) return;
        thanhCuongNoSlider.maxValue = maxCuongNo;
        thanhCuongNoSlider.value = cuongNoHienTai;
    }

    private void DongBoMauBanDau()
    {
        mauTruocDo = LayMauHienTai(mauNguoiChoi);
    }

    private void TheoDoiSatThuongTuHealthSystem()
    {
        if (mauNguoiChoi == null) return;

        int mauHienTai = LayMauHienTai(mauNguoiChoi);
        if (mauTruocDo >= 0 && mauHienTai < mauTruocDo)
        {
            CongDiemKhiNhanSatThuong(mauTruocDo - mauHienTai);
        }
        mauTruocDo = mauHienTai;
    }

    private int LayMauHienTai(HealthSystem hs)
    {
        if (hs == null) return -1;
        FieldInfo field = typeof(HealthSystem).GetField(
            "mauHienTai",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        );
        if (field != null && field.FieldType == typeof(int))
        {
            return (int)field.GetValue(hs);
        }
        return -1;
    }

    private void LuuGiaTriGoc()
    {
        if (daLuuTocDoGoc) return;

        if (scriptDiChuyen != null)
        {
            FieldInfo fNormal = typeof(IronStormMovement).GetField("normalSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo fRun = typeof(IronStormMovement).GetField("runSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fNormal != null) normalSpeedGoc = (float)fNormal.GetValue(scriptDiChuyen);
            if (fRun != null) runSpeedGoc = (float)fRun.GetValue(scriptDiChuyen);
        }

        if (scriptJumpPack != null)
        {
            FieldInfo fAir = typeof(SpaceMarineJumpPack).GetField("airMoveSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo fBoost = typeof(SpaceMarineJumpPack).GetField("boostMoveSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fAir != null) airMoveSpeedGoc = (float)fAir.GetValue(scriptJumpPack);
            if (fBoost != null) boostMoveSpeedGoc = (float)fBoost.GetValue(scriptJumpPack);
        }

        daLuuTocDoGoc = true;
    }

    private void ApDungTocDoCuongNo(float heSo)
    {
        if (!daLuuTocDoGoc) return;

        if (scriptDiChuyen != null)
        {
            FieldInfo fNormal = typeof(IronStormMovement).GetField("normalSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo fRun = typeof(IronStormMovement).GetField("runSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fNormal != null) fNormal.SetValue(scriptDiChuyen, normalSpeedGoc * heSo);
            if (fRun != null) fRun.SetValue(scriptDiChuyen, runSpeedGoc * heSo);
        }

        if (scriptJumpPack != null)
        {
            FieldInfo fAir = typeof(SpaceMarineJumpPack).GetField("airMoveSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo fBoost = typeof(SpaceMarineJumpPack).GetField("boostMoveSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fAir != null) fAir.SetValue(scriptJumpPack, airMoveSpeedGoc * heSo);
            if (fBoost != null) fBoost.SetValue(scriptJumpPack, boostMoveSpeedGoc * heSo);
        }
    }
}
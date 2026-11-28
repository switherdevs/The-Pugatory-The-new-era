using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MenuCheatVoHanDan : MonoBehaviour
{
    private struct DuLieuSatThuongGoc
    {
        public float satThuongGoc;
    }

    private struct DuLieuTocDoGoc
    {
        public float tocDoBinhThuongGoc;
        public float tocDoChayNhanhGoc;
        public float tocDoLuotDashGoc;
    }

    [Header("1. Giao Dien UI Cheat & Nut Vo Han Dan (TextMeshPro)")]
    [Tooltip("Kéo Panel hoặc object chứa giao diện cheat vào đây (nên đặt script ở object khác luôn bật, không gắn lên chính object bị tắt).")]
    [SerializeField] private GameObject panelUICheat;

    [Tooltip("Button bật/tắt chế độ vô hạn đạn (bao gồm cả súng và grenade). Bấm để giữ bật, bấm lần nữa để tắt.")]
    [SerializeField] private Button nutVoHanDan;

    [Tooltip("TextMeshPro hiển thị trạng thái trên button vô hạn đạn (tùy chọn).")]
    [SerializeField] private TMP_Text textTrangThaiNut;

    [Header("2. Nut Cheat: Player Bat Tu / Damge Chet Ngay / Toc Do Nhanh")]
    [Tooltip("Button bật/tắt chế độ Player bất tử (bấm để giữ bật, bấm lần nữa để tắt).")]
    [SerializeField] private Button nutPlayerBatTu;

    [Tooltip("TextMeshPro hiển thị trạng thái trên button Player bất tử (tùy chọn).")]
    [SerializeField] private TMP_Text textTrangThaiPlayerBatTu;

    [Tooltip("Button bật/tắt chế độ bắn trúng quái là chết ngay (bấm để giữ bật, bấm lần nữa để tắt).")]
    [SerializeField] private Button nutDamgeChetNgay;

    [Tooltip("TextMeshPro hiển thị trạng thái trên button Damage chết ngay (tùy chọn).")]
    [SerializeField] private TMP_Text textTrangThaiDamgeChetNgay;

    [Tooltip("Button bật/tắt chế độ di chuyển nhanh cho Player (bấm để giữ bật, bấm lần nữa để tắt).")]
    [SerializeField] private Button nutTocDoNhanh;

    [Tooltip("TextMeshPro hiển thị trạng thái trên button Tốc độ nhanh (tùy chọn).")]
    [SerializeField] private TMP_Text textTrangThaiTocDoNhanh;

    [Header("3. Nut Cheat: Vo Han Nang Luong JumpPack & Tang Hinh")]
    [Tooltip("Button bật/tắt vô hạn năng lượng JumpPack (bấm để giữ bật, bấm lần nữa để tắt).")]
    [SerializeField] private Button nutVoHanJumpPack;

    [Tooltip("TextMeshPro hiển thị trạng thái trên button Vô hạn JumpPack (tùy chọn).")]
    [SerializeField] private TMP_Text textTrangThaiJumpPack;

    [Tooltip("Button bật/tắt tàng hình: đổi layer Player thành Default (0), bấm lần nữa trả lại layer gốc.")]
    [SerializeField] private Button nutTangHinh;

    [Tooltip("TextMeshPro hiển thị trạng thái trên button Tàng hình (tùy chọn).")]
    [SerializeField] private TMP_Text textTrangThaiTangHinh;

    [Header("4. Thong So Cheat & Mau Sac Button")]
    [Tooltip("Mức sát thương đặt cho đạn/đòn đánh của người chơi khi bật Damage bắn cái chết luôn.")]
    [SerializeField] private float satThuongChetNgay = 999999f;

    [Tooltip("Hệ số nhân tốc độ di chuyển (đi bộ, chạy nhanh, lướt) khi bật Tốc độ di chuyển nhanh.")]
    [SerializeField] private float heSoTocDoNhanh = 2.5f;

    [Tooltip("Màu button khi chế độ đang BẬT (đang giữ).")]
    [SerializeField] private Color mauKhiDangBat = new Color(0.16f, 0.72f, 0.32f, 1f);

    [Tooltip("Màu button khi chế độ đang TẮT (đã thả).")]
    [SerializeField] private Color mauKhiDangTat = new Color(0.24f, 0.26f, 0.30f, 1f);

    [SerializeField] private string chuVoHanDanBat = "VO HAN DAN: BAT";
    [SerializeField] private string chuVoHanDanTat = "VO HAN DAN: TAT";

    [SerializeField] private string chuPlayerBatTuBat = "BAT TU: BAT";
    [SerializeField] private string chuPlayerBatTuTat = "BAT TU: TAT";

    [SerializeField] private string chuDamgeChetNgayBat = "1 HIT KILL: BAT";
    [SerializeField] private string chuDamgeChetNgayTat = "1 HIT KILL: TAT";

    [SerializeField] private string chuTocDoNhanhBat = "TOC DO NHANH: BAT";
    [SerializeField] private string chuTocDoNhanhTat = "TOC DO NHANH: TAT";

    [SerializeField] private string chuJumpPackBat = "VO HAN JUMPPACK: BAT";
    [SerializeField] private string chuJumpPackTat = "VO HAN JUMPPACK: TAT";

    [SerializeField] private string chuTangHinhBat = "TANG HINH: BAT";
    [SerializeField] private string chuTangHinhTat = "TANG HINH: TAT";

    [Header("5. Phim Mo / Dong UI Cheat & Dieu Khien Chuot")]
    [Tooltip("Bật nếu muốn dùng phím F1 để mở/đóng nhanh UI Cheat.")]
    [SerializeField] private bool dungPhimF1 = true;

    [Tooltip("Bật nếu muốn dùng phím Insert để mở/đóng nhanh UI Cheat.")]
    [SerializeField] private bool dungPhimInsert = true;

    [Tooltip("Tự động mở khóa chuột khi hiện UI Cheat và khóa lại khi ẩn UI Cheat.")]
    [SerializeField] private bool tuDongDieuKhienChuot = true;

    // Cờ trạng thái hiển thị bảng UI Cheat và các cờ giữ trạng thái từng cheat
    private bool uiCheatDangMo = false;
    private bool dangBatVoHanDan = false;
    private bool dangBatPlayerBatTu = false;
    private bool dangBatDamgeChetNgay = false;
    private bool dangBatTocDoNhanh = false;
    private bool dangBatVoHanJumpPack = false;
    private bool dangBatTangHinh = false;

    // Lưu trạng thái gốc trước khi bật cheat để khôi phục chính xác khi tắt
    private readonly Dictionary<WeaponSystem, bool> trangThaiGocWeaponSystem = new Dictionary<WeaponSystem, bool>();
    private readonly Dictionary<SniperWeapon, bool> trangThaiGocSniperWeapon = new Dictionary<SniperWeapon, bool>();
    private readonly Dictionary<DamageDealer, DuLieuSatThuongGoc> duLieuSatThuongGoc = new Dictionary<DamageDealer, DuLieuSatThuongGoc>();
    private readonly Dictionary<IronStormMovement, DuLieuTocDoGoc> duLieuTocDoGoc = new Dictionary<IronStormMovement, DuLieuTocDoGoc>();
    private readonly Dictionary<Transform, int> layerGocPlayer = new Dictionary<Transform, int>();

    // Cache danh sách đối tượng trong scene để quét định kỳ nhẹ nhàng, chạy ổn định mọi FPS
    private readonly List<WeaponSystem> danhSachSungThuong = new List<WeaponSystem>();
    private readonly List<SniperWeapon> danhSachSungTia = new List<SniperWeapon>();
    private readonly List<GrenadeWeapon> danhSachLuuDan = new List<GrenadeWeapon>();
    private readonly List<HealthSystem> danhSachMauNguoiChoi = new List<HealthSystem>();
    private readonly List<DamageDealer> danhSachGaySatThuong = new List<DamageDealer>();
    private readonly List<IronStormMovement> danhSachDiChuyen = new List<IronStormMovement>();
    private readonly List<SpaceMarineJumpPack> danhSachJumpPack = new List<SpaceMarineJumpPack>();
    private readonly List<Transform> danhSachRootPlayer = new List<Transform>();

    private float thoiGianQuetLaiDoiTuong = 0f;
    private const float CHU_KY_QUET_DOI_TUONG = 0.5f;

    // Truy cập các trường private qua Reflection để tránh sửa nhiều script lẻ
    private static readonly FieldInfo TruongInfiniteAmmoSungThuong = typeof(WeaponSystem).GetField(
        "infiniteAmmo",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static readonly FieldInfo TruongCurrentAmmoSungThuong = typeof(WeaponSystem).GetField(
        "currentAmmo",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static readonly FieldInfo TruongInfiniteAmmoSungTia = typeof(SniperWeapon).GetField(
        "infiniteAmmo",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static readonly FieldInfo TruongCurrentAmmoSungTia = typeof(SniperWeapon).GetField(
        "currentAmmo",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static readonly FieldInfo TruongSatThuongDamageDealer = typeof(DamageDealer).GetField(
        "satThuong",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static readonly FieldInfo TruongPheDamageDealer = typeof(DamageDealer).GetField(
        "pheGaySatThuong",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static readonly FieldInfo TruongNormalSpeed = typeof(IronStormMovement).GetField(
        "normalSpeed",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static readonly FieldInfo TruongRunSpeed = typeof(IronStormMovement).GetField(
        "runSpeed",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static readonly FieldInfo TruongDashSpeed = typeof(IronStormMovement).GetField(
        "dashSpeed",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static readonly FieldInfo TruongMaxEnergyJumpPack = typeof(SpaceMarineJumpPack).GetField(
        "maxEnergy",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static readonly FieldInfo TruongCurrentEnergyJumpPack = typeof(SpaceMarineJumpPack).GetField(
        "currentEnergy",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private static readonly FieldInfo TruongEnergySliderJumpPack = typeof(SpaceMarineJumpPack).GetField(
        "energySlider",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    public bool DangMoUICheat => uiCheatDangMo;
    public bool TrangThaiVoHanDan => dangBatVoHanDan;
    public bool TrangThaiPlayerBatTu => dangBatPlayerBatTu;
    public bool TrangThaiDamgeChetNgay => dangBatDamgeChetNgay;
    public bool TrangThaiTocDoNhanh => dangBatTocDoNhanh;
    public bool TrangThaiVoHanJumpPack => dangBatVoHanJumpPack;
    public bool TrangThaiTangHinh => dangBatTangHinh;

    private void Awake()
    {
        // Gắn sự kiện click cho tất cả button cheat (bấm 1 lần giữ bật, bấm lần nữa tắt)
        GanSuKienNut(nutVoHanDan, ref textTrangThaiNut, ChuyenDoiVoHanDan);
        GanSuKienNut(nutPlayerBatTu, ref textTrangThaiPlayerBatTu, ChuyenDoiPlayerBatTu);
        GanSuKienNut(nutDamgeChetNgay, ref textTrangThaiDamgeChetNgay, ChuyenDoiDamgeChetNgay);
        GanSuKienNut(nutTocDoNhanh, ref textTrangThaiTocDoNhanh, ChuyenDoiTocDoNhanh);
        GanSuKienNut(nutVoHanJumpPack, ref textTrangThaiJumpPack, ChuyenDoiVoHanJumpPack);
        GanSuKienNut(nutTangHinh, ref textTrangThaiTangHinh, ChuyenDoiTangHinh);

        // Khởi tạo mặc định khi vào game: ẩn bảng cheat và tắt tất cả cheat
        uiCheatDangMo = false;
        dangBatVoHanDan = false;
        dangBatPlayerBatTu = false;
        dangBatDamgeChetNgay = false;
        dangBatTocDoNhanh = false;
        dangBatVoHanJumpPack = false;
        dangBatTangHinh = false;

        if (panelUICheat != null)
        {
            panelUICheat.SetActive(false);
        }

        QuetTatCaDoiTuongTrongScene();
        CapNhatTatCaGiaoDienNut();
    }

    private void OnDestroy()
    {
        HuySuKienNut(nutVoHanDan, ChuyenDoiVoHanDan);
        HuySuKienNut(nutPlayerBatTu, ChuyenDoiPlayerBatTu);
        HuySuKienNut(nutDamgeChetNgay, ChuyenDoiDamgeChetNgay);
        HuySuKienNut(nutTocDoNhanh, ChuyenDoiTocDoNhanh);
        HuySuKienNut(nutVoHanJumpPack, ChuyenDoiVoHanJumpPack);
        HuySuKienNut(nutTangHinh, ChuyenDoiTangHinh);

        // Khôi phục thông số gốc nếu script bị hủy khi đang bật cheat
        if (dangBatDamgeChetNgay)
        {
            KhoiPhucSatThuongGoc();
        }
        if (dangBatTocDoNhanh)
        {
            KhoiPhucTocDoGoc();
        }
        if (dangBatTangHinh)
        {
            KhoiPhucLayerGocPlayer();
        }
        if (tuDongDieuKhienChuot && uiCheatDangMo)
        {
            DatTrangThaiChuot(dangMoMenu: false);
        }
    }

    private void GanSuKienNut(Button nut, ref TMP_Text textTmp, UnityEngine.Events.UnityAction hanhDong)
    {
        if (nut == null) return;

        nut.onClick.RemoveListener(hanhDong);
        nut.onClick.AddListener(hanhDong);

        if (textTmp == null)
        {
            textTmp = nut.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void HuySuKienNut(Button nut, UnityEngine.Events.UnityAction hanhDong)
    {
        if (nut == null) return;
        nut.onClick.RemoveListener(hanhDong);
    }

    private void Update()
    {
        // Luôn kiểm tra phím mở/đóng UI Cheat đầu mỗi frame để không bị miss phím
        KiemTraPhimMoDongUICheat();

        // Giới hạn deltaTime tối đa 0.05s để bộ đếm thời gian chạy đồng nhất trên mọi FPS / máy
        float deltaThoiGian = Mathf.Min(Time.deltaTime, 0.05f);
        if (deltaThoiGian <= 0f) return;

        thoiGianQuetLaiDoiTuong -= deltaThoiGian;
        if (thoiGianQuetLaiDoiTuong <= 0f)
        {
            thoiGianQuetLaiDoiTuong = CHU_KY_QUET_DOI_TUONG;
            QuetTatCaDoiTuongTrongScene();
        }

        // Duy trì các chế độ cheat đang được giữ bật
        if (dangBatVoHanDan)
        {
            ApDungVoHanDanChoTatCa();
        }

        if (dangBatPlayerBatTu)
        {
            ApDungBatTuChoPlayer();
        }

        if (dangBatDamgeChetNgay)
        {
            ApDungDamgeChetNgay();
        }

        if (dangBatTocDoNhanh)
        {
            ApDungTocDoNhanh();
        }

        if (dangBatVoHanJumpPack)
        {
            ApDungVoHanJumpPack();
        }

        if (dangBatTangHinh)
        {
            ApDungLayerDefaultChoPlayer();
        }
    }

    // Kiểm tra phím F1 hoặc Insert (hỗ trợ cả Legacy Input và New Input System khi bật Both)
    private void KiemTraPhimMoDongUICheat()
    {
        bool nhanPhimToggle = false;

        if (dungPhimF1)
        {
            if (Input.GetKeyDown(KeyCode.F1) || (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame))
            {
                nhanPhimToggle = true;
            }
        }

        if (dungPhimInsert)
        {
            if (Input.GetKeyDown(KeyCode.Insert) || (Keyboard.current != null && Keyboard.current.insertKey.wasPressedThisFrame))
            {
                nhanPhimToggle = true;
            }
        }

        if (nhanPhimToggle)
        {
            ChuyenDoiHienThiUICheat();
        }
    }

    public void ChuyenDoiHienThiUICheat()
    {
        DatHienThiUICheat(!uiCheatDangMo);
    }

    public void DatHienThiUICheat(bool hienThi)
    {
        uiCheatDangMo = hienThi;

        if (panelUICheat != null)
        {
            if (uiCheatDangMo)
            {
                Canvas canvasCha = panelUICheat.GetComponentInParent<Canvas>(true);
                if (canvasCha != null && !canvasCha.gameObject.activeSelf)
                {
                    canvasCha.gameObject.SetActive(true);
                }
            }

            panelUICheat.SetActive(uiCheatDangMo);
        }

        if (tuDongDieuKhienChuot)
        {
            DatTrangThaiChuot(uiCheatDangMo);
        }

        if (uiCheatDangMo)
        {
            QuetTatCaDoiTuongTrongScene();
            CapNhatTatCaGiaoDienNut();
        }
    }

    private void DatTrangThaiChuot(bool dangMoMenu)
    {
        Cursor.lockState = dangMoMenu ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = dangMoMenu;
    }

    // 1. Toggle Vô Hạn Đạn (súng thường, súng tỉa, grenade)
    public void ChuyenDoiVoHanDan()
    {
        dangBatVoHanDan = !dangBatVoHanDan;
        QuetTatCaDoiTuongTrongScene();

        if (dangBatVoHanDan)
        {
            LuuTrangThaiDanGoc();
            ApDungVoHanDanChoTatCa();
        }
        else
        {
            KhoiPhucTrangThaiDanGoc();
        }

        CapNhatMotNut(nutVoHanDan, textTrangThaiNut, dangBatVoHanDan, chuVoHanDanBat, chuVoHanDanTat);
    }

    // 2. Toggle Player Bất Tử
    public void ChuyenDoiPlayerBatTu()
    {
        dangBatPlayerBatTu = !dangBatPlayerBatTu;
        QuetTatCaDoiTuongTrongScene();

        if (dangBatPlayerBatTu)
        {
            ApDungBatTuChoPlayer();
        }

        CapNhatMotNut(nutPlayerBatTu, textTrangThaiPlayerBatTu, dangBatPlayerBatTu, chuPlayerBatTuBat, chuPlayerBatTuTat);
    }

    // 3. Toggle Damage Bắn Cái Chết Luôn
    public void ChuyenDoiDamgeChetNgay()
    {
        dangBatDamgeChetNgay = !dangBatDamgeChetNgay;
        QuetTatCaDoiTuongTrongScene();

        if (dangBatDamgeChetNgay)
        {
            LuuSatThuongGoc();
            ApDungDamgeChetNgay();
        }
        else
        {
            KhoiPhucSatThuongGoc();
        }

        CapNhatMotNut(nutDamgeChetNgay, textTrangThaiDamgeChetNgay, dangBatDamgeChetNgay, chuDamgeChetNgayBat, chuDamgeChetNgayTat);
    }

    // 4. Toggle Tốc Độ Di Chuyển Nhanh
    public void ChuyenDoiTocDoNhanh()
    {
        dangBatTocDoNhanh = !dangBatTocDoNhanh;
        QuetTatCaDoiTuongTrongScene();

        if (dangBatTocDoNhanh)
        {
            LuuTocDoGoc();
            ApDungTocDoNhanh();
        }
        else
        {
            KhoiPhucTocDoGoc();
        }

        CapNhatMotNut(nutTocDoNhanh, textTrangThaiTocDoNhanh, dangBatTocDoNhanh, chuTocDoNhanhBat, chuTocDoNhanhTat);
    }

    // 5. Toggle Vô Hạn Năng Lượng JumpPack
    public void ChuyenDoiVoHanJumpPack()
    {
        dangBatVoHanJumpPack = !dangBatVoHanJumpPack;
        QuetTatCaDoiTuongTrongScene();

        if (dangBatVoHanJumpPack)
        {
            ApDungVoHanJumpPack();
        }

        CapNhatMotNut(nutVoHanJumpPack, textTrangThaiJumpPack, dangBatVoHanJumpPack, chuJumpPackBat, chuJumpPackTat);
    }

    // 6. Toggle Tàng Hình (đổi layer Player thành Default = 0, tắt thì trả lại layer gốc)
    public void ChuyenDoiTangHinh()
    {
        dangBatTangHinh = !dangBatTangHinh;
        QuetTatCaDoiTuongTrongScene();

        if (dangBatTangHinh)
        {
            LuuLayerGocPlayer();
            ApDungLayerDefaultChoPlayer();
        }
        else
        {
            KhoiPhucLayerGocPlayer();
        }

        CapNhatMotNut(nutTangHinh, textTrangThaiTangHinh, dangBatTangHinh, chuTangHinhBat, chuTangHinhTat);
    }

    private void CapNhatTatCaGiaoDienNut()
    {
        CapNhatMotNut(nutVoHanDan, textTrangThaiNut, dangBatVoHanDan, chuVoHanDanBat, chuVoHanDanTat);
        CapNhatMotNut(nutPlayerBatTu, textTrangThaiPlayerBatTu, dangBatPlayerBatTu, chuPlayerBatTuBat, chuPlayerBatTuTat);
        CapNhatMotNut(nutDamgeChetNgay, textTrangThaiDamgeChetNgay, dangBatDamgeChetNgay, chuDamgeChetNgayBat, chuDamgeChetNgayTat);
        CapNhatMotNut(nutTocDoNhanh, textTrangThaiTocDoNhanh, dangBatTocDoNhanh, chuTocDoNhanhBat, chuTocDoNhanhTat);
        CapNhatMotNut(nutVoHanJumpPack, textTrangThaiJumpPack, dangBatVoHanJumpPack, chuJumpPackBat, chuJumpPackTat);
        CapNhatMotNut(nutTangHinh, textTrangThaiTangHinh, dangBatTangHinh, chuTangHinhBat, chuTangHinhTat);
    }

    private void CapNhatMotNut(Button nut, TMP_Text textTmp, bool trangThaiBat, string chuBat, string chuTat)
    {
        if (nut != null)
        {
            Image anhNut = nut.GetComponent<Image>();
            if (anhNut != null)
            {
                anhNut.color = trangThaiBat ? mauKhiDangBat : mauKhiDangTat;
            }
        }

        if (textTmp == null && nut != null)
        {
            textTmp = nut.GetComponentInChildren<TMP_Text>(true);
        }

        if (textTmp != null)
        {
            textTmp.text = trangThaiBat ? chuBat : chuTat;
        }
    }

    private void ThemRootPlayerNeuChuaCo(Transform ungVien)
    {
        if (ungVien == null) return;
        Transform rootObj = ungVien.root != null ? ungVien.root : ungVien;
        if (!danhSachRootPlayer.Contains(rootObj))
        {
            danhSachRootPlayer.Add(rootObj);
        }
    }

    private void QuetTatCaDoiTuongTrongScene()
    {
        danhSachSungThuong.Clear();
        danhSachSungTia.Clear();
        danhSachLuuDan.Clear();
        danhSachMauNguoiChoi.Clear();
        danhSachGaySatThuong.Clear();
        danhSachDiChuyen.Clear();
        danhSachJumpPack.Clear();
        danhSachRootPlayer.Clear();

        WeaponSystem[] cacSungThuong = FindObjectsByType<WeaponSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cacSungThuong.Length; i++)
        {
            if (cacSungThuong[i] != null) danhSachSungThuong.Add(cacSungThuong[i]);
        }

        SniperWeapon[] cacSungTia = FindObjectsByType<SniperWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cacSungTia.Length; i++)
        {
            if (cacSungTia[i] != null) danhSachSungTia.Add(cacSungTia[i]);
        }

        GrenadeWeapon[] cacLuuDan = FindObjectsByType<GrenadeWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cacLuuDan.Length; i++)
        {
            if (cacLuuDan[i] != null) danhSachLuuDan.Add(cacLuuDan[i]);
        }

        HealthSystem[] cacHeThongMau = FindObjectsByType<HealthSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cacHeThongMau.Length; i++)
        {
            HealthSystem hp = cacHeThongMau[i];
            if (hp != null && hp.Phe == HealthSystem.LoaiDoiTuong.NguoiChoi)
            {
                danhSachMauNguoiChoi.Add(hp);
                ThemRootPlayerNeuChuaCo(hp.transform);
            }
        }

        DamageDealer[] cacGaySatThuong = FindObjectsByType<DamageDealer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cacGaySatThuong.Length; i++)
        {
            if (cacGaySatThuong[i] != null) danhSachGaySatThuong.Add(cacGaySatThuong[i]);
        }

        IronStormMovement[] cacDiChuyen = FindObjectsByType<IronStormMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cacDiChuyen.Length; i++)
        {
            if (cacDiChuyen[i] != null)
            {
                danhSachDiChuyen.Add(cacDiChuyen[i]);
                ThemRootPlayerNeuChuaCo(cacDiChuyen[i].transform);
            }
        }

        SpaceMarineJumpPack[] cacJumpPack = FindObjectsByType<SpaceMarineJumpPack>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cacJumpPack.Length; i++)
        {
            if (cacJumpPack[i] != null)
            {
                danhSachJumpPack.Add(cacJumpPack[i]);
                ThemRootPlayerNeuChuaCo(cacJumpPack[i].transform);
            }
        }
    }

    private void LuuTrangThaiDanGoc()
    {
        trangThaiGocWeaponSystem.Clear();
        for (int i = 0; i < danhSachSungThuong.Count; i++)
        {
            WeaponSystem sung = danhSachSungThuong[i];
            if (sung == null || TruongInfiniteAmmoSungThuong == null) continue;
            trangThaiGocWeaponSystem[sung] = (bool)TruongInfiniteAmmoSungThuong.GetValue(sung);
        }

        trangThaiGocSniperWeapon.Clear();
        for (int i = 0; i < danhSachSungTia.Count; i++)
        {
            SniperWeapon sungTia = danhSachSungTia[i];
            if (sungTia == null || TruongInfiniteAmmoSungTia == null) continue;
            trangThaiGocSniperWeapon[sungTia] = (bool)TruongInfiniteAmmoSungTia.GetValue(sungTia);
        }
    }

    private void ApDungVoHanDanChoTatCa()
    {
        for (int i = 0; i < danhSachSungThuong.Count; i++)
        {
            WeaponSystem sung = danhSachSungThuong[i];
            if (sung == null) continue;

            if (TruongInfiniteAmmoSungThuong != null)
            {
                TruongInfiniteAmmoSungThuong.SetValue(sung, true);
            }

            if (TruongCurrentAmmoSungThuong != null && sung.CurrentAmmo < sung.MaxAmmo)
            {
                TruongCurrentAmmoSungThuong.SetValue(sung, sung.MaxAmmo);
            }
        }

        for (int i = 0; i < danhSachSungTia.Count; i++)
        {
            SniperWeapon sungTia = danhSachSungTia[i];
            if (sungTia == null) continue;

            if (TruongInfiniteAmmoSungTia != null)
            {
                TruongInfiniteAmmoSungTia.SetValue(sungTia, true);
            }

            if (TruongCurrentAmmoSungTia != null && sungTia.CurrentAmmo < sungTia.MaxAmmo)
            {
                TruongCurrentAmmoSungTia.SetValue(sungTia, sungTia.MaxAmmo);
            }
        }

        for (int i = 0; i < danhSachLuuDan.Count; i++)
        {
            GrenadeWeapon luuDan = danhSachLuuDan[i];
            if (luuDan == null) continue;

            int soLuongCanNap = luuDan.MaxGrenadeCount - luuDan.CurrentGrenadeCount;
            if (soLuongCanNap > 0)
            {
                if (!luuDan.gameObject.activeSelf)
                {
                    luuDan.gameObject.SetActive(true);
                }

                luuDan.ThietLapLoaiVaSoLuong(
                    luuDan.CurrentGrenadeType,
                    luuDan.MaxGrenadeCount,
                    luuDan.MaxGrenadeCount
                );
            }
        }
    }

    private void KhoiPhucTrangThaiDanGoc()
    {
        for (int i = 0; i < danhSachSungThuong.Count; i++)
        {
            WeaponSystem sung = danhSachSungThuong[i];
            if (sung == null || TruongInfiniteAmmoSungThuong == null) continue;

            TruongInfiniteAmmoSungThuong.SetValue(
                sung,
                trangThaiGocWeaponSystem.TryGetValue(sung, out bool goc) && goc
            );
        }

        for (int i = 0; i < danhSachSungTia.Count; i++)
        {
            SniperWeapon sungTia = danhSachSungTia[i];
            if (sungTia == null || TruongInfiniteAmmoSungTia == null) continue;

            TruongInfiniteAmmoSungTia.SetValue(
                sungTia,
                trangThaiGocSniperWeapon.TryGetValue(sungTia, out bool goc) && goc
            );
        }
    }

    private void ApDungBatTuChoPlayer()
    {
        for (int i = 0; i < danhSachMauNguoiChoi.Count; i++)
        {
            HealthSystem hp = danhSachMauNguoiChoi[i];
            if (hp == null) continue;

            if (hp.DaChet)
            {
                hp.HoiSinh();
            }

            if (hp.MauHienTai < hp.MauToiDa)
            {
                hp.HoiMau(hp.MauToiDa - hp.MauHienTai);
            }

            if (hp.GiapToiDa > 0f && hp.GiapHienTai < hp.GiapToiDa)
            {
                hp.HoiGiap(hp.GiapToiDa - hp.GiapHienTai);
            }
        }
    }

    private bool LaDamageCuaNguoiChoi(DamageDealer dealer)
    {
        if (dealer == null || TruongPheDamageDealer == null) return false;

        object pheObj = TruongPheDamageDealer.GetValue(dealer);
        if (pheObj is DamageDealer.PheGaySatThuong phe)
        {
            return phe == DamageDealer.PheGaySatThuong.TuNguoiChoi || phe == DamageDealer.PheGaySatThuong.TatCa;
        }
        return false;
    }

    private void LuuSatThuongGoc()
    {
        if (TruongSatThuongDamageDealer == null) return;

        float mucSatThuong = Mathf.Max(99999f, satThuongChetNgay);

        for (int i = 0; i < danhSachGaySatThuong.Count; i++)
        {
            DamageDealer dealer = danhSachGaySatThuong[i];
            if (dealer == null || !LaDamageCuaNguoiChoi(dealer)) continue;
            if (duLieuSatThuongGoc.ContainsKey(dealer)) continue;

            float satThuongHienTai = (float)TruongSatThuongDamageDealer.GetValue(dealer);
            // Nếu chưa từng bị ép sát thương bởi cheat mới lưu làm sát thương gốc
            if (!Mathf.Approximately(satThuongHienTai, mucSatThuong))
            {
                duLieuSatThuongGoc[dealer] = new DuLieuSatThuongGoc
                {
                    satThuongGoc = satThuongHienTai
                };
            }
        }
    }

    // Ép tăng sát thương của mọi DamageDealer thuộc người chơi (kể cả đạn mới spawn)
    private void ApDungDamgeChetNgay()
    {
        if (TruongSatThuongDamageDealer == null) return;

        float mucSatThuong = Mathf.Max(99999f, satThuongChetNgay);

        for (int i = 0; i < danhSachGaySatThuong.Count; i++)
        {
            DamageDealer dealer = danhSachGaySatThuong[i];
            if (dealer == null || !LaDamageCuaNguoiChoi(dealer)) continue;

            float satThuongHienTai = (float)TruongSatThuongDamageDealer.GetValue(dealer);
            if (!duLieuSatThuongGoc.ContainsKey(dealer) && !Mathf.Approximately(satThuongHienTai, mucSatThuong))
            {
                duLieuSatThuongGoc[dealer] = new DuLieuSatThuongGoc
                {
                    satThuongGoc = satThuongHienTai
                };
            }

            if (!Mathf.Approximately(satThuongHienTai, mucSatThuong))
            {
                dealer.SetDamage(mucSatThuong);
                TruongSatThuongDamageDealer.SetValue(dealer, mucSatThuong);
            }
        }
    }

    private void KhoiPhucSatThuongGoc()
    {
        if (TruongSatThuongDamageDealer == null) return;

        foreach (KeyValuePair<DamageDealer, DuLieuSatThuongGoc> cap in duLieuSatThuongGoc)
        {
            if (cap.Key != null)
            {
                cap.Key.SetDamage(cap.Value.satThuongGoc);
                TruongSatThuongDamageDealer.SetValue(cap.Key, cap.Value.satThuongGoc);
            }
        }
        duLieuSatThuongGoc.Clear();
    }

    private void LuuTocDoGoc()
    {
        if (TruongNormalSpeed == null || TruongRunSpeed == null || TruongDashSpeed == null) return;

        for (int i = 0; i < danhSachDiChuyen.Count; i++)
        {
            IronStormMovement diChuyen = danhSachDiChuyen[i];
            if (diChuyen == null || duLieuTocDoGoc.ContainsKey(diChuyen)) continue;

            duLieuTocDoGoc[diChuyen] = new DuLieuTocDoGoc
            {
                tocDoBinhThuongGoc = (float)TruongNormalSpeed.GetValue(diChuyen),
                tocDoChayNhanhGoc = (float)TruongRunSpeed.GetValue(diChuyen),
                tocDoLuotDashGoc = (float)TruongDashSpeed.GetValue(diChuyen)
            };
        }
    }

    private void ApDungTocDoNhanh()
    {
        if (TruongNormalSpeed == null || TruongRunSpeed == null || TruongDashSpeed == null) return;

        float heSo = Mathf.Max(1.0f, heSoTocDoNhanh);

        for (int i = 0; i < danhSachDiChuyen.Count; i++)
        {
            IronStormMovement diChuyen = danhSachDiChuyen[i];
            if (diChuyen == null) continue;

            if (!duLieuTocDoGoc.TryGetValue(diChuyen, out DuLieuTocDoGoc goc))
            {
                goc = new DuLieuTocDoGoc
                {
                    tocDoBinhThuongGoc = (float)TruongNormalSpeed.GetValue(diChuyen),
                    tocDoChayNhanhGoc = (float)TruongRunSpeed.GetValue(diChuyen),
                    tocDoLuotDashGoc = (float)TruongDashSpeed.GetValue(diChuyen)
                };
                duLieuTocDoGoc[diChuyen] = goc;
            }

            TruongNormalSpeed.SetValue(diChuyen, goc.tocDoBinhThuongGoc * heSo);
            TruongRunSpeed.SetValue(diChuyen, goc.tocDoChayNhanhGoc * heSo);
            TruongDashSpeed.SetValue(diChuyen, goc.tocDoLuotDashGoc * heSo);
        }
    }

    private void KhoiPhucTocDoGoc()
    {
        if (TruongNormalSpeed == null || TruongRunSpeed == null || TruongDashSpeed == null) return;

        foreach (KeyValuePair<IronStormMovement, DuLieuTocDoGoc> cap in duLieuTocDoGoc)
        {
            if (cap.Key != null)
            {
                TruongNormalSpeed.SetValue(cap.Key, cap.Value.tocDoBinhThuongGoc);
                TruongRunSpeed.SetValue(cap.Key, cap.Value.tocDoChayNhanhGoc);
                TruongDashSpeed.SetValue(cap.Key, cap.Value.tocDoLuotDashGoc);
            }
        }
        duLieuTocDoGoc.Clear();
    }

    private void ApDungVoHanJumpPack()
    {
        if (TruongMaxEnergyJumpPack == null || TruongCurrentEnergyJumpPack == null) return;

        for (int i = 0; i < danhSachJumpPack.Count; i++)
        {
            SpaceMarineJumpPack jumpPack = danhSachJumpPack[i];
            if (jumpPack == null) continue;

            float nangLuongToiDa = Mathf.Max(1f, (float)TruongMaxEnergyJumpPack.GetValue(jumpPack));
            TruongCurrentEnergyJumpPack.SetValue(jumpPack, nangLuongToiDa);

            if (TruongEnergySliderJumpPack != null)
            {
                Slider thanhNangLuong = TruongEnergySliderJumpPack.GetValue(jumpPack) as Slider;
                if (thanhNangLuong != null)
                {
                    thanhNangLuong.maxValue = nangLuongToiDa;
                    thanhNangLuong.value = nangLuongToiDa;
                }
            }
        }
    }

    private void LuuLayerGocPlayer()
    {
        for (int i = 0; i < danhSachRootPlayer.Count; i++)
        {
            Transform rootObj = danhSachRootPlayer[i];
            if (rootObj == null) continue;

            Transform[] tatCaTransform = rootObj.GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < tatCaTransform.Length; j++)
            {
                Transform t = tatCaTransform[j];
                if (t == null) continue;
                if (!layerGocPlayer.ContainsKey(t))
                {
                    layerGocPlayer[t] = t.gameObject.layer;
                }
            }
        }
    }

    // Đổi toàn bộ layer trên root Player sang Default (layer 0) để bật tàng hình theo layer
    private void ApDungLayerDefaultChoPlayer()
    {
        for (int i = 0; i < danhSachRootPlayer.Count; i++)
        {
            Transform rootObj = danhSachRootPlayer[i];
            if (rootObj == null) continue;

            Transform[] tatCaTransform = rootObj.GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < tatCaTransform.Length; j++)
            {
                Transform t = tatCaTransform[j];
                if (t == null) continue;

                if (!layerGocPlayer.ContainsKey(t))
                {
                    layerGocPlayer[t] = t.gameObject.layer;
                }

                if (t.gameObject.layer != 0)
                {
                    t.gameObject.layer = 0;
                }
            }
        }
    }

    private void KhoiPhucLayerGocPlayer()
    {
        foreach (KeyValuePair<Transform, int> cap in layerGocPlayer)
        {
            if (cap.Key != null)
            {
                cap.Key.gameObject.layer = Mathf.Clamp(cap.Value, 0, 31);
            }
        }
        layerGocPlayer.Clear();
    }
}
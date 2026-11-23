using UnityEngine;
using UnityEngine.InputSystem;

// Component luu dan: nhan giu de nghieng ve sau & don luc (giu cang lau cang nem xa), tha nut de choi luu dan
public class GrenadeWeapon : MonoBehaviour
{
    [Header("1. Thong Tin Vu Khi & UI")]
    [SerializeField] private string tenHienThiVuKhi = "Frag Grenade";
    [SerializeField] private int soLuongLuuDanToiDa = 4;
    [SerializeField] private int soLuongLuuDanKhoiDau = -1;

    [Tooltip("Thoi gian nghi bat buoc sau moi lan choi luu dan truoc khi duoc nem tiep (giay).")]
    [SerializeField] private float thoiGianNghiGiuaCacLanNem = 1.25f;

    [Header("2. Phim Dieu Khien (New Input System)")]
    [SerializeField] private InputActionReference hanhDongNem;
    [SerializeField] private InputActionReference hanhDongHuyNem;

    [Header("3. Nem Xa Theo Thoi Gian Giu & Quy Dao Luu Dan")]
    [SerializeField] private Transform diemXuatPhatNem;
    [SerializeField] private GameObject prefabQuaLuuDan;

    [Tooltip("Toc do nem khi moi nhan tha ngan (nem gan).")]
    [SerializeField] private float tocDoNemCoBan = 14f;

    [Tooltip("Toc do nem khi giu du thoi gian don luc toi da (nem rat xa).")]
    [SerializeField] private float tocDoNemToiDa = 36f;

    [Tooltip("Thoi gian giu nut can thiet de dat 100% luc nem xa toi da (giay).")]
    [SerializeField] private float thoiGianDonLucToiDa = 1.25f;

    [Tooltip("Duong cong dieu khien cam giac tang luc theo thoi gian giu (0 = moi nhan, 1 = dat luc toi da).")]
    [SerializeField] private AnimationCurve duongCongDonLuc = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Luc day len parabol khi nem gan (giu ngan).")]
    [SerializeField] private float lucDayLenParabolCoBan = 2.4f;

    [Tooltip("Luc day len parabol khi nem xa (giu lau) de quy dao bay cao va xa hon.")]
    [SerializeField] private float lucDayLenParabolToiDa = 6.2f;

    [SerializeField] private float thoiGianNgoiNo = 2.2f;

    [Header("4. Hieu Ung Nghien Luu Dan Khi Nhan Giu")]
    [Tooltip("Doi tuong mesh/vu khi luu dan se nghieng ve sau khi giu nut (neu de trong se tu dong dung chinh transform nay).")]
    [SerializeField] private Transform doiTuongNghienLuDan;

    [Tooltip("Goc nghieng ve phia sau (do) khi don luc toi da. Truc X am thuong nga ra sau theo chieu tay/sung.")]
    [SerializeField] private Vector3 gocNghienVeSauToiDa = new Vector3(-28f, 0f, 0f);

    [Tooltip("Do lui/nhac nhe (met) khi don luc toi da de tao cam giac lay da tu nhien.")]
    [SerializeField] private Vector3 doLuiKhiLayDaToiDa = new Vector3(0f, 0.04f, -0.08f);

    [Tooltip("Toc do noi suy nghieng/tra ve vi tri ban dau.")]
    [SerializeField] private float tocDoNoiSuyNghien = 14f;

    [Header("5. Sat Thuong & Vung No")]
    [SerializeField] private float satThuongTaiTamNo = 95f;
    [SerializeField] private float banKinhVungNo = 7.5f;
    [Range(0f, 1f)]
    [SerializeField] private float heSoSatThuongRiaNo = 0.25f;
    [SerializeField] private LayerMask lopCoTheNhanSatThuong = ~0;
    [SerializeField] private LayerMask lopVatCan = 0;
    [SerializeField] private bool kiemTraVatCanCheKhuat = false;
    [SerializeField] private float lucDayVatLy = 12f;

    [Header("6. Lien Ket Camera, Hieu Ung & Am Thanh")]
    [SerializeField] private IronStormCamera scriptCamera;
    [SerializeField] private float cuongDoRungCamera = 1.2f;
    [SerializeField] private GameObject prefabHieuUngNo;
    [SerializeField] private float thoiGianHuyHieuUngNo = 3f;

    [SerializeField] private AudioSource nguonAmThanh;
    [SerializeField] private AudioClip amThanhRutChot;
    [SerializeField] private AudioClip amThanhNem;
    [SerializeField] private AudioClip amThanhHuy;
    [SerializeField] private AudioClip amThanhNo;

    [Range(0f, 1f)]
    [SerializeField] private float amLuong = 0.9f;

    private int soLuongLuuDanHienTai;
    private float thoiDiemNemGanNhat = -999f;
    private bool dangRutChotDonLuc;
    private float thoiGianDaDonLuc;

    // Luu vi tri va goc ban dau de tra ve chinh xac sau khi tha nut nem hoac huy
    private Vector3 viTriCucBoBanDau;
    private Quaternion gocCucBoBanDau;
    private bool daLuuTrangThaiGoc;

    private WeaponSwitcher boChuyenVuKhi;
    private InputAction hanhDongNemTamThoi;
    private InputAction hanhDongHuyTamThoi;

    public string WeaponDisplayName => string.IsNullOrWhiteSpace(tenHienThiVuKhi) ? gameObject.name : tenHienThiVuKhi;
    public int CurrentGrenadeCount => soLuongLuuDanHienTai;
    public int MaxGrenadeCount => soLuongLuuDanToiDa;
    public bool ConLuuDan => soLuongLuuDanHienTai > 0;
    public float ThoiGianNghiConLai => Mathf.Max(0f, (thoiDiemNemGanNhat + thoiGianNghiGiuaCacLanNem) - Time.time);
    public string ModeLabel => "GRENADE";

    // Tinh ty le don luc (0..1) sau khi di qua AnimationCurve
    public float TyLeLucNemHienTai => TinhTyLeLucNem(thoiGianDaDonLuc);

    // Chuoi hien thi len WeaponUI (so luong dan + % luc nem hoac thoi gian nghi)
    public string CurrentStateLabel
    {
        get
        {
            if (dangRutChotDonLuc)
            {
                int phanTramLuc = Mathf.RoundToInt(TyLeLucNemHienTai * 100f);
                return $"{soLuongLuuDanHienTai} / {soLuongLuuDanToiDa} (LUC {phanTramLuc}%)";
            }

            if (soLuongLuuDanHienTai <= 0)
            {
                return $"0 / {soLuongLuuDanToiDa}";
            }

            if (ThoiGianNghiConLai > 0.01f)
            {
                return $"{soLuongLuuDanHienTai} / {soLuongLuuDanToiDa} (NGHI {ThoiGianNghiConLai:0.1}s)";
            }

            return $"{soLuongLuuDanHienTai} / {soLuongLuuDanToiDa}";
        }
    }

    private InputAction HanhDongNemDangDung =>
        hanhDongNem != null && hanhDongNem.action != null ? hanhDongNem.action : hanhDongNemTamThoi;

    private InputAction HanhDongHuyDangDung =>
        hanhDongHuyNem != null && hanhDongHuyNem.action != null ? hanhDongHuyNem.action : hanhDongHuyTamThoi;

    private void Awake()
    {
        soLuongLuuDanToiDa = Mathf.Max(1, soLuongLuuDanToiDa);
        soLuongLuuDanHienTai = soLuongLuuDanKhoiDau < 0
            ? soLuongLuuDanToiDa
            : Mathf.Clamp(soLuongLuuDanKhoiDau, 0, soLuongLuuDanToiDa);

        if (duongCongDonLuc == null || duongCongDonLuc.length == 0)
        {
            duongCongDonLuc = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        TuDongGanThanhPhan();
        LuuTrangThaiGocNghien();

        // Khoi tao InputAction du phong khi chua gan InputActionReference tren Inspector
        hanhDongNemTamThoi = new InputAction("GrenadeThrow", InputActionType.Button);
        hanhDongNemTamThoi.AddBinding("<Mouse>/leftButton");
        hanhDongNemTamThoi.AddBinding("<Keyboard>/g");

        hanhDongHuyTamThoi = new InputAction("GrenadeCancel", InputActionType.Button);
        hanhDongHuyTamThoi.AddBinding("<Mouse>/rightButton");
        hanhDongHuyTamThoi.AddBinding("<Keyboard>/r");
    }

    private void Start()
    {
        if (soLuongLuuDanHienTai <= 0)
        {
            XuLyKhiHetLuuDan();
        }
    }

    private void TuDongGanThanhPhan()
    {
        if (boChuyenVuKhi == null)
        {
            boChuyenVuKhi = GetComponentInParent<WeaponSwitcher>();
            if (boChuyenVuKhi == null)
            {
                boChuyenVuKhi = FindFirstObjectByType<WeaponSwitcher>();
            }
        }

        if (doiTuongNghienLuDan == null)
        {
            Transform goiYModel = TimChildTheoTuKhoa(transform, "mesh", "model", "grenade", "body", "visual");
            doiTuongNghienLuDan = goiYModel != null ? goiYModel : transform;
        }

        if (diemXuatPhatNem == null)
        {
            diemXuatPhatNem = TimChildTheoTuKhoa(transform, "throw", "muzzle", "hand", "grenade", "origin");
            if (diemXuatPhatNem == null)
            {
                diemXuatPhatNem = transform;
            }
        }

        if (scriptCamera == null)
        {
            scriptCamera = GetComponentInParent<IronStormCamera>();
            if (scriptCamera == null)
            {
                scriptCamera = FindFirstObjectByType<IronStormCamera>();
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
    }

    private void LuuTrangThaiGocNghien()
    {
        if (daLuuTrangThaiGoc || doiTuongNghienLuDan == null) return;
        viTriCucBoBanDau = doiTuongNghienLuDan.localPosition;
        gocCucBoBanDau = doiTuongNghienLuDan.localRotation;
        daLuuTrangThaiGoc = true;
    }

    // Goi khi vu khi duoc gan vao holder moi de dong bo trang thai goc chinh xac
    public void DongBoTrangThaiSauKhiGanHolder(Vector3 viTriHolder, Quaternion gocHolder)
    {
        dangRutChotDonLuc = false;
        thoiGianDaDonLuc = 0f;

        if (doiTuongNghienLuDan == null)
        {
            TuDongGanThanhPhan();
        }

        if (doiTuongNghienLuDan != null)
        {
            if (doiTuongNghienLuDan == transform)
            {
                viTriCucBoBanDau = viTriHolder;
                gocCucBoBanDau = gocHolder;
                daLuuTrangThaiGoc = true;
                doiTuongNghienLuDan.localPosition = viTriCucBoBanDau;
                doiTuongNghienLuDan.localRotation = gocCucBoBanDau;
            }
            else
            {
                viTriCucBoBanDau = doiTuongNghienLuDan.localPosition;
                gocCucBoBanDau = doiTuongNghienLuDan.localRotation;
                daLuuTrangThaiGoc = true;
            }
        }
    }

    private Transform TimChildTheoTuKhoa(Transform cha, params string[] tuKhoa)
    {
        Transform[] danhSachCon = cha.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < danhSachCon.Length; i++)
        {
            Transform con = danhSachCon[i];
            if (con == null || con == cha) continue;
            string tenThuong = con.name.ToLowerInvariant();
            for (int j = 0; j < tuKhoa.Length; j++)
            {
                if (tenThuong.Contains(tuKhoa[j]))
                {
                    return con;
                }
            }
        }
        return null;
    }

    private void OnEnable()
    {
        TuDongGanThanhPhan();
        LuuTrangThaiGocNghien();
        TraVeTrangThaiGocNghien(tucThi: true);
        HanhDongNemDangDung?.Enable();
        HanhDongHuyDangDung?.Enable();
    }

    private void OnDisable()
    {
        dangRutChotDonLuc = false;
        thoiGianDaDonLuc = 0f;
        TraVeTrangThaiGocNghien(tucThi: true);
        HanhDongNemDangDung?.Disable();
        HanhDongHuyDangDung?.Disable();
    }

    private void OnDestroy()
    {
        TraVeTrangThaiGocNghien(tucThi: true);
        hanhDongNemTamThoi?.Dispose();
        hanhDongHuyTamThoi?.Dispose();
    }

    private void Update()
    {
        if (!gameObject.activeInHierarchy || !enabled)
        {
            dangRutChotDonLuc = false;
            thoiGianDaDonLuc = 0f;
            TraVeTrangThaiGocNghien(tucThi: true);
            return;
        }

        if (soLuongLuuDanHienTai <= 0)
        {
            XuLyKhiHetLuuDan();
            return;
        }

        HanhDongNemDangDung?.Enable();
        HanhDongHuyDangDung?.Enable();

        bool vuaNhanNem = HanhDongNemDangDung != null && HanhDongNemDangDung.WasPressedThisFrame();
        bool dangGiuNem = HanhDongNemDangDung != null && HanhDongNemDangDung.IsPressed();
        bool vuaNhanHuy = HanhDongHuyDangDung != null && HanhDongHuyDangDung.WasPressedThisFrame();

        // Neu dang giu luu dan ma nhan huy thi huy va tra lai goc/vi tri ban dau
        if (dangRutChotDonLuc && vuaNhanHuy)
        {
            HuyDonLucNem();
            CapNhatHieuUngNghienLuDan();
            return;
        }

        // Bat dau rut chot khi da het thoi gian nghi sau lan nem truoc
        if (!dangRutChotDonLuc && vuaNhanNem && CoTheBatDauNem())
        {
            dangRutChotDonLuc = true;
            thoiGianDaDonLuc = 0f;
            PhatAmThanh(amThanhRutChot);
        }
        else if (dangRutChotDonLuc)
        {
            // Cong dồn thoi gian giu (gioi han toi da bang thoiGianDonLucToiDa de khong lech thoi gian khi tha)
            thoiGianDaDonLuc = Mathf.Min(thoiGianDonLucToiDa, thoiGianDaDonLuc + Time.deltaTime);

            // Khi tha nut nem -> choi luu dan voi luc tuong ung thoi gian da giu
            if (!dangGiuNem)
            {
                ThucHienChoiLuuDan(thoiGianDaDonLuc);
                dangRutChotDonLuc = false;
                thoiGianDaDonLuc = 0f;
                TraVeTrangThaiGocNghien(tucThi: false);
            }
        }

        CapNhatHieuUngNghienLuDan();
    }

    // Quy doi thoi gian da giu (giay) sang ty le luc (0..1) theo duong cong don luc
    private float TinhTyLeLucNem(float thoiGianGiu)
    {
        if (thoiGianDonLucToiDa <= 0.001f) return 1f;
        float tyLeTho = Mathf.Clamp01(thoiGianGiu / thoiGianDonLucToiDa);
        return duongCongDonLuc != null ? Mathf.Clamp01(duongCongDonLuc.Evaluate(tyLeTho)) : tyLeTho;
    }

    // Dieu khien do nghieng ve sau theo muc do giu nut (0 -> 1)
    private void CapNhatHieuUngNghienLuDan()
    {
        if (doiTuongNghienLuDan == null) return;
        if (!daLuuTrangThaiGoc) LuuTrangThaiGocNghien();

        float mucDoDonLuc = 0f;
        if (dangRutChotDonLuc)
        {
            // Giu muc toi thieu 0.2f ngay khi vua nhan, sau do tang dan theo thoi gian giu
            mucDoDonLuc = Mathf.Lerp(0.2f, 1f, TyLeLucNemHienTai);
        }

        Quaternion gocDich = gocCucBoBanDau * Quaternion.Euler(gocNghienVeSauToiDa * mucDoDonLuc);
        Vector3 viTriDich = viTriCucBoBanDau + (doLuiKhiLayDaToiDa * mucDoDonLuc);

        float buocNoiSuy = 1f - Mathf.Exp(-tocDoNoiSuyNghien * Time.deltaTime);
        doiTuongNghienLuDan.localRotation = Quaternion.Slerp(doiTuongNghienLuDan.localRotation, gocDich, buocNoiSuy);
        doiTuongNghienLuDan.localPosition = Vector3.Lerp(doiTuongNghienLuDan.localPosition, viTriDich, buocNoiSuy);
    }

    // Tra ve vi tri va goc ban dau
    private void TraVeTrangThaiGocNghien(bool tucThi)
    {
        if (!daLuuTrangThaiGoc || doiTuongNghienLuDan == null) return;

        if (tucThi)
        {
            doiTuongNghienLuDan.localRotation = gocCucBoBanDau;
            doiTuongNghienLuDan.localPosition = viTriCucBoBanDau;
            return;
        }

        float buocNoiSuy = 1f - Mathf.Exp(-tocDoNoiSuyNghien * Time.deltaTime);
        doiTuongNghienLuDan.localRotation = Quaternion.Slerp(doiTuongNghienLuDan.localRotation, gocCucBoBanDau, buocNoiSuy);
        doiTuongNghienLuDan.localPosition = Vector3.Lerp(doiTuongNghienLuDan.localPosition, viTriCucBoBanDau, buocNoiSuy);
    }

    private bool CoTheBatDauNem()
    {
        return soLuongLuuDanHienTai > 0 && Time.time >= thoiDiemNemGanNhat + thoiGianNghiGiuaCacLanNem;
    }

    private void HuyDonLucNem()
    {
        dangRutChotDonLuc = false;
        thoiGianDaDonLuc = 0f;
        PhatAmThanh(amThanhHuy);
    }

    private void ThucHienChoiLuuDan(float thoiGianGiu)
    {
        if (!CoTheBatDauNem()) return;

        soLuongLuuDanHienTai = Mathf.Max(0, soLuongLuuDanHienTai - 1);
        thoiDiemNemGanNhat = Time.time;
        PhatAmThanh(amThanhNem);

        Transform diemGoc = diemXuatPhatNem != null ? diemXuatPhatNem : transform;
        Vector3 viTriSinh = diemGoc.position + diemGoc.forward * 0.35f;

        Vector3 huongNem = LayHuongNem(diemGoc);
        float tyLeDonLuc = TinhTyLeLucNem(thoiGianGiu);

        // Giu cang lau -> toc do bay cang lon va luc day parabol cang cao de nem xa hon
        float tocDoNem = Mathf.Lerp(tocDoNemCoBan, tocDoNemToiDa, tyLeDonLuc);
        float lucDayLen = Mathf.Lerp(lucDayLenParabolCoBan, lucDayLenParabolToiDa, tyLeDonLuc);
        Vector3 vanTocDau = huongNem * tocDoNem + Vector3.up * lucDayLen;

        GameObject quaLuuDanObj;
        if (prefabQuaLuuDan != null)
        {
            quaLuuDanObj = Instantiate(prefabQuaLuuDan, viTriSinh, Quaternion.LookRotation(huongNem, Vector3.up));
        }
        else
        {
            quaLuuDanObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            quaLuuDanObj.name = "ThrownGrenade";
            quaLuuDanObj.transform.position = viTriSinh;
            quaLuuDanObj.transform.localScale = Vector3.one * 0.22f;
        }

        Rigidbody rb = quaLuuDanObj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = quaLuuDanObj.AddComponent<Rigidbody>();
        }
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = vanTocDau;
        rb.AddTorque(Random.insideUnitSphere * 12f, ForceMode.Impulse);

        DamageDealer nguoiGaySatThuong = quaLuuDanObj.GetComponent<DamageDealer>();
        if (nguoiGaySatThuong != null)
        {
            nguoiGaySatThuong.SetOwner(transform.root);
            nguoiGaySatThuong.SetDamage(satThuongTaiTamNo);
        }

        ThrownGrenadeProjectile vienLuuDan = quaLuuDanObj.GetComponent<ThrownGrenadeProjectile>();
        if (vienLuuDan == null)
        {
            vienLuuDan = quaLuuDanObj.AddComponent<ThrownGrenadeProjectile>();
        }

        vienLuuDan.Initialize(
            transform.root,
            thoiGianNgoiNo,
            satThuongTaiTamNo,
            banKinhVungNo,
            heSoSatThuongRiaNo,
            lopCoTheNhanSatThuong,
            lopVatCan,
            kiemTraVatCanCheKhuat,
            lucDayVatLy,
            scriptCamera,
            cuongDoRungCamera,
            prefabHieuUngNo,
            thoiGianHuyHieuUngNo,
            amThanhNo,
            amLuong
        );

        // Neu vua nem het qua luu dan cuoi cung -> an GameObject va chuyen select ve index 0
        if (soLuongLuuDanHienTai <= 0)
        {
            XuLyKhiHetLuuDan();
        }
    }

    // An GameObject luu dan va yeu cau WeaponSwitcher chuyen select sang index 0
    private void XuLyKhiHetLuuDan()
    {
        dangRutChotDonLuc = false;
        thoiGianDaDonLuc = 0f;
        TraVeTrangThaiGocNghien(tucThi: true);

        if (boChuyenVuKhi == null)
        {
            boChuyenVuKhi = GetComponentInParent<WeaponSwitcher>();
            if (boChuyenVuKhi == null)
            {
                boChuyenVuKhi = FindFirstObjectByType<WeaponSwitcher>();
            }
        }

        gameObject.SetActive(false);

        if (boChuyenVuKhi != null)
        {
            boChuyenVuKhi.XuLyKhiHetLuuDanChuyenVeIndex0();
        }
    }

    private Vector3 LayHuongNem(Transform diemGoc)
    {
        Camera cam = null;
        if (scriptCamera != null)
        {
            cam = scriptCamera.GetComponent<Camera>();
            if (cam == null)
            {
                cam = scriptCamera.GetComponentInChildren<Camera>();
            }
        }
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam != null)
        {
            Ray tiaNgam = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 diemMucTieu = Physics.Raycast(tiaNgam, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : tiaNgam.GetPoint(40f);

            Vector3 huong = diemMucTieu - diemGoc.position;
            if (huong.sqrMagnitude > 0.001f)
            {
                return huong.normalized;
            }
        }

        return diemGoc.forward.normalized;
    }

    public void AddGrenades(int soLuongThem)
    {
        if (soLuongThem <= 0) return;
        soLuongLuuDanHienTai = Mathf.Clamp(soLuongLuuDanHienTai + soLuongThem, 0, soLuongLuuDanToiDa);
    }

    private void PhatAmThanh(AudioClip clip)
    {
        if (clip != null && nguonAmThanh != null)
        {
            nguonAmThanh.PlayOneShot(clip, amLuong);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 tam = diemXuatPhatNem != null ? diemXuatPhatNem.position : transform.position;
        Gizmos.color = new Color(1f, 0.35f, 0.05f, 0.25f);
        Gizmos.DrawWireSphere(tam, banKinhVungNo);
    }
}

public class ThrownGrenadeProjectile : MonoBehaviour
{
    private static readonly Collider[] boDemVaChamNo = new Collider[32];

    private Transform gocChuSoHuu;
    private float boDemNgoiNo;
    private float satThuongGoc;
    private float banKinhNo;
    private float heSoRiaNo;
    private LayerMask lopNhanSatThuong;
    private LayerMask lopVatCan;
    private bool kiemTraCheKhuat;
    private float lucDay;

    private IronStormCamera scriptCamera;
    private float cuongDoRung;

    private GameObject prefabHieuUng;
    private float thoiGianHuyHieuUng;
    private AudioClip amThanhNo;
    private float amLuong;

    private bool daKhoiTao;
    private bool daNo;

    public void Initialize(
        Transform chuSoHuu,
        float thoiGianNgoi,
        float satThuongTam,
        float banKinhVung,
        float heSoSatThuongRia,
        LayerMask lopMucTieu,
        LayerMask lopChan,
        bool suDungKiemTraCheKhuat,
        float lucDayNo,
        IronStormCamera cameraThamChieu,
        float cuongDoRungCam,
        GameObject hieuUngPrefab,
        float thoiGianSongHieuUng,
        AudioClip clipNo,
        float amLuongClip)
    {
        gocChuSoHuu = chuSoHuu;
        boDemNgoiNo = Mathf.Max(0.05f, thoiGianNgoi);
        satThuongGoc = Mathf.Max(0f, satThuongTam);
        banKinhNo = Mathf.Max(0.2f, banKinhVung);
        heSoRiaNo = Mathf.Clamp01(heSoSatThuongRia);
        lopNhanSatThuong = lopMucTieu;
        lopVatCan = lopChan;
        kiemTraCheKhuat = suDungKiemTraCheKhuat;
        lucDay = Mathf.Max(0f, lucDayNo);

        scriptCamera = cameraThamChieu;
        cuongDoRung = cuongDoRungCam;

        prefabHieuUng = hieuUngPrefab;
        thoiGianHuyHieuUng = Mathf.Max(0.1f, thoiGianSongHieuUng);
        amThanhNo = clipNo;
        amLuong = Mathf.Clamp01(amLuongClip);

        daKhoiTao = true;
    }

    private void Update()
    {
        if (!daKhoiTao || daNo) return;

        boDemNgoiNo -= Time.deltaTime;
        if (boDemNgoiNo <= 0f)
        {
            PhatNo();
        }
    }

    private void PhatNo()
    {
        if (daNo) return;
        daNo = true;

        Vector3 viTriNo = transform.position;

        if (prefabHieuUng != null)
        {
            GameObject hieuUng = Instantiate(prefabHieuUng, viTriNo, Quaternion.identity);
            Destroy(hieuUng, thoiGianHuyHieuUng);
        }

        if (amThanhNo != null)
        {
            AudioSource.PlayClipAtPoint(amThanhNo, viTriNo, amLuong);
        }

        if (scriptCamera != null && cuongDoRung > 0f)
        {
            scriptCamera.SendMessage("AddShootShake", cuongDoRung, SendMessageOptions.DontRequireReceiver);
        }

        int soLuongVaCham = Physics.OverlapSphereNonAlloc(viTriNo, banKinhNo, boDemVaChamNo, lopNhanSatThuong, QueryTriggerInteraction.Ignore);
        System.Collections.Generic.HashSet<HealthSystem> daGaySatThuong = new System.Collections.Generic.HashSet<HealthSystem>();

        for (int i = 0; i < soLuongVaCham; i++)
        {
            Collider vaCham = boDemVaChamNo[i];
            if (vaCham == null) continue;

            if (gocChuSoHuu != null && vaCham.transform.root == gocChuSoHuu)
            {
                continue;
            }

            Vector3 diemGanNhat = vaCham.ClosestPoint(viTriNo);
            float khoangCach = Vector3.Distance(viTriNo, diemGanNhat);
            if (khoangCach > banKinhNo) continue;

            if (kiemTraCheKhuat && lopVatCan != 0)
            {
                Vector3 huong = diemGanNhat - viTriNo;
                float doDaiTia = huong.magnitude;
                if (doDaiTia > 0.01f && Physics.Raycast(viTriNo, huong / doDaiTia, doDaiTia, lopVatCan, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }
            }

            HealthSystem heThongMau = vaCham.GetComponentInParent<HealthSystem>();
            if (heThongMau != null && !heThongMau.DaChet && heThongMau.Phe == HealthSystem.LoaiDoiTuong.QuaiVat && !daGaySatThuong.Contains(heThongMau))
            {
                daGaySatThuong.Add(heThongMau);

                float tyLeGiamDan = 1f - Mathf.Clamp01(khoangCach / banKinhNo);
                float heSoSatThuong = Mathf.Lerp(heSoRiaNo, 1f, tyLeGiamDan);
                float satThuongCuoi = satThuongGoc * heSoSatThuong;

                if (satThuongCuoi > 0f)
                {
                    heThongMau.NhanSatThuong(satThuongCuoi);
                }
            }

            Rigidbody rbMucTieu = vaCham.GetComponentInParent<Rigidbody>();
            if (rbMucTieu != null && !rbMucTieu.isKinematic && lucDay > 0f)
            {
                rbMucTieu.AddExplosionForce(lucDay, viTriNo, banKinhNo, 0.5f, ForceMode.Impulse);
            }
        }

        Destroy(gameObject);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Thuoc tinh hien thi Dropdown chon Layer chuan cua Unity ngay tren Inspector
public sealed class LayerDropdownAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(LayerDropdownAttribute))]
public sealed class LayerDropdownPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Integer)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        EditorGUI.BeginProperty(position, label, property);
        int currentLayer = Mathf.Clamp(property.intValue, 0, 31);
        int selectedLayer = EditorGUI.LayerField(position, label, currentLayer);
        property.intValue = selectedLayer;
        EditorGUI.EndProperty();
    }
}
#endif

// Component luu dan: vua la vu khi nem, vua la vat pham nhat tiep dan (neu da co san se cong don)
public class GrenadeWeapon : MonoBehaviour
{
    public enum LoaiLuuDan
    {
        FragGrenade,
        Flashbang
    }

    [Header("1. Thong Tin Vu Khi & UI")]
    [SerializeField] private LoaiLuuDan loaiLuuDan = LoaiLuuDan.FragGrenade;
    [SerializeField] private string tenHienThiVuKhi = "Frag Grenade";
    [SerializeField] private int soLuongLuuDanToiDa = 4;

    [Tooltip("So luong luu dan luc moi vao game (-1 = tu dong lay full bang soLuongLuuDanToiDa).")]
    [SerializeField] private int soLuongLuuDanKhoiDau = -1;

    [Tooltip("So luong luu dan duoc cong them khi nhat qua luu dan ngoai map (-1 = tu lay so luong cua qua luu dan do, hoac 1 neu <= 0).")]
    [SerializeField] private int soLuongCongKhiNhat = -1;

    [Tooltip("Neu bat, khi sap day luu dan (vd con 1/4, nhat goi +2) se lay phan thieu cho day thay vi chan han; mac dinh TAT de dung yeu cau day thi khong nhat them.")]
    [SerializeField] private bool choPhepNhatMotPhanKhiGanDay = false;

    [Tooltip("Thoi gian nghi bat buoc sau moi lan choi luu dan truoc khi duoc nem tiep (giay).")]
    [SerializeField] private float thoiGianNghiGiuaCacLanNem = 1.25f;

    [Header("1b. Layer Khi Da Nhat & Hien Thi Khi Drop")]
    [Tooltip("Chon Layer (Dropdown) ma luu dan se doi sang khi nguoi choi nhat len tay.")]
    [LayerDropdown]
    [SerializeField] private int layerKhiDaNhat = 0;

    [Tooltip("Neu bat, se doi Layer cho ca cac object con (mesh, hieu ung...) cua luu dan khi nhat; tat neu chi doi root.")]
    [SerializeField] private bool apDungLayerChoCaObjectCon = true;

    [Tooltip("Prefab tuy chon dung de hien thi khi drop luu dan/flashbang ra map.")]
    [SerializeField] private GameObject prefabVatPhamDrop;

    [Tooltip("Bat/tat IsTrigger cho Collider cua luu dan khi drop ra ngoai map.")]
    [SerializeField] private bool batTriggerKhiDrop = false;

    [Tooltip("Ban kinh SphereCollider du phong neu vat pham drop chua co Collider nao.")]
    [SerializeField] private float banKinhColliderDrop = 0.25f;

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
    [Tooltip("Nen tro vao child mesh/model (khong tro vao root neu root bi WeaponSwitcher animate equip/holster de tranh rung giat).")]
    [SerializeField] private Transform doiTuongNghienLuDan;

    [Tooltip("Goc nghieng ve phia sau (do) khi don luc toi da. Truc X am thuong nga ra sau theo chieu tay/sung.")]
    [SerializeField] private Vector3 gocNghienVeSauToiDa = new Vector3(-28f, 0f, 0f);

    [Tooltip("Do lui/nhac nhe (met) khi don luc toi da de tao cam giac lay da tu nhien.")]
    [SerializeField] private Vector3 doLuiKhiLayDaToiDa = new Vector3(0f, 0.04f, -0.08f);

    [Tooltip("Toc do noi suy nghieng/tra ve vi tri ban dau.")]
    [SerializeField] private float tocDoNoiSuyNghien = 18f;

    [Header("5. Sat Thuong & Vung No")]
    [SerializeField] private float satThuongTaiTamNo = 95f;
    [SerializeField] private float banKinhVungNo = 7.5f;
    [Range(0f, 1f)]
    [SerializeField] private float heSoSatThuongRiaNo = 0.25f;
    [SerializeField] private LayerMask lopCoTheNhanSatThuong = ~0;
    [SerializeField] private LayerMask lopVatCan = 0;
    [SerializeField] private bool kiemTraVatCanCheKhuat = false;
    [SerializeField] private float lucDayVatLy = 12f;

    [Header("6. Thiet Lap Flashbang & Nham Mat")]
    [Tooltip("GameObject flashbang (tu dong tim theo layer 'Flash' ngay khi vao game neu de trong).")]
    [SerializeField] private GameObject flashCanvasObject;

    [Tooltip("Image flashbang (tu dong tim theo layer 'Flash' ngay khi vao game neu de trong).")]
    [SerializeField] private Image flashImage;

    [Tooltip("Ten layer dung de tu dong tim GameObject va Image flashbang khi vao game.")]
    [SerializeField] private string tenLayerFlash = "Flash";

    [Tooltip("Thoi gian hieu ung flash sang ro (giay).")]
    [SerializeField] private float thoiGianFlashSang = 1.5f;

    [Tooltip("Thoi gian flash tan dan (giay).")]
    [SerializeField] private float thoiGianFlashTan = 1.2f;

    [Tooltip("Script nham mat cua Player (neu de trong se tu tim tren Player/Scene).")]
    [SerializeField] private PlayerFlashlightAndEyelids nhamMatRef;

    [Header("7. Lien Ket Camera, Hieu Ung & Am Thanh")]
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
    private bool daTungCoLuuDan;
    private bool daQuetFlashTheoLayer;

    private Vector3 viTriCucBoBanDau;
    private Quaternion gocCucBoBanDau;
    private bool daLuuTrangThaiGoc;

    private WeaponSwitcher boChuyenVuKhi;
    private Camera cachedCam;
    private InputAction hanhDongNemTamThoi;
    private InputAction hanhDongHuyTamThoi;
    private Coroutine flashRoutineDangChay;
    private MonoBehaviour coroutineRunnerFlash;

    public LoaiLuuDan CurrentGrenadeType => loaiLuuDan;
    public string WeaponDisplayName => string.IsNullOrWhiteSpace(tenHienThiVuKhi) ? gameObject.name : tenHienThiVuKhi;
    public int CurrentGrenadeCount => soLuongLuuDanHienTai;
    public int MaxGrenadeCount => Mathf.Max(1, soLuongLuuDanToiDa);
    public bool ConLuuDan => soLuongLuuDanHienTai > 0;

    public bool DaDayLuuDan => soLuongLuuDanHienTai >= MaxGrenadeCount;
    public bool ChoPhepNhatMotPhanKhiGanDay => choPhepNhatMotPhanKhiGanDay;
    public GameObject PrefabVatPhamDrop => prefabVatPhamDrop;
    public bool BatTriggerKhiDrop => batTriggerKhiDrop;
    public float BanKinhColliderDrop => Mathf.Max(0.05f, banKinhColliderDrop);

    public float ThoiGianNghiConLai => Mathf.Max(0f, (thoiDiemNemGanNhat + thoiGianNghiGiuaCacLanNem) - Time.time);
    public string ModeLabel => loaiLuuDan == LoaiLuuDan.Flashbang ? "FLASHBANG" : "GRENADE";

    public int SoLuongCongKhiDuocNhat
    {
        get
        {
            if (soLuongCongKhiNhat > 0) return soLuongCongKhiNhat;
            if (soLuongLuuDanHienTai > 0) return soLuongLuuDanHienTai;
            return 1;
        }
    }

    public float TyLeLucNemHienTai => TinhTyLeLucNem(thoiGianDaDonLuc);

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

    private void KhoiTaoInputActionsNeuChuaCo()
    {
        if (hanhDongNemTamThoi == null)
        {
            hanhDongNemTamThoi = new InputAction("GrenadeThrow", InputActionType.Button);
            hanhDongNemTamThoi.AddBinding("<Mouse>/leftButton");
            hanhDongNemTamThoi.AddBinding("<Keyboard>/g");
        }

        if (hanhDongHuyTamThoi == null)
        {
            hanhDongHuyTamThoi = new InputAction("GrenadeCancel", InputActionType.Button);
            hanhDongHuyTamThoi.AddBinding("<Mouse>/rightButton");
            hanhDongHuyTamThoi.AddBinding("<Keyboard>/r");
        }
    }

    private void KichHoatInputActions()
    {
        KhoiTaoInputActionsNeuChuaCo();

        if (HanhDongNemDangDung != null && !HanhDongNemDangDung.enabled)
        {
            HanhDongNemDangDung.Enable();
        }

        if (HanhDongHuyDangDung != null && !HanhDongHuyDangDung.enabled)
        {
            HanhDongHuyDangDung.Enable();
        }
    }

    public void DongBoKhiKichHoatCamTrenTay()
    {
        TuDongGanThanhPhan();
        dangRutChotDonLuc = false;
        thoiGianDaDonLuc = 0f;
        enabled = true;
        LuuTrangThaiGocNghien();
        TraVeTrangThaiGocNghien(tucThi: true);
        KichHoatInputActions();
    }

    /// <summary>
    /// Doi Layer cho luu dan khi da duoc nhat len tay (goi sau RefreshWeaponLayer de khong bi ghi de).
    /// </summary>
    public void ApDungLayerKhiDaNhat()
    {
        int targetLayer = Mathf.Clamp(layerKhiDaNhat, 0, 31);
        if (string.IsNullOrEmpty(LayerMask.LayerToName(targetLayer))) return;

        Transform rootTarget = boChuyenVuKhi != null && boChuyenVuKhi.EquippedGrenadeSlot == this
            ? boChuyenVuKhi.EquippedGrenadeSlot.gameObject.transform
            : transform;

        if (!apDungLayerChoCaObjectCon)
        {
            rootTarget.gameObject.layer = targetLayer;
            return;
        }

        Transform[] allTransforms = rootTarget.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            if (allTransforms[i] != null)
            {
                allTransforms[i].gameObject.layer = targetLayer;
            }
        }
    }

    private static void DatLayerVeDefault(GameObject target)
    {
        if (target == null) return;

        target.layer = 0;
        Transform[] allTransforms = target.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            if (allTransforms[i] != null)
            {
                allTransforms[i].gameObject.layer = 0;
            }
        }
    }

    public bool DaDuocNhatVaCamTrenTay()
    {
        if (!gameObject.activeInHierarchy || !enabled) return false;
        if (soLuongLuuDanHienTai <= 0) return false;
        if (transform.parent == null) return false;

        if (boChuyenVuKhi == null)
        {
            boChuyenVuKhi = GetComponentInParent<WeaponSwitcher>();
        }

        bool thuocNguoiChoi = boChuyenVuKhi != null || GetComponentInParent<CharacterController>() != null;
        if (!thuocNguoiChoi) return false;

        if (boChuyenVuKhi != null)
        {
            if (boChuyenVuKhi.CurrentGrenadeWeapon != this) return false;
            if (boChuyenVuKhi.HolderTransform != null && !transform.IsChildOf(boChuyenVuKhi.HolderTransform)) return false;
        }

        return true;
    }

    public void ThietLapLoaiVaSoLuong(LoaiLuuDan loaiMoi, int soLuongNap, int soLuongToiDaMoi = -1)
    {
        if (soLuongToiDaMoi > 0)
        {
            soLuongLuuDanToiDa = Mathf.Max(1, soLuongToiDaMoi);
        }
        else
        {
            soLuongLuuDanToiDa = Mathf.Max(1, soLuongLuuDanToiDa);
        }

        loaiLuuDan = loaiMoi;
        tenHienThiVuKhi = loaiMoi == LoaiLuuDan.Flashbang ? "Flashbang" : "Frag Grenade";
        soLuongLuuDanHienTai = Mathf.Clamp(soLuongNap, 0, soLuongLuuDanToiDa);

        if (soLuongLuuDanHienTai > 0)
        {
            daTungCoLuuDan = true;
            gameObject.SetActive(true);
        }
    }

    public static void HienThiLaiVatPhamDrop(
        GameObject dropObj,
        LoaiLuuDan loaiDan,
        GameObject prefabDropTuyChon = null,
        bool suDungTrigger = false,
        float banKinhCollider = 0.25f,
        Transform playerRoot = null)
    {
        if (dropObj == null) return;

        dropObj.SetActive(true);
        dropObj.transform.SetParent(null, true);

        Renderer[] renderers = dropObj.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            r.enabled = true;
            if (!r.gameObject.activeSelf)
            {
                r.gameObject.SetActive(true);
            }
        }

        Collider[] cols = dropObj.GetComponentsInChildren<Collider>(true);
        bool coCollider = false;
        for (int i = 0; i < cols.Length; i++)
        {
            Collider c = cols[i];
            if (c == null) continue;
            c.enabled = true;
            c.isTrigger = suDungTrigger;
            coCollider = true;
        }

        if (!coCollider)
        {
            SphereCollider addedCol = dropObj.AddComponent<SphereCollider>();
            addedCol.radius = Mathf.Max(0.05f, banKinhCollider);
            addedCol.isTrigger = suDungTrigger;
        }

        if (!dropObj.TryGetComponent(out Rigidbody rb))
        {
            rb = dropObj.AddComponent<Rigidbody>();
        }
        rb.isKinematic = false;
        rb.useGravity = !suDungTrigger;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (playerRoot != null)
        {
            Collider[] dropCols = dropObj.GetComponentsInChildren<Collider>(true);
            Collider[] playerCols = playerRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < dropCols.Length; i++)
            {
                if (dropCols[i] == null) continue;
                for (int j = 0; j < playerCols.Length; j++)
                {
                    if (playerCols[j] == null) continue;
                    Physics.IgnoreCollision(dropCols[i], playerCols[j], true);
                }
            }
        }

        // Reset ve layer Default (0) truoc khi RefreshWeaponLayer de khong con mang layer khi cam tren tay
        DatLayerVeDefault(dropObj);
        WeaponClassifier.RefreshWeaponLayer(dropObj);
    }

    private void Awake()
    {
        soLuongLuuDanToiDa = Mathf.Max(1, soLuongLuuDanToiDa);

        if (soLuongLuuDanKhoiDau < 0 || soLuongLuuDanKhoiDau == 0)
        {
            soLuongLuuDanKhoiDau = soLuongLuuDanToiDa;
            soLuongLuuDanHienTai = soLuongLuuDanToiDa;
        }
        else
        {
            soLuongLuuDanHienTai = Mathf.Clamp(soLuongLuuDanKhoiDau, 0, soLuongLuuDanToiDa);
        }

        daTungCoLuuDan = soLuongLuuDanHienTai > 0;

        if (duongCongDonLuc == null || duongCongDonLuc.length == 0)
        {
            duongCongDonLuc = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        TuDongGanThanhPhan();
        TuDongGanFlashTheoLayer();
        DatAlphaFlash(0f);
        if (flashCanvasObject != null)
        {
            flashCanvasObject.SetActive(false);
        }

        LuuTrangThaiGocNghien();
        KhoiTaoInputActionsNeuChuaCo();
    }

    private void Start()
    {
        TuDongGanFlashTheoLayer();

        if (flashCanvasObject != null)
        {
            flashCanvasObject.SetActive(false);
        }

        if (soLuongLuuDanHienTai <= 0 && daTungCoLuuDan)
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
            Transform goiYThrow = TimChildTheoTuKhoa(transform, "throw", "muzzle", "hand", "grenade", "origin");
            diemXuatPhatNem = goiYThrow != null ? goiYThrow : transform;
        }

        if (scriptCamera == null)
        {
            scriptCamera = GetComponentInParent<IronStormCamera>();
            if (scriptCamera == null)
            {
                scriptCamera = FindFirstObjectByType<IronStormCamera>();
            }
        }

        if (nhamMatRef == null)
        {
            nhamMatRef = GetComponentInParent<PlayerFlashlightAndEyelids>();
            if (nhamMatRef == null)
            {
                nhamMatRef = FindFirstObjectByType<PlayerFlashlightAndEyelids>();
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

    private void TuDongGanFlashTheoLayer()
    {
        if (flashCanvasObject != null && flashImage != null)
        {
            return;
        }

        if (daQuetFlashTheoLayer && (flashCanvasObject != null || flashImage != null))
        {
            if (flashImage == null && flashCanvasObject != null)
            {
                flashImage = flashCanvasObject.GetComponentInChildren<Image>(true);
            }
            return;
        }

        daQuetFlashTheoLayer = true;

        string tenLayer = string.IsNullOrWhiteSpace(tenLayerFlash) ? "Flash" : tenLayerFlash.Trim();
        int flashLayer = LayerMask.NameToLayer(tenLayer);
        if (flashLayer < 0) return;

        Transform rootSearch = transform.root != null ? transform.root : transform;
        QuetGanFlashTrongDanhSachImage(rootSearch.GetComponentsInChildren<Image>(true), flashLayer);

        if (flashImage == null && flashCanvasObject == null)
        {
            GameObject[] rootObjs = gameObject.scene.IsValid() ? gameObject.scene.GetRootGameObjects() : null;
            if (rootObjs != null)
            {
                for (int r = 0; r < rootObjs.Length; r++)
                {
                    if (rootObjs[r] == null) continue;
                    QuetGanFlashTrongDanhSachImage(rootObjs[r].GetComponentsInChildren<Image>(true), flashLayer);
                    if (flashImage != null && flashCanvasObject != null) break;
                }
            }
        }

        if (flashImage == null && flashCanvasObject != null)
        {
            flashImage = flashCanvasObject.GetComponentInChildren<Image>(true);
        }
    }

    private void QuetGanFlashTrongDanhSachImage(Image[] danhSachImage, int flashLayer)
    {
        if (danhSachImage == null) return;

        for (int i = 0; i < danhSachImage.Length; i++)
        {
            Image img = danhSachImage[i];
            if (img == null || img.gameObject.layer != flashLayer) continue;

            GanThamChieuTuImageLayerFlash(img);
            if (flashImage != null && flashCanvasObject != null) break;
        }
    }

    private void GanThamChieuTuImageLayerFlash(Image img)
    {
        if (img == null) return;

        if (flashImage == null)
        {
            flashImage = img;
        }

        if (flashCanvasObject == null)
        {
            Transform current = img.transform;
            int flashLayer = img.gameObject.layer;
            while (current != null)
            {
                if (current.gameObject.layer == flashLayer)
                {
                    flashCanvasObject = current.gameObject;
                    break;
                }
                current = current.parent;
            }

            if (flashCanvasObject == null)
            {
                Canvas canvasCha = img.GetComponentInParent<Canvas>(true);
                flashCanvasObject = canvasCha != null ? canvasCha.gameObject : img.gameObject;
            }
        }
    }

    private void DatAlphaFlash(float alpha)
    {
        if (flashImage == null) return;
        Color c = flashImage.color;
        c.a = Mathf.Clamp01(alpha);
        flashImage.color = c;
    }

    private void LuuTrangThaiGocNghien()
    {
        if (doiTuongNghienLuDan == null) return;
        if (!daLuuTrangThaiGoc || doiTuongNghienLuDan != transform)
        {
            viTriCucBoBanDau = doiTuongNghienLuDan.localPosition;
            gocCucBoBanDau = doiTuongNghienLuDan.localRotation;
            daLuuTrangThaiGoc = true;
        }
    }

    public void DongBoTrangThaiSauKhiGanHolder(Vector3 viTriHolder, Quaternion gocHolder)
    {
        dangRutChotDonLuc = false;
        thoiGianDaDonLuc = 0f;
        enabled = true;

        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            if (rbs[i] == null) continue;
            rbs[i].linearVelocity = Vector3.zero;
            rbs[i].angularVelocity = Vector3.zero;
            rbs[i].isKinematic = true;
            rbs[i].useGravity = false;
            rbs[i].interpolation = RigidbodyInterpolation.None;
        }

        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null) cols[i].enabled = false;
        }

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
                doiTuongNghienLuDan.localPosition = viTriCucBoBanDau;
                doiTuongNghienLuDan.localRotation = gocCucBoBanDau;
            }
            else
            {
                viTriCucBoBanDau = doiTuongNghienLuDan.localPosition;
                gocCucBoBanDau = doiTuongNghienLuDan.localRotation;
            }
            daLuuTrangThaiGoc = true;
        }

        KichHoatInputActions();
        ApDungLayerKhiDaNhat();
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
        KichHoatInputActions();
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
        if (!DaDuocNhatVaCamTrenTay())
        {
            if (dangRutChotDonLuc)
            {
                dangRutChotDonLuc = false;
                thoiGianDaDonLuc = 0f;
                TraVeTrangThaiGocNghien(tucThi: true);
            }

            if (soLuongLuuDanHienTai <= 0 && daTungCoLuuDan && gameObject.activeInHierarchy)
            {
                XuLyKhiHetLuuDan();
            }
            return;
        }

        KichHoatInputActions();

        bool vuaNhanNem = HanhDongNemDangDung != null && HanhDongNemDangDung.WasPressedThisFrame();
        bool dangGiuNem = HanhDongNemDangDung != null && HanhDongNemDangDung.IsPressed();
        bool vuaNhanHuy = HanhDongHuyDangDung != null && HanhDongHuyDangDung.WasPressedThisFrame();

        if (dangRutChotDonLuc && vuaNhanHuy)
        {
            HuyDonLucNem();
            CapNhatHieuUngNghienLuDan();
            return;
        }

        if (!dangRutChotDonLuc && vuaNhanNem && CoTheBatDauNem())
        {
            dangRutChotDonLuc = true;
            thoiGianDaDonLuc = 0f;
            PhatAmThanh(amThanhRutChot);
        }
        else if (dangRutChotDonLuc)
        {
            thoiGianDaDonLuc = Mathf.Min(thoiGianDonLucToiDa, thoiGianDaDonLuc + Time.deltaTime);

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

    private float TinhTyLeLucNem(float thoiGianGiu)
    {
        if (thoiGianDonLucToiDa <= 0.001f) return 1f;
        float tyLeTho = Mathf.Clamp01(thoiGianGiu / thoiGianDonLucToiDa);
        return duongCongDonLuc != null ? Mathf.Clamp01(duongCongDonLuc.Evaluate(tyLeTho)) : tyLeTho;
    }

    private void CapNhatHieuUngNghienLuDan()
    {
        if (doiTuongNghienLuDan == null) return;
        if (!daLuuTrangThaiGoc) LuuTrangThaiGocNghien();

        if (doiTuongNghienLuDan == transform && boChuyenVuKhi != null && boChuyenVuKhi.IsSwitchingWeapon)
        {
            return;
        }

        if (!dangRutChotDonLuc)
        {
            float gocLech = Quaternion.Angle(doiTuongNghienLuDan.localRotation, gocCucBoBanDau);
            float viTriLech = (doiTuongNghienLuDan.localPosition - viTriCucBoBanDau).sqrMagnitude;

            if (gocLech <= 0.05f && viTriLech <= 0.000001f)
            {
                doiTuongNghienLuDan.localRotation = gocCucBoBanDau;
                doiTuongNghienLuDan.localPosition = viTriCucBoBanDau;
                return;
            }

            float buocVeGoc = 1f - Mathf.Exp(-tocDoNoiSuyNghien * Time.deltaTime);
            doiTuongNghienLuDan.localRotation = Quaternion.Slerp(doiTuongNghienLuDan.localRotation, gocCucBoBanDau, buocVeGoc);
            doiTuongNghienLuDan.localPosition = Vector3.Lerp(doiTuongNghienLuDan.localPosition, viTriCucBoBanDau, buocVeGoc);
            return;
        }

        float mucDoDonLuc = Mathf.Lerp(0.2f, 1f, TyLeLucNemHienTai);
        Quaternion gocDich = gocCucBoBanDau * Quaternion.Euler(gocNghienVeSauToiDa * mucDoDonLuc);
        Vector3 viTriDich = viTriCucBoBanDau + (doLuiKhiLayDaToiDa * mucDoDonLuc);

        float buocNoiSuy = 1f - Mathf.Exp(-tocDoNoiSuyNghien * Time.deltaTime);
        doiTuongNghienLuDan.localRotation = Quaternion.Slerp(doiTuongNghienLuDan.localRotation, gocDich, buocNoiSuy);
        doiTuongNghienLuDan.localPosition = Vector3.Lerp(doiTuongNghienLuDan.localPosition, viTriDich, buocNoiSuy);
    }

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
        return DaDuocNhatVaCamTrenTay() && Time.time >= thoiDiemNemGanNhat + thoiGianNghiGiuaCacLanNem;
    }

    private void HuyDonLucNem()
    {
        dangRutChotDonLuc = false;
        thoiGianDaDonLuc = 0f;
        PhatAmThanh(amThanhHuy);
    }

    private void ThucHienChoiLuuDan(float thoiGianGiu)
    {
        if (!CoTheBatDauNem() || prefabQuaLuuDan == null) return;

        daTungCoLuuDan = true;
        soLuongLuuDanHienTai = Mathf.Max(0, soLuongLuuDanHienTai - 1);
        thoiDiemNemGanNhat = Time.time;
        PhatAmThanh(amThanhNem);

        Transform diemGoc = diemXuatPhatNem != null ? diemXuatPhatNem : transform;
        Vector3 viTriSinh = diemGoc.position + diemGoc.forward * 0.35f;
        Vector3 huongNem = LayHuongNem(diemGoc);
        Quaternion gocSinh = Quaternion.LookRotation(huongNem, Vector3.up);

        float tyLeDonLuc = TinhTyLeLucNem(thoiGianGiu);
        float tocDoNem = Mathf.Lerp(tocDoNemCoBan, tocDoNemToiDa, tyLeDonLuc);
        float lucDayLen = Mathf.Lerp(lucDayLenParabolCoBan, lucDayLenParabolToiDa, tyLeDonLuc);
        Vector3 vanTocDau = huongNem * tocDoNem + Vector3.up * lucDayLen;

        GameObject quaLuuDanObj = Instantiate(prefabQuaLuuDan, viTriSinh, gocSinh);
        quaLuuDanObj.transform.SetParent(null, true);
        quaLuuDanObj.SetActive(true);

        if (quaLuuDanObj.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = vanTocDau;
            rb.AddTorque(Random.insideUnitSphere * 12f, ForceMode.Impulse);
        }

        ThrownGrenadeProjectile vienLuuDan = quaLuuDanObj.GetComponent<ThrownGrenadeProjectile>();
        if (vienLuuDan == null)
        {
            vienLuuDan = quaLuuDanObj.AddComponent<ThrownGrenadeProjectile>();
        }

        vienLuuDan.CauHinhBanDau(
            transform.root,
            loaiLuuDan,
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
            amLuong,
            this
        );

        if (soLuongLuuDanHienTai <= 0)
        {
            XuLyKhiHetLuuDan();
        }
    }

    private MonoBehaviour LayRunnerHoatDongChoFlash()
    {
        if (gameObject.activeInHierarchy && enabled)
        {
            coroutineRunnerFlash = this;
            return coroutineRunnerFlash;
        }

        if (coroutineRunnerFlash != null && coroutineRunnerFlash.gameObject.activeInHierarchy && coroutineRunnerFlash.enabled)
        {
            return coroutineRunnerFlash;
        }

        if (boChuyenVuKhi == null)
        {
            boChuyenVuKhi = GetComponentInParent<WeaponSwitcher>();
            if (boChuyenVuKhi == null)
            {
                boChuyenVuKhi = FindFirstObjectByType<WeaponSwitcher>();
            }
        }

        if (boChuyenVuKhi != null && boChuyenVuKhi.gameObject.activeInHierarchy && boChuyenVuKhi.enabled)
        {
            coroutineRunnerFlash = boChuyenVuKhi;
            return coroutineRunnerFlash;
        }

        Transform rootSearch = transform.root != null ? transform.root : transform;
        MonoBehaviour[] behaviours = rootSearch.GetComponentsInChildren<MonoBehaviour>(false);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour mb = behaviours[i];
            if (mb != null && mb.isActiveAndEnabled && mb.gameObject.activeInHierarchy)
            {
                coroutineRunnerFlash = mb;
                return coroutineRunnerFlash;
            }
        }

        MonoBehaviour fallbackMb = FindFirstObjectByType<MonoBehaviour>();
        if (fallbackMb != null && fallbackMb.isActiveAndEnabled && fallbackMb.gameObject.activeInHierarchy)
        {
            coroutineRunnerFlash = fallbackMb;
            return coroutineRunnerFlash;
        }

        return null;
    }

    public void KichHoatHieuUngFlashbang(Vector3 viTriNo)
    {
        TuDongGanFlashTheoLayer();

        if (nhamMatRef == null)
        {
            nhamMatRef = GetComponentInParent<PlayerFlashlightAndEyelids>();
            if (nhamMatRef == null)
            {
                nhamMatRef = FindFirstObjectByType<PlayerFlashlightAndEyelids>();
            }
        }

        if (nhamMatRef != null && nhamMatRef.IsClosingEyes)
        {
            return;
        }

        float maxRange = Mathf.Max(0.1f, banKinhVungNo * 1.5f);
        float khoangCach = Vector3.Distance(viTriNo, transform.root.position);
        if (khoangCach > maxRange)
        {
            return;
        }

        float cuongDoFlash = Mathf.Lerp(0.45f, 1f, 1f - Mathf.Clamp01(khoangCach / maxRange));

        if (flashCanvasObject == null && flashImage == null) return;

        MonoBehaviour runner = LayRunnerHoatDongChoFlash();
        if (runner == null) return;

        if (flashRoutineDangChay != null)
        {
            runner.StopCoroutine(flashRoutineDangChay);
            flashRoutineDangChay = null;
        }

        flashRoutineDangChay = runner.StartCoroutine(ChayHieuUngFlashRoutine(cuongDoFlash));
    }

    private IEnumerator ChayHieuUngFlashRoutine(float cuongDoMax)
    {
        if (flashCanvasObject != null)
        {
            flashCanvasObject.SetActive(true);
        }

        if (flashImage == null && flashCanvasObject != null)
        {
            flashImage = flashCanvasObject.GetComponentInChildren<Image>(true);
        }

        if (flashImage != null && !flashImage.gameObject.activeSelf)
        {
            flashImage.gameObject.SetActive(true);
        }

        float thoiGianSang = Mathf.Max(0.05f, thoiGianFlashSang);
        float timer = 0f;

        while (timer < thoiGianSang)
        {
            if (nhamMatRef != null && nhamMatRef.IsClosingEyes)
            {
                TatFlashNgay();
                flashRoutineDangChay = null;
                yield break;
            }

            timer += Time.deltaTime;
            DatAlphaFlash(cuongDoMax);
            yield return null;
        }

        float thoiGianTan = Mathf.Max(0.05f, thoiGianFlashTan);
        timer = 0f;

        while (timer < thoiGianTan)
        {
            if (nhamMatRef != null && nhamMatRef.IsClosingEyes)
            {
                TatFlashNgay();
                flashRoutineDangChay = null;
                yield break;
            }

            timer += Time.deltaTime;
            float t = 1f - Mathf.Clamp01(timer / thoiGianTan);
            DatAlphaFlash(cuongDoMax * t);
            yield return null;
        }

        TatFlashNgay();
        flashRoutineDangChay = null;
    }

    private void TatFlashNgay()
    {
        DatAlphaFlash(0f);

        if (flashCanvasObject != null)
        {
            flashCanvasObject.SetActive(false);
        }
    }

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
        if (cachedCam == null || !cachedCam.isActiveAndEnabled)
        {
            cachedCam = null;
            if (scriptCamera != null)
            {
                scriptCamera.TryGetComponent(out cachedCam);
                if (cachedCam == null)
                {
                    cachedCam = scriptCamera.GetComponentInChildren<Camera>();
                }
            }
            if (cachedCam == null)
            {
                cachedCam = Camera.main;
            }
        }

        if (cachedCam != null)
        {
            Ray tiaNgam = cachedCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
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

    public int AddGrenades(int soLuongThem)
    {
        if (soLuongThem <= 0) return 0;

        soLuongLuuDanToiDa = Mathf.Max(1, soLuongLuuDanToiDa);
        if (soLuongLuuDanHienTai >= soLuongLuuDanToiDa)
        {
            return 0;
        }

        int soLuongTruocKhiCong = soLuongLuuDanHienTai;
        int soLuongCanDeDay = soLuongLuuDanToiDa - soLuongLuuDanHienTai;

        if (!choPhepNhatMotPhanKhiGanDay && soLuongThem > soLuongCanDeDay)
        {
            return 0;
        }

        soLuongLuuDanHienTai = Mathf.Clamp(soLuongLuuDanHienTai + soLuongThem, 0, soLuongLuuDanToiDa);
        int soLuongDaCong = soLuongLuuDanHienTai - soLuongTruocKhiCong;

        if (soLuongLuuDanHienTai > 0)
        {
            daTungCoLuuDan = true;
            enabled = true;
            gameObject.SetActive(true);
            ApDungLayerKhiDaNhat();
        }

        return soLuongDaCong;
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
    private static readonly HashSet<HealthSystem> daGaySatThuongBuffer = new HashSet<HealthSystem>();

    [SerializeField] private GrenadeWeapon.LoaiLuuDan loaiLuuDan = GrenadeWeapon.LoaiLuuDan.FragGrenade;
    [SerializeField] private float thoiGianNgoiNoMacDinh = 2.2f;
    [SerializeField] private float satThuongGoc = 95f;
    [SerializeField] private float banKinhNo = 7.5f;
    [Range(0f, 1f)]
    [SerializeField] private float heSoRiaNo = 0.25f;
    [SerializeField] private LayerMask lopNhanSatThuong = ~0;
    [SerializeField] private LayerMask lopVatCan = 0;
    [SerializeField] private bool kiemTraCheKhuat = false;
    [SerializeField] private float lucDay = 12f;

    [SerializeField] private IronStormCamera scriptCamera;
    [SerializeField] private float cuongDoRung = 1.2f;

    [SerializeField] private GameObject prefabHieuUng;
    [SerializeField] private float thoiGianHuyHieuUng = 3f;
    [SerializeField] private AudioClip amThanhNo;
    [Range(0f, 1f)]
    [SerializeField] private float amLuong = 0.9f;

    private Transform gocChuSoHuu;
    private float boDemNgoiNo;
    private GrenadeWeapon grenadeSource;
    private bool daKhoiTao;
    private bool daNo;

    public void CauHinhBanDau(
        Transform chuSoHuu,
        GrenadeWeapon.LoaiLuuDan loaiDan,
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
        float amLuongClip,
        GrenadeWeapon nguonLuuDan)
    {
        gocChuSoHuu = chuSoHuu;
        loaiLuuDan = loaiDan;
        boDemNgoiNo = thoiGianNgoi > 0f ? thoiGianNgoi : Mathf.Max(0.05f, thoiGianNgoiNoMacDinh);
        satThuongGoc = satThuongTam > 0f ? satThuongTam : satThuongGoc;
        banKinhNo = banKinhVung > 0f ? banKinhVung : banKinhNo;
        heSoRiaNo = Mathf.Clamp01(heSoSatThuongRia);
        lopNhanSatThuong = lopMucTieu.value != 0 ? lopMucTieu : lopNhanSatThuong;
        lopVatCan = lopChan;
        kiemTraCheKhuat = suDungKiemTraCheKhuat;
        lucDay = lucDayNo >= 0f ? lucDayNo : lucDay;

        if (cameraThamChieu != null) scriptCamera = cameraThamChieu;
        cuongDoRung = cuongDoRungCam >= 0f ? cuongDoRungCam : cuongDoRung;

        if (hieuUngPrefab != null) prefabHieuUng = hieuUngPrefab;
        thoiGianHuyHieuUng = thoiGianSongHieuUng > 0f ? thoiGianSongHieuUng : thoiGianHuyHieuUng;
        if (clipNo != null) amThanhNo = clipNo;
        amLuong = Mathf.Clamp01(amLuongClip);

        grenadeSource = nguonLuuDan;
        daNo = false;
        daKhoiTao = true;
    }

    private void OnEnable()
    {
        daNo = false;
        if (!daKhoiTao)
        {
            boDemNgoiNo = Mathf.Max(0.05f, thoiGianNgoiNoMacDinh);
            daKhoiTao = true;
        }
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
            hieuUng.SetActive(true);
            ParticleSystem[] psList = hieuUng.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < psList.Length; i++)
            {
                if (psList[i] != null) psList[i].Play(true);
            }
            Destroy(hieuUng, Mathf.Max(0.1f, thoiGianHuyHieuUng));
        }

        if (amThanhNo != null)
        {
            AudioSource.PlayClipAtPoint(amThanhNo, viTriNo, Mathf.Clamp01(amLuong));
        }

        if (scriptCamera != null && cuongDoRung > 0f)
        {
            scriptCamera.SendMessage("AddShootShake", cuongDoRung, SendMessageOptions.DontRequireReceiver);
        }

        if (loaiLuuDan == GrenadeWeapon.LoaiLuuDan.Flashbang)
        {
            if (grenadeSource != null)
            {
                grenadeSource.KichHoatHieuUngFlashbang(viTriNo);
            }
            Destroy(gameObject);
            return;
        }

        float banKinhAnToan = Mathf.Max(0.2f, banKinhNo);
        int soLuongVaCham = Physics.OverlapSphereNonAlloc(viTriNo, banKinhAnToan, boDemVaChamNo, lopNhanSatThuong, QueryTriggerInteraction.Ignore);
        daGaySatThuongBuffer.Clear();

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
            if (khoangCach > banKinhAnToan) continue;

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
            if (heThongMau != null && !heThongMau.DaChet && heThongMau.Phe == HealthSystem.LoaiDoiTuong.QuaiVat && daGaySatThuongBuffer.Add(heThongMau))
            {
                float tyLeGiamDan = 1f - Mathf.Clamp01(khoangCach / banKinhAnToan);
                float heSoSatThuong = Mathf.Lerp(Mathf.Clamp01(heSoRiaNo), 1f, tyLeGiamDan);
                float satThuongCuoi = Mathf.Max(0f, satThuongGoc) * heSoSatThuong;

                if (satThuongCuoi > 0f)
                {
                    heThongMau.NhanSatThuong(satThuongCuoi);
                }
            }

            Rigidbody rbMucTieu = vaCham.GetComponentInParent<Rigidbody>();
            if (rbMucTieu != null && !rbMucTieu.isKinematic && lucDay > 0f)
            {
                rbMucTieu.AddExplosionForce(lucDay, viTriNo, banKinhAnToan, 0.5f, ForceMode.Impulse);
            }
        }

        Destroy(gameObject);
    }
}
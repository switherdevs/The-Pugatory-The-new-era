using UnityEngine;
using UnityEngine.InputSystem;

public class AnNapBuiCayPlayer : MonoBehaviour
{
    [Header("1. Thiết Lập Bụi Cây (Tag Bush)")]
    [Tooltip("Tag của GameObject bụi cây dùng để núp (mặc định: bush).")]
    [SerializeField] private string tagBuiCay = "bush";

    [Tooltip("Bán kính kiểm tra overlap quanh Player để bắt cả trường hợp bụi cây dùng Collider thường hoặc Trigger.")]
    [SerializeField] private float banKinhKiemTraBui = 0.85f;

    [Header("2. Lộ Diện Khi Tấn Công & Chạm Quái")]
    [Tooltip("Thời gian (giây) người chơi bị lộ diện sau khi tấn công để quái có thể phát hiện.")]
    [SerializeField] private float thoiGianLoDienSauTanCong = 3.0f;

    [Tooltip("Bán kính kiểm tra chạm trực tiếp với quái để quái phát hiện dù Player đang trong bụi.")]
    [SerializeField] private float banKinhChamQuai = 1.35f;

    [Tooltip("Layer của quái vật (để 0 nếu muốn quét mọi layer rồi lọc theo RangedEnemyAI / MeleeEnemyAI / HealthSystem).")]
    [SerializeField] private LayerMask layerQuaiVat = 0;

    [Tooltip("Tự động nhận biết nút chuột trái/phải hoặc phím tấn công để kích hoạt lộ diện khi tấn công.")]
    [SerializeField] private bool tuDongNhanDienTanCong = true;

    private bool dangOVungBuiCay = false;
    private float boDemThoiGianLoDien = 0f;
    private float boDemKiemTraVung = 0f;
    private const float CHU_KY_KIEM_TRA_VUNG = 0.08f;

    private WeaponSystem[] danhSachSungThuong;
    private SniperWeapon[] danhSachSungTia;
    private GrenadeWeapon[] danhSachLuuDan;
    private int[] soDanThuongTruocDo;
    private int[] soDanTiaTruocDo;
    private int[] soLuuDanTruocDo;

    public bool DangOVungBuiCay => dangOVungBuiCay;
    public bool DangLoDienDoTanCong => boDemThoiGianLoDien > 0f;

    // Người chơi được coi là đang núp an toàn trong bụi khi ở trong tag bush và không đang lộ diện do tấn công
    public bool DangAnToanTrongBuiCay => dangOVungBuiCay && boDemThoiGianLoDien <= 0f;

    private void Awake()
    {
        QuetDanhSachVuKhiCuaPlayer();
        CapNhatSoLuongVuKhiHienTai();
    }

    private void Update()
    {
        float deltaThoiGian = Mathf.Min(Time.deltaTime, 0.05f);
        if (deltaThoiGian <= 0f) return;

        if (boDemThoiGianLoDien > 0f)
        {
            boDemThoiGianLoDien -= deltaThoiGian;
            if (boDemThoiGianLoDien < 0f)
            {
                boDemThoiGianLoDien = 0f;
            }
        }

        if (tuDongNhanDienTanCong)
        {
            KiemTraHanhDongTanCongCuaPlayer();
        }

        boDemKiemTraVung -= deltaThoiGian;
        if (boDemKiemTraVung <= 0f)
        {
            boDemKiemTraVung = CHU_KY_KIEM_TRA_VUNG;
            CapNhatTrangThaiTrongBuiCay();
        }
    }

    // Cho phép gọi trực tiếp từ script vũ khí/kỹ năng nếu muốn báo Player vừa tấn công
    public void BaoNguoiChoiVuaTanCong(float thoiGianLoDienTuyChon = -1f)
    {
        float thoiGian = thoiGianLoDienTuyChon > 0f ? thoiGianLoDienTuyChon : thoiGianLoDienSauTanCong;
        if (thoiGian > boDemThoiGianLoDien)
        {
            boDemThoiGianLoDien = thoiGian;
        }
    }

    private void QuetDanhSachVuKhiCuaPlayer()
    {
        Transform rootPlayer = transform.root != null ? transform.root : transform;
        danhSachSungThuong = rootPlayer.GetComponentsInChildren<WeaponSystem>(true);
        danhSachSungTia = rootPlayer.GetComponentsInChildren<SniperWeapon>(true);
        danhSachLuuDan = rootPlayer.GetComponentsInChildren<GrenadeWeapon>(true);

        soDanThuongTruocDo = danhSachSungThuong != null ? new int[danhSachSungThuong.Length] : new int[0];
        soDanTiaTruocDo = danhSachSungTia != null ? new int[danhSachSungTia.Length] : new int[0];
        soLuuDanTruocDo = danhSachLuuDan != null ? new int[danhSachLuuDan.Length] : new int[0];
    }

    private void CapNhatSoLuongVuKhiHienTai()
    {
        if (danhSachSungThuong != null)
        {
            for (int i = 0; i < danhSachSungThuong.Length; i++)
            {
                soDanThuongTruocDo[i] = danhSachSungThuong[i] != null ? danhSachSungThuong[i].CurrentAmmo : 0;
            }
        }

        if (danhSachSungTia != null)
        {
            for (int i = 0; i < danhSachSungTia.Length; i++)
            {
                soDanTiaTruocDo[i] = danhSachSungTia[i] != null ? danhSachSungTia[i].CurrentAmmo : 0;
            }
        }

        if (danhSachLuuDan != null)
        {
            for (int i = 0; i < danhSachLuuDan.Length; i++)
            {
                soLuuDanTruocDo[i] = danhSachLuuDan[i] != null ? danhSachLuuDan[i].CurrentGrenadeCount : 0;
            }
        }
    }

    private void KiemTraHanhDongTanCongCuaPlayer()
    {
        bool vuaTanCong = false;

        // 1. Kiểm tra giảm số lượng đạn / lựu đạn trên các vũ khí của Player
        if (danhSachSungThuong != null)
        {
            for (int i = 0; i < danhSachSungThuong.Length; i++)
            {
                WeaponSystem sung = danhSachSungThuong[i];
                if (sung == null) continue;
                int danHienTai = sung.CurrentAmmo;
                if (danHienTai < soDanThuongTruocDo[i])
                {
                    vuaTanCong = true;
                }
                soDanThuongTruocDo[i] = danHienTai;
            }
        }

        if (danhSachSungTia != null)
        {
            for (int i = 0; i < danhSachSungTia.Length; i++)
            {
                SniperWeapon sungTia = danhSachSungTia[i];
                if (sungTia == null) continue;
                int danHienTai = sungTia.CurrentAmmo;
                if (danHienTai < soDanTiaTruocDo[i])
                {
                    vuaTanCong = true;
                }
                soDanTiaTruocDo[i] = danHienTai;
            }
        }

        if (danhSachLuuDan != null)
        {
            for (int i = 0; i < danhSachLuuDan.Length; i++)
            {
                GrenadeWeapon luuDan = danhSachLuuDan[i];
                if (luuDan == null) continue;
                int soLuongHienTai = luuDan.CurrentGrenadeCount;
                if (soLuongHienTai < soLuuDanTruocDo[i])
                {
                    vuaTanCong = true;
                }
                soLuuDanTruocDo[i] = soLuongHienTai;
            }
        }

        // 2. Kiểm tra bấm nút chuột trái (bắn/đánh) khi đang có súng/vũ khí
        bool nhanChuotTrai = Input.GetMouseButtonDown(0) ||
                             (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        if (nhanChuotTrai && ((danhSachSungThuong != null && danhSachSungThuong.Length > 0) ||
                              (danhSachSungTia != null && danhSachSungTia.Length > 0) ||
                              (danhSachLuuDan != null && danhSachLuuDan.Length > 0)))
        {
            vuaTanCong = true;
        }

        if (vuaTanCong)
        {
            BaoNguoiChoiVuaTanCong(thoiGianLoDienSauTanCong);
        }
    }

    private void CapNhatTrangThaiTrongBuiCay()
    {
        if (string.IsNullOrEmpty(tagBuiCay) || tagBuiCay == "Untagged")
        {
            dangOVungBuiCay = false;
            return;
        }

        float banKinh = Mathf.Max(0.25f, banKinhKiemTraBui);
        Vector3 tamKiemTra = transform.position + Vector3.up * 0.65f;

        Collider[] cacVaCham = Physics.OverlapSphere(tamKiemTra, banKinh, ~0, QueryTriggerInteraction.Collide);
        if (cacVaCham == null || cacVaCham.Length == 0)
        {
            dangOVungBuiCay = false;
            return;
        }

        bool timThayBui = false;
        for (int i = 0; i < cacVaCham.Length; i++)
        {
            Collider col = cacVaCham[i];
            if (col == null) continue;
            if (col.transform.root == transform.root) continue;

            if (CoTagBuiCay(col.transform))
            {
                timThayBui = true;
                break;
            }
        }

        dangOVungBuiCay = timThayBui;
    }

    private bool CoTagBuiCay(Transform doiTuong)
    {
        if (string.IsNullOrEmpty(tagBuiCay) || tagBuiCay == "Untagged") return false;
        Transform hienTai = doiTuong;
        while (hienTai != null)
        {
            if (hienTai.CompareTag(tagBuiCay))
            {
                return true;
            }
            hienTai = hienTai.parent;
        }
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other != null && CoTagBuiCay(other.transform))
        {
            dangOVungBuiCay = true;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other != null && CoTagBuiCay(other.transform))
        {
            dangOVungBuiCay = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != null && CoTagBuiCay(other.transform))
        {
            CapNhatTrangThaiTrongBuiCay();
        }
    }

    // Kiểm tra xem quái có đang chạm vào người chơi hay không (dùng để phá núp bụi khi chạm)
    public bool DangChamVoiQuai(Transform viTriQuai, float khoangCachChamBoSung = 0f)
    {
        float banKinh = Mathf.Max(0.5f, banKinhChamQuai + khoangCachChamBoSung);

        if (viTriQuai != null)
        {
            float khoangCach = Vector3.Distance(transform.position, viTriQuai.position);
            if (khoangCach <= banKinh)
            {
                return true;
            }
        }

        LayerMask mask = layerQuaiVat.value != 0 ? layerQuaiVat.value : ~0;
        Collider[] cacQuaiGan = Physics.OverlapSphere(transform.position + Vector3.up * 0.85f, banKinh, mask, QueryTriggerInteraction.Collide);
        if (cacQuaiGan == null || cacQuaiGan.Length == 0) return false;

        for (int i = 0; i < cacQuaiGan.Length; i++)
        {
            Collider col = cacQuaiGan[i];
            if (col == null) continue;
            if (col.transform.root == transform.root) continue;

            if (col.GetComponentInParent<RangedEnemyAI>() != null ||
                col.GetComponentInParent<MeleeEnemyAI>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = dangOVungBuiCay ? Color.green : new Color(0.2f, 0.8f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.65f, Mathf.Max(0.25f, banKinhKiemTraBui));

        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.45f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.85f, Mathf.Max(0.5f, banKinhChamQuai));
    }
}
using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChonTagTamXaAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ChonTagTamXaAttribute))]
public class ChonTagTamXaDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType == SerializedPropertyType.String)
        {
            EditorGUI.BeginProperty(position, label, property);
            string currentTag = string.IsNullOrEmpty(property.stringValue) ? "Untagged" : property.stringValue;
            property.stringValue = EditorGUI.TagField(position, label, currentTag);
            EditorGUI.EndProperty();
        }
        else
        {
            EditorGUI.PropertyField(position, property, label);
        }
    }
}
#endif

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(DoDuongRaycastAI))]
public class RangedEnemyAI : MonoBehaviour
{
    public enum TrangThaiAI
    {
        NghiNgoi,
        TuanTra,
        TruyDuoi,
        TimKiem,
        TanCong,
        VeChoCu,
        BoChayLuuDan
    }

    [Header("Phát hiện & Tầm nhìn")]
    [SerializeField] private LayerMask layerNguoiChoi;

    [ChonTagTamXa]
    [SerializeField] private string tagVatCan = "Untagged";

    [SerializeField] private float banKinhTamNhin = 24f;

    [Range(10f, 360f)]
    [SerializeField] private float gocNhin = 120f;

    [Range(0f, 90f)]
    [SerializeField] private float gocNhinTren = 35f;

    [Range(0f, 90f)]
    [SerializeField] private float gocNhinDuoi = 35f;

    [Header("Tăng Vùng Nhìn Khi Truy Đuổi")]
    [SerializeField] private float heSoTangBanKinhTruyDuoi = 1.5f;
    [SerializeField] private float heSoTangGocTruyDuoi = 1.4f;
    [SerializeField] private float heSoTangGocDocTruyDuoi = 1.35f;

    [SerializeField] private Transform diemDatMat;
    [SerializeField] private float thoiGianNhoDau = 4.5f;

    [Header("Tìm kiếm khi mất dấu & Xử lý vực thẳm")]
    [SerializeField] private float thoiGianTimKiemKhiMatDau = 10f;
    [SerializeField] private float banKinhTimKiem = 6.5f;
    [SerializeField] private bool xaDanLienTucKhiBiVucThamChan = true;

    [Header("Bỏ chạy & kiếm chỗ trốn khi nhận tin lựu đạn (Ưu tiên cao nhất)")]
    [SerializeField] private float thoiGianBoChayLuuDan = 3.0f;
    [SerializeField] private float khoangCachBoChayLuuDan = 9.5f;
    [SerializeField] private float tocDoBoChayLuuDan = 8.0f;
    [SerializeField] private float banKinhTimChoTronLuuDan = 14f;
    [SerializeField] private float khoangCachAnSauVatCan = 1.6f;

    [Header("Chế độ Lính canh & Tuần tra")]
    [SerializeField] private bool laLinhCanh = false;
    [SerializeField] private float thoiGianTuanTraKhiMatDau = 5f;
    [SerializeField] private Transform[] cacDiemTuanTra;
    [SerializeField] private float banKinhTuanTraNgauNhien = 8f;
    [SerializeField] private float khoangCachBuocTuanTra = 5f;

    [SerializeField] private float thoiGianNghi = 2f;
    [SerializeField] private float tocDoTuanTra = 3.2f;
    [SerializeField] private float tocDoTruyDuoi = 5.5f;
    [SerializeField] private float tocDoXoayHuong = 14f;

    [Header("Giữ khoảng cách & Bắn liên thanh tầm xa")]
    [SerializeField] private float tamBanToiDa = 16f;
    [SerializeField] private float khoangCachToiThieu = 7f;
    [SerializeField] private float khoangCachLyTuong = 11.5f;
    [SerializeField] private bool banLienThanh = true;

    [Min(1)]
    [SerializeField] private int soVienLienThanh = 4;
    [SerializeField] private float khoangCachGiuaCacVien = 0.14f;
    [SerializeField] private float thoiGianNghiGiuaLoat = 1.6f;

    [SerializeField] private GameObject prefabDanTamXa;
    [SerializeField] private Transform[] diemBanDan;
    [SerializeField] private bool banDongLoatTatCaNong = true;
    [SerializeField] private float tocDoDan = 24f;
    [SerializeField] private float satThuongTamXa = 15f;

    [Header("Âm thanh")]
    [SerializeField] private AudioSource nguonAmThanh;
    [SerializeField] private AudioClip amThanhBuocChan;
    [SerializeField] private float khoangCachBuocChan = 0.45f;
    [SerializeField] private AudioClip amThanhPhatHien;
    [Range(0f, 1f)]
    [SerializeField] private float amLuong = 0.9f;

    [Header("Hiển thị vùng nhìn Gizmos")]
    [SerializeField] private bool hienThiVungNhinGizmos = true;
    [SerializeField] private Color mauVungTamNhin = new Color(0.2f, 0.8f, 1f, 0.18f);
    [SerializeField] private Color mauVungTamBan = new Color(0.2f, 1f, 0.5f, 0.35f);

    private DoDuongRaycastAI boDoDuong;
    private EnemyAlertSystem heThongBaoDong;
    private TrangThaiAI trangThaiHienTai = TrangThaiAI.NghiNgoi;

    private Vector3 viTriBanDau;
    private Quaternion gocBanDau;

    private Transform mucTieuNguoiChoiHienTai;
    private Vector3 viTriNguoiChoiCuoiCung;
    private Vector3 diemTimKiemHienTai;
    private bool daCoDiemTimKiem = false;
    private bool biVucThamChanKhongDiSangDuoc = false;
    private float boDemThoiGianNhoDau = 0f;
    private float boDemThoiGianTimKiem = 0f;
    private float boDemTuanTraMatDau = 0f;
    private bool daPhatAmThanhPhatHien = false;
    private float boDemBuocChan = 0f;

    private Vector3 viTriLuuDanNguyHiem;
    private Vector3 diemBoChayLuuDan;
    private bool daCoDiemBoChayLuuDan = false;
    private float boDemThoiGianBoChay = 0f;

    private int chiSoNongBan = 0;
    private int chiSoDiemTuanTra = 0;
    private Vector3 diemDenTuanTraHienTai;
    private Quaternion huongTuanTraHienTai;
    private bool daCoDiemDenTuanTra = false;
    private float boDemThoiGianNghi = 0f;
    private float thoiDiemLoatTiepTheo = 0f;
    private bool dangBanLienThanh = false;

    private bool DangUuTienBoChayLuuDan()
    {
        return trangThaiHienTai == TrangThaiAI.BoChayLuuDan || boDemThoiGianBoChay > 0f;
    }

    private float LayBanKinhTamNhinHienTai()
    {
        bool dangTruyDuoi = trangThaiHienTai == TrangThaiAI.TruyDuoi || boDemThoiGianNhoDau > 0f;
        float heSo = dangTruyDuoi ? Mathf.Max(1f, heSoTangBanKinhTruyDuoi) : 1f;
        return banKinhTamNhin * heSo;
    }

    private float LayGocNhinHienTai()
    {
        bool dangTruyDuoi = trangThaiHienTai == TrangThaiAI.TruyDuoi || boDemThoiGianNhoDau > 0f;
        float heSo = dangTruyDuoi ? Mathf.Max(1f, heSoTangGocTruyDuoi) : 1f;
        return Mathf.Clamp(gocNhin * heSo, 10f, 360f);
    }

    private void LayGocNhinDocHienTai(out float gocTrenHienTai, out float gocDuoiHienTai)
    {
        bool dangTruyDuoi = trangThaiHienTai == TrangThaiAI.TruyDuoi || boDemThoiGianNhoDau > 0f;
        float heSo = dangTruyDuoi ? Mathf.Max(1f, heSoTangGocDocTruyDuoi) : 1f;
        gocTrenHienTai = Mathf.Clamp(gocNhinTren * heSo, 0f, 90f);
        gocDuoiHienTai = Mathf.Clamp(gocNhinDuoi * heSo, 0f, 90f);
    }

    private bool NamTrongVungNhinDoc(Vector3 diemDau, Vector3 diemDich)
    {
        Vector3 delta = diemDich - diemDau;
        Vector3 huongNgang = new Vector3(delta.x, 0f, delta.z);
        float doDaiNgang = huongNgang.magnitude;

        if (doDaiNgang < 0.0001f)
        {
            if (Mathf.Abs(delta.y) < 0.0001f) return true;
            return delta.y > 0f ? gocNhinTren >= 89.9f : gocNhinDuoi >= 89.9f;
        }

        LayGocNhinDocHienTai(out float gocTrenHienTai, out float gocDuoiHienTai);
        float gocDoLech = Mathf.Atan2(delta.y, doDaiNgang) * Mathf.Rad2Deg;

        if (gocDoLech >= 0f)
        {
            return gocDoLech <= gocTrenHienTai;
        }
        else
        {
            return (-gocDoLech) <= gocDuoiHienTai;
        }
    }

    private void Awake()
    {
        viTriBanDau = transform.position;
        gocBanDau = transform.rotation;
        huongTuanTraHienTai = transform.rotation;

        boDoDuong = GetComponent<DoDuongRaycastAI>();
        if (boDoDuong == null)
        {
            boDoDuong = gameObject.AddComponent<DoDuongRaycastAI>();
        }

        heThongBaoDong = GetComponent<EnemyAlertSystem>();

        if (nguonAmThanh == null)
        {
            nguonAmThanh = GetComponent<AudioSource>();
            if (nguonAmThanh == null)
            {
                nguonAmThanh = gameObject.AddComponent<AudioSource>();
                nguonAmThanh.playOnAwake = false;
            }
        }

        if (diemDatMat == null)
        {
            diemDatMat = transform;
        }

        diemDenTuanTraHienTai = viTriBanDau;
        diemTimKiemHienTai = viTriBanDau;
        trangThaiHienTai = laLinhCanh ? TrangThaiAI.NghiNgoi : TrangThaiAI.TuanTra;
    }

    public void NhanBaoDong(Transform mucTieu, Vector3 viTriPlayer)
    {
        if (DangUuTienBoChayLuuDan()) return;

        if (mucTieu != null) mucTieuNguoiChoiHienTai = mucTieu;
        viTriNguoiChoiCuoiCung = viTriPlayer;
        daCoDiemTimKiem = false;
        daCoDiemDenTuanTra = false;
        boDemThoiGianNhoDau = thoiGianNhoDau;
        boDemThoiGianTimKiem = thoiGianTimKiemKhiMatDau;
        boDemTuanTraMatDau = thoiGianTuanTraKhiMatDau;

        if (!daPhatAmThanhPhatHien)
        {
            PhatAmThanh(amThanhPhatHien);
            daPhatAmThanhPhatHien = true;
        }

        if (trangThaiHienTai != TrangThaiAI.TanCong)
        {
            trangThaiHienTai = TrangThaiAI.TruyDuoi;
            DiChuyenToiViTri(viTriPlayer, tocDoTruyDuoi);
        }
    }

    public void NhanBaoDongLuuDan(Vector3 viTriLuuDan)
    {
        viTriLuuDanNguyHiem = viTriLuuDan;
        boDemThoiGianBoChay = Mathf.Max(0.2f, thoiGianBoChayLuuDan);
        daCoDiemBoChayLuuDan = false;
        dangBanLienThanh = false;
        trangThaiHienTai = TrangThaiAI.BoChayLuuDan;
        ChonDiemBoChayLuuDan();
    }

    private bool CoVatCanCheLuuDanTaiDiem(Vector3 diemTron)
    {
        Vector3 diemBatDau = viTriLuuDanNguyHiem + Vector3.up * 0.85f;
        Vector3 diemKetThuc = diemTron + Vector3.up * 0.85f;
        return BiVatCanChan(diemBatDau, diemKetThuc);
    }

    private void ChonDiemBoChayLuuDan()
    {
        float banKinhTim = Mathf.Max(4f, banKinhTimChoTronLuuDan);
        float khoangCachAnToanVatCan = Mathf.Max(0.8f, khoangCachAnSauVatCan);
        float khoangCachHienTaiToiLuuDan = Vector3.Distance(transform.position, viTriLuuDanNguyHiem);

        if (!string.IsNullOrEmpty(tagVatCan) && tagVatCan != "Untagged")
        {
            Collider[] cacVatCan = Physics.OverlapSphere(transform.position, banKinhTim, ~0, QueryTriggerInteraction.Ignore);
            float diemTotNhat = float.MinValue;
            Vector3 choTronTotNhat = Vector3.zero;
            bool timThayChoTron = false;

            if (cacVatCan != null)
            {
                for (int i = 0; i < cacVatCan.Length; i++)
                {
                    Collider col = cacVatCan[i];
                    if (col == null) continue;
                    if (!CoTagVatCan(col.transform)) continue;

                    Vector3 tamVatCan = col.bounds.center;
                    Vector3 huongTuLuuDanToiVatCan = tamVatCan - viTriLuuDanNguyHiem;
                    huongTuLuuDanToiVatCan.y = 0f;
                    if (huongTuLuuDanToiVatCan.sqrMagnitude < 0.01f) continue;
                    huongTuLuuDanToiVatCan.Normalize();

                    float banKinhVatCan = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
                    Vector3 diemSauVatCan = tamVatCan + huongTuLuuDanToiVatCan * (banKinhVatCan + khoangCachAnToanVatCan);
                    diemSauVatCan.y = transform.position.y;

                    if (boDoDuong != null && boDoDuong.LaVucThamTheoHuong(diemSauVatCan - transform.position)) continue;
                    if (!CoVatCanCheLuuDanTaiDiem(diemSauVatCan)) continue;

                    float khoangCachXaLuuDan = Vector3.Distance(diemSauVatCan, viTriLuuDanNguyHiem);
                    float khoangCachDiToi = Vector3.Distance(transform.position, diemSauVatCan);
                    float diemDanhGia = khoangCachXaLuuDan * 2.0f - khoangCachDiToi * 0.5f;

                    if (khoangCachXaLuuDan > khoangCachHienTaiToiLuuDan && diemDanhGia > diemTotNhat)
                    {
                        diemTotNhat = diemDanhGia;
                        choTronTotNhat = diemSauVatCan;
                        timThayChoTron = true;
                    }
                }
            }

            if (timThayChoTron)
            {
                diemBoChayLuuDan = choTronTotNhat;
                daCoDiemBoChayLuuDan = true;
                return;
            }
        }

        Vector3 huongChay = transform.position - viTriLuuDanNguyHiem;
        huongChay.y = 0f;
        if (huongChay.sqrMagnitude < 0.001f)
        {
            huongChay = -transform.forward;
            huongChay.y = 0f;
        }
        huongChay.Normalize();

        float khoangCach = Mathf.Max(3.5f, khoangCachBoChayLuuDan);
        diemBoChayLuuDan = transform.position + huongChay * khoangCach;
        daCoDiemBoChayLuuDan = true;
    }

    private void XuLyBoChayLuuDan()
    {
        trangThaiHienTai = TrangThaiAI.BoChayLuuDan;
        boDemThoiGianBoChay -= Time.deltaTime;

        if (boDemThoiGianBoChay <= 0f)
        {
            boDemThoiGianBoChay = 0f;
            daCoDiemBoChayLuuDan = false;
            trangThaiHienTai = mucTieuNguoiChoiHienTai != null
                ? TrangThaiAI.TruyDuoi
                : (laLinhCanh ? TrangThaiAI.VeChoCu : TrangThaiAI.TuanTra);
            return;
        }

        if (!daCoDiemBoChayLuuDan || Vector3.Distance(transform.position, diemBoChayLuuDan) <= 1.1f)
        {
            ChonDiemBoChayLuuDan();
        }

        DiChuyenToiViTri(diemBoChayLuuDan, tocDoBoChayLuuDan);
        XoayMatVeMucTieu(diemBoChayLuuDan);
    }

    private void Update()
    {
        if (DangUuTienBoChayLuuDan())
        {
            XuLyBoChayLuuDan();
            CapNhatAmThanhBuocChan();
            return;
        }

        bool coTheThayNgayBayGio = QuetThayNguoiChoi();

        if (coTheThayNgayBayGio && mucTieuNguoiChoiHienTai != null)
        {
            if (!daPhatAmThanhPhatHien)
            {
                PhatAmThanh(amThanhPhatHien);
                daPhatAmThanhPhatHien = true;
            }
            viTriNguoiChoiCuoiCung = mucTieuNguoiChoiHienTai.position;
            daCoDiemTimKiem = false;
            boDemThoiGianNhoDau = thoiGianNhoDau;
            boDemThoiGianTimKiem = thoiGianTimKiemKhiMatDau;
            boDemTuanTraMatDau = thoiGianTuanTraKhiMatDau;

            if (heThongBaoDong != null)
            {
                heThongBaoDong.PhatBaoDong(mucTieuNguoiChoiHienTai, viTriNguoiChoiCuoiCung);
            }
        }
        else
        {
            biVucThamChanKhongDiSangDuoc = false;

            if (mucTieuNguoiChoiHienTai != null)
            {
                viTriNguoiChoiCuoiCung = mucTieuNguoiChoiHienTai.position;
            }

            if (boDemThoiGianNhoDau > 0f)
            {
                boDemThoiGianNhoDau -= Time.deltaTime;
                if (boDemThoiGianNhoDau <= 0f)
                {
                    mucTieuNguoiChoiHienTai = null;
                    daPhatAmThanhPhatHien = false;
                }
            }
            else
            {
                daPhatAmThanhPhatHien = false;
            }
        }

        CapNhatTrangThai(coTheThayNgayBayGio);
        ThucThiTrangThai(coTheThayNgayBayGio);
        CapNhatAmThanhBuocChan();
    }

    public void NhanSatThuong(float satThuong)
    {
        boDemThoiGianNhoDau = thoiGianNhoDau;
        boDemThoiGianTimKiem = thoiGianTimKiemKhiMatDau;
        boDemTuanTraMatDau = thoiGianTuanTraKhiMatDau;
        daCoDiemTimKiem = false;

        float banKinhQuet = LayBanKinhTamNhinHienTai() * 1.5f;
        Collider[] nguoiChoiGan = Physics.OverlapSphere(transform.position, banKinhQuet, layerNguoiChoi, QueryTriggerInteraction.Ignore);
        if (nguoiChoiGan != null && nguoiChoiGan.Length > 0)
        {
            mucTieuNguoiChoiHienTai = nguoiChoiGan[0].transform;
            viTriNguoiChoiCuoiCung = mucTieuNguoiChoiHienTai.position;
            if (heThongBaoDong != null && !DangUuTienBoChayLuuDan())
            {
                heThongBaoDong.PhatBaoDong(mucTieuNguoiChoiHienTai, viTriNguoiChoiCuoiCung);
            }
        }

        if (DangUuTienBoChayLuuDan()) return;

        if (trangThaiHienTai == TrangThaiAI.NghiNgoi ||
            trangThaiHienTai == TrangThaiAI.VeChoCu ||
            trangThaiHienTai == TrangThaiAI.TuanTra ||
            trangThaiHienTai == TrangThaiAI.TimKiem)
        {
            trangThaiHienTai = TrangThaiAI.TruyDuoi;
        }
    }

    public void TakeDamage(float amount)
    {
        NhanSatThuong(amount);
    }

    private bool BiVatCanChan(Vector3 diemDau, Vector3 diemCuoi)
    {
        if (boDoDuong != null)
        {
            return boDoDuong.BiVatCanChanGiuaHaiDiem(diemDau, diemCuoi);
        }

        if (string.IsNullOrEmpty(tagVatCan) || tagVatCan == "Untagged")
        {
            return false;
        }

        Vector3 huong = diemCuoi - diemDau;
        float khoangCach = huong.magnitude;
        if (khoangCach <= 0.001f) return false;

        RaycastHit[] cacVaCham = Physics.RaycastAll(diemDau, huong / khoangCach, khoangCach, ~0, QueryTriggerInteraction.Ignore);
        if (cacVaCham == null || cacVaCham.Length == 0) return false;

        foreach (RaycastHit hit in cacVaCham)
        {
            if (hit.collider == null) continue;
            if (hit.collider.transform.root == transform.root) continue;
            if (((1 << hit.collider.gameObject.layer) & layerNguoiChoi) != 0) continue;

            if (CoTagVatCan(hit.collider.transform))
            {
                return true;
            }
        }

        return false;
    }

    private bool CoTagVatCan(Transform obj)
    {
        if (string.IsNullOrEmpty(tagVatCan) || tagVatCan == "Untagged") return false;
        Transform hienTai = obj;
        while (hienTai != null)
        {
            if (hienTai.CompareTag(tagVatCan)) return true;
            hienTai = hienTai.parent;
        }
        return false;
    }

    private bool KiemTraBiVucThamNganCachKhongDiSangDuoc(Vector3 viTriPlayer)
    {
        if (!xaDanLienTucKhiBiVucThamChan || boDoDuong == null) return false;
        return boDoDuong.BiVucThamNganCachSangViTri(viTriPlayer);
    }

    private bool QuetThayNguoiChoi()
    {
        float banKinhQuet = LayBanKinhTamNhinHienTai();
        float gocQuet = LayGocNhinHienTai();

        Collider[] cacVaCham = Physics.OverlapSphere(transform.position, banKinhQuet, layerNguoiChoi, QueryTriggerInteraction.Ignore);
        if (cacVaCham == null || cacVaCham.Length == 0)
        {
            biVucThamChanKhongDiSangDuoc = false;
            return false;
        }

        Transform mucTieuTotNhat = null;
        float khoangCachGanNhat = float.MaxValue;
        Vector3 viTriMat = LayViTriMat();

        foreach (Collider vaCham in cacVaCham)
        {
            if (vaCham == null) continue;

            Vector3 tamMucTieu = vaCham.bounds.center;
            Vector3 huongToiMucTieu = tamMucTieu - viTriMat;
            float khoangCach = huongToiMucTieu.magnitude;
            if (khoangCach > banKinhQuet || khoangCach < 0.001f) continue;

            Vector3 huongNgangMucTieu = new Vector3(huongToiMucTieu.x, 0f, huongToiMucTieu.z);
            Vector3 huongNgangNhin = new Vector3(transform.forward.x, 0f, transform.forward.z);
            if (huongNgangMucTieu.sqrMagnitude > 0.0001f && huongNgangNhin.sqrMagnitude > 0.0001f)
            {
                float doLechGocNgang = Vector3.Angle(huongNgangNhin.normalized, huongNgangMucTieu.normalized);
                if (doLechGocNgang > gocQuet * 0.5f) continue;
            }

            if (!NamTrongVungNhinDoc(viTriMat, tamMucTieu)) continue;

            if (BiVatCanChan(viTriMat, tamMucTieu))
            {
                continue;
            }

            if (khoangCach < khoangCachGanNhat)
            {
                khoangCachGanNhat = khoangCach;
                mucTieuTotNhat = vaCham.transform;
            }
        }

        if (mucTieuTotNhat != null)
        {
            mucTieuNguoiChoiHienTai = mucTieuTotNhat;
            biVucThamChanKhongDiSangDuoc = KiemTraBiVucThamNganCachKhongDiSangDuoc(mucTieuTotNhat.position);
            return true;
        }

        biVucThamChanKhongDiSangDuoc = false;
        return false;
    }

    private Vector3 TinhDiemTruyDuoiTrucTiep(bool coTheThayNgayBayGio)
    {
        if (coTheThayNgayBayGio && mucTieuNguoiChoiHienTai != null)
        {
            return mucTieuNguoiChoiHienTai.position;
        }

        if (mucTieuNguoiChoiHienTai != null)
        {
            return mucTieuNguoiChoiHienTai.position;
        }

        return viTriNguoiChoiCuoiCung;
    }

    private void ChonDiemTimKiemGanViTriCuoi()
    {
        float goc = Random.Range(-180f, 180f);
        Vector3 huong = Quaternion.Euler(0f, goc, 0f) * Vector3.forward;
        float banKinh = Random.Range(1.5f, Mathf.Max(2f, banKinhTimKiem));
        diemTimKiemHienTai = viTriNguoiChoiCuoiCung + huong * banKinh;
        daCoDiemTimKiem = true;
    }

    private void XuLyTimKiemKhiMatDau()
    {
        boDemThoiGianTimKiem -= Time.deltaTime;
        if (boDemThoiGianTimKiem <= 0f)
        {
            daCoDiemTimKiem = false;
            if (laLinhCanh)
            {
                trangThaiHienTai = TrangThaiAI.VeChoCu;
            }
            else
            {
                daCoDiemDenTuanTra = false;
                trangThaiHienTai = TrangThaiAI.TuanTra;
            }
            return;
        }

        if (!daCoDiemTimKiem)
        {
            diemTimKiemHienTai = viTriNguoiChoiCuoiCung;
            daCoDiemTimKiem = true;
        }

        DiChuyenToiViTri(diemTimKiemHienTai, tocDoTuanTra);
        XoayMatVeMucTieu(diemTimKiemHienTai);

        if (Vector3.Distance(transform.position, diemTimKiemHienTai) <= 1.2f)
        {
            ChonDiemTimKiemGanViTriCuoi();
        }
    }

    private void CapNhatTrangThai(bool coTheThayNgayBayGio)
    {
        if (DangUuTienBoChayLuuDan())
        {
            trangThaiHienTai = TrangThaiAI.BoChayLuuDan;
            return;
        }

        if (coTheThayNgayBayGio && mucTieuNguoiChoiHienTai != null)
        {
            if (biVucThamChanKhongDiSangDuoc)
            {
                trangThaiHienTai = TrangThaiAI.TanCong;
                return;
            }

            float khoangCachToiNguoiChoi = Vector3.Distance(transform.position, mucTieuNguoiChoiHienTai.position);
            trangThaiHienTai = khoangCachToiNguoiChoi <= tamBanToiDa ? TrangThaiAI.TanCong : TrangThaiAI.TruyDuoi;
            return;
        }

        if (trangThaiHienTai == TrangThaiAI.TruyDuoi || trangThaiHienTai == TrangThaiAI.TanCong)
        {
            if (boDemThoiGianNhoDau > 0f)
            {
                trangThaiHienTai = TrangThaiAI.TruyDuoi;
                return;
            }

            boDemThoiGianTimKiem = Mathf.Max(0.1f, thoiGianTimKiemKhiMatDau);
            boDemTuanTraMatDau = thoiGianTuanTraKhiMatDau;
            daCoDiemTimKiem = false;
            trangThaiHienTai = TrangThaiAI.TimKiem;
            return;
        }

        if (trangThaiHienTai == TrangThaiAI.TimKiem)
        {
            return;
        }

        if (laLinhCanh)
        {
            if (trangThaiHienTai == TrangThaiAI.TuanTra)
            {
                boDemTuanTraMatDau -= Time.deltaTime;
                if (boDemTuanTraMatDau <= 0f)
                {
                    trangThaiHienTai = TrangThaiAI.VeChoCu;
                }
            }
            else if (trangThaiHienTai == TrangThaiAI.VeChoCu)
            {
                if (Vector3.Distance(transform.position, viTriBanDau) <= 0.6f)
                {
                    DungDiChuyen();
                    transform.position = viTriBanDau;
                    transform.rotation = Quaternion.Slerp(transform.rotation, gocBanDau, tocDoXoayHuong * Time.deltaTime);
                    trangThaiHienTai = TrangThaiAI.NghiNgoi;
                }
            }
            return;
        }

        if (trangThaiHienTai == TrangThaiAI.NghiNgoi && boDemThoiGianNghi <= 0f)
        {
            trangThaiHienTai = TrangThaiAI.TuanTra;
            daCoDiemDenTuanTra = false;
        }
    }

    private void ThucThiTrangThai(bool coTheThayNgayBayGio)
    {
        switch (trangThaiHienTai)
        {
            case TrangThaiAI.BoChayLuuDan:
                XuLyBoChayLuuDan();
                break;

            case TrangThaiAI.NghiNgoi:
                DungDiChuyen();
                if (!laLinhCanh)
                {
                    boDemThoiGianNghi -= Time.deltaTime;
                }
                break;

            case TrangThaiAI.TuanTra:
                XuLyTuanTra();
                break;

            case TrangThaiAI.TruyDuoi:
                Vector3 viTriTruyDuoi = TinhDiemTruyDuoiTrucTiep(coTheThayNgayBayGio);
                DiChuyenToiViTri(viTriTruyDuoi, tocDoTruyDuoi);
                XoayMatVeMucTieu(viTriTruyDuoi);
                break;

            case TrangThaiAI.TimKiem:
                XuLyTimKiemKhiMatDau();
                break;

            case TrangThaiAI.TanCong:
                if (mucTieuNguoiChoiHienTai != null)
                {
                    XoayMatVeMucTieu(mucTieuNguoiChoiHienTai.position);

                    if (biVucThamChanKhongDiSangDuoc)
                    {
                        DungDiChuyen();
                    }
                    else
                    {
                        DuyTriKhoangCachTamXa(mucTieuNguoiChoiHienTai.position);
                    }
                }
                else
                {
                    DungDiChuyen();
                }

                if (coTheThayNgayBayGio && mucTieuNguoiChoiHienTai != null && !dangBanLienThanh && Time.time >= thoiDiemLoatTiepTheo)
                {
                    StartCoroutine(ThucHienBanLienThanh());
                }
                break;

            case TrangThaiAI.VeChoCu:
                DiChuyenToiViTri(viTriBanDau, tocDoTuanTra);
                XoayMatVeMucTieu(viTriBanDau);
                break;
        }
    }

    private IEnumerator ThucHienBanLienThanh()
    {
        dangBanLienThanh = true;
        int soVien = banLienThanh ? Mathf.Max(1, soVienLienThanh) : 1;
        float delayGiuaVien = banLienThanh ? Mathf.Max(0.02f, khoangCachGiuaCacVien) : 0f;

        for (int i = 0; i < soVien; i++)
        {
            if (DangUuTienBoChayLuuDan()) break;
            if (mucTieuNguoiChoiHienTai == null) break;

            Collider vaChamMucTieu = mucTieuNguoiChoiHienTai.GetComponent<Collider>();
            Vector3 diemNgam = vaChamMucTieu != null
                ? vaChamMucTieu.bounds.center
                : mucTieuNguoiChoiHienTai.position + Vector3.up * 1.1f;

            if (BiVatCanChan(LayViTriMat(), diemNgam))
            {
                break;
            }

            BanMotLoatCacNong(diemNgam);

            if (i < soVien - 1 && delayGiuaVien > 0f)
            {
                yield return new WaitForSeconds(delayGiuaVien);
            }
        }

        thoiDiemLoatTiepTheo = biVucThamChanKhongDiSangDuoc
            ? Time.time + Mathf.Max(0.05f, khoangCachGiuaCacVien)
            : Time.time + thoiGianNghiGiuaLoat;
        dangBanLienThanh = false;
    }

    private void DuyTriKhoangCachTamXa(Vector3 viTriMucTieu)
    {
        Vector3 huongToiMucTieu = viTriMucTieu - transform.position;
        huongToiMucTieu.y = 0f;
        float khoangCach = huongToiMucTieu.magnitude;
        if (khoangCach < 0.001f) return;

        Vector3 donViHuong = huongToiMucTieu / khoangCach;

        if (khoangCach < khoangCachToiThieu)
        {
            Vector3 viTriLui = transform.position - donViHuong * (khoangCachLyTuong - khoangCach + 1.5f);
            DiChuyenToiViTri(viTriLui, tocDoTruyDuoi);
        }
        else if (khoangCach > khoangCachLyTuong + 1.0f)
        {
            Vector3 viTriTien = viTriMucTieu - donViHuong * khoangCachLyTuong;
            DiChuyenToiViTri(viTriTien, tocDoTuanTra);
        }
        else
        {
            DungDiChuyen();
        }
    }

    private void BanMotLoatCacNong(Vector3 diemNgam)
    {
        if (diemBanDan != null && diemBanDan.Length > 0)
        {
            if (banDongLoatTatCaNong)
            {
                for (int i = 0; i < diemBanDan.Length; i++)
                {
                    if (diemBanDan[i] != null)
                    {
                        BanTuMotNong(diemBanDan[i].position, diemNgam);
                    }
                }
            }
            else
            {
                int tries = diemBanDan.Length;
                while (tries-- > 0)
                {
                    Transform nong = diemBanDan[chiSoNongBan % diemBanDan.Length];
                    chiSoNongBan = (chiSoNongBan + 1) % diemBanDan.Length;
                    if (nong != null)
                    {
                        BanTuMotNong(nong.position, diemNgam);
                        break;
                    }
                }
            }
        }
        else
        {
            BanTuMotNong(LayViTriMat(), diemNgam);
        }
    }

    private void BanTuMotNong(Vector3 viTriXuatPhat, Vector3 diemNgam)
    {
        Vector3 huongNgam = (diemNgam - viTriXuatPhat).normalized;

        if (prefabDanTamXa != null)
        {
            GameObject vienDan = Instantiate(prefabDanTamXa, viTriXuatPhat, Quaternion.LookRotation(huongNgam));
            Rigidbody rb = vienDan.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = huongNgam * tocDoDan;
            }
            vienDan.SendMessage("SetDamage", satThuongTamXa, SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            RaycastHit[] cacVaCham = Physics.RaycastAll(viTriXuatPhat, huongNgam, tamBanToiDa, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(cacVaCham, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in cacVaCham)
            {
                if (hit.collider == null) continue;
                if (hit.collider.transform.root == transform.root) continue;

                if (CoTagVatCan(hit.collider.transform))
                {
                    break;
                }

                if (((1 << hit.collider.gameObject.layer) & layerNguoiChoi) != 0)
                {
                    hit.collider.SendMessage("TakeDamage", satThuongTamXa, SendMessageOptions.DontRequireReceiver);
                    break;
                }
            }
        }
    }

    private void XuLyTuanTra()
    {
        Vector3 tamTuanTra = laLinhCanh ? viTriBanDau : transform.position;

        if (!laLinhCanh && cacDiemTuanTra != null && cacDiemTuanTra.Length > 0)
        {
            Transform diemMoc = cacDiemTuanTra[chiSoDiemTuanTra % cacDiemTuanTra.Length];
            if (diemMoc != null)
            {
                DiChuyenToiViTri(diemMoc.position, tocDoTuanTra);
                XoayMatVeHuongDiChuyen(diemMoc.position);

                if (Vector3.Distance(transform.position, diemMoc.position) <= 1.1f)
                {
                    chiSoDiemTuanTra = (chiSoDiemTuanTra + 1) % cacDiemTuanTra.Length;
                    boDemThoiGianNghi = thoiGianNghi;
                    trangThaiHienTai = TrangThaiAI.NghiNgoi;
                }
            }
            return;
        }

        if (!daCoDiemDenTuanTra)
        {
            ChonHuongVaDiemTuanTraMoi(tamTuanTra);
        }

        DiChuyenToiViTri(diemDenTuanTraHienTai, tocDoTuanTra);
        XoayMatVeHuongDiChuyen(diemDenTuanTraHienTai);

        if (Vector3.Distance(transform.position, diemDenTuanTraHienTai) <= 1.1f)
        {
            daCoDiemDenTuanTra = false;
            if (!laLinhCanh)
            {
                boDemThoiGianNghi = thoiGianNghi;
                trangThaiHienTai = TrangThaiAI.NghiNgoi;
            }
        }
    }

    private void ChonHuongVaDiemTuanTraMoi(Vector3 tamTuanTra)
    {
        float gocNgauNhien = Random.Range(-180f, 180f);
        huongTuanTraHienTai = Quaternion.Euler(0f, gocNgauNhien, 0f);
        Vector3 huongDi = huongTuanTraHienTai * Vector3.forward;

        float khoangCach = (cacDiemTuanTra != null && cacDiemTuanTra.Length > 0)
            ? khoangCachBuocTuanTra
            : Random.Range(2.5f, Mathf.Max(3f, banKinhTuanTraNgauNhien));

        diemDenTuanTraHienTai = tamTuanTra + huongDi * khoangCach;
        daCoDiemDenTuanTra = true;
    }

    private void CapNhatAmThanhBuocChan()
    {
        if (amThanhBuocChan == null) return;

        bool dangDiChuyen = boDoDuong != null && boDoDuong.DangDiChuyen;
        if (dangDiChuyen)
        {
            boDemBuocChan -= Time.deltaTime;
            if (boDemBuocChan <= 0f)
            {
                PhatAmThanh(amThanhBuocChan);
                boDemBuocChan = khoangCachBuocChan;
            }
        }
        else
        {
            boDemBuocChan = 0.05f;
        }
    }

    private void PhatAmThanh(AudioClip clip)
    {
        if (clip != null && nguonAmThanh != null)
        {
            nguonAmThanh.PlayOneShot(clip, amLuong);
        }
    }

    private void DiChuyenToiViTri(Vector3 viTriDich, float tocDo)
    {
        if (boDoDuong != null)
        {
            boDoDuong.DiChuyenToiDich(viTriDich, tocDo);
        }
    }

    private void DungDiChuyen()
    {
        if (boDoDuong != null)
        {
            boDoDuong.DungDiChuyen();
        }
    }

    private void XoayMatVeHuongDiChuyen(Vector3 viTriDuPhong)
    {
        Vector3 huong = boDoDuong != null && boDoDuong.DangDiChuyen
            ? boDoDuong.HuongDiChuyenHienTai
            : (viTriDuPhong - transform.position);

        huong.y = 0f;
        if (huong.sqrMagnitude < 0.0001f) return;

        Quaternion gocDich = Quaternion.LookRotation(huong.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, gocDich, tocDoXoayHuong * Time.deltaTime);
    }

    private void XoayMatVeMucTieu(Vector3 viTriMucTieu)
    {
        Vector3 huong = viTriMucTieu - transform.position;
        huong.y = 0f;
        if (huong.sqrMagnitude < 0.0001f) return;

        Quaternion gocDich = Quaternion.LookRotation(huong.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, gocDich, tocDoXoayHuong * Time.deltaTime);
    }

    private Vector3 LayViTriMat()
    {
        return diemDatMat != null ? diemDatMat.position : transform.position + Vector3.up * 1.5f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!hienThiVungNhinGizmos) return;

        Vector3 viTriMat = LayViTriMat();
        float banKinhVe = Application.isPlaying ? LayBanKinhTamNhinHienTai() : banKinhTamNhin;
        float gocVe = Application.isPlaying ? LayGocNhinHienTai() : gocNhin;

        LayGocNhinDocHienTai(out float gocTrenVe, out float gocDuoiVe);
        Vector3 huongNgangVe = new Vector3(transform.forward.x, 0f, transform.forward.z);
        if (huongNgangVe.sqrMagnitude < 0.0001f) huongNgangVe = Vector3.forward;
        huongNgangVe.Normalize();

        Vector3 tiaTren = Quaternion.AngleAxis(-gocTrenVe, transform.right) * huongNgangVe;
        Vector3 tiaDuoi = Quaternion.AngleAxis(gocDuoiVe, transform.right) * huongNgangVe;
        Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.9f);
        Gizmos.DrawRay(viTriMat, tiaTren.normalized * banKinhVe);
        Gizmos.DrawRay(viTriMat, tiaDuoi.normalized * banKinhVe);

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.45f);
        Gizmos.DrawWireSphere(viTriMat, banKinhVe);

        Vector3 bienTrai = Quaternion.Euler(0f, -gocVe * 0.5f, 0f) * transform.forward;
        Vector3 bienPhai = Quaternion.Euler(0f, gocVe * 0.5f, 0f) * transform.forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(viTriMat, bienTrai.normalized * banKinhVe);
        Gizmos.DrawRay(viTriMat, bienPhai.normalized * banKinhVe);

        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.65f);
        Gizmos.DrawWireSphere(transform.position, khoangCachToiThieu);

        Gizmos.color = mauVungTamBan;
        Gizmos.DrawWireSphere(transform.position, tamBanToiDa);

        if (mucTieuNguoiChoiHienTai != null)
        {
            Vector3 tamMucTieu = mucTieuNguoiChoiHienTai.GetComponent<Collider>() != null
                ? mucTieuNguoiChoiHienTai.GetComponent<Collider>().bounds.center
                : mucTieuNguoiChoiHienTai.position + Vector3.up * 1.0f;

            bool biChan = BiVatCanChan(viTriMat, tamMucTieu);
            Gizmos.color = biChan ? Color.red : Color.green;
            Gizmos.DrawLine(viTriMat, tamMucTieu);
        }
    }
}

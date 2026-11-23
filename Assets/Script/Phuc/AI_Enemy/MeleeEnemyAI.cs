using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChonTagCanChienAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ChonTagCanChienAttribute))]
public class ChonTagCanChienDrawer : PropertyDrawer
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
public class MeleeEnemyAI : MonoBehaviour
{
    public enum TrangThaiAI
    {
        NghiNgoi,
        TuanTra,
        TruyDuoi,
        TimKiem,
        TanCong,
        VeChoCu
    }

    [Header("Phát hiện & Tầm nhìn")]
    [Tooltip("Layer của người chơi.")]
    [SerializeField] private LayerMask layerNguoiChoi;

    [Tooltip("Tag của tường / chướng ngại vật che khuất tầm nhìn và kiểm tra tường (chọn từ dropdown Tag).")]
    [ChonTagCanChien]
    [SerializeField] private string tagVatCan = "Untagged";

    [Tooltip("Độ dài tia Raycast kiểm tra tường phía trước theo hướng di chuyển (tăng/giảm tùy ý).")]
    [SerializeField] private float doDaiKiemTraTuong = 2.2f;

    [Tooltip("Bán kính tầm nhìn mặc định của quái.")]
    [SerializeField] private float banKinhTamNhin = 18f;

    [Tooltip("Góc nhìn ngang mặc định (độ) tính từ hướng nhìn phía trước của quái.")]
    [Range(10f, 360f)]
    [SerializeField] private float gocNhin = 110f;

    [Tooltip("Góc nhìn hướng lên trên (độ) tính từ mặt phẳng ngang của mắt quái.")]
    [Range(0f, 90f)]
    [SerializeField] private float gocNhinTren = 35f;

    [Tooltip("Góc nhìn hướng xuống dưới (độ) tính từ mặt phẳng ngang của mắt quái.")]
    [Range(0f, 90f)]
    [SerializeField] private float gocNhinDuoi = 35f;

    [Header("Tăng Vùng Nhìn Khi Truy Đuổi")]
    [Tooltip("Hệ số nhân bán kính tầm nhìn khi đang trong trạng thái Truy Đuổi (ví dụ 1.5 = tăng 50%).")]
    [SerializeField] private float heSoTangBanKinhTruyDuoi = 1.5f;

    [Tooltip("Hệ số nhân góc nhìn ngang khi đang trong trạng thái Truy Đuổi (ví dụ 1.4 = mở rộng góc nhìn).")]
    [SerializeField] private float heSoTangGocTruyDuoi = 1.4f;

    [Tooltip("Hệ số nhân góc nhìn trên/dưới khi đang trong trạng thái Truy Đuổi (ví dụ 1.35 = tăng 35%).")]
    [SerializeField] private float heSoTangGocDocTruyDuoi = 1.35f;

    [Tooltip("Điểm xuất phát tia kiểm tra tầm nhìn (nếu để trống tự tính từ mắt/thân quái).")]
    [SerializeField] private Transform diemDatMat;

    [Tooltip("Thời gian quái tiếp tục nhớ/truy đuổi sau khi bị tường che trước khi mất dấu.")]
    [SerializeField] private float thoiGianNhoDau = 4.5f;

    [Tooltip("Khoảng cách tìm điểm vòng sang hai bên để luồn qua tường khi bị chắn.")]
    [SerializeField] private float khoangCachVongTuong = 4.5f;

    [Header("Tìm kiếm khi mất dấu & Kiểm tra vực thẳm")]
    [Tooltip("Thời gian quái tìm kiếm quanh nơi lần cuối thấy Player trước khi về tuần tra hoặc canh gác (mặc định 10 giây).")]
    [SerializeField] private float thoiGianTimKiemKhiMatDau = 10f;

    [Tooltip("Bán kính quái đảo/tìm kiếm xung quanh vị trí cuối cùng thấy Player.")]
    [SerializeField] private float banKinhTimKiem = 6.5f;

    [Tooltip("Bật/tắt kiểm tra vực thẳm phía trước để quái dừng lại và đổi hướng khi gặp vực.")]
    [SerializeField] private bool kiemTraVucTham = true;

    [Tooltip("Layer của mặt đất / sàn vững chắc dùng để dò vực thẳm.")]
    [SerializeField] private LayerMask layerMatDat = ~0;

    [Tooltip("Khoảng cách dò phía trước chân quái để phát hiện mép vực.")]
    [SerializeField] private float khoangCachDoVucTham = 1.35f;

    [Tooltip("Độ sâu tối đa dò xuống dưới mép trước; sâu hơn mức này được coi là vực thẳm.")]
    [SerializeField] private float doSauVucTham = 2.5f;

    [Tooltip("Nếu bật: khi bị vực thẳm ngăn cách với Player và không tìm thấy đường khác, quái bỏ đi và dù quay lại thấy Player cũng không quan tâm.")]
    [SerializeField] private bool boDiKhiBiVucThamChan = true;

    [Header("Chế độ Lính canh & Tuần tra")]
    [Tooltip("Nếu bật: chỉ đứng yên tại vị trí đặt, thấy Player hoặc bị tấn công mới truy đuổi; mất dấu sẽ tìm kiếm rồi về chỗ cũ canh gác.")]
    [SerializeField] private bool laLinhCanh = false;

    [Tooltip("Thời gian đi tuần tra tìm kiếm sau khi mất dấu Player (dành cho lính canh).")]
    [SerializeField] private float thoiGianTuanTraKhiMatDau = 5f;

    [Tooltip("Danh sách điểm tuần tra (nếu để trống sẽ tự tuần tra quanh vị trí đặt).")]
    [SerializeField] private Transform[] cacDiemTuanTra;

    [Tooltip("Bán kính tuần tra ngẫu nhiên nếu không gán Các Điểm Tuần Tra.")]
    [SerializeField] private float banKinhTuanTraNgauNhien = 8f;

    [Tooltip("Khoảng cách mỗi bước đi tuần tra theo hướng mới.")]
    [SerializeField] private float khoangCachBuocTuanTra = 5f;

    [SerializeField] private float thoiGianNghi = 2f;
    [SerializeField] private float tocDoTuanTra = 3.5f;
    [SerializeField] private float tocDoTruyDuoi = 6.5f;
    [SerializeField] private float tocDoXoayHuong = 10f;

    [Header("Tấn công cận chiến")]
    [Tooltip("Khoảng cách để bắt đầu tấn công cận chiến.")]
    [SerializeField] private float tamDanhCanChien = 2.2f;

    [Tooltip("Thời gian hồi giữa hai đòn đánh cận chiến (giây).")]
    [SerializeField] private float thoiGianHoiDonDanh = 1.25f;

    [Tooltip("Sát thương mỗi đòn cận chiến.")]
    [SerializeField] private float satThuongCanChien = 20f;

    [Tooltip("Bán kính vùng va chạm đòn đánh cận chiến.")]
    [SerializeField] private float banKinhVungDanh = 1.4f;

    [Tooltip("Điểm tâm vùng đánh cận chiến (nếu để trống tự lấy trước mặt quái).")]
    [SerializeField] private Transform diemTamDonDanh;

    [Header("Âm thanh")]
    [SerializeField] private AudioSource nguonAmThanh;
    [SerializeField] private AudioClip amThanhBuocChan;
    [SerializeField] private float khoangCachBuocChan = 0.45f;
    [SerializeField] private AudioClip amThanhPhatHien;
    [Range(0f, 1f)]
    [SerializeField] private float amLuong = 0.9f;

    [Header("Hiển thị vùng nhìn Gizmos")]
    [SerializeField] private bool hienThiVungNhinGizmos = true;
    [SerializeField] private Color mauVungTamNhin = new Color(1f, 0.85f, 0.1f, 0.18f);
    [SerializeField] private Color mauTamDanh = new Color(1f, 0.2f, 0.2f, 0.25f);

    private NavMeshAgent dieuHuongNavMesh;
    private TrangThaiAI trangThaiHienTai = TrangThaiAI.NghiNgoi;

    private Vector3 viTriBanDau;
    private Quaternion gocBanDau;

    private Transform mucTieuNguoiChoiHienTai;
    private Vector3 viTriNguoiChoiCuoiCung;
    private Vector3 diemLuonTuongHienTai;
    private Vector3 diemTimKiemHienTai;
    private bool daCoDiemTimKiem = false;
    private bool daBoDiViVucTham = false;
    private float boDemThoiGianNhoDau = 0f;
    private float boDemThoiGianTimKiem = 0f;
    private float boDemTuanTraMatDau = 0f;
    private bool daPhatAmThanhPhatHien = false;
    private float boDemBuocChan = 0f;

    private int chiSoDiemTuanTra = 0;
    private Vector3 diemDenTuanTraHienTai;
    private Quaternion huongTuanTraHienTai;
    private bool daCoDiemDenTuanTra = false;
    private float boDemThoiGianNghi = 0f;
    private float thoiDiemDanhTiepTheo = 0f;

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

        dieuHuongNavMesh = GetComponent<NavMeshAgent>();
        if (dieuHuongNavMesh != null)
        {
            dieuHuongNavMesh.speed = tocDoTuanTra;
            dieuHuongNavMesh.stoppingDistance = Mathf.Max(0.5f, tamDanhCanChien * 0.8f);
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

        if (diemDatMat == null)
        {
            diemDatMat = transform;
        }

        diemDenTuanTraHienTai = viTriBanDau;
        diemTimKiemHienTai = viTriBanDau;
        trangThaiHienTai = laLinhCanh ? TrangThaiAI.NghiNgoi : TrangThaiAI.TuanTra;
    }

    private void Update()
    {
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
        }
        else
        {
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
        daBoDiViVucTham = false;
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
        }

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

    private bool CoTuongPhiaTruoc(Vector3 huongDiChuyen)
    {
        if (string.IsNullOrEmpty(tagVatCan) || tagVatCan == "Untagged")
        {
            return false;
        }

        float doDaiTia = Mathf.Max(0.1f, doDaiKiemTraTuong);
        Vector3 huongNgang = new Vector3(huongDiChuyen.x, 0f, huongDiChuyen.z);
        if (huongNgang.sqrMagnitude < 0.0001f)
        {
            huongNgang = transform.forward;
            huongNgang.y = 0f;
        }
        if (huongNgang.sqrMagnitude < 0.0001f) return false;
        huongNgang.Normalize();

        Vector3 diemBatDau = transform.position + Vector3.up * 0.85f;
        return BiVatCanChan(diemBatDau, diemBatDau + huongNgang * doDaiTia);
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

    private bool LaVucThamPhiaTruoc(Vector3 huongDiChuyen)
    {
        if (!kiemTraVucTham) return false;

        Vector3 huongNgang = new Vector3(huongDiChuyen.x, 0f, huongDiChuyen.z);
        if (huongNgang.sqrMagnitude < 0.0001f)
        {
            huongNgang = transform.forward;
            huongNgang.y = 0f;
        }
        if (huongNgang.sqrMagnitude < 0.0001f) return false;
        huongNgang.Normalize();

        Vector3 diemDoPhiaTruoc = transform.position + huongNgang * Mathf.Max(0.4f, khoangCachDoVucTham) + Vector3.up * 0.35f;
        float doSauKiemTra = Mathf.Max(0.5f, doSauVucTham) + 0.35f;

        bool coMatDat = Physics.Raycast(
            diemDoPhiaTruoc,
            Vector3.down,
            out _,
            doSauKiemTra,
            layerMatDat,
            QueryTriggerInteraction.Ignore
        );

        if (!coMatDat)
        {
            return true;
        }

        if (dieuHuongNavMesh != null && dieuHuongNavMesh.enabled && dieuHuongNavMesh.isOnNavMesh)
        {
            Vector3 diemTrenNavMesh = transform.position + huongNgang * Mathf.Max(0.4f, khoangCachDoVucTham);
            if (!NavMesh.SamplePosition(diemTrenNavMesh, out _, 0.65f, dieuHuongNavMesh.areaMask))
            {
                return true;
            }
        }

        return false;
    }

    private bool TimHuongAnToanTranhVucTham(Vector3 huongHienTai, out Vector3 huongAnToan)
    {
        Vector3 huongChuan = new Vector3(huongHienTai.x, 0f, huongHienTai.z);
        if (huongChuan.sqrMagnitude < 0.0001f)
        {
            huongChuan = transform.forward;
            huongChuan.y = 0f;
        }
        huongChuan.Normalize();

        float[] cacGocThu = { 90f, -90f, 135f, -135f, 180f, 45f, -45f };
        for (int i = 0; i < cacGocThu.Length; i++)
        {
            Vector3 huongThu = Quaternion.Euler(0f, cacGocThu[i], 0f) * huongChuan;
            if (!LaVucThamPhiaTruoc(huongThu) && !CoTuongPhiaTruoc(huongThu))
            {
                huongAnToan = huongThu.normalized;
                return true;
            }
        }

        huongAnToan = (-huongChuan).normalized;
        return false;
    }

    private bool BiVucThamNganCachVoiPlayer(Vector3 viTriPlayer)
    {
        if (!kiemTraVucTham) return false;

        Vector3 huongToiPlayer = viTriPlayer - transform.position;
        huongToiPlayer.y = 0f;
        if (huongToiPlayer.sqrMagnitude < 0.01f) return false;

        if (!LaVucThamPhiaTruoc(huongToiPlayer))
        {
            return false;
        }

        Vector3 huongChuan = huongToiPlayer.normalized;
        Vector3 huongTrai = Quaternion.Euler(0f, -75f, 0f) * huongChuan;
        Vector3 huongPhai = Quaternion.Euler(0f, 75f, 0f) * huongChuan;

        bool coLoiTrai = !LaVucThamPhiaTruoc(huongTrai);
        bool coLoiPhai = !LaVucThamPhiaTruoc(huongPhai);

        if (coLoiTrai || coLoiPhai)
        {
            return false;
        }

        if (dieuHuongNavMesh != null && dieuHuongNavMesh.enabled && dieuHuongNavMesh.isOnNavMesh)
        {
            NavMeshPath path = new NavMeshPath();
            if (dieuHuongNavMesh.CalculatePath(viTriPlayer, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                return false;
            }
        }

        return true;
    }

    private void KichHoatBoDiViVucTham()
    {
        if (!boDiKhiBiVucThamChan) return;

        daBoDiViVucTham = true;
        mucTieuNguoiChoiHienTai = null;
        daCoDiemTimKiem = false;
        boDemThoiGianNhoDau = 0f;
        boDemThoiGianTimKiem = 0f;
        daPhatAmThanhPhatHien = false;

        if (laLinhCanh)
        {
            trangThaiHienTai = TrangThaiAI.VeChoCu;
        }
        else
        {
            daCoDiemDenTuanTra = false;
            trangThaiHienTai = TrangThaiAI.TuanTra;
        }
    }

    private bool QuetThayNguoiChoi()
    {
        if (daBoDiViVucTham)
        {
            mucTieuNguoiChoiHienTai = null;
            return false;
        }

        float banKinhQuet = LayBanKinhTamNhinHienTai();
        float gocQuet = LayGocNhinHienTai();

        Collider[] cacVaCham = Physics.OverlapSphere(transform.position, banKinhQuet, layerNguoiChoi, QueryTriggerInteraction.Ignore);
        if (cacVaCham == null || cacVaCham.Length == 0)
        {
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
            if (BiVucThamNganCachVoiPlayer(mucTieuTotNhat.position))
            {
                KichHoatBoDiViVucTham();
                return false;
            }

            mucTieuNguoiChoiHienTai = mucTieuTotNhat;
            return true;
        }

        return false;
    }

    // Khi đang truy đuổi: không di chuyển ngẫu nhiên/vòng ngẫu nhiên, chỉ dí thẳng theo người chơi cho tới khi mất dấu
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
        Vector3 diemUngVien = viTriNguoiChoiCuoiCung + huong * banKinh;

        if (dieuHuongNavMesh != null && dieuHuongNavMesh.enabled && dieuHuongNavMesh.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(diemUngVien, out NavMeshHit navHit, 2.5f, dieuHuongNavMesh.areaMask))
            {
                diemTimKiemHienTai = navHit.position;
                daCoDiemTimKiem = true;
                return;
            }
        }

        diemTimKiemHienTai = diemUngVien;
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

        DiChuyenToiViTri(diemTimKiemHienTai);
        XoayMatVeMucTieu(diemTimKiemHienTai);

        if (Vector3.Distance(transform.position, diemTimKiemHienTai) <= 1.2f)
        {
            ChonDiemTimKiemGanViTriCuoi();
        }
    }

    private void CapNhatTrangThai(bool coTheThayNgayBayGio)
    {
        if (daBoDiViVucTham)
        {
            if (laLinhCanh)
            {
                if (trangThaiHienTai == TrangThaiAI.VeChoCu)
                {
                    if (Vector3.Distance(transform.position, viTriBanDau) <= 0.6f)
                    {
                        DungDiChuyen();
                        transform.position = viTriBanDau;
                        transform.rotation = Quaternion.Slerp(transform.rotation, gocBanDau, tocDoXoayHuong * Time.deltaTime);
                        trangThaiHienTai = TrangThaiAI.NghiNgoi;
                    }
                }
                else if (trangThaiHienTai != TrangThaiAI.NghiNgoi)
                {
                    trangThaiHienTai = TrangThaiAI.VeChoCu;
                }
            }
            else if (trangThaiHienTai != TrangThaiAI.TuanTra && trangThaiHienTai != TrangThaiAI.NghiNgoi)
            {
                daCoDiemDenTuanTra = false;
                trangThaiHienTai = TrangThaiAI.TuanTra;
            }
            else if (trangThaiHienTai == TrangThaiAI.NghiNgoi && boDemThoiGianNghi <= 0f)
            {
                daCoDiemDenTuanTra = false;
                trangThaiHienTai = TrangThaiAI.TuanTra;
            }
            return;
        }

        if (coTheThayNgayBayGio && mucTieuNguoiChoiHienTai != null)
        {
            float khoangCachToiNguoiChoi = Vector3.Distance(transform.position, mucTieuNguoiChoiHienTai.position);
            trangThaiHienTai = khoangCachToiNguoiChoi <= tamDanhCanChien ? TrangThaiAI.TanCong : TrangThaiAI.TruyDuoi;
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
            case TrangThaiAI.NghiNgoi:
                DungDiChuyen();
                if (!laLinhCanh)
                {
                    boDemThoiGianNghi -= Time.deltaTime;
                }
                break;

            case TrangThaiAI.TuanTra:
                DatTocDoDiChuyen(tocDoTuanTra);
                XuLyTuanTra();
                break;

            case TrangThaiAI.TruyDuoi:
                DatTocDoDiChuyen(tocDoTruyDuoi);
                Vector3 viTriTruyDuoi = TinhDiemTruyDuoiTrucTiep(coTheThayNgayBayGio);
                DiChuyenToiViTri(viTriTruyDuoi);
                XoayMatVeMucTieu(viTriTruyDuoi);
                break;

            case TrangThaiAI.TimKiem:
                DatTocDoDiChuyen(tocDoTuanTra);
                XuLyTimKiemKhiMatDau();
                break;

            case TrangThaiAI.TanCong:
                DungDiChuyen();
                if (mucTieuNguoiChoiHienTai != null)
                {
                    XoayMatVeMucTieu(mucTieuNguoiChoiHienTai.position);
                }

                if (coTheThayNgayBayGio && mucTieuNguoiChoiHienTai != null && Time.time >= thoiDiemDanhTiepTheo)
                {
                    ThucHienDanhCanChien();
                    thoiDiemDanhTiepTheo = Time.time + thoiGianHoiDonDanh;
                }
                break;

            case TrangThaiAI.VeChoCu:
                DatTocDoDiChuyen(tocDoTuanTra);
                DiChuyenToiViTri(viTriBanDau);
                XoayMatVeMucTieu(viTriBanDau);
                break;
        }
    }

    private void ChonHuongVaDiemTuanTraMoi(Vector3 tamTuanTra)
    {
        float gocNgauNhien = Random.Range(-180f, 180f);
        huongTuanTraHienTai = Quaternion.Euler(0f, gocNgauNhien, 0f);
        Vector3 huongDi = huongTuanTraHienTai * Vector3.forward;

        if (LaVucThamPhiaTruoc(huongDi) || CoTuongPhiaTruoc(huongDi))
        {
            TimHuongAnToanTranhVucTham(huongDi, out huongDi);
            huongTuanTraHienTai = Quaternion.LookRotation(huongDi, Vector3.up);
        }

        float khoangCach = (cacDiemTuanTra != null && cacDiemTuanTra.Length > 0)
            ? khoangCachBuocTuanTra
            : Random.Range(2.5f, Mathf.Max(3f, banKinhTuanTraNgauNhien));

        diemDenTuanTraHienTai = tamTuanTra + huongDi * khoangCach;
        daCoDiemDenTuanTra = true;
    }

    private void XuLyTuanTra()
    {
        Vector3 tamTuanTra = laLinhCanh ? viTriBanDau : transform.position;

        if (!laLinhCanh && cacDiemTuanTra != null && cacDiemTuanTra.Length > 0)
        {
            Transform diemMoc = cacDiemTuanTra[chiSoDiemTuanTra % cacDiemTuanTra.Length];
            if (diemMoc != null)
            {
                Vector3 huongMoc = diemMoc.position - transform.position;
                huongMoc.y = 0f;
                if (huongMoc.sqrMagnitude > 0.01f)
                {
                    huongTuanTraHienTai = Quaternion.LookRotation(huongMoc.normalized, Vector3.up);
                }

                transform.rotation = Quaternion.Slerp(transform.rotation, huongTuanTraHienTai, tocDoXoayHuong * Time.deltaTime);
                DiChuyenToiViTri(diemMoc.position);

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

        transform.rotation = Quaternion.Slerp(transform.rotation, huongTuanTraHienTai, tocDoXoayHuong * Time.deltaTime);
        DiChuyenToiViTri(diemDenTuanTraHienTai);

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

    private void ThucHienDanhCanChien()
    {
        Vector3 tamVungDanh = diemTamDonDanh != null
            ? diemTamDonDanh.position
            : transform.position + transform.forward * Mathf.Max(0.8f, tamDanhCanChien * 0.65f) + Vector3.up * 1.0f;

        Collider[] cacMucTieuTrungDon = Physics.OverlapSphere(tamVungDanh, banKinhVungDanh, layerNguoiChoi, QueryTriggerInteraction.Ignore);
        foreach (Collider mucTieu in cacMucTieuTrungDon)
        {
            if (mucTieu == null) continue;
            if (BiVatCanChan(LayViTriMat(), mucTieu.bounds.center))
            {
                continue;
            }

            mucTieu.SendMessage("TakeDamage", satThuongCanChien, SendMessageOptions.DontRequireReceiver);
        }
    }

    private void CapNhatAmThanhBuocChan()
    {
        if (amThanhBuocChan == null) return;

        bool dangDiChuyen = false;
        if (dieuHuongNavMesh != null && dieuHuongNavMesh.enabled && dieuHuongNavMesh.isOnNavMesh)
        {
            dangDiChuyen = !dieuHuongNavMesh.isStopped && dieuHuongNavMesh.velocity.magnitude > 0.25f;
        }
        else
        {
            dangDiChuyen = trangThaiHienTai == TrangThaiAI.TuanTra ||
                           trangThaiHienTai == TrangThaiAI.TruyDuoi ||
                           trangThaiHienTai == TrangThaiAI.TimKiem ||
                           trangThaiHienTai == TrangThaiAI.VeChoCu;
        }

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

    private void DiChuyenToiViTri(Vector3 viTriDich)
    {
        Vector3 huongNgang = viTriDich - transform.position;
        huongNgang.y = 0f;

        if (huongNgang.sqrMagnitude > 0.01f && LaVucThamPhiaTruoc(huongNgang))
        {
            DungDiChuyen();

            if (trangThaiHienTai == TrangThaiAI.TruyDuoi && !TimHuongAnToanTranhVucTham(huongNgang, out _))
            {
                KichHoatBoDiViVucTham();
                return;
            }

            TimHuongAnToanTranhVucTham(huongNgang, out Vector3 huongAnToan);
            float khoangCachAnToan = Mathf.Max(2.5f, khoangCachDoVucTham * 2.2f);
            Vector3 diemAnToan = transform.position + huongAnToan * khoangCachAnToan;

            if (trangThaiHienTai == TrangThaiAI.TuanTra)
            {
                diemDenTuanTraHienTai = diemAnToan;
                huongTuanTraHienTai = Quaternion.LookRotation(huongAnToan, Vector3.up);
                daCoDiemDenTuanTra = true;
            }
            else if (trangThaiHienTai == TrangThaiAI.TimKiem)
            {
                diemTimKiemHienTai = diemAnToan;
                daCoDiemTimKiem = true;
            }

            XoayMatVeMucTieu(diemAnToan);
            viTriDich = diemAnToan;
            huongNgang = huongAnToan;
        }
        else if (huongNgang.sqrMagnitude > 0.01f && CoTuongPhiaTruoc(huongNgang))
        {
            // Khi đang truy đuổi: không đổi hướng random/vòng ngẫu nhiên, giữ nguyên hướng tới mục tiêu
            if (trangThaiHienTai != TrangThaiAI.TruyDuoi)
            {
                Vector3 huongChuan = huongNgang.normalized;
                Vector3 huongTrai = Quaternion.Euler(0f, -75f, 0f) * huongChuan;
                Vector3 huongPhai = Quaternion.Euler(0f, 75f, 0f) * huongChuan;
                float khoangCachVong = Mathf.Max(doDaiKiemTraTuong + 1.5f, khoangCachVongTuong * 0.7f);

                Vector3 diemVong = huongPhai;
                if (!CoTuongPhiaTruoc(huongPhai) && !LaVucThamPhiaTruoc(huongPhai))
                {
                    diemVong = transform.position + huongPhai * khoangCachVong;
                }
                else if (!CoTuongPhiaTruoc(huongTrai) && !LaVucThamPhiaTruoc(huongTrai))
                {
                    diemVong = transform.position + huongTrai * khoangCachVong;
                }
                else
                {
                    TimHuongAnToanTranhVucTham(huongChuan, out Vector3 huongThayThe);
                    diemVong = transform.position + huongThayThe * khoangCachVong;
                }

                if (trangThaiHienTai == TrangThaiAI.TuanTra)
                {
                    diemDenTuanTraHienTai = diemVong;
                    daCoDiemDenTuanTra = true;
                }
                else if (trangThaiHienTai == TrangThaiAI.TimKiem)
                {
                    diemTimKiemHienTai = diemVong;
                    daCoDiemTimKiem = true;
                }

                viTriDich = diemVong;
                huongNgang = viTriDich - transform.position;
                huongNgang.y = 0f;
            }
        }

        if (dieuHuongNavMesh != null && dieuHuongNavMesh.enabled && dieuHuongNavMesh.isOnNavMesh)
        {
            dieuHuongNavMesh.isStopped = false;
            dieuHuongNavMesh.SetDestination(viTriDich);
        }
        else
        {
            if (huongNgang.sqrMagnitude > 0.01f)
            {
                float tocDo = dieuHuongNavMesh != null ? dieuHuongNavMesh.speed : tocDoTuanTra;
                transform.position += huongNgang.normalized * (tocDo * Time.deltaTime);
            }
        }
    }

    private void DungDiChuyen()
    {
        if (dieuHuongNavMesh != null && dieuHuongNavMesh.enabled && dieuHuongNavMesh.isOnNavMesh)
        {
            dieuHuongNavMesh.isStopped = true;
        }
    }

    private void DatTocDoDiChuyen(float tocDo)
    {
        if (dieuHuongNavMesh != null)
        {
            dieuHuongNavMesh.speed = tocDo;
        }
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

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.45f);
        Gizmos.DrawWireSphere(viTriMat, banKinhVe);

        Vector3 bienTrai = Quaternion.Euler(0f, -gocVe * 0.5f, 0f) * transform.forward;
        Vector3 bienPhai = Quaternion.Euler(0f, gocVe * 0.5f, 0f) * transform.forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(viTriMat, bienTrai.normalized * banKinhVe);
        Gizmos.DrawRay(viTriMat, bienPhai.normalized * banKinhVe);

        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.95f);
        Gizmos.DrawRay(transform.position + Vector3.up * 0.85f, transform.forward * Mathf.Max(0.1f, doDaiKiemTraTuong));

        Gizmos.color = mauVungTamNhin;
        Gizmos.DrawSphere(transform.position, banKinhVe * 0.05f);

        Gizmos.color = mauTamDanh;
        Gizmos.DrawWireSphere(transform.position, tamDanhCanChien);

        Vector3 tamVungDanh = diemTamDonDanh != null
            ? diemTamDonDanh.position
            : transform.position + transform.forward * Mathf.Max(0.8f, tamDanhCanChien * 0.65f) + Vector3.up * 1.0f;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(tamVungDanh, banKinhVungDanh);

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
using UnityEngine;
using UnityEngine.AI;

public class EnemyBodyStatusEffect : MonoBehaviour
{
    [Header("Tham Chiếu Model & Thành Phần Quái")]
    [Tooltip("Transform phần mesh/visual của quái để nghiêng người khi choáng (nếu để trống sẽ tự tìm object con có Renderer).")]
    [SerializeField] private Transform gocModelHienThi;

    [Tooltip("Tốc độ nội suy nghiêng người khi vào choáng và đứng thẳng lại khi hết choáng.")]
    [SerializeField] private float tocDoXoayMuot = 12f;

    [Tooltip("Góc nghiêng người về phía trước khi bị choáng (đơn vị độ).")]
    [SerializeField] private float gocNghiengKhiChoang = 22f;

    private NavMeshAgent dieuHuongNavMesh;
    private RangedEnemyAI aiDanhXa;
    private MeleeEnemyAI aiCanChien;
    private HealthSystem heThongMau;

    // Lưu tốc độ di chuyển gốc ban đầu để khôi phục chính xác, tránh bị cộng dồn sai lệch
    private float tocDoNavMeshGoc;
    private bool daLuuTocDoGoc;

    // Biến quản lý trạng thái làm chậm
    private float thoiGianConLaiLamCham;
    private float heSoTocDoHienTai = 1f;

    // Biến quản lý trạng thái choáng
    private float thoiGianConLaiChoang;
    private bool dangBiChoang;

    // Lưu trạng thái bật/tắt AI trước khi bị choáng để khôi phục đúng trạng thái cũ
    private bool trangThaiBatAIDanhXaTruocDo;
    private bool trangThaiBatAICanChienTruocDo;
    private bool trangThaiDungNavMeshTruocDo;
    private bool daLuuTrangThaiDieuKhien;

    public bool DangBiChoang => dangBiChoang;
    public bool DangBiLamCham => thoiGianConLaiLamCham > 0f;
    public float HeSoTocDoHienTai => dangBiChoang ? 0f : heSoTocDoHienTai;

    private void Awake()
    {
        dieuHuongNavMesh = GetComponent<NavMeshAgent>();
        aiDanhXa = GetComponent<RangedEnemyAI>();
        aiCanChien = GetComponent<MeleeEnemyAI>();
        heThongMau = GetComponent<HealthSystem>();

        // Tự tìm object con chứa Renderer làm model hiển thị nếu người dùng chưa gán tay
        if (gocModelHienThi == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform con = transform.GetChild(i);
                if (con != null && con.GetComponentInChildren<Renderer>() != null)
                {
                    gocModelHienThi = con;
                    break;
                }
            }
        }

        LuuTocDoGocNeuChuaCo();
    }

    // Lưu tốc độ gốc một lần duy nhất lúc khởi tạo để đảm bảo mọi máy tính toán đồng nhất
    private void LuuTocDoGocNeuChuaCo()
    {
        if (dieuHuongNavMesh != null && !daLuuTocDoGoc)
        {
            tocDoNavMeshGoc = Mathf.Max(0f, dieuHuongNavMesh.speed);
            daLuuTocDoGoc = true;
        }
    }

    /// <summary>
    /// Áp dụng hiệu ứng làm chậm ngay lập tức (lấy mức chậm nặng hơn và thời gian dài hơn).
    /// </summary>
    public void ApDungLamCham(float heSoTocDo, float thoiGian)
    {
        if (heThongMau != null && heThongMau.DaChet) return;

        LuuTocDoGocNeuChuaCo();

        float heSoAnToan = Mathf.Clamp(heSoTocDo, 0.05f, 1f);
        float thoiGianAnToan = Mathf.Max(0.1f, thoiGian);

        // Nếu đang bị làm chậm rồi thì giữ hệ số giảm tốc mạnh hơn (nhỏ hơn) và kéo dài thời gian nếu lâu hơn
        heSoTocDoHienTai = Mathf.Min(heSoTocDoHienTai, heSoAnToan);
        thoiGianConLaiLamCham = Mathf.Max(thoiGianConLaiLamCham, thoiGianAnToan);

        CapNhatTocDoDiChuyen();
    }

    /// <summary>
    /// Áp dụng hiệu ứng choáng ngay lập tức khi được gọi (không cần cộng dồn nhiều viên đạn).
    /// </summary>
    public void ApDungChoang(float thoiGian)
    {
        if (heThongMau != null && heThongMau.DaChet) return;

        LuuTocDoGocNeuChuaCo();

        float thoiGianAnToan = Mathf.Max(0.1f, thoiGian);

        // Cộng dồn/làm mới thời gian choáng theo mức lớn nhất để tránh bị giật thời gian ngắn lại
        thoiGianConLaiChoang = Mathf.Max(thoiGianConLaiChoang, thoiGianAnToan);

        if (!dangBiChoang)
        {
            dangBiChoang = true;
            LuuVaTatAIHienTai();

            // Dừng hẳn quán tính và di chuyển trên NavMesh khi bị choáng
            if (dieuHuongNavMesh != null && dieuHuongNavMesh.enabled && dieuHuongNavMesh.isOnNavMesh)
            {
                dieuHuongNavMesh.velocity = Vector3.zero;
                dieuHuongNavMesh.isStopped = true;
            }

            CapNhatTocDoDiChuyen();
        }
    }

    // Lưu trạng thái cũ rồi tạm tắt AI để quái không tấn công/điều khiển NavMesh khi đang choáng
    private void LuuVaTatAIHienTai()
    {
        if (daLuuTrangThaiDieuKhien) return;
        daLuuTrangThaiDieuKhien = true;

        if (dieuHuongNavMesh != null)
        {
            trangThaiDungNavMeshTruocDo = dieuHuongNavMesh.isStopped;
        }

        if (aiDanhXa != null)
        {
            trangThaiBatAIDanhXaTruocDo = aiDanhXa.enabled;
            aiDanhXa.enabled = false;
        }

        if (aiCanChien != null)
        {
            trangThaiBatAICanChienTruocDo = aiCanChien.enabled;
            aiCanChien.enabled = false;
        }
    }

    // Khôi phục lại trạng thái bật/tắt AI và NavMesh khi hết choáng
    private void KhoiPhucAIHienTai()
    {
        if (!daLuuTrangThaiDieuKhien) return;
        daLuuTrangThaiDieuKhien = false;

        if (aiDanhXa != null)
        {
            aiDanhXa.enabled = trangThaiBatAIDanhXaTruocDo;
        }

        if (aiCanChien != null)
        {
            aiCanChien.enabled = trangThaiBatAICanChienTruocDo;
        }

        if (dieuHuongNavMesh != null && dieuHuongNavMesh.enabled && dieuHuongNavMesh.isOnNavMesh)
        {
            dieuHuongNavMesh.isStopped = trangThaiDungNavMeshTruocDo;
        }

        CapNhatTocDoDiChuyen();
    }

    // Cập nhật tốc độ NavMesh theo trạng thái choáng hoặc làm chậm dựa trên tốc độ gốc đã lưu
    private void CapNhatTocDoDiChuyen()
    {
        if (dieuHuongNavMesh == null || !dieuHuongNavMesh.enabled) return;

        LuuTocDoGocNeuChuaCo();

        if (dangBiChoang)
        {
            dieuHuongNavMesh.speed = 0f;
        }
        else
        {
            dieuHuongNavMesh.speed = tocDoNavMeshGoc * heSoTocDoHienTai;
        }
    }

    private void Update()
    {
        // Giới hạn deltaTime tối đa 0.05s để khi tụt FPS mạnh hoặc chuyển cửa sổ không bị nhảy trạng thái quá đà
        float deltaThoiGian = Mathf.Min(Time.deltaTime, 0.05f);
        if (deltaThoiGian <= 0f) return;

        // Nếu quái chết thì xóa trạng thái hiệu ứng và trả lại góc xoay bình thường
        if (heThongMau != null && heThongMau.DaChet)
        {
            dangBiChoang = false;
            thoiGianConLaiChoang = 0f;
            thoiGianConLaiLamCham = 0f;
            heSoTocDoHienTai = 1f;
            daLuuTrangThaiDieuKhien = false;
            CapNhatGocNghiengKhiChoang(deltaThoiGian);
            return;
        }

        // Đếm ngược thời gian làm chậm
        if (thoiGianConLaiLamCham > 0f)
        {
            thoiGianConLaiLamCham = Mathf.Max(0f, thoiGianConLaiLamCham - deltaThoiGian);
            if (thoiGianConLaiLamCham <= 0f)
            {
                heSoTocDoHienTai = 1f;
            }
        }

        // Đếm ngược thời gian choáng
        if (dangBiChoang)
        {
            thoiGianConLaiChoang -= deltaThoiGian;

            if (dieuHuongNavMesh != null && dieuHuongNavMesh.enabled && dieuHuongNavMesh.isOnNavMesh)
            {
                dieuHuongNavMesh.velocity = Vector3.zero;
                dieuHuongNavMesh.isStopped = true;
            }

            if (thoiGianConLaiChoang <= 0f)
            {
                thoiGianConLaiChoang = 0f;
                dangBiChoang = false;
                KhoiPhucAIHienTai();
            }
        }

        CapNhatTocDoDiChuyen();
        CapNhatGocNghiengKhiChoang(deltaThoiGian);
    }

    // Nội suy góc nghiêng model mượt mà theo thời gian thực để đảm bảo đồng nhất giữa máy mạnh và máy yếu
    private void CapNhatGocNghiengKhiChoang(float deltaThoiGian)
    {
        if (gocModelHienThi == null) return;

        Quaternion gocMucTieu = dangBiChoang
            ? Quaternion.Euler(gocNghiengKhiChoang, 0f, 0f)
            : Quaternion.identity;

        float heSoNoiSuy = 1f - Mathf.Exp(-tocDoXoayMuot * deltaThoiGian);
        gocModelHienThi.localRotation = Quaternion.Slerp(
            gocModelHienThi.localRotation,
            gocMucTieu,
            heSoNoiSuy
        );
    }
}
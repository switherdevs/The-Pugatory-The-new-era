using UnityEngine;

[DisallowMultipleComponent]
public class DoDuongRaycastAI : MonoBehaviour
{
    [Header("1. Thiet Lap Tia Raycast Do Duong & Ne Collider")]
    [Tooltip("Chiều dài tia raycast dò chướng ngại vật phía trước (mét).")]
    [SerializeField] private float doDaiTiaDoDuong = 2.6f;

    [Tooltip("Bán kính SphereCast dò vật cản để quái có độ dày thân, tránh quệt cạnh collider.")]
    [SerializeField] private float banKinhDoVatCan = 0.45f;

    [Tooltip("Tổng góc quạt (độ) tỏa sang trái và phải để bắn tia tìm đường né.")]
    [Range(30f, 360f)]
    [SerializeField] private float gocQuatDoDuong = 170f;

    [Tooltip("Số lượng tia raycast bắn tỏa ra trong vùng quạt (số lẻ sẽ có 1 tia chính giữa).")]
    [Range(3, 25)]
    [SerializeField] private int soLuongTiaQuat = 11;

    [Tooltip("Độ cao điểm bắn tia tính từ chân quái (mét).")]
    [SerializeField] private float doCaoPhatTia = 0.85f;

    [Tooltip("Tốc độ làm mượt hướng né khi gặp vật cản (càng cao càng đổi hướng nhanh nhưng vẫn mượt).")]
    [SerializeField] private float tocDoLamMuonHuong = 10f;

    [Header("2. Loc Layer & Tag Vat Can")]
    [Tooltip("Layer của các Collider vật cản cần né (để 0 nếu muốn quét mọi layer rồi lọc theo tag/bỏ qua Player & bản thân).")]
    [SerializeField] private LayerMask lopVatCan = 0;

    [Tooltip("Nếu đặt Tag vật cản (khác Untagged), chỉ coi các Collider có Tag này là vật cản cần né; để Untagged nếu né mọi Collider khác.")]
    [SerializeField] private string tagVatCan = "Untagged";

    [Tooltip("Layer của người chơi để không coi người chơi là tường chắn khi quái tiến lại gần.")]
    [SerializeField] private LayerMask lopNguoiChoi = 0;

    [Header("3. Kiem Tra Vuc Tham Bang Tia Raycast")]
    [Tooltip("Bật/tắt bắn tia xuống dưới để phát hiện vực thẳm và tự né.")]
    [SerializeField] private bool kiemTraVucTham = true;

    [Tooltip("Layer mặt đất dùng cho tia dò vực thẳm.")]
    [SerializeField] private LayerMask lopMatDat = ~0;

    [Tooltip("Khoảng cách dò vực thẳm phía trước (mét).")]
    [SerializeField] private float khoangCachDoVucTham = 1.35f;

    [Tooltip("Độ sâu tối đa của tia dò vực thẳm (mét).")]
    [SerializeField] private float doSauVucTham = 2.5f;

    [Header("4. Hien Thi Tia Raycast Tren Scene (Gizmos)")]
    [SerializeField] private bool hienThiTiaDoDuong = true;

    private Vector3 huongDiChuyenHienTai = Vector3.zero;
    private bool dangDiChuyenFrameNay = false;
    private float vanTocHienTai = 0f;

    public Vector3 HuongDiChuyenHienTai => huongDiChuyenHienTai;
    public float VanTocHienTai => dangDiChuyenFrameNay ? vanTocHienTai : 0f;
    public bool DangDiChuyen => dangDiChuyenFrameNay && vanTocHienTai > 0.05f;

    private void Awake()
    {
        huongDiChuyenHienTai = transform.forward;
        huongDiChuyenHienTai.y = 0f;
        if (huongDiChuyenHienTai.sqrMagnitude < 0.0001f)
        {
            huongDiChuyenHienTai = Vector3.forward;
        }
        huongDiChuyenHienTai.Normalize();
    }

    private void LateUpdate()
    {
        // Đặt lại cờ mỗi cuối frame để biết chính xác frame tiếp theo quái có di chuyển hay đứng yên
        dangDiChuyenFrameNay = false;
        vanTocHienTai = 0f;
    }

    public void DungDiChuyen()
    {
        dangDiChuyenFrameNay = false;
        vanTocHienTai = 0f;
    }

    // Hàm dò đường chủ đạo: bắn raycast tìm hướng thoáng nhất tới viTriDich và di chuyển mượt
    public void DiChuyenToiDich(Vector3 viTriDich, float tocDo)
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f || tocDo <= 0f)
        {
            DungDiChuyen();
            return;
        }

        Vector3 huongToiDich = viTriDich - transform.position;
        huongToiDich.y = 0f;
        float khoangCachToiDich = huongToiDich.magnitude;

        if (khoangCachToiDich <= 0.05f)
        {
            DungDiChuyen();
            return;
        }

        Vector3 huongMongMuon = huongToiDich / khoangCachToiDich;
        Vector3 huongTotNhat = TimHuongTotNhatBangRaycast(huongMongMuon, khoangCachToiDich);

        float buocNoiSuy = 1f - Mathf.Exp(-Mathf.Max(1f, tocDoLamMuonHuong) * dt);
        if (huongDiChuyenHienTai.sqrMagnitude < 0.0001f)
        {
            huongDiChuyenHienTai = huongTotNhat;
        }
        else
        {
            huongDiChuyenHienTai = Vector3.Slerp(huongDiChuyenHienTai, huongTotNhat, buocNoiSuy).normalized;
        }

        // Kiểm tra lại lần cuối trước khi dịch chuyển để không bước hụt vào vực thẳm
        if (LaVucThamTheoHuong(huongDiChuyenHienTai))
        {
            Vector3 huongThoatVuc = TimHuongThoatVucTham(huongMongMuon);
            huongDiChuyenHienTai = huongThoatVuc;
        }

        float quangDuongBuoc = Mathf.Min(khoangCachToiDich, tocDo * dt);
        transform.position += huongDiChuyenHienTai * quangDuongBuoc;

        dangDiChuyenFrameNay = quangDuongBuoc > 0.0001f;
        vanTocHienTai = dt > 0f ? (quangDuongBuoc / dt) : 0f;
    }

    // Bắn nhiều tia raycast quạt ngang để tìm hướng không vướng collider và không gặp vực thẳm
    private Vector3 TimHuongTotNhatBangRaycast(Vector3 huongMongMuon, float khoangCachToiDich)
    {
        int soTia = Mathf.Max(3, soLuongTiaQuat);
        float tongGoc = Mathf.Clamp(gocQuatDoDuong, 30f, 360f);
        float doDaiKiemTra = Mathf.Clamp(doDaiTiaDoDuong, 0.5f, Mathf.Max(0.8f, khoangCachToiDich + 0.5f));

        Vector3 diemPhatTia = transform.position + Vector3.up * Mathf.Max(0.2f, doCaoPhatTia);

        float diemCaoNhat = float.MinValue;
        Vector3 huongChon = huongMongMuon;

        for (int i = 0; i < soTia; i++)
        {
            float tyLe = soTia == 1 ? 0.5f : (float)i / (soTia - 1);
            float gocLech = Mathf.Lerp(-tongGoc * 0.5f, tongGoc * 0.5f, tyLe);
            Vector3 huongTia = (Quaternion.Euler(0f, gocLech, 0f) * huongMongMuon).normalized;

            float khoangCachThoang = DoKhoangCachThoangCuaTia(diemPhatTia, huongTia, doDaiKiemTra);
            bool coVucTham = LaVucThamTheoHuong(huongTia);

            // Nếu tia gặp vực thẳm thì trừ điểm nặng để quái tự né sang phần đất liền
            float diemVucTham = coVucTham ? -100f : 0f;

            // Ưu tiên: (1) khoảng cách thoáng lớn, (2) gần hướng tới mục tiêu, (3) giữ đà mượt theo hướng hiện tại
            float doGanMucTieu = Vector3.Dot(huongTia, huongMongMuon);
            float doMuonHuong = Vector3.Dot(huongTia, huongDiChuyenHienTai);
            float diem = khoangCachThoang * 2.5f + doGanMucTieu * 3.0f + doMuonHuong * 0.8f + diemVucTham;

            if (diem > diemCaoNhat)
            {
                diemCaoNhat = diem;
                huongChon = huongTia;
            }
        }

        return huongChon;
    }

    // Bắn tia raycast/spherecast kiểm tra va chạm với collider phía trước
    private float DoKhoangCachThoangCuaTia(Vector3 diemBatDau, Vector3 huongTia, float doDaiToiDa)
    {
        int mask = lopVatCan.value != 0 ? lopVatCan.value : ~0;
        float banKinh = Mathf.Max(0.05f, banKinhDoVatCan);

        RaycastHit[] danhSachVaCham = Physics.SphereCastAll(
            diemBatDau,
            banKinh,
            huongTia,
            doDaiToiDa,
            mask,
            QueryTriggerInteraction.Ignore
        );

        if (danhSachVaCham == null || danhSachVaCham.Length == 0)
        {
            return doDaiToiDa;
        }

        float khoangCachGanNhat = doDaiToiDa;

        for (int i = 0; i < danhSachVaCham.Length; i++)
        {
            RaycastHit hit = danhSachVaCham[i];
            if (hit.collider == null) continue;
            if (hit.collider.transform.root == transform.root) continue;

            // Bỏ qua collider của người chơi để quái vẫn tiếp cận được mục tiêu
            if (lopNguoiChoi.value != 0 && ((1 << hit.collider.gameObject.layer) & lopNguoiChoi.value) != 0)
            {
                continue;
            }

            if (!LaColliderVatCanHopLe(hit.collider.transform))
            {
                continue;
            }

            float kc = hit.distance > 0f ? hit.distance : Vector3.Distance(diemBatDau, hit.point);
            if (kc < khoangCachGanNhat)
            {
                khoangCachGanNhat = kc;
            }
        }

        return khoangCachGanNhat;
    }

    private bool LaColliderVatCanHopLe(Transform doiTuong)
    {
        if (string.IsNullOrEmpty(tagVatCan) || tagVatCan == "Untagged")
        {
            return true;
        }

        Transform hienTai = doiTuong;
        while (hienTai != null)
        {
            if (hienTai.CompareTag(tagVatCan))
            {
                return true;
            }
            hienTai = hienTai.parent;
        }

        return false;
    }

    public bool LaVucThamTheoHuong(Vector3 huong)
    {
        if (!kiemTraVucTham) return false;

        Vector3 huongNgang = new Vector3(huong.x, 0f, huong.z);
        if (huongNgang.sqrMagnitude < 0.0001f)
        {
            huongNgang = transform.forward;
            huongNgang.y = 0f;
        }
        if (huongNgang.sqrMagnitude < 0.0001f) return false;
        huongNgang.Normalize();

        Vector3 diemDo = transform.position + huongNgang * Mathf.Max(0.4f, khoangCachDoVucTham) + Vector3.up * 0.35f;
        float doSau = Mathf.Max(0.5f, doSauVucTham) + 0.35f;

        return !Physics.Raycast(diemDo, Vector3.down, out _, doSau, lopMatDat, QueryTriggerInteraction.Ignore);
    }

    private Vector3 TimHuongThoatVucTham(Vector3 huongMongMuon)
    {
        float[] cacGocThu = { 75f, -75f, 110f, -110f, 145f, -145f, 180f, 45f, -45f };
        Vector3 diemPhatTia = transform.position + Vector3.up * Mathf.Max(0.2f, doCaoPhatTia);
        float doDaiKiemTra = Mathf.Max(1.0f, doDaiTiaDoDuong);

        for (int i = 0; i < cacGocThu.Length; i++)
        {
            Vector3 huongThu = (Quaternion.Euler(0f, cacGocThu[i], 0f) * huongMongMuon).normalized;
            if (!LaVucThamTheoHuong(huongThu) && DoKhoangCachThoangCuaTia(diemPhatTia, huongThu, doDaiKiemTra) > 0.6f)
            {
                return huongThu;
            }
        }

        return (-huongMongMuon).normalized;
    }

    // Kiểm tra có vật cản chắn thẳng giữa 2 điểm (dùng cho tầm nhìn / đường đạn)
    public bool BiVatCanChanGiuaHaiDiem(Vector3 diemDau, Vector3 diemCuoi)
    {
        Vector3 huong = diemCuoi - diemDau;
        float khoangCach = huong.magnitude;
        if (khoangCach <= 0.001f) return false;

        int mask = lopVatCan.value != 0 ? lopVatCan.value : ~0;
        RaycastHit[] hits = Physics.RaycastAll(diemDau, huong / khoangCach, khoangCach, mask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;
            if (col.transform.root == transform.root) continue;
            if (lopNguoiChoi.value != 0 && ((1 << col.gameObject.layer) & lopNguoiChoi.value) != 0) continue;

            if (LaColliderVatCanHopLe(col.transform))
            {
                return true;
            }
        }

        return false;
    }

    // Kiểm tra vực thẳm có chặn hoàn toàn đường sang vị trí người chơi hay không
    public bool BiVucThamNganCachSangViTri(Vector3 viTriMucTieu)
    {
        if (!kiemTraVucTham) return false;

        Vector3 huongToiMucTieu = viTriMucTieu - transform.position;
        huongToiMucTieu.y = 0f;
        if (huongToiMucTieu.sqrMagnitude < 0.01f) return false;

        if (!LaVucThamTheoHuong(huongToiMucTieu))
        {
            return false;
        }

        Vector3 huongChuan = huongToiMucTieu.normalized;
        Vector3 huongTrai = Quaternion.Euler(0f, -75f, 0f) * huongChuan;
        Vector3 huongPhai = Quaternion.Euler(0f, 75f, 0f) * huongChuan;

        if (!LaVucThamTheoHuong(huongTrai) || !LaVucThamTheoHuong(huongPhai))
        {
            return false;
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!hienThiTiaDoDuong) return;

        int soTia = Mathf.Max(3, soLuongTiaQuat);
        float tongGoc = Mathf.Clamp(gocQuatDoDuong, 30f, 360f);
        float doDai = Mathf.Max(0.5f, doDaiTiaDoDuong);
        Vector3 diemPhatTia = transform.position + Vector3.up * Mathf.Max(0.2f, doCaoPhatTia);
        Vector3 huongGoc = huongDiChuyenHienTai.sqrMagnitude > 0.001f ? huongDiChuyenHienTai : transform.forward;
        huongGoc.y = 0f;
        huongGoc.Normalize();

        for (int i = 0; i < soTia; i++)
        {
            float tyLe = soTia == 1 ? 0.5f : (float)i / (soTia - 1);
            float gocLech = Mathf.Lerp(-tongGoc * 0.5f, tongGoc * 0.5f, tyLe);
            Vector3 huongTia = (Quaternion.Euler(0f, gocLech, 0f) * huongGoc).normalized;

            float kcThoang = Application.isPlaying
                ? DoKhoangCachThoangCuaTia(diemPhatTia, huongTia, doDai)
                : doDai;

            Gizmos.color = kcThoang < doDai * 0.95f ? Color.red : new Color(0.2f, 1f, 0.45f, 0.75f);
            Gizmos.DrawRay(diemPhatTia, huongTia * kcThoang);
        }

        if (kiemTraVucTham)
        {
            Vector3 diemDoVuc = transform.position + huongGoc * Mathf.Max(0.4f, khoangCachDoVucTham) + Vector3.up * 0.35f;
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.85f);
            Gizmos.DrawRay(diemDoVuc, Vector3.down * (Mathf.Max(0.5f, doSauVucTham) + 0.35f));
        }
    }
}
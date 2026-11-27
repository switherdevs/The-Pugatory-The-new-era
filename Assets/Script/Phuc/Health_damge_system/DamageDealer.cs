using System.Collections.Generic;
using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    public enum PheGaySatThuong
    {
        TuNguoiChoi, // Chỉ gây sát thương cho Quái vật
        TuQuaiVat,   // Chỉ gây sát thương cho Người chơi
        TatCa        // Gây sát thương cho cả hai (bẫy, nổ thùng xăng...)
    }

    [Header("Thiết lập Sát thương")]
    [Tooltip("Xác định nguồn sát thương này thuộc phe nào để không tự bắn trúng bản thân/đồng đội.")]
    [SerializeField] private PheGaySatThuong pheGaySatThuong = PheGaySatThuong.TuQuaiVat;

    [Tooltip("Lượng sát thương gây ra mỗi lần trúng.")]
    [SerializeField] private float satThuong = 15f;

    [Tooltip("Hủy đạn/vật thể ngay sau khi va chạm trúng mục tiêu (bật cho đạn bay, tắt cho bẫy gai/vùng lửa).")]
    [SerializeField] private bool huySauKhiVaCham = true;

    [Tooltip("Thời gian tự hủy nếu đạn bay ra ngoài khoảng không (giây). Đặt <= 0 nếu không tự hủy.")]
    [SerializeField] private float thoiGianTonTaiToiDa = 5f;

    [Tooltip("Thời gian giãn cách giữa 2 lần gây sát thương liên tục nếu mục tiêu đứng trong vùng bẫy (giây).")]
    [SerializeField] private float thoiGianHoiSatThuongLienTuc = 0.5f;

    [Header("Damage Popup Settings (Tại Collider Quái)")]
    [Tooltip("Bật/tắt hiển thị popup sát thương khi trúng quái.")]
    [SerializeField] private bool hienThiDamagePopup = true;

    [Tooltip("Chỉ hiện popup khi trúng Player nếu bật tùy chọn này (mặc định TẮT: không hiện với Player).")]
    [SerializeField] private bool hienThiVoiPlayer = false;

    [Tooltip("Prefab có gắn script DamagePopup3D (TextMeshPro 3D).")]
    [SerializeField] private DamagePopup3D damagePopupPrefab;

    [Tooltip("Độ lệch vị trí cộng thêm tính từ tâm collider của quái.")]
    [SerializeField] private Vector3 popupOffsetFromCollider = new Vector3(0f, 0.25f, 0f);

    [Tooltip("Độ lệch ngẫu nhiên nhẹ quanh tâm collider để tránh các số sát thương đè khít nhau.")]
    [SerializeField] private float popupRandomJitter = 0.15f;

    [Header("Chế độ nhận va chạm (Collision & Trigger)")]
    [Tooltip("Cho phép gây sát thương thông qua OnCollisionEnter / OnCollisionStay (va chạm vật lý cứng).")]
    [SerializeField] private bool gaySatThuongQuaCollision = true;

    [Tooltip("Cho phép gây sát thương thông qua OnTriggerEnter / OnTriggerStay (vùng trigger / đạn trigger).")]
    [SerializeField] private bool gaySatThuongQuaTrigger = true;

    [Tooltip("Cho phép gây sát thương duy trì khi giữ va chạm (OnCollisionStay / OnTriggerStay) nếu không hủy sau va chạm.")]
    [SerializeField] private bool gaySatThuongKhiDangCham = true;

    private Transform chuSoHuu;
    private Collider[] ownColliders;
    private float thoiDiemGaySatThuongTiepTheo = 0f;
    private readonly Dictionary<int, float> thoiDiemTrungTheoMucTieu = new Dictionary<int, float>();
    private bool daBiHuyHoacHoanTat = false;

    private void Awake()
    {
        ownColliders = GetComponentsInChildren<Collider>(true);
    }

    private void Start()
    {
        IgnoreAlliedCollisions();

        if (thoiGianTonTaiToiDa > 0f && huySauKhiVaCham)
        {
            Destroy(gameObject, thoiGianTonTaiToiDa);
        }
    }

    public void SetDamage(float amount)
    {
        satThuong = amount;
    }

    public void SetOwner(Transform ownerRoot)
    {
        chuSoHuu = ownerRoot;
        IgnoreAlliedCollisions();
    }

    private void IgnoreAlliedCollisions()
    {
        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider>(true);
        }
        if (ownColliders == null || ownColliders.Length == 0) return;

        if (chuSoHuu != null)
        {
            Collider[] ownerCols = chuSoHuu.root.GetComponentsInChildren<Collider>(true);
            IgnorePairs(ownColliders, ownerCols);
        }

        if (pheGaySatThuong == PheGaySatThuong.TatCa) return;

        HealthSystem.LoaiDoiTuong pheDongDoi = pheGaySatThuong == PheGaySatThuong.TuQuaiVat
            ? HealthSystem.LoaiDoiTuong.QuaiVat
            : HealthSystem.LoaiDoiTuong.NguoiChoi;

        HealthSystem[] allHealths = FindObjectsByType<HealthSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < allHealths.Length; i++)
        {
            HealthSystem hp = allHealths[i];
            if (hp != null && hp.Phe == pheDongDoi)
            {
                Collider[] allyCols = hp.GetComponentsInChildren<Collider>(true);
                IgnorePairs(ownColliders, allyCols);
            }
        }
    }

    private static void IgnorePairs(Collider[] sourceCols, Collider[] targetCols)
    {
        if (sourceCols == null || targetCols == null) return;
        for (int i = 0; i < sourceCols.Length; i++)
        {
            if (sourceCols[i] == null) continue;
            for (int j = 0; j < targetCols.Length; j++)
            {
                if (targetCols[j] == null) continue;
                Physics.IgnoreCollision(sourceCols[i], targetCols[j], true);
            }
        }
    }

    private bool LaDongDoi(GameObject doiTuongVaCham, HealthSystem heThongMau)
    {
        if (doiTuongVaCham == null) return false;

        if (chuSoHuu != null && doiTuongVaCham.transform.root == chuSoHuu.root)
        {
            return true;
        }

        if (heThongMau != null)
        {
            if (pheGaySatThuong == PheGaySatThuong.TuQuaiVat && heThongMau.Phe == HealthSystem.LoaiDoiTuong.QuaiVat)
            {
                return true;
            }

            if (pheGaySatThuong == PheGaySatThuong.TuNguoiChoi && heThongMau.Phe == HealthSystem.LoaiDoiTuong.NguoiChoi)
            {
                return true;
            }
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!gaySatThuongQuaTrigger || other == null) return;
        XuLyVaCham(other, other.ClosestPoint(transform.position), laVaChamLienTuc: false);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!gaySatThuongQuaTrigger || !gaySatThuongKhiDangCham || huySauKhiVaCham || other == null) return;
        XuLyVaCham(other, other.ClosestPoint(transform.position), laVaChamLienTuc: true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!gaySatThuongQuaCollision || collision == null) return;
        Vector3 diemCham = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        XuLyVaCham(collision.collider, diemCham, laVaChamLienTuc: false);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!gaySatThuongQuaCollision || !gaySatThuongKhiDangCham || huySauKhiVaCham || collision == null) return;
        Vector3 diemCham = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        XuLyVaCham(collision.collider, diemCham, laVaChamLienTuc: true);
    }

    private bool XuLyVaCham(Collider colliderVaCham, Vector3 diemVaCham, bool laVaChamLienTuc)
    {
        if (daBiHuyHoacHoanTat || colliderVaCham == null) return false;

        GameObject doiTuongVaCham = colliderVaCham.gameObject;
        HealthSystem heThongMau = doiTuongVaCham.GetComponentInParent<HealthSystem>();

        if (LaDongDoi(doiTuongVaCham, heThongMau))
        {
            return false;
        }

        bool daGaySatThuong = false;

        if (heThongMau != null)
        {
            if (!CoHopLeDeGaySatThuong(heThongMau))
            {
                return false;
            }

            int targetId = heThongMau.GetInstanceID();
            float thoiGianHoi = Mathf.Max(0.05f, thoiGianHoiSatThuongLienTuc);

            if (laVaChamLienTuc)
            {
                if (Time.time < thoiDiemGaySatThuongTiepTheo)
                {
                    return false;
                }

                if (thoiDiemTrungTheoMucTieu.TryGetValue(targetId, out float nextTargetHitTime) && Time.time < nextTargetHitTime)
                {
                    return false;
                }
            }
            else
            {
                if (thoiDiemTrungTheoMucTieu.TryGetValue(targetId, out float lastHitTime) && Time.time - lastHitTime < 0.02f)
                {
                    return false;
                }
            }

            EnemyBodyPartHitbox vungHitbox = colliderVaCham.GetComponent<EnemyBodyPartHitbox>();
            if (vungHitbox == null)
            {
                vungHitbox = colliderVaCham.GetComponentInParent<EnemyBodyPartHitbox>();
            }

            bool trungDau = vungHitbox != null && vungHitbox.VungCoThe == EnemyBodyPartHitbox.LoaiVungCoThe.Dau;
            float satThuongHienThi = satThuong;

            if (vungHitbox != null)
            {
                // EnemyBodyPartHitbox tự tính hệ số vùng và gọi heThongMau.NhanSatThuong
                float mauTruoc = heThongMau.MauHienTai;
                vungHitbox.NhanDan(satThuong, laDanGayChoang: false, diemVaCham);
                float mauSau = heThongMau.MauHienTai;
                satThuongHienThi = Mathf.Max(0f, mauTruoc - mauSau);
            }
            else
            {
                heThongMau.NhanSatThuong(satThuong);
            }

            HienThiDamagePopupTaiCollider(heThongMau, colliderVaCham, satThuongHienThi, diemVaCham, trungDau);
            daGaySatThuong = true;
            thoiDiemTrungTheoMucTieu[targetId] = Time.time + thoiGianHoi;

            if (laVaChamLienTuc)
            {
                thoiDiemGaySatThuongTiepTheo = Time.time + thoiGianHoi;
            }
        }

        if (huySauKhiVaCham)
        {
            daBiHuyHoacHoanTat = true;
            Destroy(gameObject);
        }

        return daGaySatThuong;
    }

    private void HienThiDamagePopupTaiCollider(
        HealthSystem mucTieu,
        Collider colliderMucTieu,
        float luongSatThuong,
        Vector3 diemChamFallback,
        bool laTrungDau)
    {
        if (!hienThiDamagePopup || damagePopupPrefab == null || mucTieu == null || luongSatThuong <= 0f)
            return;

        if (mucTieu.Phe == HealthSystem.LoaiDoiTuong.NguoiChoi && !hienThiVoiPlayer)
            return;

        Vector3 viTriCollider = colliderMucTieu != null ? colliderMucTieu.bounds.center : diemChamFallback;
        Vector3 jitter = new Vector3(
            Random.Range(-popupRandomJitter, popupRandomJitter),
            Random.Range(-popupRandomJitter * 0.5f, popupRandomJitter),
            Random.Range(-popupRandomJitter, popupRandomJitter)
        );

        Vector3 viTriSpawn = viTriCollider + popupOffsetFromCollider + jitter;

        // Sinh popup độc lập ở root Scene, không gắn làm con của quái hay bất kỳ GameObject nào
        DamagePopup3D popupInstance = Instantiate(damagePopupPrefab, viTriSpawn, Quaternion.identity);
        if (popupInstance != null)
        {
            popupInstance.transform.SetParent(null, true);
            popupInstance.HienThi(luongSatThuong, laTrungDau);
        }
    }

    private bool CoHopLeDeGaySatThuong(HealthSystem mucTieu)
    {
        if (mucTieu.DaChet) return false;

        switch (pheGaySatThuong)
        {
            case PheGaySatThuong.TuNguoiChoi:
                return mucTieu.Phe == HealthSystem.LoaiDoiTuong.QuaiVat;

            case PheGaySatThuong.TuQuaiVat:
                return mucTieu.Phe == HealthSystem.LoaiDoiTuong.NguoiChoi;

            case PheGaySatThuong.TatCa:
            default:
                return true;
        }
    }
}
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DichChuyenNguoiChoiKhiCham : MonoBehaviour
{
    [Header("1. Vị Trí Dịch Chuyển Tới")]
    [Tooltip("Nếu kéo Transform vào đây, người chơi sẽ dịch chuyển tới vị trí của Transform này (ưu tiên hơn Tọa Độ Cố Định).")]
    [SerializeField] private Transform diemDichChuyen;

    [Tooltip("Tọa độ cố định (X, Y, Z) trong World Space nếu không dùng Diem Dich Chuyen.")]
    [SerializeField] private Vector3 viTriCoDinh = Vector3.zero;

    [Tooltip("Độ lệch cộng thêm sau khi dịch chuyển (ví dụ nâng lên Y = 0.2f để không kẹt sàn).")]
    [SerializeField] private Vector3 doLechSauDichChuyen = new Vector3(0f, 0.1f, 0f);

    [Tooltip("Có xoay hướng của người chơi theo hướng của Diem Dich Chuyen không.")]
    [SerializeField] private bool dongBoHuongXoay = false;

    [Header("2. Nhận Diện Người Chơi & Chống Kích Hoạt Trùng")]
    [Tooltip("Layer của người chơi (để 0 nếu muốn nhận diện qua HealthSystem / CharacterController / Tag).")]
    [SerializeField] private LayerMask layerNguoiChoi = 0;

    [Tooltip("Tag của người chơi (để trống hoặc Untagged nếu không bắt buộc theo Tag).")]
    [SerializeField] private string tagNguoiChoi = "Player";

    [Tooltip("Thời gian giãn cách tối thiểu (giây) giữa 2 lần dịch chuyển để tránh kẹt vòng lặp nếu chạm liên tục.")]
    [SerializeField] private float thoiGianHoiDichChuyen = 0.5f;

    [Tooltip("Bật nếu muốn kích hoạt qua OnTriggerEnter (Collider có tick Is Trigger).")]
    [SerializeField] private bool kichHoatQuaTrigger = true;

    [Tooltip("Bật nếu muốn kích hoạt qua OnCollisionEnter (Collider vật lý không tick Is Trigger).")]
    [SerializeField] private bool kichHoatQuaCollision = true;

    [Header("3. Hiệu Ứng Tùy Chọn Khi Dịch Chuyển")]
    [Tooltip("Prefab hiệu ứng spawn tại vị trí người chơi đứng trước khi dịch chuyển (tùy chọn).")]
    [SerializeField] private GameObject hieuUngDiemDi;

    [Tooltip("Prefab hiệu ứng spawn tại vị trí đích sau khi dịch chuyển (tùy chọn).")]
    [SerializeField] private GameObject hieuUngDiemDen;

    [Tooltip("Âm thanh phát khi dịch chuyển (tùy chọn).")]
    [SerializeField] private AudioClip amThanhDichChuyen;

    [Range(0f, 1f)]
    [SerializeField] private float amLuong = 0.9f;

    private float thoiDiemCoTheDichChuyenTiep = 0f;
    private AudioSource nguonAmThanh;

    private void Awake()
    {
        // Đảm bảo Collider gắn trên object này hoạt động đúng theo chế độ Trigger/Collision
        Collider col = GetComponent<Collider>();
        if (col != null && kichHoatQuaTrigger && !col.isTrigger && !kichHoatQuaCollision)
        {
            col.isTrigger = true;
        }

        if (amThanhDichChuyen != null)
        {
            nguonAmThanh = GetComponent<AudioSource>();
            if (nguonAmThanh == null)
            {
                nguonAmThanh = gameObject.AddComponent<AudioSource>();
                nguonAmThanh.playOnAwake = false;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!kichHoatQuaTrigger || other == null) return;
        ThuDichChuyenNguoiChoi(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!kichHoatQuaCollision || collision == null || collision.collider == null) return;
        ThuDichChuyenNguoiChoi(collision.collider.gameObject);
    }

    private void ThuDichChuyenNguoiChoi(GameObject vatVaCham)
    {
        float thoiGianHienTai = Time.time;
        if (thoiGianHienTai < thoiDiemCoTheDichChuyenTiep) return;

        Transform rootNguoiChoi = LayRootNguoiChoi(vatVaCham);
        if (rootNguoiChoi == null) return;

        Vector3 viTriDich = LayViTriDichChuyen();
        Quaternion gocDich = dongBoHuongXoay && diemDichChuyen != null
            ? diemDichChuyen.rotation
            : rootNguoiChoi.rotation;

        if (hieuUngDiemDi != null)
        {
            Instantiate(hieuUngDiemDi, rootNguoiChoi.position, Quaternion.identity);
        }

        DichChuyenAnToan(rootNguoiChoi, viTriDich, gocDich);

        if (hieuUngDiemDen != null)
        {
            Instantiate(hieuUngDiemDen, rootNguoiChoi.position, Quaternion.identity);
        }

        if (amThanhDichChuyen != null && nguonAmThanh != null)
        {
            nguonAmThanh.PlayOneShot(amThanhDichChuyen, amLuong);
        }

        // Đặt cooldown sau khi dịch chuyển để không kích hoạt lại ngay lập tức
        thoiDiemCoTheDichChuyenTiep = Time.time + Mathf.Max(0.1f, thoiGianHoiDichChuyen);
    }

    private Transform LayRootNguoiChoi(GameObject vatVaCham)
    {
        if (vatVaCham == null) return null;

        // 1. Kiểm tra HealthSystem thuộc phe NguoiChoi
        HealthSystem hp = vatVaCham.GetComponentInParent<HealthSystem>();
        if (hp != null && hp.Phe == HealthSystem.LoaiDoiTuong.NguoiChoi)
        {
            return hp.transform.root != null ? hp.transform.root : hp.transform;
        }

        // 2. Kiểm tra các script đặc trưng của người chơi trong dự án
        if (vatVaCham.GetComponentInParent<IronStormMovement>() != null ||
            vatVaCham.GetComponentInParent<SpaceMarineJumpPack>() != null)
        {
            return vatVaCham.transform.root != null ? vatVaCham.transform.root : vatVaCham.transform;
        }

        // 3. Kiểm tra Layer nếu có cấu hình Layer Nguoi Choi
        if (layerNguoiChoi.value != 0)
        {
            if (((1 << vatVaCham.layer) & layerNguoiChoi.value) != 0)
            {
                return vatVaCham.transform.root != null ? vatVaCham.transform.root : vatVaCham.transform;
            }
        }

        // 4. Kiểm tra Tag nếu có cấu hình Tag Nguoi Choi
        if (!string.IsNullOrEmpty(tagNguoiChoi) && tagNguoiChoi != "Untagged")
        {
            Transform hienTai = vatVaCham.transform;
            while (hienTai != null)
            {
                if (hienTai.CompareTag(tagNguoiChoi))
                {
                    return hienTai.root != null ? hienTai.root : hienTai;
                }
                hienTai = hienTai.parent;
            }
        }

        return null;
    }

    private Vector3 LayViTriDichChuyen()
    {
        Vector3 viTriGoc = diemDichChuyen != null ? diemDichChuyen.position : viTriCoDinh;
        return viTriGoc + doLechSauDichChuyen;
    }

    // Dịch chuyển an toàn cho cả CharacterController lẫn Rigidbody
    private void DichChuyenAnToan(Transform rootNguoiChoi, Vector3 viTriDich, Quaternion gocDich)
    {
        CharacterController cc = rootNguoiChoi.GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = rootNguoiChoi.GetComponentInChildren<CharacterController>();
        }

        Rigidbody rb = rootNguoiChoi.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = rootNguoiChoi.GetComponentInChildren<Rigidbody>();
        }

        if (cc != null)
        {
            bool daBatCc = cc.enabled;
            cc.enabled = false;
            rootNguoiChoi.position = viTriDich;
            rootNguoiChoi.rotation = gocDich;
            cc.enabled = daBatCc;
        }
        else
        {
            rootNguoiChoi.position = viTriDich;
            rootNguoiChoi.rotation = gocDich;
        }

        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = viTriDich;
            rb.rotation = gocDich;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 viTriDich = LayViTriDichChuyen();
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, viTriDich);
        Gizmos.DrawWireSphere(viTriDich, 0.45f);
    }
}
using UnityEngine;
using UnityEngine.InputSystem;

// Phan loai loai dan cua hop dan (da loai bo DanLuuDan va DanFlashbang vi luu dan tu hoat dong nhu vu khi + tiep dan)
public enum LoaiDanHopDan
{
    DanVuKhiThuong = 0,       // Cong dan du phong cho WeaponSystem
    DanSungTia = 1,            // Cong dan du phong cho SniperWeapon
    TuDongTheoVuKhiDangCam = 2 // Tu cong theo vu khi dang cam tren tay (khong ap dung cho luu dan)
}

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))] // Dam bao luon co Rigidbody de nhan dien vat ly
public class AmmoBox : MonoBehaviour
{
    [Header("Phan Loai & So Luong Dan")]
    [Tooltip("Chon loai dan cua hop dan nay qua Dropdown.")]
    [SerializeField] private LoaiDanHopDan loaiDan = LoaiDanHopDan.DanVuKhiThuong;

    [Tooltip("So luong dan du phong duoc cong them khi nhat hop dan.")]
    [SerializeField] private int soLuongDanCongThem = 30;

    [Tooltip("Tag cua doi tuong nguoi choi.")]
    [SerializeField] private string tagNguoiChoi = "Player";

    [Header("Che Do Nhat Phim E")]
    [Tooltip("Neu bat, khi o gan hop dan se nhat khi nhan phim E (hoac InputAction nhat); neu tat se nhat ngay khi cham.")]
    [SerializeField] private bool nhatBangPhimE = true;

    [Tooltip("Khoang cach toi da cho phep nhan phim E nhat hop dan.")]
    [SerializeField] private float khoangCachNhatPhimE = 3.5f;

    [Tooltip("Tuy chon InputActionReference neu dung Action Asset rieng; de trong se tu dung phim E.")]
    [SerializeField] private InputActionReference hanhDongNhatPhimE;

    private InputAction hanhDongNhatTamThoi;
    private Collider vaChamHopDan;
    private Rigidbody vatLyHopDan;

    // Cache tham chieu nguoi choi de giam quet scene moi frame
    private Transform cachedPlayerRoot;
    private WeaponSwitcher cachedWeaponSwitcher;

    private InputAction HanhDongNhatDangDung =>
        hanhDongNhatPhimE != null && hanhDongNhatPhimE.action != null
            ? hanhDongNhatPhimE.action
            : hanhDongNhatTamThoi;

    private void Awake()
    {
        vaChamHopDan = GetComponent<Collider>();
        if (vaChamHopDan != null)
        {
            vaChamHopDan.isTrigger = true;
        }

        vatLyHopDan = GetComponent<Rigidbody>();
        if (vatLyHopDan != null)
        {
            vatLyHopDan.isKinematic = false;
        }

        if (hanhDongNhatPhimE == null)
        {
            hanhDongNhatTamThoi = new InputAction("PickupAmmoBox", InputActionType.Button, "<Keyboard>/e");
        }
    }

    private void OnEnable()
    {
        HanhDongNhatDangDung?.Enable();
    }

    private void OnDisable()
    {
        HanhDongNhatDangDung?.Disable();
    }

    private void OnDestroy()
    {
        hanhDongNhatTamThoi?.Dispose();
    }

    private void Update()
    {
        if (!nhatBangPhimE || soLuongDanCongThem <= 0) return;

        InputAction action = HanhDongNhatDangDung;
        if (action == null || !action.WasPressedThisFrame()) return;

        DamBaoCacheNguoiChoi();
        if (cachedPlayerRoot == null) return;

        Transform diemDo = Camera.main != null ? Camera.main.transform : cachedPlayerRoot;
        float khoangCach = Vector3.Distance(diemDo.position, transform.position);
        if (khoangCach > khoangCachNhatPhimE) return;

        if (CongDanTheoLoai(cachedPlayerRoot, cachedWeaponSwitcher))
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (nhatBangPhimE || other == null || soLuongDanCongThem <= 0) return;

        Transform gocNguoiChoi = TimGocNguoiChoi(other);
        if (gocNguoiChoi == null) return;

        WeaponSwitcher boChuyen = gocNguoiChoi.GetComponentInChildren<WeaponSwitcher>(true);
        if (CongDanTheoLoai(gocNguoiChoi, boChuyen))
        {
            Destroy(gameObject);
        }
    }

    private void DamBaoCacheNguoiChoi()
    {
        if (cachedPlayerRoot != null && cachedWeaponSwitcher != null) return;

        if (cachedWeaponSwitcher == null)
        {
            cachedWeaponSwitcher = FindFirstObjectByType<WeaponSwitcher>();
        }

        if (cachedPlayerRoot == null)
        {
            if (cachedWeaponSwitcher != null)
            {
                cachedPlayerRoot = cachedWeaponSwitcher.transform.root;
            }
            else if (Camera.main != null)
            {
                cachedPlayerRoot = Camera.main.transform.root;
            }
        }
    }

    // Xac dinh xem object va cham co thuoc ve nguoi choi / he thong vu khi hay khong
    private Transform TimGocNguoiChoi(Collider vaCham)
    {
        if (vaCham.CompareTag(tagNguoiChoi))
        {
            return vaCham.transform.root;
        }

        if (vaCham.transform.root != null && vaCham.transform.root.CompareTag(tagNguoiChoi))
        {
            return vaCham.transform.root;
        }

        WeaponSwitcher boChuyenCha = vaCham.GetComponentInParent<WeaponSwitcher>();
        if (boChuyenCha != null) return boChuyenCha.transform.root;

        WeaponSwitcher boChuyenCon = vaCham.GetComponentInChildren<WeaponSwitcher>(true);
        if (boChuyenCon != null) return boChuyenCon.transform.root;

        if (vaCham.GetComponentInParent<CharacterController>() != null ||
            vaCham.GetComponentInChildren<CharacterController>(true) != null ||
            vaCham.GetComponentInParent<WeaponSystem>() != null ||
            vaCham.GetComponentInParent<SniperWeapon>() != null)
        {
            return vaCham.transform.root;
        }

        return null;
    }

    // Cong dan dung theo dropdown loaiDan da chon (khong cong luu dan)
    private bool CongDanTheoLoai(Transform gocNguoiChoi, WeaponSwitcher boChuyen)
    {
        if (boChuyen == null && gocNguoiChoi != null)
        {
            boChuyen = gocNguoiChoi.GetComponentInChildren<WeaponSwitcher>(true);
        }

        switch (loaiDan)
        {
            case LoaiDanHopDan.DanVuKhiThuong:
                return CongDanVuKhiThuong(gocNguoiChoi, boChuyen);

            case LoaiDanHopDan.DanSungTia:
                return CongDanSungTia(gocNguoiChoi, boChuyen);

            case LoaiDanHopDan.TuDongTheoVuKhiDangCam:
            default:
                return CongDanTuDongTheoVuKhiDangCam(gocNguoiChoi, boChuyen);
        }
    }

    private bool CongDanVuKhiThuong(Transform gocNguoiChoi, WeaponSwitcher boChuyen)
    {
        if (boChuyen != null && boChuyen.CurrentWeaponSystem != null)
        {
            boChuyen.CurrentWeaponSystem.AddReserveAmmo(soLuongDanCongThem);
            return true;
        }

        WeaponSystem sungThuong = gocNguoiChoi != null ? gocNguoiChoi.GetComponentInChildren<WeaponSystem>(true) : null;
        if (sungThuong != null)
        {
            sungThuong.AddReserveAmmo(soLuongDanCongThem);
            return true;
        }

        return false;
    }

    private bool CongDanSungTia(Transform gocNguoiChoi, WeaponSwitcher boChuyen)
    {
        if (boChuyen != null && boChuyen.CurrentSniperWeapon != null)
        {
            boChuyen.CurrentSniperWeapon.AddReserveAmmo(soLuongDanCongThem);
            return true;
        }

        SniperWeapon sungTia = gocNguoiChoi != null ? gocNguoiChoi.GetComponentInChildren<SniperWeapon>(true) : null;
        if (sungTia != null)
        {
            sungTia.AddReserveAmmo(soLuongDanCongThem);
            return true;
        }

        return false;
    }

    private bool CongDanTuDongTheoVuKhiDangCam(Transform gocNguoiChoi, WeaponSwitcher boChuyen)
    {
        if (boChuyen != null)
        {
            if (boChuyen.CurrentSniperWeapon != null)
            {
                boChuyen.CurrentSniperWeapon.AddReserveAmmo(soLuongDanCongThem);
                return true;
            }

            if (boChuyen.CurrentWeaponSystem != null)
            {
                boChuyen.CurrentWeaponSystem.AddReserveAmmo(soLuongDanCongThem);
                return true;
            }
        }

        SniperWeapon sungTia = gocNguoiChoi != null ? gocNguoiChoi.GetComponentInChildren<SniperWeapon>(true) : null;
        if (sungTia != null)
        {
            sungTia.AddReserveAmmo(soLuongDanCongThem);
            return true;
        }

        WeaponSystem sungThuong = gocNguoiChoi != null ? gocNguoiChoi.GetComponentInChildren<WeaponSystem>(true) : null;
        if (sungThuong != null)
        {
            sungThuong.AddReserveAmmo(soLuongDanCongThem);
            return true;
        }

        return false;
    }
}
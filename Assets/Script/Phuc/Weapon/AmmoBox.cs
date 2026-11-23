using UnityEngine;

// Phan loai loai dan cua hop dan hien thi duoi dang Dropdown tren Inspector
public enum LoaiDanHopDan
{
    DanVuKhiThuong = 0,     // Cong dan du phong cho WeaponSystem
    DanSungTia = 1,          // Cong dan du phong cho SniperWeapon
    DanLuuDan = 2,           // Cong so luong cho GrenadeWeapon
    TuDongTheoVuKhiDangCam = 3 // Tu cong theo vu khi dang cam tren tay
}

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))] // Dam bao luon co Rigidbody de nhan dien vat ly
public class AmmoBox : MonoBehaviour
{
    [Header("Phan Loai & So Luong Dan")]
    [Tooltip("Chon loai dan cua hop dan nay qua Dropdown (Vu khi thuong, Sung tia, Luu dan, hoac Tu dong).")]
    [SerializeField] private LoaiDanHopDan loaiDan = LoaiDanHopDan.DanVuKhiThuong;

    [Tooltip("So luong dan hoac luu dan duoc cong them khi nhat hop dan.")]
    [SerializeField] private int soLuongDanCongThem = 30;

    [Tooltip("Tag cua doi tuong nguoi choi.")]
    [SerializeField] private string tagNguoiChoi = "Player";

    [Tooltip("Neu bat, khi nhat hop dan luu dan se tu dong chuyen sang slot luu dan neu truoc do het dan.")]
    [SerializeField] private bool tuDongChuyenSangLuuDanKhiNhat = false;

    private void Awake()
    {
        // Dam bao Collider luon la Trigger de nguoi choi di vao nhat duoc
        Collider vaCham = GetComponent<Collider>();
        if (vaCham != null)
        {
            vaCham.isTrigger = true;
        }

        // Dam bao Rigidbody khong bi kinematic de hop dan roi dung trong luc tren moi may
        Rigidbody vatLy = GetComponent<Rigidbody>();
        if (vatLy != null)
        {
            vatLy.isKinematic = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || soLuongDanCongThem <= 0) return;

        Transform gocNguoiChoi = TimGocNguoiChoi(other);
        if (gocNguoiChoi == null) return;

        bool daCongDan = CongDanTheoLoai(gocNguoiChoi);
        if (daCongDan)
        {
            Destroy(gameObject);
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
            vaCham.GetComponentInParent<SniperWeapon>() != null ||
            vaCham.GetComponentInParent<GrenadeWeapon>() != null)
        {
            return vaCham.transform.root;
        }

        return null;
    }

    // Cong dan dung theo dropdown loaiDan da chon
    private bool CongDanTheoLoai(Transform gocNguoiChoi)
    {
        WeaponSwitcher boChuyen = gocNguoiChoi.GetComponentInChildren<WeaponSwitcher>(true);

        switch (loaiDan)
        {
            case LoaiDanHopDan.DanVuKhiThuong:
                return CongDanVuKhiThuong(gocNguoiChoi, boChuyen);

            case LoaiDanHopDan.DanSungTia:
                return CongDanSungTia(gocNguoiChoi, boChuyen);

            case LoaiDanHopDan.DanLuuDan:
                return CongDanLuuDan(gocNguoiChoi, boChuyen);

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

        WeaponSystem sungThuong = gocNguoiChoi.GetComponentInChildren<WeaponSystem>(true);
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

        SniperWeapon sungTia = gocNguoiChoi.GetComponentInChildren<SniperWeapon>(true);
        if (sungTia != null)
        {
            sungTia.AddReserveAmmo(soLuongDanCongThem);
            return true;
        }

        return false;
    }

    private bool CongDanLuuDan(Transform gocNguoiChoi, WeaponSwitcher boChuyen)
    {
        GrenadeWeapon luuDan = null;

        if (boChuyen != null)
        {
            luuDan = boChuyen.CurrentGrenadeWeapon ?? boChuyen.EquippedGrenadeSlot;
        }

        if (luuDan == null)
        {
            luuDan = gocNguoiChoi.GetComponentInChildren<GrenadeWeapon>(true);
        }

        if (luuDan == null) return false;

        luuDan.AddGrenades(soLuongDanCongThem);

        // Chi cho phep hien thi neu nguoi choi dang chon dung slot luu dan
        bool dangChonLuuDan = boChuyen != null &&
                              (boChuyen.CurrentGrenadeWeapon == luuDan ||
                               boChuyen.CurrentWeaponIndex == (int)WeaponCategory.Grenade);

        if (luuDan.ConLuuDan && dangChonLuuDan)
        {
            luuDan.gameObject.SetActive(true);
        }
        else
        {
            luuDan.gameObject.SetActive(false);
        }

        return true;
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

            if (boChuyen.CurrentGrenadeWeapon != null)
            {
                boChuyen.CurrentGrenadeWeapon.AddGrenades(soLuongDanCongThem);
                if (boChuyen.CurrentGrenadeWeapon.ConLuuDan)
                {
                    boChuyen.CurrentGrenadeWeapon.gameObject.SetActive(true);
                }
                return true;
            }
        }

        SniperWeapon sungTia = gocNguoiChoi.GetComponentInChildren<SniperWeapon>(true);
        if (sungTia != null)
        {
            sungTia.AddReserveAmmo(soLuongDanCongThem);
            return true;
        }

        WeaponSystem sungThuong = gocNguoiChoi.GetComponentInChildren<WeaponSystem>(true);
        if (sungThuong != null)
        {
            sungThuong.AddReserveAmmo(soLuongDanCongThem);
            return true;
        }

        GrenadeWeapon luuDan = gocNguoiChoi.GetComponentInChildren<GrenadeWeapon>(true);
        if (luuDan != null)
        {
            luuDan.AddGrenades(soLuongDanCongThem);
            bool dangChonLuuDan = boChuyen != null &&
                                  (boChuyen.CurrentGrenadeWeapon == luuDan ||
                                   boChuyen.CurrentWeaponIndex == (int)WeaponCategory.Grenade);
            luuDan.gameObject.SetActive(luuDan.ConLuuDan && dangChonLuuDan);
            return true;
        }

        return false;
    }
}
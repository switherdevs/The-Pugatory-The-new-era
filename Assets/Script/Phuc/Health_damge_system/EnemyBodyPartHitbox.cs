using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyBodyPartHitbox : MonoBehaviour
{
    public enum LoaiVungCoThe
    {
        Chan,
        Than,
        Dau
    }

    [Header("Phân Loại Vùng Cơ Thể (Dropdown)")]
    [Tooltip("Chọn vùng cơ thể tương ứng với Collider này: Chân, Thân hoặc Đầu.")]
    [SerializeField] private LoaiVungCoThe vungCoThe = LoaiVungCoThe.Than;

    [Header("Hệ Số Sát Thương Theo Vùng")]
    [SerializeField] private float heSoSatThuongChan = 1.0f;
    [SerializeField] private float heSoSatThuongThan = 1.0f;
    [SerializeField] private float heSoSatThuongDau = 2.0f;

    [Header("Hiệu Ứng Vùng Chân (Làm Chậm)")]
    [Tooltip("Hệ số tốc độ còn lại khi trúng chân (0.45 = giảm còn 45% tốc độ).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float heSoTocDoKhiTrungChan = 0.45f;

    [Tooltip("Thời gian bị làm chậm khi trúng chân (giây).")]
    [SerializeField] private float thoiGianLamCham = 2.5f;

    [Header("Hiệu Ứng Choáng (Đầu & Đạn Gây Choáng Trúng Thân)")]
    [Tooltip("Nếu bật: khi trúng Thân bằng loại đạn có bật cờ gây choáng thì quái sẽ bị choáng.")]
    [SerializeField] private bool choPhepChoangKhiTrungThan = true;

    [Tooltip("Thời gian bị choáng khi trúng Đầu hoặc trúng Thân bằng đạn gây choáng (giây).")]
    [SerializeField] private float thoiGianChoang = 1.5f;

    [Header("Hiệu Ứng Tùy Chọn Khi Trúng Vùng")]
    [SerializeField] private GameObject hieuUngTrungVung;

    private HealthSystem healthSystem;
    private EnemyBodyStatusEffect statusEffect;
    private RangedEnemyAI rangedAI;
    private MeleeEnemyAI meleeAI;

    public LoaiVungCoThe VungCoThe => vungCoThe;

    private void Awake()
    {
        healthSystem = GetComponentInParent<HealthSystem>();
        statusEffect = GetComponentInParent<EnemyBodyStatusEffect>();
        if (statusEffect == null && healthSystem != null)
        {
            statusEffect = healthSystem.GetComponent<EnemyBodyStatusEffect>();
            if (statusEffect == null)
            {
                statusEffect = healthSystem.gameObject.AddComponent<EnemyBodyStatusEffect>();
            }
        }

        rangedAI = GetComponentInParent<RangedEnemyAI>();
        meleeAI = GetComponentInParent<MeleeEnemyAI>();
    }

    public void NhanDan(float satThuongGoc, bool laDanGayChoang = false, Vector3 diemVaCham = default)
    {
        if (healthSystem != null && healthSystem.DaChet) return;

        float heSoVung = 1f;
        switch (vungCoThe)
        {
            case LoaiVungCoThe.Chan:
                heSoVung = Mathf.Max(0f, heSoSatThuongChan);
                if (statusEffect != null)
                {
                    statusEffect.ApDungLamCham(heSoTocDoKhiTrungChan, thoiGianLamCham);
                }
                break;

            case LoaiVungCoThe.Than:
                heSoVung = Mathf.Max(0f, heSoSatThuongThan);
                if (laDanGayChoang && choPhepChoangKhiTrungThan && statusEffect != null)
                {
                    statusEffect.ApDungChoang(thoiGianChoang);
                }
                break;

            case LoaiVungCoThe.Dau:
                heSoVung = Mathf.Max(0f, heSoSatThuongDau);
                if (statusEffect != null)
                {
                    statusEffect.ApDungChoang(thoiGianChoang);
                }
                break;
        }

        float satThuongThucTe = Mathf.Max(0f, satThuongGoc * heSoVung);

        if (healthSystem != null && satThuongThucTe > 0f)
        {
            healthSystem.NhanSatThuong(satThuongThucTe);
        }

        if (rangedAI != null)
        {
            rangedAI.NhanSatThuong(satThuongThucTe);
        }

        if (meleeAI != null)
        {
            meleeAI.NhanSatThuong(satThuongThucTe);
        }

        if (hieuUngTrungVung != null)
        {
            Vector3 spawnPos = diemVaCham.sqrMagnitude > 0.0001f ? diemVaCham : transform.position;
            Instantiate(hieuUngTrungVung, spawnPos, Quaternion.identity);
        }
    }

    public void NhanSatThuong(float satThuong)
    {
        NhanDan(satThuong, laDanGayChoang: false, transform.position);
    }

    public void TakeDamage(float amount)
    {
        NhanDan(amount, laDanGayChoang: false, transform.position);
    }
}
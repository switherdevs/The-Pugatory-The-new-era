using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class WeaponSystem : MonoBehaviour
{
    public enum WeaponType { SemiAuto, FullAuto }

    [Header("Weapon Config")]
    [Tooltip("Chọn loại súng: Súng trường (bắn từng viên) hoặc Liên thanh.")]
    [SerializeField] private WeaponType weaponType = WeaponType.FullAuto;

    [Tooltip("Tốc độ bắn (số viên đạn mỗi phút).")]
    [SerializeField] private float fireRate = 600f;

    [Tooltip("Số lượng đạn tối đa trong một băng.")]
    [SerializeField] private int maxAmmo = 30;
    private int currentAmmo;

    [Header("Ammo & Reserve System (Hệ thống đạn dự phòng)")]
    [Tooltip("Số lượng đạn dự phòng hiện tại.")]
    [SerializeField] private int reserveAmmo = 90;
    [Tooltip("Bật chế độ vô hạn đạn: Súng sẽ tự động nạp đầy liên tục mà không cần tiêu hao đạn dự phòng.")]
    [SerializeField] private bool infiniteAmmo = false;

    [Tooltip("Thời gian thay đạn (giây).")]
    [SerializeField] private float reloadTime = 2.0f;
    private bool isReloading = false;
    private float reloadTimer = 0f;
    private float nextFireTime = 0f;

    [Header("Recoil Settings (Độ giật súng)")]
    [Tooltip("Độ giật hướng lên trên mỗi khi bắn.")]
    [SerializeField] private float recoilUp = 1.5f;
    [Tooltip("Độ giật sang trái/phải ngẫu nhiên mỗi khi bắn.")]
    [SerializeField] private float recoilSide = 0.5f;

    [Header("Crosshair Spread Settings")]
    [Tooltip("Lực bung tâm ngắm mỗi khi bắn một viên đạn.")]
    [SerializeField] private float spreadForce = 5f;
    private CrosshairController crosshairUI;

    [Header("References")]
    [Tooltip("Prefab của viên đạn.")]
    [SerializeField] private GameObject bulletPrefab;

    [Tooltip("Điểm xuất phát đạn (Nòng súng).")]
    [SerializeField] private Transform firePoint;

    [Tooltip("Hiệu ứng lóe lửa khi bắn.")]
    [SerializeField] private GameObject muzzleFlashPrefab;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip shootAudioClip;
    [Range(0f, 1f)]
    [SerializeField] private float shootVolume = 1.0f;

    [SerializeField] private AudioClip reloadAudioClip;
    [Range(0f, 1f)]
    [SerializeField] private float reloadVolume = 1.0f;

    private AudioSource audioSource;
    private IronStormCamera cameraRecoilRef;

    [Header("Reload Animation (Nghiêng súng theo trục Z)")]
    [SerializeField] private float reloadTiltZAngle = 30f;
    [SerializeField] private float reloadTransitionSpeed = 10f;

    private Quaternion originalLocalRotation;
    private float currentZOffset = 0f;

    [Header("Input Action References")]
    [SerializeField] private InputActionReference fireAction;
    [SerializeField] private InputActionReference reloadAction;

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;
    public int ReserveAmmo => reserveAmmo; // Truyền ra ngoài nếu cần làm UI hiển thị đạn dự phòng
    public WeaponType CurrentWeaponType => weaponType;

    public void ToggleWeaponMode()
    {
        weaponType = (weaponType == WeaponType.FullAuto) ? WeaponType.SemiAuto : WeaponType.FullAuto;
    }

    /// <summary>
    /// Hàm cộng thêm đạn dự phòng khi người chơi nhặt hộp đạn
    /// </summary>
    public void AddReserveAmmo(int amount)
    {
        reserveAmmo += amount;
        Debug.Log("Đã nhặt thêm " + amount + " viên đạn dự phòng. Tổng: " + reserveAmmo);
    }

    private void Start()
    {
        currentAmmo = maxAmmo;
        originalLocalRotation = transform.localRotation;
        audioSource = GetComponent<AudioSource>();

        if (Camera.main != null)
        {
            cameraRecoilRef = Camera.main.GetComponent<IronStormCamera>();
        }

        crosshairUI = FindFirstObjectByType<CrosshairController>();
        if (crosshairUI == null)
        {
            Debug.LogWarning("Không tìm thấy CrosshairController trong Scene!");
        }

        if (fireAction != null && !fireAction.action.enabled)
        {
            fireAction.action.Enable();
        }
        if (reloadAction != null && !reloadAction.action.enabled)
        {
            reloadAction.action.Enable();
        }
    }

    private void Update()
    {
        if (isReloading)
        {
            reloadTimer -= Time.deltaTime;
            currentZOffset = Mathf.Lerp(currentZOffset, reloadTiltZAngle, reloadTransitionSpeed * Time.deltaTime);

            if (reloadTimer <= 0f)
            {
                CompleteReload();
            }

            ApplyRotation();
            return;
        }
        else
        {
            currentZOffset = Mathf.Lerp(currentZOffset, 0f, reloadTransitionSpeed * Time.deltaTime);
        }

        ApplyRotation();

        // Kiểm tra lệnh thay đạn từ người chơi (phím R)
        if (reloadAction != null && reloadAction.action.triggered && !isReloading && currentAmmo < maxAmmo)
        {
            // Nếu không bật vô hạn đạn và hết sạch đạn dự phòng thì không cho thay đạn
            if (!infiniteAmmo && reserveAmmo <= 0)
            {
                Debug.Log("Hết sạch đạn dự phòng, không thể thay đạn!");
                return;
            }

            StartReload();
            return;
        }

        bool isFiringInput = (weaponType == WeaponType.FullAuto)
            ? (fireAction != null && fireAction.action.IsPressed())
            : (fireAction != null && fireAction.action.triggered);

        if (isFiringInput && Time.time >= nextFireTime)
        {
            if (currentAmmo > 0)
            {
                Shoot();
                nextFireTime = Time.time + (60f / fireRate);
            }
            else
            {
                // Hết đạn trong băng -> Thử tự động thay đạn
                if (infiniteAmmo || reserveAmmo > 0)
                {
                    StartReload();
                }
                else
                {
                    Debug.Log("Hết đạn hoàn toàn! Không thể bắn.");
                }
            }
        }
    }

    private void ApplyRotation()
    {
        Quaternion reloadTiltRotation = Quaternion.Euler(0f, 0f, currentZOffset);
        transform.localRotation = originalLocalRotation * reloadTiltRotation;
    }

    private void Shoot()
    {
        currentAmmo--;

        if (cameraRecoilRef != null)
        {
            cameraRecoilRef.AddRecoil(recoilUp, recoilSide);
        }

        if (crosshairUI != null)
        {
            crosshairUI.SpreadCrosshair(spreadForce);
        }

        if (bulletPrefab != null && firePoint != null)
        {
            Vector3 targetPoint;
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                targetPoint = hit.point;
            }
            else
            {
                targetPoint = ray.GetPoint(1000f);
            }

            Vector3 shootDir = (targetPoint - firePoint.position).normalized;
            Quaternion shootRot = Quaternion.LookRotation(shootDir, Vector3.up);
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, shootRot);

            Bullet bulletScript = bullet.GetComponent<Bullet>();
        }

        if (muzzleFlashPrefab != null && firePoint != null)
        {
            Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation, firePoint);
        }

        if (audioSource != null && shootAudioClip != null)
        {
            audioSource.PlayOneShot(shootAudioClip, shootVolume);
        }
    }

    private void StartReload()
    {
        if (isReloading) return;

        isReloading = true;
        reloadTimer = reloadTime;

        if (audioSource != null && reloadAudioClip != null)
        {
            audioSource.Stop();
            audioSource.PlayOneShot(reloadAudioClip, reloadVolume);
        }
    }

    private void CompleteReload()
    {
        if (infiniteAmmo)
        {
            // Chế độ vô hạn đạn: Luôn nạp đầy băng, không trừ đạn dự phòng
            currentAmmo = maxAmmo;
        }
        else
        {
            // Tính số đạn cần thiết để nạp đầy băng
            int ammoNeeded = maxAmmo - currentAmmo;

            if (reserveAmmo >= ammoNeeded)
            {
                reserveAmmo -= ammoNeeded;
                currentAmmo = maxAmmo;
            }
            else
            {
                // Đạn dự phòng không đủ đầy một băng, gom hết số còn lại vào súng
                currentAmmo += reserveAmmo;
                reserveAmmo = 0;
            }
        }

        isReloading = false;
    }
}
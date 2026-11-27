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

    [Tooltip("Thời gian thay đạn khi VẪN CÒN đạn trong băng (giây - nhanh hơn).")]
    [SerializeField] private float reloadTime = 1.75f;

    [Tooltip("Thời gian thay đạn khi ĐÃ HẾT SẠCH đạn trong băng (giây - lâu hơn chút).")]
    [SerializeField] private float emptyReloadTime = 2.35f;

    private bool isReloading = false;
    private float reloadTimer = 0f;
    private float nextFireTime = 0f;

    [Header("Recoil Settings (Độ giật súng)")]
    [Tooltip("Độ giật hướng lên trên mỗi khi bắn.")]
    [SerializeField] private float recoilUp = 1.5f;

    [Tooltip("Độ giật sang trái/phải ngẫu nhiên mỗi khi bắn.")]
    [SerializeField] private float recoilSide = 0.5f;

    [Tooltip("Hệ số nhân độ giật khi đang Aim (0.5 = giảm 50% độ giật).")]
    [Range(0f, 1f)]
    [SerializeField] private float aimRecoilMultiplier = 0.5f;

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
    private PlayerAimAndLean aimAndLeanRef;

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
    public int ReserveAmmo => reserveAmmo;
    public WeaponType CurrentWeaponType => weaponType;

    private bool IsEquippedByPlayer()
    {
        if (!gameObject.activeInHierarchy || !enabled) return false;
        if (transform.parent == null) return false;
        return GetComponentInParent<CharacterController>() != null || GetComponentInParent<WeaponSwitcher>() != null;
    }

    private bool IsCurrentlyAiming()
    {
        if (aimAndLeanRef == null)
        {
            aimAndLeanRef = GetComponentInParent<PlayerAimAndLean>();
            if (aimAndLeanRef == null)
            {
                aimAndLeanRef = FindFirstObjectByType<PlayerAimAndLean>();
            }
        }

        if (aimAndLeanRef != null && aimAndLeanRef.enabled)
        {
            return aimAndLeanRef.IsAiming;
        }

        return Mouse.current != null && Mouse.current.rightButton.isPressed;
    }

    public void ToggleWeaponMode()
    {
        weaponType = (weaponType == WeaponType.FullAuto) ? WeaponType.SemiAuto : WeaponType.FullAuto;
    }

    public void AddReserveAmmo(int amount)
    {
        reserveAmmo += Mathf.Max(0, amount);
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

        aimAndLeanRef = GetComponentInParent<PlayerAimAndLean>();
        if (aimAndLeanRef == null)
        {
            aimAndLeanRef = FindFirstObjectByType<PlayerAimAndLean>();
        }

        crosshairUI = FindFirstObjectByType<CrosshairController>();

        if (fireAction != null && fireAction.action != null && !fireAction.action.enabled)
        {
            fireAction.action.Enable();
        }
        if (reloadAction != null && reloadAction.action != null && !reloadAction.action.enabled)
        {
            reloadAction.action.Enable();
        }
    }

    private void Update()
    {
        if (!IsEquippedByPlayer())
        {
            isReloading = false;
            return;
        }

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

        if (reloadAction != null && reloadAction.action != null &&
            reloadAction.action.triggered && !isReloading && currentAmmo < maxAmmo)
        {
            if (!infiniteAmmo && reserveAmmo <= 0)
            {
                return;
            }

            StartReload();
            return;
        }

        bool isFiringInput = (weaponType == WeaponType.FullAuto)
            ? (fireAction != null && fireAction.action != null && fireAction.action.IsPressed())
            : (fireAction != null && fireAction.action != null && fireAction.action.triggered);

        if (isFiringInput && Time.time >= nextFireTime)
        {
            if (currentAmmo > 0)
            {
                Shoot();
                nextFireTime = Time.time + (60f / Mathf.Max(1f, fireRate));
            }
            else
            {
                if (infiniteAmmo || reserveAmmo > 0)
                {
                    StartReload();
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
        if (!IsEquippedByPlayer()) return;

        currentAmmo--;

        float recoilMultiplier = IsCurrentlyAiming() ? aimRecoilMultiplier : 1f;
        float appliedRecoilUp = recoilUp * recoilMultiplier;
        float appliedRecoilSide = recoilSide * recoilMultiplier;

        if (cameraRecoilRef != null)
        {
            cameraRecoilRef.AddRecoil(appliedRecoilUp, appliedRecoilSide);
        }

        if (crosshairUI != null)
        {
            crosshairUI.SpreadCrosshair(spreadForce);
        }

        if (bulletPrefab != null && firePoint != null)
        {
            Vector3 targetPoint;
            Ray ray = Camera.main != null
                ? Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : new Ray(firePoint.position, firePoint.forward);

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
            Instantiate(bulletPrefab, firePoint.position, shootRot);
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

        // Còn đạn trong băng -> dùng reloadTime (nhanh hơn); hết sạch đạn trong băng -> dùng emptyReloadTime (lâu hơn)
        bool hasAmmoInMag = currentAmmo > 0;
        float chosenDuration = hasAmmoInMag
            ? Mathf.Max(0.05f, reloadTime)
            : Mathf.Max(0.05f, emptyReloadTime);

        isReloading = true;
        reloadTimer = chosenDuration;

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
            currentAmmo = maxAmmo;
        }
        else
        {
            int ammoNeeded = maxAmmo - currentAmmo;

            if (reserveAmmo >= ammoNeeded)
            {
                reserveAmmo -= ammoNeeded;
                currentAmmo = maxAmmo;
            }
            else
            {
                currentAmmo += reserveAmmo;
                reserveAmmo = 0;
            }
        }

        isReloading = false;
    }
}
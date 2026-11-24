using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class SniperWeapon : MonoBehaviour
{
    [Header("Weapon Config")]
    [Tooltip("Số lượng đạn tối đa trong một băng.")]
    [SerializeField] private int maxAmmo = 5;
    private int currentAmmo;

    [Header("Ammo & Reserve System (Hệ thống đạn dự phòng)")]
    [Tooltip("Số lượng đạn dự phòng hiện tại.")]
    [SerializeField] private int reserveAmmo = 20;
    [Tooltip("Bật chế độ vô hạn đạn: Không tiêu hao đạn dự phòng.")]
    [SerializeField] private bool infiniteAmmo = false;

    [Header("Reload & Cocking Settings (Thay đạn & Lên đạn chuẩn AWP)")]
    [Tooltip("Thời gian thay băng đạn (giây).")]
    [SerializeField] private float reloadTime = 2.5f;

    [Tooltip("Thời gian thực hiện động tác kéo búa/lên đạn (giây).")]
    [SerializeField] private float cockingTime = 0.6f;

    [Header("Animation Speeds (Tốc độ chuyển động)")]
    [SerializeField] private float cockingAnimSpeed = 16f;
    [SerializeField] private float reloadAnimSpeed = 10f;
    [SerializeField] private float returnSpeed = 12f;

    private bool isReloading = false;
    private bool isCocking = false;
    private bool isRecoiling = false;
    private float actionTimer = 0f;

    [Header("Recoil Settings (Độ giật súng tỉa)")]
    [SerializeField] private float recoilUp = 4.0f;
    [SerializeField] private float recoilSide = 1.0f;

    [Header("Crosshair Spread Settings")]
    [SerializeField] private float spreadForce = 25f;
    private CrosshairController crosshairUI;

    [Header("References")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject muzzleFlashPrefab;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip shootAudioClip;
    [Range(0f, 1f)][SerializeField] private float shootVolume = 1.0f;

    [SerializeField] private AudioClip reloadAudioClip;
    [Range(0f, 1f)][SerializeField] private float reloadVolume = 1.0f;

    [SerializeField] private AudioClip cockingAudioClip;
    [Range(0f, 1f)][SerializeField] private float cockingVolume = 1.0f;

    private AudioSource shootAudioSource;
    private AudioSource actionAudioSource;
    private IronStormCamera cameraRecoilRef;

    [Header("AWP Animation Settings (Rotation Offsets)")]
    [SerializeField] private float shootRecoilTiltX = 12f;
    [SerializeField] private float cockingTiltZAngle = 25f;
    [SerializeField] private float reloadTiltZAngle = 35f;
    [SerializeField] private float reloadTiltXAngle = 15f;

    private Quaternion originalLocalRotation;
    private float currentXOffset = 0f;
    private float currentZOffset = 0f;

    [Header("CS2 Sniper Scope / Zoom Settings")]
    [Tooltip("Kéo GameObject chứa ảnh tâm ngắm (Scope UI Canvas/Panel) vào đây.")]
    [SerializeField] private GameObject scopeUIObject;
    [Tooltip("Kéo các GameObject chứa mesh súng (kể cả ở object khác) vào đây để ẩn khi đang ngắm scope.")]
    [SerializeField] private GameObject[] weaponMeshesToHideOnScope;
    [Tooltip("Góc nhìn (FOV) mặc định của Camera khi không zoom.")]
    [SerializeField] private float normalFOV = 60f;
    [Tooltip("Góc nhìn (FOV) khi Zoom mức 1.")]
    [SerializeField] private float zoomLevel1FOV = 40f;
    [Tooltip("Góc nhìn (FOV) khi Zoom mức 2 (Gần hơn nữa).")]
    [SerializeField] private float zoomLevel2FOV = 15f;
    [Tooltip("Tốc độ chuyển đổi FOV mượt mà (Zoom Speed).")]
    [SerializeField] private float zoomTransitionSpeed = 15f;

    [Tooltip("Tỷ lệ scale của RectTransform ảnh tâm ngắm ở mức Zoom 1.")]
    [SerializeField] private Vector3 zoom1ImageScale = new Vector3(1f, 1f, 1f);
    [Tooltip("Tỷ lệ scale của RectTransform ảnh tâm ngắm ở mức Zoom 2 (Phóng to hơn).")]
    [SerializeField] private Vector3 zoom2ImageScale = new Vector3(1.5f, 1.5f, 1f);

    private int zoomStage = 0; // 0: Không zoom, 1: Zoom lần 1, 2: Zoom lần 2
    private float targetFOV;
    private Camera mainCam;
    private PlayerAimAndLean aimAndLeanRef;
    private bool aimAndLeanWasEnabledBeforeScope = true;
    private bool hasOverriddenAimAndLean = false;

    [Header("Input Action References")]
    [SerializeField] private InputActionReference fireAction;
    [SerializeField] private InputActionReference reloadAction;
    [Tooltip("InputAction dạng Button cho phím chuột phải (Right Click) để Zoom.")]
    [SerializeField] private InputActionReference zoomAction;

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;
    public int ReserveAmmo => reserveAmmo;

    private bool IsEquippedByPlayer()
    {
        if (!gameObject.activeInHierarchy || !enabled) return false;
        if (transform.parent == null) return false;
        return GetComponentInParent<CharacterController>() != null || GetComponentInParent<WeaponSwitcher>() != null;
    }

    public void AddReserveAmmo(int amount)
    {
        reserveAmmo += amount;
        Debug.Log("Súng tỉa đã nhặt thêm " + amount + " viên đạn. Tổng dự phòng: " + reserveAmmo);
    }

    private void Awake()
    {
        aimAndLeanRef = GetComponentInParent<PlayerAimAndLean>();
        if (aimAndLeanRef == null)
        {
            aimAndLeanRef = FindFirstObjectByType<PlayerAimAndLean>();
        }
    }

    private void SetWeaponMeshesVisible(bool visible)
    {
        if (weaponMeshesToHideOnScope == null) return;

        for (int i = 0; i < weaponMeshesToHideOnScope.Length; i++)
        {
            GameObject meshObj = weaponMeshesToHideOnScope[i];
            if (meshObj != null && meshObj.activeSelf != visible)
            {
                meshObj.SetActive(visible);
            }
        }
    }

    private void DongBoTrangThaiAimAndLean(bool dangNgamScope)
    {
        if (aimAndLeanRef == null)
        {
            aimAndLeanRef = GetComponentInParent<PlayerAimAndLean>();
            if (aimAndLeanRef == null)
            {
                aimAndLeanRef = FindFirstObjectByType<PlayerAimAndLean>();
            }
        }

        if (aimAndLeanRef == null) return;

        if (dangNgamScope)
        {
            if (!hasOverriddenAimAndLean)
            {
                aimAndLeanWasEnabledBeforeScope = aimAndLeanRef.enabled;
                hasOverriddenAimAndLean = true;
            }
            if (aimAndLeanRef.enabled)
            {
                aimAndLeanRef.enabled = false;
            }
        }
        else if (hasOverriddenAimAndLean)
        {
            aimAndLeanRef.enabled = aimAndLeanWasEnabledBeforeScope;
            hasOverriddenAimAndLean = false;
        }
    }

    private void Start()
    {
        currentAmmo = maxAmmo;
        originalLocalRotation = transform.localRotation;

        mainCam = Camera.main;
        if (mainCam != null)
        {
            normalFOV = mainCam.fieldOfView;
            targetFOV = normalFOV;
            cameraRecoilRef = mainCam.GetComponent<IronStormCamera>();
        }

        if (scopeUIObject != null)
        {
            scopeUIObject.SetActive(false);
        }
        SetWeaponMeshesVisible(true);

        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length >= 2)
        {
            shootAudioSource = sources[0];
            actionAudioSource = sources[1];
        }
        else
        {
            shootAudioSource = GetComponent<AudioSource>();
            actionAudioSource = gameObject.AddComponent<AudioSource>();
        }

        crosshairUI = FindFirstObjectByType<CrosshairController>();
        if (crosshairUI == null)
        {
            Debug.LogWarning("Không tìm thấy CrosshairController trong Scene!");
        }

        if (fireAction != null && fireAction.action != null) fireAction.action.Enable();
        if (reloadAction != null && reloadAction.action != null) reloadAction.action.Enable();
        if (zoomAction != null && zoomAction.action != null) zoomAction.action.Enable();
    }

    private void OnDisable()
    {
        ResetZoomState();
    }

    private void Update()
    {
        if (!IsEquippedByPlayer())
        {
            isReloading = false;
            isCocking = false;
            isRecoiling = false;
            ResetZoomState();
            return;
        }

        HandleZoomInput();

        if (mainCam != null)
        {
            mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, targetFOV, zoomTransitionSpeed * Time.deltaTime);
        }

        if (isReloading)
        {
            actionTimer -= Time.deltaTime;
            currentZOffset = Mathf.Lerp(currentZOffset, reloadTiltZAngle, reloadAnimSpeed * Time.deltaTime);
            currentXOffset = Mathf.Lerp(currentXOffset, reloadTiltXAngle, reloadAnimSpeed * Time.deltaTime);

            if (actionTimer <= 0f)
            {
                CompleteReload();
            }

            ApplyRotation();
            return;
        }

        if (isCocking)
        {
            actionTimer -= Time.deltaTime;

            float progress = Mathf.Clamp01(1f - (actionTimer / cockingTime));
            float smoothCurve = Mathf.Sin(progress * Mathf.PI);
            float targetTiltZ = smoothCurve * cockingTiltZAngle;

            if (progress > 0.85f)
            {
                currentZOffset = Mathf.Lerp(currentZOffset, 0f, cockingAnimSpeed * Time.deltaTime);
            }
            else
            {
                currentZOffset = Mathf.Lerp(currentZOffset, targetTiltZ, cockingAnimSpeed * Time.deltaTime);
            }

            currentXOffset = Mathf.Lerp(currentXOffset, 0f, cockingAnimSpeed * Time.deltaTime);

            if (actionTimer <= 0f)
            {
                FinishCocking();
            }

            ApplyRotation();
            return;
        }

        if (isRecoiling)
        {
            currentXOffset = Mathf.Lerp(currentXOffset, shootRecoilTiltX, returnSpeed * 4f * Time.deltaTime);
            currentZOffset = Mathf.Lerp(currentZOffset, 0f, returnSpeed * Time.deltaTime);

            ApplyRotation();
            return;
        }

        currentZOffset = Mathf.Lerp(currentZOffset, 0f, returnSpeed * Time.deltaTime);
        currentXOffset = Mathf.Lerp(currentXOffset, 0f, returnSpeed * Time.deltaTime);
        ApplyRotation();

        // Kiểm tra lệnh thay đạn
        if (reloadAction != null && reloadAction.action.triggered && !isReloading && !isCocking && !isRecoiling && currentAmmo < maxAmmo)
        {
            if (!infiniteAmmo && reserveAmmo <= 0)
            {
                Debug.Log("Hết sạch đạn dự phòng, không thể thay đạn!");
                return;
            }
            StartReload();
            return;
        }

        // Kiểm tra lệnh bóp cò
        bool isFiringInput = fireAction != null && fireAction.action.triggered;

        if (isFiringInput && !isReloading && !isRecoiling && !isCocking)
        {
            if (currentAmmo > 0)
            {
                Shoot();
            }
            else
            {
                if (infiniteAmmo || reserveAmmo > 0)
                {
                    StartReload();
                }
                else
                {
                    Debug.Log("Súng tỉa đã hết sạch đạn!");
                }
            }
        }
    }

    private void FinishCocking()
    {
        isCocking = false;
        currentZOffset = 0f;
        currentXOffset = 0f;
    }

    private void HandleZoomInput()
    {
        if (zoomAction != null && zoomAction.action.triggered)
        {
            zoomStage++;

            if (zoomStage == 1)
            {
                targetFOV = zoomLevel1FOV;
                DongBoTrangThaiAimAndLean(true);
                SetWeaponMeshesVisible(false);
                if (scopeUIObject != null)
                {
                    scopeUIObject.SetActive(true);
                    scopeUIObject.transform.localScale = zoom1ImageScale;
                }
            }
            else if (zoomStage == 2)
            {
                targetFOV = zoomLevel2FOV;
                DongBoTrangThaiAimAndLean(true);
                SetWeaponMeshesVisible(false);
                if (scopeUIObject != null)
                {
                    scopeUIObject.SetActive(true);
                    scopeUIObject.transform.localScale = zoom2ImageScale;
                }
            }
            else
            {
                ResetZoomState();
            }
        }
    }

    public void ResetZoomState()
    {
        zoomStage = 0;
        targetFOV = normalFOV;
        SetWeaponMeshesVisible(true);
        DongBoTrangThaiAimAndLean(false);
        if (scopeUIObject != null)
        {
            scopeUIObject.SetActive(false);
            scopeUIObject.transform.localScale = zoom1ImageScale;
        }
    }

    private void ApplyRotation()
    {
        Quaternion tiltRotation = Quaternion.Euler(currentXOffset, 0f, currentZOffset);
        transform.localRotation = originalLocalRotation * tiltRotation;
    }

    private void Shoot()
    {
        if (!IsEquippedByPlayer()) return;

        currentAmmo--;

        if (shootAudioSource != null && shootAudioClip != null)
        {
            shootAudioSource.PlayOneShot(shootAudioClip, shootVolume);
        }

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
            Instantiate(bulletPrefab, firePoint.position, shootRot);
        }

        if (muzzleFlashPrefab != null && firePoint != null)
        {
            Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation, firePoint);
        }

        isRecoiling = true;
        StopAllCoroutines();
        StartCoroutine(ShootSequenceRoutine());
    }

    private IEnumerator ShootSequenceRoutine()
    {
        yield return new WaitForSeconds(0.12f);

        isRecoiling = false;
        StartCocking();
    }

    private void StartCocking()
    {
        isCocking = true;
        actionTimer = cockingTime;

        if (actionAudioSource != null && cockingAudioClip != null)
        {
            actionAudioSource.PlayOneShot(cockingAudioClip, cockingVolume);
        }
    }

    private void StartReload()
    {
        if (isReloading) return;

        ResetZoomState();

        StopAllCoroutines();
        isReloading = true;
        isRecoiling = false;
        isCocking = false;
        actionTimer = reloadTime;

        if (actionAudioSource != null && reloadAudioClip != null)
        {
            actionAudioSource.Stop();
            actionAudioSource.PlayOneShot(reloadAudioClip, reloadVolume);
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
        StartCocking();
    }
}
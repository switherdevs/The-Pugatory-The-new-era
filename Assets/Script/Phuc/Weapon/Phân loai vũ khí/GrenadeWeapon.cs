using UnityEngine;
using UnityEngine.InputSystem;

public class GrenadeWeapon : MonoBehaviour
{
    [Header("1. Thong Tin Vu Khi & UI")]
    [SerializeField] private string tenHienThiVuKhi = "Frag Grenade";
    [SerializeField] private int soLuongLuuDanToiDa = 4;
    [SerializeField] private int soLuongLuuDanKhoiDau = -1;
    [SerializeField] private float thoiGianTuHoiLuuDan = 8f;
    [SerializeField] private float thoiGianHoiChieuNem = 0.65f;

    [Header("2. Phim Dieu Khien (New Input System)")]
    [SerializeField] private InputActionReference hanhDongNem;
    [SerializeField] private InputActionReference hanhDongHuyNem;

    [Header("3. Nem & Quy Dao Luu Dan")]
    [SerializeField] private Transform diemXuatPhatNem;
    [SerializeField] private GameObject prefabQuaLuuDan;
    [SerializeField] private float tocDoNemCoBan = 20f;
    [SerializeField] private float tocDoNemToiDa = 30f;
    [SerializeField] private float thoiGianDonLucToiDa = 1.0f;
    [SerializeField] private float lucDayLenParabol = 3.2f;
    [SerializeField] private float thoiGianNgoiNo = 2.2f;

    [Header("4. Sat Thuong & Vung No")]
    [SerializeField] private float satThuongTaiTamNo = 95f;
    [SerializeField] private float banKinhVungNo = 7.5f;
    [Range(0f, 1f)]
    [SerializeField] private float heSoSatThuongRiaNo = 0.25f;
    [SerializeField] private LayerMask lopCoTheNhanSatThuong = ~0;
    [SerializeField] private LayerMask lopVatCan = 0;
    [SerializeField] private bool kiemTraVatCanCheKhuat = false;
    [SerializeField] private float lucDayVatLy = 12f;

    [Header("5. Lien Ket Camera, Hieu Ung & Am Thanh")]
    [SerializeField] private IronStormCamera scriptCamera;
    [SerializeField] private float cuongDoRungCamera = 1.2f;
    [SerializeField] private GameObject prefabHieuUngNo;
    [SerializeField] private float thoiGianHuyHieuUngNo = 3f;

    [SerializeField] private AudioSource nguonAmThanh;
    [SerializeField] private AudioClip amThanhRutChot;
    [SerializeField] private AudioClip amThanhNem;
    [SerializeField] private AudioClip amThanhHuy;
    [SerializeField] private AudioClip amThanhNo;

    [Range(0f, 1f)]
    [SerializeField] private float amLuong = 0.9f;

    private int currentGrenades;
    private float lastThrowTime = -999f;
    private float regenTimer;
    private bool isCookingPin;
    private float chargeTimer;

    private InputAction runtimeThrowAction;
    private InputAction runtimeCancelAction;

    public string WeaponDisplayName => string.IsNullOrWhiteSpace(tenHienThiVuKhi) ? gameObject.name : tenHienThiVuKhi;
    public int CurrentGrenadeCount => currentGrenades;
    public int MaxGrenadeCount => soLuongLuuDanToiDa;
    public float RemainingCooldown => Mathf.Max(0f, (lastThrowTime + thoiGianHoiChieuNem) - Time.time);
    public string ModeLabel => "GRENADE";

    public string CurrentStateLabel
    {
        get
        {
            if (isCookingPin) return $"COOKING ({chargeTimer:0.1}s)";
            if (currentGrenades <= 0)
            {
                if (thoiGianTuHoiLuuDan > 0f)
                {
                    float remain = Mathf.Max(0f, thoiGianTuHoiLuuDan - regenTimer);
                    return $"0 / {soLuongLuuDanToiDa} (+{remain:0.1}s)";
                }
                return $"0 / {soLuongLuuDanToiDa} (EMPTY)";
            }
            if (RemainingCooldown > 0.01f) return $"{currentGrenades} / {soLuongLuuDanToiDa} (CD {RemainingCooldown:0.1}s)";
            return $"{currentGrenades} / {soLuongLuuDanToiDa}";
        }
    }

    private InputAction ActiveThrowAction =>
        hanhDongNem != null && hanhDongNem.action != null ? hanhDongNem.action : runtimeThrowAction;

    private InputAction ActiveCancelAction =>
        hanhDongHuyNem != null && hanhDongHuyNem.action != null ? hanhDongHuyNem.action : runtimeCancelAction;

    private void Awake()
    {
        soLuongLuuDanToiDa = Mathf.Max(1, soLuongLuuDanToiDa);
        currentGrenades = soLuongLuuDanKhoiDau < 0
            ? soLuongLuuDanToiDa
            : Mathf.Clamp(soLuongLuuDanKhoiDau, 0, soLuongLuuDanToiDa);

        TuDongGanThanhPhan();

        runtimeThrowAction = new InputAction("GrenadeThrow", InputActionType.Button);
        runtimeThrowAction.AddBinding("<Mouse>/leftButton");
        runtimeThrowAction.AddBinding("<Keyboard>/g");

        runtimeCancelAction = new InputAction("GrenadeCancel", InputActionType.Button);
        runtimeCancelAction.AddBinding("<Mouse>/rightButton");
        runtimeCancelAction.AddBinding("<Keyboard>/r");
    }

    private void TuDongGanThanhPhan()
    {
        if (diemXuatPhatNem == null)
        {
            diemXuatPhatNem = TimChildTheoTuKhoa(transform, "throw", "muzzle", "hand", "grenade", "origin");
            if (diemXuatPhatNem == null)
            {
                diemXuatPhatNem = transform;
            }
        }

        if (scriptCamera == null)
        {
            scriptCamera = GetComponentInParent<IronStormCamera>();
            if (scriptCamera == null)
            {
                scriptCamera = FindFirstObjectByType<IronStormCamera>();
            }
        }

        if (nguonAmThanh == null)
        {
            nguonAmThanh = GetComponent<AudioSource>();
            if (nguonAmThanh == null)
            {
                nguonAmThanh = gameObject.AddComponent<AudioSource>();
                nguonAmThanh.playOnAwake = false;
            }
        }
    }

    private Transform TimChildTheoTuKhoa(Transform parent, params string[] keywords)
    {
        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == parent) continue;
            string lowerName = child.name.ToLowerInvariant();
            for (int j = 0; j < keywords.Length; j++)
            {
                if (lowerName.Contains(keywords[j]))
                {
                    return child;
                }
            }
        }
        return null;
    }

    private void OnEnable()
    {
        TuDongGanThanhPhan();
        ActiveThrowAction?.Enable();
        ActiveCancelAction?.Enable();
    }

    private void OnDisable()
    {
        isCookingPin = false;
        chargeTimer = 0f;
        ActiveThrowAction?.Disable();
        ActiveCancelAction?.Disable();
    }

    private void OnDestroy()
    {
        runtimeThrowAction?.Dispose();
        runtimeCancelAction?.Dispose();
    }

    private void Update()
    {
        HandleAutoRegen();

        if (!gameObject.activeInHierarchy || !enabled)
        {
            isCookingPin = false;
            chargeTimer = 0f;
            return;
        }

        ActiveThrowAction?.Enable();
        ActiveCancelAction?.Enable();

        bool pressedThrow = ActiveThrowAction != null && ActiveThrowAction.WasPressedThisFrame();
        bool heldThrow = ActiveThrowAction != null && ActiveThrowAction.IsPressed();
        bool pressedCancel = ActiveCancelAction != null && ActiveCancelAction.WasPressedThisFrame();

        if (isCookingPin && pressedCancel)
        {
            CancelCooking();
            return;
        }

        if (!isCookingPin && pressedThrow && CanStartThrow())
        {
            isCookingPin = true;
            chargeTimer = 0f;
            PlayClip(amThanhRutChot);
        }
        else if (isCookingPin)
        {
            chargeTimer += Time.deltaTime;

            if (!heldThrow)
            {
                ExecuteThrow(chargeTimer);
                isCookingPin = false;
                chargeTimer = 0f;
            }
        }
    }

    private bool CanStartThrow()
    {
        return currentGrenades > 0 && Time.time >= lastThrowTime + thoiGianHoiChieuNem;
    }

    private void CancelCooking()
    {
        isCookingPin = false;
        chargeTimer = 0f;
        PlayClip(amThanhHuy);
    }

    private void HandleAutoRegen()
    {
        if (thoiGianTuHoiLuuDan <= 0f || currentGrenades >= soLuongLuuDanToiDa)
        {
            regenTimer = 0f;
            return;
        }

        regenTimer += Time.deltaTime;
        if (regenTimer >= thoiGianTuHoiLuuDan)
        {
            regenTimer = 0f;
            currentGrenades = Mathf.Min(soLuongLuuDanToiDa, currentGrenades + 1);
        }
    }

    private void ExecuteThrow(float heldDuration)
    {
        if (!CanStartThrow()) return;

        currentGrenades = Mathf.Max(0, currentGrenades - 1);
        lastThrowTime = Time.time;
        PlayClip(amThanhNem);

        Transform origin = diemXuatPhatNem != null ? diemXuatPhatNem : transform;
        Vector3 spawnPos = origin.position + origin.forward * 0.35f;

        Vector3 aimDirection = GetThrowDirection(origin);
        float chargeRatio = thoiGianDonLucToiDa > 0.001f ? Mathf.Clamp01(heldDuration / thoiGianDonLucToiDa) : 0f;
        float speed = Mathf.Lerp(tocDoNemCoBan, tocDoNemToiDa, chargeRatio);
        Vector3 initialVelocity = aimDirection * speed + Vector3.up * lucDayLenParabol;

        GameObject grenadeObj;
        if (prefabQuaLuuDan != null)
        {
            grenadeObj = Instantiate(prefabQuaLuuDan, spawnPos, Quaternion.LookRotation(aimDirection, Vector3.up));
        }
        else
        {
            grenadeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            grenadeObj.name = "ThrownGrenade";
            grenadeObj.transform.position = spawnPos;
            grenadeObj.transform.localScale = Vector3.one * 0.22f;
        }

        Rigidbody rb = grenadeObj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = grenadeObj.AddComponent<Rigidbody>();
        }
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = initialVelocity;
        rb.AddTorque(Random.insideUnitSphere * 12f, ForceMode.Impulse);

        DamageDealer dealer = grenadeObj.GetComponent<DamageDealer>();
        if (dealer != null)
        {
            dealer.SetOwner(transform.root);
            dealer.SetDamage(satThuongTaiTamNo);
        }

        ThrownGrenadeProjectile projectile = grenadeObj.GetComponent<ThrownGrenadeProjectile>();
        if (projectile == null)
        {
            projectile = grenadeObj.AddComponent<ThrownGrenadeProjectile>();
        }

        projectile.Initialize(
            transform.root,
            thoiGianNgoiNo,
            satThuongTaiTamNo,
            banKinhVungNo,
            heSoSatThuongRiaNo,
            lopCoTheNhanSatThuong,
            lopVatCan,
            kiemTraVatCanCheKhuat,
            lucDayVatLy,
            scriptCamera,
            cuongDoRungCamera,
            prefabHieuUngNo,
            thoiGianHuyHieuUngNo,
            amThanhNo,
            amLuong
        );
    }

    private Vector3 GetThrowDirection(Transform origin)
    {
        Camera cam = null;
        if (scriptCamera != null)
        {
            cam = scriptCamera.GetComponent<Camera>();
            if (cam == null)
            {
                cam = scriptCamera.GetComponentInChildren<Camera>();
            }
        }
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam != null)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, 120f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.GetPoint(40f);

            Vector3 dir = targetPoint - origin.position;
            if (dir.sqrMagnitude > 0.001f)
            {
                return dir.normalized;
            }
        }

        return origin.forward.normalized;
    }

    public void AddGrenades(int amount)
    {
        if (amount <= 0) return;
        currentGrenades = Mathf.Clamp(currentGrenades + amount, 0, soLuongLuuDanToiDa);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip != null && nguonAmThanh != null)
        {
            nguonAmThanh.PlayOneShot(clip, amLuong);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = diemXuatPhatNem != null ? diemXuatPhatNem.position : transform.position;
        Gizmos.color = new Color(1f, 0.35f, 0.05f, 0.25f);
        Gizmos.DrawWireSphere(center, banKinhVungNo);
    }
}

public class ThrownGrenadeProjectile : MonoBehaviour
{
    private Transform ownerRoot;
    private float fuseTimer;
    private float damage;
    private float radius;
    private float edgeMultiplier;
    private LayerMask damageLayers;
    private LayerMask obstacleMask;
    private bool checkLineOfSight;
    private float pushForce;

    private IronStormCamera camScript;
    private float shakeIntensity;

    private GameObject vfxPrefab;
    private float vfxLifetime;
    private AudioClip sfxClip;
    private float volume;

    private bool initialized;
    private bool exploded;

    public void Initialize(
        Transform owner,
        float fuse,
        float explosionDamage,
        float explosionRadius,
        float edgeDamageMul,
        LayerMask targetLayers,
        LayerMask blockLayers,
        bool useLosCheck,
        float explosionPush,
        IronStormCamera cameraRef,
        float camShakeInt,
        GameObject effectPrefab,
        float effectLife,
        AudioClip explosionAudio,
        float audioVol)
    {
        ownerRoot = owner;
        fuseTimer = Mathf.Max(0.05f, fuse);
        damage = Mathf.Max(0f, explosionDamage);
        radius = Mathf.Max(0.2f, explosionRadius);
        edgeMultiplier = Mathf.Clamp01(edgeDamageMul);
        damageLayers = targetLayers;
        obstacleMask = blockLayers;
        checkLineOfSight = useLosCheck;
        pushForce = Mathf.Max(0f, explosionPush);

        camScript = cameraRef;
        shakeIntensity = camShakeInt;

        vfxPrefab = effectPrefab;
        vfxLifetime = Mathf.Max(0.1f, effectLife);
        sfxClip = explosionAudio;
        volume = Mathf.Clamp01(audioVol);

        initialized = true;
    }

    private void Update()
    {
        if (!initialized || exploded) return;

        fuseTimer -= Time.deltaTime;
        if (fuseTimer <= 0f)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (exploded) return;
        exploded = true;

        Vector3 blastPos = transform.position;

        if (vfxPrefab != null)
        {
            GameObject vfx = Instantiate(vfxPrefab, blastPos, Quaternion.identity);
            Destroy(vfx, vfxLifetime);
        }

        if (sfxClip != null)
        {
            AudioSource.PlayClipAtPoint(sfxClip, blastPos, volume);
        }

        if (camScript != null && shakeIntensity > 0f)
        {
            camScript.SendMessage("AddShootShake", shakeIntensity, SendMessageOptions.DontRequireReceiver);
        }

        Collider[] hits = Physics.OverlapSphere(blastPos, radius, damageLayers, QueryTriggerInteraction.Ignore);
        System.Collections.Generic.HashSet<HealthSystem> damagedHealths = new System.Collections.Generic.HashSet<HealthSystem>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            if (ownerRoot != null && hit.transform.root == ownerRoot)
            {
                continue;
            }

            Vector3 closestPoint = hit.ClosestPoint(blastPos);
            float dist = Vector3.Distance(blastPos, closestPoint);
            if (dist > radius) continue;

            if (checkLineOfSight && obstacleMask != 0)
            {
                Vector3 dir = closestPoint - blastPos;
                float rayDist = dir.magnitude;
                if (rayDist > 0.01f && Physics.Raycast(blastPos, dir / rayDist, rayDist, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }
            }

            HealthSystem health = hit.GetComponentInParent<HealthSystem>();
            if (health != null && !health.DaChet && health.Phe == HealthSystem.LoaiDoiTuong.QuaiVat && !damagedHealths.Contains(health))
            {
                damagedHealths.Add(health);

                float falloff = 1f - Mathf.Clamp01(dist / radius);
                float damageFactor = Mathf.Lerp(edgeMultiplier, 1f, falloff);
                float finalDamage = damage * damageFactor;

                if (finalDamage > 0f)
                {
                    health.NhanSatThuong(finalDamage);
                }
            }

            Rigidbody targetRb = hit.GetComponentInParent<Rigidbody>();
            if (targetRb != null && !targetRb.isKinematic && pushForce > 0f)
            {
                targetRb.AddExplosionForce(pushForce, blastPos, radius, 0.5f, ForceMode.Impulse);
            }
        }

        Destroy(gameObject);
    }
}
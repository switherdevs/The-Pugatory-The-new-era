using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAimAndLean : MonoBehaviour
{
    [Header("1. Thiết Lập Aim (Ngắm Bắn)")]
    [Tooltip("Phím giữ để Aim (nếu để trống sẽ tự dùng Chuột phải).")]
    [SerializeField] private InputActionReference aimAction;

    [Tooltip("Camera chính của người chơi (để trống sẽ tự tìm trên GameObject này hoặc con).")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("Camera riêng cho vũ khí nếu có (tùy chọn).")]
    [SerializeField] private Camera weaponCamera;

    [Tooltip("FOV khi ngắm (càng nhỏ phóng to càng nhiều).")]
    [SerializeField] private float aimFov = 42f;

    [Tooltip("Tốc độ chuyển đổi FOV khi Aim.")]
    [SerializeField] private float aimFovSpeed = 14f;

    [Tooltip("Transform vũ khí/tay cầm để đưa vào vị trí ngắm (tùy chọn).")]
    [SerializeField] private Transform weaponPivot;

    [Tooltip("Vị trí local của weaponPivot khi đang Aim.")]
    [SerializeField] private Vector3 aimWeaponLocalPosition = new Vector3(0f, -0.02f, 0.12f);

    [Tooltip("Góc xoay local (độ) của weaponPivot khi đang Aim.")]
    [SerializeField] private Vector3 aimWeaponLocalEuler = Vector3.zero;

    [Header("2. Thiết Lập Nghiêng Người (Lean Trái / Phải)")]
    [Tooltip("Phím nghiêng trái (nếu để trống sẽ tự dùng phím Q).")]
    [SerializeField] private InputActionReference leanLeftAction;

    [Tooltip("Phím nghiêng phải (nếu để trống sẽ tự dùng phím E).")]
    [SerializeField] private InputActionReference leanRightAction;

    [Tooltip("Bật chế độ bấm 1 lần để giữ nghiêng (tắt = giữ phím mới nghiêng).")]
    [SerializeField] private bool toggleLeanMode = false;

    [Tooltip("Transform trục nghiêng (nên là object cha của MainCamera, không phải object có IronStormCamera).")]
    [SerializeField] private Transform leanPivot;

    [Tooltip("Góc nghiêng trục Z tối đa (độ) đúng theo giá trị điền ở đây. Dương = nghiêng trái, Âm = nghiêng phải.")]
    [SerializeField] private float maxLeanAngleZ = 16f;

    [Tooltip("Độ lệch ngang (trục X, mét) khi nghiêng người để ló đầu qua góc tường.")]
    [SerializeField] private float maxLeanOffsetX = 0.35f;

    [Tooltip("Độ hạ thấp nhẹ (trục Y, mét) khi nghiêng người.")]
    [SerializeField] private float leanDropY = -0.05f;

    [Tooltip("Tốc độ nội suy khi nghiêng hoặc trả về thẳng đứng.")]
    [SerializeField] private float leanSmoothness = 12f;

    [Header("3. Tích Hợp Script Khác")]
    [SerializeField] private IronStormMovement movementScript;
    [SerializeField] private WeaponSwitcher weaponSwitcher;

    private bool isAiming = false;
    private int leanState = 0; // -1: Nghiêng phải, 0: Thẳng, 1: Nghiêng trái
    private float currentLeanFactor = 0f;

    private float baseMainFov = 60f;
    private float baseWeaponFov = 60f;

    private Vector3 leanPivotBaseLocalPos;
    private Vector3 leanPivotBaseLocalEuler;
    private bool hasSavedLeanBase = false;

    private Vector3 weaponPivotBaseLocalPos;
    private Quaternion weaponPivotBaseLocalRot;
    private bool hasSavedWeaponBase = false;

    private InputAction runtimeAimAction;
    private InputAction runtimeLeanLeftAction;
    private InputAction runtimeLeanRightAction;

    public bool IsAiming => isAiming;
    public int LeanDirection => leanState; // -1 = Phải, 0 = Giữa, 1 = Trái

    private InputAction ActiveAimAction =>
        aimAction != null && aimAction.action != null ? aimAction.action : runtimeAimAction;

    private InputAction ActiveLeanLeftAction =>
        leanLeftAction != null && leanLeftAction.action != null ? leanLeftAction.action : runtimeLeanLeftAction;

    private InputAction ActiveLeanRightAction =>
        leanRightAction != null && leanRightAction.action != null ? leanRightAction.action : runtimeLeanRightAction;

    private void Awake()
    {
        TuDongGanThanhPhan();
        LuuTrangThaiBanDau();

        runtimeAimAction = new InputAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
        runtimeLeanLeftAction = new InputAction("LeanLeft", InputActionType.Button, "<Keyboard>/q");
        runtimeLeanRightAction = new InputAction("LeanRight", InputActionType.Button, "<Keyboard>/e");
    }

    private void OnEnable()
    {
        ActiveAimAction?.Enable();
        ActiveLeanLeftAction?.Enable();
        ActiveLeanRightAction?.Enable();
    }

    private void OnDisable()
    {
        isAiming = false;
        leanState = 0;
        currentLeanFactor = 0f;

        if (hasSavedLeanBase && leanPivot != null)
        {
            leanPivot.localPosition = leanPivotBaseLocalPos;
            leanPivot.localEulerAngles = leanPivotBaseLocalEuler;
        }

        if (hasSavedWeaponBase && weaponPivot != null)
        {
            weaponPivot.localPosition = weaponPivotBaseLocalPos;
            weaponPivot.localRotation = weaponPivotBaseLocalRot;
        }

        if (mainCamera != null)
        {
            mainCamera.fieldOfView = baseMainFov;
        }

        if (weaponCamera != null)
        {
            weaponCamera.fieldOfView = baseWeaponFov;
        }

        ActiveAimAction?.Disable();
        ActiveLeanLeftAction?.Disable();
        ActiveLeanRightAction?.Disable();
    }

    private void OnDestroy()
    {
        runtimeAimAction?.Dispose();
        runtimeLeanLeftAction?.Dispose();
        runtimeLeanRightAction?.Dispose();
    }

    private void Start()
    {
        TuDongGanThanhPhan();
        LuuTrangThaiBanDau();
    }

    private void TuDongGanThanhPhan()
    {
        if (mainCamera == null)
        {
            mainCamera = GetComponent<Camera>();
            if (mainCamera == null) mainCamera = GetComponentInChildren<Camera>(true);
            if (mainCamera == null) mainCamera = Camera.main;
        }

        if (movementScript == null)
        {
            movementScript = GetComponentInParent<IronStormMovement>();
            if (movementScript == null) movementScript = GetComponent<IronStormMovement>();
        }

        if (weaponSwitcher == null)
        {
            weaponSwitcher = GetComponentInParent<WeaponSwitcher>();
            if (weaponSwitcher == null) weaponSwitcher = GetComponentInChildren<WeaponSwitcher>(true);
            if (weaponSwitcher == null) weaponSwitcher = FindFirstObjectByType<WeaponSwitcher>();
        }

        // Tránh đặt leanPivot trùng với object có IronStormCamera (vì IronStormCamera khóa trục Z = 0)
        if (leanPivot == null || leanPivot.GetComponent<IronStormCamera>() != null)
        {
            if (mainCamera != null && mainCamera.transform.parent != null &&
                (movementScript == null || mainCamera.transform.parent != movementScript.transform))
            {
                leanPivot = mainCamera.transform.parent;
            }
            else if (mainCamera != null)
            {
                // Tự tạo object trung gian bọc ngoài MainCamera để nghiêng trục Z chính xác
                GameObject holder = new GameObject("CameraLeanPivot");
                Transform camT = mainCamera.transform;
                holder.transform.SetParent(camT.parent, false);
                holder.transform.localPosition = camT.localPosition;
                holder.transform.localRotation = camT.localRotation;
                holder.transform.localScale = Vector3.one;

                camT.SetParent(holder.transform, false);
                camT.localPosition = Vector3.zero;
                camT.localRotation = Quaternion.identity;

                leanPivot = holder.transform;
            }
            else
            {
                leanPivot = transform;
            }
        }
    }

    private void LuuTrangThaiBanDau()
    {
        if (mainCamera != null)
        {
            baseMainFov = mainCamera.fieldOfView;
        }

        if (weaponCamera != null)
        {
            baseWeaponFov = weaponCamera.fieldOfView;
        }

        if (!hasSavedLeanBase && leanPivot != null)
        {
            leanPivotBaseLocalPos = leanPivot.localPosition;
            leanPivotBaseLocalEuler = leanPivot.localEulerAngles;
            hasSavedLeanBase = true;
        }

        if (!hasSavedWeaponBase && weaponPivot != null)
        {
            weaponPivotBaseLocalPos = weaponPivot.localPosition;
            weaponPivotBaseLocalRot = weaponPivot.localRotation;
            hasSavedWeaponBase = true;
        }
    }

    private bool CoVuKhiDangCam()
    {
        if (weaponSwitcher != null)
        {
            if (weaponSwitcher.CurrentWeaponName == "UNARMED")
            {
                return false;
            }

            if (weaponSwitcher.CurrentWeaponSystem != null && weaponSwitcher.CurrentWeaponSystem.gameObject.activeInHierarchy)
                return true;

            if (weaponSwitcher.CurrentSniperWeapon != null && weaponSwitcher.CurrentSniperWeapon.gameObject.activeInHierarchy)
                return true;

            if (weaponSwitcher.CurrentMeleeWeapon != null && weaponSwitcher.CurrentMeleeWeapon.gameObject.activeInHierarchy)
                return true;

            if (weaponSwitcher.CurrentGrenadeWeapon != null && weaponSwitcher.CurrentGrenadeWeapon.gameObject.activeInHierarchy)
                return true;

            return false;
        }

        Transform rootSearch = movementScript != null ? movementScript.transform : transform.root;
        if (rootSearch == null) return false;

        if (rootSearch.GetComponentInChildren<WeaponSystem>(false) != null) return true;
        if (rootSearch.GetComponentInChildren<SniperWeapon>(false) != null) return true;
        if (rootSearch.GetComponentInChildren<MeleeWeapon>(false) != null) return true;
        if (rootSearch.GetComponentInChildren<GrenadeWeapon>(false) != null) return true;
        if (rootSearch.GetComponentInChildren<GrappleHook>(false) != null) return true;

        return false;
    }

    private bool DangCamLuuDan()
    {
        if (weaponSwitcher != null)
        {
            if (weaponSwitcher.CurrentGrenadeWeapon != null &&
                weaponSwitcher.CurrentGrenadeWeapon.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        GrenadeWeapon luuDanActive = GetComponentInChildren<GrenadeWeapon>(false);
        if (luuDanActive == null && movementScript != null)
        {
            luuDanActive = movementScript.GetComponentInChildren<GrenadeWeapon>(false);
        }

        return luuDanActive != null && luuDanActive.gameObject.activeInHierarchy;
    }

    private void Update()
    {
        CapNhatInputAim();
        CapNhatInputLean();
    }

    private void LateUpdate()
    {
        LuuTrangThaiBanDau();
        ApDungHieuUngAim();
        ApDungHieuUngLean();
    }

    private void CapNhatInputAim()
    {
        // Không cho phép Aim khi không cầm vũ khí, đang cầm lựu đạn hoặc đang Dash
        if (!CoVuKhiDangCam() || DangCamLuuDan())
        {
            isAiming = false;
            return;
        }

        bool dangDash = movementScript != null && movementScript.IsDashing;
        bool giuAim = ActiveAimAction != null && ActiveAimAction.IsPressed();
        isAiming = giuAim && !dangDash;
    }

    private void CapNhatInputLean()
    {
        bool dangDash = movementScript != null && movementScript.IsDashing;
        if (dangDash)
        {
            leanState = 0;
            return;
        }

        bool nhanTrai = ActiveLeanLeftAction != null && ActiveLeanLeftAction.WasPressedThisFrame();
        bool nhanPhai = ActiveLeanRightAction != null && ActiveLeanRightAction.WasPressedThisFrame();
        bool giuTrai = ActiveLeanLeftAction != null && ActiveLeanLeftAction.IsPressed();
        bool giuPhai = ActiveLeanRightAction != null && ActiveLeanRightAction.IsPressed();

        if (toggleLeanMode)
        {
            if (nhanTrai)
            {
                leanState = (leanState == 1) ? 0 : 1;
            }
            else if (nhanPhai)
            {
                leanState = (leanState == -1) ? 0 : -1;
            }
        }
        else
        {
            if (giuTrai && !giuPhai)
            {
                leanState = 1;
            }
            else if (giuPhai && !giuTrai)
            {
                leanState = -1;
            }
            else
            {
                leanState = 0;
            }
        }
    }

    private void ApDungHieuUngAim()
    {
        float t = 1f - Mathf.Exp(-aimFovSpeed * Time.deltaTime);

        if (mainCamera != null)
        {
            float targetFov = isAiming ? aimFov : baseMainFov;
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFov, t);
        }

        if (weaponCamera != null)
        {
            float targetWeaponFov = isAiming ? Mathf.Min(baseWeaponFov, aimFov + 8f) : baseWeaponFov;
            weaponCamera.fieldOfView = Mathf.Lerp(weaponCamera.fieldOfView, targetWeaponFov, t);
        }

        if (weaponPivot != null && hasSavedWeaponBase)
        {
            Vector3 targetPos = isAiming ? aimWeaponLocalPosition : weaponPivotBaseLocalPos;
            Quaternion targetRot = isAiming ? Quaternion.Euler(aimWeaponLocalEuler) : weaponPivotBaseLocalRot;

            weaponPivot.localPosition = Vector3.Lerp(weaponPivot.localPosition, targetPos, t);
            weaponPivot.localRotation = Quaternion.Slerp(weaponPivot.localRotation, targetRot, t);
        }
    }

    private void ApDungHieuUngLean()
    {
        if (leanPivot == null || !hasSavedLeanBase) return;

        float t = 1f - Mathf.Exp(-leanSmoothness * Time.deltaTime);
        currentLeanFactor = Mathf.Lerp(currentLeanFactor, leanState, t);

        float absLean = Mathf.Abs(currentLeanFactor);
        Vector3 offset = new Vector3(
            -currentLeanFactor * maxLeanOffsetX,
            absLean * leanDropY,
            0f
        );

        float targetZ = leanPivotBaseLocalEuler.z + (currentLeanFactor * maxLeanAngleZ);
        leanPivot.localPosition = leanPivotBaseLocalPos + offset;
        leanPivot.localEulerAngles = new Vector3(
            leanPivotBaseLocalEuler.x,
            leanPivotBaseLocalEuler.y,
            targetZ
        );
    }
}
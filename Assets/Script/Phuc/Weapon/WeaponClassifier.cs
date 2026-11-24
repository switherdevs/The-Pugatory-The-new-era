using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum WeaponCategory
{
    PrimaryWeapon = 0,   // Index 0: Vu khi chinh
    SecondaryWeapon = 1, // Index 1: Vu khi phu
    MeleeWeapon = 2,     // Index 2: Can chien
    Grenade = 3,         // Index 3: Luu dan / Flashbang
    SupportGear = 4      // Index 4: Thiet bi ho tro
}

/// <summary>
/// Thuoc tinh hien thi Dropdown danh sach Layer co san trong Unity (-1 = Giu layer goc / Tu tim).
/// </summary>
public class SingleLayerDropdownAttribute : PropertyAttribute
{
    public bool AllowKeepOriginal { get; }

    public SingleLayerDropdownAttribute(bool allowKeepOriginal = true)
    {
        AllowKeepOriginal = allowKeepOriginal;
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(SingleLayerDropdownAttribute))]
public class SingleLayerDropdownDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SingleLayerDropdownAttribute attr = (SingleLayerDropdownAttribute)attribute;

        string[] projectLayers = new string[32];
        for (int i = 0; i < 32; i++)
        {
            string layerName = LayerMask.LayerToName(i);
            projectLayers[i] = string.IsNullOrEmpty(layerName) ? $"Layer {i} (Unused)" : layerName;
        }

        string[] options;
        int selectedPopupIndex;

        if (attr.AllowKeepOriginal)
        {
            options = new string[33];
            options[0] = "Giu Layer Goc / Tu Tim";
            for (int i = 0; i < 32; i++)
            {
                options[i + 1] = projectLayers[i];
            }
            selectedPopupIndex = property.intValue < 0 ? 0 : Mathf.Clamp(property.intValue + 1, 1, 32);
        }
        else
        {
            options = projectLayers;
            selectedPopupIndex = Mathf.Clamp(property.intValue, 0, 31);
        }

        EditorGUI.BeginProperty(position, label, property);
        Rect fieldRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
        int newPopupIndex = EditorGUI.Popup(fieldRect, selectedPopupIndex, options);

        if (attr.AllowKeepOriginal)
        {
            property.intValue = (newPopupIndex == 0) ? -1 : (newPopupIndex - 1);
        }
        else
        {
            property.intValue = newPopupIndex;
        }

        EditorGUI.EndProperty();
    }
}
#endif

public class WeaponClassifier : MonoBehaviour
{
    [Header("Phan Loai Vu Khi (5 Nhom = Index 0..4)")]
    [Tooltip("Chon loai cua vu khi nay: 0 Vu khi chinh, 1 Vu khi phu, 2 Can chien, 3 Luu dan/Flashbang, 4 Thiet bi ho tro.")]
    [SerializeField] private WeaponCategory category = WeaponCategory.PrimaryWeapon;

    [Tooltip("Neu bat, script se tu doan loai vu khi dua tren component/ten khi chua tu chon.")]
    [SerializeField] private bool autoDetectIfUnset = true;

    [Header("Ban Kinh Nhat Vu Khi (Pickup Radius)")]
    [Tooltip("Bat/tat bat buoc phai nam trong ban kinh moi duoc phep nhat vu khi.")]
    [SerializeField] private bool requirePickupRadius = true;

    [Tooltip("Ban kinh nhat vu khi (don vi met). Phai gan trong khoang nay moi nhat duoc.")]
    [Min(0.1f)]
    [SerializeField] private float pickupRadius = 2.5f;

    [Tooltip("Neu bat, chi tinh khoang cach theo mat phang XZ (tranh lech do cao pivot Player/vu khi).")]
    [SerializeField] private bool useHorizontalDistanceXZ = false;

    [Tooltip("Diem tam tuy chon de do ban kinh nhat (neu de trong se dung vi tri vu khi hien tai).")]
    [SerializeField] private Transform pickupAnchorOverride;

    [Tooltip("Hien thi vong ban kinh nhat vu khi trong Scene view khi chon object.")]
    [SerializeField] private bool drawPickupGizmo = true;

    [Header("Tu Dong Doi Layer Khi Nhat / Vut Vu Khi")]
    [Tooltip("Bat/tat tu dong doi layer theo trang thai duoc Player cam hay nam ngoai.")]
    [SerializeField] private bool autoSwitchLayerOnEquipDrop = true;

    [Tooltip("Ap dung doi layer cho ca cac GameObject con ben trong vu khi.")]
    [SerializeField] private bool applyToChildren = true;

    [Tooltip("Dropdown chon Layer vu khi khi duoc Player cam / nhat len (mac dinh tu tim layer 'Weapon' neu chon 'Giu Layer Goc / Tu Tim').")]
    [SingleLayerDropdown(allowKeepOriginal: true)]
    [SerializeField] private int weaponLayerIndex = -1;

    [Tooltip("Dropdown chon Layer khi vu khi KHONG duoc Player cam / bi vut ra (mac dinh la Default = 0).")]
    [SingleLayerDropdown(allowKeepOriginal: false)]
    [SerializeField] private int unheldLayerIndex = 0;

    private bool hasManualCategory = false;
    private int defaultLayerIndex = 0;
    private int cachedInitialLayerIndex = -1;
    private bool hasCachedInitialLayer = false;
    private bool lastHeldState = false;
    private bool hasEvaluatedHeldState = false;
    private Transform[] cachedChildTransforms;

    public WeaponCategory Category => category;

    // Tra ve dung index tuong ung (0..4) de WeaponSwitcher nhan dien
    public int SlotIndex => (int)category;

    public bool IsHeldByPlayer => CheckIsHeldByPlayer();

    public bool RequirePickupRadius => requirePickupRadius;

    public float PickupRadius => Mathf.Max(0.1f, pickupRadius);

    private Vector3 PickupWorldOrigin => pickupAnchorOverride != null ? pickupAnchorOverride.position : transform.position;

    private void OnValidate()
    {
        hasManualCategory = true;
        pickupRadius = Mathf.Max(0.1f, pickupRadius);

        defaultLayerIndex = LayerMask.NameToLayer("Default");
        if (defaultLayerIndex < 0) defaultLayerIndex = 0;
        if (unheldLayerIndex < 0 || unheldLayerIndex >= 32) unheldLayerIndex = defaultLayerIndex;
    }

    private void Awake()
    {
        pickupRadius = Mathf.Max(0.1f, pickupRadius);

        defaultLayerIndex = LayerMask.NameToLayer("Default");
        if (defaultLayerIndex < 0) defaultLayerIndex = 0;
        if (unheldLayerIndex < 0 || unheldLayerIndex >= 32) unheldLayerIndex = defaultLayerIndex;

        if (!hasCachedInitialLayer)
        {
            cachedInitialLayerIndex = gameObject.layer;
            hasCachedInitialLayer = true;
        }

        if (applyToChildren && cachedChildTransforms == null)
        {
            cachedChildTransforms = GetComponentsInChildren<Transform>(true);
        }

        if (autoDetectIfUnset && !hasManualCategory)
        {
            category = DetectCategoryAutomatically();
        }

        EvaluateAndApplyLayerState(forceApply: true);
    }

    private void Start()
    {
        EvaluateAndApplyLayerState(forceApply: true);
    }

    private void OnEnable()
    {
        EvaluateAndApplyLayerState(forceApply: true);
    }

    private void OnTransformParentChanged()
    {
        // Nhan dien ngay khi Player nhat vu khi vao tay hoac vua vut vu khi ra khoi tay
        if (applyToChildren)
        {
            cachedChildTransforms = GetComponentsInChildren<Transform>(true);
        }
        EvaluateAndApplyLayerState(forceApply: true);
    }

    private void Update()
    {
        EvaluateAndApplyLayerState(forceApply: false);
    }

    /// <summary>
    /// Tinh khoang cach tu diem nhap vu khi den mot vi tri / Transform cua Player.
    /// </summary>
    public float GetDistanceTo(Vector3 worldPoint)
    {
        Vector3 origin = PickupWorldOrigin;

        if (useHorizontalDistanceXZ)
        {
            origin.y = 0f;
            worldPoint.y = 0f;
        }

        return Vector3.Distance(origin, worldPoint);
    }

    /// <summary>
    /// Kiem tra xem mot diem / Player co nam trong ban kinh nhap vu khi hay khong.
    /// Dung sqrMagnitude de toi uu hieu nang khi kiem tra thuong xuyen.
    /// </summary>
    public bool IsWithinPickupRadius(Vector3 worldPoint)
    {
        if (!requirePickupRadius) return true;

        float radius = Mathf.Max(0.1f, pickupRadius);
        Vector3 origin = PickupWorldOrigin;

        if (useHorizontalDistanceXZ)
        {
            origin.y = 0f;
            worldPoint.y = 0f;
        }

        float sqrDist = (origin - worldPoint).sqrMagnitude;
        return sqrDist <= radius * radius;
    }

    /// <summary>
    /// Kiem tra xem Player (truyen Transform hoac tu dong tim neu null) co du dieu kien nhat vu khi nay khong.
    /// Tra ve true neu: object dang active/enabled, chua bi cam boi Player, va nam trong ban kinh nhap (neu bat).
    /// </summary>
    public bool CanBePickedUp(Transform pickerTransform = null)
    {
        if (!isActiveAndEnabled) return false;

        // Vu khi dang duoc cam tren tay thi khong coi la vat pham cho nhat duoi dat
        if (CheckIsHeldByPlayer()) return false;

        if (!requirePickupRadius) return true;

        Transform resolvedPicker = ResolvePickerTransform(pickerTransform);
        if (resolvedPicker == null)
        {
            // Khong tim thay Player hop le thi chan nhat de tranh nhat tu xa ngoai tam kiem soat
            return false;
        }

        return IsWithinPickupRadius(resolvedPicker.position);
    }

    /// <summary>
    /// Ham tien ich cho he thong nhat do / WeaponSwitcher: chi cho phep nhat khi du dieu kien ban kinh.
    /// </summary>
    public bool TryValidatePickup(Transform pickerTransform, out float currentDistance)
    {
        currentDistance = float.MaxValue;

        if (!isActiveAndEnabled || CheckIsHeldByPlayer())
        {
            return false;
        }

        Transform resolvedPicker = ResolvePickerTransform(pickerTransform);
        if (resolvedPicker != null)
        {
            currentDistance = GetDistanceTo(resolvedPicker.position);
        }

        if (!requirePickupRadius)
        {
            return true;
        }

        if (resolvedPicker == null)
        {
            return false;
        }

        return IsWithinPickupRadius(resolvedPicker.position);
    }

    /// <summary>
    /// Ham tinh kiem tra nhanh xem mot GameObject vu khi co the duoc nhat boi picker hay khong.
    /// </summary>
    public static bool CanPickUpWeapon(GameObject weaponObj, Transform pickerTransform = null)
    {
        if (weaponObj == null || !weaponObj.activeInHierarchy) return false;

        WeaponClassifier classifier = weaponObj.GetComponent<WeaponClassifier>();
        if (classifier == null)
        {
            classifier = weaponObj.GetComponentInChildren<WeaponClassifier>(true);
        }

        if (classifier != null)
        {
            return classifier.CanBePickedUp(pickerTransform);
        }

        // Fallback neu vu khi chua gan WeaponClassifier: kiem tra khoang cach mac dinh 2.5m neu co picker
        Transform resolvedPicker = pickerTransform;
        if (resolvedPicker == null)
        {
            WeaponSwitcher switcher = Object.FindFirstObjectByType<WeaponSwitcher>();
            if (switcher != null) resolvedPicker = switcher.transform;
        }

        if (resolvedPicker == null && Camera.main != null)
        {
            resolvedPicker = Camera.main.transform;
        }

        if (resolvedPicker == null) return false;

        const float fallbackRadius = 2.5f;
        float sqrDist = (weaponObj.transform.position - resolvedPicker.position).sqrMagnitude;
        return sqrDist <= fallbackRadius * fallbackRadius;
    }

    /// <summary>
    /// Tu tim Transform cua Player neu he thong goi khong truyen vao.
    /// </summary>
    private Transform ResolvePickerTransform(Transform explicitPicker)
    {
        if (explicitPicker != null)
        {
            return explicitPicker;
        }

        WeaponSwitcher switcher = Object.FindFirstObjectByType<WeaponSwitcher>();
        if (switcher != null)
        {
            return switcher.transform;
        }

        CharacterController cc = Object.FindFirstObjectByType<CharacterController>();
        if (cc != null)
        {
            return cc.transform;
        }

        if (Camera.main != null)
        {
            return Camera.main.transform;
        }

        return null;
    }

    private int ResolveWeaponHeldLayer()
    {
        if (weaponLayerIndex >= 0 && weaponLayerIndex < 32)
        {
            return weaponLayerIndex;
        }

        int namedWeaponLayer = LayerMask.NameToLayer("Weapon");
        if (namedWeaponLayer >= 0)
        {
            return namedWeaponLayer;
        }

        if (cachedInitialLayerIndex >= 0 && cachedInitialLayerIndex != defaultLayerIndex)
        {
            return cachedInitialLayerIndex;
        }

        return gameObject.layer;
    }

    /// <summary>
    /// Kiem tra vu khi hien co dang duoc Player cam hay khong
    /// (co cha thuoc Player co CharacterController hoac WeaponSwitcher).
    /// </summary>
    public bool CheckIsHeldByPlayer()
    {
        if (!enabled) return false;
        if (transform.parent == null) return false;

        return GetComponentInParent<CharacterController>() != null ||
               GetComponentInParent<WeaponSwitcher>() != null;
    }

    /// <summary>
    /// Nhan dien trang thai cam/khong cam va dong bo layer hieu qua.
    /// </summary>
    public void EvaluateAndApplyLayerState(bool forceApply = false)
    {
        if (!autoSwitchLayerOnEquipDrop) return;

        bool currentlyHeld = CheckIsHeldByPlayer();
        if (!forceApply && hasEvaluatedHeldState && currentlyHeld == lastHeldState)
        {
            return;
        }

        lastHeldState = currentlyHeld;
        hasEvaluatedHeldState = true;

        int targetLayer = currentlyHeld
            ? ResolveWeaponHeldLayer()
            : (unheldLayerIndex >= 0 && unheldLayerIndex < 32 ? unheldLayerIndex : defaultLayerIndex);

        SetLayerRecursive(gameObject, targetLayer, applyToChildren);
    }

    /// <summary>
    /// Ham tinh ep cap nhat layer ngay cho vu khi khi vua nhat hoac vua vut.
    /// </summary>
    public static void RefreshWeaponLayer(GameObject weaponObj)
    {
        if (weaponObj == null) return;

        WeaponClassifier classifier = weaponObj.GetComponent<WeaponClassifier>();
        if (classifier == null)
        {
            classifier = weaponObj.GetComponentInChildren<WeaponClassifier>(true);
        }

        if (classifier != null)
        {
            classifier.EvaluateAndApplyLayerState(forceApply: true);
        }
    }

    private void SetLayerRecursive(GameObject target, int layer, bool includeChildren)
    {
        if (target == null || layer < 0 || layer >= 32) return;

        if (!includeChildren)
        {
            target.layer = layer;
            return;
        }

        if (cachedChildTransforms == null || cachedChildTransforms.Length == 0)
        {
            cachedChildTransforms = target.GetComponentsInChildren<Transform>(true);
        }

        for (int i = 0; i < cachedChildTransforms.Length; i++)
        {
            Transform t = cachedChildTransforms[i];
            if (t != null)
            {
                t.gameObject.layer = layer;
            }
        }
    }

    /// <summary>
    /// Ham tinh giup WeaponSwitcher nhan dien loai vu khi cua bat ky GameObject nao.
    /// </summary>
    public static WeaponCategory GetWeaponCategory(GameObject weaponObj)
    {
        if (weaponObj == null) return WeaponCategory.PrimaryWeapon;

        WeaponClassifier classifier = weaponObj.GetComponent<WeaponClassifier>();
        if (classifier == null)
        {
            classifier = weaponObj.GetComponentInChildren<WeaponClassifier>(true);
        }

        if (classifier != null)
        {
            return classifier.Category;
        }

        return DetectFromObject(weaponObj);
    }

    /// <summary>
    /// Tra ve index (0..4) tuong ung cua vu khi de WeaponSwitcher sap xep & kiem tra trung loai.
    /// </summary>
    public static int GetWeaponSlotIndex(GameObject weaponObj)
    {
        return (int)GetWeaponCategory(weaponObj);
    }

    private WeaponCategory DetectCategoryAutomatically()
    {
        return DetectFromObject(gameObject);
    }

    private static WeaponCategory DetectFromObject(GameObject obj)
    {
        if (obj == null) return WeaponCategory.PrimaryWeapon;

        if (obj.GetComponentInChildren<GrenadeWeapon>(true) != null)
        {
            return WeaponCategory.Grenade;
        }

        string lower = obj.name.ToLowerInvariant();

        if (obj.GetComponentInChildren<MeleeWeapon>(true) != null ||
            lower.Contains("melee") || lower.Contains("knife") || lower.Contains("sword"))
        {
            return WeaponCategory.MeleeWeapon;
        }

        if (lower.Contains("grenade") || lower.Contains("flash") || lower.Contains("flashbang") ||
            lower.Contains("luu_dan") || lower.Contains("lựu đạn"))
        {
            return WeaponCategory.Grenade;
        }

        if (obj.GetComponentInChildren<GrappleHook>(true) != null ||
            lower.Contains("support") || lower.Contains("gear") || lower.Contains("grapple"))
        {
            return WeaponCategory.SupportGear;
        }

        if (lower.Contains("secondary") || lower.Contains("pistol") || lower.Contains("sidearm"))
        {
            return WeaponCategory.SecondaryWeapon;
        }

        return WeaponCategory.PrimaryWeapon;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawPickupGizmo || !requirePickupRadius) return;

        Vector3 center = PickupWorldOrigin;
        float radius = Mathf.Max(0.1f, pickupRadius);
        Transform picker = ResolvePickerTransform(null);
        bool inRange = picker != null && IsWithinPickupRadius(picker.position);

        Handles.color = inRange ? new Color(0.15f, 0.9f, 0.35f, 0.9f) : new Color(0.2f, 0.75f, 1f, 0.85f);
        Handles.DrawWireDisc(center, Vector3.up, radius);

        Gizmos.color = inRange ? new Color(0.15f, 0.9f, 0.35f, 0.14f) : new Color(0.2f, 0.75f, 1f, 0.1f);
        Gizmos.DrawSphere(center, radius);
    }
#endif
}
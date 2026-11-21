using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum WeaponCategory
{
    PrimaryWeapon = 0,   // Index 0: Vũ khí chính
    SecondaryWeapon = 1, // Index 1: Vũ khí phụ
    MeleeWeapon = 2,     // Index 2: Cận chiến
    Grenade = 3,         // Index 3: Lựu đạn
    SupportGear = 4      // Index 4: Thiết bị hỗ trợ
}

/// <summary>
/// Thuộc tính hiển thị Dropdown danh sách Layer có sẵn trong Unity (-1 = Giữ layer gốc / Tự tìm).
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
            options[0] = "Giữ Layer Gốc / Tự Tìm";
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
    [Header("Phân Loại Vũ Khí (5 Nhóm = Index 0..4)")]
    [Tooltip("Chọn loại của vũ khí này: 0 Vũ khí chính, 1 Vũ khí phụ, 2 Cận chiến, 3 Lựu đạn, 4 Thiết bị hỗ trợ.")]
    [SerializeField] private WeaponCategory category = WeaponCategory.PrimaryWeapon;

    [Tooltip("Nếu bật, script sẽ tự đoán loại vũ khí dựa trên component/tên khi chưa tự chọn.")]
    [SerializeField] private bool autoDetectIfUnset = true;

    [Header("Tự Động Đổi Layer Khi Nhặt / Vứt Vũ Khí")]
    [Tooltip("Bật/tắt tự động đổi layer theo trạng thái được Player cầm hay nằm ngoài.")]
    [SerializeField] private bool autoSwitchLayerOnEquipDrop = true;

    [Tooltip("Áp dụng đổi layer cho cả các GameObject con bên trong vũ khí.")]
    [SerializeField] private bool applyToChildren = true;

    [Tooltip("Dropdown chọn Layer vũ khí khi được Player cầm / nhặt lên (mặc định tự tìm layer 'Weapon' nếu chọn 'Giữ Layer Gốc / Tự Tìm').")]
    [SingleLayerDropdown(allowKeepOriginal: true)]
    [SerializeField] private int weaponLayerIndex = -1;

    [Tooltip("Dropdown chọn Layer khi vũ khí KHÔNG được Player cầm / bị vứt ra (mặc định là Default = 0).")]
    [SingleLayerDropdown(allowKeepOriginal: false)]
    [SerializeField] private int unheldLayerIndex = 0;

    private bool hasManualCategory = false;
    private int defaultLayerIndex = 0;
    private int cachedInitialLayerIndex = -1;
    private bool hasCachedInitialLayer = false;
    private bool lastHeldState = false;
    private bool hasEvaluatedHeldState = false;

    public WeaponCategory Category => category;

    // Trả về đúng index tương ứng (0..4) để WeaponSwitcher nhận diện
    public int SlotIndex => (int)category;

    public bool IsHeldByPlayer => CheckIsHeldByPlayer();

    private void OnValidate()
    {
        hasManualCategory = true;
        defaultLayerIndex = LayerMask.NameToLayer("Default");
        if (defaultLayerIndex < 0) defaultLayerIndex = 0;
        if (unheldLayerIndex < 0 || unheldLayerIndex >= 32) unheldLayerIndex = defaultLayerIndex;
    }

    private void Awake()
    {
        defaultLayerIndex = LayerMask.NameToLayer("Default");
        if (defaultLayerIndex < 0) defaultLayerIndex = 0;
        if (unheldLayerIndex < 0 || unheldLayerIndex >= 32) unheldLayerIndex = defaultLayerIndex;

        if (!hasCachedInitialLayer)
        {
            cachedInitialLayerIndex = gameObject.layer;
            hasCachedInitialLayer = true;
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
        // Nhận diện ngay khi Player nhặt vũ khí vào tay hoặc vừa vứt vũ khí ra khỏi tay
        EvaluateAndApplyLayerState(forceApply: true);
    }

    private void Update()
    {
        EvaluateAndApplyLayerState(forceApply: false);
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
    /// Kiểm tra vũ khí hiện có đang được Player cầm hay không
    /// (có cha thuộc Player có CharacterController hoặc WeaponSwitcher).
    /// </summary>
    public bool CheckIsHeldByPlayer()
    {
        if (!enabled) return false;
        if (transform.parent == null) return false;

        return GetComponentInParent<CharacterController>() != null ||
               GetComponentInParent<WeaponSwitcher>() != null;
    }

    /// <summary>
    /// Nhận diện trạng thái cầm/không cầm:
    /// - Khi được Player cầm / nhặt lên: đổi về Layer vũ khí (Weapon Layer Index / 'Weapon').
    /// - Khi không được Player cầm / bị vứt: chuyển thành Unheld Layer Index (mặc định Default).
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
    /// Hàm tĩnh ép cập nhật layer ngay cho vũ khí khi vừa nhặt hoặc vừa vứt.
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

        Transform[] transforms = target.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].gameObject.layer = layer;
        }
    }

    /// <summary>
    /// Hàm tĩnh giúp WeaponSwitcher nhận diện loại vũ khí của bất kỳ GameObject nào
    /// (ưu tiên lấy từ WeaponClassifier nếu có, nếu không sẽ tự nhận diện).
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
    /// Trả về index (0..4) tương ứng của vũ khí để WeaponSwitcher sắp xếp & kiểm tra trùng loại.
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

        string lower = obj.name.ToLower();

        if (obj.GetComponentInChildren<MeleeWeapon>(true) != null ||
            lower.Contains("melee") || lower.Contains("knife") || lower.Contains("sword"))
        {
            return WeaponCategory.MeleeWeapon;
        }

        if (lower.Contains("grenade") || lower.Contains("luu_dan") || lower.Contains("lựu đạn"))
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
}
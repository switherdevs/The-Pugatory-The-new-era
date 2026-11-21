using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class WeaponUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Tham chiếu tới script WeaponSwitcher trong Scene (Sẽ tự động tìm nếu để trống).")]
    [SerializeField] private WeaponSwitcher weaponSwitcher;

    [Tooltip("Tham chiếu tới script súng bắn móc GrappleHook (Sẽ tự động tìm nếu để trống).")]
    [SerializeField] private GrappleHook grappleHook;

    [Header("UI Components (TextMeshPro)")]
    [Tooltip("TextMeshProUGUI hiển thị TÊN VŨ KHÍ hiện tại (Ví dụ: Rifle, AWP, CombatKnife, Grapple Hook).")]
    [SerializeField] private TextMeshProUGUI weaponNameText;

    [Tooltip("TextMeshProUGUI hiển thị số đạn hoặc trạng thái dây móc.")]
    [SerializeField] private TextMeshProUGUI ammoText;

    [Tooltip("TextMeshProUGUI hiển thị chế độ bắn hoặc loại vũ khí (SEMI / AUTO / SNIPER / MELEE / GRAPPLE).")]
    [SerializeField] private TextMeshProUGUI fireModeText;

    [Header("Weapon Slot Images (Mảng ô hiển thị vũ khí)")]
    [Tooltip("Mảng hiển thị các ô Image vũ khí (mặc định 3 ô). Ô nào đang được chọn sẽ tự động phóng to mượt mà.")]
    [SerializeField] private Image[] weaponSlotImages = new Image[3];

    [Tooltip("Tỉ lệ phóng to của ô vũ khí đang được chọn (Ví dụ: 1.25 là to gấp 1.25 lần).")]
    [SerializeField] private float selectedSlotScale = 1.25f;

    [Tooltip("Tốc độ phóng to / thu nhỏ mượt mà của các ô vũ khí.")]
    [SerializeField] private float slotScaleSpeed = 12f;

    [Header("Audio (Âm thanh đổi vũ khí)")]
    [Tooltip("AudioSource dùng để phát âm thanh (tự động lấy hoặc thêm nếu để trống).")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Âm thanh phát ra khi người chơi đổi vũ khí.")]
    [SerializeField] private AudioClip switchWeaponSound;

    [Range(0f, 1f)]
    [Tooltip("Âm lượng khi phát tiếng đổi vũ khí.")]
    [SerializeField] private float switchSoundVolume = 1f;

    [Header("Input Action References")]
    [Tooltip("Phím bấm New Input System để chuyển đổi chế độ bắn (Ví dụ: phím V).")]
    [SerializeField] private InputActionReference switchModeAction;

    private Vector3[] originalSlotScales;
    private int lastWeaponIndex = -1;

    private void OnEnable()
    {
        if (switchModeAction != null) switchModeAction.action.Enable();
    }

    private void OnDisable()
    {
        if (switchModeAction != null) switchModeAction.action.Disable();
    }

    private void Awake()
    {
        if (weaponSwitcher == null)
        {
            weaponSwitcher = FindFirstObjectByType<WeaponSwitcher>();
            if (weaponSwitcher == null)
            {
                Debug.LogWarning("WeaponUI không tìm thấy WeaponSwitcher nào trong Scene!");
            }
        }

        if (grappleHook == null)
        {
            grappleHook = FindFirstObjectByType<GrappleHook>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        CacheOriginalSlotScales();
    }

    private void Start()
    {
        if (weaponSwitcher != null)
        {
            lastWeaponIndex = weaponSwitcher.CurrentWeaponIndex;
        }
    }

    private void Update()
    {
        if (switchModeAction != null && switchModeAction.action.triggered)
        {
            ToggleWeaponModeViaInput();
        }

        if (weaponSwitcher == null) return;

        int currentIndex = weaponSwitcher.CurrentWeaponIndex;

        if (currentIndex != lastWeaponIndex)
        {
            if (lastWeaponIndex != -1)
            {
                PlaySwitchWeaponSound();
            }
            lastWeaponIndex = currentIndex;
        }

        UpdateWeaponSlotScales(currentIndex);

        // Lấy thông tin các loại vũ khí từ Switcher
        WeaponSystem currentWeapon = weaponSwitcher.CurrentWeaponSystem;
        SniperWeapon currentSniper = weaponSwitcher.CurrentSniperWeapon;
        MeleeWeapon currentMelee = weaponSwitcher.CurrentMeleeWeapon;

        GrappleHook activeGrapple = grappleHook;
        if (activeGrapple == null && weaponSwitcher.CurrentWeaponName != null)
        {
            activeGrapple = FindFirstObjectByType<GrappleHook>();
        }

        bool isGrappleEquipped = activeGrapple != null && activeGrapple.isActiveAndEnabled;

        if (weaponNameText != null)
        {
            weaponNameText.text = isGrappleEquipped
                ? activeGrapple.WeaponDisplayName.ToUpper()
                : weaponSwitcher.CurrentWeaponName.ToUpper();
        }

        if (currentWeapon != null)
        {
            if (ammoText != null)
            {
                ammoText.text = currentWeapon.CurrentAmmo + " / " + currentWeapon.ReserveAmmo;
            }
            if (fireModeText != null)
            {
                fireModeText.text = currentWeapon.CurrentWeaponType.ToString().ToUpper();
            }
        }
        else if (currentSniper != null)
        {
            if (ammoText != null)
            {
                ammoText.text = currentSniper.CurrentAmmo + " / " + currentSniper.ReserveAmmo;
            }
            if (fireModeText != null)
            {
                fireModeText.text = "SNIPER";
            }
        }
        else if (currentMelee != null)
        {
            if (ammoText != null)
            {
                ammoText.text = "MELEE";
            }
            if (fireModeText != null)
            {
                fireModeText.text = "CLOSE COMBAT";
            }
        }
        else if (isGrappleEquipped)
        {
            if (ammoText != null)
            {
                ammoText.text = activeGrapple.CurrentStateLabel;
            }
            if (fireModeText != null)
            {
                fireModeText.text = $"GRAPPLE ({activeGrapple.RangeInfoLabel})";
            }
        }
        else
        {
            if (ammoText != null) ammoText.text = "- / -";
            if (fireModeText != null) fireModeText.text = "";
            if (weaponNameText != null) weaponNameText.text = "UNARMED";
        }
    }

    private void CacheOriginalSlotScales()
    {
        if (weaponSlotImages == null || weaponSlotImages.Length == 0) return;

        originalSlotScales = new Vector3[weaponSlotImages.Length];
        for (int i = 0; i < weaponSlotImages.Length; i++)
        {
            originalSlotScales[i] = weaponSlotImages[i] != null
                ? weaponSlotImages[i].rectTransform.localScale
                : Vector3.one;
        }
    }

    private void UpdateWeaponSlotScales(int selectedIndex)
    {
        if (weaponSlotImages == null || weaponSlotImages.Length == 0) return;

        if (originalSlotScales == null || originalSlotScales.Length != weaponSlotImages.Length)
        {
            CacheOriginalSlotScales();
        }

        float lerpFactor = 1f - Mathf.Exp(-slotScaleSpeed * Time.unscaledDeltaTime);

        for (int i = 0; i < weaponSlotImages.Length; i++)
        {
            if (weaponSlotImages[i] == null) continue;

            Vector3 baseScale = originalSlotScales[i];
            Vector3 targetScale = (i == selectedIndex) ? baseScale * selectedSlotScale : baseScale;

            weaponSlotImages[i].rectTransform.localScale = Vector3.Lerp(
                weaponSlotImages[i].rectTransform.localScale,
                targetScale,
                lerpFactor
            );
        }
    }

    private void PlaySwitchWeaponSound()
    {
        if (audioSource != null && switchWeaponSound != null)
        {
            audioSource.PlayOneShot(switchWeaponSound, switchSoundVolume);
        }
    }

    private void ToggleWeaponModeViaInput()
    {
        if (weaponSwitcher != null && weaponSwitcher.CurrentWeaponSystem != null)
        {
            weaponSwitcher.CurrentWeaponSystem.ToggleWeaponMode();
        }
    }
}
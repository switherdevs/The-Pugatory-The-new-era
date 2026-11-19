using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class WeaponUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Tham chiếu tới script WeaponSwitcher trong Scene (Sẽ tự động tìm nếu để trống).")]
    [SerializeField] private WeaponSwitcher weaponSwitcher;

    [Header("UI Components (TextMeshPro)")]
    [Tooltip("TextMeshProUGUI hiển thị TÊN VŨ KHÍ hiện tại (Ví dụ: Rifle, AWP, CombatKnife).")]
    [SerializeField] private TextMeshProUGUI weaponNameText;

    [Tooltip("TextMeshProUGUI hiển thị số đạn (Định dạng: Đạn trong băng / Đạn dự phòng, ví dụ: 30 / 90).")]
    [SerializeField] private TextMeshProUGUI ammoText;

    [Tooltip("TextMeshProUGUI hiển thị chế độ bắn hoặc loại vũ khí (SEMI / AUTO / SNIPER / MELEE).")]
    [SerializeField] private TextMeshProUGUI fireModeText;

    [Header("Input Action References")]
    [Tooltip("Phím bấm New Input System để chuyển đổi chế độ bắn (Ví dụ: phím V).")]
    [SerializeField] private InputActionReference switchModeAction;

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
    }

    private void Update()
    {
        // Chuyển đổi chế độ bắn (chỉ áp dụng cho súng thường)
        if (switchModeAction != null && switchModeAction.action.triggered)
        {
            ToggleWeaponModeViaInput();
        }

        if (weaponSwitcher == null) return;

        // 1. Cập nhật tên vũ khí lên UI
        if (weaponNameText != null)
        {
            weaponNameText.text = weaponSwitcher.CurrentWeaponName.ToUpper();
        }

        // Lấy thông tin các loại vũ khí từ Switcher
        WeaponSystem currentWeapon = weaponSwitcher.CurrentWeaponSystem;
        SniperWeapon currentSniper = weaponSwitcher.CurrentSniperWeapon;
        MeleeWeapon currentMelee = weaponSwitcher.CurrentMeleeWeapon;

        if (currentWeapon != null)
        {
            // Trường hợp: Súng trường / Súng lục (Súng thường)
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
            // Trường hợp: Súng tỉa (Sniper)
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
            // Trường hợp: Vũ khí cận chiến (Dao, Kiếm...) -> Không dùng đạn
            if (ammoText != null)
            {
                ammoText.text = "MELEE"; // Hoặc để trống nếu không muốn hiển thị
            }
            if (fireModeText != null)
            {
                fireModeText.text = "CLOSE COMBAT";
            }
        }
        else
        {
            // Không cầm vũ khí nào
            if (ammoText != null) ammoText.text = "- / -";
            if (fireModeText != null) fireModeText.text = "";
            if (weaponNameText != null) weaponNameText.text = "UNARMED";
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
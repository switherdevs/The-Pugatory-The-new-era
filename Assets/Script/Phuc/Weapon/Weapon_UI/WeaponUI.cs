using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// Hien thi ten vu khi, so luong dan/luu dan, che do ban va phong to o vu khi dang chon
public class WeaponUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Tham chieu toi script WeaponSwitcher trong Scene (Se tu dong tim neu de trong).")]
    [SerializeField] private WeaponSwitcher weaponSwitcher;

    [Tooltip("Tham chieu toi script sung ban moc GrappleHook (Se tu dong tim neu de trong).")]
    [SerializeField] private GrappleHook grappleHook;

    [Header("UI Components (TextMeshPro)")]
    [Tooltip("TextMeshProUGUI hien thi TEN VU KHI hien tai (Vi du: Rifle, AWP, CombatKnife, Frag Grenade, Grapple Hook).")]
    [SerializeField] private TextMeshProUGUI weaponNameText;

    [Tooltip("TextMeshProUGUI hien thi so dan, so luu dan hoac trang thai day moc.")]
    [SerializeField] private TextMeshProUGUI ammoText;

    [Tooltip("TextMeshProUGUI hien thi che do ban hoac loai vu khi (SEMI / AUTO / SNIPER / MELEE / GRENADE / GRAPPLE).")]
    [SerializeField] private TextMeshProUGUI fireModeText;

    [Header("Weapon Slot Images (Mang o hien thi vu khi 0..4)")]
    [Tooltip("Mang hien thi cac o Image vu khi (5 o tuong ung 0..4). O nao dang duoc chon se tu dong phong to muot ma.")]
    [SerializeField] private Image[] weaponSlotImages = new Image[WeaponSwitcher.MaxWeaponSlots];

    [Tooltip("Ti le phong to cua o vu khi dang duoc chon (Vi du: 1.25 la to gap 1.25 lan).")]
    [SerializeField] private float selectedSlotScale = 1.25f;

    [Tooltip("Toc do phong to / thu nho muot ma cua cac o vu khi.")]
    [SerializeField] private float slotScaleSpeed = 12f;

    [Range(0.1f, 1f)]
    [Tooltip("Do trong suot (Alpha) cua o vu khi khi trong hoac da het dan/luu dan.")]
    [SerializeField] private float alphaOTrongHoacHetDan = 0.35f;

    [Header("Audio (Am thanh doi vu khi)")]
    [Tooltip("AudioSource dung de phat am thanh (tu dong lay hoac them neu de trong).")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Am thanh phat ra khi nguoi choi doi vu khi.")]
    [SerializeField] private AudioClip switchWeaponSound;

    [Range(0f, 1f)]
    [Tooltip("Am luong khi phat tieng doi vu khi.")]
    [SerializeField] private float switchSoundVolume = 1f;

    [Header("Input Action References")]
    [Tooltip("Phim bam New Input System de chuyen doi che do ban (Vi du: phim V).")]
    [SerializeField] private InputActionReference switchModeAction;

    private Vector3[] originalSlotScales;
    private int lastWeaponIndex = -1;

    private void OnEnable()
    {
        if (switchModeAction != null && switchModeAction.action != null) switchModeAction.action.Enable();
    }

    private void OnDisable()
    {
        if (switchModeAction != null && switchModeAction.action != null) switchModeAction.action.Disable();
    }

    private void Awake()
    {
        if (weaponSwitcher == null)
        {
            weaponSwitcher = FindFirstObjectByType<WeaponSwitcher>();
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
        if (switchModeAction != null && switchModeAction.action != null && switchModeAction.action.triggered)
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

        UpdateWeaponSlotScalesAndAlpha(currentIndex);

        // Lay thong tin cac loai vu khi tu WeaponSwitcher
        WeaponSystem currentWeapon = weaponSwitcher.CurrentWeaponSystem;
        SniperWeapon currentSniper = weaponSwitcher.CurrentSniperWeapon;
        MeleeWeapon currentMelee = weaponSwitcher.CurrentMeleeWeapon;
        GrenadeWeapon currentGrenade = weaponSwitcher.CurrentGrenadeWeapon;

        GrappleHook activeGrapple = grappleHook;
        if (activeGrapple == null && weaponSwitcher.CurrentWeaponName != null)
        {
            activeGrapple = FindFirstObjectByType<GrappleHook>();
        }

        bool isGrappleEquipped = activeGrapple != null && activeGrapple.isActiveAndEnabled;

        if (currentGrenade != null)
        {
            if (weaponNameText != null)
            {
                weaponNameText.text = currentGrenade.WeaponDisplayName.ToUpper();
            }
            if (ammoText != null)
            {
                ammoText.text = currentGrenade.CurrentStateLabel;
            }
            if (fireModeText != null)
            {
                fireModeText.text = currentGrenade.ModeLabel;
            }
        }
        else if (currentWeapon != null)
        {
            if (weaponNameText != null)
            {
                weaponNameText.text = weaponSwitcher.CurrentWeaponName.ToUpper();
            }
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
            if (weaponNameText != null)
            {
                weaponNameText.text = weaponSwitcher.CurrentWeaponName.ToUpper();
            }
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
            if (weaponNameText != null)
            {
                weaponNameText.text = weaponSwitcher.CurrentWeaponName.ToUpper();
            }
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
            if (weaponNameText != null)
            {
                weaponNameText.text = activeGrapple.WeaponDisplayName.ToUpper();
            }
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

    private void UpdateWeaponSlotScalesAndAlpha(int selectedIndex)
    {
        if (weaponSlotImages == null || weaponSlotImages.Length == 0) return;

        if (originalSlotScales == null || originalSlotScales.Length != weaponSlotImages.Length)
        {
            CacheOriginalSlotScales();
        }

        float lerpFactor = 1f - Mathf.Exp(-slotScaleSpeed * Time.unscaledDeltaTime);

        for (int i = 0; i < weaponSlotImages.Length; i++)
        {
            Image slotImage = weaponSlotImages[i];
            if (slotImage == null) continue;

            bool slotKhaDung = weaponSwitcher.CoTheChonSlot(i);
            Vector3 baseScale = originalSlotScales[i];
            Vector3 targetScale = (i == selectedIndex && slotKhaDung) ? baseScale * selectedSlotScale : baseScale;

            slotImage.rectTransform.localScale = Vector3.Lerp(
                slotImage.rectTransform.localScale,
                targetScale,
                lerpFactor
            );

            // Lam mo nhe o UI neu slot trong hoac da het luu dan
            Color mauHienTai = slotImage.color;
            mauHienTai.a = slotKhaDung ? 1f : alphaOTrongHoacHetDan;
            slotImage.color = mauHienTai;
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
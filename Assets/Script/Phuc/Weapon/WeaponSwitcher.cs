using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class WeaponSwitcher : MonoBehaviour
{
    [Header("Weapon Settings")]
    [Tooltip("Mảng chứa các GameObject vũ khí (Súng, Dao, Kiếm...). Kéo thả vào đây.")]
    [SerializeField] private GameObject[] weapons;

    [Header("Input Action")]
    [Tooltip("Kéo InputAction loại Vector2 (hoặc Mouse Scroll) từ Input Actions Asset vào đây.")]
    [SerializeField] private InputActionReference scrollAction;

    [Header("=== 1. HOLSTER SETTINGS (CẤT VŨ KHÍ) ===")]
    [Tooltip("Độ dịch chuyển vị trí Y khi cất súng (Nhập số âm tùy ý, ví dụ: -0.4).")]
    [SerializeField] private float holsterOffsetY = -0.4f;

    [Tooltip("Góc nghiêng Z khi cất súng (Ví dụ: 30 độ).")]
    [SerializeField] private float holsterOffsetZ = 30f;

    [Tooltip("Thời gian thực hiện cất súng (giây).")]
    [SerializeField] private float holsterDuration = 0.2f;

    [Header("=== 2. EQUIP SETTINGS (RÚT VŨ KHÍ) ===")]
    [Tooltip("Độ dịch chuyển vị trí Y khi rút súng từ dưới lên (Nhập số âm tùy ý, ví dụ: -0.4).")]
    [SerializeField] private float equipOffsetY = -0.4f;

    [Tooltip("Góc nghiêng Z khi bắt đầu rút súng (Ví dụ: -30 độ).")]
    [SerializeField] private float equipOffsetZ = -30f;

    [Tooltip("Thời gian thực hiện rút súng (giây).")]
    [SerializeField] private float equipDuration = 0.2f;

    private int currentWeaponIndex = 0;

    // Hỗ trợ lưu trữ 3 loại component vũ khí: Súng thường, Súng tỉa, Cận chiến
    private WeaponSystem[] weaponSystems;
    private SniperWeapon[] sniperWeapons;
    private MeleeWeapon[] meleeWeapons;

    private Vector3[] originalLocalPositions;
    private Quaternion[] originalLocalRotations;
    private Coroutine switchCoroutine;
    private bool isSwitchesInProgress = false;

    // Các Property trả về vũ khí hiện tại để các script khác truy xuất
    public WeaponSystem CurrentWeaponSystem => (weaponSystems != null && currentWeaponIndex >= 0 && currentWeaponIndex < weaponSystems.Length) ? weaponSystems[currentWeaponIndex] : null;
    public SniperWeapon CurrentSniperWeapon => (sniperWeapons != null && currentWeaponIndex >= 0 && currentWeaponIndex < sniperWeapons.Length) ? sniperWeapons[currentWeaponIndex] : null;
    public MeleeWeapon CurrentMeleeWeapon => (meleeWeapons != null && currentWeaponIndex >= 0 && currentWeaponIndex < meleeWeapons.Length) ? meleeWeapons[currentWeaponIndex] : null;

    // Property lấy tên của GameObject vũ khí hiện tại để hiển thị lên UI
    public string CurrentWeaponName
    {
        get
        {
            if (weapons != null && currentWeaponIndex >= 0 && currentWeaponIndex < weapons.Length && weapons[currentWeaponIndex] != null)
            {
                return weapons[currentWeaponIndex].name;
            }
            return "UNKNOWN";
        }
    }

    private void OnEnable()
    {
        if (scrollAction != null) scrollAction.action.Enable();
    }

    private void OnDisable()
    {
        if (scrollAction != null) scrollAction.action.Disable();
    }

    private void Start()
    {
        if (weapons != null && weapons.Length > 0)
        {
            weaponSystems = new WeaponSystem[weapons.Length];
            sniperWeapons = new SniperWeapon[weapons.Length];
            meleeWeapons = new MeleeWeapon[weapons.Length];
            originalLocalPositions = new Vector3[weapons.Length];
            originalLocalRotations = new Quaternion[weapons.Length];

            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] != null)
                {
                    weapons[i].SetActive(true);

                    // Lưu lại vị trí và góc xoay chuẩn gốc
                    originalLocalPositions[i] = weapons[i].transform.localPosition;
                    originalLocalRotations[i] = weapons[i].transform.localRotation;

                    // Quét linh hoạt component: Súng thường, Súng tỉa hoặc Cận chiến
                    weaponSystems[i] = weapons[i].GetComponent<WeaponSystem>();
                    sniperWeapons[i] = weapons[i].GetComponent<SniperWeapon>();
                    meleeWeapons[i] = weapons[i].GetComponent<MeleeWeapon>();

                    if (weaponSystems[i] == null && sniperWeapons[i] == null && meleeWeapons[i] == null)
                    {
                        Debug.LogWarning($"GameObject vũ khí '{weapons[i].name}' không tìm thấy component súng hoặc cận chiến!");
                    }

                    weapons[i].SetActive(false);
                }
            }

            // Vào game: Kích hoạt ngay vũ khí đầu tiên với animation rút lên
            currentWeaponIndex = 0;
            if (weapons[currentWeaponIndex] != null)
            {
                weapons[currentWeaponIndex].SetActive(true);
                StartCoroutine(PlayEquipAnimation(weapons[currentWeaponIndex].transform, originalLocalPositions[currentWeaponIndex], originalLocalRotations[currentWeaponIndex]));
            }
        }
    }

    private void Update()
    {
        if (isSwitchesInProgress || weapons == null || weapons.Length == 0) return;

        float scrollValue = 0f;

        if (scrollAction != null && scrollAction.action != null)
        {
            Vector2 scrollInput = scrollAction.action.ReadValue<Vector2>();
            scrollValue = scrollInput.y;
        }
        else if (Mouse.current != null)
        {
            scrollValue = Mouse.current.scroll.ReadValue().y;
        }

        if (Mathf.Abs(scrollValue) > 0.01f)
        {
            int nextIndex = currentWeaponIndex;

            if (scrollValue > 0f)
            {
                nextIndex--;
                if (nextIndex < 0) nextIndex = weapons.Length - 1;
            }
            else if (scrollValue < 0f)
            {
                nextIndex++;
                if (nextIndex >= weapons.Length) nextIndex = 0;
            }

            if (nextIndex != currentWeaponIndex)
            {
                if (switchCoroutine != null) StopCoroutine(switchCoroutine);
                switchCoroutine = StartCoroutine(SwitchWeaponSequence(currentWeaponIndex, nextIndex));
                currentWeaponIndex = nextIndex;
            }
        }
    }

    private IEnumerator SwitchWeaponSequence(int oldIndex, int newIndex)
    {
        isSwitchesInProgress = true;

        GameObject oldWeapon = weapons[oldIndex];
        GameObject newWeapon = weapons[newIndex];

        // GIAI ĐOẠN 1: CẤT VŨ KHÍ CŨ
        if (oldWeapon != null && oldWeapon.activeSelf)
        {
            Transform oldTrans = oldWeapon.transform;
            Vector3 targetPos = originalLocalPositions[oldIndex];
            Quaternion targetRot = originalLocalRotations[oldIndex];

            Vector3 holsterEndPos = targetPos + new Vector3(0f, holsterOffsetY, 0f);
            Quaternion holsterEndRot = targetRot * Quaternion.Euler(0f, 0f, holsterOffsetZ);

            float elapsedTime = 0f;
            while (elapsedTime < holsterDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / holsterDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                oldTrans.localPosition = Vector3.Lerp(targetPos, holsterEndPos, smoothT);
                oldTrans.localRotation = Quaternion.Lerp(targetRot, holsterEndRot, smoothT);

                yield return null;
            }

            oldWeapon.SetActive(false);
            oldTrans.localPosition = targetPos;
            oldTrans.localRotation = targetRot;
        }

        // GIAI ĐOẠN 2: RÚT VŨ KHÍ MỚI
        if (newWeapon != null)
        {
            Transform newTrans = newWeapon.transform;
            Vector3 targetPos = originalLocalPositions[newIndex];
            Quaternion targetRot = originalLocalRotations[newIndex];

            Vector3 equipStartPos = targetPos + new Vector3(0f, equipOffsetY, 0f);
            Quaternion equipStartRot = targetRot * Quaternion.Euler(0f, 0f, equipOffsetZ);

            newTrans.localPosition = equipStartPos;
            newTrans.localRotation = equipStartRot;
            newWeapon.SetActive(true);

            float elapsedTime = 0f;
            while (elapsedTime < equipDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / equipDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                newTrans.localPosition = Vector3.Lerp(equipStartPos, targetPos, smoothT);
                newTrans.localRotation = Quaternion.Lerp(equipStartRot, targetRot, smoothT);

                yield return null;
            }

            newTrans.localPosition = targetPos;
            newTrans.localRotation = targetRot;
        }

        isSwitchesInProgress = false;
    }

    private IEnumerator PlayEquipAnimation(Transform weaponTransform, Vector3 targetPos, Quaternion targetRot)
    {
        isSwitchesInProgress = true;

        Vector3 equipStartPos = targetPos + new Vector3(0f, equipOffsetY, 0f);
        Quaternion equipStartRot = targetRot * Quaternion.Euler(0f, 0f, equipOffsetZ);

        weaponTransform.localPosition = equipStartPos;
        weaponTransform.localRotation = equipStartRot;

        float elapsedTime = 0f;
        while (elapsedTime < equipDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / equipDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            weaponTransform.localPosition = Vector3.Lerp(equipStartPos, targetPos, smoothT);
            weaponTransform.localRotation = Quaternion.Lerp(equipStartRot, targetRot, smoothT);

            yield return null;
        }

        weaponTransform.localPosition = targetPos;
        weaponTransform.localRotation = targetRot;
        isSwitchesInProgress = false;
    }
}
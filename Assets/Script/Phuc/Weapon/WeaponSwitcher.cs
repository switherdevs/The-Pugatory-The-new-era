using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class WeaponSwitcher : MonoBehaviour
{
    public const int MaxWeaponSlots = 5; // 0: Chính, 1: Phụ, 2: Cận chiến, 3: Lựu đạn, 4: Hỗ trợ

    [Header("Weapon Settings (5 Index Cố Định: 0..4)")]
    [Tooltip("Mảng 5 ô: [0] Vũ khí chính, [1] Vũ khí phụ, [2] Cận chiến, [3] Lựu đạn, [4] Thiết bị hỗ trợ.")]
    [SerializeField] private GameObject[] weapons = new GameObject[MaxWeaponSlots];

    [Tooltip("Transform chứa vũ khí khi cầm trên tay (để trống sẽ tự lấy chính GameObject này).")]
    [SerializeField] private Transform weaponHolder;

    [Header("Input Action")]
    [Tooltip("Kéo InputAction loại Vector2 (hoặc Mouse Scroll) từ Input Actions Asset vào đây.")]
    [SerializeField] private InputActionReference scrollAction;

    [Tooltip("Phím nhặt vũ khí bằng New Input System (để trống sẽ tự dùng phím E).")]
    [SerializeField] private InputActionReference pickupWeaponAction;

    [Tooltip("Phím vứt vũ khí đang cầm bằng New Input System (để trống sẽ tự dùng phím G).")]
    [SerializeField] private InputActionReference dropWeaponAction;

    [Header("Pickup & Drop Settings")]
    [SerializeField] private float pickupRange = 3.5f;
    [SerializeField] private LayerMask pickupLayerMask = ~0;
    [SerializeField] private Vector3 defaultEquippedLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 defaultEquippedLocalEuler = Vector3.zero;
    [SerializeField] private float dropForwardForce = 4.5f;
    [SerializeField] private float dropUpwardForce = 2.0f;

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

    private WeaponSystem[] weaponSystems;
    private SniperWeapon[] sniperWeapons;
    private MeleeWeapon[] meleeWeapons;

    private Vector3[] originalLocalPositions;
    private Quaternion[] originalLocalRotations;
    private Vector3[] originalLocalScales;
    private Coroutine switchCoroutine;
    private bool isSwitchesInProgress = false;

    private InputAction runtimePickupAction;
    private InputAction runtimeDropAction;

    private InputAction ActivePickupAction =>
        pickupWeaponAction != null && pickupWeaponAction.action != null ? pickupWeaponAction.action : runtimePickupAction;

    private InputAction ActiveDropAction =>
        dropWeaponAction != null && dropWeaponAction.action != null ? dropWeaponAction.action : runtimeDropAction;

    public int CurrentWeaponIndex => currentWeaponIndex;

    public WeaponSystem CurrentWeaponSystem => (weaponSystems != null && currentWeaponIndex >= 0 && currentWeaponIndex < weaponSystems.Length) ? weaponSystems[currentWeaponIndex] : null;
    public SniperWeapon CurrentSniperWeapon => (sniperWeapons != null && currentWeaponIndex >= 0 && currentWeaponIndex < sniperWeapons.Length) ? sniperWeapons[currentWeaponIndex] : null;
    public MeleeWeapon CurrentMeleeWeapon => (meleeWeapons != null && currentWeaponIndex >= 0 && currentWeaponIndex < meleeWeapons.Length) ? meleeWeapons[currentWeaponIndex] : null;

    public string CurrentWeaponName
    {
        get
        {
            if (weapons != null && currentWeaponIndex >= 0 && currentWeaponIndex < weapons.Length && weapons[currentWeaponIndex] != null)
            {
                return weapons[currentWeaponIndex].name;
            }
            return "UNARMED";
        }
    }

    private void OnValidate()
    {
        // Đảm bảo mảng vũ khí mặc định luôn chỉ có 5 index (0..4)
        if (weapons == null || weapons.Length != MaxWeaponSlots)
        {
            GameObject[] resized = new GameObject[MaxWeaponSlots];
            if (weapons != null)
            {
                int count = Mathf.Min(weapons.Length, MaxWeaponSlots);
                for (int i = 0; i < count; i++)
                {
                    resized[i] = weapons[i];
                }
            }
            weapons = resized;
        }
    }

    private void Awake()
    {
        OnValidate();

        if (weaponHolder == null)
        {
            weaponHolder = transform;
        }

        if (pickupWeaponAction == null)
        {
            runtimePickupAction = new InputAction("PickupWeapon", InputActionType.Button, "<Keyboard>/e");
        }

        if (dropWeaponAction == null)
        {
            runtimeDropAction = new InputAction("DropWeapon", InputActionType.Button, "<Keyboard>/g");
        }
    }

    private void OnEnable()
    {
        if (scrollAction != null && scrollAction.action != null) scrollAction.action.Enable();
        ActivePickupAction?.Enable();
        ActiveDropAction?.Enable();
    }

    private void OnDisable()
    {
        if (scrollAction != null && scrollAction.action != null) scrollAction.action.Disable();
        ActivePickupAction?.Disable();
        ActiveDropAction?.Disable();
    }

    private void OnDestroy()
    {
        if (pickupWeaponAction == null && runtimePickupAction != null) runtimePickupAction.Dispose();
        if (dropWeaponAction == null && runtimeDropAction != null) runtimeDropAction.Dispose();
    }

    private void Start()
    {
        ArrangeWeaponsIntoClassifiedSlots();
        RebuildWeaponCaches(initialSetup: true);

        int firstIdx = FindFirstValidWeaponIndex(0);
        currentWeaponIndex = firstIdx >= 0 ? firstIdx : 0;

        if (firstIdx >= 0 && weapons[firstIdx] != null)
        {
            weapons[firstIdx].SetActive(true);
            StartCoroutine(PlayEquipAnimation(
                weapons[firstIdx].transform,
                originalLocalPositions[firstIdx],
                originalLocalRotations[firstIdx]
            ));
        }
    }

    /// <summary>
    /// Sắp xếp các vũ khí ban đầu vào đúng index phân loại (0..4) của chúng
    /// </summary>
    private void ArrangeWeaponsIntoClassifiedSlots()
    {
        GameObject[] arranged = new GameObject[MaxWeaponSlots];

        for (int i = 0; i < MaxWeaponSlots; i++)
        {
            GameObject w = weapons[i];
            if (w == null) continue;

            int classifiedIndex = WeaponClassifier.GetWeaponSlotIndex(w);
            classifiedIndex = Mathf.Clamp(classifiedIndex, 0, MaxWeaponSlots - 1);

            if (arranged[classifiedIndex] == null)
            {
                arranged[classifiedIndex] = w;
            }
            else if (arranged[i] == null)
            {
                arranged[i] = w;
            }
            else
            {
                Debug.LogWarning($"Slot [{classifiedIndex}] đã có vũ khí, bỏ qua '{w.name}' trùng loại.");
            }
        }

        weapons = arranged;
    }

    private void Update()
    {
        if (isSwitchesInProgress) return;

        if (ActivePickupAction != null && ActivePickupAction.WasPressedThisFrame())
        {
            TryPickupNearbyWeapon();
            return;
        }

        if (ActiveDropAction != null && ActiveDropAction.WasPressedThisFrame())
        {
            TryDropCurrentWeapon();
            return;
        }

        if (weapons == null || weapons.Length == 0) return;

        int numberKeyIndex = GetNumberKeyWeaponIndex();
        if (numberKeyIndex >= 0 && numberKeyIndex < weapons.Length)
        {
            StartSwitchToWeapon(numberKeyIndex);
            return;
        }

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
            int nextIndex = GetNextValidWeaponIndex(currentWeaponIndex, scrollValue > 0f ? -1 : 1);
            if (nextIndex >= 0)
            {
                StartSwitchToWeapon(nextIndex);
            }
        }
    }

    public bool TryPickupNearbyWeapon()
    {
        Transform searchOrigin = Camera.main != null ? Camera.main.transform : transform;
        Collider[] hits = Physics.OverlapSphere(searchOrigin.position, pickupRange, pickupLayerMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        GameObject bestCandidate = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            GameObject candidate = ResolvePickupWeaponObject(hits[i].gameObject);
            if (candidate == null || IsAlreadyOwned(candidate)) continue;

            float dist = Vector3.Distance(searchOrigin.position, candidate.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCandidate = candidate;
            }
        }

        if (bestCandidate == null) return false;
        return EquipPickedUpWeapon(bestCandidate);
    }

    public bool EquipPickedUpWeapon(GameObject pickedWeapon)
    {
        if (pickedWeapon == null || IsAlreadyOwned(pickedWeapon)) return false;

        WeaponCategory category = WeaponClassifier.GetWeaponCategory(pickedWeapon);
        int targetIndex = (int)category;

        // Nếu index đó đã có súng/vũ khí rồi thì không thể nhặt thêm vũ khí cùng loại
        if (targetIndex < 0 || targetIndex >= MaxWeaponSlots || weapons[targetIndex] != null)
        {
            Debug.LogWarning($"Index [{targetIndex}] ({category}) đã có vũ khí! Không thể nhặt thêm vũ khí cùng loại.");
            return false;
        }

        if (weaponHolder == null) weaponHolder = transform;

        Rigidbody rb = pickedWeapon.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Collider[] cols = pickedWeapon.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            cols[i].enabled = false;
        }

        pickedWeapon.transform.SetParent(weaponHolder, false);
        pickedWeapon.transform.localPosition = defaultEquippedLocalPosition;
        pickedWeapon.transform.localRotation = Quaternion.Euler(defaultEquippedLocalEuler);

        weapons[targetIndex] = pickedWeapon;
        RebuildWeaponCaches(initialSetup: false);

        for (int i = 0; i < MaxWeaponSlots; i++)
        {
            if (weapons[i] != null)
            {
                weapons[i].SetActive(i == targetIndex);
            }
        }

        int previousIndex = currentWeaponIndex;
        currentWeaponIndex = targetIndex;

        if (previousIndex >= 0 && previousIndex < MaxWeaponSlots && previousIndex != targetIndex && weapons[previousIndex] != null)
        {
            if (switchCoroutine != null) StopCoroutine(switchCoroutine);
            switchCoroutine = StartCoroutine(SwitchWeaponSequence(previousIndex, targetIndex));
        }
        else
        {
            pickedWeapon.SetActive(true);
            if (switchCoroutine != null) StopCoroutine(switchCoroutine);
            switchCoroutine = StartCoroutine(PlayEquipAnimation(
                pickedWeapon.transform,
                originalLocalPositions[targetIndex],
                originalLocalRotations[targetIndex]
            ));
        }

        return true;
    }

    public bool TryDropCurrentWeapon()
    {
        if (weapons == null || weapons.Length == 0) return false;
        if (currentWeaponIndex < 0 || currentWeaponIndex >= MaxWeaponSlots) return false;

        GameObject weaponToDrop = weapons[currentWeaponIndex];
        if (weaponToDrop == null) return false;

        if (switchCoroutine != null)
        {
            StopCoroutine(switchCoroutine);
            switchCoroutine = null;
            isSwitchesInProgress = false;
        }

        int droppedIndex = currentWeaponIndex;
        Transform dropRef = Camera.main != null ? Camera.main.transform : transform;
        Vector3 dropPos = dropRef.position + dropRef.forward * 1.1f + Vector3.up * 0.1f;
        Vector3 dropDir = (dropRef.forward * dropForwardForce) + (Vector3.up * dropUpwardForce);

        weaponToDrop.SetActive(true);
        weaponToDrop.transform.SetParent(null, true);
        weaponToDrop.transform.position = dropPos;
        weaponToDrop.transform.rotation = Quaternion.LookRotation(dropRef.forward, Vector3.up);

        if (originalLocalScales != null && droppedIndex < originalLocalScales.Length)
        {
            weaponToDrop.transform.localScale = originalLocalScales[droppedIndex];
        }

        Collider[] cols = weaponToDrop.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            cols[i].enabled = true;
        }

        Rigidbody rb = weaponToDrop.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = weaponToDrop.AddComponent<Rigidbody>();
        }
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(dropDir, ForceMode.Impulse);

        // Giải phóng index để có thể nhặt vũ khí mới cùng loại
        weapons[droppedIndex] = null;
        RebuildWeaponCaches(initialSetup: false);

        int nextIdx = GetNextValidWeaponIndex(droppedIndex, 1);
        if (nextIdx >= 0)
        {
            currentWeaponIndex = nextIdx;
            weapons[currentWeaponIndex].SetActive(true);
            weapons[currentWeaponIndex].transform.localPosition = originalLocalPositions[currentWeaponIndex];
            weapons[currentWeaponIndex].transform.localRotation = originalLocalRotations[currentWeaponIndex];
        }

        return true;
    }

    private GameObject ResolvePickupWeaponObject(GameObject hitObj)
    {
        if (hitObj == null || hitObj.transform.IsChildOf(transform.root)) return null;

        Transform current = hitObj.transform;
        while (current != null)
        {
            if (current.GetComponent<WeaponClassifier>() != null ||
                current.GetComponent<WeaponSystem>() != null ||
                current.GetComponent<SniperWeapon>() != null ||
                current.GetComponent<MeleeWeapon>() != null ||
                current.GetComponent<GrappleHook>() != null)
            {
                return current.gameObject;
            }
            current = current.parent;
        }

        return null;
    }

    private bool IsAlreadyOwned(GameObject candidate)
    {
        if (weapons == null || candidate == null) return false;
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] == candidate) return true;
        }
        return false;
    }

    private void RebuildWeaponCaches(bool initialSetup)
    {
        weaponSystems = new WeaponSystem[MaxWeaponSlots];
        sniperWeapons = new SniperWeapon[MaxWeaponSlots];
        meleeWeapons = new MeleeWeapon[MaxWeaponSlots];
        originalLocalPositions = new Vector3[MaxWeaponSlots];
        originalLocalRotations = new Quaternion[MaxWeaponSlots];
        originalLocalScales = new Vector3[MaxWeaponSlots];

        for (int i = 0; i < MaxWeaponSlots; i++)
        {
            if (weapons[i] != null)
            {
                bool wasActive = weapons[i].activeSelf;
                weapons[i].SetActive(true);

                originalLocalPositions[i] = weapons[i].transform.localPosition;
                originalLocalRotations[i] = weapons[i].transform.localRotation;
                originalLocalScales[i] = weapons[i].transform.localScale;

                weaponSystems[i] = weapons[i].GetComponent<WeaponSystem>();
                sniperWeapons[i] = weapons[i].GetComponent<SniperWeapon>();
                meleeWeapons[i] = weapons[i].GetComponent<MeleeWeapon>();

                if (initialSetup && weaponSystems[i] == null && sniperWeapons[i] == null && meleeWeapons[i] == null && weapons[i].GetComponent<WeaponClassifier>() == null)
                {
                    Debug.LogWarning($"GameObject vũ khí '{weapons[i].name}' chưa có WeaponClassifier hoặc component vũ khí!");
                }

                weapons[i].SetActive(initialSetup ? false : wasActive);
            }
            else
            {
                originalLocalPositions[i] = defaultEquippedLocalPosition;
                originalLocalRotations[i] = Quaternion.Euler(defaultEquippedLocalEuler);
                originalLocalScales[i] = Vector3.one;
            }
        }
    }

    private int FindFirstValidWeaponIndex(int startIndex)
    {
        int clampedStart = Mathf.Clamp(startIndex, 0, MaxWeaponSlots - 1);
        for (int i = clampedStart; i < MaxWeaponSlots; i++)
        {
            if (weapons[i] != null) return i;
        }
        for (int i = 0; i < clampedStart; i++)
        {
            if (weapons[i] != null) return i;
        }
        return -1;
    }

    private int GetNextValidWeaponIndex(int fromIndex, int step)
    {
        int idx = Mathf.Clamp(fromIndex, 0, MaxWeaponSlots - 1);
        for (int attempt = 0; attempt < MaxWeaponSlots; attempt++)
        {
            idx += step;
            if (idx < 0) idx = MaxWeaponSlots - 1;
            else if (idx >= MaxWeaponSlots) idx = 0;

            if (weapons[idx] != null) return idx;
        }
        return -1;
    }

    private int GetNumberKeyWeaponIndex()
    {
        for (int i = 0; i < MaxWeaponSlots; i++)
        {
            KeyCode key = KeyCode.Alpha1 + i;
            if (Input.GetKeyDown(key))
            {
                return i;
            }
        }
        return -1;
    }

    private void StartSwitchToWeapon(int targetIndex)
    {
        if (weapons == null || targetIndex < 0 || targetIndex >= MaxWeaponSlots) return;
        if (targetIndex == currentWeaponIndex) return;
        if (weapons[targetIndex] == null) return;

        if (switchCoroutine != null) StopCoroutine(switchCoroutine);
        switchCoroutine = StartCoroutine(SwitchWeaponSequence(currentWeaponIndex, targetIndex));
        currentWeaponIndex = targetIndex;
    }

    private IEnumerator SwitchWeaponSequence(int oldIndex, int newIndex)
    {
        isSwitchesInProgress = true;

        GameObject oldWeapon = (oldIndex >= 0 && oldIndex < MaxWeaponSlots) ? weapons[oldIndex] : null;
        GameObject newWeapon = (newIndex >= 0 && newIndex < MaxWeaponSlots) ? weapons[newIndex] : null;

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
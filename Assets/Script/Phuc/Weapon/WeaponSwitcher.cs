using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Quan ly doi vu khi, nhat/vut vu khi vao GameObject Holder, tu chuyen ve index 0 khi het luu dan
public class WeaponSwitcher : MonoBehaviour
{
    public const int MaxWeaponSlots = 5; // 0: Chinh, 1: Phu, 2: Can chien, 3: Luu dan, 4: Ho tro

    [Header("Weapon Settings (5 Index Co Dinh: 0..4)")]
    [Tooltip("Mang 5 o: [0] Vu khi chinh, [1] Vu khi phu, [2] Can chien, [3] Luu dan, [4] Thiet bi ho tro.")]
    [SerializeField] private GameObject[] weapons = new GameObject[MaxWeaponSlots];

    [Tooltip("GameObject Holder chua vu khi khi nhat/cam tren tay (neu de trong se tu lay chinh GameObject nay).")]
    [SerializeField] private Transform weaponHolder;

    [Header("Input Action")]
    [Tooltip("Keo InputAction loai Vector2 (hoac Mouse Scroll) tu Input Actions Asset vao day.")]
    [SerializeField] private InputActionReference scrollAction;

    [Tooltip("Phim nhat vu khi bang New Input System (de trong se tu dung phim E).")]
    [SerializeField] private InputActionReference pickupWeaponAction;

    [Tooltip("Phim vut vu khi dang cam bang New Input System (de trong se tu dung phim G).")]
    [SerializeField] private InputActionReference dropWeaponAction;

    [Header("Pickup & Drop Settings")]
    [SerializeField] private float pickupRange = 3.5f;
    [SerializeField] private LayerMask pickupLayerMask = ~0;
    [SerializeField] private Vector3 defaultEquippedLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 defaultEquippedLocalEuler = Vector3.zero;
    [SerializeField] private float dropForwardForce = 4.5f;
    [SerializeField] private float dropUpwardForce = 2.0f;

    [Header("=== 1. HOLSTER SETTINGS (CAT VU KHI) ===")]
    [Tooltip("Do dich chuyen vi tri Y khi cat sung (Nhap so am tuy y, vi du: -0.4).")]
    [SerializeField] private float holsterOffsetY = -0.4f;

    [Tooltip("Goc nghieng Z khi cat sung (Vi du: 30 do).")]
    [SerializeField] private float holsterOffsetZ = 30f;

    [Tooltip("Thoi gian thuc hien cat sung (giay).")]
    [SerializeField] private float holsterDuration = 0.2f;

    [Header("=== 2. EQUIP SETTINGS (RUT VU KHI) ===")]
    [Tooltip("Do dich chuyen vi tri Y khi rut sung tu duoi len (Nhap so am tuy y, vi du: -0.4).")]
    [SerializeField] private float equipOffsetY = -0.4f;

    [Tooltip("Goc nghieng Z khi bat dau rut sung (Vi du: -30 do).")]
    [SerializeField] private float equipOffsetZ = -30f;

    [Tooltip("Thoi gian thuc hien rut sung (giay).")]
    [SerializeField] private float equipDuration = 0.2f;

    private int currentWeaponIndex = 0;

    private WeaponSystem[] weaponSystems;
    private SniperWeapon[] sniperWeapons;
    private MeleeWeapon[] meleeWeapons;
    private GrenadeWeapon[] grenadeWeapons;

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

    public Transform HolderTransform => weaponHolder != null ? weaponHolder : transform;
    public int CurrentWeaponIndex => currentWeaponIndex;

    public WeaponSystem CurrentWeaponSystem => (weaponSystems != null && currentWeaponIndex >= 0 && currentWeaponIndex < weaponSystems.Length) ? weaponSystems[currentWeaponIndex] : null;
    public SniperWeapon CurrentSniperWeapon => (sniperWeapons != null && currentWeaponIndex >= 0 && currentWeaponIndex < sniperWeapons.Length) ? sniperWeapons[currentWeaponIndex] : null;
    public MeleeWeapon CurrentMeleeWeapon => (meleeWeapons != null && currentWeaponIndex >= 0 && currentWeaponIndex < meleeWeapons.Length) ? meleeWeapons[currentWeaponIndex] : null;
    public GrenadeWeapon CurrentGrenadeWeapon => (grenadeWeapons != null && currentWeaponIndex >= 0 && currentWeaponIndex < grenadeWeapons.Length) ? grenadeWeapons[currentWeaponIndex] : null;

    // Lay nhanh GrenadeWeapon dang nam o o Luu dan (Slot 3)
    public GrenadeWeapon EquippedGrenadeSlot => (grenadeWeapons != null && grenadeWeapons.Length > (int)WeaponCategory.Grenade)
        ? grenadeWeapons[(int)WeaponCategory.Grenade]
        : null;

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
        GanCacVuKhiBanDauVaoHolder();
        RebuildWeaponCaches(initialSetup: true);

        int firstIdx = FindFirstValidWeaponIndex(0);
        currentWeaponIndex = firstIdx >= 0 ? firstIdx : 0;

        if (firstIdx >= 0 && weapons[firstIdx] != null && CoTheChonSlot(firstIdx))
        {
            weapons[firstIdx].SetActive(true);
            StartCoroutine(PlayEquipAnimation(
                weapons[firstIdx].transform,
                originalLocalPositions[firstIdx],
                originalLocalRotations[firstIdx]
            ));
        }
    }

    // Kiem tra slot co vu khi hop le de rut ra cam hay khong (neu la luu dan ma het dan thi khong cho chon)
    public bool CoTheChonSlot(int index)
    {
        if (weapons == null || index < 0 || index >= MaxWeaponSlots) return false;
        if (weapons[index] == null) return false;

        if (grenadeWeapons != null && index < grenadeWeapons.Length && grenadeWeapons[index] != null)
        {
            return grenadeWeapons[index].ConLuuDan;
        }

        return true;
    }

    // Duoc goi tu GrenadeWeapon ngay khi nem het qua luu dan cuoi cung
    public void XuLyKhiHetLuuDanChuyenVeIndex0()
    {
        int slotLuuDan = (int)WeaponCategory.Grenade;
        if (weapons != null && slotLuuDan >= 0 && slotLuuDan < MaxWeaponSlots && weapons[slotLuuDan] != null)
        {
            weapons[slotLuuDan].SetActive(false);
        }

        if (switchCoroutine != null)
        {
            StopCoroutine(switchCoroutine);
            switchCoroutine = null;
            isSwitchesInProgress = false;
        }

        // Uu tien chuyen select ve index 0 neu index 0 hop le; neu index 0 trong thi tim vu khi hop le gan nhat
        int targetIndex = CoTheChonSlot(0) ? 0 : FindFirstValidWeaponIndex(0);
        if (targetIndex >= 0 && weapons[targetIndex] != null)
        {
            currentWeaponIndex = targetIndex;
            weapons[targetIndex].SetActive(true);
            switchCoroutine = StartCoroutine(PlayEquipAnimation(
                weapons[targetIndex].transform,
                originalLocalPositions[targetIndex],
                originalLocalRotations[targetIndex]
            ));
        }
        else
        {
            currentWeaponIndex = 0;
        }
    }

    // Sap xep cac vu khi ban dau vao dung index phan loai (0..4) cua chung
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
        }

        weapons = arranged;
    }

    // Dam bao cac vu khi/luu dan da gan san trong mang deu thuoc con cua weaponHolder
    private void GanCacVuKhiBanDauVaoHolder()
    {
        Transform holder = HolderTransform;
        for (int i = 0; i < MaxWeaponSlots; i++)
        {
            GameObject w = weapons[i];
            if (w == null) continue;

            if (w.transform.parent != holder)
            {
                w.transform.SetParent(holder, false);
                w.transform.localPosition = defaultEquippedLocalPosition;
                w.transform.localRotation = Quaternion.Euler(defaultEquippedLocalEuler);
            }

            GrenadeWeapon grenadeComp = w.GetComponentInChildren<GrenadeWeapon>(true);
            if (grenadeComp != null)
            {
                grenadeComp.DongBoTrangThaiSauKhiGanHolder(defaultEquippedLocalPosition, Quaternion.Euler(defaultEquippedLocalEuler));
            }

            WeaponClassifier.RefreshWeaponLayer(w);
        }
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

    // Nhat vu khi/luu dan: gan lam con cua weaponHolder, dung vat ly, chuyen layer va xep vao dung slot (0..4)
    public bool EquipPickedUpWeapon(GameObject pickedWeapon)
    {
        if (pickedWeapon == null || IsAlreadyOwned(pickedWeapon)) return false;

        WeaponCategory category = WeaponClassifier.GetWeaponCategory(pickedWeapon);
        int targetIndex = (int)category;

        // Neu da co object luu dan o slot 3 (ke ca khi dang an do het dan) -> cong them luu dan, GIU AN cho toi khi nguoi choi chon slot
        if (category == WeaponCategory.Grenade && targetIndex >= 0 && targetIndex < MaxWeaponSlots && weapons[targetIndex] != null)
        {
            GrenadeWeapon existingGrenade = weapons[targetIndex].GetComponentInChildren<GrenadeWeapon>(true);
            GrenadeWeapon pickedGrenade = pickedWeapon.GetComponentInChildren<GrenadeWeapon>(true);
            if (existingGrenade != null)
            {
                int addCount = pickedGrenade != null ? Mathf.Max(1, pickedGrenade.CurrentGrenadeCount) : 1;
                existingGrenade.AddGrenades(addCount);
                Destroy(pickedWeapon);

                // Chi hien lai neu nguoi choi dang chon dung slot luu dan
                if (currentWeaponIndex == targetIndex)
                {
                    weapons[targetIndex].SetActive(true);
                }
                else
                {
                    weapons[targetIndex].SetActive(false);
                }
                return true;
            }
        }

        if (targetIndex < 0 || targetIndex >= MaxWeaponSlots || weapons[targetIndex] != null)
        {
            return false;
        }

        Transform holder = HolderTransform;

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

        Vector3 savedScale = pickedWeapon.transform.localScale;

        pickedWeapon.transform.SetParent(holder, false);
        pickedWeapon.transform.localPosition = defaultEquippedLocalPosition;
        pickedWeapon.transform.localRotation = Quaternion.Euler(defaultEquippedLocalEuler);
        pickedWeapon.transform.localScale = savedScale;

        GrenadeWeapon grenadeComp = pickedWeapon.GetComponentInChildren<GrenadeWeapon>(true);
        if (grenadeComp != null)
        {
            grenadeComp.DongBoTrangThaiSauKhiGanHolder(defaultEquippedLocalPosition, Quaternion.Euler(defaultEquippedLocalEuler));
        }

        WeaponClassifier.RefreshWeaponLayer(pickedWeapon);

        weapons[targetIndex] = pickedWeapon;
        RebuildWeaponCaches(initialSetup: false);

        // Neu nhặt luu dan lan dau khi dang cam vu khi khac -> chi cong vao slot, khong bat/chuyen sang ngay
        if (category == WeaponCategory.Grenade && currentWeaponIndex != targetIndex)
        {
            pickedWeapon.SetActive(false);
            return true;
        }

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
        Vector3 dropDir = (dropRef.forward * dropForwardForce) + (dropRef.up * dropUpwardForce);

        weaponToDrop.SetActive(true);
        weaponToDrop.transform.SetParent(null, true);
        weaponToDrop.transform.position = dropPos;
        weaponToDrop.transform.rotation = Quaternion.LookRotation(dropRef.forward, Vector3.up);

        if (originalLocalScales != null && droppedIndex < originalLocalScales.Length)
        {
            weaponToDrop.transform.localScale = originalLocalScales[droppedIndex];
        }

        WeaponClassifier.RefreshWeaponLayer(weaponToDrop);

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
        else
        {
            currentWeaponIndex = 0;
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
                current.GetComponent<GrenadeWeapon>() != null ||
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
        grenadeWeapons = new GrenadeWeapon[MaxWeaponSlots];
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
                grenadeWeapons[i] = weapons[i].GetComponentInChildren<GrenadeWeapon>(true);

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
            if (CoTheChonSlot(i)) return i;
        }
        for (int i = 0; i < clampedStart; i++)
        {
            if (CoTheChonSlot(i)) return i;
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

            if (CoTheChonSlot(idx)) return idx;
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
        if (!CoTheChonSlot(targetIndex)) return;
        if (targetIndex == currentWeaponIndex && weapons[targetIndex].activeSelf) return;

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
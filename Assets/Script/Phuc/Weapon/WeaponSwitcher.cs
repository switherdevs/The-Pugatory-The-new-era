using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Quan ly doi vu khi, nhat/vut vu khi vao GameObject Holder, tu chuyen ve index 0 khi het luu dan
public class WeaponSwitcher : MonoBehaviour
{
    public const int MaxWeaponSlots = 5; // 0: Chinh, 1: Phu, 2: Can chien, 3: Luu dan, 4: Ho tro

    [Header("Weapon Settings (5 Index Co Dinh: 0..4)")]
    [Tooltip("Mang 5 o: [0] Vu khi chinh, [1] Vu khi phu, [2] Can chien, [3] Luu dan (chi 1 loai: Frag hoac Flashbang), [4] Thiet bi ho tro.")]
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

    [Tooltip("Bat/tat co che khoa tam thoi cho phep nhat lai sau khi bo luu dan ra ngoai.")]
    [SerializeField] private bool khoaTamThoiChoNhatLaiSauKhiBo = true;

    [Tooltip("Thoi gian ngan (giay) sau khi bo luu dan ra ngoai truoc khi cho phep nhat lai de tranh bi nhat ngay luc vua vut.")]
    [SerializeField] private float thoiGianChoNhatLaiSauKhiBo = 0.65f;

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

    private readonly Dictionary<GameObject, float> danhSachLuuDanTamThoiKhoaNhat = new Dictionary<GameObject, float>();

    private InputAction runtimePickupAction;
    private InputAction runtimeDropAction;

    private InputAction ActivePickupAction =>
        pickupWeaponAction != null && pickupWeaponAction.action != null ? pickupWeaponAction.action : runtimePickupAction;

    private InputAction ActiveDropAction =>
        dropWeaponAction != null && dropWeaponAction.action != null ? dropWeaponAction.action : runtimeDropAction;

    public Transform HolderTransform => weaponHolder != null ? weaponHolder : transform;
    public int CurrentWeaponIndex => currentWeaponIndex;
    public bool IsSwitchingWeapon => isSwitchesInProgress;

    public WeaponSystem CurrentWeaponSystem => (weaponSystems != null && currentWeaponIndex >= 0 && currentWeaponIndex < weaponSystems.Length) ? weaponSystems[currentWeaponIndex] : null;
    public SniperWeapon CurrentSniperWeapon => (sniperWeapons != null && currentWeaponIndex >= 0 && currentWeaponIndex < sniperWeapons.Length) ? sniperWeapons[currentWeaponIndex] : null;
    public MeleeWeapon CurrentMeleeWeapon => (meleeWeapons != null && currentWeaponIndex >= 0 && currentWeaponIndex < meleeWeapons.Length) ? meleeWeapons[currentWeaponIndex] : null;
    public GrenadeWeapon CurrentGrenadeWeapon => (grenadeWeapons != null && currentWeaponIndex >= 0 && currentWeaponIndex < grenadeWeapons.Length) ? grenadeWeapons[currentWeaponIndex] : null;

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

    private void DongBoLuuDanTaiSlot(int index)
    {
        if (grenadeWeapons != null && index >= 0 && index < grenadeWeapons.Length && grenadeWeapons[index] != null)
        {
            grenadeWeapons[index].DongBoKhiKichHoatCamTrenTay();
        }
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
            DongBoLuuDanTaiSlot(firstIdx);
            switchCoroutine = StartCoroutine(PlayEquipAnimation(
                weapons[firstIdx].transform,
                originalLocalPositions[firstIdx],
                originalLocalRotations[firstIdx]
            ));
        }
    }

    public bool CoTheChonSlot(int index)
    {
        if (weapons == null || index < 0 || index >= MaxWeaponSlots) return false;
        if (weapons[index] == null) return false;
        if (!weapons[index].transform.IsChildOf(HolderTransform)) return false;

        if (grenadeWeapons != null && index < grenadeWeapons.Length && grenadeWeapons[index] != null)
        {
            return grenadeWeapons[index].ConLuuDan;
        }

        return true;
    }

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

        int targetIndex = CoTheChonSlot(0) ? 0 : FindFirstValidWeaponIndex(0);
        if (targetIndex >= 0 && weapons[targetIndex] != null)
        {
            currentWeaponIndex = targetIndex;
            weapons[targetIndex].SetActive(true);
            DongBoLuuDanTaiSlot(targetIndex);
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

    private void ArrangeWeaponsIntoClassifiedSlots()
    {
        GameObject[] arranged = new GameObject[MaxWeaponSlots];
        int grenadeSlot = (int)WeaponCategory.Grenade;

        for (int i = 0; i < MaxWeaponSlots; i++)
        {
            GameObject w = weapons[i];
            if (w == null) continue;

            int classifiedIndex = Mathf.Clamp(WeaponClassifier.GetWeaponSlotIndex(w), 0, MaxWeaponSlots - 1);

            if (classifiedIndex == grenadeSlot)
            {
                if (arranged[grenadeSlot] == null)
                {
                    arranged[grenadeSlot] = w;
                }
                else
                {
                    w.SetActive(false);
                }
                continue;
            }

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

            // Goi lai sau RefreshWeaponLayer de layer chon trong dropdown cua GrenadeWeapon luon duoc giu
            if (grenadeComp != null)
            {
                grenadeComp.ApDungLayerKhiDaNhat();
            }
        }
    }

    private void Update()
    {
        DonDepDanhSachKhoaNhatTamThoi();

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

    private void DonDepDanhSachKhoaNhatTamThoi()
    {
        if (danhSachLuuDanTamThoiKhoaNhat.Count == 0) return;

        List<GameObject> canXoa = null;
        foreach (KeyValuePair<GameObject, float> item in danhSachLuuDanTamThoiKhoaNhat)
        {
            if (item.Key == null || Time.time >= item.Value)
            {
                if (canXoa == null) canXoa = new List<GameObject>();
                canXoa.Add(item.Key);
            }
        }

        if (canXoa != null)
        {
            for (int i = 0; i < canXoa.Count; i++)
            {
                danhSachLuuDanTamThoiKhoaNhat.Remove(canXoa[i]);
            }
        }
    }

    private bool DangTamKhoaNhatLai(GameObject candidate)
    {
        if (!khoaTamThoiChoNhatLaiSauKhiBo || candidate == null) return false;
        if (danhSachLuuDanTamThoiKhoaNhat.TryGetValue(candidate, out float thoiDiemMoKhoa))
        {
            if (Time.time < thoiDiemMoKhoa)
            {
                return true;
            }
            danhSachLuuDanTamThoiKhoaNhat.Remove(candidate);
        }
        return false;
    }

    private bool NamTrongBanKinhNhatHopLe(GameObject candidate, Transform searchOrigin)
    {
        if (candidate == null || searchOrigin == null) return false;

        float sqrDist = (candidate.transform.position - searchOrigin.position).sqrMagnitude;
        if (sqrDist > pickupRange * pickupRange)
        {
            return false;
        }

        WeaponClassifier classifier = candidate.GetComponent<WeaponClassifier>();
        if (classifier == null)
        {
            classifier = candidate.GetComponentInChildren<WeaponClassifier>(true);
        }

        if (classifier != null && classifier.RequirePickupRadius)
        {
            if (!classifier.IsWithinPickupRadius(searchOrigin.position) &&
                !classifier.IsWithinPickupRadius(transform.position))
            {
                return false;
            }
        }

        return true;
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
            if (candidate == null || IsAlreadyOwned(candidate) || DangTamKhoaNhatLai(candidate)) continue;
            if (!NamTrongBanKinhNhatHopLe(candidate, searchOrigin)) continue;
            if (LaLuuDanCungLoaiVaDaDay(candidate)) continue;

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

    private bool LaLuuDanCungLoaiVaDaDay(GameObject candidate)
    {
        if (candidate == null) return false;
        int grenadeSlot = (int)WeaponCategory.Grenade;
        if (weapons == null || grenadeSlot < 0 || grenadeSlot >= MaxWeaponSlots || weapons[grenadeSlot] == null)
        {
            return false;
        }

        GrenadeWeapon existingGrenade = weapons[grenadeSlot].GetComponentInChildren<GrenadeWeapon>(true);
        GrenadeWeapon pickedGrenade = candidate.GetComponentInChildren<GrenadeWeapon>(true);

        if (existingGrenade != null && pickedGrenade != null &&
            existingGrenade.CurrentGrenadeType == pickedGrenade.CurrentGrenadeType)
        {
            if (existingGrenade.DaDayLuuDan)
            {
                return true;
            }

            if (!existingGrenade.ChoPhepNhatMotPhanKhiGanDay)
            {
                int soLuongCanThem = pickedGrenade.SoLuongCongKhiDuocNhat;
                int soLuongConThieu = existingGrenade.MaxGrenadeCount - existingGrenade.CurrentGrenadeCount;
                if (soLuongCanThem > soLuongConThieu)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool EquipPickedUpWeapon(GameObject pickedWeapon, bool autoEquipIfGrenade = false)
    {
        if (pickedWeapon == null || IsAlreadyOwned(pickedWeapon) || DangTamKhoaNhatLai(pickedWeapon)) return false;

        WeaponCategory category = WeaponClassifier.GetWeaponCategory(pickedWeapon);
        int targetIndex = (int)category;
        if (targetIndex < 0 || targetIndex >= MaxWeaponSlots) return false;

        if (category == WeaponCategory.Grenade && weapons[targetIndex] != null)
        {
            GrenadeWeapon existingGrenade = weapons[targetIndex].GetComponentInChildren<GrenadeWeapon>(true);
            GrenadeWeapon pickedGrenade = pickedWeapon.GetComponentInChildren<GrenadeWeapon>(true);

            bool cungLoai = existingGrenade != null &&
                            pickedGrenade != null &&
                            existingGrenade.CurrentGrenadeType == pickedGrenade.CurrentGrenadeType;

            if (cungLoai)
            {
                if (existingGrenade.DaDayLuuDan)
                {
                    return false;
                }

                int addCount = pickedGrenade != null ? pickedGrenade.SoLuongCongKhiDuocNhat : 1;
                int added = existingGrenade.AddGrenades(addCount);
                if (added <= 0)
                {
                    return false;
                }

                // Dam bao layer da nhat duoc ap dung sau khi cong don luu dan
                existingGrenade.ApDungLayerKhiDaNhat();

                Destroy(pickedWeapon);

                weapons[targetIndex].SetActive(existingGrenade.ConLuuDan && (currentWeaponIndex == targetIndex || autoEquipIfGrenade));
                if (currentWeaponIndex == targetIndex || autoEquipIfGrenade)
                {
                    existingGrenade.DongBoKhiKichHoatCamTrenTay();
                }
                if (autoEquipIfGrenade && existingGrenade.ConLuuDan && currentWeaponIndex != targetIndex)
                {
                    StartSwitchToWeapon(targetIndex);
                }
                return true;
            }

            // Khac loai luu dan: bo qua luu dan dang giu ra map (va dam bao hien thi lai) de nguoi choi co the nhat lai doi qua lai
            if (existingGrenade != null && existingGrenade.ConLuuDan)
            {
                DropGrenadeObject(targetIndex, weapons[targetIndex], existingGrenade, switchToOtherWeapon: false, khoaTamThoiChoVatMoi: pickedWeapon);
            }
            else
            {
                Destroy(weapons[targetIndex]);
                weapons[targetIndex] = null;
                RebuildWeaponCaches(initialSetup: false);
            }
        }

        if (weapons[targetIndex] != null)
        {
            return false;
        }

        Transform holder = HolderTransform;
        Vector3 savedScale = pickedWeapon.transform.localScale;

        GrenadeWeapon grenadeComp = pickedWeapon.GetComponentInChildren<GrenadeWeapon>(true);
        if (grenadeComp != null)
        {
            grenadeComp.enabled = false;
        }

        Rigidbody[] rbs = pickedWeapon.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            if (rbs[i] == null) continue;
            rbs[i].linearVelocity = Vector3.zero;
            rbs[i].angularVelocity = Vector3.zero;
            rbs[i].isKinematic = true;
            rbs[i].useGravity = false;
            rbs[i].interpolation = RigidbodyInterpolation.None;
        }

        Collider[] cols = pickedWeapon.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null) cols[i].enabled = false;
        }

        pickedWeapon.transform.SetParent(holder, false);
        pickedWeapon.transform.localPosition = defaultEquippedLocalPosition;
        pickedWeapon.transform.localRotation = Quaternion.Euler(defaultEquippedLocalEuler);
        pickedWeapon.transform.localScale = savedScale;

        WeaponClassifier.RefreshWeaponLayer(pickedWeapon);

        weapons[targetIndex] = pickedWeapon;
        RebuildWeaponCaches(initialSetup: false);

        if (grenadeComp != null)
        {
            grenadeComp.DongBoTrangThaiSauKhiGanHolder(originalLocalPositions[targetIndex], originalLocalRotations[targetIndex]);
            // Ap dung layer da chon trong dropdown sau RefreshWeaponLayer de khong bi ghi de
            grenadeComp.ApDungLayerKhiDaNhat();
        }

        if (category == WeaponCategory.Grenade && currentWeaponIndex != targetIndex && !autoEquipIfGrenade)
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
        DongBoLuuDanTaiSlot(targetIndex);

        if (switchCoroutine != null) StopCoroutine(switchCoroutine);

        if (previousIndex >= 0 && previousIndex < MaxWeaponSlots && previousIndex != targetIndex && weapons[previousIndex] != null)
        {
            switchCoroutine = StartCoroutine(SwitchWeaponSequence(previousIndex, targetIndex));
        }
        else
        {
            pickedWeapon.SetActive(true);
            DongBoLuuDanTaiSlot(targetIndex);
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
        GrenadeWeapon droppedGrenade = weaponToDrop.GetComponentInChildren<GrenadeWeapon>(true);
        if (droppedGrenade != null)
        {
            return DropGrenadeObject(droppedIndex, weaponToDrop, droppedGrenade, switchToOtherWeapon: true);
        }

        DropWeaponObject(droppedIndex, weaponToDrop, switchToOtherWeapon: true);
        return true;
    }

    private void DropWeaponObject(int droppedIndex, GameObject weaponToDrop, bool switchToOtherWeapon)
    {
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

        Renderer[] renderers = weaponToDrop.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = true;
                if (!renderers[i].gameObject.activeSelf)
                {
                    renderers[i].gameObject.SetActive(true);
                }
            }
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
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(dropDir, ForceMode.Impulse);

        weapons[droppedIndex] = null;
        RebuildWeaponCaches(initialSetup: false);

        if (!switchToOtherWeapon) return;

        int nextIdx = GetNextValidWeaponIndex(droppedIndex, 1);
        if (nextIdx >= 0)
        {
            currentWeaponIndex = nextIdx;
            weapons[currentWeaponIndex].SetActive(true);
            weapons[currentWeaponIndex].transform.localPosition = originalLocalPositions[currentWeaponIndex];
            weapons[currentWeaponIndex].transform.localRotation = originalLocalRotations[currentWeaponIndex];
            DongBoLuuDanTaiSlot(currentWeaponIndex);
        }
        else
        {
            currentWeaponIndex = 0;
        }
    }

    private bool DropGrenadeObject(
        int droppedIndex,
        GameObject grenadeObj,
        GrenadeWeapon grenadeComp,
        bool switchToOtherWeapon,
        GameObject khoaTamThoiChoVatMoi = null)
    {
        int soLuongVut = grenadeComp != null ? grenadeComp.CurrentGrenadeCount : 1;
        GrenadeWeapon.LoaiLuuDan loaiVut = grenadeComp != null ? grenadeComp.CurrentGrenadeType : GrenadeWeapon.LoaiLuuDan.FragGrenade;
        int soLuongToiDaVut = grenadeComp != null ? grenadeComp.MaxGrenadeCount : 4;
        GameObject prefabDropTuyChon = grenadeComp != null ? grenadeComp.PrefabVatPhamDrop : null;

        weapons[droppedIndex] = null;
        if (grenadeObj != null)
        {
            Destroy(grenadeObj);
        }
        RebuildWeaponCaches(initialSetup: false);

        if (soLuongVut > 0 && prefabDropTuyChon != null)
        {
            Transform dropRef = Camera.main != null ? Camera.main.transform : transform;
            Vector3 dropPos = dropRef.position + dropRef.forward * 1.1f + Vector3.up * 0.1f;
            Vector3 dropDir = (dropRef.forward * dropForwardForce) + (dropRef.up * dropUpwardForce);

            GameObject physicalDrop = Instantiate(prefabDropTuyChon, dropPos, Quaternion.LookRotation(dropRef.forward, Vector3.up));
            physicalDrop.name = loaiVut == GrenadeWeapon.LoaiLuuDan.Flashbang ? "Dropped_Flashbang" : "Dropped_FragGrenade";

            GrenadeWeapon droppedComp = physicalDrop.GetComponentInChildren<GrenadeWeapon>(true);
            if (droppedComp == null)
                droppedComp = physicalDrop.AddComponent<GrenadeWeapon>();

            droppedComp.ThietLapLoaiVaSoLuong(loaiVut, Mathf.Clamp(soLuongVut, 1, soLuongToiDaVut), soLuongToiDaVut);
            droppedComp.enabled = false;

            GrenadeWeapon.HienThiLaiVatPhamDrop(physicalDrop, loaiVut, null,
                grenadeComp != null && grenadeComp.BatTriggerKhiDrop,
                grenadeComp != null ? grenadeComp.BanKinhColliderDrop : 0.25f,
                transform.root);

            Rigidbody rb = physicalDrop.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddForce(dropDir, ForceMode.Impulse);

            if (khoaTamThoiChoNhatLaiSauKhiBo && khoaTamThoiChoVatMoi != null && thoiGianChoNhatLaiSauKhiBo > 0f)
                danhSachLuuDanTamThoiKhoaNhat[physicalDrop] = Time.time + thoiGianChoNhatLaiSauKhiBo;
        }

        if (!switchToOtherWeapon) return true;

        int nextIdx = GetNextValidWeaponIndex(droppedIndex, 1);
        if (nextIdx >= 0)
        {
            currentWeaponIndex = nextIdx;
            weapons[currentWeaponIndex].SetActive(true);
            weapons[currentWeaponIndex].transform.localPosition = originalLocalPositions[currentWeaponIndex];
            weapons[currentWeaponIndex].transform.localRotation = originalLocalRotations[currentWeaponIndex];
            DongBoLuuDanTaiSlot(currentWeaponIndex);
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

                if (weapons[i].transform.parent == HolderTransform)
                {
                    originalLocalPositions[i] = defaultEquippedLocalPosition;
                    originalLocalRotations[i] = Quaternion.Euler(defaultEquippedLocalEuler);
                }
                else
                {
                    originalLocalPositions[i] = weapons[i].transform.localPosition;
                    originalLocalRotations[i] = weapons[i].transform.localRotation;
                }

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
            DongBoLuuDanTaiSlot(newIndex);

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
            DongBoLuuDanTaiSlot(newIndex);
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
        DongBoLuuDanTaiSlot(currentWeaponIndex);
        isSwitchesInProgress = false;
    }
}
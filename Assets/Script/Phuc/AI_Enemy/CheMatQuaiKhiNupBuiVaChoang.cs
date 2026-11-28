using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class CheMatQuaiKhiNupBuiVaChoang : MonoBehaviour
{
    private struct DuLieuLuuDanChoangTheoDoi
    {
        public Vector3 viTriTruocDo;
        public int soLanKhongDiChuyen;
    }

    [Header("1. Che Mat Khi Nguoi Choi Nup Bui Cay")]
    [Tooltip("Bật/tắt việc che mắt quái khi người chơi núp an toàn trong bụi cây (tag bush).")]
    [SerializeField] private bool batCheMatKhiNupBui = true;

    [Tooltip("Bán kính bổ sung khi kiểm tra quái chạm vào người chơi để phá núp bụi.")]
    [SerializeField] private float khoangCachChamPhuBoSung = 0.25f;

    [Header("2. Hieu Ung Choang Khi Luu Dan Choang No")]
    [Tooltip("Bật/tắt hiệu ứng bị choáng (che mắt/mù tạm thời) khi lựu đạn Flashbang nổ gần quái.")]
    [SerializeField] private bool batChoangKhiLuuDanChoangNo = true;

    [Tooltip("Thời gian quái bị choáng (giây) khi trúng lựu đạn choáng.")]
    [SerializeField] private float thoiGianChoangMacDinh = 3.2f;

    [Tooltip("Bán kính hiệu lực của lựu đạn choáng tính từ tâm vụ nổ (mặc định <= 0 sẽ tự nhân theo bán kính vùng nổ của lựu đạn).")]
    [SerializeField] private float banKinhChoangTuyChinh = 0f;

    [Tooltip("Nếu bật, vật cản có tagVatCan hoặc lopVatCan chắn giữa điểm nổ và quái sẽ làm giảm hoặc chặn choáng.")]
    [SerializeField] private bool kiemTraVatCanChanChoang = true;

    [Tooltip("Layer vật cản che khuất choáng (để 0 nếu muốn dùng tagVatCan trên AI).")]
    [SerializeField] private LayerMask lopVatCanChoang = 0;

    [Tooltip("Nếu bật, quái sẽ lảo đảo nhẹ tại chỗ khi đang bị choáng.")]
    [SerializeField] private bool hieuUngLaoDaoKhiChoang = true;

    [Tooltip("Biên độ góc lảo đảo (độ) khi bị choáng.")]
    [SerializeField] private float gocLaoDaoKhiChoang = 12f;

    [Tooltip("Tốc độ lảo đảo khi bị choáng.")]
    [SerializeField] private float tocDoLaoDaoKhiChoang = 9f;

    [Header("3. Nhan Dien Qua Luu Dan Choang & Toi Uu Hieu Nang")]
    [Tooltip("Từ khóa trong tên GameObject/Prefab để nhận diện lựu đạn choáng (Flashbang).")]
    [SerializeField] private string tuKhoaLuuDanChoang = "flash";

    [Tooltip("Chu kỳ quét lại danh sách quái / lựu đạn trong scene (giây). Giá trị 0.25 - 0.5s rất nhẹ cho mọi máy.")]
    [SerializeField] private float chuKyQuetScene = 0.35f;

    [Tooltip("Hiển thị vòng bán kính hiệu lực choáng trong Scene view khi chọn quái.")]
    [SerializeField] private bool hienThiGizmosChoang = true;

    // Cache danh sách toàn bộ instance script này trong scene để đồng bộ nhẹ nhàng
    private static readonly List<CheMatQuaiKhiNupBuiVaChoang> tatCaQuaiTrongScene = new List<CheMatQuaiKhiNupBuiVaChoang>();
    private static readonly Dictionary<int, DuLieuLuuDanChoangTheoDoi> duLieuLuuDanDangBay = new Dictionary<int, DuLieuLuuDanChoangTheoDoi>();
    private static readonly List<int> khoaLuuDanCanXoa = new List<int>();
    private static readonly Collider[] boDemVaCham = new Collider[24];

    private static AnNapBuiCayPlayer cachedNguoiChoiAnNap;
    private static float boDemQuetToanCuc = 0f;

    // Thành phần của chính con quái này
    private MeleeEnemyAI aiCanChien;
    private RangedEnemyAI aiTamXa;
    private NavMeshAgent navMeshAgent;
    private HealthSystem heThongMau;
    private Transform diemMatQuai;

    private bool trangThaiAISauKhiBiCheMat = true;
    private bool navMeshDangBiTamDung = false;
    private float boDemThoiGianChoang = 0f;
    private float boDemThoiGianLaoDao = 0f;
    private Quaternion gocGocTruocKhiLaoDao;
    private bool daLuuGocLaoDao = false;

    public bool DangBiChoang => boDemThoiGianChoang > 0f;
    public bool DangBiCheMatDoBuiCay => batCheMatKhiNupBui && !DangBiChoang && KiemTraBiCheMatBoiBuiCay();

    private void OnEnable()
    {
        if (!tatCaQuaiTrongScene.Contains(this))
        {
            tatCaQuaiTrongScene.Add(this);
        }

        if (aiCanChien == null) aiCanChien = GetComponent<MeleeEnemyAI>();
        if (aiTamXa == null) aiTamXa = GetComponent<RangedEnemyAI>();
        if (navMeshAgent == null) navMeshAgent = GetComponent<NavMeshAgent>();
        if (heThongMau == null) heThongMau = GetComponentInParent<HealthSystem>();
        if (diemMatQuai == null) diemMatQuai = transform;
    }

    private void OnDisable()
    {
        tatCaQuaiTrongScene.Remove(this);
        KhoiPhucTrangThaiBinhThuong(tucThi: true);
    }

    private void OnDestroy()
    {
        tatCaQuaiTrongScene.Remove(this);
        KhoiPhucTrangThaiBinhThuong(tucThi: true);
    }

    // Cho phép gọi trực tiếp từ script khác nếu muốn làm quái bị choáng ngay lập tức
    public void KichHoatChoang(float thoiGianChoang)
    {
        if (!batChoangKhiLuuDanChoangNo) return;
        if (heThongMau != null && heThongMau.DaChet) return;

        float thoiGian = thoiGianChoang > 0f ? thoiGianChoang : Mathf.Max(0.2f, thoiGianChoangMacDinh);
        if (thoiGian > boDemThoiGianChoang)
        {
            boDemThoiGianChoang = thoiGian;
        }

        if (!daLuuGocLaoDao)
        {
            gocGocTruocKhiLaoDao = transform.localRotation;
            daLuuGocLaoDao = true;
        }
    }

    private void Update()
    {
        // Giới hạn deltaTime tối đa 0.05s để mô phỏng mượt và đồng đều trên mọi mức FPS
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;

        if (heThongMau != null && heThongMau.DaChet)
        {
            boDemThoiGianChoang = 0f;
            KhoiPhucTrangThaiBinhThuong(tucThi: true);
            return;
        }

        // Chỉ quái đầu tiên trong danh sách chạy trình quét Flashbang toàn scene để tiết kiệm CPU
        if (tatCaQuaiTrongScene.Count > 0 && tatCaQuaiTrongScene[0] == this)
        {
            CapNhatQuetToanScene(dt);
        }

        if (boDemThoiGianChoang > 0f)
        {
            boDemThoiGianChoang -= dt;
            if (boDemThoiGianChoang < 0f)
            {
                boDemThoiGianChoang = 0f;
            }

            ApDungTrangThaiVoHieuHoaAI(dt);
            return;
        }

        if (DangBiCheMatDoBuiCay)
        {
            ApDungTrangThaiCheMatBuiCay();
            return;
        }

        KhoiPhucTrangThaiBinhThuong(tucThi: false);
    }

    private bool KiemTraBiCheMatBoiBuiCay()
    {
        if (cachedNguoiChoiAnNap == null || !cachedNguoiChoiAnNap.isActiveAndEnabled)
        {
            cachedNguoiChoiAnNap = FindFirstObjectByType<AnNapBuiCayPlayer>();
        }

        if (cachedNguoiChoiAnNap == null) return false;

        // Nếu người chơi đang núp an toàn trong bụi và quái không chạm trực tiếp vào người chơi -> quái bị che mắt
        if (!cachedNguoiChoiAnNap.DangAnToanTrongBuiCay) return false;

        if (cachedNguoiChoiAnNap.DangChamVoiQuai(transform, khoangCachChamPhuBoSung))
        {
            return false;
        }

        return true;
    }

    private void ApDungTrangThaiCheMatBuiCay()
    {
        SetTrangThaiCacScriptAI(batAI: false);
        TamDungNavMesh(tamDung: true);
    }

    private void ApDungTrangThaiVoHieuHoaAI(float dt)
    {
        SetTrangThaiCacScriptAI(batAI: false);
        TamDungNavMesh(tamDung: true);

        if (hieuUngLaoDaoKhiChoang)
        {
            if (!daLuuGocLaoDao)
            {
                gocGocTruocKhiLaoDao = transform.localRotation;
                daLuuGocLaoDao = true;
            }

            boDemThoiGianLaoDao += dt * Mathf.Max(1f, tocDoLaoDaoKhiChoang);
            float gocZ = Mathf.Sin(boDemThoiGianLaoDao) * gocLaoDaoKhiChoang;
            float gocX = Mathf.Cos(boDemThoiGianLaoDao * 0.7f) * (gocLaoDaoKhiChoang * 0.45f);
            transform.localRotation = gocGocTruocKhiLaoDao * Quaternion.Euler(gocX, 0f, gocZ);
        }
    }

    private void KhoiPhucTrangThaiBinhThuong(bool tucThi)
    {
        if (daLuuGocLaoDao)
        {
            if (tucThi)
            {
                transform.localRotation = gocGocTruocKhiLaoDao;
                daLuuGocLaoDao = false;
                boDemThoiGianLaoDao = 0f;
            }
            else
            {
                float gocLech = Quaternion.Angle(transform.localRotation, gocGocTruocKhiLaoDao);
                if (gocLech <= 0.25f)
                {
                    transform.localRotation = gocGocTruocKhiLaoDao;
                    daLuuGocLaoDao = false;
                    boDemThoiGianLaoDao = 0f;
                }
                else
                {
                    float buoc = 1f - Mathf.Exp(-12f * Mathf.Min(Time.deltaTime, 0.05f));
                    transform.localRotation = Quaternion.Slerp(transform.localRotation, gocGocTruocKhiLaoDao, buoc);
                }
            }
        }

        SetTrangThaiCacScriptAI(batAI: true);
        TamDungNavMesh(tamDung: false);
    }

    private void SetTrangThaiCacScriptAI(bool batAI)
    {
        if (aiCanChien != null && aiCanChien.enabled != batAI)
        {
            aiCanChien.enabled = batAI;
        }

        if (aiTamXa != null && aiTamXa.enabled != batAI)
        {
            aiTamXa.enabled = batAI;
        }
    }

    private void TamDungNavMesh(bool tamDung)
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh) return;

        if (tamDung)
        {
            if (!navMeshDangBiTamDung)
            {
                trangThaiAISauKhiBiCheMat = navMeshAgent.isStopped;
                navMeshDangBiTamDung = true;
            }
            navMeshAgent.isStopped = true;
            navMeshAgent.velocity = Vector3.zero;
        }
        else if (navMeshDangBiTamDung)
        {
            navMeshAgent.isStopped = trangThaiAISauKhiBiCheMat;
            navMeshDangBiTamDung = false;
        }
    }

    private static void CapNhatQuetToanScene(float dt)
    {
        boDemQuetToanCuc -= dt;
        if (boDemQuetToanCuc > 0f) return;
        boDemQuetToanCuc = Mathf.Max(0.1f, chuKyQuetSceneStatic());

        ThrownGrenadeProjectile[] cacQuaLuuDan = FindObjectsByType<ThrownGrenadeProjectile>(FindObjectsSortMode.None);
        HashSet<int> idConTonTai = new HashSet<int>();

        for (int i = 0; i < cacQuaLuuDan.Length; i++)
        {
            ThrownGrenadeProjectile dan = cacQuaLuuDan[i];
            if (dan == null || !dan.gameObject.activeInHierarchy) continue;

            int id = dan.GetInstanceID();
            idConTonTai.Add(id);

            Vector3 viTriHienTai = dan.transform.position;
            if (!duLieuLuuDanDangBay.TryGetValue(id, out DuLieuLuuDanChoangTheoDoi duLieu))
            {
                duLieu = new DuLieuLuuDanChoangTheoDoi
                {
                    viTriTruocDo = viTriHienTai,
                    soLanKhongDiChuyen = 0
                };
            }
            else
            {
                float quangDuong = (viTriHienTai - duLieu.viTriTruocDo).sqrMagnitude;
                if (quangDuong < 0.0004f)
                {
                    duLieu.soLanKhongDiChuyen++;
                }
                else
                {
                    duLieu.soLanKhongDiChuyen = 0;
                }
                duLieu.viTriTruocDo = viTriHienTai;
            }

            duLieuLuuDanDangBay[id] = duLieu;
        }

        // Những quả lựu đạn biến mất/bị hủy sau khi đứng yên (hoặc phát nổ) sẽ kích hoạt kiểm tra choáng quanh vị trí đó
        khoaLuuDanCanXoa.Clear();
        foreach (KeyValuePair<int, DuLieuLuuDanChoangTheoDoi> cap in duLieuLuuDanDangBay)
        {
            if (!idConTonTai.Contains(cap.Key))
            {
                khoaLuuDanCanXoa.Add(cap.Key);
                PhatChoangQuanhViTriNo(cap.Value.viTriTruocDo);
            }
        }

        for (int i = 0; i < khoaLuuDanCanXoa.Count; i++)
        {
            duLieuLuuDanDangBay.Remove(khoaLuuDanCanXoa[i]);
        }
    }

    private static float chuKyQuetSceneStatic()
    {
        if (tatCaQuaiTrongScene.Count > 0 && tatCaQuaiTrongScene[0] != null)
        {
            return tatCaQuaiTrongScene[0].chuKyQuetScene;
        }
        return 0.35f;
    }

    private static void PhatChoangQuanhViTriNo(Vector3 viTriNo)
    {
        for (int i = 0; i < tatCaQuaiTrongScene.Count; i++)
        {
            CheMatQuaiKhiNupBuiVaChoang quai = tatCaQuaiTrongScene[i];
            if (quai == null || !quai.isActiveAndEnabled || !quai.batChoangKhiLuuDanChoangNo) continue;
            if (quai.heThongMau != null && quai.heThongMau.DaChet) continue;

            float banKinhHieuLuc = quai.banKinhChoangTuyChinh > 0f
                ? quai.banKinhChoangTuyChinh
                : 11f;

            Vector3 diemMat = quai.diemMatQuai != null
                ? quai.diemMatQuai.position
                : quai.transform.position + Vector3.up * 1.5f;

            float khoangCach = Vector3.Distance(viTriNo, diemMat);
            if (khoangCach > banKinhHieuLuc) continue;

            if (quai.kiemTraVatCanChanChoang && quai.BiVatCanChanChoang(viTriNo, diemMat))
            {
                continue;
            }

            float tyLeXaGan = 1f - Mathf.Clamp01(khoangCach / banKinhHieuLuc);
            float thoiGianChoang = Mathf.Lerp(quai.thoiGianChoangMacDinh * 0.45f, quai.thoiGianChoangMacDinh, tyLeXaGan);
            quai.KichHoatChoang(thoiGianChoang);
        }
    }

    private bool BiVatCanChanChoang(Vector3 diemNo, Vector3 diemMat)
    {
        Vector3 huong = diemMat - diemNo;
        float khoangCach = huong.magnitude;
        if (khoangCach <= 0.01f) return false;

        int mask = lopVatCanChoang.value != 0 ? lopVatCanChoang.value : ~0;
        RaycastHit[] hits = Physics.RaycastAll(diemNo, huong / khoangCach, khoangCach, mask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;
            if (col.transform.root == transform.root) continue;

            if (lopVatCanChoang.value != 0)
            {
                return true;
            }

            string tagVatCan = LayTagVatCanCuaAI();
            if (!string.IsNullOrEmpty(tagVatCan) && tagVatCan != "Untagged" && CoTagDeQuy(col.transform, tagVatCan))
            {
                return true;
            }
        }

        return false;
    }

    private string LayTagVatCanCuaAI()
    {
        if (aiCanChien != null)
        {
            // Truy cập an toàn nếu có tag qua SerializedObject/trường không làm thay đổi script AI
            return "Untagged";
        }
        return "Untagged";
    }

    private static bool CoTagDeQuy(Transform obj, string tagCanTim)
    {
        Transform hienTai = obj;
        while (hienTai != null)
        {
            if (hienTai.CompareTag(tagCanTim)) return true;
            hienTai = hienTai.parent;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!hienThiGizmosChoang || !batChoangKhiLuuDanChoangNo) return;

        float banKinh = banKinhChoangTuyChinh > 0f ? banKinhChoangTuyChinh : 11f;
        Vector3 viTriTam = diemMatQuai != null ? diemMatQuai.position : transform.position + Vector3.up * 1.5f;

        Gizmos.color = DangBiChoang
            ? new Color(1f, 0.95f, 0.2f, 0.55f)
            : new Color(0.85f, 0.9f, 1f, 0.25f);
        Gizmos.DrawWireSphere(viTriTam, banKinh);
    }
}
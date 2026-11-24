using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.AI;

public class HealthSystem : MonoBehaviour
{
    public enum LoaiDoiTuong
    {
        NguoiChoi,
        QuaiVat
    }

    [Header("1. Thiết lập Phe & Máu cơ bản")]
    [Tooltip("Chọn đối tượng này là Người chơi hay Quái vật.")]
    [SerializeField] private LoaiDoiTuong loaiDoiTuong = LoaiDoiTuong.QuaiVat;

    [Tooltip("Lượng máu tối đa.")]
    [SerializeField] private float mauToiDa = 100f;

    [Tooltip("Giáp giảm sát thương cố định khi hết năng lượng giáp (ví dụ: 5 nghĩa là trừ 5 sát thương, tối thiểu vẫn mất 1 máu).")]
    [SerializeField] private float giapPhongThu = 0f;

    [Header("2. Giáp Năng Lượng (Hấp thụ & Giới hạn mất tối đa 10% Máu)")]
    [Tooltip("Bật/tắt cơ chế giáp năng lượng (khi còn năng lượng giáp sẽ chỉ trừ tối đa 10% máu tối đa trở xuống tùy damage).")]
    [SerializeField] private bool dungGiapNangLuong = true;

    [Tooltip("Lượng năng lượng giáp tối đa.")]
    [SerializeField] private float giapToiDa = 100f;

    [Tooltip("Lượng năng lượng giáp khởi đầu (để < 0 sẽ tự lấy bằng Giáp Tối Đa).")]
    [SerializeField] private float giapKhoiDau = -1f;

    [Tooltip("Tỉ lệ máu tối đa bị mất khi đang có giáp năng lượng (mặc định 0.1 = 10% máu tối đa).")]
    [Range(0.01f, 1f)]
    [SerializeField] private float tiLeMauMatToiDaKhiCoGiap = 0.1f;

    [Tooltip("Mức sát thương chuẩn để chạm ngưỡng mất tối đa 10% máu (damage nhỏ hơn mức này sẽ mất ít hơn 10% máu).")]
    [SerializeField] private float nguongDamageDatTranGiap = 50f;

    [Tooltip("Hệ số tiêu hao năng lượng giáp theo sát thương nhận vào (ví dụ: 1 nghĩa là nhận 20 damage thì tốn 20 năng lượng giáp).")]
    [SerializeField] private float heSoTieuHaoGiap = 1f;

    [Header("3. Bất tử tạm thời & Hồi máu (Chủ yếu cho Người chơi)")]
    [Tooltip("Thời gian miễn nhiễm sát thương sau khi vừa trúng đòn (giây). Nên đặt 0 cho Quái để nhận đủ sát thương súng liên thanh.")]
    [SerializeField] private float thoiGianBatTuSauTrungDon = 0f;

    [Tooltip("Bật/tắt tự động hồi máu khi không giao tranh.")]
    [SerializeField] private bool tuDongHoiMau = false;

    [Tooltip("Thời gian chờ sau lần trúng đòn cuối cùng mới bắt đầu hồi máu (giây).")]
    [SerializeField] private float thoiGianChoHoiMau = 5f;

    [Tooltip("Lượng máu hồi phục mỗi giây.")]
    [SerializeField] private float tocDoHoiMau = 10f;

    [Header("3b. Ragdoll & Chìm Xuống Đất Khi Quái Chết")]
    [Tooltip("Bật/tắt chuyển Quái sang Ragdoll khi chết.")]
    [SerializeField] private bool batRagdollKhiChet = true;

    [Tooltip("Thời gian chờ sau khi thành Ragdoll trước khi bắt đầu chìm xuống lòng đất (giây).")]
    [SerializeField] private float thoiGianChoTruocKhiChim = 15f;

    [Tooltip("Độ sâu chìm xuống dưới mặt đất (mét).")]
    [SerializeField] private float doSauChimXuongDat = 2.5f;

    [Tooltip("Thời gian chạy hiệu ứng chìm xuống lòng đất trước khi Destroy (giây).")]
    [SerializeField] private float thoiGianChimXuongDat = 2.5f;

    [Tooltip("Nếu bật, tự bật Rigidbody/Collider cho các khớp con để làm ragdoll ngay cả khi chưa tạo sẵn Rigidbody con.")]
    [SerializeField] private bool tuDongKichHoatKhungXuongRagdoll = true;

    [Header("4. Thanh máu & Thanh giáp UI (Tùy chọn)")]
    [Tooltip("Kéo Slider thanh máu của Player (trên HUD) hoặc thanh máu trên đầu Quái vào đây.")]
    [SerializeField] private Slider thanhMauSlider;

    [Tooltip("Kéo Slider thanh năng lượng giáp vào đây (tùy chọn).")]
    [SerializeField] private Slider thanhGiapSlider;

    [Tooltip("Tự động ẩn thanh máu của Quái khi đầy máu hoặc khi đã chết.")]
    [SerializeField] private bool anThanhMauKhiDay = true;

    [Header("5. Âm thanh & Hiệu ứng")]
    [SerializeField] private AudioSource nguonAmThanh;
    [SerializeField] private AudioClip amThanhTrungDon;
    [SerializeField] private AudioClip amThanhGiapChan;
    [SerializeField] private AudioClip amThanhHoiGiap;
    [SerializeField] private AudioClip amThanhChet;
    [Range(0f, 1f)]
    [SerializeField] private float amLuong = 0.9f;

    [Tooltip("Prefab hiệu ứng nổ/máu/tia lửa sinh ra khi bị hạ gục (tùy chọn).")]
    [SerializeField] private GameObject hieuUngKhiChet;

    [Header("6. Sự kiện mở rộng (Events)")]
    public UnityEvent<float, float> onMauThayDoi; // (mauHienTai, mauToiDa)
    public UnityEvent<float, float> onGiapThayDoi; // (giapHienTai, giapToiDa)
    public UnityEvent onTrungDon;
    public UnityEvent onChet;

    private float mauHienTai;
    private float giapHienTai;
    private bool daChet = false;
    private float thoiDiemTrungDonCuoi = -999f;
    private Coroutine quyTrinhRagdollVaChimRoutine;

    // Tham chiếu tự động tới AI để quái quay lại truy đuổi ngay khi bị bắn từ xa
    private MeleeEnemyAI meleeAI;
    private RangedEnemyAI rangedAI;
    private NavMeshAgent navMeshAgent;
    private Animator animator;

    // Các Property công khai để UI hoặc script khác đọc
    public LoaiDoiTuong Phe => loaiDoiTuong;
    public float MauHienTai => mauHienTai;
    public float MauToiDa => mauToiDa;
    public float TiLeMau => mauToiDa > 0f ? mauHienTai / mauToiDa : 0f;
    public float GiapHienTai => giapHienTai;
    public float GiapToiDa => giapToiDa;
    public float TiLeGiap => giapToiDa > 0f ? giapHienTai / giapToiDa : 0f;
    public bool CoGiapNangLuong => dungGiapNangLuong && giapHienTai > 0.001f;
    public bool DaChet => daChet;

    private void Awake()
    {
        mauToiDa = Mathf.Max(1f, mauToiDa);
        giapToiDa = Mathf.Max(0f, giapToiDa);

        mauHienTai = mauToiDa;
        giapHienTai = giapKhoiDau >= 0f
            ? Mathf.Clamp(giapKhoiDau, 0f, giapToiDa)
            : giapToiDa;

        if (nguonAmThanh == null)
        {
            nguonAmThanh = GetComponent<AudioSource>();
            if (nguonAmThanh == null)
            {
                nguonAmThanh = gameObject.AddComponent<AudioSource>();
                nguonAmThanh.playOnAwake = false;
            }
        }

        if (loaiDoiTuong == LoaiDoiTuong.QuaiVat)
        {
            meleeAI = GetComponent<MeleeEnemyAI>();
            rangedAI = GetComponent<RangedEnemyAI>();
            navMeshAgent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
        }

        KhoiTaoUI();
    }

    private void Update()
    {
        if (daChet) return;

        if (tuDongHoiMau && mauHienTai < mauToiDa && Time.time >= thoiDiemTrungDonCuoi + thoiGianChoHoiMau)
        {
            HoiMau(tocDoHoiMau * Time.deltaTime);
        }
    }

    /// <summary>
    /// Hàm nhận sát thương chuẩn (tương thích với SendMessage("TakeDamage", amount) từ mọi script vũ khí và quái).
    /// </summary>
    public void TakeDamage(float amount)
    {
        NhanSatThuong(amount);
    }

    /// <summary>
    /// Xử lý trừ giáp/máu, kích hoạt phản ứng AI, cập nhật UI và kiểm tra hạ gục.
    /// Khi còn năng lượng giáp: chỉ mất tối đa 10% máu tối đa trở xuống tùy theo sát thương.
    /// </summary>
    public void NhanSatThuong(float satThuongGoc)
    {
        if (daChet || satThuongGoc <= 0f) return;

        // Kiểm tra thời gian bất tử tạm thời (i-frames)
        if (thoiGianBatTuSauTrungDon > 0f && Time.time < thoiDiemTrungDonCuoi + thoiGianBatTuSauTrungDon)
        {
            return;
        }

        thoiDiemTrungDonCuoi = Time.time;
        float satThuongVaoMau = 0f;
        bool daChanBangGiap = false;

        if (dungGiapNangLuong && giapHienTai > 0.001f)
        {
            daChanBangGiap = true;

            // Mức mất máu tối đa theo cấu hình (mặc định 10% máu tối đa)
            float tranMauMat = mauToiDa * Mathf.Clamp01(tiLeMauMatToiDaKhiCoGiap);

            // Làm mượt đường cong sát thương khi trúng giáp để đạn súng không bị bóp quá nhỏ
            float nguongAnToan = Mathf.Max(0.01f, nguongDamageDatTranGiap);
            float tyLeDamage = Mathf.Clamp01(satThuongGoc / nguongAnToan);
            float heSoDamage = loaiDoiTuong == LoaiDoiTuong.QuaiVat
                ? Mathf.Sqrt(tyLeDamage)
                : tyLeDamage;

            float satThuongToiThieu = loaiDoiTuong == LoaiDoiTuong.QuaiVat
                ? Mathf.Min(satThuongGoc * 0.5f, tranMauMat)
                : 0f;

            satThuongVaoMau = Mathf.Max(satThuongToiThieu, Mathf.Min(satThuongGoc, tranMauMat * heSoDamage));
            satThuongVaoMau = Mathf.Clamp(satThuongVaoMau, 0f, tranMauMat);

            // Trừ năng lượng giáp (quái tiêu hao giáp mượt hơn để cảm nhận tụt giáp rõ)
            float heSoGiap = loaiDoiTuong == LoaiDoiTuong.QuaiVat
                ? Mathf.Max(1f, heSoTieuHaoGiap)
                : Mathf.Max(0f, heSoTieuHaoGiap);
            float luongGiapTieuHao = satThuongGoc * heSoGiap;
            giapHienTai = Mathf.Max(0f, giapHienTai - luongGiapTieuHao);
            onGiapThayDoi?.Invoke(giapHienTai, giapToiDa);
        }
        else
        {
            // Hết giáp năng lượng: trừ máu bình thường (có áp dụng giáp phòng thủ cơ bản)
            satThuongVaoMau = Mathf.Max(1f, satThuongGoc - giapPhongThu);
        }

        mauHienTai = Mathf.Clamp(mauHienTai - satThuongVaoMau, 0f, mauToiDa);

        // Báo cho AI biết vừa bị tấn công để truy đuổi ngay lập tức
        if (loaiDoiTuong == LoaiDoiTuong.QuaiVat)
        {
            if (meleeAI != null) meleeAI.NhanSatThuong(satThuongVaoMau);
            if (rangedAI != null) rangedAI.NhanSatThuong(satThuongVaoMau);
        }

        CapNhatUI();
        onTrungDon?.Invoke();
        onMauThayDoi?.Invoke(mauHienTai, mauToiDa);

        if (mauHienTai <= 0f)
        {
            XuLyKhiChet();
        }
        else if (daChanBangGiap && amThanhGiapChan != null)
        {
            PhatAmThanh(amThanhGiapChan);
        }
        else
        {
            PhatAmThanh(amThanhTrungDon);
        }
    }

    /// <summary>
    /// Hồi phục năng lượng giáp khi nhặt pin hoặc nhận buff giáp.
    /// </summary>
    public void HoiGiap(float luongGiapHoi)
    {
        if (daChet || luongGiapHoi <= 0f || giapToiDa <= 0f) return;

        dungGiapNangLuong = true;
        giapHienTai = Mathf.Clamp(giapHienTai + luongGiapHoi, 0f, giapToiDa);

        CapNhatUI();
        onGiapThayDoi?.Invoke(giapHienTai, giapToiDa);
        PhatAmThanh(amThanhHoiGiap);
    }

    /// <summary>
    /// Hồi phục một lượng máu nhất định (dùng cho bình máu, kỹ năng hoặc tự hồi).
    /// </summary>
    public void HoiMau(float luongMauHoi)
    {
        if (daChet || luongMauHoi <= 0f) return;

        mauHienTai = Mathf.Clamp(mauHienTai + luongMauHoi, 0f, mauToiDa);
        CapNhatUI();
        onMauThayDoi?.Invoke(mauHienTai, mauToiDa);
    }

    /// <summary>
    /// Hồi sinh lại đối tượng với đầy máu và giáp.
    /// </summary>
    public void HoiSinh()
    {
        if (quyTrinhRagdollVaChimRoutine != null)
        {
            StopCoroutine(quyTrinhRagdollVaChimRoutine);
            quyTrinhRagdollVaChimRoutine = null;
        }

        daChet = false;
        mauHienTai = mauToiDa;
        giapHienTai = giapToiDa;
        CapNhatUI();
        onMauThayDoi?.Invoke(mauHienTai, mauToiDa);
        onGiapThayDoi?.Invoke(giapHienTai, giapToiDa);
    }

    private void XuLyKhiChet()
    {
        if (daChet) return;
        daChet = true;

        PhatAmThanh(amThanhChet);

        if (hieuUngKhiChet != null)
        {
            Instantiate(hieuUngKhiChet, transform.position + Vector3.up * 0.8f, Quaternion.identity);
        }

        if (thanhMauSlider != null && loaiDoiTuong == LoaiDoiTuong.QuaiVat)
        {
            thanhMauSlider.gameObject.SetActive(false);
        }

        onChet?.Invoke();

        if (loaiDoiTuong == LoaiDoiTuong.QuaiVat)
        {
            // Tắt toàn bộ logic AI & điều hướng để xác quái không còn bị AI kéo/ép vị trí
            if (meleeAI != null) meleeAI.enabled = false;
            if (rangedAI != null) rangedAI.enabled = false;

            if (navMeshAgent != null)
            {
                if (navMeshAgent.enabled)
                {
                    navMeshAgent.isStopped = true;
                    navMeshAgent.ResetPath();
                }
                navMeshAgent.enabled = false;
            }

            // Tắt Animator để ragdoll hoạt động tự do thay vì bị animation khóa tư thế
            if (animator != null)
            {
                animator.enabled = false;
            }

            // Tắt các MonoBehaviour khác trên quái (trừ HealthSystem) để không can thiệp transform/physics khi đang nằm xác
            MonoBehaviour[] cacScriptKhac = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < cacScriptKhac.Length; i++)
            {
                MonoBehaviour mb = cacScriptKhac[i];
                if (mb == null || mb == this) continue;
                string tenType = mb.GetType().Name.ToLowerInvariant();
                if (tenType.Contains("ai") || tenType.Contains("enemy") || tenType.Contains("attack") || tenType.Contains("controller") || tenType.Contains("movement"))
                {
                    mb.enabled = false;
                }
            }

            if (quyTrinhRagdollVaChimRoutine != null)
            {
                StopCoroutine(quyTrinhRagdollVaChimRoutine);
            }
            quyTrinhRagdollVaChimRoutine = StartCoroutine(RagdollRoiCho15GiayRoiChimXuongDat());
        }
    }

    /// <summary>
    /// Bỏ khóa Freeze Rotation (X, Y, Z) trên Rigidbody để ragdoll xoay/đổ tự nhiên.
    /// </summary>
    private static void MoKhoaRotationRigidbody(Rigidbody rb)
    {
        if (rb == null) return;
        rb.constraints &= ~RigidbodyConstraints.FreezeRotation;
    }

    private IEnumerator RagdollRoiCho15GiayRoiChimXuongDat()
    {
        // 1. Kích hoạt Ragdoll: bật Rigidbody & Collider trên các khớp/xương con và bỏ khóa rotation nếu có tick
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>(true);
        Collider[] cols = GetComponentsInChildren<Collider>(true);

        if (batRagdollKhiChet)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                {
                    cols[i].enabled = true;
                    cols[i].isTrigger = false;
                }
            }

            bool coRigidbodyCon = false;
            for (int i = 0; i < rbs.Length; i++)
            {
                Rigidbody rb = rbs[i];
                if (rb == null) continue;

                // Bỏ tick Freeze Rotation (FreezeRotationX / Y / Z) để ragdoll đổ tự do
                MoKhoaRotationRigidbody(rb);

                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                if (rb.gameObject != gameObject)
                {
                    coRigidbodyCon = true;
                }
            }

            // Nếu quái chưa dựng sẵn ragdoll con mà bật tự động kích hoạt khung xương:
            // bật Rigidbody cho các Transform con có Renderer/Collider (tránh gắn trùng vô tội vạ nếu đã có sẵn)
            if (!coRigidbodyCon && tuDongKichHoatKhungXuongRagdoll)
            {
                Transform[] children = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    Transform t = children[i];
                    if (t == null || t == transform) continue;

                    bool coCol = t.TryGetComponent(out Collider c);
                    bool coMesh = t.TryGetComponent(out Renderer _);
                    if (!coCol && !coMesh) continue;

                    if (!coCol)
                    {
                        SphereCollider sc = t.gameObject.AddComponent<SphereCollider>();
                        sc.radius = 0.12f;
                    }
                    else
                    {
                        c.enabled = true;
                        c.isTrigger = false;
                    }

                    if (!t.TryGetComponent(out Rigidbody boneRb))
                    {
                        boneRb = t.gameObject.AddComponent<Rigidbody>();
                    }
                    MoKhoaRotationRigidbody(boneRb);
                    boneRb.mass = Mathf.Max(0.5f, boneRb.mass);
                    boneRb.isKinematic = false;
                    boneRb.useGravity = true;
                    boneRb.interpolation = RigidbodyInterpolation.Interpolate;
                    boneRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                }
            }
        }

        // 2. Đứng yên/nằm ragdoll trên mặt đất đủ 15 giây (hoặc thời gian cấu hình)
        float choThoiGian = Mathf.Max(0f, thoiGianChoTruocKhiChim);
        if (choThoiGian > 0f)
        {
            yield return new WaitForSeconds(choThoiGian);
        }

        // 3. Trước khi chìm xuống lòng đất: chuyển các Rigidbody sang kinematic & tắt collider cản đường
        // để quái chìm mượt xuống đất mà không bị vật lý đẩy giật ngược lên
        rbs = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rbs.Length; i++)
        {
            if (rbs[i] == null) continue;
            rbs[i].linearVelocity = Vector3.zero;
            rbs[i].angularVelocity = Vector3.zero;
            rbs[i].isKinematic = true;
            rbs[i].useGravity = false;
        }

        cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
            {
                cols[i].enabled = false;
            }
        }

        // 4. Chìm từ từ xuống lòng đất rồi hủy GameObject
        float thoiGianChim = Mathf.Max(0.1f, thoiGianChimXuongDat);
        float doSau = Mathf.Max(0.1f, doSauChimXuongDat);

        Vector3 viTriBatDau = transform.position;
        Vector3 viTriDich = viTriBatDau + Vector3.down * doSau;

        float elapsed = 0f;
        while (elapsed < thoiGianChim)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / thoiGianChim);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            transform.position = Vector3.Lerp(viTriBatDau, viTriDich, smoothT);
            yield return null;
        }

        transform.position = viTriDich;
        quyTrinhRagdollVaChimRoutine = null;
        Destroy(gameObject);
    }

    private void KhoiTaoUI()
    {
        if (thanhMauSlider != null)
        {
            thanhMauSlider.minValue = 0f;
            thanhMauSlider.maxValue = mauToiDa;
            thanhMauSlider.value = mauHienTai;

            if (loaiDoiTuong == LoaiDoiTuong.QuaiVat && anThanhMauKhiDay)
            {
                thanhMauSlider.gameObject.SetActive(false);
            }
        }

        if (thanhGiapSlider != null)
        {
            thanhGiapSlider.minValue = 0f;
            thanhGiapSlider.maxValue = giapToiDa;
            thanhGiapSlider.value = giapHienTai;
        }
    }

    private void CapNhatUI()
    {
        if (thanhMauSlider != null)
        {
            if (loaiDoiTuong == LoaiDoiTuong.QuaiVat && anThanhMauKhiDay && !daChet)
            {
                bool hienThi = mauHienTai < mauToiDa;
                if (thanhMauSlider.gameObject.activeSelf != hienThi)
                {
                    thanhMauSlider.gameObject.SetActive(hienThi);
                }
            }

            thanhMauSlider.maxValue = mauToiDa;
            thanhMauSlider.value = mauHienTai;
        }

        if (thanhGiapSlider != null)
        {
            thanhGiapSlider.maxValue = giapToiDa;
            thanhGiapSlider.value = giapHienTai;
        }
    }

    private void PhatAmThanh(AudioClip clip)
    {
        if (clip != null && nguonAmThanh != null)
        {
            nguonAmThanh.PlayOneShot(clip, amLuong);
        }
    }
}
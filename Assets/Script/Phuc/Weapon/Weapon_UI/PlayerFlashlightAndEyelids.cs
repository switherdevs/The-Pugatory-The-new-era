using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerFlashlightAndEyelids : MonoBehaviour
{
    [Header("1. Thiết Lập Đèn Pin (Flashlight)")]
    [Tooltip("Component Light đèn pin (nếu để trống sẽ tự tìm Light con của Player/Camera).")]
    [SerializeField] private Light denPinLight;

    [Tooltip("Bật đèn pin ngay khi bắt đầu.")]
    [SerializeField] private bool batDenPinKhiKhoiDong = false;

    [Tooltip("Phím bật/tắt đèn pin (nếu để trống sẽ tự dùng phím F).")]
    [SerializeField] private InputActionReference hanhDongBatTatDenPin;

    [SerializeField] private AudioSource nguonAmThanhDenPin;
    [SerializeField] private AudioClip amThanhBatDenPin;
    [SerializeField] private AudioClip amThanhTatDenPin;
    [Range(0f, 1f)]
    [SerializeField] private float amLuongDenPin = 0.8f;

    [Header("2. Thiết Lập Nhắm Mắt (Eyelids / Che Mắt)")]
    [Tooltip("UI Image che mắt khi nhắm mắt (sẽ tự bật/tắt GameObject & chỉnh độ alpha).")]
    [SerializeField] private Image anhCheMatUI;

    [Tooltip("Phím giữ để nhắm mắt (nếu để trống sẽ tự dùng phím C).")]
    [SerializeField] private InputActionReference hanhDongNhamMat;

    [Tooltip("Độ alpha tối đa của Image che mắt khi đang giữ nhắm mắt (0 = trong suốt, 1 = che kín hoàn toàn).")]
    [Range(0f, 1f)]
    [SerializeField] private float doAlphaKhiNhamMat = 0.92f;

    [Tooltip("Tốc độ chuyển đổi alpha khi nhắm/mở mắt.")]
    [SerializeField] private float tocDoChuyenAlpha = 14f;

    private bool denPinDangBat = false;
    private bool dangNhamMat = false;
    private float alphaHienTai = 0f;

    private InputAction hanhDongDenPinTamThoi;
    private InputAction hanhDongNhamMatTamThoi;

    public bool IsFlashlightOn => denPinDangBat;
    public bool IsClosingEyes => dangNhamMat;
    public float CurrentEyelidAlpha => alphaHienTai;

    private InputAction HanhDongDenPinDangDung =>
        hanhDongBatTatDenPin != null && hanhDongBatTatDenPin.action != null
            ? hanhDongBatTatDenPin.action
            : hanhDongDenPinTamThoi;

    private InputAction HanhDongNhamMatDangDung =>
        hanhDongNhamMat != null && hanhDongNhamMat.action != null
            ? hanhDongNhamMat.action
            : hanhDongNhamMatTamThoi;

    private void Awake()
    {
        TuDongGanThanhPhan();

        hanhDongDenPinTamThoi = new InputAction("ToggleFlashlight", InputActionType.Button, "<Keyboard>/f");
        hanhDongNhamMatTamThoi = new InputAction("CloseEyes", InputActionType.Button, "<Keyboard>/c");

        denPinDangBat = batDenPinKhiKhoiDong;
        ApDungTrangThaiDenPin(phatAmThanh: false);
        ApDungAlphaCheMat(0f, batBuocCapNhat: true);
    }

    private void OnEnable()
    {
        HanhDongDenPinDangDung?.Enable();
        HanhDongNhamMatDangDung?.Enable();
        ApDungTrangThaiDenPin(phatAmThanh: false);
    }

    private void OnDisable()
    {
        dangNhamMat = false;
        alphaHienTai = 0f;
        ApDungAlphaCheMat(0f, batBuocCapNhat: true);

        HanhDongDenPinDangDung?.Disable();
        HanhDongNhamMatDangDung?.Disable();
    }

    private void OnDestroy()
    {
        hanhDongDenPinTamThoi?.Dispose();
        hanhDongNhamMatTamThoi?.Dispose();
    }

    private void TuDongGanThanhPhan()
    {
        if (denPinLight == null)
        {
            Light[] lights = GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Spot)
                {
                    denPinLight = lights[i];
                    break;
                }
            }
            if (denPinLight == null && lights.Length > 0)
            {
                denPinLight = lights[0];
            }
        }

        if (nguonAmThanhDenPin == null)
        {
            nguonAmThanhDenPin = GetComponent<AudioSource>();
            if (nguonAmThanhDenPin == null)
            {
                nguonAmThanhDenPin = gameObject.AddComponent<AudioSource>();
                nguonAmThanhDenPin.playOnAwake = false;
            }
        }
    }

    private void Update()
    {
        if (HanhDongDenPinDangDung != null && HanhDongDenPinDangDung.WasPressedThisFrame())
        {
            denPinDangBat = !denPinDangBat;
            ApDungTrangThaiDenPin(phatAmThanh: true);
        }

        dangNhamMat = HanhDongNhamMatDangDung != null && HanhDongNhamMatDangDung.IsPressed();

        float targetAlpha = dangNhamMat ? Mathf.Clamp01(doAlphaKhiNhamMat) : 0f;
        float t = 1f - Mathf.Exp(-tocDoChuyenAlpha * Time.unscaledDeltaTime);
        alphaHienTai = Mathf.Lerp(alphaHienTai, targetAlpha, t);

        if (Mathf.Abs(alphaHienTai - targetAlpha) < 0.002f)
        {
            alphaHienTai = targetAlpha;
        }

        ApDungAlphaCheMat(alphaHienTai, batBuocCapNhat: false);
    }

    public void SetFlashlightEnabled(bool bat, bool phatAmThanh = true)
    {
        denPinDangBat = bat;
        ApDungTrangThaiDenPin(phatAmThanh);
    }

    private void ApDungTrangThaiDenPin(bool phatAmThanh)
    {
        if (denPinLight != null)
        {
            denPinLight.enabled = denPinDangBat;
        }

        if (phatAmThanh && nguonAmThanhDenPin != null)
        {
            AudioClip clip = denPinDangBat ? amThanhBatDenPin : amThanhTatDenPin;
            if (clip != null)
            {
                nguonAmThanhDenPin.PlayOneShot(clip, amLuongDenPin);
            }
        }
    }

    private void ApDungAlphaCheMat(float alpha, bool batBuocCapNhat)
    {
        if (anhCheMatUI == null) return;

        bool canHien = alpha > 0.001f;
        if (batBuocCapNhat || anhCheMatUI.gameObject.activeSelf != canHien)
        {
            anhCheMatUI.gameObject.SetActive(canHien);
        }

        Color mau = anhCheMatUI.color;
        mau.a = Mathf.Clamp01(alpha);
        anhCheMatUI.color = mau;
    }
}
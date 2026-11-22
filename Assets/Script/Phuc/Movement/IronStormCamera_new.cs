using UnityEngine;
using UnityEngine.InputSystem;

public class IronStormCamera_new : MonoBehaviour
{
    [Header("References")]
    public Transform playerBody;
    public Transform cameraPitchPivot;
    public Camera mainCamera;

    [Header("Mouse Look")]
    public float sensitivity = 2f;
    public bool invertY = false;
    public float minPitch = -80f;
    public float maxPitch = 80f;
    public InputActionReference lookAction;

    [Header("Camera Bob")]
    public float walkBobFrequency = 8f;
    public float walkBobAmplitude = 0.035f;
    public float runBobFrequency = 11f;
    public float runBobAmplitude = 0.055f;
    public float bobSmooth = 10f;
    public float jumpBobAmount = 0.08f;

    [Header("Recoil")]
    public float recoilReturnSpeed = 8f;
    public float maxRecoilPitch = 12f;
    public float maxRecoilYaw = 5f;

    [Header("Shake")]
    public float shakeReturnSpeed = 12f;

    [Header("Jump Pack FOV")]
    public float flightFovIncrease = 8f;
    public float boostFovIncrease = 14f;
    public float fovTransitionSpeed = 5f;

    private float pitch;
    private float recoilPitch;
    private float recoilYaw;
    private float recoilPitchTarget;
    private float recoilYawTarget;

    private float shakeStrength;
    private float jumpOffset;
    private float bobTime;
    private float baseFov;

    private bool jumpPackFlying;
    private bool jumpPackBoosting;

    private Vector3 effectsBasePosition;
    private Quaternion effectsBaseRotation;
    private IronStormMovement movementRef;

    private void OnEnable()
    {
        if (lookAction != null && lookAction.action != null)
        {
            lookAction.action.Enable();
        }
        LockCursor();
    }

    private void OnDisable()
    {
        if (lookAction != null && lookAction.action != null)
        {
            lookAction.action.Disable();
        }
    }

    private void Awake()
    {
        if (!mainCamera)
            mainCamera = GetComponentInChildren<Camera>();

        if (!cameraPitchPivot && transform.parent)
            cameraPitchPivot = transform.parent;

        if (!playerBody && cameraPitchPivot && cameraPitchPivot.parent)
            playerBody = cameraPitchPivot.parent;

        if (playerBody)
            movementRef = playerBody.GetComponent<IronStormMovement>();

        if (mainCamera)
            baseFov = mainCamera.fieldOfView;

        effectsBasePosition = transform.localPosition;
        effectsBaseRotation = transform.localRotation;

        pitch = 0f;
        LockCursor();
    }

    private void Update()
    {
        if (movementRef == null && playerBody != null)
            movementRef = playerBody.GetComponent<IronStormMovement>();

        HandleLook();
        HandleBob();
        HandleJumpInput();
        HandleRecoil();
        HandleShake();
        HandleFov();
        ApplyEffects();
    }

    private Vector2 ReadMouseDelta()
    {
        if (lookAction != null && lookAction.action != null)
        {
            return lookAction.action.ReadValue<Vector2>() * 0.05f;
        }

        if (Mouse.current != null)
        {
            return Mouse.current.delta.ReadValue() * 0.05f;
        }

        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }

    private void HandleLook()
    {
        if (!playerBody || !cameraPitchPivot)
            return;

        Vector2 lookDelta = ReadMouseDelta() * sensitivity;
        float mouseX = lookDelta.x;
        float mouseY = invertY ? -lookDelta.y : lookDelta.y;

        // Xoay ngang trên playerBody quanh trục Y thế giới để không làm méo object gắn theo
        playerBody.Rotate(Vector3.up * mouseX, Space.World);

        // Xoay dọc trên pivot riêng, khóa roll Z = 0
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        cameraPitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleBob()
    {
        Vector2 moveInput = movementRef != null
            ? movementRef.CurrentInputDirection
            : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        bool moving = moveInput.sqrMagnitude > 0.01f;
        bool running = movementRef != null
            ? movementRef.IsRunning
            : (moving && Input.GetKey(KeyCode.LeftShift));

        float frequency = running ? runBobFrequency : walkBobFrequency;
        float amplitude = running ? runBobAmplitude : walkBobAmplitude;

        Vector3 target = effectsBasePosition;

        if (moving)
        {
            bobTime += Time.deltaTime * frequency;
            target.y += Mathf.Sin(bobTime) * amplitude;
            target.x += Mathf.Cos(bobTime * 0.5f) * amplitude * 0.35f;
        }

        jumpOffset = Mathf.MoveTowards(jumpOffset, 0f, Time.deltaTime * 1.8f);
        target.y += jumpOffset;

        effectsBasePosition = Vector3.Lerp(effectsBasePosition, target, Time.deltaTime * bobSmooth);
    }

    private void HandleJumpInput()
    {
        bool jumped = movementRef != null && movementRef.IsGrounded && Input.GetKeyDown(KeyCode.Space);
        if (jumped)
            jumpOffset += jumpBobAmount;
    }

    private void HandleRecoil()
    {
        recoilPitch = Mathf.Lerp(recoilPitch, recoilPitchTarget, Time.deltaTime * recoilReturnSpeed);
        recoilYaw = Mathf.Lerp(recoilYaw, recoilYawTarget, Time.deltaTime * recoilReturnSpeed);

        recoilPitchTarget = Mathf.Lerp(recoilPitchTarget, 0f, Time.deltaTime * recoilReturnSpeed);
        recoilYawTarget = Mathf.Lerp(recoilYawTarget, 0f, Time.deltaTime * recoilReturnSpeed);
    }

    private void HandleShake()
    {
        shakeStrength = Mathf.MoveTowards(shakeStrength, 0f, Time.deltaTime * shakeReturnSpeed);
    }

    private void HandleFov()
    {
        if (!mainCamera)
            return;

        float targetFov = baseFov;

        if (jumpPackFlying)
            targetFov += flightFovIncrease;

        if (jumpPackBoosting)
            targetFov += boostFovIncrease;

        mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFov, Time.deltaTime * fovTransitionSpeed);
    }

    private void ApplyEffects()
    {
        Vector3 pos = effectsBasePosition;

        if (shakeStrength > 0f)
        {
            pos += Random.insideUnitSphere * shakeStrength * 0.025f;
        }

        transform.localPosition = pos;

        Quaternion shakeRot = Quaternion.identity;
        if (shakeStrength > 0f)
        {
            shakeRot = Quaternion.Euler(Random.insideUnitSphere * shakeStrength * 0.6f);
        }

        transform.localRotation =
            effectsBaseRotation
            * Quaternion.Euler(-recoilPitch, recoilYaw, 0f)
            * shakeRot;
    }

    public void AddRecoil(float up, float side)
    {
        recoilPitchTarget = Mathf.Clamp(recoilPitchTarget + Mathf.Abs(up), 0f, maxRecoilPitch);
        recoilYawTarget = Mathf.Clamp(recoilYawTarget + Random.Range(-Mathf.Abs(side), Mathf.Abs(side)), -maxRecoilYaw, maxRecoilYaw);
        AddShootShake(Mathf.Clamp(Mathf.Abs(up) * 0.12f, 0.2f, 1.5f));
    }

    public void AddShootShake()
    {
        AddShootShake(1f);
    }

    public void AddShootShake(float strength)
    {
        shakeStrength = Mathf.Max(shakeStrength, Mathf.Clamp(strength, 0f, 5f));
    }

    public void SetJumpPackFlying(bool active, bool boosting)
    {
        jumpPackFlying = active;
        jumpPackBoosting = active && boosting;
    }

    public void SetSensitivity(float value)
    {
        sensitivity = Mathf.Max(0.01f, value);
    }

    public float GetSensitivity()
    {
        return sensitivity;
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
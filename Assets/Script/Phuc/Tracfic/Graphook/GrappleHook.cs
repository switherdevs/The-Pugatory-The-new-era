using UnityEngine;
using UnityEngine.InputSystem;

public class GrappleHook : MonoBehaviour
{
    [Header("New Input System")]
    [Tooltip("Giữ phím để bắn và giữ dây móc; thả phím để buông dây (mặc định phím F nếu để trống).")]
    [SerializeField] private InputActionReference grappleAction;
    [Tooltip("Vector2 di chuyển ngang để điều khiển đà lắc lư khi đang đu dây (mặc định WASD nếu để trống).")]
    [SerializeField] private InputActionReference moveAction;
    [Tooltip("Giữ phím để thu dây kéo người chơi lên đứng trên cột mốc khi đang móc dây (mặc định phím E nếu để trống).")]
    [SerializeField] private InputActionReference pullToStandAction;

    [Header("Target Filtering (Layer & Tag)")]
    [Tooltip("Các Layer cho phép bắn dây móc trúng.")]
    [SerializeField] private LayerMask grappleableLayers = ~0;
    [Tooltip("Tag hợp lệ để bắn móc (để trống nếu chỉ cần lọc theo Layer).")]
    [SerializeField] private string requiredTag = "";
    [Tooltip("Tầm bắn dây móc tối đa (mét).")]
    [SerializeField] private float maxGrappleDistance = 45f;

    [Header("Suspend Rope & Pendulum Swing Physics")]
    [Tooltip("Độ dài sợi dây khi người chơi đang lơ lửng / đung đưa (mét).")]
    [SerializeField] private float suspendRopeLength = 6f;
    [Tooltip("Trọng lực kéo người chơi rơi xuống để tạo độ treo lơ lửng bên dưới cột mốc.")]
    [SerializeField] private float pendulumGravity = 18f;
    [Tooltip("Độ siết giữ độ dài dây khi người chơi văng xa hơn suspendRopeLength.")]
    [SerializeField] private float ropeTensionStiffness = 28f;
    [Tooltip("Bật/tắt lắc lư qua lại theo vật lý khi giữ dây chưa kéo lên cột móc.")]
    [SerializeField] private bool enableSwingPhysics = true;
    [Tooltip("Biên độ lực lắc lư qua lại tự động khi đang treo dây.")]
    [SerializeField] private float swingForce = 14f;
    [Tooltip("Tần số đung đưa qua lại của con lắc.")]
    [SerializeField] private float swingFrequency = 2.2f;
    [Tooltip("Hệ số giảm chấn để dao động lắc lư mượt, không bị văng vô hạn.")]
    [SerializeField] private float swingDamping = 2.2f;
    [Tooltip("Lực người chơi tự tạo đà lắc qua lại bằng phím di chuyển (WASD) khi đang đu.")]
    [SerializeField] private float playerSwingControl = 16f;

    [Header("Pull To Stand & Climb Physics")]
    [Tooltip("Tốc độ kéo rút dây lên đứng trên cột mốc khi giữ phím kéo lên.")]
    [SerializeField] private float pullSpeed = 20f;
    [Tooltip("Gia tốc tăng tốc khi kéo lên mốc.")]
    [SerializeField] private float pullAcceleration = 45f;
    [Tooltip("Lực nâng phụ khi vướng tường lúc đang kéo lên cột mốc.")]
    [SerializeField] private float wallClimbBoostSpeed = 10f;
    [Tooltip("Khoảng cách vượt qua điểm móc để kéo người chơi lên đứng trên mốc.")]
    [SerializeField] private float overshootDistance = 1.0f;
    [Tooltip("Độ cao bù trừ để chân người chơi đứng vững trên điểm móc.")]
    [SerializeField] private float standHeightOffset = 0.25f;
    [Tooltip("Khoảng cách coi như đã tới điểm móc để đứng lên.")]
    [SerializeField] private float arriveThreshold = 0.9f;
    [Tooltip("Thời gian hồi chiêu giữa hai lần bắn móc (giây).")]
    [SerializeField] private float cooldown = 0.35f;

    [Header("Gun Aim & Smooth Line")]
    [Tooltip("Tốc độ súng xoay hướng về điểm móc theo thời gian thực.")]
    [SerializeField] private float gunAimSpeed = 18f;
    [Tooltip("Tốc độ đầu dây móc phóng ra tới điểm móc (chưa kéo người chơi cho đến khi chạm mốc).")]
    [SerializeField] private float hookShootSpeed = 95f;
    [Tooltip("Tốc độ thu dây móc về súng khi buông tay.")]
    [SerializeField] private float hookRetractSpeed = 110f;
    [Tooltip("Độ dày dây móc khi tự tạo LineRenderer.")]
    [SerializeField] private float lineWidth = 0.04f;
    [Tooltip("Màu dây móc khi tự tạo LineRenderer.")]
    [SerializeField] private Color lineColor = Color.cyan;

    [Header("Audio")]
    [SerializeField] private AudioClip shootHookClip;
    [SerializeField] private AudioClip detachHookClip;
    [Range(0f, 1f)][SerializeField] private float audioVolume = 0.9f;

    private CharacterController controller;
    private Camera playerCamera;
    private Transform playerRoot;
    private Transform hookOrigin;
    private LineRenderer lineRenderer;
    private IronStormMovement movementScript;
    private AudioSource audioSource;

    private Quaternion initialLocalRotation;
    private InputAction runtimeGrappleAction;
    private InputAction runtimeMoveAction;
    private InputAction runtimePullToStandAction;

    private bool isHoldingInput = false;
    private bool isGrappling = false;
    private bool isHookAttached = false;
    private bool isRetractingLine = false;
    private bool isPullingToStand = false;
    private bool hasReachedAnchor = false;

    private Vector3 grapplePoint;
    private Vector3 currentHookTipPos;
    private Vector3 pendulumVelocity = Vector3.zero;
    private float swingPhase = 0f;
    private float currentPullSpeed = 0f;
    private float activeRopeLength = 6f;
    private float lastGrappleTime = -999f;

    private InputAction ActiveGrappleAction =>
        grappleAction != null && grappleAction.action != null ? grappleAction.action : runtimeGrappleAction;

    private InputAction ActiveMoveAction =>
        moveAction != null && moveAction.action != null ? moveAction.action : runtimeMoveAction;

    private InputAction ActivePullToStandAction =>
        pullToStandAction != null && pullToStandAction.action != null ? pullToStandAction.action : runtimePullToStandAction;

    private void Awake()
    {
        initialLocalRotation = transform.localRotation;
        AutoFindAndBindComponents();

        if (grappleAction == null)
        {
            runtimeGrappleAction = new InputAction("Grapple", InputActionType.Button, "<Keyboard>/f");
        }

        if (moveAction == null)
        {
            runtimeMoveAction = new InputAction("GrappleMove", InputActionType.Value);
            runtimeMoveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        }

        if (pullToStandAction == null)
        {
            runtimePullToStandAction = new InputAction("PullToStand", InputActionType.Button, "<Keyboard>/e");
        }
    }

    private void AutoFindAndBindComponents()
    {
        controller = GetComponentInParent<CharacterController>();
        if (controller == null)
        {
            controller = FindFirstObjectByType<CharacterController>();
        }

        playerRoot = controller != null ? controller.transform : transform.root;

        movementScript = GetComponentInParent<IronStormMovement>();
        if (movementScript == null && playerRoot != null)
        {
            movementScript = playerRoot.GetComponent<IronStormMovement>();
        }
        if (movementScript == null)
        {
            movementScript = FindFirstObjectByType<IronStormMovement>();
        }

        playerCamera = GetComponentInParent<Camera>();
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
        if (playerCamera == null)
        {
            playerCamera = FindFirstObjectByType<Camera>();
        }

        hookOrigin = FindChildByKeyword(transform, "muzzle", "fire", "hook", "tip", "barrel");
        if (hookOrigin == null)
        {
            hookOrigin = transform;
        }

        lineRenderer = GetComponentInChildren<LineRenderer>();
        if (lineRenderer == null)
        {
            GameObject lineObj = new GameObject("GrappleLineRenderer");
            lineObj.transform.SetParent(transform, false);
            lineRenderer = lineObj.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.numCapVertices = 4;

            Shader unlitShader = Shader.Find("Sprites/Default");
            if (unlitShader != null)
            {
                lineRenderer.material = new Material(unlitShader);
            }
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
        }
        else
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
        }
        lineRenderer.enabled = false;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    private Transform FindChildByKeyword(Transform parent, params string[] keywords)
    {
        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child == parent) continue;
            string lowerName = child.name.ToLower();
            foreach (string kw in keywords)
            {
                if (lowerName.Contains(kw))
                {
                    return child;
                }
            }
        }
        return null;
    }

    private void OnEnable()
    {
        if (controller == null || playerCamera == null || movementScript == null || lineRenderer == null || audioSource == null)
        {
            AutoFindAndBindComponents();
        }
        ActiveGrappleAction?.Enable();
        ActiveMoveAction?.Enable();
        ActivePullToStandAction?.Enable();
    }

    private void OnDisable()
    {
        ReleaseGrapple(playSound: false);
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
        isRetractingLine = false;
        transform.localRotation = initialLocalRotation;
        ActiveGrappleAction?.Disable();
        ActiveMoveAction?.Disable();
        ActivePullToStandAction?.Disable();
    }

    private void OnDestroy()
    {
        if (runtimeGrappleAction != null)
        {
            runtimeGrappleAction.Dispose();
        }
        if (runtimeMoveAction != null)
        {
            runtimeMoveAction.Dispose();
        }
        if (runtimePullToStandAction != null)
        {
            runtimePullToStandAction.Dispose();
        }
    }

    private bool WasGrapplePressedThisFrame()
    {
        if (ActiveGrappleAction != null && ActiveGrappleAction.WasPressedThisFrame())
        {
            return true;
        }
        return grappleAction == null && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
    }

    private bool IsGrappleHeld()
    {
        if (ActiveGrappleAction != null && ActiveGrappleAction.IsPressed())
        {
            return true;
        }
        return grappleAction == null && Keyboard.current != null && Keyboard.current.fKey.isPressed;
    }

    private bool IsPullToStandHeld()
    {
        if (ActivePullToStandAction != null && ActivePullToStandAction.IsPressed())
        {
            return true;
        }
        return pullToStandAction == null && Keyboard.current != null && Keyboard.current.eKey.isPressed;
    }

    private void Update()
    {
        ActiveGrappleAction?.Enable();
        ActiveMoveAction?.Enable();
        ActivePullToStandAction?.Enable();

        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        bool pressedNow = WasGrapplePressedThisFrame();
        bool heldNow = IsGrappleHeld();

        if (pressedNow && !isGrappling && Time.time >= lastGrappleTime + cooldown)
        {
            isHoldingInput = true;
            TryStartGrapple();
        }
        else if (!heldNow && isHoldingInput)
        {
            isHoldingInput = false;
            if (isGrappling)
            {
                ReleaseGrapple(playSound: true);
            }
        }

        if (isGrappling)
        {
            if (!heldNow)
            {
                isHoldingInput = false;
                ReleaseGrapple(playSound: true);
            }
            else
            {
                AimGunTowardGrapplePoint(dt);
                UpdateLineVisual(dt);

                if (isHookAttached)
                {
                    UpdatePendulumSwingAndStand(dt);
                }
            }
        }
        else
        {
            transform.localRotation = Quaternion.Slerp(transform.localRotation, initialLocalRotation, gunAimSpeed * dt);

            if (isRetractingLine)
            {
                UpdateLineRetract(dt);
            }

            if (movementScript != null && !movementScript.enabled)
            {
                movementScript.enabled = true;
            }
        }
    }

    private void TryStartGrapple()
    {
        if (controller == null || !controller.enabled)
        {
            AutoFindAndBindComponents();
            if (controller == null || !controller.enabled) return;
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null) return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, maxGrappleDistance, grappleableLayers, QueryTriggerInteraction.Ignore))
        {
            if (!IsValidTarget(hit.collider.gameObject)) return;

            grapplePoint = hit.point;
            currentHookTipPos = hookOrigin != null ? hookOrigin.position : transform.position;
            isGrappling = true;
            isHookAttached = false;
            isRetractingLine = false;
            isPullingToStand = false;
            hasReachedAnchor = false;
            swingPhase = 0f;
            pendulumVelocity = Vector3.zero;
            currentPullSpeed = Mathf.Min(pullSpeed * 0.5f, pullSpeed);

            float initialDist = Vector3.Distance(controller.transform.position, grapplePoint);
            activeRopeLength = Mathf.Clamp(initialDist, Mathf.Max(0.5f, suspendRopeLength), maxGrappleDistance);

            if (lineRenderer != null)
            {
                lineRenderer.enabled = true;
            }

            if (audioSource != null && shootHookClip != null)
            {
                audioSource.PlayOneShot(shootHookClip, audioVolume);
            }
        }
    }

    private bool IsValidTarget(GameObject target)
    {
        Transform rootToCheck = playerRoot != null ? playerRoot : transform.root;
        if (target.transform.root == rootToCheck) return false;

        if (!string.IsNullOrEmpty(requiredTag))
        {
            Transform current = target.transform;
            bool tagMatched = false;
            while (current != null)
            {
                if (current.CompareTag(requiredTag))
                {
                    tagMatched = true;
                    break;
                }
                current = current.parent;
            }
            if (!tagMatched) return false;
        }

        return true;
    }

    private void AimGunTowardGrapplePoint(float dt)
    {
        Vector3 originPos = hookOrigin != null ? hookOrigin.position : transform.position;
        Vector3 aimDir = grapplePoint - originPos;
        if (aimDir.sqrMagnitude < 0.0001f) return;

        Quaternion targetWorldRot = Quaternion.LookRotation(aimDir.normalized, Vector3.up);
        if (transform.parent != null)
        {
            Quaternion targetLocalRot = Quaternion.Inverse(transform.parent.rotation) * targetWorldRot;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRot, gunAimSpeed * dt);
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetWorldRot, gunAimSpeed * dt);
        }
    }

    private void UpdatePendulumSwingAndStand(float dt)
    {
        if (controller == null || !controller.enabled)
        {
            ReleaseGrapple(playSound: false);
            return;
        }

        if (movementScript != null && movementScript.enabled)
        {
            movementScript.enabled = false;
        }

        Transform moveTarget = controller.transform;
        Vector3 toAnchor = grapplePoint - moveTarget.position;
        float distToAnchor = toAnchor.magnitude;
        Vector3 ropeDir = distToAnchor > 0.0001f ? toAnchor / distToAnchor : Vector3.up;

        bool wantsPullUp = IsPullToStandHeld();
        if (wantsPullUp)
        {
            isPullingToStand = true;
        }

        if (isPullingToStand && distToAnchor <= arriveThreshold)
        {
            hasReachedAnchor = true;
            pendulumVelocity = Vector3.zero;
        }

        if (!hasReachedAnchor)
        {
            float targetRopeLength = Mathf.Max(0.5f, suspendRopeLength);

            if (isPullingToStand)
            {
                // Kéo rút dây rút ngắn dần về mốc để đứng lên trên
                currentPullSpeed = Mathf.MoveTowards(currentPullSpeed, pullSpeed, pullAcceleration * dt);
                activeRopeLength = Mathf.MoveTowards(activeRopeLength, 0.25f, currentPullSpeed * dt);
                pendulumVelocity += ropeDir * (currentPullSpeed * 2.2f * dt);

                bool hitWallAhead = Physics.Raycast(
                    moveTarget.position + Vector3.up * 0.5f,
                    new Vector3(ropeDir.x, 0f, ropeDir.z).normalized,
                    out _,
                    controller.radius + 0.35f,
                    grappleableLayers,
                    QueryTriggerInteraction.Ignore
                );

                if (hitWallAhead || (controller.collisionFlags & CollisionFlags.CollidedSides) != 0)
                {
                    pendulumVelocity.y = Mathf.Max(pendulumVelocity.y, wallClimbBoostSpeed);
                }
            }
            else
            {
                // Chế độ giữ đu dây ổn định: thu dần về suspendRopeLength nếu đang ở xa hơn
                activeRopeLength = Mathf.MoveTowards(activeRopeLength, targetRopeLength, pullSpeed * 0.65f * dt);

                // 1. Trọng lực kéo xuống để tạo tư thế treo lơ lửng dưới điểm móc
                pendulumVelocity += Vector3.down * (pendulumGravity * dt);

                // 2. Lực đung đưa qua lại theo vật lý quanh điểm treo
                if (enableSwingPhysics)
                {
                    Vector3 swingAxis = Vector3.Cross(ropeDir, Vector3.up);
                    if (swingAxis.sqrMagnitude < 0.0001f)
                    {
                        swingAxis = moveTarget.right;
                    }
                    else
                    {
                        swingAxis.Normalize();
                    }

                    swingPhase += dt * swingFrequency;
                    float autoSwing = Mathf.Sin(swingPhase) * swingForce;

                    Vector2 moveInput = ActiveMoveAction != null ? ActiveMoveAction.ReadValue<Vector2>() : Vector2.zero;
                    Vector3 camForward = playerCamera != null ? playerCamera.transform.forward : moveTarget.forward;
                    camForward.y = 0f;
                    if (camForward.sqrMagnitude < 0.0001f) camForward = Vector3.forward;
                    camForward.Normalize();
                    Vector3 camRight = new Vector3(camForward.z, 0f, -camForward.x).normalized;

                    Vector3 inputAccel = (camRight * moveInput.x + camForward * moveInput.y) * playerSwingControl;
                    pendulumVelocity += (swingAxis * autoSwing + inputAccel) * dt;
                }

                // 3. Nếu khoảng cách vượt quá độ dài dây hiện tại, triệt tiêu vận tốc văng ra xa và kéo nhẹ vào trong
                if (distToAnchor > activeRopeLength)
                {
                    Vector3 awayDir = -ropeDir;
                    float awaySpeed = Vector3.Dot(pendulumVelocity, awayDir);
                    if (awaySpeed > 0f)
                    {
                        pendulumVelocity -= awayDir * awaySpeed;
                    }
                    float stretch = distToAnchor - activeRopeLength;
                    pendulumVelocity += ropeDir * (stretch * ropeTensionStiffness * dt);
                }
            }

            // 4. Giảm chấn mượt để dao động không bị giật hoặc văng mất kiểm soát
            pendulumVelocity = Vector3.Lerp(pendulumVelocity, Vector3.zero, Mathf.Clamp01(swingDamping * dt));

            // 5. Di chuyển bằng CharacterController và kẹp vị trí êm ái theo chiều dài dây nếu bị căng dây
            Vector3 desiredDelta = pendulumVelocity * dt;
            Vector3 predictedPos = moveTarget.position + desiredDelta;
            Vector3 fromAnchorToPredicted = predictedPos - grapplePoint;

            if (!isPullingToStand && fromAnchorToPredicted.magnitude > activeRopeLength)
            {
                Vector3 clampedPos = grapplePoint + fromAnchorToPredicted.normalized * activeRopeLength;
                desiredDelta = clampedPos - moveTarget.position;
                // Đồng bộ lại vận tốc thực tế theo độ dịch chuyển đã kẹp để không bị giật ở frame sau
                pendulumVelocity = dt > 0.0001f ? desiredDelta / dt : Vector3.zero;
            }

            controller.Move(desiredDelta);
        }
        else
        {
            // Đã tới cột móc khi kéo lên: đưa người chơi đứng vững trên điểm móc mượt mà
            Vector3 desiredStandPos = grapplePoint + ropeDir * overshootDistance + Vector3.up * standHeightOffset;
            Vector3 delta = desiredStandPos - moveTarget.position;
            if (delta.magnitude > 0.01f)
            {
                controller.Move(delta * Mathf.Clamp01(10f * dt));
            }
        }
    }

    private void ReleaseGrapple(bool playSound)
    {
        if (!isGrappling) return;

        isGrappling = false;
        isHookAttached = false;
        isHoldingInput = false;
        isPullingToStand = false;
        hasReachedAnchor = false;
        pendulumVelocity = Vector3.zero;
        lastGrappleTime = Time.time;

        if (lineRenderer != null && lineRenderer.enabled)
        {
            isRetractingLine = true;
        }

        if (playSound && audioSource != null && detachHookClip != null)
        {
            audioSource.PlayOneShot(detachHookClip, audioVolume);
        }

        if (movementScript != null)
        {
            movementScript.enabled = true;
        }
    }

    private void UpdateLineVisual(float dt)
    {
        if (lineRenderer == null || !lineRenderer.enabled) return;

        Vector3 startPos = hookOrigin != null ? hookOrigin.position : transform.position;
        currentHookTipPos = Vector3.MoveTowards(currentHookTipPos, grapplePoint, hookShootSpeed * dt);

        if (!isHookAttached && Vector3.Distance(currentHookTipPos, grapplePoint) <= 0.05f)
        {
            currentHookTipPos = grapplePoint;
            isHookAttached = true;

            if (movementScript != null && movementScript.enabled)
            {
                movementScript.enabled = false;
            }
        }

        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, currentHookTipPos);
    }

    private void UpdateLineRetract(float dt)
    {
        if (lineRenderer == null)
        {
            isRetractingLine = false;
            return;
        }

        Vector3 startPos = hookOrigin != null ? hookOrigin.position : transform.position;
        currentHookTipPos = Vector3.MoveTowards(currentHookTipPos, startPos, hookRetractSpeed * dt);

        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, currentHookTipPos);

        if (Vector3.Distance(currentHookTipPos, startPos) <= 0.08f)
        {
            isRetractingLine = false;
            lineRenderer.enabled = false;
        }
    }
}
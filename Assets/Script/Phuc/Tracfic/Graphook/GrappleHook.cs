using UnityEngine;
using UnityEngine.InputSystem;

public class GrappleHook : MonoBehaviour
{
    [Header("0. Liên Kết WeaponUI & JumpPack")]
    [Tooltip("Tên hiển thị của súng bắn móc trên WeaponUI (nếu để trống sẽ lấy tên GameObject).")]
    [SerializeField] private string weaponDisplayName = "Grapple Hook";

    [Tooltip("Script Jump Pack của người chơi (sẽ tự tìm nếu để trống).")]
    [SerializeField] private SpaceMarineJumpPack jumpPackScript;

    [Header("1. Phím Điều Khiển (New Input System)")]
    [Tooltip("Dùng để làm gì: Gán InputAction nhấn giữ để bắn và giữ dây móc đung đưa bên dưới mốc, thả ra để buông dây. Để trống sẽ tự dùng phím F.")]
    [InspectorName("Phím Bắn Và Giữ Dây Móc (Mặc định: F)")]
    [SerializeField] private InputActionReference grappleAction;

    [Tooltip("Dùng để làm gì: Gán InputAction Vector2 (WASD) để người chơi điều khiển hướng đung đưa lắc lư tốc độ cao khi đang treo dây (hỗ trợ cả tổ hợp 2 phím như W+D). Để trống sẽ tự dùng WASD.")]
    [InspectorName("Phím Điều Khiển Đung Đưa Khi Đu (Mặc định: WASD)")]
    [SerializeField] private InputActionReference moveAction;

    [Tooltip("Dùng để làm gì: Gán InputAction nhấn/giữ (kết hợp cùng giữ F) để nhảy hẳn lên cột mốc. Để trống sẽ tự dùng phím E.")]
    [InspectorName("Phím Nhảy Hẳn Lên Cột Khi Giữ F + Nhấn E")]
    [SerializeField] private InputActionReference pullToStandAction;

    [Header("2. Lọc Mục Tiêu & Độ Chính Xác Khi Bắn Móc")]
    [Tooltip("Dùng để làm gì: Chọn các Layer của vật thể/mặt phẳng mà dây móc có thể bắn trúng và bám vào.")]
    [InspectorName("Layer Được Phép Móc Dây Vào")]
    [SerializeField] private LayerMask grappleableLayers = ~0;

    [Tooltip("Dùng để làm gì: Chỉ cho phép móc vào vật thể có Tag này (ví dụ: GrapplePoint). Để trống nếu không muốn lọc theo Tag.")]
    [InspectorName("Tag Bắt Buộc Của Điểm Móc (Để Trống = Không Lọc)")]
    [SerializeField] private string requiredTag = "";

    [Tooltip("Dùng để làm gì: Giới hạn khoảng cách xa nhất (tính bằng mét) mà súng có thể bắn dây móc tới.")]
    [InspectorName("Tầm Bắn Dây Móc Tối Đa (Mét)")]
    [SerializeField] private float maxGrappleDistance = 55f;

    [Tooltip("Dùng để làm gì: Bán kính vùng bắt mục tiêu quanh tâm ngắm (mét) giúp bắn mốc dễ trúng và chính xác hơn khi đang đu tốc độ cao.")]
    [InspectorName("Bán Kính Hỗ Trợ Bắt Móc Quanh Tâm (Mét)")]
    [SerializeField] private float aimAssistRadius = 1.35f;

    [Header("3. Vật Lý Treo Dây & Đung Đưa Tốc Độ Cao")]
    [Tooltip("Dùng để làm gì: Quy định độ dài sợi dây (mét) khi người chơi đang treo lơ lửng đung đưa dưới điểm móc.")]
    [InspectorName("Độ Dài Dây Khi Treo Lơ Lửng (Mét)")]
    [SerializeField] private float suspendRopeLength = 6.5f;

    [Tooltip("Dùng để làm gì: Khoảng cách tối thiểu (mét) giữ người chơi nằm bên dưới độ cao điểm móc để không bị bay vút lên trời khi giữ F.")]
    [InspectorName("Độ Sâu Treo Dưới Điểm Móc (Mét)")]
    [SerializeField] private float minHangBelowAnchor = 1.6f;

    [Tooltip("Dùng để làm gì: Giới hạn vận tốc hướng lên tối đa khi đung đưa thường (giữ F) để không bị nhảy nhảy lên.")]
    [InspectorName("Tốc Độ Hướng Lên Tối Đa Khi Chỉ Giữ F")]
    [SerializeField] private float maxUpwardSwingSpeed = 2.5f;

    [Tooltip("Dùng để làm gì: Tạo trọng lực kéo người chơi rơi xuống để sợi dây căng ra và tạo tư thế treo lơ lửng bên dưới mốc.")]
    [InspectorName("Trọng Lực Kéo Xuống Khi Đu Dây")]
    [SerializeField] private float pendulumGravity = 20f;

    [Tooltip("Dùng để làm gì: Quy định độ siết dây giữ người chơi đi theo quỹ đạo vòng cung của sợi dây.")]
    [InspectorName("Độ Siết Giữ Độ Dài Sợi Dây")]
    [SerializeField] private float ropeTensionStiffness = 32f;

    [Tooltip("Dùng để làm gì: Bật hoặc tắt lực đung đưa qua lại khi đang treo trên dây móc.")]
    [InspectorName("Bật Vật Lý Đung Đưa Qua Lại")]
    [SerializeField] private bool enableSwingPhysics = true;

    [Tooltip("Dùng để làm gì: Chỉnh độ mạnh của lực đẩy đung đưa qua lại tự động khi người chơi không bấm phím hướng.")]
    [InspectorName("Độ Mạnh Lực Đung Đưa Tự Động")]
    [SerializeField] private float swingForce = 18f;

    [Tooltip("Dùng để làm gì: Chỉnh tốc độ nhịp đung đưa qua lại nhanh hay chậm theo thời gian.")]
    [InspectorName("Tốc Độ Nhịp Đung Đưa (Tần Số)")]
    [SerializeField] private float swingFrequency = 2.5f;

    [Tooltip("Dùng để làm gì: Làm dịu dao động để người chơi đu qua lại tốc độ cao mượt mà, không bị giật nảy.")]
    [InspectorName("Độ Hãm Dao Động Đung Đưa (Giảm Chấn)")]
    [SerializeField] private float swingDamping = 1.4f;

    [Tooltip("Dùng để làm gì: Chỉnh lực người chơi điều khiển đung đưa theo hướng phím bấm (A trái, D phải, W+D chéo...) tốc độ cao khi đang bám dây.")]
    [InspectorName("Lực Điều Khiển Đu Theo Hướng (WASD)")]
    [SerializeField] private float playerSwingControl = 26f;

    [Tooltip("Dùng để làm gì: Tốc độ lướt tối đa theo hướng phím bấm (kể cả tổ hợp 2 phím như W+D) khi đang đung đưa trên dây.")]
    [InspectorName("Tốc Độ Đu Lướt Tối Đa Theo Hướng (m/s)")]
    [SerializeField] private float maxDirectionalSwingSpeed = 16f;

    [Tooltip("Dùng để làm gì: Góc lệch tối đa (độ, tính từ phương thẳng đứng xuống dưới) trước khi dây con lắc bị hãm và đảo chiều đu ngược lại.")]
    [InspectorName("Góc Đu Tối Đa Trước Khi Đảo Chiều (Độ)")]
    [SerializeField] private float maxSwingAngle = 62f;

    [Tooltip("Dùng để làm gì: Hệ số giữ đà văng ngược lại khi người chơi đu chạm tới biên góc tối đa của sợi dây.")]
    [InspectorName("Độ Nảy Đảo Chiều Khi Chạm Biên Góc")]
    [SerializeField] private float reverseSwingMomentum = 0.78f;

    [Header("4. Nhảy Hẳn Lên Cột Khi Giữ F Và Nhấn E")]
    [Tooltip("Dùng để làm gì: Khoảng cách tới cột mốc mà khi giữ F và nhấn E người chơi sẽ bắt đầu nhảy vọt lên cột từ sớm.")]
    [InspectorName("Khoảng Cách Gần Mốc Để Nhảy Lên Sớm (Mét)")]
    [SerializeField] private float earlyVaultDistance = 7.5f;

    [Tooltip("Dùng để làm gì: Chỉnh lực bật trục Y nhảy vọt lên cao khi giữ F và nhấn E để nhảy hẳn lên cột.")]
    [InspectorName("Lực Bật Trục Y Nhảy Hẳn Lên Cột")]
    [SerializeField] private float vaultJumpUpSpeed = 15f;

    [Tooltip("Dùng để làm gì: Chỉnh tốc độ kéo trục X, Z tiến lên cột mốc khi nhảy hẳn lên cột.")]
    [InspectorName("Tốc Độ Kéo Ngang Tiến Lên Cột")]
    [SerializeField] private float vaultHorizontalPullSpeed = 22f;

    [Tooltip("Dùng để làm gì: Chỉnh tốc độ thu dây kéo người chơi lên đứng trên cột mốc khi giữ F và nhấn E.")]
    [InspectorName("Tốc Độ Thu Dây Kéo Lên Cột")]
    [SerializeField] private float pullSpeed = 24f;

    [Tooltip("Dùng để làm gì: Chỉnh gia tốc tăng tốc khi kéo lên cột.")]
    [InspectorName("Gia Tốc Tăng Tốc Khi Kéo Lên Cột")]
    [SerializeField] private float pullAcceleration = 55f;

    [Tooltip("Dùng để làm gì: Tăng thêm lực nâng thẳng đứng khi bị vướng thành cột lúc đang nhảy/kéo lên cột.")]
    [InspectorName("Lực Hỗ Trợ Trượt Lên Khi Vướng Cột")]
    [SerializeField] private float wallClimbBoostSpeed = 12f;

    [Tooltip("Dùng để làm gì: Chỉnh khoảng cách nhích thêm vượt qua tâm điểm móc để người chơi đứng vững hẳn trên cột.")]
    [InspectorName("Khoảng Cách Nhích Qua Tâm Móc Để Đứng")]
    [SerializeField] private float overshootDistance = 1.0f;

    [Tooltip("Dùng để làm gì: Nâng độ cao điểm đứng thêm một đoạn (mét) để chân người chơi đứng vững trên mặt cột.")]
    [InspectorName("Độ Cao Đệm Chân Đứng Trên Cột (Mét)")]
    [SerializeField] private float standHeightOffset = 0.3f;

    [Tooltip("Dùng để làm gì: Khoảng cách tới điểm móc mà khi nhỏ hơn mức này sẽ coi như đã lên tới đỉnh cột.")]
    [InspectorName("Khoảng Cách Coi Như Đã Lên Tới Cột (Mét)")]
    [SerializeField] private float arriveThreshold = 1.25f;

    [Tooltip("Dùng để làm gì: Thời gian chờ (giây) giữa hai lần bắn dây móc liên tiếp.")]
    [InspectorName("Thời Gian Hồi Chiêu Giữa 2 Lần Bắn (Giây)")]
    [SerializeField] private float cooldown = 0.25f;

    [Header("5. Xoay Súng & Hiển Thị Sợi Dây Móc")]
    [Tooltip("Dùng để làm gì: Chỉnh tốc độ nòng súng xoay hướng về điểm móc đang bám.")]
    [InspectorName("Tốc Độ Súng Xoay Hướng Về Điểm Móc")]
    [SerializeField] private float gunAimSpeed = 22f;

    [Tooltip("Dùng để làm gì: Chỉnh tốc độ đầu dây móc phóng ra tới điểm móc.")]
    [InspectorName("Tốc Độ Đầu Dây Phóng Tới Điểm Móc")]
    [SerializeField] private float hookShootSpeed = 130f;

    [Tooltip("Dùng để làm gì: Chỉnh tốc độ thu dây móc trở về nòng súng khi người chơi buông phím.")]
    [InspectorName("Tốc Độ Thu Dây Về Súng Khi Buông Tay")]
    [SerializeField] private float hookRetractSpeed = 130f;

    [Tooltip("Dùng để làm gì: Chỉnh độ to/dày của sợi dây khi script tự tạo LineRenderer.")]
    [InspectorName("Độ Dày Sợi Dây Móc (LineRenderer)")]
    [SerializeField] private float lineWidth = 0.04f;

    [Tooltip("Dùng để làm gì: Chọn màu sắc của sợi dây móc khi script tự tạo LineRenderer.")]
    [InspectorName("Màu Sắc Sợi Dây Móc (LineRenderer)")]
    [SerializeField] private Color lineColor = Color.cyan;

    [Header("6. Âm Thanh Dây Móc")]
    [Tooltip("Dùng để làm gì: Gán file âm thanh phát ra ngay lúc bắn dây móc.")]
    [InspectorName("File Âm Thanh Lúc Bắn Dây Móc")]
    [SerializeField] private AudioClip shootHookClip;

    [Tooltip("Dùng để làm gì: Gán file âm thanh phát ra khi buông hoặc tháo dây móc.")]
    [InspectorName("File Âm Thanh Lúc Buông Dây Móc")]
    [SerializeField] private AudioClip detachHookClip;

    [Range(0f, 1f)]
    [Tooltip("Dùng để làm gì: Chỉnh âm lượng to nhỏ (từ 0 đến 1) cho âm thanh bắn và buông dây móc.")]
    [InspectorName("Âm Lượng Hiệu Ứng Dây Móc (0 - 1)")]
    [SerializeField] private float audioVolume = 0.9f;

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
    private bool isVaultJumpingUp = false;
    private bool hasReachedAnchor = false;

    private Vector3 grapplePoint;
    private Vector3 currentHookTipPos;
    private Vector3 smoothedLineStartPos;
    private bool hasSmoothedLineStart = false;
    private Vector3 pendulumVelocity = Vector3.zero;
    private float swingPhase = 0f;
    private float currentPullSpeed = 0f;
    private float activeRopeLength = 6.5f;
    private float lastGrappleTime = -999f;

    // Thuộc tính phục vụ WeaponUI và các script khác
    public string WeaponDisplayName => string.IsNullOrWhiteSpace(weaponDisplayName) ? gameObject.name : weaponDisplayName;
    public bool IsGrappling => isGrappling;
    public bool IsHookAttached => isHookAttached;
    public bool IsSwingingOrPulling => isGrappling && isHookAttached;
    public float RemainingCooldown => Mathf.Max(0f, (lastGrappleTime + cooldown) - Time.time);

    private bool IsEquippedByPlayer()
    {
        if (!gameObject.activeInHierarchy || !enabled) return false;
        if (transform.parent == null) return false;
        return GetComponentInParent<CharacterController>() != null || GetComponentInParent<WeaponSwitcher>() != null;
    }

    public string CurrentStateLabel
    {
        get
        {
            if (isPullingToStand) return "PULLING (F+E)";
            if (isGrappling && isHookAttached) return "SWINGING (HOLD F)";
            if (isGrappling) return "SHOOTING";
            if (RemainingCooldown > 0.01f) return $"CD {RemainingCooldown:0.1}s";
            return "READY [F]";
        }
    }

    public string RangeInfoLabel => $"{maxGrappleDistance:0}m MAX";

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

        if (jumpPackScript == null)
        {
            jumpPackScript = GetComponentInParent<SpaceMarineJumpPack>();
        }
        if (jumpPackScript == null && playerRoot != null)
        {
            jumpPackScript = playerRoot.GetComponent<SpaceMarineJumpPack>();
        }
        if (jumpPackScript == null)
        {
            jumpPackScript = FindFirstObjectByType<SpaceMarineJumpPack>();
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
        if (controller == null || playerCamera == null || movementScript == null || jumpPackScript == null || lineRenderer == null || audioSource == null)
        {
            AutoFindAndBindComponents();
        }
        hasSmoothedLineStart = false;
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
        hasSmoothedLineStart = false;
        transform.localRotation = initialLocalRotation;

        if (jumpPackScript != null && !jumpPackScript.enabled)
        {
            jumpPackScript.enabled = true;
        }

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

    private bool WasPullToStandPressedThisFrame()
    {
        if (ActivePullToStandAction != null && ActivePullToStandAction.WasPressedThisFrame())
        {
            return true;
        }
        return pullToStandAction == null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    private bool IsPullToStandHeld()
    {
        if (ActivePullToStandAction != null && ActivePullToStandAction.IsPressed())
        {
            return true;
        }
        return pullToStandAction == null && Keyboard.current != null && Keyboard.current.eKey.isPressed;
    }

    private Vector2 ReadDirectionalSwingInput()
    {
        Vector2 input = ActiveMoveAction != null ? ActiveMoveAction.ReadValue<Vector2>() : Vector2.zero;

        if (Keyboard.current != null)
        {
            float x = 0f;
            float y = 0f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.sKey.isPressed) y -= 1f;
            if (Keyboard.current.wKey.isPressed) y += 1f;

            Vector2 kbVec = new Vector2(x, y);
            if (kbVec.sqrMagnitude > 0.01f)
            {
                return Vector2.ClampMagnitude(kbVec, 1f);
            }
        }

        return Vector2.ClampMagnitude(input, 1f);
    }

    private void Update()
    {
        if (!IsEquippedByPlayer())
        {
            if (isGrappling)
            {
                ReleaseGrapple(playSound: false);
            }
            return;
        }

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

                if (isHookAttached)
                {
                    if (jumpPackScript != null && jumpPackScript.enabled)
                    {
                        jumpPackScript.enabled = false;
                    }
                    UpdatePendulumSwingAndStand(dt, heldNow);
                }
            }
        }
        else
        {
            if (jumpPackScript != null && !jumpPackScript.enabled)
            {
                jumpPackScript.enabled = true;
            }

            transform.localRotation = Quaternion.Slerp(transform.localRotation, initialLocalRotation, gunAimSpeed * dt);

            if (movementScript != null && !movementScript.enabled)
            {
                movementScript.enabled = true;
            }
        }
    }

    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        if (isGrappling)
        {
            UpdateLineVisual(dt);
        }
        else if (isRetractingLine)
        {
            UpdateLineRetract(dt);
        }
    }

    private bool FindBestGrappleHit(out RaycastHit bestHit)
    {
        bestHit = default;
        Vector3 screenCenter = new Vector3(0.5f, 0.5f, 0f);
        Ray centerRay = playerCamera.ViewportPointToRay(screenCenter);

        if (Physics.Raycast(centerRay, out RaycastHit directHit, maxGrappleDistance, grappleableLayers, QueryTriggerInteraction.Ignore))
        {
            if (IsValidTarget(directHit.collider.gameObject))
            {
                bestHit = directHit;
                return true;
            }
        }

        float radius = Mathf.Max(0.05f, aimAssistRadius);
        RaycastHit[] sphereHits = Physics.SphereCastAll(centerRay, radius, maxGrappleDistance, grappleableLayers, QueryTriggerInteraction.Ignore);
        if (sphereHits != null && sphereHits.Length > 0)
        {
            float bestScore = float.MaxValue;
            bool found = false;

            for (int i = 0; i < sphereHits.Length; i++)
            {
                RaycastHit candidate = sphereHits[i];
                if (candidate.collider == null || !IsValidTarget(candidate.collider.gameObject))
                {
                    continue;
                }

                Vector3 toPoint = candidate.point - centerRay.origin;
                float projDist = Vector3.Dot(toPoint, centerRay.direction);
                if (projDist <= 0.1f) continue;

                Vector3 closestOnRay = centerRay.origin + centerRay.direction * projDist;
                float offCenterDist = Vector3.Distance(candidate.point, closestOnRay);

                float score = offCenterDist * 10f + candidate.distance * 0.05f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestHit = candidate;
                    found = true;
                }
            }

            if (found)
            {
                return true;
            }
        }

        return false;
    }

    private void TryStartGrapple()
    {
        if (!IsEquippedByPlayer()) return;

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

        if (FindBestGrappleHit(out RaycastHit hit))
        {
            grapplePoint = hit.point;
            Vector3 originPos = hookOrigin != null ? hookOrigin.position : transform.position;
            currentHookTipPos = originPos;
            smoothedLineStartPos = originPos;
            hasSmoothedLineStart = true;

            isGrappling = true;
            isHookAttached = false;
            isRetractingLine = false;
            isPullingToStand = false;
            isVaultJumpingUp = false;
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

    /// <summary>
    /// Đu dây có giới hạn góc tối đa và đảo chiều ngược lại khi chạm biên con lắc; chỉ nhảy hẳn lên cột khi giữ F và nhấn E
    /// </summary>
    private void UpdatePendulumSwingAndStand(float dt, bool isHoldingF)
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

        bool pressedPullThisFrame = WasPullToStandPressedThisFrame();
        bool heldPull = IsPullToStandHeld();

        if (isHoldingF && (pressedPullThisFrame || (heldPull && isPullingToStand)))
        {
            isPullingToStand = true;

            if (!isVaultJumpingUp && distToAnchor <= earlyVaultDistance)
            {
                isVaultJumpingUp = true;
                float heightNeeded = Mathf.Max(0f, (grapplePoint.y + standHeightOffset) - moveTarget.position.y);
                float requiredJumpSpeed = Mathf.Sqrt(Mathf.Max(2f, heightNeeded * 2f * Mathf.Max(5f, pendulumGravity) + 14f));
                pendulumVelocity.y = Mathf.Max(pendulumVelocity.y, Mathf.Max(vaultJumpUpSpeed, requiredJumpSpeed));

                float dx = grapplePoint.x - moveTarget.position.x;
                float dz = grapplePoint.z - moveTarget.position.z;
                Vector2 flatDir = new Vector2(dx, dz);
                if (flatDir.sqrMagnitude > 0.001f)
                {
                    flatDir.Normalize();
                    float pullHorizSpeed = Mathf.Max(vaultHorizontalPullSpeed, pullSpeed * 0.9f);
                    pendulumVelocity.x = flatDir.x * pullHorizSpeed;
                    pendulumVelocity.z = flatDir.y * pullHorizSpeed;
                }
            }
            else if (pressedPullThisFrame && !isVaultJumpingUp)
            {
                isVaultJumpingUp = true;
                pendulumVelocity.y = Mathf.Max(pendulumVelocity.y, vaultJumpUpSpeed * 0.8f);
            }
        }
        else if (!heldPull && !isVaultJumpingUp)
        {
            isPullingToStand = false;
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
                currentPullSpeed = Mathf.MoveTowards(currentPullSpeed, pullSpeed, pullAcceleration * dt);
                activeRopeLength = Mathf.MoveTowards(activeRopeLength, 0.25f, currentPullSpeed * dt);

                if (isVaultJumpingUp)
                {
                    float dx = grapplePoint.x - moveTarget.position.x;
                    float dz = grapplePoint.z - moveTarget.position.z;
                    float targetVelX = Mathf.Clamp(dx * 6.5f, -vaultHorizontalPullSpeed, vaultHorizontalPullSpeed);
                    float targetVelZ = Mathf.Clamp(dz * 6.5f, -vaultHorizontalPullSpeed, vaultHorizontalPullSpeed);

                    pendulumVelocity.x = Mathf.Lerp(pendulumVelocity.x, targetVelX, 12f * dt);
                    pendulumVelocity.z = Mathf.Lerp(pendulumVelocity.z, targetVelZ, 12f * dt);

                    pendulumVelocity.y -= pendulumGravity * 0.3f * dt;

                    float pullStep = currentPullSpeed * 1.5f * dt;
                    pendulumVelocity.x += ropeDir.x * pullStep;
                    pendulumVelocity.z += ropeDir.z * pullStep;
                }
                else
                {
                    pendulumVelocity += ropeDir * (currentPullSpeed * 2.4f * dt);
                }

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
                activeRopeLength = Mathf.MoveTowards(activeRopeLength, targetRopeLength, pullSpeed * 0.75f * dt);

                pendulumVelocity.y -= pendulumGravity * dt;

                Vector3 fromAnchor = moveTarget.position - grapplePoint;
                Vector3 downAxis = Vector3.down;
                float currentAngle = fromAnchor.sqrMagnitude > 0.0001f
                    ? Vector3.Angle(fromAnchor.normalized, downAxis)
                    : 0f;

                float clampedMaxAngle = Mathf.Clamp(maxSwingAngle, 10f, 88f);
                bool reachedSwingLimit = currentAngle >= clampedMaxAngle;

                Vector3 outwardFlat = new Vector3(fromAnchor.x, 0f, fromAnchor.z);
                if (outwardFlat.sqrMagnitude > 0.0001f)
                {
                    outwardFlat.Normalize();
                }

                Vector2 moveInput = ReadDirectionalSwingInput();
                bool hasDirectionalInput = moveInput.sqrMagnitude > 0.01f;

                Vector3 camForward = playerCamera != null ? playerCamera.transform.forward : moveTarget.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude < 0.0001f) camForward = Vector3.forward;
                camForward.Normalize();
                Vector3 camRight = new Vector3(camForward.z, 0f, -camForward.x).normalized;

                Vector3 desiredSwingDir = (camRight * moveInput.x + camForward * moveInput.y);
                if (desiredSwingDir.sqrMagnitude > 0.0001f)
                {
                    desiredSwingDir.Normalize();
                }

                if (enableSwingPhysics)
                {
                    if (hasDirectionalInput)
                    {
                        float pushOutwardDot = outwardFlat.sqrMagnitude > 0.0001f
                            ? Vector3.Dot(desiredSwingDir, outwardFlat)
                            : 0f;

                        if (reachedSwingLimit && pushOutwardDot > 0.1f)
                        {
                            Vector3 currentHoriz = new Vector3(pendulumVelocity.x, 0f, pendulumVelocity.z);
                            Vector3 reverseDir = -outwardFlat;
                            float reverseSpeed = Mathf.Max(currentHoriz.magnitude * reverseSwingMomentum, maxDirectionalSwingSpeed * 0.45f);
                            Vector3 reversedHoriz = reverseDir * reverseSpeed;

                            pendulumVelocity.x = Mathf.Lerp(pendulumVelocity.x, reversedHoriz.x, 14f * dt);
                            pendulumVelocity.z = Mathf.Lerp(pendulumVelocity.z, reversedHoriz.z, 14f * dt);
                            pendulumVelocity.y = Mathf.Min(pendulumVelocity.y, -2.5f);
                        }
                        else
                        {
                            float angleFactor = 1f - Mathf.Clamp01((currentAngle - clampedMaxAngle * 0.75f) / Mathf.Max(5f, clampedMaxAngle * 0.25f));
                            float effectiveMaxSpeed = maxDirectionalSwingSpeed * Mathf.Lerp(0.45f, 1f, angleFactor);

                            Vector3 targetHorizVel = desiredSwingDir * effectiveMaxSpeed;
                            Vector3 currentHorizVel = new Vector3(pendulumVelocity.x, 0f, pendulumVelocity.z);
                            Vector3 steeredHorizVel = Vector3.MoveTowards(
                                currentHorizVel,
                                targetHorizVel,
                                (playerSwingControl + ropeTensionStiffness) * dt
                            );

                            pendulumVelocity.x = steeredHorizVel.x;
                            pendulumVelocity.z = steeredHorizVel.z;
                        }
                    }
                    else
                    {
                        Vector3 swingAxis = Vector3.Cross(ropeDir, Vector3.up);
                        if (swingAxis.sqrMagnitude < 0.0001f)
                        {
                            swingAxis = moveTarget.right;
                        }
                        swingAxis.y = 0f;
                        swingAxis.Normalize();

                        swingPhase += dt * swingFrequency;
                        float autoSwing = Mathf.Sin(swingPhase) * swingForce;

                        if (reachedSwingLimit)
                        {
                            swingPhase += Mathf.PI;
                            autoSwing = -Mathf.Abs(swingForce) * 0.85f;
                        }

                        pendulumVelocity.x += swingAxis.x * autoSwing * dt;
                        pendulumVelocity.z += swingAxis.z * autoSwing * dt;
                        pendulumVelocity = Vector3.Lerp(pendulumVelocity, Vector3.zero, Mathf.Clamp01(swingDamping * dt));
                    }
                }

                float allowedUpSpeed = Mathf.Max(0f, maxUpwardSwingSpeed);
                if (pendulumVelocity.y > allowedUpSpeed)
                {
                    pendulumVelocity.y = allowedUpSpeed;
                }

                float maxAllowedY = grapplePoint.y - Mathf.Max(0.3f, minHangBelowAnchor);
                if (moveTarget.position.y > maxAllowedY)
                {
                    float excessHeight = moveTarget.position.y - maxAllowedY;
                    pendulumVelocity.y = Mathf.Min(pendulumVelocity.y, -excessHeight * 10f - 2.5f);
                }

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

            Vector3 desiredDelta = pendulumVelocity * dt;
            Vector3 predictedPos = moveTarget.position + desiredDelta;

            if (!isPullingToStand)
            {
                float ceilingHangY = grapplePoint.y - Mathf.Max(0.3f, minHangBelowAnchor);
                if (predictedPos.y > ceilingHangY)
                {
                    predictedPos.y = ceilingHangY;
                    desiredDelta = predictedPos - moveTarget.position;
                    if (pendulumVelocity.y > 0f)
                    {
                        pendulumVelocity.y = 0f;
                    }
                }

                Vector3 fromAnchorToPredicted = predictedPos - grapplePoint;
                if (fromAnchorToPredicted.magnitude > activeRopeLength)
                {
                    Vector3 clampedPos = grapplePoint + fromAnchorToPredicted.normalized * activeRopeLength;
                    desiredDelta = clampedPos - moveTarget.position;
                    float radialVel = Vector3.Dot(pendulumVelocity, fromAnchorToPredicted.normalized);
                    if (radialVel > 0f)
                    {
                        pendulumVelocity -= fromAnchorToPredicted.normalized * radialVel;
                    }
                }

                Vector3 predictedFromAnchor = predictedPos - grapplePoint;
                float predictedAngle = predictedFromAnchor.sqrMagnitude > 0.0001f
                    ? Vector3.Angle(predictedFromAnchor.normalized, Vector3.down)
                    : 0f;

                float limitAngle = Mathf.Clamp(maxSwingAngle, 10f, 88f);
                if (predictedAngle > limitAngle)
                {
                    Vector3 clampedDir = Vector3.Slerp(
                        Vector3.down,
                        predictedFromAnchor.normalized,
                        limitAngle / Mathf.Max(0.001f, predictedAngle)
                    ).normalized;

                    float radiusLen = Mathf.Max(0.5f, predictedFromAnchor.magnitude);
                    Vector3 angleClampedPos = grapplePoint + clampedDir * radiusLen;
                    desiredDelta = angleClampedPos - moveTarget.position;

                    Vector3 backToCenterFlat = new Vector3(-clampedDir.x, 0f, -clampedDir.z);
                    if (backToCenterFlat.sqrMagnitude > 0.0001f)
                    {
                        backToCenterFlat.Normalize();
                        float currentHorizSpeed = new Vector3(pendulumVelocity.x, 0f, pendulumVelocity.z).magnitude;
                        float returnSpeed = currentHorizSpeed * reverseSwingMomentum;
                        pendulumVelocity.x = backToCenterFlat.x * returnSpeed;
                        pendulumVelocity.z = backToCenterFlat.z * returnSpeed;
                    }
                }
            }

            controller.Move(desiredDelta);
        }
        else
        {
            Vector3 horizontalDir = new Vector3(ropeDir.x, 0f, ropeDir.z).normalized;
            Vector3 desiredStandPos = new Vector3(grapplePoint.x, grapplePoint.y + standHeightOffset, grapplePoint.z)
                + horizontalDir * Mathf.Min(overshootDistance, 0.5f);
            Vector3 delta = desiredStandPos - moveTarget.position;
            if (delta.magnitude > 0.01f)
            {
                controller.Move(delta * Mathf.Clamp01(12f * dt));
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
        isVaultJumpingUp = false;
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

        if (jumpPackScript != null && !jumpPackScript.enabled)
        {
            jumpPackScript.enabled = true;
        }

        if (movementScript != null)
        {
            movementScript.enabled = true;
        }
    }

    private void UpdateLineVisual(float dt)
    {
        if (lineRenderer == null || !lineRenderer.enabled) return;

        Vector3 rawStartPos = hookOrigin != null ? hookOrigin.position : transform.position;
        if (!hasSmoothedLineStart)
        {
            smoothedLineStartPos = rawStartPos;
            hasSmoothedLineStart = true;
        }
        else
        {
            smoothedLineStartPos = Vector3.Lerp(smoothedLineStartPos, rawStartPos, 1f - Mathf.Exp(-35f * dt));
        }

        if (!isHookAttached)
        {
            currentHookTipPos = Vector3.MoveTowards(currentHookTipPos, grapplePoint, hookShootSpeed * dt);
            if (Vector3.Distance(currentHookTipPos, grapplePoint) <= 0.08f)
            {
                currentHookTipPos = grapplePoint;
                isHookAttached = true;

                if (movementScript != null && movementScript.enabled)
                {
                    movementScript.enabled = false;
                }
            }
        }
        else
        {
            // Khi đã bám mốc: khóa cứng đầu dây tại grapplePoint để dây không bị giật khi đu qua lại
            currentHookTipPos = grapplePoint;
        }

        lineRenderer.SetPosition(0, smoothedLineStartPos);
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
            hasSmoothedLineStart = false;
            lineRenderer.enabled = false;
        }
    }
}
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class GrappleHook : MonoBehaviour
{
    [Header("New Input System")]
    [Tooltip("Phím bắn / thu dây móc (mặc định phím E nếu để trống).")]
    [SerializeField] private InputActionReference grappleAction;

    [Header("Target Filtering (Layer & Tag)")]
    [Tooltip("Các Layer cho phép bắn dây móc trúng.")]
    [SerializeField] private LayerMask grappleableLayers = ~0;
    [Tooltip("Tag hợp lệ để bắn móc (để trống nếu chỉ cần lọc theo Layer).")]
    [SerializeField] private string requiredTag = "";
    [Tooltip("Tầm bắn dây móc tối đa (mét).")]
    [SerializeField] private float maxGrappleDistance = 45f;

    [Header("Pull Physics & Feel")]
    [Tooltip("Tốc độ kéo người chơi tới điểm móc.")]
    [SerializeField] private float pullSpeed = 28f;
    [Tooltip("Gia tốc tăng tốc khi bắt đầu bị kéo.")]
    [SerializeField] private float pullAcceleration = 60f;
    [Tooltip("Khoảng cách dừng kéo khi tới sát điểm móc.")]
    [SerializeField] private float stopDistance = 2.0f;
    [Tooltip("Lực hất nhẹ lên trên khi thả/kết thúc móc để bay tiếp mượt hơn.")]
    [SerializeField] private float releaseUpwardBoost = 4.5f;
    [Tooltip("Thời gian hồi chiêu giữa hai lần bắn móc (giây).")]
    [SerializeField] private float cooldown = 0.6f;

    [Header("References & Visuals")]
    [Tooltip("Camera góc nhìn thứ nhất để bắn raycast từ tâm màn hình.")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("Điểm xuất phát của dây móc (ví dụ đầu tay / đầu nòng / điểm móc trên người).")]
    [SerializeField] private Transform hookOrigin;
    [Tooltip("LineRenderer vẽ sợi dây móc (nếu để trống sẽ tự tạo nếu có bật).")]
    [SerializeField] private LineRenderer lineRenderer;
    [Tooltip("Script di chuyển mặt đất sẽ tạm tắt khi đang bị dây móc kéo.")]
    [SerializeField] private IronStormMovement movementScript;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioClip shootHookClip;
    [SerializeField] private AudioClip detachHookClip;
    [Range(0f, 1f)][SerializeField] private float audioVolume = 0.9f;

    private CharacterController controller;
    private AudioSource audioSource;

    private InputAction runtimeGrappleAction;
    private bool usingFallbackAction = false;

    private bool isGrappling = false;
    private Vector3 grapplePoint;
    private float currentPullSpeed = 0f;
    private float lastGrappleTime = -999f;

    private InputAction ActiveGrappleAction =>
        grappleAction != null && grappleAction.action != null ? grappleAction.action : runtimeGrappleAction;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (hookOrigin == null)
        {
            hookOrigin = transform;
        }

        if (movementScript == null)
        {
            movementScript = GetComponent<IronStormMovement>();
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }

        if (grappleAction == null)
        {
            usingFallbackAction = true;
            runtimeGrappleAction = new InputAction("Grapple", InputActionType.Button, "<Keyboard>/e");
        }
    }

    private void OnEnable()
    {
        ActiveGrappleAction?.Enable();
    }

    private void OnDisable()
    {
        if (isGrappling)
        {
            StopGrapple(applyBoost: false);
        }

        ActiveGrappleAction?.Disable();
    }

    private void OnDestroy()
    {
        if (usingFallbackAction)
        {
            runtimeGrappleAction?.Dispose();
        }
    }

    private void Update()
    {
        ActiveGrappleAction?.Enable();

        bool pressed = (ActiveGrappleAction != null && ActiveGrappleAction.WasPressedThisFrame())
                       || (grappleAction == null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);

        if (pressed)
        {
            if (isGrappling)
            {
                // Nhấn lại phím móc khi đang kéo để thả dây chủ động
                StopGrapple(applyBoost: true);
            }
            else if (Time.time >= lastGrappleTime + cooldown)
            {
                TryStartGrapple();
            }
        }

        if (isGrappling)
        {
            UpdateGrapplePull();
            UpdateLineVisual();
        }
        else if (movementScript != null && !movementScript.enabled)
        {
            movementScript.enabled = true;
        }
    }

    private void TryStartGrapple()
    {
        if (playerCamera == null || controller == null || !controller.enabled) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, maxGrappleDistance, grappleableLayers, QueryTriggerInteraction.Ignore))
        {
            if (!IsValidTarget(hit.collider.gameObject)) return;

            grapplePoint = hit.point;
            isGrappling = true;
            currentPullSpeed = Mathf.Min(pullSpeed * 0.5f, pullSpeed);

            if (movementScript != null)
            {
                movementScript.enabled = false;
            }

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
        if (target.transform.root == transform.root) return false;

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

    private void UpdateGrapplePull()
    {
        if (controller == null || !controller.enabled)
        {
            StopGrapple(applyBoost: false);
            return;
        }

        Vector3 toPoint = grapplePoint - transform.position;
        float distance = toPoint.magnitude;

        if (distance <= stopDistance)
        {
            StopGrapple(applyBoost: true);
            return;
        }

        Vector3 dir = toPoint / distance;
        currentPullSpeed = Mathf.MoveTowards(currentPullSpeed, pullSpeed, pullAcceleration * Time.deltaTime);

        Vector3 step = dir * currentPullSpeed * Time.deltaTime;
        if (step.magnitude > distance - stopDistance)
        {
            step = dir * Mathf.Max(0f, distance - stopDistance);
        }

        CollisionFlags flags = controller.Move(step);

        // Nếu bị vướng trần/tường sát ngay trước mặt khi đang kéo thì dừng kéo để tránh kẹt
        if ((flags & CollisionFlags.CollidedSides) != 0 && distance < stopDistance * 1.5f)
        {
            StopGrapple(applyBoost: false);
        }
    }

    private void StopGrapple(bool applyBoost)
    {
        if (!isGrappling) return;

        isGrappling = false;
        lastGrappleTime = Time.time;

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }

        if (audioSource != null && detachHookClip != null)
        {
            audioSource.PlayOneShot(detachHookClip, audioVolume);
        }

        if (controller != null && controller.enabled && applyBoost && releaseUpwardBoost > 0f)
        {
            controller.Move(Vector3.up * (releaseUpwardBoost * Time.deltaTime));
        }

        if (movementScript != null)
        {
            movementScript.enabled = true;
        }
    }

    private void UpdateLineVisual()
    {
        if (lineRenderer == null || !lineRenderer.enabled) return;

        Vector3 startPos = hookOrigin != null ? hookOrigin.position : transform.position;
        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, grapplePoint);
    }
}
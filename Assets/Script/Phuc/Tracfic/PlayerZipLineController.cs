using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerZipLineController : MonoBehaviour
{
    [Header("New Input System")]
    [Tooltip("Phím tương tác bám/rời dây (để trống sẽ tự dùng phím E).")]
    [SerializeField] private InputActionReference interactAction;

    [Tooltip("Phím nhảy rời dây sớm khi đang đu (để trống sẽ tự dùng phím Space).")]
    [SerializeField] private InputActionReference jumpOffAction;

    [Header("Phạm Vi & Bám Dây")]
    [Tooltip("Khoảng cách tối đa để bấm E tương tác với sợi dây.")]
    [SerializeField] private float interactRange = 3.0f;

    [Tooltip("Độ lệch chiều cao từ vị trí người chơi tới điểm bám dây (ví dụ 1.6m ngang vai/tay).")]
    [SerializeField] private float gripHeightOffset = 1.6f;

    [Tooltip("Không cho phép bắt đầu đu dây nếu đã quá sát cuối dây (0.92 = bỏ qua 8% cuối).")]
    [Range(0f, 0.99f)]
    [SerializeField] private float maxStartProgress = 0.92f;

    [Header("Tốc Độ Trượt Từ Cao Xuống Thấp")]
    [SerializeField] private float baseZipSpeed = 12f;
    [SerializeField] private float maxZipSpeed = 26f;
    [SerializeField] private float slopeAcceleration = 8f;
    [SerializeField] private float positionFollowSmoothness = 24f;
    [SerializeField] private float jumpOffUpwardBoost = 5.5f;
    [SerializeField] private float exitForwardMomentumMultiplier = 0.75f;

    [Header("Tích Hợp Script Khác")]
    [SerializeField] private IronStormMovement movementScript;
    [SerializeField] private SpaceMarineJumpPack jumpPackScript;
    [SerializeField] private IronStormSlide slideScript;
    [SerializeField] private IronStormCamera cameraScript;

    [Header("Âm Thanh (Tùy Chọn)")]
    [SerializeField] private AudioSource zipAudioSource;
    [SerializeField] private AudioClip zipStartClip;
    [SerializeField] private AudioClip zipLoopClip;
    [Range(0f, 1f)]
    [SerializeField] private float audioVolume = 0.85f;

    private static readonly List<ZipLineTrack> allTracks = new List<ZipLineTrack>();

    private CharacterController controller;
    private InputAction runtimeInteractAction;
    private InputAction runtimeJumpOffAction;

    private ZipLineTrack activeTrack;
    private float currentProgress;
    private float currentZipSpeed;
    private bool isZipLining;

    private bool prevMovementEnabled;
    private bool prevJumpPackEnabled;

    public bool IsZipLining => isZipLining;

    private InputAction ActiveInteractAction =>
        interactAction != null && interactAction.action != null ? interactAction.action : runtimeInteractAction;

    private InputAction ActiveJumpOffAction =>
        jumpOffAction != null && jumpOffAction.action != null ? jumpOffAction.action : runtimeJumpOffAction;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (movementScript == null) movementScript = GetComponent<IronStormMovement>();
        if (jumpPackScript == null) jumpPackScript = GetComponent<SpaceMarineJumpPack>();
        if (slideScript == null) slideScript = GetComponent<IronStormSlide>();
        if (cameraScript == null) cameraScript = GetComponentInChildren<IronStormCamera>();
        if (zipAudioSource == null) zipAudioSource = GetComponent<AudioSource>();

        if (interactAction == null)
        {
            runtimeInteractAction = new InputAction("ZipLineInteract", InputActionType.Button, "<Keyboard>/e");
        }

        if (jumpOffAction == null)
        {
            runtimeJumpOffAction = new InputAction("ZipLineJumpOff", InputActionType.Button, "<Keyboard>/space");
        }
    }

    private void OnEnable()
    {
        if (cameraScript == null)
        {
            cameraScript = GetComponentInChildren<IronStormCamera>();
        }

        ActiveInteractAction?.Enable();
        ActiveJumpOffAction?.Enable();
        RefreshTrackCache();
    }

    private void OnDisable()
    {
        if (isZipLining)
        {
            ExitZipLine(applyJumpBoost: false);
        }
        else if (cameraScript != null)
        {
            cameraScript.SetZipLineActive(false);
        }

        ActiveInteractAction?.Disable();
        ActiveJumpOffAction?.Disable();
    }

    private void OnDestroy()
    {
        if (interactAction == null && runtimeInteractAction != null) runtimeInteractAction.Dispose();
        if (jumpOffAction == null && runtimeJumpOffAction != null) runtimeJumpOffAction.Dispose();
    }

    private static void RefreshTrackCache()
    {
        allTracks.RemoveAll(t => t == null);
        if (allTracks.Count == 0)
        {
            ZipLineTrack[] found = FindObjectsByType<ZipLineTrack>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && !allTracks.Contains(found[i]))
                {
                    allTracks.Add(found[i]);
                }
            }
        }
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;

        bool pressedE = ActiveInteractAction != null && ActiveInteractAction.WasPressedThisFrame();
        bool pressedJumpOff = ActiveJumpOffAction != null && ActiveJumpOffAction.WasPressedThisFrame();

        if (!isZipLining)
        {
            if (pressedE)
            {
                TryStartNearestZipLine();
            }
            return;
        }

        if (pressedE)
        {
            ExitZipLine(applyJumpBoost: false);
            return;
        }

        if (pressedJumpOff)
        {
            ExitZipLine(applyJumpBoost: true);
            return;
        }

        StepZipLine(dt);
    }

    private void TryStartNearestZipLine()
    {
        if (allTracks.Count == 0)
        {
            RefreshTrackCache();
        }

        Vector3 gripReferencePos = transform.position + Vector3.up * gripHeightOffset;
        float maxRangeSq = interactRange * interactRange;

        ZipLineTrack bestTrack = null;
        float bestProgress = 0f;
        float bestDistSq = maxRangeSq;

        for (int i = 0; i < allTracks.Count; i++)
        {
            ZipLineTrack track = allTracks[i];
            if (track == null || !track.isActiveAndEnabled || track.Length <= 0.1f) continue;

            float t;
            Vector3 closestOnLine = track.ClosestPointOnLine(gripReferencePos, out t);
            if (t >= maxStartProgress) continue;

            float distSq = (closestOnLine - gripReferencePos).sqrMagnitude;
            if (distSq <= bestDistSq)
            {
                bestDistSq = distSq;
                bestTrack = track;
                bestProgress = t;
            }
        }

        if (bestTrack != null)
        {
            EnterZipLine(bestTrack, bestProgress);
        }
    }

    private void EnterZipLine(ZipLineTrack track, float startT)
    {
        activeTrack = track;
        currentProgress = Mathf.Clamp(startT, 0f, maxStartProgress);

        // Chênh lệch độ cao từ vị trí bám hiện tại tới điểm thấp nhất tạo đà ban đầu
        Vector3 currentWirePos = activeTrack.GetPointAtProgress(currentProgress);
        float remainingDrop = Mathf.Max(0f, currentWirePos.y - activeTrack.EndPoint.position.y);
        currentZipSpeed = Mathf.Clamp(baseZipSpeed + Mathf.Sqrt(remainingDrop * 2f * Mathf.Max(0f, slopeAcceleration)), baseZipSpeed, maxZipSpeed);

        isZipLining = true;

        if (slideScript != null && slideScript.IsSliding)
        {
            slideScript.CancelSlide();
        }

        if (movementScript != null)
        {
            prevMovementEnabled = movementScript.enabled;
            movementScript.enabled = false;
        }

        if (jumpPackScript != null)
        {
            prevJumpPackEnabled = jumpPackScript.enabled;
            jumpPackScript.enabled = false;
        }

        if (cameraScript == null)
        {
            cameraScript = GetComponentInChildren<IronStormCamera>();
        }
        if (cameraScript != null)
        {
            cameraScript.SetZipLineActive(true);
        }

        if (zipAudioSource != null)
        {
            if (zipStartClip != null)
            {
                zipAudioSource.PlayOneShot(zipStartClip, audioVolume);
            }
            if (zipLoopClip != null)
            {
                zipAudioSource.clip = zipLoopClip;
                zipAudioSource.loop = true;
                zipAudioSource.volume = audioVolume;
                zipAudioSource.Play();
            }
        }
    }

    private void StepZipLine(float dt)
    {
        if (activeTrack == null || activeTrack.StartPoint == null || activeTrack.EndPoint == null || controller == null || !controller.enabled)
        {
            ExitZipLine(applyJumpBoost: false);
            return;
        }

        float trackLen = Mathf.Max(0.01f, activeTrack.Length);
        currentZipSpeed = Mathf.Min(maxZipSpeed, currentZipSpeed + slopeAcceleration * dt);

        float deltaProgress = (currentZipSpeed * dt) / trackLen;
        currentProgress = Mathf.Clamp01(currentProgress + deltaProgress);

        Vector3 wirePoint = activeTrack.GetPointAtProgress(currentProgress);
        Vector3 desiredFootPos = wirePoint - Vector3.up * gripHeightOffset;

        float blend = 1f - Mathf.Exp(-positionFollowSmoothness * dt);
        Vector3 smoothedPos = Vector3.Lerp(transform.position, desiredFootPos, blend);
        Vector3 moveDelta = smoothedPos - transform.position;

        controller.Move(moveDelta);

        if (currentProgress >= 0.999f)
        {
            ExitZipLine(applyJumpBoost: false);
        }
    }

    private void ExitZipLine(bool applyJumpBoost)
    {
        Vector3 exitDir = activeTrack != null ? activeTrack.GetDirection() : transform.forward;
        exitDir.y = 0f;
        if (exitDir.sqrMagnitude < 0.001f) exitDir = transform.forward;
        exitDir.Normalize();

        float exitSpeed = currentZipSpeed * exitForwardMomentumMultiplier;

        isZipLining = false;
        activeTrack = null;
        currentProgress = 0f;
        currentZipSpeed = 0f;

        if (cameraScript == null)
        {
            cameraScript = GetComponentInChildren<IronStormCamera>();
        }
        if (cameraScript != null)
        {
            cameraScript.SetZipLineActive(false);
        }

        if (zipAudioSource != null && zipAudioSource.loop)
        {
            zipAudioSource.Stop();
            zipAudioSource.clip = null;
        }

        if (movementScript != null)
        {
            movementScript.enabled = prevMovementEnabled || true;
            Vector3 exitVel = exitDir * exitSpeed;
            if (applyJumpBoost)
            {
                exitVel.y = jumpOffUpwardBoost;
            }
            movementScript.OverrideHorizontalVelocityFromSlide(new Vector3(exitVel.x, 0f, exitVel.z));
        }

        if (jumpPackScript != null)
        {
            jumpPackScript.enabled = prevJumpPackEnabled || true;
        }

        if (applyJumpBoost && controller != null && controller.enabled)
        {
            controller.Move(Vector3.up * (jumpOffUpwardBoost * 0.05f));
        }
    }
}
using UnityEngine;
using UnityEngine.AI;

public class EnemyBodyStatusEffect : MonoBehaviour
{
    [Header("Tham Chiếu Model & Thành Phần Quái")]
    [Tooltip("Transform phần mesh/visual của quái để ngã nhẹ về phía trước khi choáng (nếu để trống tự tìm object con đầu tiên).")]
    [SerializeField] private Transform visualModelRoot;

    [Tooltip("Tốc độ nội suy ngã người khi choáng và đứng dậy khi hết choáng.")]
    [SerializeField] private float tiltSmoothSpeed = 12f;

    [Tooltip("Góc ngã người nhẹ về phía trước khi bị choáng (độ).")]
    [SerializeField] private float stunForwardTiltAngle = 22f;

    private NavMeshAgent navMeshAgent;
    private RangedEnemyAI rangedAI;
    private MeleeEnemyAI meleeAI;
    private HealthSystem healthSystem;

    private float baseNavSpeed;
    private bool hasCachedBaseNavSpeed;

    private float slowTimer;
    private float currentSlowMultiplier = 1f;

    private float stunTimer;
    private bool isStunned;

    private bool prevRangedEnabled;
    private bool prevMeleeEnabled;
    private bool prevNavStopped;
    private bool hasSavedControlState;

    public bool IsStunned => isStunned;
    public bool IsSlowed => slowTimer > 0f;
    public float CurrentSpeedMultiplier => isStunned ? 0f : currentSlowMultiplier;

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        rangedAI = GetComponent<RangedEnemyAI>();
        meleeAI = GetComponent<MeleeEnemyAI>();
        healthSystem = GetComponent<HealthSystem>();

        if (visualModelRoot == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.GetComponentInChildren<Renderer>() != null)
                {
                    visualModelRoot = child;
                    break;
                }
            }
        }

        CacheBaseSpeeds();
    }

    private void CacheBaseSpeeds()
    {
        if (navMeshAgent != null && !hasCachedBaseNavSpeed)
        {
            baseNavSpeed = navMeshAgent.speed;
            hasCachedBaseNavSpeed = true;
        }
    }

    public void ApDungLamCham(float speedMultiplier, float duration)
    {
        if (healthSystem != null && healthSystem.DaChet) return;

        float clampedMult = Mathf.Clamp(speedMultiplier, 0.05f, 1f);
        float clampedDur = Mathf.Max(0.1f, duration);

        currentSlowMultiplier = Mathf.Min(currentSlowMultiplier, clampedMult);
        slowTimer = Mathf.Max(slowTimer, clampedDur);

        ApplySpeedModifier();
    }

    public void ApDungChoang(float duration)
    {
        if (healthSystem != null && healthSystem.DaChet) return;

        float clampedDur = Mathf.Max(0.1f, duration);
        stunTimer = Mathf.Max(stunTimer, clampedDur);

        if (!isStunned)
        {
            isStunned = true;
            SaveAndDisableActiveAI();

            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.velocity = Vector3.zero;
                navMeshAgent.isStopped = true;
            }
        }
    }

    private void SaveAndDisableActiveAI()
    {
        if (hasSavedControlState) return;
        hasSavedControlState = true;

        if (navMeshAgent != null)
        {
            prevNavStopped = navMeshAgent.isStopped;
        }

        if (rangedAI != null)
        {
            prevRangedEnabled = rangedAI.enabled;
            rangedAI.enabled = false;
        }

        if (meleeAI != null)
        {
            prevMeleeEnabled = meleeAI.enabled;
            meleeAI.enabled = false;
        }
    }

    private void RestoreActiveAI()
    {
        if (!hasSavedControlState) return;
        hasSavedControlState = false;

        if (rangedAI != null)
        {
            rangedAI.enabled = prevRangedEnabled;
            rangedAI.NhanSatThuong(0f);
        }

        if (meleeAI != null)
        {
            meleeAI.enabled = prevMeleeEnabled;
            meleeAI.NhanSatThuong(0f);
        }

        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = prevNavStopped;
        }
    }

    private void ApplySpeedModifier()
    {
        CacheBaseSpeeds();
        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            if (isStunned)
            {
                navMeshAgent.speed = 0f;
            }
            else
            {
                float targetBase = Mathf.Max(baseNavSpeed, navMeshAgent.speed);
                baseNavSpeed = targetBase;
                navMeshAgent.speed = baseNavSpeed * currentSlowMultiplier;
            }
        }
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;

        if (healthSystem != null && healthSystem.DaChet)
        {
            isStunned = false;
            stunTimer = 0f;
            slowTimer = 0f;
            return;
        }

        if (slowTimer > 0f)
        {
            slowTimer = Mathf.Max(0f, slowTimer - dt);
            if (slowTimer <= 0f)
            {
                currentSlowMultiplier = 1f;
            }
        }

        if (isStunned)
        {
            stunTimer -= dt;
            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.velocity = Vector3.zero;
                navMeshAgent.isStopped = true;
            }

            if (stunTimer <= 0f)
            {
                stunTimer = 0f;
                isStunned = false;
                RestoreActiveAI();
            }
        }

        ApplySpeedModifier();
        UpdateStunForwardTilt(dt);
    }

    private void UpdateStunForwardTilt(float dt)
    {
        if (visualModelRoot == null) return;

        Quaternion targetLocalRot = isStunned
            ? Quaternion.Euler(stunForwardTiltAngle, 0f, 0f)
            : Quaternion.identity;

        visualModelRoot.localRotation = Quaternion.Slerp(
            visualModelRoot.localRotation,
            targetLocalRot,
            tiltSmoothSpeed * dt
        );
    }
}
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class MeleeWeapon : MonoBehaviour
{
    [Header("Attack & Damage Settings")]
    [Tooltip("Sát thương gây ra khi chém trúng mục tiêu.")]
    [SerializeField] private float attackDamage = 25f;
    [Tooltip("Lực đẩy vật lý tác động lên vật thể có Rigidbody khi chém trúng.")]
    [SerializeField] private float hitForce = 10f;
    [Tooltip("Thời gian đệm giữa các cú chém (giây).")]
    [SerializeField] private float attackCooldown = 0.15f; // Giảm thời gian này xuống để phản hồi nhanh hơn
    private float lastAttackTime = 0f;

    [Header("Swing Animation Settings (Chém Trái sang Phải)")]
    [Tooltip("Độ rộng góc quét ngang (Ví dụ: 50 độ -> Vung từ -50 đến +50 độ).")]
    [SerializeField] private float sweepRangeY = 50f;
    [Tooltip("Góc nghiêng dọc cố định (Trục X) giúp đường chém đẹp mắt.")]
    [SerializeField] private float sweepTiltX = 15f;
    [Tooltip("Tốc độ vung chém.")]
    [SerializeField] private float attackSpeed = 20f;
    [Tooltip("Tốc độ hồi về vị trí thủ ban đầu.")]
    [SerializeField] private float returnSpeed = 15f;

    private Quaternion originalLocalRotation;
    private float currentSweepY = 0f;
    private bool isAttacking = false;
    private bool returningToOriginal = false;
    private bool attackBuffer = false; // Bộ nhớ đệm ghi nhận cú bấm đánh tiếp theo

    [Header("Sword Transform References (Đầu kiếm & Đuôi kiếm)")]
    [Tooltip("Kéo đối tượng Đuôi kiếm / Chuôi kiếm (Gốc cầm) vào đây.")]
    [SerializeField] private Transform swordBase;
    [Tooltip("Kéo đối tượng Đầu kiếm (Mũi kiếm) vào đây.")]
    [SerializeField] private Transform swordTip;

    [Tooltip("Bán kính vùng quét va chạm (SphereCast Radius).")]
    [SerializeField] private float sweepRadius = 0.3f;

    [Header("Tag & Layer Settings (Nhận diện mục tiêu)")]
    [TagSelector]
    [SerializeField] private string wallTag = "Wall";
    [TagSelector]
    [SerializeField] private string enemyTag = "Enemy";
    [Tooltip("Layer tính là mặt đất/sàn nhà để định hướng vết chém phẳng.")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Slash Mark (Vết xước) Settings")]
    [SerializeField] private GameObject slashMarkPrefab;
    [Tooltip("Khoảng cách bù trừ cực nhỏ để vết xước bám sát bề mặt mà không bị lơ lửng.")]
    [SerializeField] private float offsetDistance = 0.002f;
    [Tooltip("Thời gian tồn tại của vết chém trước khi biến mất (giây).")]
    [SerializeField] private float slashMarkLifetime = 15f;

    private static List<GameObject> activeSlashMarks = new List<GameObject>();
    private const int maxSlashMarks = 30;

    [Header("Anti-Clipping Settings (Chống đâm xuyên tường khi áp sát)")]
    [Tooltip("Khoảng cách tầm với tối đa của kiếm tính từ Camera. Tránh chém xuyên qua tường dày.")]
    [SerializeField] private float maxReachDistance = 1.8f;

    [Header("Audio Settings (Random Swing & Hit)")]
    [Tooltip("Mảng âm thanh vung kiếm (Phát ngẫu nhiên mỗi lần chém gió).")]
    [SerializeField] private AudioClip[] swingAudioClips;
    [Tooltip("Mảng âm thanh chém trúng tường/mặt đất/vật thể môi trường.")]
    [SerializeField] private AudioClip[] wallHitAudioClips;
    [Tooltip("Mảng âm thanh chém trúng kẻ địch (Phát ngẫu nhiên khi chém trúng Tag Enemy).")]
    [SerializeField] private AudioClip[] hitAudioClips;
    [Range(0f, 1f)][SerializeField] private float audioVolume = 1.0f;
    private AudioSource audioSource;

    [Header("Input Action References")]
    [SerializeField] private InputActionReference attackAction;

    // Biến phụ trợ lọc trùng lặp trong một cú chém
    private HashSet<Collider> hitCollidersInCurrentSwing = new HashSet<Collider>();
    private bool hasPlayedImpactAudioThisSwing = false;

    private bool IsEquippedByPlayer()
    {
        if (!gameObject.activeInHierarchy || !enabled) return false;
        if (transform.parent == null) return false;
        return GetComponentInParent<CharacterController>() != null || GetComponentInParent<WeaponSwitcher>() != null;
    }

    private void Awake()
    {
        originalLocalRotation = transform.localRotation;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (swordBase == null || swordTip == null)
        {
            Debug.LogWarning("Chưa gán đầy đủ Đuôi kiếm (Sword Base) hoặc Đầu kiếm (Sword Tip) cho MeleeWeapon!");
        }
    }

    private void OnEnable()
    {
        if (attackAction != null && attackAction.action != null)
        {
            attackAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (attackAction != null && attackAction.action != null)
        {
            attackAction.action.Disable();
        }
    }

    private void Update()
    {
        if (!IsEquippedByPlayer())
        {
            isAttacking = false;
            returningToOriginal = false;
            attackBuffer = false;
            currentSweepY = 0f;
            return;
        }

        bool isAttackInput = attackAction != null && attackAction.action.triggered;

        // Lưu lại tín hiệu bấm đánh (Attack Buffer) nếu người chơi bấm trong lúc kiếm đang thu về
        if (isAttackInput)
        {
            attackBuffer = true;
        }

        // Kiểm tra điều kiện để thực hiện cú chém mới ngay khi có thể
        bool canAttack = !isAttacking && (Time.time >= lastAttackTime + attackCooldown || (!returningToOriginal && attackBuffer));

        if (canAttack && (isAttackInput || attackBuffer))
        {
            attackBuffer = false;
            StopAllCoroutines(); // Ngắt trạng thái hồi về cũ để vung đòn mới ngay lập tức
            StartCoroutine(ExecuteLeftToRightSlash());
        }

        UpdateSwordAnimation();
    }

    private System.Collections.IEnumerator ExecuteLeftToRightSlash()
    {
        isAttacking = true;
        returningToOriginal = false;
        lastAttackTime = Time.time;

        hitCollidersInCurrentSwing.Clear();
        hasPlayedImpactAudioThisSwing = false;

        PlayRandomSwingAudio();

        float progress = 0f;
        while (progress < 1f)
        {
            if (!IsEquippedByPlayer())
            {
                isAttacking = false;
                returningToOriginal = false;
                yield break;
            }

            progress += Time.deltaTime * attackSpeed;
            float t = Mathf.Sin(progress * Mathf.PI * 0.5f);
            currentSweepY = Mathf.Lerp(-sweepRangeY, sweepRangeY, t);

            PerformSweepHitDetection();

            // Cho phép người chơi nhấn đánh tiếp ngay giai đoạn cuối của cú chém (Combo Buffer)
            if (progress > 0.6f && (attackAction != null && attackAction.action.triggered))
            {
                attackBuffer = true;
            }

            yield return null;
        }

        isAttacking = false;
        returningToOriginal = true;

        while (returningToOriginal)
        {
            if (!IsEquippedByPlayer())
            {
                returningToOriginal = false;
                yield break;
            }

            currentSweepY = Mathf.Lerp(currentSweepY, 0f, returnSpeed * Time.deltaTime);

            if (Mathf.Abs(currentSweepY) < 0.1f)
            {
                currentSweepY = 0f;
                returningToOriginal = false;
            }

            // Nếu người chơi bấm đánh trong lúc kiếm đang trượt về vị trí cũ, cho phép kích hoạt ngay
            if (attackBuffer)
            {
                returningToOriginal = false;
                break;
            }

            yield return null;
        }
    }

    private void PerformSweepHitDetection()
    {
        if (swordBase == null || swordTip == null) return;

        if (Camera.main != null)
        {
            Transform camTransform = Camera.main.transform;
            if (Physics.Raycast(camTransform.position, camTransform.forward, out RaycastHit directHit, maxReachDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (directHit.transform.root != transform.root)
                {
                    if (!hitCollidersInCurrentSwing.Contains(directHit.collider))
                    {
                        hitCollidersInCurrentSwing.Add(directHit.collider);
                        ProcessMeleeImpact(directHit, directHit.point, directHit.normal);
                        return;
                    }
                }
            }
        }

        Vector3 p1 = swordBase.position;
        Vector3 p2 = swordTip.position;
        float distance = Vector3.Distance(p1, p2);
        Vector3 direction = (p2 - p1).normalized;

        RaycastHit[] hits = Physics.SphereCastAll(p1, sweepRadius, direction, distance, ~0, QueryTriggerInteraction.Ignore);

        foreach (var hit in hits)
        {
            if (hit.transform.root == transform.root) continue;
            if (hitCollidersInCurrentSwing.Contains(hit.collider)) continue;

            Vector3 finalPoint = hit.point;
            Vector3 finalNormal = hit.normal;

            if (finalPoint == Vector3.zero || float.IsNaN(finalPoint.x))
            {
                finalPoint = hit.collider.ClosestPoint(p1);
                finalNormal = -direction;
            }

            if (Camera.main != null)
            {
                float distToCam = Vector3.Distance(Camera.main.transform.position, finalPoint);
                if (distToCam > maxReachDistance) continue;
            }

            hitCollidersInCurrentSwing.Add(hit.collider);
            ProcessMeleeImpact(hit, finalPoint, finalNormal);
        }
    }

    private bool IsTargetTag(GameObject obj, string targetTagToCheck)
    {
        Transform current = obj.transform;
        while (current != null)
        {
            if (IsTagSimilar(current.tag, targetTagToCheck))
            {
                return true;
            }
            current = current.parent;
        }
        return false;
    }

    private bool IsInGroundLayer(GameObject obj)
    {
        return ((1 << obj.layer) & groundLayer.value) != 0;
    }

    private bool IsTagSimilar(string objectTag, string targetTag)
    {
        if (string.IsNullOrEmpty(objectTag) || string.IsNullOrEmpty(targetTag)) return false;

        string objLower = objectTag.ToLower();
        string targetLower = targetTag.ToLower();

        if (objLower == targetLower || objLower.Contains(targetLower) || targetLower.Contains(objLower))
            return true;

        int distance = ComputeLevenshteinDistance(objLower, targetLower);
        int maxLen = Mathf.Max(objLower.Length, targetLower.Length);
        float similarity = 1.0f - ((float)distance / maxLen);

        return similarity >= 0.5f;
    }

    private int ComputeLevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Mathf.Min(Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    private void ProcessMeleeImpact(RaycastHit hit, Vector3 hitPoint, Vector3 hitNormal)
    {
        bool isEnemyHit = IsTargetTag(hit.collider.gameObject, enemyTag);
        bool isWallHit = IsTargetTag(hit.collider.gameObject, wallTag) || !isEnemyHit;

        if (!hasPlayedImpactAudioThisSwing)
        {
            if (isEnemyHit)
            {
                PlayRandomHitAudio();
            }
            else if (isWallHit)
            {
                PlayRandomWallHitAudio();
            }
            hasPlayedImpactAudioThisSwing = true;
        }

        if (isEnemyHit)
            hit.collider.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);

        Rigidbody hitRb = hit.collider.GetComponentInParent<Rigidbody>();
        if (hitRb != null && !hitRb.isKinematic)
        {
            Vector3 forceDir = (transform.forward + Vector3.up * 0.2f).normalized;
            hitRb.AddForce(forceDir * hitForce, ForceMode.Impulse);
        }

        if (!isEnemyHit)
        {
            bool isGround = IsInGroundLayer(hit.collider.gameObject);
            SpawnSlashMark(hitPoint, hitNormal, hit.transform, isGround);
        }
    }

    private void PlayRandomSwingAudio()
    {
        if (audioSource != null && swingAudioClips != null && swingAudioClips.Length > 0)
        {
            AudioClip clip = swingAudioClips[Random.Range(0, swingAudioClips.Length)];
            if (clip != null) audioSource.PlayOneShot(clip, audioVolume);
        }
    }

    private void PlayRandomHitAudio()
    {
        if (audioSource != null && hitAudioClips != null && hitAudioClips.Length > 0)
        {
            AudioClip clip = hitAudioClips[Random.Range(0, hitAudioClips.Length)];
            if (clip != null) audioSource.PlayOneShot(clip, audioVolume);
        }
    }

    private void PlayRandomWallHitAudio()
    {
        if (audioSource != null && wallHitAudioClips != null && wallHitAudioClips.Length > 0)
        {
            AudioClip clip = wallHitAudioClips[Random.Range(0, wallHitAudioClips.Length)];
            if (clip != null) audioSource.PlayOneShot(clip, audioVolume);
        }
    }

    private void SpawnSlashMark(Vector3 point, Vector3 normal, Transform hitTransform, bool isGround)
    {
        if (slashMarkPrefab != null)
        {
            Quaternion rot;

            if (isGround && Mathf.Abs(normal.y) > 0.95f)
            {
                rot = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            }
            else
            {
                Vector3 upward = Vector3.up;
                if (Mathf.Abs(Vector3.Dot(normal, upward)) > 0.99f)
                {
                    upward = Vector3.forward;
                }
                rot = Quaternion.LookRotation(-normal, upward);
                rot *= Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            }

            Vector3 pos = point + (normal * offsetDistance);
            GameObject slashMark = Instantiate(slashMarkPrefab, pos, rot);

            if (hitTransform != null)
            {
                slashMark.transform.SetParent(hitTransform, true);
            }

            activeSlashMarks.Add(slashMark);

            if (activeSlashMarks.Count > maxSlashMarks)
            {
                if (activeSlashMarks[0] != null)
                {
                    Destroy(activeSlashMarks[0]);
                }
                activeSlashMarks.RemoveAt(0);
            }

            Destroy(slashMark, slashMarkLifetime);
        }
    }

    private void UpdateSwordAnimation()
    {
        Quaternion sweepRot = Quaternion.Euler(sweepTiltX, currentSweepY, 0f);
        transform.localRotation = originalLocalRotation * sweepRot;
    }
}
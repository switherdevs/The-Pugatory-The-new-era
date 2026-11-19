using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum CombatType { Melee, Ranged }

    [Header("Combat Configuration")]
    [Tooltip("Chọn phong cách chiến đấu của kẻ địch (Cận chiến hay Tầm xa).")]
    [SerializeField] private CombatType combatType = CombatType.Melee;

    [Header("Target & Layer Settings")]
    [Tooltip("Kéo Transform của người chơi vào đây (hoặc script sẽ tự tìm qua Player Layer).")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("Layer của Player để AI nhận diện.")]
    [SerializeField] private LayerMask playerLayer;

    [Header("Ranged Settings (Chỉ dùng khi chọn Tầm xa)")]
    [Tooltip("Khoảng cách tối ưu mà AI tầm xa muốn duy trì so với người chơi.")]
    [SerializeField] private float preferredDistance = 7f;
    [Tooltip("Sai số khoảng cách khi lùi/tiến.")]
    [SerializeField] private float distanceTolerance = 1.5f;

    [Header("Jump & Off-Mesh Link Settings (Nhận diện chỗ cao/thấp)")]
    [Tooltip("Tốc độ nhảy qua các điểm Off-Mesh Link (vực, bậc thang cao, chỗ nhảy).")]
    [SerializeField] private float jumpSpeed = 6f;

    private NavMeshAgent agent;
    private bool isJumping = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        // Tự động tìm Player nếu chưa gán trên Inspector
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }
    }

    private void Update()
    {
        if (playerTransform == null) return;

        // Xử lý di chuyển qua lại giữa các bậc cao/thấp (Off-Mesh Link)
        if (agent.isOnOffMeshLink)
        {
            if (!isJumping)
            {
                StartCoroutine(HandleOffMeshLinkJump());
            }
            return;
        }

        // Điều phối hành vi dựa theo loại chiến đấu (Combat Type) được chọn từ Dropdown (Enum)
        switch (combatType)
        {
            case CombatType.Melee:
                ExecuteMeleeBehavior();
                break;

            case CombatType.Ranged:
                ExecuteRangedBehavior();
                break;
        }
    }

    /// <summary>
    /// Hành vi cận chiến: Bám đuổi theo người chơi mọi lúc mọi nơi
    /// </summary>
    private void ExecuteMeleeBehavior()
    {
        agent.isStopped = false;
        agent.SetDestination(playerTransform.position);
    }

    /// <summary>
    /// Hành vi tầm xa: Tiến tới hoặc lùi lại để giữ khoảng cách an toàn với người chơi
    /// </summary>
    private void ExecuteRangedBehavior()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer > preferredDistance + distanceTolerance)
        {
            // Người chơi ở quá xa -> Di chuyển áp sát đến khoảng cách mong muốn
            agent.isStopped = false;
            agent.SetDestination(playerTransform.position);
        }
        else if (distanceToPlayer < preferredDistance - distanceTolerance)
        {
            // Người chơi lại quá gần -> Lùi ra xa để giữ khoảng cách
            agent.isStopped = false;
            Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
            Vector3 retreatPosition = transform.position - dirToPlayer * 4f;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(retreatPosition, out hit, 4f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
        else
        {
            // Đã đứng đúng khoảng cách lý tưởng -> Dừng di chuyển để ngắm bắn / tấn công
            agent.isStopped = true;

            // Xoay mặt mượt mà về phía người chơi
            Vector3 lookDir = (playerTransform.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
            }
        }
    }

    /// <summary>
    /// Xử lý logic nhảy lên hoặc nhảy xuống khi gặp các khu vực chênh lệch địa hình (Off-Mesh Link trên NavMesh)
    /// </summary>
    private System.Collections.IEnumerator HandleOffMeshLinkJump()
    {
        isJumping = true;
        agent.updatePosition = false; // Đã sửa lỗi chính tả từ venutoUpdatePosition thành updatePosition

        OffMeshLinkData data = agent.currentOffMeshLinkData;
        Vector3 startPos = agent.transform.position;
        Vector3 endPos = data.endPos + Vector3.up * agent.baseOffset;

        float duration = Vector3.Distance(startPos, endPos) / jumpSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // Tạo đường cong parabol cho cú nhảy (lên cao ở giữa và rơi xuống)
            float verticalOffset = Mathf.Sin(t * Mathf.PI) * 2f;
            transform.position = Vector3.Lerp(startPos, endPos, t) + new Vector3(0, verticalOffset, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;
        agent.CompleteOffMeshLink();
        agent.updatePosition = true;
        isJumping = false;
    }

    private void OnDrawGizmosSelected()
    {
        // Vẽ vòng tròn thể hiện khoảng cách giữ khoảng cách khi ở chế độ Ranged
        if (combatType == CombatType.Ranged)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, preferredDistance);
        }
    }
}
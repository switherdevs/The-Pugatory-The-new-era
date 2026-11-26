using UnityEngine;

[ExecuteAlways]
public class ZipLineTrack : MonoBehaviour
{
    [Header("Điểm Neo Dây (Từ Nơi Cao Về Nơi Thấp)")]
    [Tooltip("Điểm bắt đầu ở trên cao.")]
    [SerializeField] private Transform startPoint;

    [Tooltip("Điểm kết thúc ở nơi thấp hơn.")]
    [SerializeField] private Transform endPoint;

    [Tooltip("Tự động hoán đổi Start và End nếu Start thấp hơn End để luôn đi từ cao xuống thấp.")]
    [SerializeField] private bool enforceHighToLow = true;

    public Transform StartPoint => startPoint;
    public Transform EndPoint => endPoint;

    public float Length
    {
        get
        {
            if (startPoint == null || endPoint == null) return 0f;
            return Vector3.Distance(startPoint.position, endPoint.position);
        }
    }

    private void Awake()
    {
        EnsurePointsValid();
    }

    private void OnValidate()
    {
        EnsurePointsValid();
    }

    private void EnsurePointsValid()
    {
        if (startPoint == null || endPoint == null) return;

        if (enforceHighToLow && startPoint.position.y < endPoint.position.y)
        {
            (startPoint.position, endPoint.position) = (endPoint.position, startPoint.position);
        }
    }

    public Vector3 GetPointAtProgress(float t)
    {
        if (startPoint == null || endPoint == null) return transform.position;
        return Vector3.Lerp(startPoint.position, endPoint.position, Mathf.Clamp01(t));
    }

    public Vector3 GetDirection()
    {
        if (startPoint == null || endPoint == null) return Vector3.forward;
        Vector3 dir = endPoint.position - startPoint.position;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
    }

    public float ProjectProgress01(Vector3 worldPoint)
    {
        if (startPoint == null || endPoint == null) return 0f;

        Vector3 a = startPoint.position;
        Vector3 b = endPoint.position;
        Vector3 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq <= 0.0001f) return 0f;

        float t = Vector3.Dot(worldPoint - a, ab) / lenSq;
        return Mathf.Clamp01(t);
    }

    public Vector3 ClosestPointOnLine(Vector3 worldPoint, out float progress01)
    {
        progress01 = ProjectProgress01(worldPoint);
        return GetPointAtProgress(progress01);
    }

    private void OnDrawGizmos()
    {
        if (startPoint == null || endPoint == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(startPoint.position, endPoint.position);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(startPoint.position, 0.2f);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(endPoint.position, 0.2f);
    }
}
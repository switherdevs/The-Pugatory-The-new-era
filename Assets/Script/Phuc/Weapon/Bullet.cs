using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TagSelectorAttribute : PropertyAttribute { }

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Bullet : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float speed = 50f;
    [SerializeField] private float lifeTime = 5f;

    [Header("Physics Impact Settings")]
    [Tooltip("Lực đẩy tác động lên vật thể có Rigidbody khi trúng đạn.")]
    [SerializeField] private float hitForce = 15f;

    [Header("Collision Targets (Dropdown)")]
    [TagSelector]
    [SerializeField] private string targetTag = "Wall";
    [SerializeField] private LayerMask groundLayer;

    [Header("Effects & Offset")]
    [SerializeField] private GameObject bulletHolePrefab;
    [SerializeField] private GameObject dustEffectPrefab;
    [Tooltip("Khoảng cách bù trừ đẩy lỗ đạn lùi ra xa bề mặt để không bị chìm lấp.")]
    [SerializeField] private float offsetDistance = 0.005f;

    // Quản lý tối đa 30 lỗ đạn (Cơ chế FIFO)
    private static List<GameObject> activeBulletHoles = new List<GameObject>();
    private const int maxBulletHoles = 30;

    private Rigidbody rb;
    private bool hasCollided = false;
    private Vector3 lastPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void Start()
    {
        Collider bulletCol = GetComponent<Collider>();
        if (bulletCol != null)
        {
            bulletCol.isTrigger = true;
        }

        if (rb != null)
        {
            rb.linearVelocity = transform.forward * speed;
        }

        lastPosition = transform.position;
        Destroy(gameObject, lifeTime);

        // Bỏ qua va chạm với Player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && bulletCol != null)
        {
            foreach (Collider col in player.GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(bulletCol, col, true);
            }
        }
    }

    private void FixedUpdate()
    {
        if (hasCollided) return;

        Vector3 currentPosition = transform.position;
        Vector3 displacement = currentPosition - lastPosition;
        float rayDistance = displacement.magnitude + 0.1f;
        Vector3 rayDir = displacement.sqrMagnitude > 0.0001f ? displacement.normalized : transform.forward;

        if (Physics.Raycast(lastPosition, rayDir, out RaycastHit hit, rayDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (IsValidTarget(hit.collider.gameObject))
            {
                hasCollided = true;
                ProcessImpact(hit.collider, hit.point, hit.normal, hit.transform);
                Destroy(gameObject);
                return;
            }
        }

        lastPosition = currentPosition;
    }

    public void Initialize(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();
            transform.rotation = Quaternion.LookRotation(direction);
            if (rb != null)
            {
                rb.linearVelocity = direction * speed;
            }
        }
        lastPosition = transform.position;
    }

    private bool IsValidTarget(GameObject obj)
    {
        if (IsInGroundLayer(obj)) return true;

        if (!string.IsNullOrEmpty(targetTag))
        {
            Transform current = obj.transform;
            while (current != null)
            {
                if (current.CompareTag(targetTag))
                {
                    return true;
                }
                current = current.parent;
            }
        }
        return false;
    }

    private bool IsInGroundLayer(GameObject obj)
    {
        return ((1 << obj.layer) & groundLayer.value) != 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasCollided) return;

        if (IsValidTarget(other.gameObject))
        {
            hasCollided = true;

            Vector3 rayOrigin = transform.position + (transform.forward * 0.2f);
            Vector3 rayDir = -transform.forward;
            Vector3 contactPoint = transform.position;
            Vector3 contactNormal = transform.forward;

            if (other.Raycast(new Ray(rayOrigin, rayDir), out RaycastHit hit, 0.5f))
            {
                contactPoint = hit.point;
                contactNormal = hit.normal;
            }
            else
            {
                // Sử dụng biến cục bộ thay vì gọi trực tiếp để trình biên dịch không báo warning unused
                Vector3 fallbackPoint = other.ClosestPoint(transform.position);
                contactPoint = fallbackPoint;
            }

            ProcessImpact(other, contactPoint, contactNormal, other.transform);
            Destroy(gameObject);
        }
    }

    private void ProcessImpact(Collider hitCollider, Vector3 contactPoint, Vector3 contactNormal, Transform hitTransform)
    {
        // 1. TƯƠNG TÁC VẬT LÝ
        if (hitTransform != null)
        {
            Rigidbody hitRb = hitTransform.GetComponentInParent<Rigidbody>();
            if (hitRb != null && !hitRb.isKinematic)
            {
                Vector3 forceDir = (transform.forward + Vector3.up * 0.2f).normalized;
                hitRb.AddForce(forceDir * hitForce, ForceMode.Impulse);
            }
        }

        bool isGround = IsInGroundLayer(hitCollider.gameObject);
        SpawnEffects(contactPoint, contactNormal, isGround, hitTransform);
    }

    private void SpawnEffects(Vector3 point, Vector3 normal, bool isGround, Transform hitTransform)
    {
        // 2. Tạo lỗ đạn và bám sát bề mặt lưới mesh
        if (bulletHolePrefab != null)
        {
            Quaternion rot;

            if (isGround && Mathf.Abs(normal.y) > 0.95f)
            {
                rot = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);
            }
            else
            {
                rot = Quaternion.LookRotation(-normal, Vector3.up);
            }

            Vector3 pos = point + (normal * offsetDistance);
            GameObject hole = Instantiate(bulletHolePrefab, pos, rot);

            if (hitTransform != null)
            {
                hole.transform.SetParent(hitTransform, true);
            }

            activeBulletHoles.Add(hole);

            if (activeBulletHoles.Count > maxBulletHoles)
            {
                if (activeBulletHoles[0] != null)
                {
                    Destroy(activeBulletHoles[0]);
                }
                activeBulletHoles.RemoveAt(0);
            }

            Destroy(hole, 10f);
        }

        // 3. TẠO HIỆU ỨNG BỤI NÂNG CAO
        if (dustEffectPrefab != null)
        {
            Quaternion baseRot = Quaternion.LookRotation(normal);
            Quaternion randomRot = baseRot * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Vector3 dustPos = point + (normal * 0.005f);
            GameObject dust = Instantiate(dustEffectPrefab, dustPos, randomRot);

            if (hitTransform != null)
            {
                dust.transform.SetParent(hitTransform, true);
            }

            ParticleSystem ps = dust.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                float totalDuration = ps.main.duration + ps.main.startLifetime.constantMax;
                Destroy(dust, Mathf.Max(totalDuration, 2f));
            }
            else
            {
                Destroy(dust, 3f);
            }
        }
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(TagSelectorAttribute))]
public class TagSelectorPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType == SerializedPropertyType.String)
        {
            EditorGUI.BeginProperty(position, label, property);
            property.stringValue = EditorGUI.TagField(position, label, property.stringValue);
            EditorGUI.EndProperty();
        }
        else
        {
            // Đảm bảo sử dụng tham số truyền vào để tránh warning unused parameter trong custom drawer
            EditorGUI.PropertyField(position, property, label);
        }
    }
}
#endif
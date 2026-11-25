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
    [SerializeField] private float damage = 20f;
    [SerializeField] private float hitCastRadius = 0.04f;
    [SerializeField] private HealthSystem.LoaiDoiTuong pheMucTieu = HealthSystem.LoaiDoiTuong.QuaiVat;

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
    private static readonly List<GameObject> activeBulletHoles = new List<GameObject>();
    private const int maxBulletHoles = 30;

    private Rigidbody rb;
    private Collider bulletCollider;
    private Transform ownerRoot;
    private Vector3 moveDirection = Vector3.forward;
    private bool hasCollided = false;
    private bool hasDamaged = false;
    private Vector3 lastPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bulletCollider = GetComponent<Collider>();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }

        if (bulletCollider != null)
        {
            bulletCollider.isTrigger = true;
        }

        moveDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
        lastPosition = transform.position;
    }

    private void Start()
    {
        DamageDealer dealer = GetComponent<DamageDealer>();
        if (dealer != null)
        {
            dealer.enabled = false;
        }

        if (transform.forward.sqrMagnitude > 0.0001f)
        {
            moveDirection = transform.forward.normalized;
        }

        lastPosition = transform.position;
        Destroy(gameObject, Mathf.Max(0.05f, lifeTime));

        IgnoreOwnerAndPlayerCollisions();
    }

    private void IgnoreOwnerAndPlayerCollisions()
    {
        if (bulletCollider == null) return;

        if (ownerRoot != null)
        {
            Collider[] ownerCols = ownerRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < ownerCols.Length; i++)
            {
                if (ownerCols[i] != null)
                {
                    Physics.IgnoreCollision(bulletCollider, ownerCols[i], true);
                }
            }
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Collider[] playerCols = player.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < playerCols.Length; i++)
            {
                if (playerCols[i] != null)
                {
                    Physics.IgnoreCollision(bulletCollider, playerCols[i], true);
                }
            }
        }
    }

    public void Initialize(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.0001f)
        {
            moveDirection = direction.normalized;
            transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        }
        lastPosition = transform.position;
    }

    public void SetDamage(float amount)
    {
        damage = Mathf.Max(0f, amount);
    }

    public void SetOwner(Transform owner)
    {
        ownerRoot = owner != null ? owner.root : null;
        IgnoreOwnerAndPlayerCollisions();
    }

    private void Update()
    {
        if (hasCollided) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 startPos = lastPosition;
        float stepDistance = Mathf.Max(0f, speed) * dt;
        Vector3 nextPos = startPos + moveDirection * stepDistance;

        float castRadius = Mathf.Max(0.005f, hitCastRadius);
        if (stepDistance > 0.0001f &&
            Physics.SphereCast(startPos, castRadius, moveDirection, out RaycastHit hit, stepDistance + castRadius, ~0, QueryTriggerInteraction.Collide))
        {
            if (IsIgnoredOwner(hit.collider))
            {
                transform.position = nextPos;
                lastPosition = nextPos;
                return;
            }

            if (IsValidTarget(hit.collider.gameObject))
            {
                hasCollided = true;
                Vector3 impactPoint = hit.point.sqrMagnitude > 0.0001f ? hit.point : startPos + moveDirection * hit.distance;
                Vector3 impactNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal : -moveDirection;
                transform.position = impactPoint;
                ProcessImpact(hit.collider, impactPoint, impactNormal, hit.transform);
                Destroy(gameObject);
                return;
            }
        }

        transform.position = nextPos;
        lastPosition = nextPos;
    }

    private bool IsIgnoredOwner(Collider col)
    {
        if (col == null) return true;
        if (col == bulletCollider) return true;
        if (ownerRoot != null && col.transform.root == ownerRoot) return true;
        return false;
    }

    private bool IsValidTarget(GameObject obj)
    {
        if (obj == null) return false;

        HealthSystem hp = obj.GetComponentInParent<HealthSystem>();
        if (hp != null && !hp.DaChet && hp.Phe == pheMucTieu)
        {
            return true;
        }

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
        if (hasCollided || IsIgnoredOwner(other)) return;

        if (IsValidTarget(other.gameObject))
        {
            hasCollided = true;

            Vector3 rayOrigin = transform.position + (moveDirection * 0.2f);
            Vector3 rayDir = -moveDirection;
            Vector3 contactPoint = transform.position;
            Vector3 contactNormal = -moveDirection;

            if (other.Raycast(new Ray(rayOrigin, rayDir), out RaycastHit hit, 0.5f))
            {
                contactPoint = hit.point;
                contactNormal = hit.normal;
            }
            else
            {
                contactPoint = other.ClosestPoint(transform.position);
            }

            ProcessImpact(other, contactPoint, contactNormal, other.transform);
            Destroy(gameObject);
        }
    }

    private void ProcessImpact(Collider hitCollider, Vector3 contactPoint, Vector3 contactNormal, Transform hitTransform)
    {
        // 1. GÂY SÁT THƯƠNG CHO MỤC TIÊU HỢP LỆ
        if (!hasDamaged && hitCollider != null)
        {
            HealthSystem heThongMau = hitCollider.GetComponentInParent<HealthSystem>();
            if (heThongMau != null && !heThongMau.DaChet && heThongMau.Phe == pheMucTieu && damage > 0f)
            {
                hasDamaged = true;
                heThongMau.NhanSatThuong(damage);
            }
        }

        // 2. TƯƠNG TÁC VẬT LÝ
        if (hitTransform != null)
        {
            Rigidbody hitRb = hitTransform.GetComponentInParent<Rigidbody>();
            if (hitRb != null && !hitRb.isKinematic)
            {
                Vector3 forceDir = (moveDirection + Vector3.up * 0.2f).normalized;
                hitRb.AddForce(forceDir * hitForce, ForceMode.Impulse);
            }
        }

        bool isGround = IsInGroundLayer(hitCollider.gameObject);
        SpawnEffects(contactPoint, contactNormal, isGround, hitTransform);
    }

    private void SpawnEffects(Vector3 point, Vector3 normal, bool isGround, Transform hitTransform)
    {
        // 3. Tạo lỗ đạn và bám sát bề mặt lưới mesh
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

        // 4. Tạo hiệu ứng bụi / tia lửa
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
            EditorGUI.PropertyField(position, property, label);
        }
    }
}
#endif
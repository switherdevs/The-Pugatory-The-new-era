using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChonTagLuuDanAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ChonTagLuuDanAttribute))]
public class ChonTagLuuDanDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType == SerializedPropertyType.String)
        {
            EditorGUI.BeginProperty(position, label, property);
            string currentTag = string.IsNullOrEmpty(property.stringValue) ? "Untagged" : property.stringValue;
            property.stringValue = EditorGUI.TagField(position, label, currentTag);
            EditorGUI.EndProperty();
        }
        else
        {
            EditorGUI.PropertyField(position, property, label);
        }
    }
}
#endif

public class EnemyAlertSystem : MonoBehaviour
{
    [Header("Báo động đồng minh xung quanh")]
    [SerializeField] private float banKinhBaoDong = 20f;
    [SerializeField] private LayerMask layerDongMinh = 0;
    [SerializeField] private float thoiGianGianCachBaoDong = 0.4f;

    [Header("Quét lựu đạn để báo động bỏ chạy")]
    [Tooltip("Bật/tắt quét lựu đạn xung quanh để báo động cho quái bỏ chạy.")]
    [SerializeField] private bool batQuetLuuDan = true;

    [Tooltip("Bán kính quét lựu đạn để phát tín hiệu báo động.")]
    [SerializeField] private float banKinhQuetLuuDan = 12f;

    [Tooltip("Layer của lựu đạn (để 0 nếu muốn quét mọi layer rồi lọc theo tag/tên).")]
    [SerializeField] private LayerMask layerLuuDan = 0;

    [Tooltip("Chọn Tag lựu đạn từ danh sách dropdown.")]
    [ChonTagLuuDan]
    [SerializeField] private string tagLuuDan = "Untagged";

    [Tooltip("Từ khóa trong tên GameObject để nhận diện lựu đạn nếu không dùng tag/layer riêng.")]
    [SerializeField] private string tuKhoaTenLuuDan = "grenade";

    [Tooltip("Thời gian giãn cách giữa 2 lần phát báo động lựu đạn (giây).")]
    [SerializeField] private float thoiGianGianCachBaoDongLuuDan = 0.25f;

    [Header("Gizmos")]
    [SerializeField] private bool hienThiGizmos = true;

    private float boDemGianCach = 0f;
    private float boDemGianCachLuuDan = 0f;

    private void Update()
    {
        if (boDemGianCach > 0f)
        {
            boDemGianCach -= Time.deltaTime;
        }

        if (boDemGianCachLuuDan > 0f)
        {
            boDemGianCachLuuDan -= Time.deltaTime;
        }

        if (batQuetLuuDan)
        {
            QuetVaPhatBaoDongLuuDan();
        }
    }

    public void PhatBaoDong(Transform mucTieuPlayer, Vector3 viTriPlayer)
    {
        if (boDemGianCach > 0f) return;
        boDemGianCach = Mathf.Max(0.05f, thoiGianGianCachBaoDong);

        float banKinh = Mathf.Max(0.5f, banKinhBaoDong);
        LayerMask mask = layerDongMinh.value != 0 ? layerDongMinh.value : ~0;

        Collider[] cacVaCham = Physics.OverlapSphere(transform.position, banKinh, mask, QueryTriggerInteraction.Collide);
        if (cacVaCham == null || cacVaCham.Length == 0) return;

        for (int i = 0; i < cacVaCham.Length; i++)
        {
            Collider col = cacVaCham[i];
            if (col == null) continue;
            if (col.transform.root == transform.root) continue;

            RangedEnemyAI rangedAlly = col.GetComponentInParent<RangedEnemyAI>();
            if (rangedAlly != null)
            {
                rangedAlly.NhanBaoDong(mucTieuPlayer, viTriPlayer);
            }

            MeleeEnemyAI meleeAlly = col.GetComponentInParent<MeleeEnemyAI>();
            if (meleeAlly != null)
            {
                meleeAlly.NhanBaoDong(mucTieuPlayer, viTriPlayer);
            }
        }
    }

    public void PhatBaoDongLuuDan(Vector3 viTriLuuDan)
    {
        if (boDemGianCachLuuDan > 0f) return;
        boDemGianCachLuuDan = Mathf.Max(0.05f, thoiGianGianCachBaoDongLuuDan);

        float banKinh = Mathf.Max(0.5f, banKinhBaoDong);
        LayerMask mask = layerDongMinh.value != 0 ? layerDongMinh.value : ~0;

        Collider[] cacVaCham = Physics.OverlapSphere(transform.position, banKinh, mask, QueryTriggerInteraction.Collide);
        if (cacVaCham == null || cacVaCham.Length == 0) return;

        for (int i = 0; i < cacVaCham.Length; i++)
        {
            Collider col = cacVaCham[i];
            if (col == null) continue;
            if (col.transform.root == transform.root) continue;

            RangedEnemyAI rangedAlly = col.GetComponentInParent<RangedEnemyAI>();
            if (rangedAlly != null)
            {
                rangedAlly.NhanBaoDongLuuDan(viTriLuuDan);
            }

            MeleeEnemyAI meleeAlly = col.GetComponentInParent<MeleeEnemyAI>();
            if (meleeAlly != null)
            {
                meleeAlly.NhanBaoDongLuuDan(viTriLuuDan);
            }
        }
    }

    private void QuetVaPhatBaoDongLuuDan()
    {
        if (boDemGianCachLuuDan > 0f) return;

        float banKinhQuet = Mathf.Max(0.5f, banKinhQuetLuuDan);
        LayerMask mask = layerLuuDan.value != 0 ? layerLuuDan.value : ~0;

        Collider[] cacVatThe = Physics.OverlapSphere(transform.position, banKinhQuet, mask, QueryTriggerInteraction.Collide);
        if (cacVatThe == null || cacVatThe.Length == 0) return;

        float khoangCachGanNhat = float.MaxValue;
        Vector3 viTriLuuDanGanNhat = Vector3.zero;
        bool timThayLuuDan = false;

        for (int i = 0; i < cacVatThe.Length; i++)
        {
            Collider col = cacVatThe[i];
            if (col == null) continue;
            if (col.transform.root == transform.root) continue;

            if (LaLuuDan(col.gameObject))
            {
                float khoangCach = Vector3.Distance(transform.position, col.bounds.center);
                if (khoangCach < khoangCachGanNhat)
                {
                    khoangCachGanNhat = khoangCach;
                    viTriLuuDanGanNhat = col.bounds.center;
                    timThayLuuDan = true;
                }
            }
        }

        if (timThayLuuDan)
        {
            PhatBaoDongLuuDan(viTriLuuDanGanNhat);
        }
    }

    private bool LaLuuDan(GameObject obj)
    {
        if (obj == null) return false;

        if (!string.IsNullOrEmpty(tagLuuDan) && tagLuuDan != "Untagged")
        {
            Transform hienTai = obj.transform;
            while (hienTai != null)
            {
                if (hienTai.CompareTag(tagLuuDan)) return true;
                hienTai = hienTai.parent;
            }
        }

        if (!string.IsNullOrEmpty(tuKhoaTenLuuDan))
        {
            string tenLower = obj.transform.root.name.ToLowerInvariant();
            string tuKhoaLower = tuKhoaTenLuuDan.ToLowerInvariant();
            if (tenLower.Contains(tuKhoaLower) || obj.name.ToLowerInvariant().Contains(tuKhoaLower))
            {
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!hienThiGizmos) return;

        Gizmos.color = new Color(1f, 0.55f, 0.05f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, banKinhBaoDong);

        if (batQuetLuuDan)
        {
            Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, banKinhQuetLuuDan);
        }
    }
}
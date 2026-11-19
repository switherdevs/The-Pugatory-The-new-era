using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))] // Đảm bảo luôn có Rigidbody để nhận diện vật lý
public class AmmoBox : MonoBehaviour
{
    [Header("Ammo Settings")]
    [Tooltip("Số lượng đạn được cộng thêm khi nhặt hộp đạn.")]
    [SerializeField] private int ammoAmount = 30;
    [SerializeField] private string playerTag = "Player";

    private void Awake()
    {
        // Đảm bảo code tự động bật Is Trigger cho Collider để nhặt đồ
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // Đảm bảo Rigidbody không bị kinematic để nó rơi đúng trọng lực
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Kiểm tra xem đối tượng va chạm có phải Player hoặc chứa các thành phần vũ khí không
        if (other.CompareTag(playerTag) ||
            other.GetComponentInChildren<WeaponSwitcher>() != null ||
            other.GetComponentInChildren<SniperWeapon>() != null ||
            other.GetComponentInChildren<WeaponSystem>() != null)
        {
            bool ammoAdded = false;

            // 1. Ưu tiên kiểm tra qua WeaponSwitcher (nếu người chơi đang dùng hệ thống đổi súng chung)
            WeaponSwitcher switcher = other.GetComponentInChildren<WeaponSwitcher>();
            if (switcher != null)
            {
                // Nếu đang cầm súng tỉa -> Cộng đạn vào súng tỉa
                if (switcher.CurrentSniperWeapon != null)
                {
                    switcher.CurrentSniperWeapon.AddReserveAmmo(ammoAmount);
                    ammoAdded = true;
                }
                // Nếu đang cầm súng thường -> Cộng đạn vào súng thường
                else if (switcher.CurrentWeaponSystem != null)
                {
                    switcher.CurrentWeaponSystem.AddReserveAmmo(ammoAmount);
                    ammoAdded = true;
                }
            }

            // 2. Fallback: Nếu không tìm thấy Switcher, kiểm tra trực tiếp các component súng trên người chơi
            if (!ammoAdded)
            {
                SniperWeapon sniper = other.GetComponentInChildren<SniperWeapon>();
                if (sniper != null)
                {
                    sniper.AddReserveAmmo(ammoAmount);
                    ammoAdded = true;
                }
                else
                {
                    WeaponSystem weapon = other.GetComponentInChildren<WeaponSystem>();
                    if (weapon != null)
                    {
                        weapon.AddReserveAmmo(ammoAmount);
                        ammoAdded = true;
                    }
                }
            }

            // Nếu cộng đạn thành công thì hủy hộp đạn trong Scene
            if (ammoAdded)
            {
                Destroy(gameObject);
            }
        }
    }
}

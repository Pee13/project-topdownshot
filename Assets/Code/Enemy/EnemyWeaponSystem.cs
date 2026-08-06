using UnityEngine;

/// <summary>
/// ระบบอาวุธของศัตรู แยกออกมาจาก EnemyAI เพื่อให้ปรับแต่งปืนแต่ละตัวได้อิสระ
/// (กระสุน, อัตรายิง, แม็กกาซีน, การรีโหลด) โดยไม่ต้องแก้ไฟล์ EnemyAI.cs โดยตรง
///
/// EnemyAI.cs จะ auto-find component นี้เองใน Start() ผ่าน GetComponent&lt;EnemyWeaponSystem&gt;()
/// ถ้าไม่มีคอมโพเนนต์นี้ติดอยู่ EnemyAI จะ fallback ไปใช้ระบบยิงแบบ Legacy (bulletPrefab/firePoint ของตัวเอง) แทน
///
/// วิธีติดตั้ง: แปะสคริปต์นี้ไว้ที่ GameObject เดียวกับ EnemyAI แล้วตั้งค่า Bullet Prefab / Fire Point เอง
/// </summary>
[RequireComponent(typeof(EnemyAI))]
public class EnemyWeaponSystem : MonoBehaviour
{
    [Header("── Bullet ──")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float bulletSpeed = 400f;
    public int damage = 10;
    [Tooltip("มุมสุ่มคลาดเคลื่อนตอนเล็ง (องศา) ยิ่งเยอะยิ่งแม่นน้อยลง")]
    [Range(0f, 30f)] public float aimInaccuracy = 8f;
    [Tooltip("สัดส่วนเวลาที่ใช้เดาตำแหน่งล่วงหน้าจากความเร็วเป้าหมาย (0 = ไม่เดาเลย ยิงตรงตำแหน่งปัจจุบัน)")]
    public float predictionTime = 0.2f;

    [Header("── Fire Rate ──")]
    [Tooltip("เวลาหน่วงระหว่างนัด (วินาที)")]
    public float fireCooldown = 0.35f;

    [Header("── Magazine / Reload ──")]
    public int magazineSize = 12;
    public float reloadDuration = 1.5f;

    /// <summary>กำลังรีโหลดอยู่ตอนนี้หรือไม่ — EnemyAI.cs อ่านค่านี้ไปใช้กับ Animator ("IsReloading")</summary>
    public bool IsReloading { get; private set; }

    public int CurrentAmmo { get; private set; }

    private float _fireTimer;
    private float _reloadTimer;

    private void Awake()
    {
        CurrentAmmo = magazineSize;
    }

    private void Update()
    {
        if (_fireTimer > 0f)
            _fireTimer -= Time.deltaTime;

        if (IsReloading)
        {
            _reloadTimer -= Time.deltaTime;
            if (_reloadTimer <= 0f)
            {
                CurrentAmmo = magazineSize;
                IsReloading = false;
            }
        }
    }

    /// <summary>
    /// พยายามยิงไปยังตำแหน่งเป้าหมาย โดยเดาตำแหน่งล่วงหน้าจากความเร็วเป้าหมาย (targetVelocity)
    /// คืนค่า true ถ้ายิงสำเร็จจริง, false ถ้ายิงไม่ได้ (ติดคูลดาวน์/กำลังรีโหลด/ไม่มี Prefab)
    /// </summary>
    public bool TryShoot(Vector3 targetPosition, Vector2 targetVelocity)
    {
        if (IsReloading) return false;
        if (_fireTimer > 0f) return false;

        if (CurrentAmmo <= 0)
        {
            StartReload();
            return false;
        }

        if (bulletPrefab == null || firePoint == null) return false;

        Vector2 predicted = PredictTarget(targetPosition, targetVelocity);
        Vector2 dir = (predicted - (Vector2)firePoint.position).normalized;
        dir = Quaternion.Euler(0, 0, Random.Range(-aimInaccuracy, aimInaccuracy)) * dir;

        GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        if (bulletObj.TryGetComponent(out Rigidbody2D rb))
            rb.linearVelocity = dir * bulletSpeed;
        bulletObj.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);

        CurrentAmmo--;
        _fireTimer = fireCooldown;

        if (CurrentAmmo <= 0)
            StartReload();

        return true;
    }

    private void StartReload()
    {
        IsReloading = true;
        _reloadTimer = reloadDuration;
    }

    private Vector2 PredictTarget(Vector3 targetPosition, Vector2 targetVelocity)
    {
        float dist = Vector2.Distance(firePoint.position, targetPosition);
        float travelTime = dist / Mathf.Max(bulletSpeed, 1f);
        return (Vector2)targetPosition + targetVelocity * travelTime * predictionTime;
    }
}

using UnityEngine;
using TopDownTacticalAI.Tactical;

namespace TopDownTacticalAI.Player
{
    /// <summary>
    /// ควบคุมการเล็ง (หันปืนเข้าหาเมาส์) และยิงกระสุนของผู้เล่น
    /// กระสุนที่ยิงต้องอยู่บน Layer ที่ตรงกับ PlayerBulletMask ใน EnemyBrain
    /// ทุกนัดที่ยิงจะส่งเสียงดัง (Sound Investigation) ให้ศัตรูในระยะได้ยินแม้จะไม่เห็นตัวก็ตาม
    ///
    /// ถ้ามี PlayerMana ติดอยู่ที่ตัวเดียวกัน การยิงจะใช้ Mana ทุกนัด (ตามสเปคเอกสารบทที่ 3.3.1.1)
    /// ยิงไม่ได้ถ้า Mana ไม่พอ — ถ้าไม่มี PlayerMana ติดอยู่เลย จะยิงได้อิสระแบบเดิม (ไม่บังคับต้องมี)
    /// </summary>
    public class PlayerShoot : MonoBehaviour
    {
        public GameObject BulletPrefab;
        public Transform MuzzlePoint;
        public float FireRate = 4f;
        public float BulletSpeed = 14f;

        [Tooltip("ระยะที่เสียงปืนไปถึง (ดังกว่าเสียงเดิน/วิ่งมาก)")]
        public float GunshotNoiseRadius = 14f;

        private Camera _cam;
        private float _cooldown;
        private PlayerMana _mana;

        private void Awake()
        {
            _cam = Camera.main;
            _mana = GetComponent<PlayerMana>(); // ไม่บังคับต้องมี ถ้าไม่มีจะยิงได้อิสระเหมือนเดิม
        }

        private void Update()
        {
            AimAtMouse();

            if (_cooldown > 0f)
                _cooldown -= Time.deltaTime;

            bool hasEnoughMana = _mana == null || _mana.HasEnoughMana();

            if (Input.GetMouseButton(0) && _cooldown <= 0f && hasEnoughMana)
            {
                Shoot();
                _cooldown = 1f / FireRate;
            }
        }

        private void AimAtMouse()
        {
            if (_cam == null) return;

            Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 direction = ((Vector2)mouseWorld - (Vector2)transform.position).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        private void Shoot()
        {
            if (BulletPrefab == null || MuzzlePoint == null) return;

            GameObject bulletObj = Instantiate(BulletPrefab, MuzzlePoint.position, MuzzlePoint.rotation);
            if (bulletObj.TryGetComponent(out Rigidbody2D rb))
            {
                rb.linearVelocity = MuzzlePoint.up * BulletSpeed;
            }

            _mana?.ConsumeForShot();

            // เสียงปืนดังไปไกล ศัตรูที่อยู่ในระยะจะได้ยินแล้วมาตรวจสอบ แม้จะยังไม่เห็นตัวผู้เล่นเลยก็ตาม
            GroupAI.Instance?.EmitNoise(transform.position, GunshotNoiseRadius);
        }
    }
}

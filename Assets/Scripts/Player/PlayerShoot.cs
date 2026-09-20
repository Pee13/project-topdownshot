using UnityEngine;
using TopDownTacticalAI.Tactical;

namespace TopDownTacticalAI.Player
{
    public class PlayerShoot : MonoBehaviour
    {
        [Header("Bullet Setup")]
        public GameObject BulletPrefab;
        public Transform MuzzlePoint;
        public float FireRate = 4f;
        public float BulletSpeed = 180f;

        [Header("Aim Settings")]
        [Tooltip("สำหรับโมเดลหันขวา ให้ตั้งค่าเป็น 0")]
        public float AngleOffset = 0f;

        [Header("Sound")]
        public float GunshotNoiseRadius = 14f;

        private Camera _cam;
        private float _cooldown;
        private PlayerMana _mana;

        private void Awake()
        {
            _cam = Camera.main;
            _mana = GetComponent<PlayerMana>();
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
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            Vector3 mousePos = Input.mousePosition;
            mousePos.z = -_cam.transform.position.z;
            Vector3 mouseWorld = _cam.ScreenToWorldPoint(mousePos);

            Vector2 direction = ((Vector2)mouseWorld - (Vector2)transform.position).normalized;
            
            // หมุนแกน Right เข้าหาเมาส์โดยตรง
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + AngleOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // 🟢 เส้นเขียว: หน้าตัวละครจริง
            Debug.DrawRay(transform.position, transform.right * 8f, Color.green);
            // 🔴 เส้นแดง: ตำแหน่งเมาส์
            Debug.DrawLine(transform.position, mouseWorld, Color.red);
        }

        private void Shoot()
        {
            if (BulletPrefab == null || MuzzlePoint == null) return;

            GameObject bulletObj = Instantiate(BulletPrefab, MuzzlePoint.position, MuzzlePoint.rotation);
            
            // สั่งให้กระสุนพุ่งไปข้างหน้าตามแกน Right (ไม่ใช่ Up แล้ว)
            if (bulletObj.TryGetComponent(out Rigidbody2D rb))
            {
                rb.linearVelocity = MuzzlePoint.right * BulletSpeed;
            }

            _mana?.ConsumeForShot();
            GroupAI.Instance?.EmitNoise(transform.position, GunshotNoiseRadius);
        }
    }
}
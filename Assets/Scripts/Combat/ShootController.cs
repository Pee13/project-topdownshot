using UnityEngine;

namespace TopDownTacticalAI.Combat
{
    /// <summary>
    /// จัดการจังหวะการยิง (Fire Rate) และการสร้างกระสุน
    /// </summary>
    public class ShootController
    {
        private readonly float _fireRate; // นัดต่อวินาที
        private float _cooldown;

        public GameObject BulletPrefab;
        public Transform MuzzlePoint;

        public ShootController(float fireRate, GameObject bulletPrefab, Transform muzzlePoint)
        {
            _fireRate = fireRate;
            BulletPrefab = bulletPrefab;
            MuzzlePoint = muzzlePoint;
        }

        public bool CanShoot => _cooldown <= 0f;

        public void Tick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown -= deltaTime;
        }

        public void Shoot(Vector2 direction)
        {
            if (!CanShoot || BulletPrefab == null || MuzzlePoint == null) return;

            // หมุนกระสุนให้ "ด้านบน" ของมันชี้ไปทางเป้า (Bullet.cs พุ่งตาม transform.up)
            Vector2 dir = direction.normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            GameObject bulletObj = Object.Instantiate(BulletPrefab, MuzzlePoint.position, Quaternion.Euler(0f, 0f, angle));

            // บังคับเป็นกระสุนศัตรู: โดนผู้เล่นได้ ไม่โดนศัตรูด้วยกัน
            if (bulletObj.TryGetComponent(out TopDownTacticalAI.Player.Bullet bullet))
                bullet.IsEnemyBullet = true;

            _cooldown = 1f / _fireRate;
        }
    }
}

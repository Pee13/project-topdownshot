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

            GameObject bulletObj = Object.Instantiate(BulletPrefab, MuzzlePoint.position, Quaternion.identity);
            if (bulletObj.TryGetComponent(out Rigidbody2D rb))
            {
                rb.linearVelocity = direction.normalized * 12f;
            }

            _cooldown = 1f / _fireRate;
        }
    }
}

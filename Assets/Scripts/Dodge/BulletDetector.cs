using UnityEngine;

namespace TopDownTacticalAI.Dodge
{
    /// <summary>
    /// ตรวจจับกระสุนของผู้เล่นที่กำลังพุ่งเข้าหาศัตรูในระยะใกล้
    /// ใช้ OverlapCircle หา Collider ที่ติด Tag "PlayerBullet"
    /// </summary>
    public static class BulletDetector
    {
        public static bool IsBulletIncoming(Vector2 origin, float detectRadius, LayerMask bulletMask, out Vector2 bulletVelocity, out Vector2 bulletPosition)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, detectRadius, bulletMask);

            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out Rigidbody2D rb))
                {
                    Vector2 toSelf = origin - (Vector2)hit.transform.position;
                    // เช็คว่ากระสุนกำลังพุ่งเข้าหาศัตรูจริงๆ (ทิศทางความเร็วชี้เข้าหา)
                    if (Vector2.Dot(rb.linearVelocity.normalized, toSelf.normalized) > 0.5f)
                    {
                        bulletVelocity = rb.linearVelocity;
                        bulletPosition = hit.transform.position;
                        return true;
                    }
                }
            }

            bulletVelocity = Vector2.zero;
            bulletPosition = Vector2.zero;
            return false;
        }
    }
}

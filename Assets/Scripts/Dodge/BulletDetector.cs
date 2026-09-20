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

        // ─────────────────────────────────────────────────────────────────
        // Projectile Prediction (§17)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>ตำแหน่งกระสุนที่คาดว่าจะอยู่ในอีก timeAhead วินาทีข้างหน้า</summary>
        public static Vector2 PredictBulletPosition(Vector2 bulletPosition, Vector2 bulletVelocity, float timeAhead)
        {
            return bulletPosition + bulletVelocity * timeAhead;
        }

        /// <summary>
        /// เวลา (วินาที) ที่กระสุนจะวิ่งมาถึงจุด "ใกล้ตัวเราที่สุด"
        /// ถ้ากระสุนกำลังเคลื่อนออกห่าง คืนค่า 0
        /// </summary>
        public static float TimeToClosestApproach(Vector2 origin, Vector2 bulletPosition, Vector2 bulletVelocity)
        {
            if (bulletVelocity.sqrMagnitude < 0.0001f) return 0f;

            // อนุพันธ์ของ |origin - (bulletPos + vel*t)| = 0 → t ที่ระยะน้อยที่สุด
            float t = Vector2.Dot(origin - bulletPosition, bulletVelocity) / bulletVelocity.sqrMagnitude;
            return Mathf.Max(0f, t);
        }

        /// <summary>
        /// ระยะตั้งฉากที่สั้นที่สุดจาก "ตัวเรา" ถึงแนววิถีของกระสุน
        /// ค่าน้อย = กระสุนกำลังพุ่งเข้าใส่พอดี, ค่ามาก = กระสุนพุ่งผ่านไปไกล
        /// ต่างจากการรอให้กระสุน "แตะตัว" แล้วค่อยหลบ (§17 — ต้องทำนายล่วงหน้า)
        /// </summary>
        public static float ClosestApproachDistance(Vector2 origin, Vector2 bulletPosition, Vector2 bulletVelocity)
        {
            if (bulletVelocity.sqrMagnitude < 0.0001f)
                return Vector2.Distance(origin, bulletPosition);

            float t = TimeToClosestApproach(origin, bulletPosition, bulletVelocity);
            Vector2 closest = PredictBulletPosition(bulletPosition, bulletVelocity, t);
            return Vector2.Distance(origin, closest);
        }

        /// <summary>
        /// กระสุนจะโดนตัวเราจริงหรือไม่ (ไม่ใช่แค่ "กำลังบินมาแถวๆ นั้น")
        ///
        /// เกณฑ์สองข้อพร้อมกัน:
        ///   1) แนววิถีของกระสุนต้องพาดผ่านใกล้ตัวเราไม่เกินรัศมีตัว + ระยะกันชน
        ///   2) ต้องมาถึงภายในเวลา horizonSeconds (กระสุนที่จะพุ่งผ่านหลังจากนานมากไม่ต้องสน)
        /// ใช้แทนการทอยสุ่มว่าจะหลบหรือไม่ (§18 — การตัดสินใจต้องมาจากสถานการณ์)
        /// </summary>
        public static bool WillBulletHit(
            Vector2 origin,
            float bodyRadius,
            Vector2 bulletPosition,
            Vector2 bulletVelocity,
            float horizonSeconds = 0.6f,
            float safetyMargin = 0.15f)
        {
            if (bulletVelocity.sqrMagnitude < 0.0001f) return false;

            // 1) แนววิถีต้องพาดผ่านใกล้ตัว
            float closestDistance = ClosestApproachDistance(origin, bulletPosition, bulletVelocity);
            if (closestDistance > bodyRadius + safetyMargin) return false;

            // 2) ต้องมาถึงในเวลาที่รับได้
            float timeToClosest = TimeToClosestApproach(origin, bulletPosition, bulletVelocity);
            if (timeToClosest > horizonSeconds) return false;

            return true;
        }
    }
}

using UnityEngine;

namespace TopDownTacticalAI.Dodge
{
    /// <summary>
    /// หาทิศ/ตำแหน่งปลอดภัยสำหรับหลบกระสุน
    ///
    /// เดิมเลือกซ้าย/ขวาด้วย <c>Random.value &gt; 0.5f</c> ซึ่งทำให้ AI หลบไปมา
    /// ไร้เหตุผล (§18 — การตัดสินใจต้องมาจากคะแนน ไม่ใช่สุ่ม)
    ///
    /// ตอนนี้ย้ายการตัดสินใจไปที่ <see cref="DodgeDirectionScorer"/> ซึ่งให้คะแนนทิศหลบ
    /// จากการทำนายแนวกระสุน + กำแพง + เพื่อน + ผู้เล่น + ที่กำบัง
    ///
    /// คลาสนี้เหลือไว้เพื่อคง API เดิมไว้ให้โค้ดอื่นเรียกได้ โดย delegate ไปที่ scorer
    /// </summary>
    public static class SafePositionFinder
    {
        /// <summary>
        /// ทิศหลบแบบมีข้อมูลครบ (แนะนำ) — ให้คะแนนจาก DodgeDirectionScorer
        /// </summary>
        /// <returns>ทิศหลบที่ดีที่สุด (normalized) หรือ Vector2.zero ถ้าหลบทางไหนก็ไม่ปลอดภัย</returns>
        public static Vector2 GetDodgeDirection(
            Vector2 origin,
            Vector2 bulletPosition,
            Vector2 bulletVelocity,
            float dodgeDistance,
            LayerMask obstacleMask,
            Vector2 playerPosition)
        {
            bool found = DodgeDirectionScorer.TryFindBestDodgeDirection(
                origin, bulletPosition, bulletVelocity, dodgeDistance,
                obstacleMask, playerPosition,
                out Vector2 direction, out float score);

            return found ? direction : Vector2.zero;
        }

        /// <summary>
        /// API เดิม — คงไว้เพื่อความเข้ากันได้ แต่ไม่มีบริบท (ตำแหน่งตัว/ผู้เล่น/กำแพง)
        /// จึงเลือกได้แค่ "ทิศตั้งฉากที่ไม่ทำให้เราวิ่งเข้าหากระสุน" แบบ deterministic
        /// ไม่สุ่มอีกต่อไป
        /// </summary>
        public static Vector2 GetDodgeDirection(Vector2 bulletVelocity)
        {
            if (bulletVelocity.sqrMagnitude < 0.0001f) return Vector2.zero;

            Vector2 perpendicular = new Vector2(-bulletVelocity.y, bulletVelocity.x).normalized;

            // เลือกทิศที่ "วิ่งหนีกระสุน" มากกว่า — คือทิศที่องค์ประกอบตามแนวกระสุนเป็นลบ
            // (วิ่งออกด้านข้าง + ถอยหลังเล็กน้อย ปลอดภัยกว่าวิ่งออกด้านข้างแต่ฝืนไปข้างหน้า)
            Vector2 backStep = -bulletVelocity.normalized;
            Vector2 left = (perpendicular + backStep * 0.35f).normalized;
            Vector2 right = (-perpendicular + backStep * 0.35f).normalized;

            // สองทิศนี้ดีเท่ากันตามคำนวณ (สมมาตร) จึงใช้สุ่มตัดสินได้ตาม §41 — เป็น tie-breaker เท่านั้น
            return Random.value > 0.5f ? left : right;
        }
    }
}

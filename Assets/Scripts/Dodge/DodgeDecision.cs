using UnityEngine;

namespace TopDownTacticalAI.Dodge
{
    /// <summary>
    /// ตัดสินใจว่าควรหลบหรือไม่
    ///
    /// เดิมใช้การทอยสุ่ม 70% ซึ่งขัดกับหลัก "การตัดสินใจต้องมาจากสถานการณ์ ไม่ใช่สุ่ม"
    /// ตอนนี้เปลี่ยนเป็น deterministic: หลบก็ต่อเมื่อ **การทำนายบอกว่ากระสุนจะโดนจริง**
    ///
    /// ผลข้างเคียงที่ดี: AI จะไม่หลบเลยถ้ากระสุนพุ่งผ่านไปไกล (ลดการหลบฟุ่มเฟือย)
    /// และจะหลบทุกครั้งที่กระสุนจะโดนจริง (ไม่มีโอกาส "เผลอไม่หลบ" อีก)
    /// </summary>
    public static class DodgeDecision
    {
        /// <summary>
        /// ควรหลบหรือไม่ — แบบทำนายล่วงหน้า (แนะนำ)
        /// </summary>
        /// <param name="bulletIncoming">มีกระสุนพุ่งเข้าหาอยู่หรือไม่ (จาก BulletDetector)</param>
        /// <param name="origin">ตำแหน่งตัวเอง</param>
        /// <param name="bulletPosition">ตำแหน่งกระสุนปัจจุบัน</param>
        /// <param name="bulletVelocity">ความเร็วกระสุน</param>
        /// <param name="bodyRadius">รัศมีตัวเอง</param>
        /// <param name="horizonSeconds">กระสุนต้องจะมาถึงภายในกี่วินาทีถึงจะหลบ</param>
        public static bool ShouldDodge(
            bool bulletIncoming,
            Vector2 origin,
            Vector2 bulletPosition,
            Vector2 bulletVelocity,
            float bodyRadius = 0.5f,
            float horizonSeconds = 0.6f)
        {
            if (!bulletIncoming) return false;
            return BulletDetector.WillBulletHit(origin, bodyRadius, bulletPosition, bulletVelocity, horizonSeconds);
        }
    }
}

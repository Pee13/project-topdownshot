using UnityEngine;

namespace TopDownTacticalAI.Vision
{
    /// <summary>
    /// ยิง Raycast เพื่อตรวจสอบว่ามีสิ่งกีดขวาง (กำแพง) บดบังระหว่างศัตรูกับเป้าหมายหรือไม่
    /// </summary>
    public static class RaycastDetector
    {
        public static bool HasLineOfSight(Vector2 origin, Vector2 targetPosition, LayerMask obstacleMask, float maxDistance)
        {
            Vector2 direction = (targetPosition - origin).normalized;
            float distance = Vector2.Distance(origin, targetPosition);

            if (distance > maxDistance)
                return false;

            RaycastHit2D hit = Physics2D.Raycast(origin, direction, distance, obstacleMask);
            // ถ้าไม่โดนอะไรเลย (hit.collider == null) แปลว่าไม่มีสิ่งกีดขวาง มองเห็นได้
            return hit.collider == null;
        }

        // buffer กลาง (ไม่ allocate ทุกครั้งที่เช็ค — single-threaded ใช้แล้วจบในรอบเรียก)
        private static readonly RaycastHit2D[] s_losHits = new RaycastHit2D[8];

        /// <summary>
        /// LOS แบบ "ไม่นับ collider ของตัวเองและของเป้าหมาย" — ใช้ตอนฮีล/ให้คะแนนเป้าฮีล
        ///
        /// ปัญหาที่เจอจริง: รังสีจาก healer ไปยังเพื่อนเป้าหมาย ถ้าชน collider ของตัวละคร
        /// (ตัวเองหรือเพื่อน) ที่ดันอยู่บน Layer ObstacleMask ระบบจะคิดว่า "มีสิ่งกีดขวางบัง"
        /// → เดินถึงตัวเพื่อนแล้วแต่ไม่ยอมฮีลเสียที (ยืนชิดกันแต่ LOS = false ตลอด)
        /// โดยการเช็คแบบนี้จะ "ข้าม" collider สองตัวนี้ เหลือแค่กำแพงจริง ๆ ที่มีสิทธิ์บัง
        /// </summary>
        public static bool HasLineOfSightIgnoring(
            Vector2 origin, Vector2 targetPosition, LayerMask obstacleMask, float maxDistance,
            Collider2D ignoreA, Collider2D ignoreB)
        {
            Vector2 direction = targetPosition - origin;
            float distance = direction.magnitude;
            if (distance < 0.0001f) return true;
            if (distance > maxDistance) return false;

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = obstacleMask,
                useTriggers = false
            };

            int count = Physics2D.Raycast(origin, direction / distance, filter, s_losHits, distance);
            for (int i = 0; i < count; i++)
            {
                Collider2D col = s_losHits[i].collider;
                if (col == null || col == ignoreA || col == ignoreB) continue;
                return false; // มีสิ่งกีดขวางจริง (กำแพง) บัง
            }

            return true;
        }
    }
}

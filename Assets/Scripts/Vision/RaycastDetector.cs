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
    }
}

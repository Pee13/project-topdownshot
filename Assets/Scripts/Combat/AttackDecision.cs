using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Vision;

namespace TopDownTacticalAI.Combat
{
    /// <summary>
    /// ตัดสินใจว่าควรยิงตอนนี้หรือไม่ โดยพิจารณาระยะ, กระสุน, สถานะรีโหลด, การเล็ง
    /// และที่สำคัญที่สุดคือ "ต้องมองเห็นผู้เล่นจริงๆ" (Line of Sight) ก่อนเสมอ
    /// ป้องกันไม่ให้ AI ยิงทะลุกำแพงหรือยิงได้แค่เพราะอยู่ในระยะ/เลเยอร์ตรงกันเฉยๆ
    /// </summary>
    public static class AttackDecision
    {
        public static bool ShouldFire(Blackboard blackboard, Vector2 selfPosition, Vector2 targetPosition, LayerMask obstacleMask, float maxRange, bool isAimed)
        {
            if (blackboard.IsReloading) return false;
            if (blackboard.CurrentAmmo <= 0) return false;

            // ต้องเห็นผู้เล่นจริงตามระบบ Vision (มุมมอง + ไม่มีอะไรบัง) ก่อนถึงจะพิจารณายิงต่อ
            if (!blackboard.CanSeeTarget) return false;

            float distance = Vector2.Distance(selfPosition, targetPosition);
            if (distance > maxRange) return false;
            if (!isAimed) return false;

            // เช็ค Line of Sight ซ้ำอีกครั้ง ณ จังหวะยิงจริง (กันกรณีผู้เล่นเพิ่งหลบเข้าที่กำบังในเฟรมเดียวกัน)
            if (!RaycastDetector.HasLineOfSight(selfPosition, targetPosition, obstacleMask, maxRange))
                return false;

            return true;
        }
    }
}

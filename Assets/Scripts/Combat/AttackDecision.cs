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

            // หมายเหตุ: เดิมยิง raycast เช็ค LOS ซ้ำที่นี่ แต่ถูกตัดออก —
            // `CanSeeTarget` ข้างบนถูก VisionSensor ตั้งค่าในเฟรมเดียวกันโดยยิง LOS ไปแล้ว
            // (RaycastDetector.HasLineOfSight) การเช็คซ้ำจึงเป็นค่าใช้จ่ายเปล่า ๆ ทุกเฟรม × 6 states
            // ข้อจำกัด: ถ้าตั้ง ScoringWeights.visionInterval > 0.1 ควรมีค่า LOS เก่าถึง 0.1 วิ
            // — แนะนำให้คง visionInterval ≤ 0.1 เพื่อความแม่นยำของการยิง

            return true;
        }
    }
}

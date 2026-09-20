using UnityEngine;
using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.Vision
{
    /// <summary>
    /// ระบบการมองเห็นหลักของศัตรู
    /// รวม TargetDetector + FieldOfView + RaycastDetector เข้าด้วยกัน
    /// แล้วอัปเดตผลลง Blackboard ทุกเฟรม
    /// </summary>
    public class VisionSensor : MonoBehaviour
    {
        [Header("Vision Settings")]
        public float ViewRadius = 8f;
        [Range(0, 360)] public float ViewAngle = 90f;
        public LayerMask TargetMask;
        public LayerMask ObstacleMask;

        [Header("Reference")]
        public Transform EyePoint;

        [Header("Field of View")]
        [Tooltip("เปิดไว้ = ใช้มุมมองกว้างแบบเดิม (รวมกรวยด้าน up และด้าน right เข้าด้วยกัน " +
                 "ทำให้มุมที่มองเห็นจริงเป็นราว 2 เท่าของค่า View Angle)\n" +
                 "ปิด = ใช้กรวยเดียวตามค่า View Angle ตรงๆ (สมจริงกว่า แต่พื้นที่ตรวจจับจะแคบลง)")]
        public bool LegacyWideVision = false;

        private Blackboard _blackboard;

        public void Initialize(Blackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public void Tick()
        {
            Vector2 origin = EyePoint != null ? (Vector2)EyePoint.position : (Vector2)transform.position;
            Transform target = TargetDetector.FindTargetInRadius(origin, ViewRadius, TargetMask);

            // ไม่มีเป้าหมายในรัศมีเลย: ต้องเคลียร์ค่าทั้งหมด ไม่ปล่อยให้ค่าเก่าค้าง
            // ไม่งั้น AI จะตัดสินใจจากข้อมูลที่ตัวเองไม่ได้มองเห็นแล้ว (รู้ทั้งที่ควรไม่รู้)
            if (target == null)
            {
                ClearSight();
                return;
            }

            Vector2 toTarget = (Vector2)target.position - origin;
            float distance = toTarget.magnitude;

            // มุมระหว่างทิศที่หันหน้า (transform.up) กับทิศไปเป้าหมาย
            float angleToTarget = Vector2.Angle(transform.up, toTarget);

            bool inAngle = LegacyWideVision
                ? FieldOfView.IsWithinView(transform.up, toTarget, ViewAngle)
                  || FieldOfView.IsWithinView(transform.right, toTarget, ViewAngle)
                : angleToTarget <= ViewAngle * 0.5f;

            bool hasLoS = RaycastDetector.HasLineOfSight(origin, target.position, ObstacleMask, ViewRadius);

            bool canSee = inAngle && hasLoS;

            _blackboard.CanSeeTarget = canSee;

            if (!canSee)
            {
                // อยู่ในรัศมีแต่ถูกกำแพงบัง หรืออยู่นอกกรวยสายตา = มองไม่เห็น
                ClearSight();
                return;
            }

            _blackboard.DistanceToTarget = distance;
            _blackboard.CurrentTarget = target;
            _blackboard.VisionConfidence = ComputeConfidence(distance, angleToTarget, hasLoS);
        }

        /// <summary>
        /// ความมั่นใจในการมองเห็น (0..1) คำนวณจาก 3 ปัจจัยคูณกัน:
        ///   1) ระยะ — ยิ่งไกลยิ่งไม่มั่นใจ (ที่ขอบรัศมีสายตา = 0)
        ///   2) มุม — ยิ่งอยู่ขอบกรวยสายตา ยิ่งไม่มั่นใจ (กลางกรวย = 1)
        ///   3) สายตาที่ไม่ถูกบัง — ถูกกำแพงบัง = 0
        /// ทำให้ AI แยกได้ว่า "เห็นชัดๆ ตรงหน้า" ต่างจาก "เห็นเงาๆ อยู่ไกลๆ ริมสายตา"
        /// </summary>
        private float ComputeConfidence(float distance, float angleToTarget, bool hasLoS)
        {
            float distanceFactor = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, ViewRadius));

            float halfAngle = Mathf.Max(0.01f, ViewAngle * 0.5f);
            float angleFactor = 1f - Mathf.Clamp01(angleToTarget / halfAngle);

            float losFactor = hasLoS ? 1f : 0f;

            return Mathf.Clamp01(distanceFactor * angleFactor * losFactor);
        }

        /// <summary>เคลียร์ข้อมูลการมองเห็นทั้งหมดเมื่อเป้าหมายหลุดสายตา</summary>
        private void ClearSight()
        {
            _blackboard.CanSeeTarget = false;
            _blackboard.VisionConfidence = 0f;
            _blackboard.DistanceToTarget = 0f;
            _blackboard.CurrentTarget = null;
        }
    }
}

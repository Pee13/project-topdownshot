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

        [Header("Peripheral Vision (ทรงกลมรอบตัว)")]
        [Tooltip("ทรงกลมรอบตัว (360°) รัศมี = ViewRadius × ค่านี้\n" +
                 "• ยังไม่เคยเจอผู้เล่น → รู้สึก 'อ่อนๆ' ว่ามีคนอยู่ข้างหลัง (ไม่เห็นชัด ยิงไม่ได้)\n" +
                 "• เจอแล้ว/ความจำยังสด → ทรงกลมทำงาน 'เต็มประสิทธิภาพ' = รู้ตัวทันทีที่มีคนเข้ามาใกล้รอบตัว")]
        [Range(0.1f, 1f)] public float NearVisionRatio = 0.3f;

        [Header("Reference")]
        public Transform EyePoint;

        // ── Vision Stability: ยืนยัน 2 เฟรมติดกันก่อนเปลี่ยนค่า CanSeeTarget (กัน LOS กระพริบที่ขอบกำแพง) ──
        private const int VisionStableFrames = 2;
        private bool _lastRawSeen;
        private int _stableSeenFrames;

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

            // กรวยสายตาเดียวตามค่า View Angle ตรงๆ (สมจริง)
            bool inAngle = angleToTarget <= ViewAngle * 0.5f;
            bool hasLoS = RaycastDetector.HasLineOfSight(origin, target.position, ObstacleMask, ViewRadius);
            bool coneSeen = inAngle && hasLoS;

            // ── Zone 2: ทรงกลมรอบตัว 360° (รัศมีสั้น = ไม่ไกลมาก ตามแบบที่ขอ) ──
            float nearRadius = ViewRadius * NearVisionRatio;
            bool inCircle = distance <= nearRadius;
            bool circleLos = !coneSeen && inCircle
                && RaycastDetector.HasLineOfSight(origin, target.position, ObstacleMask, distance + 1f);

            // "เจอแล้ว" = ความจำยังสด (เห็นเองเมื่อกี้/เพื่อนแจ้ง/เพิ่งรู้สึกได้) → ทรงกลมทำงานเต็ม
            bool engaged = _blackboard.HasMemory && _blackboard.MemoryConfidence >= 0.45f;
            bool circleFullSeen = engaged && inCircle && circleLos;

            // ── Vision Stability (แก้ LOS กระพริบ) ──
            // ต้องยืนยัน 2 เฟรมติดต่อกันก่อนสลับค่า (รวมทั้งกรวยและทรงกลม)
            bool rawSeen = coneSeen || circleFullSeen;
            if (rawSeen == _lastRawSeen)
            {
                if (_stableSeenFrames < 100) _stableSeenFrames++;
            }
            else
            {
                _lastRawSeen = rawSeen;
                _stableSeenFrames = 0;
            }
            bool canSee = _stableSeenFrames >= VisionStableFrames - 1 ? rawSeen : _blackboard.CanSeeTarget;

            _blackboard.CanSeeTarget = canSee;

            if (!canSee)
            {
                // เพิ่งคลาดสายตาเฟรมนี้ → เริ่มนับเวลาไล่ต่อ (Pursuit Grace)
                if (_blackboard.CurrentTarget != null)
                {
                    _blackboard.PursuitTarget = _blackboard.CurrentTarget;
                    _blackboard.LostSightTime = Time.time;
                }

                ClearSight(); // เคลียร์ก่อน แล้วค่อยตั้ง peripheral ถ้าเข้าเงื่อนไข

                // ── Peripheral (อ่อนๆ): มีคนในทรงกลมรอบตัวแต่ยังเห็นไม่ชัด → รู้สึกตัวแบบอ่อน ──
                if (inCircle && circleLos)
                {
                    _blackboard.PeripheralHit = true;
                    _blackboard.PeripheralPosition = target.position;
                    _blackboard.PeripheralDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.zero;
                }
                return;
            }

            _blackboard.DistanceToTarget = distance;
            _blackboard.CurrentTarget = target;

            float confidence = ComputeConfidence(distance, angleToTarget, hasLoS);
            // เห็นจากทรงกลมรอบตัว (ด้านข้าง/หลัง — ไม่ใช่กรวย) → มั่นใจปานกลาง ไม่เต็มแบบเห็นตรงหน้า
            if (!coneSeen)
                confidence = Mathf.Max(confidence, Mathf.Clamp01(1f - distance / Mathf.Max(0.01f, nearRadius)) * 0.7f);
            _blackboard.VisionConfidence = confidence;
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

        /// <summary>เคลียร์ข้อมูลการมองเห็นทั้งหมดเมื่อเป้าหมายหลุดสายตา (รวม peripheral)</summary>
        private void ClearSight()
        {
            _blackboard.CanSeeTarget = false;
            _blackboard.VisionConfidence = 0f;
            _blackboard.DistanceToTarget = 0f;
            _blackboard.CurrentTarget = null;
            _blackboard.PeripheralHit = false;
            _blackboard.PeripheralDirection = Vector2.zero;
            _blackboard.PeripheralPosition = Vector2.zero;
        }
    }
}
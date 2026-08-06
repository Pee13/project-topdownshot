using UnityEngine;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Patrol
{
    /// <summary>
    /// ลาดตระเวนแบบ "จำพื้นที่ทั้งหมดได้" แทนที่จะสุ่มจุดมั่วๆ ไปเรื่อยๆ (RandomPatrol แบบเดิม)
    /// สร้างจุดตรวจตราล่วงหน้าครั้งเดียวตอนเริ่ม กระจายทั่วพื้นที่รอบจุดกำเนิดแบบสม่ำเสมอ
    /// (ใช้ Golden Angle Spiral เหมือนเมล็ดทานตะวัน ทำให้ครอบคลุมพื้นที่ทั่วถึงโดยไม่ต้องสุ่ม)
    /// แล้วเดินไปทีละจุดตามลำดับ ถ้าจุดไหนไปไม่ถึง (ติดทางตัน) ก็ข้ามไปจุดถัดไปเลย
    /// พอครบทุกจุดแล้ว จะวนกลับไปจุดแรกใหม่ (วนลาดตระเวนไปเรื่อยๆ จนกว่าจะเจอผู้เล่น)
    /// </summary>
    public class CoveragePatrol
    {
        private readonly Vector2[] _points;
        private int _currentIndex;

        private const float GoldenAngle = 137.5077641f; // องศา — ให้จุดกระจายสม่ำเสมอไม่ซ้อนทับกัน

        public CoveragePatrol(Vector2 origin, float radius, LayerMask obstacleMask, int pointCount = 8)
        {
            _points = new Vector2[pointCount];

            for (int i = 0; i < pointCount; i++)
            {
                // กระจายแบบ Spiral: ยิ่งลำดับหลังยิ่งอยู่วงนอก มุมหมุนสม่ำเสมอตาม Golden Angle
                float t = (i + 1) / (float)pointCount;
                float pointRadius = radius * Mathf.Sqrt(t);
                float angle = i * GoldenAngle * Mathf.Deg2Rad;

                Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * pointRadius;

                // ถ้าจุดที่คำนวณได้ดันไปอยู่ในกำแพงพอดี ให้หาจุดใกล้เคียงที่เดินได้แทน
                if (PhysicsUtility.IsPositionBlocked(candidate, 1.2f, obstacleMask))
                {
                    PhysicsUtility.TryFindNearestValidPoint(candidate, radius * 0.2f, obstacleMask, out candidate);
                }

                _points[i] = candidate;
            }
        }

        public Vector2 GetCurrentPoint() => _points[_currentIndex];

        public bool HasReached(Vector2 currentPosition, float threshold = 0.3f)
        {
            return Vector2.Distance(currentPosition, GetCurrentPoint()) <= threshold;
        }

        /// <summary>ไปจุดถัดไปในลำดับ ถ้าครบทุกจุดแล้วจะวนกลับไปจุดแรก (ลาดตระเวนวนไปเรื่อยๆ)</summary>
        public void AdvanceToNext()
        {
            _currentIndex = (_currentIndex + 1) % _points.Length;
        }

        public bool HasPoints => _points != null && _points.Length > 0;
    }
}

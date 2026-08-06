using UnityEngine;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Patrol
{
    /// <summary>
    /// เดินวนตามลำดับ Waypoint ที่กำหนดไว้ล่วงหน้า (ผู้ออกแบบด่านลากวางเองใน Inspector)
    ///
    /// สำคัญ: จุด Waypoint ที่คนวางเองอาจดันไปอยู่ใกล้กำแพงเกินไปโดยไม่ตั้งใจ (ไม่ได้ผ่านการตรวจสอบ
    /// ระยะห่างจากกำแพงเหมือนจุดที่ระบบสุ่มสร้างเอง เช่น CoveragePatrol) ทำให้เดินไปถึงจุดนั้นไม่ได้จริง
    /// เพราะระบบเลี่ยงกำแพง (SteeringMovement) จะคอยผลักออกตลอด กลายเป็นค้างเดินติดกำแพง/หมุนสะบัด
    /// คลาสนี้จึงคำนวณ "ตำแหน่งปลอดภัยจริง" ใกล้ๆ Waypoint แต่ละจุดไว้ล่วงหน้า (แค่ครั้งเดียวต่อจุด ไม่คำนวณทุกเฟรม)
    /// ถ้า Waypoint จุดไหนอยู่ใกล้กำแพงเกินไป จะขยับเป้าหมายจริงออกมาห่างจากกำแพงเล็กน้อยแทน
    /// </summary>
    public class WaypointPatrol
    {
        private readonly Transform[] _waypoints;
        private readonly LayerMask _obstacleMask;
        private readonly Vector2[] _safePositionCache;
        private readonly bool[] _cacheComputed;
        private int _currentIndex;

        public WaypointPatrol(Transform[] waypoints, LayerMask obstacleMask = default)
        {
            _waypoints = waypoints;
            _obstacleMask = obstacleMask;

            int count = waypoints?.Length ?? 0;
            _safePositionCache = new Vector2[count];
            _cacheComputed = new bool[count];
        }

        public Vector2 GetCurrentWaypoint()
        {
            if (_waypoints == null || _waypoints.Length == 0)
                return Vector2.zero;

            // คำนวณตำแหน่งปลอดภัยแค่ครั้งแรกที่เข้าถึงจุดนี้ แล้วเก็บ Cache ไว้ใช้ซ้ำ (ไม่ใช่คำนวณทุกเฟรม)
            if (!_cacheComputed[_currentIndex])
            {
                Vector2 rawPosition = _waypoints[_currentIndex].position;
                _safePositionCache[_currentIndex] = ResolveSafePosition(rawPosition);
                _cacheComputed[_currentIndex] = true;
            }

            return _safePositionCache[_currentIndex];
        }

        /// <summary>
        /// ถ้าจุดที่กำหนดอยู่ใกล้กำแพงเกินไป (อยู่ในระยะที่ระบบเลี่ยงกำแพงจะผลักออก) ให้หาจุดใกล้เคียง
        /// ที่ปลอดภัยจริงแทน มิฉะนั้นคืนตำแหน่งเดิม (Waypoint ส่วนใหญ่ที่วางไว้กลางทางเดินอยู่แล้วจะไม่โดนขยับเลย)
        /// </summary>
        private Vector2 ResolveSafePosition(Vector2 rawPosition)
        {
            const float wallClearance = 1.2f; // ต้องตรงกับ wallMargin ที่ใช้ใน SteeringMovement

            if (!PhysicsUtility.IsPositionBlocked(rawPosition, wallClearance, _obstacleMask))
                return rawPosition; // ปลอดภัยอยู่แล้ว ไม่ต้องขยับ

            if (PhysicsUtility.TryFindNearestValidPoint(rawPosition, wallClearance * 2f, _obstacleMask, out Vector2 safePoint))
                return safePoint;

            return rawPosition; // หาไม่เจอจริงๆ (เช่นล้อมมุมอับ) ใช้ตำแหน่งเดิมไปก่อนดีกว่าไม่มีจุดหมายเลย
        }

        public bool HasReached(Vector2 currentPosition, float threshold = 0.2f)
        {
            return Vector2.Distance(currentPosition, GetCurrentWaypoint()) <= threshold;
        }

        public void AdvanceToNext()
        {
            if (_waypoints == null || _waypoints.Length == 0) return;
            _currentIndex = (_currentIndex + 1) % _waypoints.Length;
        }

        public bool HasWaypoints => _waypoints != null && _waypoints.Length > 0;
    }
}

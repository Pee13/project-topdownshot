using UnityEngine;
using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.Alert
{
    /// <summary>
    /// State "สงสัย" (Suspicious) — ช่วงก่อนที่ AI จะ "ยืนยัน" ว่าเจอผู้เล่นจริงๆ
    /// เห็นเงาๆ/เห็นแวบๆ ก่อน ต้องเห็นต่อเนื่องจนสะสมความสงสัยครบ (Blackboard.SuspicionLevel ถึง 1)
    /// ถึงจะเข้าสู่ Combat/Chase/Cover จริงจัง — เหมือนโหมด Caution ก่อน Alert เต็มตัวในเกมแนวลอบเร้น (เช่น MGS)
    ///
    /// ระหว่างอยู่ใน State นี้จะหยุดเดิน หันหน้าเข้าหาจุดที่สงสัยอยู่กับที่ ไม่รีบวิ่งเข้าใส่/ยิงทันที
    /// ถ้าผู้เล่นหลบออกจากสายตาก่อนสะสมครบ ความสงสัยจะค่อยๆ ลดลง (ไม่ใช่ลืมทันที) แล้วกลับไป Patrol/Search ต่อ
    /// </summary>
    public class SuspiciousState : IAIState
    {
        public EnemyState StateType => EnemyState.Suspicious;

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly float _turnSpeed;

        public SuspiciousState(Transform self, Blackboard blackboard, float turnSpeed = 240f)
        {
            _self = self;
            _blackboard = blackboard;
            _turnSpeed = turnSpeed;
        }

        public void Enter() { }

        public void Tick(float deltaTime)
        {
            // หันหน้าเข้าหาจุดที่สงสัยอยู่ (ตำแหน่งผู้เล่นถ้ายังเห็นอยู่ ไม่งั้นใช้จุดล่าสุดที่เห็น)
            Vector2 lookTarget = _blackboard.CurrentTarget != null
                ? (Vector2)_blackboard.CurrentTarget.position
                : _blackboard.LastSeen.Position;

            _blackboard.CurrentDestination = lookTarget; // ให้ AIPathVisualizer เห็นด้วยว่ากำลังจ้องจุดไหนอยู่

            Vector2 toTarget = lookTarget - (Vector2)_self.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
                float currentAngle = _self.eulerAngles.z;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, _turnSpeed * deltaTime);
                _self.rotation = Quaternion.Euler(0, 0, newAngle);
            }
        }

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit() { }
    }
}

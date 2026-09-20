using UnityEngine;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Chase
{
    /// <summary>
    /// ควบคุมการเคลื่อนที่และหมุนตัวของศัตรูขณะไล่ตามเป้าหมาย
    /// ใช้ SteeringMovement (Whisker Raycast) เลี่ยงกำแพง + StuckDetector กันติดมุมอับ
    /// + แรงผลักจากพันธมิตรตัวอื่น (GroupAI) กันไม่ให้เดินซ้อนทับ/ชิดกันเกินไปตอนเข้าหาผู้เล่นพร้อมกันหลายตัว
    /// </summary>
    public class ChaseMovement
    {
        private readonly Transform _self;
        private readonly float _moveSpeed;
        private readonly LayerMask _obstacleMask;
        private readonly float _personalSpace;
        private readonly StuckDetector _stuckDetector = new StuckDetector();

        public ChaseMovement(Transform self, float moveSpeed, LayerMask obstacleMask, float personalSpace = 1.3f)
        {
            _self = self;
            _moveSpeed = moveSpeed;
            _obstacleMask = obstacleMask;
            _personalSpace = personalSpace;
            _stuckDetector.Reset(self.position);
        }

        public void MoveTowards(Vector2 destination, float deltaTime)
        {
            Vector2 current = _self.position;
            Vector2 desiredDirection = (destination - current).normalized;

            // แรงผลักจากพันธมิตรตัวอื่น (Separation) และแรงผลักจากกำแพง ตอนนี้ SteeringMovement จัดการให้อัตโนมัติแล้ว
            // (ส่ง _self + _personalSpace เข้าไป ไม่ต้องผสมเองซ้ำตรงนี้)
            Vector2 direction = SteeringMovement.GetSteeredDirection(current, desiredDirection, _obstacleMask, self: _self, personalSpace: _personalSpace);

            _stuckDetector.Tick(current, deltaTime);
            if (_stuckDetector.IsStuck && direction.sqrMagnitude < 0.0001f)
            {
                // ติดมุมอับจริงๆ (steering หาทางไม่ได้เลย): สไลด์ออกด้านข้างแบบ deterministic
                // (เดิมสุ่มซ้าย/ขวา — §41 ให้สุ่มได้เฉพาะ tie-breaker)
                Vector2 sideDir = Vector2.Perpendicular(desiredDirection);
                direction = Random.value > 0.5f ? sideDir : -sideDir;
            }

            // เดินแบบ "เช็คก่อนก้าว" — กันหน้าแหลมมุดกำแพงก่อน collider จะแตะ
            // และกันข้ามกำแพงในสเต็ปเดียว (ระบบชนหลังย้ายของ EnemyBrain ยังคุมอยู่เป็นชั้นสุดท้าย)
            Vector2 step = direction * (_moveSpeed * deltaTime);
            Vector2 newPos = SteeringMovement.MoveWithCollisionCheck(_self, step, _obstacleMask);
            _self.position = newPos;

            if (direction.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                _self.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
    }
}

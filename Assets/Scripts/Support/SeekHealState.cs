using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Tactical;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Support
{
    /// <summary>
    /// ถึงระยะฮีลแล้ว — อยู่กับที่ให้ Healer ฮีลจนเลือดกลับมา (§18: ReachHealingZone → SEEK_HEAL)
    ///
    /// หน้าที่ของ state นี้:
    ///   - อยู่ใกล้ Healer ไม่ให้หลุดระยะฮีล (ถ้า Healer เคลื่อน ให้เดินตามเบาๆ)
    ///   - "ไม่" เข้าโจมตี — การตัดสินใจนี้อยู่ที่ TacticalDecision ซึ่งจะไม่เสนอ
    ///     Chase/Combat ให้ตัวที่เลือดต่ำกว่าเกณฑ์ถอย
    ///   - ไม่เขียน rotation (ให้คงทิศเดิมไว้ ระหว่างรอฮีลไม่จำเป็นต้องเล็งใคร)
    /// </summary>
    public class SeekHealState : IAIState
    {
        public EnemyState StateType => EnemyState.SeekHeal;

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly LayerMask _obstacleMask;
        private readonly float _followSpeed;

        public SeekHealState(Transform self, Blackboard blackboard, LayerMask obstacleMask, float followSpeed = 20f)
        {
            _self = self;
            _blackboard = blackboard;
            _obstacleMask = obstacleMask;
            _followSpeed = followSpeed;
        }

        public void Enter() { }

        public void Tick(float deltaTime)
        {
            Transform healer = _blackboard.HealerTransform;
            if (healer == null) return;

            Vector2 selfPos = _self.position;
            Vector2 healerPos = healer.position;
            float distance = Vector2.Distance(selfPos, healerPos);

            // ถ้าหลุดระยะฮีล (healer ลงแดก/ถอยหนี) ให้เดินตามเบาๆ คงสถานะการฮีลไว้
            if (distance > _blackboard.HealRange)
            {
                Vector2 dir = (healerPos - selfPos).normalized;
                Vector2 steered = SteeringMovement.GetSteeredDirection(selfPos, dir, _obstacleMask, self: _self, personalSpace: 1.2f);

                if (steered.sqrMagnitude > 0.0001f)
                {
                    // เดินตาม healer ด้วยความเร็วจริง (เดิม 2 หน่วย/วิ = เดินริ้วรอยบนแมพใหญ่)
                    _self.position = SteeringMovement.MoveWithCollisionCheck(
                        _self, steered * (_followSpeed * deltaTime), _obstacleMask);
                    _blackboard.CurrentDestination = healerPos;
                }
                return;
            }

            // อยู่ในระยะฮีลแล้ว — ยืนนิ่ง อย่าเดินวนให้ healer เล็งไม่ถูก
            _blackboard.CurrentDestination = selfPos;
        }

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit() { }
    }
}

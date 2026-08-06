using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Memory;
using TopDownTacticalAI.Tactical;

namespace TopDownTacticalAI.Chase
{
    /// <summary>
    /// State ไล่ตามผู้เล่นขณะที่ยังมองเห็นอยู่
    /// ถ้ามองไม่เห็นแล้ว ระบบภายนอก (EnemyBrain) จะเปลี่ยนไป Search โดยใช้ตำแหน่งใน Memory
    /// ถ้าระยะใกล้พอและมีกระสุน จะเปลี่ยนไป Combat
    ///
    /// ถ้ามีศัตรูหลายตัวไล่ผู้เล่นพร้อมกัน (เช็คผ่าน GroupAI):
    /// - ตัวแรก (index 0) จะไล่ตรงเข้าหาผู้เล่นตามปกติ
    /// - ตัวอื่นๆ จะ "อ้อม" ไปดักคนละมุมรอบผู้เล่นแทน (ดู Flanking.cs) เพื่อปิดทางหนี ไม่ใช่วิ่งไล่ทับเส้นทางเดียวกันหมด
    /// </summary>
    public class ChaseState : IAIState
    {
        public EnemyState StateType => EnemyState.Chase;

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly ChaseMovement _movement;
        private readonly TargetMemory _targetMemory;
        private readonly float _predictionLeadTime;
        private readonly float _flankRadius;

        public ChaseState(Transform self, Blackboard blackboard, TargetMemory targetMemory, float moveSpeed, LayerMask obstacleMask, float predictionLeadTime = 0.3f, float flankRadius = 3f, float personalSpace = 1.3f)
        {
            _self = self;
            _blackboard = blackboard;
            _targetMemory = targetMemory;
            _movement = new ChaseMovement(self, moveSpeed, obstacleMask, personalSpace);
            _predictionLeadTime = predictionLeadTime;
            _flankRadius = flankRadius;
        }

        public void Enter() { }

        public void Tick(float deltaTime)
        {
            if (_blackboard.CurrentTarget == null) return;

            Vector2 targetPos = _blackboard.CurrentTarget.position;
            _targetMemory.Update(targetPos, deltaTime);

            Vector2 predicted = TargetPrediction.Predict(targetPos, _targetMemory.LastVelocity, _predictionLeadTime);
            Vector2 destination = GetDestinationWithFlanking(predicted);
            _blackboard.CurrentDestination = destination;

            _movement.MoveTowards(destination, deltaTime);
        }

        /// <summary>
        /// ถ้ามีเพื่อนไล่ผู้เล่นอยู่ด้วยหลายตัว ให้แบ่งบทบาท: ตัวหลักไล่ตรง ตัวอื่นอ้อมไปดักรอบๆ แทน
        /// </summary>
        private Vector2 GetDestinationWithFlanking(Vector2 predictedTargetPos)
        {
            if (GroupAI.Instance == null) return predictedTargetPos;

            int myIndex = GroupAI.Instance.GetActiveChaseIndex(_self, out int totalChasers);

            // index 0 (ตัวหลัก) หรือมีตัวเดียว -> ไล่ตรงตามปกติ ไม่ต้องอ้อม
            if (myIndex <= 0 || totalChasers <= 1)
                return predictedTargetPos;

            return Flanking.GetFlankPosition(predictedTargetPos, myIndex, totalChasers, _flankRadius);
        }

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit() { }
    }
}

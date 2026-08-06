using UnityEngine;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Cover
{
    /// <summary>
    /// ควบคุมการถอยห่างจากเป้าหมายไปยังจุดกำบัง
    /// ใช้ SteeringMovement เลี่ยงกำแพงระหว่างทางไปที่กำบัง
    /// </summary>
    public class RetreatSystem
    {
        private readonly Transform _self;
        private readonly float _moveSpeed;
        private readonly LayerMask _obstacleMask;

        public RetreatSystem(Transform self, float moveSpeed, LayerMask obstacleMask = default)
        {
            _self = self;
            _moveSpeed = moveSpeed;
            _obstacleMask = obstacleMask;
        }

        public bool MoveToCover(Vector2 coverPosition, float deltaTime, float arriveThreshold = 0.2f)
        {
            Vector2 current = _self.position;
            Vector2 desiredDir = (coverPosition - current).normalized;
            Vector2 dir = SteeringMovement.GetSteeredDirection(current, desiredDir, _obstacleMask, self: _self);

            _self.position = current + dir * _moveSpeed * deltaTime;
            return Vector2.Distance(_self.position, coverPosition) <= arriveThreshold;
        }
    }
}

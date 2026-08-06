using UnityEngine;

namespace TopDownTacticalAI.Animation
{
    /// <summary>
    /// คำนวณความเร็วปัจจุบันจากการเปลี่ยนตำแหน่งเฟรมต่อเฟรม เพื่อป้อนให้ Animator (Blend Tree)
    /// </summary>
    public class MovementAnimation
    {
        private Vector2 _lastPosition;

        public MovementAnimation(Vector2 startPosition)
        {
            _lastPosition = startPosition;
        }

        public float CalculateSpeed(Vector2 currentPosition, float deltaTime)
        {
            if (deltaTime <= 0f) return 0f;
            float speed = Vector2.Distance(currentPosition, _lastPosition) / deltaTime;
            _lastPosition = currentPosition;
            return speed;
        }
    }
}

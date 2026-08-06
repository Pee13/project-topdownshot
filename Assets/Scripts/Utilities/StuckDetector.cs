using UnityEngine;

namespace TopDownTacticalAI.Utilities
{
    /// <summary>
    /// ตรวจจับว่า AI กำลัง "ติด" อยู่หรือไม่ (พยายามเดินแต่ตำแหน่งแทบไม่ขยับ)
    /// ใช้คู่กับ SteeringMovement เพื่อบังคับสไลด์ออกด้านข้างเมื่อติดมุมอับ
    /// </summary>
    public class StuckDetector
    {
        private Vector2 _lastCheckPosition;
        private float _timer;
        private readonly float _checkInterval;
        private readonly float _minMoveDistance;

        public bool IsStuck { get; private set; }

        public StuckDetector(float checkInterval = 0.5f, float minMoveDistance = 0.15f)
        {
            _checkInterval = checkInterval;
            _minMoveDistance = minMoveDistance;
        }

        /// <summary>เรียกทุกเฟรมขณะที่ AI "ตั้งใจ" จะเดินอยู่ (ไม่ใช่ตอนหยุดรอ/ยิง)</summary>
        public void Tick(Vector2 currentPosition, float deltaTime)
        {
            _timer += deltaTime;

            if (_timer >= _checkInterval)
            {
                float moved = Vector2.Distance(currentPosition, _lastCheckPosition);
                IsStuck = moved < _minMoveDistance;

                _lastCheckPosition = currentPosition;
                _timer = 0f;
            }
        }

        public void Reset(Vector2 currentPosition)
        {
            _lastCheckPosition = currentPosition;
            _timer = 0f;
            IsStuck = false;
        }
    }
}

using UnityEngine;

namespace TopDownTacticalAI.Patrol
{
    /// <summary>
    /// พฤติกรรมหยุดมองซ้าย-ขวาระหว่างจุด Patrol เพื่อให้ AI ดูสมจริงมากขึ้น
    /// </summary>
    public class IdleLookAround
    {
        private readonly float _lookDuration;
        private float _timer;
        private float _swayAngle;
        public bool IsLooking { get; private set; }

        public IdleLookAround(float lookDuration = 1.5f)
        {
            _lookDuration = lookDuration;
        }

        public void StartLooking()
        {
            IsLooking = true;
            _timer = 0f;
        }

        /// <returns>มุมหมุนที่ควรนำไปใช้กับตัวศัตรู (องศา, แกว่งซ้าย-ขวา)</returns>
        public float Tick(float deltaTime)
        {
            if (!IsLooking) return 0f;

            _timer += deltaTime;
            _swayAngle = Mathf.Sin(_timer * 3f) * 45f;

            if (_timer >= _lookDuration)
                IsLooking = false;

            return _swayAngle;
        }
    }
}

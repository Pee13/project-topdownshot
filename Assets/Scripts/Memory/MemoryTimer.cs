namespace TopDownTacticalAI.Memory
{
    /// <summary>
    /// จับเวลานับถอยหลังว่าความจำของ AI จะหมดอายุเมื่อไหร่
    /// </summary>
    public class MemoryTimer
    {
        private float _remaining;
        public bool IsExpired => _remaining <= 0f;

        public void Start(float duration)
        {
            _remaining = duration;
        }

        public void Tick(float deltaTime)
        {
            if (_remaining > 0f)
                _remaining -= deltaTime;
        }

        public void Reset()
        {
            _remaining = 0f;
        }

        public float Remaining => _remaining;
    }
}

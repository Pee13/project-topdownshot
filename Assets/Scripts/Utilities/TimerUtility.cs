namespace TopDownTacticalAI.Utilities
{
    /// <summary>
    /// Timer อเนกประสงค์แบบนับถอยหลัง ใช้ซ้ำได้ในหลายระบบ (Patrol wait, Reload, Peek, ฯลฯ)
    /// </summary>
    public class CountdownTimer
    {
        private float _duration;
        private float _elapsed;

        public bool IsFinished => _elapsed >= _duration;
        public float Progress01 => _duration <= 0f ? 1f : Mathf01(_elapsed / _duration);

        public void Start(float duration)
        {
            _duration = duration;
            _elapsed = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (_elapsed < _duration)
                _elapsed += deltaTime;
        }

        public void Stop()
        {
            _elapsed = _duration;
        }

        private static float Mathf01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}

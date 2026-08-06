using UnityEngine;
using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.Memory
{
    /// <summary>
    /// ระบบความจำหลักของศัตรู: จดจำตำแหน่งล่าสุด, เวลา, ทิศทาง
    /// และลืมข้อมูลอัตโนมัติเมื่อครบเวลาที่กำหนด (MemoryDuration)
    /// </summary>
    public class EnemyMemory : MonoBehaviour
    {
        [Header("Memory Settings")]
        [Tooltip("ระยะเวลา (วินาที) ก่อนที่ AI จะลืมตำแหน่งล่าสุดของผู้เล่น")]
        public float MemoryDuration = 6f;

        private Blackboard _blackboard;
        private readonly MemoryTimer _timer = new MemoryTimer();
        public TargetMemory TargetMemoryData { get; } = new TargetMemory();

        public void Initialize(Blackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public void RecordSighting(Vector2 position, Vector2 direction)
        {
            _blackboard.LastSeen.Record(position, direction, Time.time);
            _blackboard.HasMemory = true;
            _timer.Start(MemoryDuration);
        }

        public void Tick(float deltaTime)
        {
            if (!_blackboard.HasMemory)
                return;

            _timer.Tick(deltaTime);

            if (_timer.IsExpired)
            {
                Forget();
            }
        }

        public void Forget()
        {
            _blackboard.HasMemory = false;
            _blackboard.LastSeen.Clear();
            _timer.Reset();
        }

        public float MemoryTimeRemaining => _timer.Remaining;
    }
}

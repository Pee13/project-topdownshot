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
            _blackboard.MemoryConfidence = 1f;   // เพิ่งเห็นสดๆ = มั่นใจเต็มที่
            _timer.Start(MemoryDuration);
        }

        public void Tick(float deltaTime)
        {
            if (!_blackboard.HasMemory)
                return;

            _timer.Tick(deltaTime);

            // ความมั่นใจในความจำลดลงเรื่อยๆ ตามเวลาที่ผ่านไปตั้งแต่เห็นครั้งสุดท้าย
            // ยิ่งนานยิ่งไม่แน่ใจว่าตำแหน่งเดิมยังจริงอยู่ไหม (ข้อมูลเก่า = เชื่อน้อยลง)
            _blackboard.MemoryConfidence = ComputeConfidence();

            if (_timer.IsExpired)
            {
                Forget();
            }
        }

        public void Forget()
        {
            _blackboard.HasMemory = false;
            _blackboard.MemoryConfidence = 0f;
            _blackboard.LastSeen.Clear();
            _timer.Reset();
        }

        /// <summary>เวลาที่ผ่านไปตั้งแต่เห็นเป้าหมายครั้งสุดท้าย (วินาที)</summary>
        public float LastSeenAge => Mathf.Max(0f, MemoryDuration - _timer.Remaining);

        /// <summary>
        /// ความมั่นใจในข้อมูลที่จำได้ (0..1)
        /// ลดลงเป็นเส้นตรงตามอายุความจำ — พอครบกำหนด (MemoryDuration) จะเหลือ 0 พอดี
        /// </summary>
        private float ComputeConfidence()
        {
            if (MemoryDuration <= 0f) return 0f;
            return Mathf.Clamp01(1f - (LastSeenAge / MemoryDuration));
        }

        public float MemoryTimeRemaining => _timer.Remaining;
    }
}

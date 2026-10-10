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

        // ความมั่นใจ "ตอนบันทึก" — คูณกับการลดลงตามอายุใน ComputeConfidence
        // ทำให้แหล่งข้อมูลที่ไม่แน่นอน (เช่น เสียงปืนระยะไกล = แค่ "อาจจะอยู่แถวนั้น")
        // มี MemoryConfidence ต่ำกว่าการเห็นด้วยตาจริง ๆ ตั้งแต่แรก (§50: confidence ทุกแหล่ง)
        private float _peakConfidence = 1f;

        public void Initialize(Blackboard blackboard)
        {
            _blackboard = blackboard;
        }

        /// <summary>
        /// บันทึกการพบเห็น/ได้ยิน
        /// </summary>
        /// <param name="confidence">
        /// ความมั่นใจ 0..1 ณ ตอนบันทึก — เห็นด้วยตา/เพื่อนแจ้งตำแหน่ง = 1.0,
        /// เสียงปืนใกล้ = ~1.0, เสียงปืนไกล = ~0.25 (ลดหลั่นตามระยะ — EnemyBrain.ReceiveNoise คำนวณให้)
        /// </param>
        public void RecordSighting(Vector2 position, Vector2 direction, float confidence = 1f)
        {
            _blackboard.LastSeen.Record(position, direction, Time.time);
            _blackboard.HasMemory = true;
            _peakConfidence = Mathf.Clamp01(confidence);
            _blackboard.MemoryConfidence = _peakConfidence; // ยิ่งใหม่ยิ่งชัด (คูณกับอายุใน Tick)
            _timer.Start(MemoryDuration);
        }

        public void Tick(float deltaTime)
        {
            if (!_blackboard.HasMemory)
                return;

            _timer.Tick(deltaTime);

            // ความมั่นใจในความจำลดลงเรื่อยๆ ตามเวลาที่ผ่านไปตั้งแต่เห็นครั้งสุดท้าย
            // ยิ่งนานยิ่งไม่แน่ใจว่าตำแหน่งเดิมยังจริงอยู่ไหม (ข้อมูลเก่า = เชื่อน้อยลง)
            // คูณด้วย _peakConfidence ด้วย — เสียงไกล ๆ ที่ "อาจจะอยู่แถวนั้น" จะไม่มีทางชัดเท่าการเห็นจริง
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
            _peakConfidence = 1f; // รอบหน้าเริ่มใหม่ที่ค่าเต็ม
            _timer.Reset();
        }

        /// <summary>เวลาที่ผ่านไปตั้งแต่เห็นเป้าหมายครั้งสุดท้าย (วินาที)</summary>
        public float LastSeenAge => Mathf.Max(0f, MemoryDuration - _timer.Remaining);

        /// <summary>
        /// ความมั่นใจในข้อมูลที่จำได้ (0..1)
        /// = ความมั่นใจตอนบันทึก × การลดลงตามอายุความจำ (พอครบ MemoryDuration จะเหลือ 0 พอดี)
        /// </summary>
        private float ComputeConfidence()
        {
            if (MemoryDuration <= 0f) return 0f;
            return Mathf.Clamp01(_peakConfidence * (1f - LastSeenAge / MemoryDuration));
        }

        public float MemoryTimeRemaining => _timer.Remaining;
    }
}

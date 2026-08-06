using System.Collections.Generic;
using UnityEngine;

namespace TopDownTacticalAI.Memory
{
    /// <summary>
    /// เก็บ "ประวัติเส้นทาง" (Breadcrumb Trail) ของเป้าหมายที่เคยเห็น ไม่ใช่แค่จุดเดียวล่าสุด
    /// เพื่อให้ระบบ Search เดาทิศทางที่ผู้เล่นวิ่งหนีได้แม่นยำขึ้น (ใช้ค่าเฉลี่ยจากหลายจุด แทนความเร็วเฟรมเดียวที่อาจสั่น/ไม่แม่น)
    /// เช่น ถ้าผู้เล่นเดินอ้อมโค้งกำแพง ประวัติเส้นทางจะช่วยให้ AI เดาทิศตามแนวโค้งนั้นต่อได้ แทนที่จะพุ่งตรงเป็นเส้นตรงเฉยๆ
    /// </summary>
    public class TargetMemory
    {
        private const int MaxTrailPoints = 8;
        private const float MinIntervalBetweenPoints = 0.35f;
        private readonly List<Vector2> _trail = new List<Vector2>();
        private float _timeSinceLastTrailPoint;

        public Vector2 LastVelocity { get; private set; }
        public Vector2 LastPosition { get; private set; }

        /// <summary>เรียกทุกเฟรมขณะเห็นเป้าหมายอยู่ (เช่นใน ChaseState) เพื่อบันทึกเส้นทางและคำนวณความเร็ว</summary>
        public void Update(Vector2 currentPosition, float deltaTime)
        {
            if (deltaTime > 0f)
                LastVelocity = (currentPosition - LastPosition) / deltaTime;

            LastPosition = currentPosition;

            // บันทึกจุดลงประวัติเส้นทางอัตโนมัติ แต่เว้นระยะห่างเวลาไว้ (ไม่ต้องทุกเฟรม กันจุดถี่เกินจนไม่มีประโยชน์)
            _timeSinceLastTrailPoint += deltaTime;
            if (_timeSinceLastTrailPoint >= MinIntervalBetweenPoints)
            {
                RecordTrailPoint(currentPosition);
                _timeSinceLastTrailPoint = 0f;
            }
        }

        /// <summary>
        /// บันทึกจุดที่เห็นเป้าหมายลงในประวัติเส้นทาง (เรียกห่างๆ กัน เช่นทุก 0.3-0.5 วินาที ไม่ต้องทุกเฟรม)
        /// เก็บไว้แค่ล่าสุด MaxTrailPoints จุด (จุดเก่ากว่านั้นจะถูกทิ้งอัตโนมัติ)
        /// </summary>
        public void RecordTrailPoint(Vector2 position)
        {
            _trail.Add(position);
            if (_trail.Count > MaxTrailPoints)
                _trail.RemoveAt(0);
        }

        public void ClearTrail()
        {
            _trail.Clear();
        }

        public bool HasTrail => _trail.Count >= 2;

        /// <summary>
        /// ทิศทางเฉลี่ยจากประวัติเส้นทางล่าสุด (แม่นยำ/นิ่งกว่าการใช้ LastVelocity เฟรมเดียว)
        /// ใช้เป็นทิศทางหลักตอนเดา "ผู้เล่นน่าจะวิ่งต่อไปทางไหน" ตอนเริ่ม Search
        /// </summary>
        public Vector2 GetSmoothedDirection()
        {
            if (!HasTrail) return LastVelocity.normalized;

            Vector2 sum = Vector2.zero;
            int segments = 0;

            for (int i = 1; i < _trail.Count; i++)
            {
                Vector2 segment = _trail[i] - _trail[i - 1];
                if (segment.sqrMagnitude > 0.0001f)
                {
                    sum += segment.normalized;
                    segments++;
                }
            }

            return segments > 0 ? (sum / segments).normalized : LastVelocity.normalized;
        }

        public Vector2 PredictPosition(float timeAhead)
        {
            return LastPosition + LastVelocity * timeAhead;
        }

        /// <summary>
        /// ใส่ข้อมูลทิศทางจาก "เพื่อนที่แจ้งเตือนมา" (Group Alert) เข้าไปแทนประวัติเส้นทางของตัวเอง
        /// ใช้ตอนที่ตัวนี้ยังไม่เคยเห็นผู้เล่นด้วยตาตัวเองเลย แต่ได้รับข้อมูลทิศทางจากเพื่อนที่เห็นมา
        /// ทำให้ตอนเข้าสู่ Search ก็ยังมีทิศทางให้เดาต่อได้ ไม่ใช่มีแค่จุดเดียวโดยไม่รู้ทิศทางเหมือนเดิม
        /// </summary>
        public void SeedFromExternalIntel(Vector2 position, Vector2 direction)
        {
            _trail.Clear();

            if (direction.sqrMagnitude > 0.01f)
            {
                Vector2 dirNormalized = direction.normalized;
                _trail.Add(position - dirNormalized); // จุดสมมติก่อนหน้า เพื่อให้มีเส้นทาง 2 จุดคำนวณทิศทางได้
                _trail.Add(position);
                LastVelocity = dirNormalized;
            }

            LastPosition = position;
        }
    }
}

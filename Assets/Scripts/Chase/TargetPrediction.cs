using UnityEngine;

namespace TopDownTacticalAI.Chase
{
    /// <summary>
    /// ทำนายตำแหน่งในอนาคตของเป้าหมายจากความเร็วปัจจุบัน
    /// เพื่อให้การไล่ตามตัดหน้าได้แม่นยำขึ้นแทนการวิ่งตามตำแหน่งปัจจุบันตรงๆ
    /// </summary>
    public static class TargetPrediction
    {
        public static Vector2 Predict(Vector2 targetPosition, Vector2 targetVelocity, float leadTime)
        {
            return targetPosition + targetVelocity * leadTime;
        }

        /// <summary>
        /// ทำนายตำแหน่งเป้าหมายพร้อม "ความมั่นใจ" ในการทำนาย
        ///
        /// ต่างจาก Predict() ตรงที่:
        ///   1) เวลานำ (lead time) คำนวณจากระยะทางจริง หารด้วยความเร็วที่คาดว่า AI จะไปถึง
        ///      ไม่ใช่ค่าคงที่ — เป้าหมายอยู่ไกลก็ต้องนำนานกว่าเพื่อให้ทันตัดหน้า
        ///   2) ถูก clamp ด้วย min/max กันไม่ให้เป้าหมายที่วิ่งเร็วมากทำให้ AI ทำนายไกลเกินจริง
        ///   3) คืนค่า confidence บอกว่าควรเชื่อการทำนายนี้แค่ไหน
        ///
        /// confidence ต่ำลงเมื่อเป้าหมายเปลี่ยนทิศทางบ่อย (คาดเดายาก)
        /// ผู้เรียกควรคูณน้ำหนักการทำนายด้วยค่านี้ ไม่ควรเชื่อ 100%
        /// </summary>
        /// <param name="targetPosition">ตำแหน่งปัจจุบันของเป้าหมาย</param>
        /// <param name="targetVelocity">ความเร็วปัจจุบันของเป้าหมาย</param>
        /// <param name="distance">ระยะห่างจาก AI ถึงเป้าหมาย</param>
        /// <param name="estimatedTravelSpeed">ความเร็วที่ใช้ประมาณเวลาที่ AI จะไปถึง</param>
        /// <param name="minLeadTime">เวลานำขั้นต่ำ (วินาที)</param>
        /// <param name="maxLeadTime">เวลานำสูงสุด (วินาที)</param>
        /// <param name="previousDirection">ทิศทางการเคลื่อนที่ที่เคยเห็นครั้งก่อน (ใช้เทียบความนิ่ง)</param>
        /// <param name="confidence">ผลลัพธ์: ความมั่นใจในการทำนาย 0..1</param>
        public static Vector2 PredictWithConfidence(
            Vector2 targetPosition,
            Vector2 targetVelocity,
            float distance,
            float estimatedTravelSpeed,
            float minLeadTime,
            float maxLeadTime,
            Vector2 previousDirection,
            out float confidence)
        {
            float speed = Mathf.Max(0.1f, estimatedTravelSpeed);

            // เวลานำตามระยะทางจริง แล้ว clamp ให้อยู่ในกรอบที่สมเหตุสมผล
            float leadTime = Mathf.Clamp(distance / speed, minLeadTime, maxLeadTime);

            Vector2 predicted = targetPosition + targetVelocity * leadTime;

            // ── ความมั่นใจในการทำนาย ──
            // เป้าหมายที่วิ่งเป็นเส้นตรง = ทำนายง่าย (มั่นใจสูง)
            // เป้าหมายที่เปลี่ยนทิศไปมา = ทำนายยาก (มั่นใจต่ำ)
            float confidenceNow = ComputeDirectionStability(targetVelocity, previousDirection);

            // เป้าหมายที่แทบไม่ขยับ ก็ไม่ต้องเชื่อการทำนายมากนัก (ทำนายได้แต่มันไม่มีนัยสำคัญ)
            float speedFactor = Mathf.Clamp01(targetVelocity.magnitude / Mathf.Max(0.1f, speed));
            confidenceNow *= Mathf.Lerp(0.5f, 1f, speedFactor);

            confidence = Mathf.Clamp01(confidenceNow);
            return predicted;
        }

        /// <summary>
        /// วัดความนิ่งของทิศทาง 0..1
        /// ทิศทางตรงกับครั้งก่อน = 1 (คาดเดาได้), สวนทางกัน = 0 (คาดเดาไม่ได้)
        /// </summary>
        public static float ComputeDirectionStability(Vector2 currentVelocity, Vector2 previousDirection)
        {
            if (currentVelocity.sqrMagnitude < 0.0001f) return 0f;
            if (previousDirection.sqrMagnitude < 0.0001f) return 0.5f; // ยังไม่เคยเห็นทิศทาง = กลางๆ

            Vector2 currentDir = currentVelocity.normalized;
            Vector2 previousDir = previousDirection.normalized;

            // Dot เป็น 1 = ทิศเดิมเป๊ะ, 0 = ตั้งฉาก, -1 = สวนทาง
            float dot = Vector2.Dot(currentDir, previousDir);

            // แปลงจาก -1..1 ให้เป็น 0..1
            return Mathf.Clamp01((dot + 1f) * 0.5f);
        }
    }
}

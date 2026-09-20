using UnityEngine;

namespace TopDownTacticalAI.Tactical
{
    /// <summary>
    /// น้ำหนักและอัตราการคำนวณทั้งหมดของระบบตัดสินใจ AI รวมไว้ที่เดียว
    /// เพื่อให้ปรับแต่งได้จาก Inspector โดยไม่ต้องแก้โค้ด
    ///
    /// หลักการ: คะแนนทุกตัวถูก normalize เป็น 0..1 ก่อน แล้วค่อยคูณน้ำหนักรวมกัน
    /// ถ้าน้ำหนักรวมกันได้ 1.0 ผลลัพธ์สุดท้ายจะอยู่ในช่วง 0..1 เสมอ ทำให้เทียบคะแนน
    /// ระหว่าง Action ต่างชนิดกันได้อย่างเป็นธรรม
    /// </summary>
    [System.Serializable]
    public class AIScoringWeights
    {
        [Header("── Health Priority (§5) ──")]
        [Tooltip("น้ำหนักของความต้องการเลือด (ยิ่งเลือดน้อยยิ่งต้องการมาก)")]
        [Range(0f, 1f)] public float healthNeed = 0.35f;
        [Tooltip("น้ำหนักโบนัสเมื่อเลือดเข้าขั้นวิกฤต")]
        [Range(0f, 1f)] public float criticalBonus = 0.20f;
        [Tooltip("น้ำหนักความสำคัญตามบทบาทของตัวนั้นๆ")]
        [Range(0f, 1f)] public float roleImportance = 0.15f;
        [Tooltip("น้ำหนักของภัยคุกคามรอบตัว")]
        [Range(0f, 1f)] public float threatImportance = 0.10f;
        [Tooltip("น้ำหนักของการมองเห็นเป้าหมาย")]
        [Range(0f, 1f)] public float visibility = 0.10f;
        [Tooltip("น้ำหนักของความสามารถในการเข้าถึงตำแหน่ง")]
        [Range(0f, 1f)] public float accessibility = 0.10f;

        [Header("── Health Curve (§6) ──")]
        [Tooltip("เส้นโค้งความสำคัญของเลือด: แกน X = HP ที่เหลือ (0..1), แกน Y = ความต้องการ (0..1)")]
        public AnimationCurve healthPriorityCurve = DefaultHealthPriorityCurve();

        [Tooltip("ต่ำกว่านี้ถือว่าเลือดวิกฤต")]
        [Range(0f, 1f)] public float criticalThreshold = 0.35f;

        [Header("── Tactical Position Weights (§21) ──")]
        [Range(0f, 1f)] public float positionCover = 0.20f;
        [Range(0f, 1f)] public float positionVisibility = 0.20f;
        [Range(0f, 1f)] public float positionDistance = 0.15f;
        [Range(0f, 1f)] public float positionThreat = 0.15f;
        [Range(0f, 1f)] public float positionEscape = 0.15f;
        [Range(0f, 1f)] public float positionRole = 0.10f;
        [Range(0f, 1f)] public float positionTeam = 0.05f;

        [Header("── Decision Stability (§29, §30, §47) ──")]
        [Tooltip("คะแนนใหม่ต้องชนะคะแนนเดิมเกินค่านี้ก่อน จึงจะยอมเปลี่ยน Action (กันเปลี่ยนใจรัวๆ)")]
        [Range(0f, 0.5f)] public float switchThreshold = 0.10f;
        [Tooltip("ความเร็วในการปรับ Threat ให้ราบรื่น (ยิ่งสูงยิ่งตอบสนองไว)")]
        public float threatSmoothingSpeed = 3f;
        [Tooltip("คะแนน Threat ที่ถือว่า 'วิกฤต' จนต้องยกเลิก Action ปกติทันที (§48)")]
        [Range(0f, 1f)] public float criticalThreatOverride = 0.85f;
        [Tooltip("คะแนนเลือดที่ถือว่า 'วิกฤต' จนต้องหนีทันที (§48)")]
        [Range(0f, 1f)] public float criticalHealthOverride = 0.15f;
        [Tooltip("ถ้า Action ที่ทำอยู่ได้คะแนนต่ำกว่านี้ ถือว่า 'ใช้ไม่ได้แล้ว' " +
                 "ให้เปลี่ยนแผนได้ทันทีโดยไม่ต้องรอส่วนต่างคะแนน (กัน AI ดื้อทำแผนที่หมดความหมาย)")]
        [Range(0f, 1f)] public float minViableScore = 0.25f;

        [Header("── Prediction (§15–§18) ──")]
        [Tooltip("เวลานำขั้นต่ำที่ยอมให้ทำนายล่วงหน้า (วินาที)")]
        public float minPredictionTime = 0.1f;
        [Tooltip("เวลานำสูงสุด กันทำนายไกลเกินจริงเมื่อเป้าหมายเร็วมาก (วินาที)")]
        public float maxPredictionTime = 2.0f;
        [Tooltip("ความเร็วที่ใช้ประมาณเวลาที่ AI จะไปถึงเป้าหมาย (หน่วย/วินาที)")]
        public float estimatedTravelSpeed = 4f;

        [Header("── Update Rates (§43) ──")]
        [Tooltip("ความถี่การสแกนการมองเห็น (วินาที) — 0 = ทุกเฟรม")]
        public float visionInterval = 0f;
        [Tooltip("ความถี่การประเมินภัยคุกคาม (วินาที) — 0 = ทุกเฟรม")]
        public float threatInterval = 0f;
        [Tooltip("ความถี่การประสานงานกับทีม (วินาที) — 0 = ทุกเฟรม")]
        public float teamInterval = 0f;

        /// <summary>
        /// เส้นโค้งเริ่มต้นตามตารางในเอกสาร: 100%→0.00, 80%→0.10, 60%→0.30, 40%→0.65, 20%→0.90, 0%→1.00
        /// ความสำคัญจะเพิ่มขึ้นแบบทวีคูณเมื่อเลือดลดต่ำ ไม่ใช่เป็นเส้นตรง
        /// </summary>
        public static AnimationCurve DefaultHealthPriorityCurve()
        {
            var curve = new AnimationCurve(
                new Keyframe(0.00f, 1.00f),
                new Keyframe(0.20f, 0.90f),
                new Keyframe(0.40f, 0.65f),
                new Keyframe(0.60f, 0.30f),
                new Keyframe(0.80f, 0.10f),
                new Keyframe(1.00f, 0.00f));
            curve.preWrapMode = WrapMode.Clamp;
            curve.postWrapMode = WrapMode.Clamp;
            return curve;
        }

        /// <summary>ดึงค่าความต้องการเลือดจากเส้นโค้ง (กันกรณี curve ยังไม่ถูกตั้งค่า)</summary>
        public float EvaluateHealthNeed(float healthPercent01)
        {
            if (healthPriorityCurve == null || healthPriorityCurve.length == 0)
                return 1f - Mathf.Clamp01(healthPercent01);
            return Mathf.Clamp01(healthPriorityCurve.Evaluate(Mathf.Clamp01(healthPercent01)));
        }

        /// <summary>ตรวจว่าน้ำหนักทั้งหมดรวมกันได้ใกล้เคียง 1.0 หรือไม่ (ใช้เตือนใน Inspector)</summary>
        public float TotalWeight()
        {
            return healthNeed + criticalBonus + roleImportance
                 + threatImportance + visibility + accessibility;
        }

        private void OnValidate()
        {
            if (minPredictionTime < 0f) minPredictionTime = 0f;
            if (maxPredictionTime < minPredictionTime) maxPredictionTime = minPredictionTime;
            if (estimatedTravelSpeed < 0.1f) estimatedTravelSpeed = 0.1f;
        }
    }
}

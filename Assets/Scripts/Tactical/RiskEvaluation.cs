using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.Tactical
{
    /// <summary>
    /// ประเมินระดับความเสี่ยงของสถานการณ์ปัจจุบัน (0 = ปลอดภัย, 1 = อันตรายมาก)
    /// ใช้เป็นข้อมูลประกอบการตัดสินใจของ TacticalDecision
    /// </summary>
    public static class RiskEvaluation
    {
        public static float EvaluateRisk(Blackboard blackboard, float distanceToTarget, float dangerRange)
        {
            float hpRisk = 1f - (blackboard.CurrentHP / blackboard.MaxHP);
            float ammoRisk = blackboard.CurrentAmmo <= 0 ? 1f : 0f;
            float distanceRisk = distanceToTarget <= dangerRange ? 1f : 0f;

            // ถ่วงน้ำหนัก: HP สำคัญสุด รองมาคือกระสุน แล้วค่อยระยะ
            float risk = hpRisk * 0.5f + ammoRisk * 0.3f + distanceRisk * 0.2f;
            return UnityEngine.Mathf.Clamp01(risk);
        }
    }
}

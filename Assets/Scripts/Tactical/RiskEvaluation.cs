using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Tactical
{
    /// <summary>
    /// ประเมินระดับภัยคุกคามของสถานการณ์ปัจจุบัน (0 = ปลอดภัย, 1 = อันตรายถึงชีวิต)
    ///
    /// หลักการ: ใช้ข้อมูลหลายด้านร่วมกัน ไม่ตัดสินจากค่าเดียว
    ///   - เลือดที่เหลือ (ยิ่งน้อยยิ่งอันตราย)
    ///   - กระสุน (หมดกระสุน = ป้องกันตัวไม่ได้)
    ///   - ระยะห่างจากผู้เล่น เทียบกับระยะอันตราย
    ///   - ความมั่นใจในการมองเห็น (ไม่เห็น = ไม่รู้ว่าอันตรายแค่ไหน)
    ///   - ที่กำบัง (อยู่ในที่กำบัง = อันตรายน้อยลง)
    ///   - จำนวนเพื่อนที่อยู่ใกล้ (มีเพื่อนเยอะ = อันตรายน้อยลง)
    ///
    /// ทุกค่าถูก normalize เป็น 0..1 ก่อนถ่วงน้ำหนัก เพื่อไม่ให้ค่าที่ Scale ใหญ่
    /// (เช่น HP เป็นร้อย) กลืนค่าที่ Scale เล็ก (เช่น คะแนน 0..1)
    /// </summary>
    public static class RiskEvaluation
    {
        // น้ำหนักเริ่มต้น — รวมกันได้ 1.0
        private const float WeightHealth = 0.35f;
        private const float WeightAmmo = 0.15f;
        private const float WeightProximity = 0.25f;
        private const float WeightExposure = 0.15f;
        private const float WeightIsolation = 0.10f;

        /// <summary>
        /// ประเมินภัยคุกคามจากข้อมูลใน Blackboard (ค่าเริ่มต้น ไม่ต้องส่ง weights)
        /// </summary>
        public static float EvaluateRisk(Blackboard blackboard, float distanceToTarget, float dangerRange)
        {
            return EvaluateRisk(blackboard, distanceToTarget, dangerRange, null);
        }

        /// <summary>
        /// ประเมินภัยคุกคามแบบปรับน้ำหนักได้
        /// </summary>
        public static float EvaluateRisk(Blackboard blackboard, float distanceToTarget, float dangerRange, AIScoringWeights weights)
        {
            if (blackboard == null) return 0f;

            // 1) เลือด — ใช้สัดส่วนที่หายไป (กัน MaxHP เป็น 0 ทำให้เกิด NaN)
            float hpRisk = MathUtility.MissingHealth(blackboard.CurrentHP, blackboard.MaxHP);

            // 2) กระสุน — เหลือน้อยก็เสี่ยง แต่ค่อยๆ เพิ่ม ไม่ใช่กระโดดเป็น 1 ทันที
            float ammoRisk = blackboard.IsReloading
                ? 1f
                : 1f - MathUtility.Normalize01(blackboard.CurrentAmmo, Mathf.Max(1, blackboard.MaxAmmo));

            // 3) ระยะ — ยิ่งผู้เล่นใกล้ยิ่งอันตราย
            //    ถ้าเห็นผู้เล่น: ใช้ระยะจริงเทียบระยะอันตราย
            //    ถ้ามองไม่เห็น: ใช้ความมั่นใจในความจำแทน (จำได้แม่น = ยังต้องระวัง)
            float proximityRisk;
            if (blackboard.CanSeeTarget)
            {
                proximityRisk = 1f - MathUtility.Normalize01(distanceToTarget, Mathf.Max(0.01f, dangerRange * 2f));
            }
            else
            {
                proximityRisk = blackboard.MemoryConfidence * 0.5f;
            }

            // 4) การเปิดเผยตัว — ถูกมองเห็นชัดแค่ไหน หักลบด้วยการอยู่ในที่กำบัง
            float exposureRisk = blackboard.VisionConfidence;
            if (blackboard.InCover) exposureRisk *= 0.4f;

            // 5) ความโดดเดี่ยว — อยู่คนเดียวอันตรายกว่าอยู่กับพวก
            float isolationRisk = 1f - Mathf.Clamp01(blackboard.NearbyAllyCount / 3f);

            float risk = hpRisk * WeightHealth
                       + ammoRisk * WeightAmmo
                       + proximityRisk * WeightProximity
                       + exposureRisk * WeightExposure
                       + isolationRisk * WeightIsolation;

            return Mathf.Clamp01(risk);
        }

        /// <summary>
        /// แปลงค่าภัยคุกคามเป็นคำอธิบายระดับความรุนแรง เพื่อใช้แสดงผล Debug
        /// </summary>
        public static string DescribeThreat(float threat)
        {
            if (threat >= 0.75f) return "สูงมาก";
            if (threat >= 0.5f) return "ปานกลาง";
            if (threat >= 0.25f) return "ต่ำ";
            return "ปลอดภัย";
        }
    }
}

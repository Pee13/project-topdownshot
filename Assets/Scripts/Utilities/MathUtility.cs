using UnityEngine;

namespace TopDownTacticalAI.Utilities
{
    public static class MathUtility
    {
        public static Vector2 RandomPointInRadius(Vector2 center, float radius)
        {
            return center + Random.insideUnitCircle * radius;
        }

        public static float RemapClamped(float value, float inMin, float inMax, float outMin, float outMax)
        {
            float t = Mathf.InverseLerp(inMin, inMax, value);
            return Mathf.Lerp(outMin, outMax, t);
        }

        public static Vector2 DirectionFromAngle(float angleDegrees)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        // ─────────────────────────────────────────────────────────────────
        // Normalization helpers
        //
        // ค่าที่ใช้ตัดสินใจในระบบ AI มี Scale ต่างกันมาก (ระยะเป็นสิบหน่วย, HP เป็นร้อย,
        // Threat เป็น 0..1) การนำมาบวกกันตรงๆ จะทำให้ค่าที่ Scale ใหญ่กว่ากลืนค่าอื่นทั้งหมด
        // helper เหล่านี้แปลงทุกอย่างให้อยู่ในช่วง 0..1 ก่อนเสมอ เพื่อให้ Score แต่ละตัว
        // มีความหมายสม่ำเสมอกันแล้วนำไปถ่วงน้ำหนักรวมได้
        // ─────────────────────────────────────────────────────────────────

        /// <summary>แปลงค่าใดๆ ให้อยู่ในช่วง 0..1 โดยเทียบกับค่าสูงสุด (clamp ให้อัตโนมัติ)</summary>
        public static float Normalize01(float value, float max)
        {
            if (max <= 0f) return 0f;
            return Mathf.Clamp01(value / max);
        }

        /// <summary>สัดส่วนเลือดที่เหลือ 0..1 — กัน MaxHP เป็น 0 หรือติดลบ</summary>
        public static float HealthPercent(float currentHP, float maxHP)
        {
            if (maxHP <= 0f) return 0f;
            return Mathf.Clamp01(currentHP / maxHP);
        }

        /// <summary>สัดส่วนเลือดที่หายไป 0..1 (0 = เลือดเต็ม, 1 = เลือดหมด)</summary>
        public static float MissingHealth(float currentHP, float maxHP)
        {
            return 1f - HealthPercent(currentHP, maxHP);
        }

        /// <summary>คะแนนความใกล้ 0..1 — ระยะ 0 ได้ 1, ระยะเท่าหรือเกิน maxDistance ได้ 0</summary>
        public static float DistanceScore(float distance, float maxDistance)
        {
            return 1f - Normalize01(distance, maxDistance);
        }

        /// <summary>
        /// Bonus สำหรับค่าเลือดวิกฤต (smooth ไม่ใช่ boolean)
        /// ยิ่ง HP ต่ำกว่า threshold มาก ยิ่งได้คะแนนใกล้ 1 — ที่ threshold พอดีได้ 0
        /// </summary>
        public static float CriticalBonus(float healthPercent, float criticalThreshold)
        {
            if (criticalThreshold <= 0f) return 0f;
            return Mathf.Clamp01(1f - (healthPercent / criticalThreshold));
        }

        /// <summary>ค่าน้ำหนักที่ถูกต้อง: 0..1 เสมอ</summary>
        public static float ClampWeight(float weight)
        {
            return Mathf.Clamp01(weight);
        }
    }
}

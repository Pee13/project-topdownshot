using UnityEngine;

namespace TopDownTacticalAI.Tactical
{
    /// <summary>
    /// คำนวณตำแหน่งอ้อมข้างเป้าหมาย เพื่อใช้ตอน AI หลายตัวร่วมมือกันล้อมผู้เล่น (GroupAI)
    /// </summary>
    public static class Flanking
    {
        /// <param name="targetPosition">ตำแหน่งผู้เล่น</param>
        /// <param name="allyIndex">ลำดับของ AI ตัวนี้ในกลุ่ม (0, 1, 2, ...)</param>
        /// <param name="totalAllies">จำนวน AI ทั้งหมดที่กำลังล้อม</param>
        /// <param name="flankRadius">ระยะห่างจากเป้าหมายที่จะไปยืน</param>
        /// <param name="escapeDirection">
        /// ทิศที่ผู้เล่นกำลังหนี (velocity/ความจำ) — ถ้ามี วงล้อมจะ "เอียงไปทางทิศหนี"
        /// ตัวอ้อมจึงไปรอขวาง "ด้านหน้าทางหนี" แทนการยืนกระจายมั่ว = ปิดโอกาสหนีได้จริง
        /// (ส่ง Vector2.zero = ใช้วงกลมรอบทิศโลกแบบเดิม)
        /// </param>
        public static Vector2 GetFlankPosition(Vector2 targetPosition, int allyIndex, int totalAllies, float flankRadius, Vector2 escapeDirection = default)
        {
            if (totalAllies <= 0) totalAllies = 1;

            float angle;
            if (allyIndex > 0 && escapeDirection.sqrMagnitude > 0.01f)
            {
                // ── Intercept mode: จัดตำแหน่งสัมพัทธ์กับ "ทิศที่ผู้เล่นจะวิ่งหนี" ──
                // ตัวคี่อยู่ฝั่งขวาของทิศหนี ตัวคู่อยู่ฝั่งซ้าย ห่างเพิ่มขึ้นตามลำดับ
                // เช่น ล้อม 3 ตัว: ตัวแรกตรงหน้าทางหนี+60°, ตัวสอง -60° → ปิดปากทางทั้งสองฝั่ง
                float escapeHeading = Mathf.Atan2(escapeDirection.y, escapeDirection.x) * Mathf.Rad2Deg;
                float side = (allyIndex % 2 == 1) ? 1f : -1f;
                float magnitude = ((allyIndex + 1) / 2) * (180f / totalAllies);
                angle = escapeHeading + side * magnitude;
            }
            else
            {
                // ไม่รู้ทิศหนี → กระจายรอบวงตามเดิม
                float angleStep = 360f / totalAllies;
                angle = angleStep * allyIndex;
            }

            float rad = angle * Mathf.Deg2Rad;

            Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * flankRadius;
            return targetPosition + offset;
        }
    }
}

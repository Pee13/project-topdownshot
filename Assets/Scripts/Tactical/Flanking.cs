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
        public static Vector2 GetFlankPosition(Vector2 targetPosition, int allyIndex, int totalAllies, float flankRadius)
        {
            if (totalAllies <= 0) totalAllies = 1;

            float angleStep = 360f / totalAllies;
            float angle = angleStep * allyIndex;
            float rad = angle * Mathf.Deg2Rad;

            Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * flankRadius;
            return targetPosition + offset;
        }
    }
}

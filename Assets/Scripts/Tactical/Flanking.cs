using UnityEngine;

namespace TopDownTacticalAI.Tactical
{
    public static class Flanking
    {
        /// <summary>
        /// คำนวณตำแหน่งโอบล้อม (Flanking) ตามลำดับคิวของตัวละครใน GroupAI
        /// </summary>
        /// <param name="targetPos">ตำแหน่งเป้าหมาย (ผู้เล่น)</param>
        /// <param name="chaserIndex">ลำดับของ AI ตัวนี้ในกลุ่ม (0, 1, 2...)</param>
        /// <param name="totalChasers">จำนวน AI ทั้งหมดที่กำลังไล่ล่า</param>
        /// <param name="flankRadius">ระยะห่างในการตีวงอ้อม</param>
        public static Vector2 GetFlankPosition(Vector2 targetPos, int chaserIndex, int totalChasers, float flankRadius)
        {
            // ถ้ามีตัวเดียว หรือเป็นตัวนำทัพ (index 0) ให้เดินตรงหาผู้เล่น
            if (totalChasers <= 1 || chaserIndex <= 0)
                return targetPos;

            // กระจายมุมโอบซ้าย-ขวา: ตัวคี่ฉีกออกซ้าย (+องศา) ตัวคู่ฉีกออกขวา (-องศา)
            int pairIndex = (chaserIndex + 1) / 2;
            float sign = (chaserIndex % 2 == 1) ? 1f : -1f;
            float angleDegrees = sign * (50f + (pairIndex - 1) * 35f);

            // คำนวณพิกัดจุดโอบรอบตัวผู้เล่น
            float angleRad = angleDegrees * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad)) * flankRadius;

            return targetPos + offset;
        }

        /// <summary>
        /// คำนวณจุดโอบด้านข้างหรือด้านหลังแบบอ้างอิงทิศทางผู้เล่น
        /// </summary>
        public static Vector2 CalculateFlankPosition(Vector2 flankerPos, Vector2 playerPos, Vector2 playerForward, float flankRadius)
        {
            Vector2 toFlanker = (flankerPos - playerPos).normalized;
            Vector2 playerRight = new Vector2(playerForward.y, -playerForward.x);
            bool isOnRightSide = Vector2.Dot(toFlanker, playerRight) > 0f;
            float flankAngle = isOnRightSide ? 115f : -115f;

            Quaternion rot = Quaternion.Euler(0, 0, flankAngle);
            Vector2 flankDirection = rot * (-playerForward);

            return playerPos + (flankDirection.normalized * flankRadius);
        }
    }
}
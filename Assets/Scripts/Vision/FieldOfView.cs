using UnityEngine;

namespace TopDownTacticalAI.Vision
{
    /// <summary>
    /// คำนวณว่าจุดเป้าหมายอยู่ในมุมมอง (Field of View) ของศัตรูหรือไม่
    /// </summary>
    public static class FieldOfView
    {
        /// <param name="forward">ทิศทางที่ศัตรูหันหน้าอยู่</param>
        /// <param name="toTarget">เวกเตอร์จากศัตรูไปยังเป้าหมาย</param>
        /// <param name="fovAngle">มุมมองรวม (องศา) เช่น 90 = ซ้าย 45 ขวา 45</param>
        public static bool IsWithinView(Vector2 forward, Vector2 toTarget, float fovAngle)
        {
            float angle = Vector2.Angle(forward, toTarget);
            return angle <= fovAngle * 0.5f;
        }
    }
}

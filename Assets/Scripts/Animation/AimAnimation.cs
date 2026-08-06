using UnityEngine;

namespace TopDownTacticalAI.Animation
{
    /// <summary>
    /// คำนวณพารามิเตอร์สำหรับ Animation การเล็ง เช่น ทิศทางของแขน/ปืนเทียบกับลำตัว
    /// </summary>
    public static class AimAnimation
    {
        /// <returns>มุม (-180 ถึง 180) ระหว่างทิศทางที่ตัวละครหันหน้ากับทิศทางที่เล็ง</returns>
        public static float GetAimAngleOffset(Vector2 bodyForward, Vector2 aimDirection)
        {
            float bodyAngle = Mathf.Atan2(bodyForward.y, bodyForward.x) * Mathf.Rad2Deg;
            float aimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(bodyAngle, aimAngle);
        }
    }
}

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
    }
}

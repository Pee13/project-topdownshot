using UnityEngine;

namespace TopDownTacticalAI.Dodge
{
    /// <summary>
    /// หาตำแหน่งปลอดภัยที่ตั้งฉากกับแนวกระสุนเพื่อใช้หลบ
    /// </summary>
    public static class SafePositionFinder
    {
        public static Vector2 GetDodgeDirection(Vector2 bulletVelocity)
        {
            Vector2 perpendicular = new Vector2(-bulletVelocity.y, bulletVelocity.x).normalized;
            // สุ่มว่าจะหลบซ้ายหรือขวา
            return Random.value > 0.5f ? perpendicular : -perpendicular;
        }
    }
}

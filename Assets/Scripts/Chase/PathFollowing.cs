using UnityEngine;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Chase
{
    /// <summary>
    /// เก็บไว้เพื่อความเข้ากันได้ย้อนหลัง — Logic จริงย้ายไปอยู่ที่ SteeringMovement (Whisker Raycast)
    /// ซึ่งเลี่ยงกำแพง/มุมอับได้ดีกว่าเวอร์ชันเดิมที่เช็คแค่เส้นตรงเส้นเดียว
    /// </summary>
    public static class PathFollowing
    {
        public static Vector2 GetSteeredDirection(Vector2 origin, Vector2 destination, LayerMask obstacleMask, float avoidDistance = 1f)
        {
            Vector2 desiredDirection = (destination - origin).normalized;
            return SteeringMovement.GetSteeredDirection(origin, desiredDirection, obstacleMask, avoidDistance);
        }
    }
}

using UnityEngine;

namespace TopDownTacticalAI.DebugTools
{
    /// <summary>
    /// รวมฟังก์ชันวาด Gizmos อเนกประสงค์ที่ใช้ซ้ำได้หลายระบบ
    /// </summary>
    public static class GizmosDrawer
    {
        public static void DrawCircle(Vector3 center, float radius, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(center, radius);
        }

        public static void DrawArrow(Vector3 from, Vector3 direction, Color color, float headSize = 0.2f)
        {
            Gizmos.color = color;
            Vector3 to = from + direction;
            Gizmos.DrawLine(from, to);

            Vector3 right = Quaternion.Euler(0, 0, 150) * direction.normalized * headSize;
            Vector3 left = Quaternion.Euler(0, 0, -150) * direction.normalized * headSize;
            Gizmos.DrawLine(to, to + right);
            Gizmos.DrawLine(to, to + left);
        }
    }
}

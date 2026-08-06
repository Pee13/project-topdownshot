using UnityEngine;

namespace TopDownTacticalAI.Utilities
{
    public static class ExtensionMethods
    {
        public static Vector2 XY(this Vector3 v) => new Vector2(v.x, v.y);

        public static Vector2 To2D(this Transform t) => t.position.XY();

        /// <summary>หมุน Vector2 ทวนเข็ม/ตามเข็มตามองศาที่กำหนด</summary>
        public static Vector2 Rotate(this Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(rad);
            float cos = Mathf.Cos(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }
}

using UnityEngine;

namespace TopDownTacticalAI.Vision
{
    /// <summary>
    /// ค้นหา Target (ผู้เล่น) ที่อยู่ในรัศมีด้วย OverlapCircle
    /// </summary>
    public static class TargetDetector
    {
        public static Transform FindTargetInRadius(Vector2 origin, float radius, LayerMask targetMask)
        {
            Collider2D hit = Physics2D.OverlapCircle(origin, radius, targetMask);
            return hit != null ? hit.transform : null;
        }
    }
}

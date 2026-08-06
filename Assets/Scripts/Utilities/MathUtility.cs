using UnityEngine;

namespace TopDownTacticalAI.Utilities
{
    public static class MathUtility
    {
        public static Vector2 RandomPointInRadius(Vector2 center, float radius)
        {
            return center + Random.insideUnitCircle * radius;
        }

        public static float RemapClamped(float value, float inMin, float inMax, float outMin, float outMax)
        {
            float t = Mathf.InverseLerp(inMin, inMax, value);
            return Mathf.Lerp(outMin, outMax, t);
        }

        public static Vector2 DirectionFromAngle(float angleDegrees)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}

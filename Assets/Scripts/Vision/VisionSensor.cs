using UnityEngine;
using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.Vision
{
    /// <summary>
    /// ระบบการมองเห็นหลักของศัตรู
    /// รวม TargetDetector + FieldOfView + RaycastDetector เข้าด้วยกัน
    /// แล้วอัปเดตผลลง Blackboard ทุกเฟรม
    /// </summary>
    public class VisionSensor : MonoBehaviour
    {
        [Header("Vision Settings")]
        public float ViewRadius = 8f;
        [Range(0, 360)] public float ViewAngle = 90f;
        public LayerMask TargetMask;
        public LayerMask ObstacleMask;

        [Header("Reference")]
        public Transform EyePoint;

        private Blackboard _blackboard;

        public void Initialize(Blackboard blackboard)
        {
            _blackboard = blackboard;
        }

        public void Tick()
        {
            Vector2 origin = EyePoint != null ? (Vector2)EyePoint.position : (Vector2)transform.position;
            Transform target = TargetDetector.FindTargetInRadius(origin, ViewRadius, TargetMask);

            if (target == null)
            {
                _blackboard.CanSeeTarget = false;
                return;
            }

            Vector2 toTarget = (Vector2)target.position - origin;
            bool inAngle = FieldOfView.IsWithinView(transform.up, toTarget, ViewAngle) || FieldOfView.IsWithinView(transform.right, toTarget, ViewAngle);
            bool hasLoS = RaycastDetector.HasLineOfSight(origin, target.position, ObstacleMask, ViewRadius);

            bool canSee = inAngle && hasLoS;

            _blackboard.CanSeeTarget = canSee;
            _blackboard.DistanceToTarget = toTarget.magnitude;

            if (canSee)
            {
                _blackboard.CurrentTarget = target;
            }
        }
    }
}

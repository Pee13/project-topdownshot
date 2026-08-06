using UnityEngine;

namespace TopDownTacticalAI.Combat
{
    /// <summary>
    /// หมุนตัวศัตรู (หรือปืน) ให้เล็งไปทางเป้าหมายอย่างนุ่มนวล
    /// </summary>
    public class AimController
    {
        private readonly Transform _aimTransform;
        private readonly float _turnSpeed;

        public AimController(Transform aimTransform, float turnSpeed = 360f)
        {
            _aimTransform = aimTransform;
            _turnSpeed = turnSpeed;
        }

        public void AimAt(Vector2 targetPosition, float deltaTime)
        {
            Vector2 direction = (targetPosition - (Vector2)_aimTransform.position).normalized;
            if (direction.sqrMagnitude < 0.0001f) return;

            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            float currentAngle = _aimTransform.eulerAngles.z;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, _turnSpeed * deltaTime);

            _aimTransform.rotation = Quaternion.Euler(0, 0, newAngle);
        }

        public bool IsAimedAt(Vector2 targetPosition, float toleranceDegrees = 5f)
        {
            Vector2 direction = (targetPosition - (Vector2)_aimTransform.position).normalized;
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            float diff = Mathf.DeltaAngle(_aimTransform.eulerAngles.z, targetAngle);
            return Mathf.Abs(diff) <= toleranceDegrees;
        }
    }
}

using UnityEngine;

namespace TopDownTacticalAI.Vision
{
    /// <summary>
    /// วาด Gizmos แสดงรัศมีและมุมมองของศัตรูใน Scene View เพื่อ Debug
    /// </summary>
    [RequireComponent(typeof(VisionSensor))]
    public class VisionDebugger : MonoBehaviour
    {
        private VisionSensor _vision;

        private void Awake()
        {
            _vision = GetComponent<VisionSensor>();
        }

        private void OnDrawGizmos()
        {
            if (_vision == null) _vision = GetComponent<VisionSensor>();
            if (_vision == null) return;

            Vector3 origin = _vision.EyePoint != null ? _vision.EyePoint.position : transform.position;

            Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(origin, _vision.ViewRadius);

            Vector3 forward = transform.up;
            float halfAngle = _vision.ViewAngle * 0.5f;

            Quaternion leftRot = Quaternion.Euler(0, 0, halfAngle);
            Quaternion rightRot = Quaternion.Euler(0, 0, -halfAngle);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin, origin + leftRot * forward * _vision.ViewRadius);
            Gizmos.DrawLine(origin, origin + rightRot * forward * _vision.ViewRadius);
        }
    }
}

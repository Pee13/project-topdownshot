using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Vision;

namespace TopDownTacticalAI.DebugTools
{
    /// <summary>
    /// แสดงระยะและมุมมอง (Field of View) ของศัตรูแบบเรียลไทม์ เป็นเส้นจริงในโลกเกม (LineRenderer)
    /// เห็นได้ทันทีใน Game View ตอน Play (ต่างจาก VisionDebugger เดิมที่วาดผ่าน Gizmos เห็นแค่ Scene View)
    ///
    /// สีของเส้นบอกสถานะปัจจุบัน:
    /// - สีเหลืองจาง = ยังไม่เห็นผู้เล่น (ปกติ)
    /// - สีแดงสด = "เห็นผู้เล่นอยู่ตอนนี้" (CanSeeTarget = true)
    ///
    /// วิธีใช้: แปะสคริปต์นี้ที่ตัวศัตรู (ต้องมี EnemyBrain + VisionSensor อยู่ด้วย) แล้วกด Play
    /// </summary>
    [RequireComponent(typeof(EnemyBrain))]
    public class VisionRangeDebugDisplay : MonoBehaviour
    {
        [Header("เปิด/ปิด")]
        public bool ShowVisionCone = true;

        [Header("สไตล์")]
        public float LineWidth = 0.04f;
        [Tooltip("จำนวนจุดที่ใช้วาดส่วนโค้งของมุมมอง ยิ่งเยอะยิ่งโค้งเนียน")]
        public int ArcResolution = 20;
        public Color NormalColor = new Color(1f, 1f, 0.2f, 0.5f);
        public Color SeeingTargetColor = new Color(1f, 0.15f, 0.15f, 0.85f);

        private EnemyBrain _brain;
        private VisionSensor _vision;
        private LineRenderer _coneOutline;
        private LineRenderer _sightLine; // เส้นตรงไปยังผู้เล่น เฉพาะตอนเห็นอยู่จริง

        private void Awake()
        {
            _brain = GetComponent<EnemyBrain>();
            _vision = GetComponent<VisionSensor>();

            _coneOutline = CreateLineRenderer("VisionCone_Outline", ArcResolution + 3, true);
            _sightLine = CreateLineRenderer("VisionCone_SightLine", 2, false);
        }

        private LineRenderer CreateLineRenderer(string name, int pointCount, bool loop)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform, false);

            LineRenderer lr = obj.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.widthMultiplier = LineWidth;
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.positionCount = pointCount;
            lr.numCapVertices = 2;
            lr.sortingOrder = 9;
            return lr;
        }

        private void LateUpdate()
        {
            if (_vision == null || _brain == null) return;

            if (!ShowVisionCone)
            {
                _coneOutline.positionCount = 0;
                _sightLine.positionCount = 0;
                return;
            }

            Blackboard blackboard = _brain.GetBlackboard();
            bool canSeeNow = blackboard.CanSeeTarget;
            Color activeColor = canSeeNow ? SeeingTargetColor : NormalColor;

            Vector3 origin = _vision.EyePoint != null ? _vision.EyePoint.position : transform.position;
            DrawVisionCone(origin, activeColor);

            if (canSeeNow && blackboard.CurrentTarget != null)
            {
                _sightLine.positionCount = 2;
                _sightLine.SetPosition(0, origin);
                _sightLine.SetPosition(1, blackboard.CurrentTarget.position);
                _sightLine.startColor = SeeingTargetColor;
                _sightLine.endColor = SeeingTargetColor;
            }
            else
            {
                _sightLine.positionCount = 0;
            }
        }

        /// <summary>วาดรูปพัด (Pie Slice) แทนขอบเขตมุมมองจริงของศัตรู แทนวงกลมเต็มวง+เส้นสองข้างแบบเดิม</summary>
        private void DrawVisionCone(Vector3 origin, Color color)
        {
            int totalPoints = ArcResolution + 3; // origin -> จุดโค้ง N จุด -> origin (ปิดรูป)
            Vector3[] points = new Vector3[totalPoints];

            float halfAngle = _vision.ViewAngle * 0.5f;
            Vector3 forward = transform.up;

            points[0] = origin;

            for (int i = 0; i <= ArcResolution; i++)
            {
                float t = (float)i / ArcResolution;
                float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
                Vector3 dir = Quaternion.Euler(0, 0, angle) * forward;
                points[i + 1] = origin + dir * _vision.ViewRadius;
            }

            points[totalPoints - 1] = origin;

            _coneOutline.positionCount = totalPoints;
            _coneOutline.SetPositions(points);
            _coneOutline.startColor = color;
            _coneOutline.endColor = color;
        }
    }
}

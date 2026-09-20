using System.Collections.Generic;
using UnityEngine;
using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.DebugTools
{
    /// <summary>
    /// วาดเส้นทางที่ AI "กำลังตั้งใจจะไป" (Destination Line) และ "เส้นทางที่เดินผ่านมาแล้ว" (Trail)
    /// เป็นเส้นจริงในโลกเกม (ใช้ LineRenderer) จึงเห็นได้ใน Game View ตอน Play ทันที
    /// ไม่ต้องพึ่ง Gizmos/Scene View เหมือน Debug Tool ตัวอื่นๆ (VisionDebugger, InfluenceMapDebugDisplay)
    ///
    /// สีของเส้นจะเปลี่ยนตาม State ปัจจุบัน (เขียวเทา=ลาดตระเวน, ส้ม=ไล่ตาม, เหลือง=ค้นหา, แดง=ปะทะ,
    /// ฟ้า=หลบกำบัง, ชมพู=หลบกระสุน) ให้เข้าใจง่ายว่า "ตอนนี้มันคิดว่าตัวเองกำลังทำอะไรอยู่ ถึงเดินไปทางนั้น"
    ///
    /// วิธีใช้: แปะสคริปต์นี้ที่ตัวศัตรู (ต้องมี EnemyBrain อยู่ด้วย) แล้วกด Play — ไม่ต้องตั้งค่าอะไรเพิ่ม
    /// </summary>
    [RequireComponent(typeof(EnemyBrain))]
    public class AIPathVisualizer : MonoBehaviour
    {
        [Header("เปิด/ปิดการแสดงผล")]
        public bool ShowDestinationLine = true;
        public bool ShowTrail = true;

        [Header("เส้นบอกจุดหมาย (Destination Line)")]
        [Tooltip("ความกว้างของเส้นตรงไปยังจุดหมายปัจจุบัน")]
        public float DestinationLineWidth = 0.08f;

        [Header("รอยเดิน (Trail)")]
        [Tooltip("จำนวนจุดสูงสุดที่จะเก็บไว้ในรอยเดิน ยิ่งเยอะยิ่งเห็นเส้นทางย้อนหลังไกลขึ้น")]
        public int MaxTrailPoints = 40;
        [Tooltip("ระยะห่างขั้นต่ำก่อนจะบันทึกจุดใหม่ลงรอยเดิน (กันจุดถี่เกินตอนเดินช้า)")]
        public float MinPointSpacing = 0.15f;
        public float TrailLineWidth = 0.05f;

        private EnemyBrain _brain;
        private LineRenderer _destinationLine;
        private LineRenderer _trailLine;
        private readonly List<Vector3> _trailPoints = new List<Vector3>();

        private static readonly Dictionary<EnemyState, Color> StateColors = new Dictionary<EnemyState, Color>
        {
            { EnemyState.Patrol, new Color(0.6f, 0.6f, 0.6f) },
            { EnemyState.Suspicious, new Color(1f, 0.75f, 0.1f) },
            { EnemyState.Chase, new Color(1f, 0.65f, 0f) },
            { EnemyState.Search, new Color(1f, 0.9f, 0.2f) },
            { EnemyState.Combat, new Color(1f, 0.25f, 0.25f) },
            { EnemyState.Cover, new Color(0.3f, 0.6f, 1f) },
            { EnemyState.Dodge, new Color(1f, 0.2f, 0.8f) },
        };

        private void Awake()
        {
            _brain = GetComponent<EnemyBrain>();
            _destinationLine = CreateLineRenderer("AIPath_Destination", DestinationLineWidth);
            _trailLine = CreateLineRenderer("AIPath_Trail", TrailLineWidth);
            _trailPoints.Add(transform.position);
        }

        private LineRenderer CreateLineRenderer(string name, float width)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform, false);

            LineRenderer lr = obj.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.widthMultiplier = width;
            lr.useWorldSpace = true;
            lr.positionCount = 0;
            lr.numCapVertices = 4;
            lr.sortingOrder = 10;
            lr.receiveShadows = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        private void LateUpdate()
        {
            if (_brain == null) return;

            AIDebugInfo info = _brain.GetDebugInfo();
            Color stateColor = StateColors.TryGetValue(info.State, out Color c) ? c : Color.white;

            if (ShowDestinationLine)
                UpdateDestinationLine(stateColor);
            else
                _destinationLine.positionCount = 0;

            if (ShowTrail)
                UpdateTrail(stateColor);
            else
                _trailLine.positionCount = 0;
        }

        /// <summary>วาดเส้นตรงจากตัว AI ไปยัง Blackboard.CurrentDestination ที่ State ปัจจุบันตั้งใจจะไป</summary>
        private void UpdateDestinationLine(Color stateColor)
        {
            // กันพัง: Blackboard ถูกสร้างใน EnemyBrain.Awake() ถ้าสคริปต์นั้นถูกปิดอยู่
            // หรือยังไม่ทันรัน Awake ค่าจะเป็น null ทำให้เกิด NullReferenceException ทุกเฟรม
            var blackboard = _brain.GetBlackboard();
            if (blackboard == null)
            {
                _destinationLine.positionCount = 0;
                return;
            }

            Vector2 destination = blackboard.CurrentDestination;

            if (destination == Vector2.zero)
            {
                _destinationLine.positionCount = 0;
                return;
            }

            _destinationLine.positionCount = 2;
            _destinationLine.SetPosition(0, transform.position);
            _destinationLine.SetPosition(1, destination);

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(stateColor, 0f), new GradientColorKey(stateColor, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.35f, 1f) }
            );
            _destinationLine.colorGradient = gradient;
        }

        /// <summary>บันทึกและวาดรอยเดินจริงที่ผ่านมา (จางลงเรื่อยๆ ยิ่งเก่ายิ่งจาง)</summary>
        private void UpdateTrail(Color stateColor)
        {
            Vector3 currentPos = transform.position;

            if (_trailPoints.Count == 0 || Vector3.Distance(_trailPoints[_trailPoints.Count - 1], currentPos) >= MinPointSpacing)
            {
                _trailPoints.Add(currentPos);
                if (_trailPoints.Count > MaxTrailPoints)
                    _trailPoints.RemoveAt(0);
            }

            _trailLine.positionCount = _trailPoints.Count;
            _trailLine.SetPositions(_trailPoints.ToArray());

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(stateColor, 0f), new GradientColorKey(stateColor, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 1f) }
            );
            _trailLine.colorGradient = gradient;
        }
    }
}

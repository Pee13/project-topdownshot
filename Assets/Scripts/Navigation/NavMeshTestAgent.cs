using UnityEngine;
using UnityEngine.AI;

namespace TopDownTacticalAI.Pathfinding
{
    /// <summary>
    /// ตัวทดสอบ NavMesh 2D — เดินไล่ตามเป้าหมาย (Player) ด้วย NavMeshAgent แล้ววาดเส้นทางให้เห็น
    /// สร้างผ่านเมนู: Tools > NavMesh 2D > Setup + Bake (TEST Scene)
    ///
    /// ใช้ยืนยันว่า NavMesh ทำงานครบทุกด้าน:
    /// - กล่องสถานะมุมซ้ายบน: Complete = หาเส้นทางถึง, Partial = ถึงบางส่วน, Invalid = หาไม่เจอ, OFF MESH = ยังไม่ได้ยืนบน NavMesh
    /// - เส้นสีฟ้า = เส้นทางที่ NavMesh คำนวณ (ต้องอ้อมกำแพง ไม่ทะลุ)
    /// - ตัวทดสอบเดินเข้าหาผู้เล่นตลอดเวลา — วิ่งหลบดูว่ามันอ้อมกำแพงตามได้จริงไหม
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NavMeshTestAgent : MonoBehaviour
    {
        [Tooltip("เป้าหมายที่จะเดินไปหา — ถ้าเว้นว่าง จะหาจาก Tag Player อัตโนมัติ")]
        public Transform Target;

        [Tooltip("สั่งหาเส้นทางใหม่ทุกกี่วินาที")]
        public float RepathInterval = 0.35f;

        [Header("เส้นทาง (LineRenderer)")]
        public bool ShowPathLine = true;
        public Color PathColor = new Color(0.25f, 0.85f, 1f, 0.95f);
        public float PathWidth = 0.35f;

        private NavMeshAgent _agent;
        private LineRenderer _line;
        private float _repathTimer;
        private string _lastStatusText = "";

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();

            // สำคัญมากสำหรับ NavMesh ระนาบ XY (เกม2D): ห้ามให้ Agent หมุน/เอียงตัวละครตามระนาบ navmesh
            // ไม่งั้น sprite จะพลิกคว่ำ — updateUpAxis คือบั๊กแกน Y ตอนหมุน surface 90° (แก้แล้วใน Unity 6)
            _agent.updateRotation = false;
            _agent.updateUpAxis = false;

            if (ShowPathLine)
                CreatePathLine();
        }

        private void Start()
        {
            ResolveTarget();
            SnapToNavMesh();
        }

        private void Update()
        {
            if (Target == null)
            {
                ResolveTarget();
                if (Target == null) return;
            }

            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f)
            {
                _repathTimer = RepathInterval;

                if (!_agent.isOnNavMesh && !SnapToNavMesh()) return;

                _agent.SetDestination(Target.position);

                string status = _agent.isOnNavMesh
                    ? $"NavMesh: {_agent.pathStatus} | dist: {_agent.remainingDistance:F0}"
                    : "NavMesh: OFF MESH";
                if (status != _lastStatusText)
                {
                    _lastStatusText = status;
                    Debug.Log($"[NavMeshTestAgent] {status}", this);
                }
            }

            DrawPathLine();
        }

        private void ResolveTarget()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                Target = player.transform;
        }

        /// <summary>ดีดตัวกลับขึ้น NavMesh ถ้าตำแหน่งปัจจุบันไม่อยู่บนพื้นที่ bake (กัน SetDestination fail ตั้งแต่เฟรมแรก)</summary>
        private bool SnapToNavMesh()
        {
            if (_agent.isOnNavMesh) return true;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 100f, NavMesh.AllAreas))
            {
                bool warped = _agent.Warp(hit.position);
                if (!warped)
                    Debug.LogWarning("[NavMeshTestAgent] Warp ล้มเหลว", this);
                return warped;
            }

            Debug.LogError("[NavMeshTestAgent] หา NavMesh ไม่เจอในรัศมี 100 หน่วยจาก " + transform.position +
                           " — ยังไม่ได้ Bake หรือไม่ได้กด Play หลังรันเมนู Setup", this);
            return false;
        }

        private void CreatePathLine()
        {
            var lineGo = new GameObject("PathLine");
            lineGo.transform.SetParent(transform, false);
            _line = lineGo.AddComponent<LineRenderer>();
            _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.startColor = _line.endColor = PathColor;
            _line.widthMultiplier = PathWidth;
            _line.positionCount = 0;
            _line.sortingOrder = 60;
            _line.useWorldSpace = true;
        }

        private void DrawPathLine()
        {
            if (_line == null) return;

            if (_agent.isOnNavMesh && _agent.hasPath && _agent.path.corners.Length > 1)
            {
                Vector3[] corners = _agent.path.corners; // คืน array ใหม่ทุกครั้ง แก้ค่าได้ปลอดภัย
                _line.positionCount = corners.Length;
                for (int i = 0; i < corners.Length; i++)
                {
                    corners[i].z = transform.position.z - 0.1f; // ดึงเส้นมาใกล้กล้อง กันถูก sprite บัง
                    _line.SetPosition(i, corners[i]);
                }
            }
            else
            {
                _line.positionCount = 0;
            }
        }

        private void OnGUI()
        {
            // ใช้อังกฤษเพราะ IMGUI ฟอนต์ built-in แสดงไทยเป็นสี่เหลี่ยม (ตามที่บันทึกไว้ในโปรเจกต์)
            string txt = _agent == null
                ? "NavMesh: (no agent)"
                : _agent.isOnNavMesh
                    ? $"NavMesh: {_agent.pathStatus}  |  dist: {_agent.remainingDistance:F0}"
                    : "NavMesh: OFF MESH (snap failed)";
            GUI.Box(new Rect(10, 10, 340, 26), txt);
        }
    }
}

using UnityEngine;
using UnityEngine.AI;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Pathfinding
{
    /// <summary>
    /// ตัวกลางระหว่าง State กับระบบนำทาง (Hybrid Movement)
    ///
    /// หลักการ: แต่ละ State "สั่งเดินไปจุดหมาย" ผ่าน MoveTo() เดียว — ไม่ต้องรู้ว่า
    /// ตอนนี้กำลังเดินด้วย NavMesh หรือ Steering
    ///   - ฉากมี NavMesh (ตรวจตอน Initialize): ใช้ NavMeshAgent หาเส้นทางอ้อมกำแพงถูกต้อง
    ///     (ระดับระยะไกล — Patrol/Chase/Search/SeekHeal)
    ///   - ไม่มี NavMesh / จุดหมายนอกพื้นที่ bake / Agent เสีย: fallback เป็น SteeringMovement
    ///     (whisker raycast) เหมือนพฤติกรรมเดิมทุกประการ — ไม่มี mesh = ไม่ต่างจากก่อนทำแผนนี้
    ///
    /// Single-writer invariant (สำคัญมาก):
    ///   - IsPathActive = true ในเฟรมนั้น → เฉพาะ NavMeshAgent ที่เขียน transform
    ///     (EnemyBrain จะข้าม anti-wall clamps เพื่อไม่ให้สองระบบแย่งกัน)
    ///   - IsPathActive = false → เฉพาะ Steering ที่เขียน transform (Agent ถูก ResetPath ไว้เงียบ)
    ///   - State ที่ไม่ใช่ path-eligible (Dodge/Combat/Cover/Flank/...) เขียนตำแหน่งเองตามเดิม
    ///     โดย OnStateChanged/EndFrame จะสั่ง Agent หยุดเสมอ
    ///
    /// ยืมแนวทางจาก NavMeshTestAgent.cs (updateRotation/updateUpAxis = false, Warp ดีดขึ้น mesh)
    /// </summary>
    public sealed class AgentPathMover
    {
        /// <summary>State ที่ให้ NavMesh นำทาง (เดินไกล อ้อมกำแพงต้องถูก) — ส่วนอื่นใช้ steering เดิม</summary>
        public static bool IsPathEligible(EnemyState state)
        {
            return state == EnemyState.Patrol
                || state == EnemyState.Chase
                || state == EnemyState.Search
                || state == EnemyState.SeekHeal
                || state == EnemyState.HealAlly; // ฮีลเลอร์เดินข้ามแมพไปหาเพื่อนบาดเจ็บ — เคยติดกำาวบ่อยที่สุด
        }

        /// <summary>สั่ง Agent หยุดเส้นทางทันที — เรียกก่อนที่ state จะเขียน transform เองโดยตรง (กันแย่งกันเขียนในเฟรมเดียว)</summary>
        public void StopPath()
        {
            if (_agent != null && _agent.hasPath) _agent.ResetPath();
        }

        private readonly Transform _self;
        private readonly LayerMask _obstacleMask;
        private readonly float _personalSpace;

        private NavMeshAgent _agent;
        private readonly StuckDetector _stuckDetector = new StuckDetector();

        /// <summary>true = ตรวจพบ NavMesh และติด NavMeshAgent แล้ว (ใช้โหมดเส้นทางได้)</summary>
        public bool PathAvailable { get; private set; }

        /// <summary>true = ในเฟรมนี้ตำแหน่งถูกขับเคลื่อนโดย NavMeshAgent (ห้ามระบบอื่นเขียน/ clamp)</summary>
        public bool IsPathActive { get; private set; }

        private bool _movedThisFrame;
        private Vector2 _lastRequestedDest;
        private float _repathTimer;
        private float _lastSyncedSpeed = -1f;
        private float _lastArriveThreshold = -1f;
        private int _consecutiveFails;
        private bool _loggedFallback;

        private const float RepathInterval = 0.25f;      // หาเส้นทางใหม่ทุก ๆ 0.25 วิ (หรือเมื่อเป้าหมายขยับ)
        private const float DestMoveThreshold = 1f;       // เป้าหมายขยับเกินนี้ (หน่วย) = สั่งเส้นทางใหม่ทันที
        private const float NavSearchRadius = 25f;        // รัศมีค้น NavMesh ตอน Warp/ตรวจตอน Start
        private const float DestSampleRadius = 3f;        // รัศมีหาจุดบน mesh สำหรับเป้าหมายที่อาจอยู่นอก mesh
        private const int MaxConsecutiveFails = 3;        // SetDestination ล้มเหลวติดกันเท่านี้ = ปิดโหมดเส้นทางถาวร
        private const float TurnSpeedDegrees = 720f;      // ความเร็วหมุนหาทิศ (องศา/วินาที) — หมุนเนียน ไม่ snap ทีเดียว
                                                          // (แก้ "สะบัดรัว": เดิมตั้ง Euler ตรง ๆ ทุกเฟรม → เห็นการกระตุกชัดเจนตอนไล่ตาม)
        private const float SeparationSearchRadius = 4f;  // รัศมีตรวจเพื่อนชิดตัวในโหมดเส้นทาง (แยกเส้นทางออกจากกัน)
        private const float SeparationNudge = 2.5f;       // ระยะขยับเป้าหมายออกห่างเพื่อนสูงสุด (หน่วย)

        private TopDownTacticalAI.Core.EnemyBrain _brainCache; // lazy — ใช้เช็ค IsHealPatient ตอน sync ความเร็ว

        public AgentPathMover(Transform self, LayerMask obstacleMask, float personalSpace)
        {
            _self = self;
            _obstacleMask = obstacleMask;
            _personalSpace = personalSpace;
        }

        /// <summary>
        /// เรียกครั้งเดียวจาก EnemyBrain.Start (หลัง Awake ของทุก object แล้ว เพื่อให้ NavMesh data พร้อม)
        /// ตรวจพบ mesh → เพิ่ม NavMeshAgent + Warp ขึ้นไปยืนบน mesh
        /// ไม่พบ → PathAvailable = false = โหมด fallback ล้วน ไม่มีค่าใช้จ่ายต่อเฟรมเพิ่ม
        /// </summary>
        public void Initialize()
        {
            if (PathAvailable || _agent != null) return;

            if (!NavMesh2DUtility.TrySamplePosition(_self.position, NavSearchRadius, out Vector3 navPos))
                return; // ไม่มี NavMesh ในฉาก — ใช้ steering เหมือนเดิม ไม่ log กันรบกวนทุกตัว

            // สำคัญ: ดีดตัวไปตำแหน่งบน navmesh "ก่อน" AddComponent
            // Unity ผูก agent กับ transform.position ปัจจุบันตอน AddComponent —
            // ถ้ายังไม่อยู่บน mesh จะโยน "Failed to create agent because it is not close enough to the NavMesh"
            // (ถึงท้ายจะ Warp สำเร็จก็จริง แต่ error สีแดงขึ้น console ทุกตัวที่เกิดห่าง mesh)
            float preZ = _self.position.z;
            _self.position = new Vector3(navPos.x, navPos.y, preZ);

            _agent = _self.gameObject.GetComponent<NavMeshAgent>();
            if (_agent == null)
                _agent = _self.gameObject.AddComponent<NavMeshAgent>();

            // สำคัญมากสำหรับ NavMesh ระนาบ XY (เกม 2D): ห้ามให้ Agent หมุน/เอียงตัวละครตามระนาบ navmesh
            // ไม่งั้น sprite จะพลิกคว่ำ (updateUpAxis คือบั๊กแกน Y ตอนหมุน surface 90° — แก้แล้วใน Unity 6)
            _agent.agentTypeID = NavMesh2DUtility.GetCompatibleAgentTypeId();
            _agent.updateRotation = false;
            _agent.updateUpAxis = false;
            _agent.acceleration = 512f; // ตอบสนองทันทีเท่าที่ทำได้ (รักษาระดับกับ speed ที่ state ส่งมา)

            if (!_agent.Warp(navPos))
            {
                Debug.LogWarning($"[AgentPathMover] Warp ล้มเหลวที่ {navPos} — ใช้ Steering fallback แทน", _self);
                _agent = null;
                return;
            }
            _self.position = new Vector3(_self.position.x, _self.position.y, preZ); // คงระนาบ z=0 ของเกม 2D

            PathAvailable = true;
            _repathTimer = Random.value * RepathInterval; // สุ่ม offset กันศัตรูหลายตัว repath พร้อมกันทุกเฟรม
            _lastSyncedSpeed = -1f;
            _lastArriveThreshold = -1f;
        }

        /// <summary>เรียกทุกเฟรมก่อน State.Tick — รีเซ็ตธงเฟรมนี้</summary>
        public void BeginFrame()
        {
            IsPathActive = false;
            _movedThisFrame = false;
        }

        /// <summary>
        /// เรียกทุกเฟรมหลัง State.Tick — ถ้า state ที่ใช้เส้นทางไม่ได้สั่งเดินเฟรมนี้
        /// (เช่น Patrol หยุดรอที่จุด, Search มองซ้ายขวา) ให้สั่ง Agent หยุดนิ่ง
        /// กันไม่ให้วิ่งตามเส้นทางเก่าค้างไปเรื่อยๆ
        /// </summary>
        public void EndFrame()
        {
            if (_agent != null && !_movedThisFrame && _agent.isOnNavMesh && _agent.hasPath)
                _agent.ResetPath();
        }

        /// <summary>
        /// เปลี่ยน State → ถ้า state ใหม่ไม่ใช่ path-eligible ให้สั่ง Agent เงียบทันที
        /// เพื่อให้ state ใหม่เป็นผู้เขียน transform แต่เพียงผู้เดียวในเฟรมถัดไป
        /// </summary>
        public void OnStateChanged(EnemyState nextState)
        {
            if (_agent == null || !PathAvailable) return;

            if (!IsPathEligible(nextState) && _agent.hasPath)
                _agent.ResetPath();
        }

        /// <summary>
        /// สั่งเดินไป destination — จุดเดียวที่ state ต้องเรียกแทนการเขียน transform.position เอง
        /// โหมดเส้นทาง: ให้ NavMeshAgent หาทางอ้อมกำแพง / โหมด fallback: steering เดิม
        /// </summary>
        /// <param name="speed">ความเร็วของ state นี้ (role preset คำนวณแล้ว) — sync เข้า agent.speed ให้เอง</param>
        /// <param name="arriveThreshold">ถือว่าถึงจุดหมายเมื่ออยู่ใกล้เท่านี้ (หน่วย)</param>
        public void MoveTo(Vector2 destination, float deltaTime, float speed, float arriveThreshold = 0.3f)
        {
            _movedThisFrame = true;

            // Cooperative Waiting: เพื่อนที่เป็นเป้าฮีล (HP ≤30%) เดินช้าลงในโหมดเส้นทางด้วย
            // (เดิม damping ทำงานเฉพาะ state ที่เขียนตำแหน่งเอง — ตัวที่ขับด้วย agent ไม่โดนลดความเร็วเลย)
            if (_brainCache == null) _self.TryGetComponent(out _brainCache);
            if (_brainCache != null && _brainCache.IsHealPatient)
                speed *= TopDownTacticalAI.Core.EnemyBrain.PatientSpeedFactor;

            if (PathAvailable && TryPathMove(destination, deltaTime, speed, arriveThreshold))
            {
                IsPathActive = true;
                return;
            }

            // ── Fallback: steering เดิม (พฤติกรรมเดียวกับก่อนมี NavMesh) ──
            IsPathActive = false;
            if (_agent != null && _agent.hasPath)
                _agent.ResetPath(); // กันสองระบบแย่งกันเขียน transform ในเฟรมเดียว
            SteerMove(destination, deltaTime, speed);
        }

        /// <summary>
        /// คืน true ถ้า NavMeshAgent รับงานเดินในเฟรมนี้แล้ว
        /// คืน false เมื่อ: ไม่มี agent / หลุดจาก mesh (Warp ไม่สำเร็จ) / จุดหมายหาบน mesh ไม่เจอ /
        /// SetDestination ล้มเหลว — ผู้เรียกจะ fallback ไป steering
        /// </summary>
        private bool TryPathMove(Vector2 destination, float deltaTime, float speed, float arriveThreshold)
        {
            if (_agent == null || !PathAvailable) return false;

            if (!_agent.isOnNavMesh && !TryWarpToNavMesh())
            {
                DisablePathMode("ยืนไม่ติด NavMesh และ Warp ล้มเหลว");
                return false;
            }

            float dist = Vector2.Distance(_self.position, destination);
            if (dist <= arriveThreshold)
            {
                // ถึงจุดหมายแล้ว — หยุดนิ่ง (state จะจัดการเองว่าถึงแล้วทำอะไรต่อ เช่น หยุดรอ/เริ่มค้นหา)
                if (_agent.hasPath) _agent.ResetPath();
                _repathTimer = RepathInterval;
                return true;
            }

            // เป้าหมายอาจอยู่นอกพื้นที่ bake (ชิดกำแพง/นอกแมพ) — หาจุดบน mesh ที่ใกล้ที่สุดแทน
            // ── แยกเส้นทางออกจากเพื่อนที่ยืนชิด (แก้ "แทงค์+ซับเดินวนไปมา ขวางกันเอง") ──
            // NavMesh agent ในโหมดนี้ไม่มีแรงผลักออกจากกัน → ถ้าเพื่อนอยู่ใกล้ ให้ขยับเป้าหมายออกห่างเล็กน้อย
            // เส้นทางของสองตัวจะแยกจากกัน ไม่เดินชนกันจนค้างอยู่กับที่
            if (Tactical.GroupAI.Instance != null)
            {
                Vector2 push = Tactical.GroupAI.Instance.ComputeSeparationPush(
                    _self.position, _self, SeparationSearchRadius);
                if (push.sqrMagnitude > 0.0001f)
                    destination += push * SeparationNudge;
            }

            if (!NavMesh2DUtility.TrySamplePosition(destination, DestSampleRadius, out Vector3 resolved))
                return false; // หาไม่เจอ → steering จัดการแทน

            Vector2 resolved2 = resolved;
            _repathTimer -= deltaTime;

            bool needRepath = !_agent.hasPath
                || (resolved2 - _lastRequestedDest).sqrMagnitude > DestMoveThreshold * DestMoveThreshold
                || _repathTimer <= 0f;

            if (needRepath)
            {
                if (!_agent.SetDestination(resolved))
                {
                    _consecutiveFails++;
                    if (_consecutiveFails >= MaxConsecutiveFails)
                    {
                        DisablePathMode("SetDestination ล้มเหลวซ้ำ ๆ");
                        return false;
                    }
                    return false; // เฟรมนี้ใช้ steering ไปก่อน
                }

                _consecutiveFails = 0;
                _lastRequestedDest = resolved2;
                _repathTimer = RepathInterval;
            }

            // sync ค่าที่ state ส่งมา (เปลี่ยนเฉพาะเมื่อต่างจากเดิม — ไม่เขียนทุกเฟรม)
            if (_lastSyncedSpeed != speed)
            {
                _agent.speed = speed;
                _lastSyncedSpeed = speed;
            }
            if (_lastArriveThreshold != arriveThreshold)
            {
                _agent.stoppingDistance = arriveThreshold;
                _lastArriveThreshold = arriveThreshold;
            }

            FaceDirection(_agent.velocity, deltaTime);
            return true;
        }

        /// <summary>Steering เดิม: whisker raycast เลี่ยงกำแพง + StuckDetector สไลด์ข้าง + เช็คก่อนก้าว</summary>
        private void SteerMove(Vector2 destination, float deltaTime, float speed)
        {
            Vector2 current = _self.position;
            Vector2 desiredDirection = (destination - current).normalized;

            // แรงผลักจากพันธมิตร (Separation) + แรงผลักจากกำแพง — SteeringMovement จัดการให้ในตัว
            Vector2 direction = SteeringMovement.GetSteeredDirection(
                current, desiredDirection, _obstacleMask, self: _self, personalSpace: _personalSpace);

            _stuckDetector.Tick(current, deltaTime);
            if (_stuckDetector.IsStuck && direction.sqrMagnitude < 0.0001f)
            {
                // ติดมุมอับจริง ๆ (steering หาทางไม่ได้): สไลด์ออกด้านข้างแบบ deterministic
                Vector2 sideDir = Vector2.Perpendicular(desiredDirection);
                direction = Random.value > 0.5f ? sideDir : -sideDir;
            }

            // เดินแบบ "เช็คก่อนก้าว" — กันมุดกำแพง/ข้ามกำแพงในสเต็ปเดียว
            Vector2 step = direction * (speed * deltaTime);
            _self.position = SteeringMovement.MoveWithCollisionCheck(_self, step, _obstacleMask);

            FaceDirection(direction, deltaTime);
        }

        private bool TryWarpToNavMesh()
        {
            if (_agent == null) return false;
            if (!NavMesh2DUtility.TrySamplePosition(_self.position, NavSearchRadius, out Vector3 navPos))
                return false;

            float z = _self.position.z;
            if (!_agent.Warp(navPos)) return false;
            _self.position = new Vector3(_self.position.x, _self.position.y, z);
            return true;
        }

        private void DisablePathMode(string why)
        {
            PathAvailable = false;
            if (!_loggedFallback)
            {
                _loggedFallback = true;
                Debug.LogWarning($"[AgentPathMover] ปิดโหมด NavMesh ถาวร ({why}) — ตัวนี้ใช้ Steering fallback จนจบเกม", _self);
            }
        }

        /// <summary>
        /// หันหน้าตามทิศที่กำลังเคลื่อนที่ — แบบหมุนเนียน (MoveTowardsAngle 720°/วิ)
        /// แก้ "สะบัดรัว": เดิม snap Euler ตรง ๆ ทุกเฟรม → ทิศเปลี่ยนนิดเดียวก็กระตุกเห็นชัดตอนไล่ตาม
        /// </summary>
        private void FaceDirection(Vector2 direction, float deltaTime)
        {
            if (direction.sqrMagnitude <= 0.001f) return; // หยุดนิ่ง → คงทิศเดิมไว้ (ไม่สั่นรอทิศศูนย์)
            float target = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            float smoothed = Mathf.MoveTowardsAngle(_self.eulerAngles.z, target, TurnSpeedDegrees * deltaTime);
            _self.rotation = Quaternion.Euler(0, 0, smoothed);
        }

        /// <summary>
        /// คัดลอกมุมเส้นทางปัจจุบัน (NavMesh path corners) เข้า buffer — ใช้โดย AIPathVisualizer
        /// วาดเส้น "จริง" ที่งอตามกำแพง แทนเส้นตรงไปจุดหมาย
        /// คืน false ถ้าไม่มีเส้นทาง (ไม่ได้อยู่โหมด navmesh / ยังไม่สั่งเดิน)
        /// </summary>
        public bool TryGetPathCorners(System.Collections.Generic.List<Vector3> buffer)
        {
            buffer.Clear();
            if (_agent == null || !_agent.isOnNavMesh || !_agent.hasPath) return false;

            Vector3[] corners = _agent.path.corners; // API ของ Unity คืน array ใหม่ — เรียกเฉพาะตอนมี visualizer เท่านั้น
            if (corners.Length < 2) return false;

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i].z = _self.position.z - 0.1f; // ดึงเส้นใกล้กล้อง กันถูก sprite บัง (แบบ NavMeshTestAgent)
                buffer.Add(corners[i]);
            }
            return true;
        }
    }
}

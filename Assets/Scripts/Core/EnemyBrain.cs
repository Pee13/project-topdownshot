using UnityEngine;
using TopDownTacticalAI.Vision;
using TopDownTacticalAI.Memory;
using TopDownTacticalAI.Patrol;
using TopDownTacticalAI.Chase;
using TopDownTacticalAI.Search;
using TopDownTacticalAI.Combat;
using TopDownTacticalAI.Cover;
using TopDownTacticalAI.Dodge;
using TopDownTacticalAI.Tactical;
using TopDownTacticalAI.Animation;
using TopDownTacticalAI.Audio;
using TopDownTacticalAI.DebugTools;
using TopDownTacticalAI.Player;
using TopDownTacticalAI.Map;

namespace TopDownTacticalAI.Core
{
    /// <summary>
    /// สมองกลางของศัตรูแต่ละตัว: ประกอบร่างทุกระบบเข้าด้วยกัน
    /// (Vision, Memory, Patrol, Chase, Search, Combat, Cover, Dodge, Tactical, Animation, Audio, Debug)
    /// แล้วขับเคลื่อน StateMachine ตามลำดับการทำงานที่ออกแบบไว้ใน Framework
    /// </summary>
    public class EnemyBrain : MonoBehaviour
    {
        [Header("Role (ปรับพารามิเตอร์หลายตัวพร้อมกันตามบุคลิก)")]
        [Tooltip("เลือก Custom ถ้าอยากตั้งค่าเองทั้งหมดใน Inspector โดยไม่ให้ระบบ Role มาปรับทับ")]
        public EnemyRole Role = EnemyRole.Custom;

        [Header("References")]
        public Transform[] PatrolWaypoints;
        public Transform EyePoint;
        public Transform MuzzlePoint;
        public GameObject BulletPrefab;

        [Header("Layer Masks")]
        public LayerMask TargetMask;
        public LayerMask ObstacleMask;
        public LayerMask CoverMask;
        public LayerMask PlayerBulletMask;

        [Header("Vision")]
        public float ViewRadius = 8f;
        public float ViewAngle = 90f;

        [Header("Movement Speeds")]
        public float PatrolSpeed = 2f;
        [Tooltip("รัศมีพื้นที่ลาดตระเวนรอบจุดเริ่มต้น (ใช้เมื่อไม่ได้กำหนด Patrol Waypoints)")]
        public float PatrolRadius = 15f;
        [Tooltip("จำนวนจุดลาดตระเวนที่จะสร้างกระจายทั่วพื้นที่ (ยิ่งเยอะยิ่งครอบคลุมละเอียด)")]
        public int PatrolPointCount = 12;
        [Tooltip("ถ้าเปิดไว้และมี MapBounds อยู่ในฉาก จะคำนวณระยะลาดตระเวนให้เหมาะกับขนาดแมพจริงอัตโนมัติ (แทนค่า Patrol Radius ด้านบน) ทำให้สำรวจพื้นที่ได้ทั่วถึงกว่าตั้งค่าคงที่ตายตัว")]
        public bool AutoSizePatrolFromMap = true;
        public float ChaseSpeed = 3.5f;
        [Tooltip("ถ้ามีเพื่อนไล่ผู้เล่นด้วยกันหลายตัว ตัวที่ไม่ใช่ตัวหลักจะอ้อมไปดักที่ระยะนี้รอบตัวผู้เล่น")]
        public float FlankRadius = 3f;
        [Tooltip("ระยะห่างขั้นต่ำที่อยากให้มีจากพันธมิตรตัวอื่น (กันเดินซ้อนทับกันตอนไล่ผู้เล่นพร้อมกัน)")]
        public float PersonalSpace = 1.3f;
        public float SearchSpeed = 2.2f;
        [Tooltip("รัศมีพื้นที่ค้นหารอบจุดล่าสุดที่เห็นผู้เล่น (จะขยายกว้างขึ้นเรื่อยๆ ระหว่างค้นหา)")]
        public float SearchWanderRadius = 6f;
        public float CoverMoveSpeed = 3f;

        [Header("Combat Settings")]
        public float MaxAttackRange = 6f;
        public float PreferredMinRange = 2.5f;
        public float FireRate = 2f;
        public float ReloadDuration = 1.8f;
        public int MaxAmmo = 12;

        [Header("Tactical Settings")]
        public float DangerRange = 3f;
        public float CoverSearchRadius = 10f;
        public float DodgeDetectRadius = 2.5f;
        [Tooltip("ความเร็วตอน Dash หลบกระสุน (ปรับให้ช้าลงได้ถ้าอยากให้ AI หลบดูสมจริง/ไม่ไวเกินไป)")]
        public float DodgeSpeed = 4f;
        [Tooltip("ระยะทางที่จะเดินหลบออกไปจากจุดเดิม (World Units)")]
        public float DodgeDistance = 2f;

        [Header("Memory")]
        [Tooltip("ระยะเวลา (วินาที) ที่ AI จะจำตำแหน่งล่าสุดของผู้เล่นได้ก่อนลืม")]
        public float MemoryDuration = 20f;
        [Tooltip("ระยะที่จะ 'วิ่งไล่ต่อ' ตามทิศทางที่ผู้เล่นกำลังวิ่งอยู่ตอนเสียสายตา ก่อนเริ่มค้นหาแบบละเอียด")]
        public float SearchPursuitOvershoot = 3f;
        [Tooltip("เวลาสูงสุด (วินาที) ที่ยอมให้อยู่ในโหมดตื่นตัว/ค้นหาต่อเนื่อง ก่อนยอมแพ้แล้วกลับไปเฝ้าจุดเดิม (เหมือนโหมด Alert ใน Metal Gear Solid)")]
        public float MaxAlertDuration = 99f;

        [Header("Group Alert (เรียกพวกเมื่อโดนโจมตี)")]
        [Tooltip("รัศมีที่เสียง/สัญญาณเตือนไปถึงเมื่อตัวนี้โดนโจมตี เพื่อนที่อยู่ไกลกว่านี้จะไม่รู้เรื่อง")]
        public float AlertShoutRadius = 8f;

        [Header("Player Mana Awareness (Utility AI ตามเอกสารบทที่ 3.1.2)")]
        [Tooltip("ลาก PlayerMana ของผู้เล่นมาใส่เอง หรือเว้นว่างไว้ให้ระบบหาอัตโนมัติจาก Tag 'Player'")]
        public Player.PlayerMana PlayerManaRef;
        [Tooltip("รัศมีค้นหาตำแหน่งหลบ/ป้องกันตัวรอบ ๆ ตัวเอง (ใช้กับ Influence Map)")]
        public float SafetySearchRadius = 6f;
        [Tooltip("ขนาดช่องตาราง Influence Map (ยิ่งเล็กยิ่งละเอียดแต่ยิ่งคำนวณหนัก)")]
        public float InfluenceCellSize = 0.75f;
        [Tooltip("คะแนนพิเศษที่จะให้ช่องที่เห็นผู้เล่นได้ตอนผู้เล่น Mana ต่ำ (กระตุ้นให้ AI เข้าโจมตี)")]
        public float AttackUtilityBonus = 30f;

        [Header("Alert Phase (สถานะสงสัยก่อนยืนยันเจอผู้เล่น)")]
        [Tooltip("เวลาที่ต้องเห็นผู้เล่นต่อเนื่อง ก่อนจะ 'ยืนยัน' ว่าเจอจริง (วินาที) — ยิ่งน้อยยิ่งรู้ตัวไว")]
        public float SuspicionBuildTime = 1.2f;
        [Tooltip("เวลาที่ความสงสัยจะหายไปหมด ถ้าไม่เห็นผู้เล่นแล้ว (วินาที)")]
        public float SuspicionDecayTime = 2f;

        // --- Internal Systems ---
        private Blackboard _blackboard;
        private StateMachine _stateMachine;
        private Search.SearchState _searchState;
        private VisionSensor _vision;
        private EnemyMemory _memory;

        private AimController _aim;
        private ShootController _shoot;
        private ReloadController _reload;

        private AnimationController _animController;
        private MovementAnimation _movementAnim;
        private AlertAudio _alertAudio;
        private Health _health;
        private PlayerMana _playerMana;

        private bool _wasVisibleLastFrame;

        /// <summary>
        /// ปรับพารามิเตอร์หลายตัวพร้อมกันตาม Role ที่เลือกไว้ (เรียกก่อนอย่างอื่นทั้งหมดใน Awake)
        /// ถ้าเลือก Custom จะไม่ทำอะไรเลย ใช้ค่าที่ตั้งเองใน Inspector ทั้งหมด
        /// </summary>
        private void ApplyRolePreset()
        {
            switch (Role)
            {
                case EnemyRole.Aggressive:
                    MaxAttackRange = 4.5f;
                    PreferredMinRange = 0.8f;
                    DangerRange = 1.5f; // ทนได้มากกว่าจะถอย ไม่ค่อยหลบเข้าที่กำบัง
                    ChaseSpeed *= 1.3f;
                    DodgeDetectRadius *= 0.7f; // ระวังตัวน้อยกว่าปกติ
                    break;

                case EnemyRole.Defensive:
                    MaxAttackRange = 7f;
                    PreferredMinRange = 4f;
                    DangerRange = 4.5f; // ถอยเข้าที่กำบังไว
                    CoverSearchRadius *= 1.4f;
                    ChaseSpeed *= 0.9f;
                    break;

                case EnemyRole.Sniper:
                    MaxAttackRange = 12f;
                    PreferredMinRange = 8f;
                    DangerRange = 6f;
                    ViewRadius *= 1.5f;
                    ChaseSpeed *= 0.6f; // ไม่ค่อยไล่ตาม เน้นยิงจากระยะไกล
                    break;

                case EnemyRole.Scout:
                    ViewRadius *= 1.4f;
                    PatrolRadius *= 1.6f;
                    AlertShoutRadius *= 1.5f; // แจ้งเตือนพวกได้ไกลกว่าปกติ
                    PatrolSpeed *= 1.2f;
                    break;

                case EnemyRole.Support:
                    MaxAttackRange = 8f; // ยืนแนวหลัง
                    PreferredMinRange = 5f; // รักษาระยะห่างจากศัตรู
                    DangerRange = 6f; // ตกใจง่าย ถอยหาที่กำบังไว
                    CoverSearchRadius *= 1.5f; // กวาดสายตาหาที่กำบังได้กว้างขึ้น
                    ChaseSpeed *= 0.8f; // ไม่เน้นวิ่งไล่ล่า
                    DodgeDetectRadius *= 1.2f; // ระวังตัวสูง หลบกระสุนไว
                    PersonalSpace = 0.5f; // ลดระยะเว้นห่างจากเพื่อนลง (ยอมยืนเบียดได้เพื่อหลบหลังแทงก์หรือเข้าไปฮีล)
                    break;

                case EnemyRole.Custom:
                default:
                    break; // ไม่ปรับอะไร ใช้ค่าที่ตั้งเองทั้งหมด
            }
        }
        private void Awake()
        {
            ApplyRolePreset();

            _blackboard = new Blackboard { MaxAmmo = MaxAmmo, CurrentAmmo = MaxAmmo };
            _stateMachine = new StateMachine();

            // สำคัญมาก: ถ้ามี Rigidbody2D ติดอยู่ที่ตัวศัตรู ต้องบังคับให้เป็น Kinematic เสมอ
            // เพราะโค้ด AI ทั้งหมด (Patrol/Chase/Search/Combat/Cover/Dodge) เขียนตำแหน่งผ่าน transform.position ตรงๆ
            // ถ้า Rigidbody2D เป็น Dynamic (ค่าเริ่มต้น) Physics Engine จะพยายามขยับ/หมุนตัวเองจากแรงชนไปด้วยพร้อมกัน
            // กลายเป็นสองระบบแย่งกันคุมตำแหน่งเดียวกัน ทำให้เกิดอาการไหล/สั่นซ้ายขวาเวลาชนสิ่งกีดขวาง
            if (TryGetComponent(out Rigidbody2D rb))
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.freezeRotation = true;
            }

            // Vision
            _vision = gameObject.GetComponent<VisionSensor>();
            if (_vision == null) _vision = gameObject.AddComponent<VisionSensor>();
            _vision.ViewRadius = ViewRadius;
            _vision.ViewAngle = ViewAngle;
            _vision.TargetMask = TargetMask;
            _vision.ObstacleMask = ObstacleMask;
            _vision.EyePoint = EyePoint != null ? EyePoint : transform;
            _vision.Initialize(_blackboard);

            // Memory
            _memory = gameObject.GetComponent<EnemyMemory>();
            if (_memory == null) _memory = gameObject.AddComponent<EnemyMemory>();
            _memory.MemoryDuration = MemoryDuration;
            _memory.Initialize(_blackboard);

            // Combat helpers
            _aim = new AimController(transform);
            _shoot = new ShootController(FireRate, BulletPrefab, MuzzlePoint != null ? MuzzlePoint : transform);
            _reload = new ReloadController(_blackboard, ReloadDuration);

            // Animation / Audio (optional components)
            _animController = GetComponent<AnimationController>();
            _movementAnim = new MovementAnimation(transform.position);
            _alertAudio = GetComponent<AlertAudio>();

            // Health (ถ้ามี Component Health ติดอยู่ จะ Sync ค่า HP เข้า Blackboard ทุกเฟรม)
            _health = GetComponent<Health>();
            if (_health != null)
            {
                _blackboard.MaxHP = _health.MaxHP;
                _blackboard.CurrentHP = _health.CurrentHP;

                // ทุกครั้งที่โดนโจมตี ให้ "ตะโกน" เรียกพวกที่อยู่ในระยะได้ยินมาช่วย
                _health.OnDamaged.AddListener(OnDamagedTakeAlert);
            }

            // Player Mana Awareness: ถ้าไม่ได้ลาก Reference มาเอง ให้หาอัตโนมัติจาก GameObject ที่ Tag เป็น "Player"
            _playerMana = PlayerManaRef;
            if (_playerMana == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    _playerMana = playerObj.GetComponent<Player.PlayerMana>();
            }

            // Register States

            // คำนวณระยะลาดตระเวนให้เหมาะกับขนาดแมพจริง ถ้าเปิด AutoSizePatrolFromMap และมี MapBounds อยู่ในฉาก
            // (แทนค่า PatrolRadius คงที่ตายตัว ทำให้สำรวจพื้นที่ได้ทั่วถึงกว่าเดิมโดยไม่ต้องตั้งเลขเองทุกตัว)
            float effectivePatrolRadius = PatrolRadius;
            if (AutoSizePatrolFromMap && Map.MapBounds.Instance != null)
            {
                var bounds = Map.MapBounds.Instance;
                effectivePatrolRadius = Mathf.Min(bounds.Size.x, bounds.Size.y) * 0.45f;
            }

            _stateMachine.RegisterState(new PatrolState(transform, _blackboard, PatrolWaypoints, ObstacleMask, PatrolSpeed, patrolRadius: effectivePatrolRadius, coveragePointCount: PatrolPointCount, personalSpace: PersonalSpace));
            _stateMachine.RegisterState(new ChaseState(transform, _blackboard, _memory.TargetMemoryData, ChaseSpeed, ObstacleMask, flankRadius: FlankRadius, personalSpace: PersonalSpace));
            _searchState = new Search.SearchState(transform, _blackboard, SearchSpeed, SearchWanderRadius, ObstacleMask, _memory.TargetMemoryData, SearchPursuitOvershoot, MaxAlertDuration, PersonalSpace);
            _stateMachine.RegisterState(_searchState);
            _stateMachine.RegisterState(new Alert.SuspiciousState(transform, _blackboard));
            _stateMachine.RegisterState(new CombatState(transform, _blackboard, _aim, _shoot, _reload, ObstacleMask, MaxAttackRange, PreferredMinRange, personalSpace: PersonalSpace));
            _stateMachine.RegisterState(new CoverState(transform, _blackboard, CoverMoveSpeed, _aim, _shoot, ObstacleMask, MaxAttackRange));
            _stateMachine.RegisterState(new DodgeState(transform, _blackboard, PlayerBulletMask, ObstacleMask, _shoot, DodgeDetectRadius, DodgeSpeed, DodgeDistance, SafetySearchRadius, InfluenceCellSize, AttackUtilityBonus, MaxAttackRange));

            _stateMachine.ChangeState(EnemyState.Patrol);

            // ลงทะเบียนกับ GroupAI ถ้ามีอยู่ในฉาก
            if (Tactical.GroupAI.Instance != null)
                Tactical.GroupAI.Instance.Register(transform);
        }

        private void OnDestroy()
        {
            if (Tactical.GroupAI.Instance != null)
                Tactical.GroupAI.Instance.Unregister(transform);

            if (_blackboard.CurrentCover != null && _blackboard.CurrentCover.TryGetComponent(out CoverPoint cp))
                cp.Release();

            if (_health != null)
                _health.OnDamaged.RemoveListener(OnDamagedTakeAlert);
        }

        /// <summary>เรียกอัตโนมัติทุกครั้งที่ Health ของตัวนี้โดนดาเมจ (ผูกไว้ผ่าน UnityEvent ตอน Awake)</summary>
        private void OnDamagedTakeAlert(float currentHp)
        {
            if (Tactical.GroupAI.Instance == null) return;

            // เดาตำแหน่งภัยคุกคาม: ถ้าเห็นผู้เล่นอยู่ตอนนี้ ใช้ตำแหน่งผู้เล่นจริง
            // ถ้าไม่เห็นแต่จำได้ ใช้ตำแหน่งล่าสุดที่จำไว้ ถ้าไม่มีข้อมูลเลย ใช้ตำแหน่งตัวเองเป็นจุดโดนโจมตีแทน
            Vector2 threatPosition = _blackboard.CanSeeTarget && _blackboard.CurrentTarget != null
                ? (Vector2)_blackboard.CurrentTarget.position
                : (_blackboard.HasMemory ? _blackboard.LastSeen.Position : (Vector2)transform.position);

            Vector2 threatDirection = _memory.TargetMemoryData.GetSmoothedDirection();
            Tactical.GroupAI.Instance.BroadcastAlert(transform.position, AlertShoutRadius, threatPosition, transform, threatDirection);
            _blackboard.DeterminationScore += 20; // โดนแล้วยังไม่ถอย เรียกพวกมาช่วย -> ยิ่งตั้งใจเข้าไปใหญ่
        }

        /// <summary>เรียกจาก GroupAI.BroadcastAlert เมื่อเพื่อนตัวอื่นในระยะได้ยินโดนโจมตี ให้ไปตรวจสอบจุดนั้นทันที</summary>
        public void ReceiveAlert(Vector2 threatPosition, Vector2 threatDirection = default)
        {
            // ถ้าเห็นผู้เล่นอยู่แล้วด้วยตาตัวเอง ไม่ต้องพึ่งข้อมูลจากเพื่อน
            if (_blackboard.CanSeeTarget) return;

            // ต้องบันทึกผ่าน EnemyMemory.RecordSighting (ไม่ใช่แก้ Blackboard ตรงๆ)
            // เพราะข้างในจะ Start ตัวจับเวลาความจำให้ถูกต้องด้วย ไม่งั้นความจำจะหายไปเองในเฟรมถัดไปทันที
            _memory.RecordSighting(threatPosition, threatDirection);

            // สำคัญ: ใส่ทิศทางที่เพื่อนแจ้งมาเข้า TargetMemory ของตัวเองด้วย (ไม่ใช่แค่ LastSeen.Direction เฉยๆ)
            // เพราะ SearchState ใช้ TargetMemory.GetSmoothedDirection() เดาทิศทางค้นหาต่อ ถ้าไม่ใส่ตรงนี้
            // ตัวที่รับสัญญาณจะไปสืบแค่จุดเดียวโดยไม่รู้ทิศทางเลย ทั้งที่เพื่อนที่แจ้งมารู้ทิศทางอยู่แล้ว
            _memory.TargetMemoryData.SeedFromExternalIntel(threatPosition, threatDirection);
        }

        /// <summary>
        /// เรียกจาก GroupAI.EmitNoise เมื่อได้ยินเสียงผู้เล่น (เดิน/วิ่ง/ยิงปืน) ในระยะ
        /// ต่างจาก ReceiveAlert ตรงที่ไม่รู้ทิศทางที่แน่ชัด (แค่รู้ว่าเสียงมาจากตรงไหน)
        /// </summary>
        public void ReceiveNoise(Vector2 noisePosition)
        {
            // เห็นผู้เล่นด้วยตาตัวเองอยู่แล้ว ไม่ต้องสนใจเสียง
            if (_blackboard.CanSeeTarget) return;

            _memory.RecordSighting(noisePosition, Vector2.zero);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 0) ซิงค์ HP ล่าสุดจาก Health component (ถ้ามี)
            if (_health != null)
                _blackboard.CurrentHP = _health.CurrentHP;

            // 0.5) ซิงค์สถานะ Mana ผู้เล่นล่าสุด ให้ระบบ Utility AI ใช้ตัดสินใจ (บทที่ 3.1.2)
            _blackboard.PlayerManaLow = _playerMana != null && _playerMana.IsLow;

            // 1) อัปเดตการมองเห็นก่อนเสมอ
            _vision.Tick();

            // 2) ถ้าเห็นผู้เล่น ให้บันทึกลง Memory และเช็คว่าเป็นครั้งแรกหรือไม่ (สำหรับเสียงแจ้งเตือน + เรียกพวก)
            if (_blackboard.CanSeeTarget && _blackboard.CurrentTarget != null)
            {
                Vector2 dir = ((Vector2)_blackboard.CurrentTarget.position - (Vector2)transform.position).normalized;
                _memory.RecordSighting(_blackboard.CurrentTarget.position, dir);

                if (!_wasVisibleLastFrame)
                {
                    _alertAudio?.PlayAlert();
                    _blackboard.DeterminationScore += 15; // เจอผู้เล่นครั้งแรก -> ตั้งใจแล้ว!

                    // เจอผู้เล่นครั้งแรก -> ตะโกนเรียกพวกในระยะได้ยิน เหมือนตอนโดนโจมตี
                    Tactical.GroupAI.Instance?.BroadcastAlert(transform.position, AlertShoutRadius, _blackboard.CurrentTarget.position, transform);
                }
            }
            else if (_wasVisibleLastFrame && _alertAudio != null)
            {
                _alertAudio.PlayLostTarget();
            }
            _wasVisibleLastFrame = _blackboard.CanSeeTarget;

            _memory.Tick(dt);
            _reload.Tick(dt);

            // Alert Phase: สะสม/ลดความสงสัยตามว่าเห็นผู้เล่นอยู่จริงไหม (ก่อนจะ "ยืนยัน" เข้าสู่ Combat/Chase/Cover เต็มตัว)
            if (_blackboard.CanSeeTarget)
            {
                _blackboard.SuspicionLevel = Mathf.Clamp01(_blackboard.SuspicionLevel + dt / Mathf.Max(0.01f, SuspicionBuildTime));
                if (_blackboard.SuspicionLevel >= 1f)
                    _blackboard.IsTargetConfirmed = true;
            }
            else
            {
                _blackboard.SuspicionLevel = Mathf.Clamp01(_blackboard.SuspicionLevel - dt / Mathf.Max(0.01f, SuspicionDecayTime));
            }

            // ลืมผู้เล่นไปเต็มๆ แล้ว (กลับ Patrol) -> รีเซ็ตสถานะยืนยัน/สงสัยทั้งหมด เริ่มนับใหม่ถ้าเจออีกครั้ง
            if (!_blackboard.HasMemory)
            {
                _blackboard.IsTargetConfirmed = false;
                _blackboard.SuspicionLevel = 0f;
            }

            // ถ้ากำลังค้นหาอยู่ และ SearchState แจ้งว่า "ยอมแพ้แล้ว" (หมดเวลาตื่นตัว หรือค้นครบทุกจุดแล้วไม่เจอ)
            // ให้บังคับลืมความจำทันที เพื่อให้เฟรมนี้ตัดสินใจกลับไป Patrol ได้เลย ไม่ต้องรอ MemoryDuration หมดเองซึ่งอาจนานเกินไป
            if (_stateMachine.CurrentStateType == EnemyState.Search && _searchState.IsSearchComplete)
            {
                _memory.Forget();
            }

            // 3) ตัดสินใจ State ถัดไปด้วย Tactical Layer
            EnemyState decided = TacticalDecision.DecideNextState(
                _blackboard,
                transform.position,
                PlayerBulletMask,
                CoverMask,
                ObstacleMask,
                DangerRange,
                CoverSearchRadius,
                DodgeDetectRadius,
                _stateMachine.CurrentStateType,
                out string reason);
            _lastDecisionReason = reason;

            // ใช้ PrioritySystem ป้องกันไม่ให้ State ที่สำคัญน้อยกว่ามาขัดจังหวะ State ที่สำคัญกว่า
            // (เช่น กำลัง Dodge อยู่ ห้ามให้ Patrol/Search มาแทรกจนกว่า Dodge จะจบ)
            bool currentIsHigherPriority = PrioritySystem.GetPriority(_stateMachine.CurrentStateType) > PrioritySystem.GetPriority(decided);
            bool isDodgingNow = _stateMachine.CurrentStateType == EnemyState.Dodge && _blackboard.IsDodging;

            if (!(currentIsHigherPriority && isDodgingNow))
            {
                _stateMachine.ChangeState(decided);
            }

            // 4) รัน Tick ของ State ปัจจุบัน
            _stateMachine.Tick(dt);

            // สะสมคะแนนความพยายามระหว่างที่กำลังไล่ล่า/ค้นหา/ต่อสู้อยู่ (แสดงว่า AI ยังไม่ยอมแพ้)
            if (_stateMachine.CurrentStateType == EnemyState.Chase
                || _stateMachine.CurrentStateType == EnemyState.Search
                || _stateMachine.CurrentStateType == EnemyState.Combat)
            {
                _blackboard.DeterminationScore += dt * 2f; // ~2 แต้มต่อวินาทีที่ยังพยายามอยู่
            }

            // กันหลุดกรอบแมพ: ไม่ว่า State ไหนจะขยับตำแหน่งไปเท่าไหร่ ก็ดึงกลับเข้ากรอบเสมอ
            Vector2 clampedPos = MapBounds.Clamp(transform.position);
            transform.position = new Vector3(clampedPos.x, clampedPos.y, transform.position.z);

            // 5) อัปเดต Animation
            if (_animController != null)
            {
                _animController.SetState(_stateMachine.CurrentStateType);
                _animController.SetSpeed(_movementAnim.CalculateSpeed(transform.position, dt));
                _animController.SetAiming(_stateMachine.CurrentStateType == EnemyState.Combat || _stateMachine.CurrentStateType == EnemyState.Cover);
            }
        }

        private void FixedUpdate()
        {
            _stateMachine.FixedTick(Time.fixedDeltaTime);
        }

        public Blackboard GetBlackboard() => _blackboard;
        public EnemyState GetCurrentState() => _stateMachine.CurrentStateType;

        private string _lastDecisionReason = "";

        /// <summary>ข้อมูลสรุปสถานะ AI ณ ปัจจุบัน สำหรับแสดงผล Debug/HUD</summary>
        public AIDebugInfo GetDebugInfo()
        {
            return new AIDebugInfo
            {
                State = _stateMachine.CurrentStateType,
                Reason = _lastDecisionReason,
                CanSeeTarget = _blackboard.CanSeeTarget,
                HasMemory = _blackboard.HasMemory,
                DistanceToTarget = _blackboard.DistanceToTarget,
                CurrentHP = _blackboard.CurrentHP,
                MaxHP = _blackboard.MaxHP,
                CurrentAmmo = _blackboard.CurrentAmmo,
                MaxAmmo = _blackboard.MaxAmmo,
                IsReloading = _blackboard.IsReloading,
                InCover = _blackboard.InCover,
                IsDodging = _blackboard.IsDodging,
                LastSeenPosition = _blackboard.LastSeen.Position,
                LastSeenValid = _blackboard.LastSeen.IsValid,
                DeterminationScore = _blackboard.DeterminationScore
            };
        }
    }

    /// <summary>ก้อนข้อมูลสรุปสถานะ AI แบบอ่านง่าย ใช้ส่งให้ระบบ Debug/HUD แสดงผล</summary>
    public struct AIDebugInfo
    {
        public EnemyState State;
        public string Reason;
        public bool CanSeeTarget;
        public bool HasMemory;
        public float DistanceToTarget;
        public float CurrentHP;
        public float MaxHP;
        public int CurrentAmmo;
        public int MaxAmmo;
        public bool IsReloading;
        public bool InCover;
        public bool IsDodging;
        public Vector2 LastSeenPosition;
        public bool LastSeenValid;
        public float DeterminationScore;
    }
}

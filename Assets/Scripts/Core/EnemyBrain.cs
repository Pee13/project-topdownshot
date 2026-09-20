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
using TopDownTacticalAI.UI;
using TopDownTacticalAI.Utilities;

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
        [Tooltip("รัศมีที่เสียง/สัญญาณเตือนไปถึงเมื่อตัวนี้โดนโจมตี เพื่อนที่อยู่ไกลกว่านี้จะไม่รู้เรื่อง\n(ใช้เมื่อ AlertWholeTeam = false เท่านั้น)")]
        public float AlertShoutRadius = 8f;
        [Tooltip("เปิด = โดนยิงครั้งเดียว ทีมรู้ตำแหน่งผู้เล่นแม่นยำ 100% ทันที ไม่ว่าอยู่ไกลแค่ไหน\nปิด = ใช้ระยะเสียง AlertShoutRadius ปกติ")]
        public bool AlertWholeTeam = true;

        [Header("Player Mana Awareness (Utility AI ตามเอกสารบทที่ 3.1.2)")]
        [Tooltip("ลาก PlayerMana ของผู้เล่นมาใส่เอง หรือเว้นว่างไว้ให้ระบบหาอัตโนมัติจาก Tag 'Player'")]
        public Player.PlayerMana PlayerManaRef;
        [Tooltip("รัศมีค้นหาตำแหน่งหลบ/ป้องกันตัวรอบ ๆ ตัวเอง (ใช้กับ Influence Map)")]
        public float SafetySearchRadius = 6f;
        [Tooltip("ขนาดช่องตาราง Influence Map (ยิ่งเล็กยิ่งละเอียดแต่ยิ่งคำนวณหนัก)")]
        public float InfluenceCellSize = 0.75f;
        [Tooltip("คะแนนพิเศษที่จะให้ช่องที่เห็นผู้เล่นได้ตอนผู้เล่น Mana ต่ำ (กระตุ้นให้ AI เข้าโจมตี)")]
        public float AttackUtilityBonus = 30f;

        [Header("Retreat & Heal (§18, §19, §14)")]
        [Tooltip("เลือดต่ำกว่านี้ → เข้าสู่ Retreat ถอยกลับไปหา Healer")]
        [Range(0f, 1f)] public float RetreatHealthThreshold = 0.30f;
        [Tooltip("เลือดต่ำกว่านี้ → Critical Retreat — เอาชีวิตรอดเหนือกว่าการโจมตีทุกอย่าง")]
        [Range(0f, 1f)] public float CriticalRetreatHealthThreshold = 0.15f;
        [Tooltip("ฮีลเฉพาะเพื่อนที่เลือดต่ำกว่านี้ (กัน Healer ไล่ฮีลคนที่เลือดเกือบเต็ม)")]
        [Range(0f, 1f)] public float HealWorthThreshold = 0.80f;
        [Tooltip("Tank เลือดต่ำกว่านี้ได้โบนัสความสำคัญพิเศษ (Tank-first §16)")]
        [Range(0f, 1f)] public float TankHealThreshold = 0.80f;
        [Tooltip("เกณฑ์ 'เพื่อนวิกฤต' ที่ชนะลำดับความสำคัญปกติได้ (§16)")]
        [Range(0f, 1f)] public float CriticalAllyHealthThreshold = 0.25f;
        [Tooltip("ระยะที่ฮีลได้ (หน่วยโลก)")]
        public float HealRange = 25f;
        [Tooltip("อัตราการฮีล (HP ต่อวินาที)")]
        public float HealPerSecond = 20f;
        [Tooltip("ความเร็วตอนถอยกลับไปหา Healer")]
        public float RetreatSpeed = 3f;
        [Tooltip("ระยะที่ Tank ยืนห่างจาก Healer ตอนออกไปบังผู้เล่น (§4)")]
        public float ProtectionDistance = 4f;
        [Tooltip("รัศมีค้นหาจุดยืนรอบตัว (Position Scoring §5)")]
        public float PositionSearchRadius = 6f;
        [Tooltip("ผู้เล่นอยู่ใกล้ Healer น้อยกว่านี้ = Healer ถูกคุกคาม (§9)")]
        public float HealerThreatRange = 20f;
        [Tooltip("เพื่อนเลือดต่ำกว่านี้ + ถูกผู้เล่นไล่ → Tank ออกไป Peel (§10/§21)")]
        [Range(0f, 1f)] public float PeelAllyHpThreshold = 0.4f;
        [Tooltip("LineRenderer สำหรับวาดลำแสงฮีล (เว้นว่างให้หาจากตัวเองเอง)")]
        public LineRenderer HealBeam;

        [Header("AI Scoring Weights (§5, §21, §29, §43)")]
        [Tooltip("น้ำหนักการให้คะแนนและอัตราการคำนวณทั้งหมดของระบบตัดสินใจ " +
                 "ปรับได้โดยไม่ต้องแก้โค้ด")]
        public Tactical.AIScoringWeights ScoringWeights = new Tactical.AIScoringWeights();

        [Header("Alert Phase (สถานะสงสัยก่อนยืนยันเจอผู้เล่น)")]
        [Tooltip("เวลาที่ต้องเห็นผู้เล่นต่อเนื่อง ก่อนจะ 'ยืนยัน' ว่าเจอจริง (วินาที) — ยิ่งน้อยยิ่งรู้ตัวไว")]
        public float SuspicionBuildTime = 1.2f;
        [Tooltip("เวลาที่ความสงสัยจะหายไปหมด ถ้าไม่เห็นผู้เล่นแล้ว (วินาที)")]
        public float SuspicionDecayTime = 2f;

        // --- Internal Systems ---
        private Blackboard _blackboard;
        private StateMachine _stateMachine;
        private Collider2D _bodyCollider;
        private Vector2 _positionBeforeStateTick;
        private Vector2 _positionBeforeFixedTick;
        private Search.SearchState _searchState;

        [Header("Obstacle Collision")]
        [Tooltip("ระยะเผื่อจากกำแพง ป้องกันมอนค้างหรือฝังใน Collider")]
        public float ObstacleSkinWidth = 0.03f;
        [Tooltip("ระยะดันออกจากกำแพง " + "สูงสุดต่อเฟรม — จำกัดไว้เพื่อกันอาการ " +
                 "มอนโดนดันข้ามกำแพงไปอีกฟากในเฟรมเดียว (วาร์ป)")]
        public float MaxObstaclePushPerFrame = 0.5f;
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
        private Transform _playerTransform;

        private bool _wasVisibleLastFrame;

        // ── กันสุดท้าย (Last Safe Position) ──
        // จำตำแหน่งล่าสุด "ที่พิสูจน์แล้วว่าไม่ซ้อนกำแพง" ไว้เสมอ
        // ถ้าไม่ว่า state ไหนจะพาตัวเข้ากำแพง (วาร์ป/ดัน/หลบ) ก็จะถูกดีดกลับมาที่นี่ทันที
        // ผลลัพธ์: มอน "อยู่ในกำแพงไม่ได้แม้แต่เฟรมเดียว" — การันตีระดับสุดท้าย
        private Vector2 _lastSafePosition;
        private bool _hasSafePosition;

        // ตั้งค่าใน ApplyRolePreset() แล้วค่อยส่งเข้า Blackboard หลังจากที่ Blackboard ถูกสร้าง
        // (ApplyRolePreset ทำงานก่อนสร้าง Blackboard จึงต้องเก็บไว้ตัวแปรชั่วคราวก่อน)
        private bool _canDodge = true;

        // ตัวจับเวลาสำหรับลดความถี่ของงานหนัก (ดู ScoringWeights.visionInterval / threatInterval / teamInterval)
        private float _visionTimer;
        private float _threatTimer;
        private float _teamTimer;

        /// <summary>
        /// ถึงเวลาทำงานนั้นหรือยัง — ใช้ลดความถี่ของงานหนักไม่ให้ทำทุกเฟรม
        /// interval &lt;= 0 หมายถึงให้ทำทุกเฟรม (ค่าเริ่มต้น จะได้ไม่เปลี่ยนพฤติกรรมเดิม)
        /// </summary>
        private static bool IsDue(ref float timer, float interval, float deltaTime)
        {
            if (interval <= 0f) return true;

            timer += deltaTime;
            if (timer < interval) return false;

            timer = 0f;
            return true;
        }

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
                    _canDodge = true;
                    break;

                case EnemyRole.Defensive:
                    // บทบาท "Tank" ของฉากนี้ — ตัวใหญ่ ทน ไม่เบี่ยงหลบแบบปกติ (§29)
                    // เพราะหน้าที่คือ "ยืนบัง" ไม่ใช่ "หลบ" — หลบไปแล้วเพื่อนที่อยู่หลังจะโดน
                    MaxAttackRange = 7f;
                    PreferredMinRange = 4f;
                    DangerRange = 4.5f; // ถอยเข้าที่กำบังไว
                    CoverSearchRadius *= 1.4f;
                    ChaseSpeed *= 0.9f;
                    _canDodge = false;
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
                    AlertShoutRadius *= 1.5f; // แจ้งเตือนพวกได้ไกลกว่ากติ
                    PatrolSpeed *= 1.2f;
                    break;

                case EnemyRole.Support:
                    // ตัวสนับสนุน: ต้องรอดนานที่สุดและอยู่ห่างจากแนวหน้า
                    // ไม่เน้นบุก ไม่เน้นยิง — เน้นอยู่ใกล้พวกเพื่อช่วยเหลือ
                    // หลบกระสุนได้ "แบบชิบหลับ" — ตรวจเฉพาะกระสุนที่ใกล้มาก ๆ เท่านั้น
                    MaxAttackRange = 5f;
                    PreferredMinRange = 5f;      // ถอยห่างกว่าใคร เพื่อไม่ให้ติดแนวหน้า
                    DangerRange = 6f;            // ระวังตัวสูง ถอยไว
                    ChaseSpeed *= 0.7f;          // ไม่ไล่ตามไกล
                    PersonalSpace *= 1.6f;       // เว้นระยะจากเพื่อนมากกว่าปกติ (กันไปบังเพื่อน)
                    AlertShoutRadius *= 1.4f;    // รับรู้/กระจายข่าวในวงกว้างเพื่อช่วยทีม
                    CoverSearchRadius *= 1.3f;   // หาที่กำบังเก่งกว่า เพราะต้องรอด
                    _canDodge = true;            // หลบได้แต่ตรวจแค่ใกล้มาก (แย่กว่า Flanker)
                    DodgeDetectRadius *= 0.5f;   // ตรวจเฉพาะกระสุนที่ใกล้มาก — หลบช้ากว่า Flanker
                    break;

                case EnemyRole.Flanker:
                    // ตัวอ้อมโจมตี: เร็วและอ้อมกว้าง เพื่อเข้าทางข้าง/หลังผู้เล่น
                    // หลบกระสุนเก่งที่สุดในทีม (ตัวเดียวที่หลบเก่งจริง ๆ)
                    // ยิงได้จากระยะที่ผู้เล่น "มองเห็นได้" — ไม่ต้องปากต่อปาก
                    MaxAttackRange = 30f;        // ยิงได้จากระยะที่เห็นกัน (แมพนี้สเกลใหญ่)
                    PreferredMinRange = 8f;      // เว้นระยะห่างพอที่ผู้เล่นมองเห็นตัว
                    DangerRange = 2.5f;
                    ChaseSpeed *= 1.35f;         // เร็วที่สุดในกอง
                    SearchSpeed *= 1.2f;         // ตามรอยได้ไว
                    FlankRadius *= 1.8f;         // อ้อมวงกว้างกว่าปกติ (จุดที่ทำให้ต่างจาก Aggressive)
                    ViewRadius *= 1.15f;         // มองกว้างเพื่อเลือกจังหวะอ้อม
                    PatrolRadius *= 1.3f;
                    DodgeDetectRadius *= 2f;     // ตรวจกระสุนไกล ๆ ได้ — หลบทันทีที่กระสุนออกปาก
                    DodgeDistance *= 1.5f;       // หลบ "ไกล" ตามที่ออกแบบ — ไม่ใช่ขยับนิดเดียว
                    RetreatSpeed *= 10f;         // หนีไปหา healer ให้ไว (เดิม 3 = เดินริ้วรอย)
                    _canDodge = true;
                    break;

                case EnemyRole.Custom:
                default:
                    _canDodge = true; // Custom ใช้ค่า default ทั้งหมด รวมถึงการ dodge
                    break; // ไม่ปรับอะไร ใช้ค่าที่ตั้งเองทั้งหมด
            }
        }

        /// <summary>
        /// ใช้ตัวคูณจาก GameDifficulty ปรับพารามิเตอร์ AI ตามระดับความยากที่ผู้เล่นเลือก
        /// เรียกหลัง ApplyRolePreset() เพื่อให้ Role preset ทำงานก่อน แล้วค่อยคูณ Difficulty เข้าไป
        /// </summary>
        private void ApplyDifficultyMultipliers()
        {
            var mult = GameDifficulty.GetMultipliers();

            // Movement
            PatrolSpeed *= mult.patrolSpeedMultiplier;
            ChaseSpeed *= mult.chaseSpeedMultiplier;
            SearchSpeed *= mult.searchSpeedMultiplier;

            // Combat
            FireRate *= mult.fireRateMultiplier;
            ReloadDuration *= mult.reloadSpeedMultiplier;
            MaxAmmo = Mathf.RoundToInt(MaxAmmo * mult.maxAmmoMultiplier);
            // Damage จะถูกนำไปใช้ใน ShootController/Bullet
            // Accuracy จะถูกนำไปใช้ใน AimController

            // Vision
            ViewRadius *= mult.viewRadiusMultiplier;
            ViewAngle *= mult.viewAngleMultiplier;

            // Alert Phase
            SuspicionBuildTime *= mult.suspicionBuildTimeMultiplier;
            SuspicionDecayTime *= mult.suspicionDecayTimeMultiplier;

            // Tactical
            DangerRange *= mult.dangerRangeMultiplier;
            CoverSearchRadius *= mult.coverSearchRadiusMultiplier;
            DodgeDetectRadius *= mult.dodgeDetectRadiusMultiplier;
            DodgeSpeed *= mult.dodgeSpeedMultiplier;

            // Health
            // MaxHP จะถูกนำไปใช้ใน Health component

            // Group AI
            AlertShoutRadius *= mult.alertShoutRadiusMultiplier;
            FlankRadius *= mult.flankRadiusMultiplier;
        }

        private void Awake()
        {
            ApplyRolePreset();
            ApplyDifficultyMultipliers();

            _blackboard = new Blackboard { MaxAmmo = MaxAmmo, CurrentAmmo = MaxAmmo };
            _stateMachine = new StateMachine();
            _bodyCollider = GetComponent<Collider2D>();

            // รัศมีตัวเอง — ใช้ตอนทำนายว่ากระสุนจะโดนไหม (DodgeDecision)
            // ใช้ขนาด collider จริง ไม่ใช่ค่า default 0.5 ที่ทำให้หลบเฉพาะกระสุนเข้ากลางตัวเป๊ะ ๆ
            if (_bodyCollider != null)
                _blackboard.BodyRadius = Mathf.Max(
                    _bodyCollider.bounds.extents.x, _bodyCollider.bounds.extents.y);

            // ความสำคัญต่อทีมตามบทบาท — ใช้ถ่วงการตัดสินใจ (ตัวสำคัญจะยอมหลบมากกว่า)
            // ตั้งค่าตรงนี้จุดเดียว เพราะ Role ถูกปรับจบแล้วใน ApplyRolePreset() ข้างบน
            _blackboard.SelfRoleImportance = AIActionScorer.GetRoleImportance(Role);
            _blackboard.CanDodge = _canDodge;

            // ระบบฮีล/ถอย — ส่งค่าจาก Inspector เข้า Blackboard ให้ TacticalDecision และ State อ่าน
            // โดยไม่ต้องขยาย signature ของ TacticalDecision.DecideNextState ให้ยาวขึ้นอีก
            _blackboard.IsHealerRole = Role == EnemyRole.Support;
            _blackboard.IsTankRole = Role == EnemyRole.Defensive;

            // Tank ไม่ควรถอยหนีง่าย ๆ (§2 — RETREAT อยู่ลำดับสุดท้ายของหน้าที่ tank)
            // จึงให้ถอยเฉพาะเมื่อเลือดวิกฤตจริง ๆ (ไม่งั้นตายแล้วใครจะบังให้)
            _blackboard.RetreatOnlyWhenCritical = Role == EnemyRole.Defensive;
            _blackboard.IsFlankerRole = Role == EnemyRole.Flanker;
            _blackboard.MaxAttackRange = MaxAttackRange;
            _blackboard.RetreatHealthThreshold = RetreatHealthThreshold;
            _blackboard.CriticalRetreatHealthThreshold = CriticalRetreatHealthThreshold;
            _blackboard.HealRange = HealRange;
            _blackboard.HealWorthThreshold = HealWorthThreshold;
            _blackboard.TankHealThreshold = TankHealThreshold;
            _blackboard.CriticalAllyHealthThreshold = CriticalAllyHealthThreshold;

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
            _stateMachine.RegisterState(new ChaseState(transform, _blackboard, _memory.TargetMemoryData, ChaseSpeed, ObstacleMask, flankRadius: FlankRadius, personalSpace: PersonalSpace, weights: ScoringWeights));
            _searchState = new Search.SearchState(transform, _blackboard, SearchSpeed, SearchWanderRadius, ObstacleMask, _memory.TargetMemoryData, SearchPursuitOvershoot, MaxAlertDuration, PersonalSpace);
            _stateMachine.RegisterState(_searchState);
            _stateMachine.RegisterState(new Alert.SuspiciousState(transform, _blackboard));
            _stateMachine.RegisterState(new CombatState(transform, _blackboard, _aim, _shoot, _reload, ObstacleMask, MaxAttackRange, PreferredMinRange, personalSpace: PersonalSpace));
            _stateMachine.RegisterState(new CoverState(transform, _blackboard, CoverMoveSpeed, _aim, _shoot, ObstacleMask, MaxAttackRange));
            _stateMachine.RegisterState(new DodgeState(transform, _blackboard, PlayerBulletMask, ObstacleMask, _shoot, DodgeDetectRadius, DodgeSpeed, DodgeDistance, SafetySearchRadius, InfluenceCellSize, AttackUtilityBonus, MaxAttackRange));

            // ── State กลุ่มเอาชีวิตรอด + สนับสนุน (§18/§19/§14) ──
            // Retreat/SeekHeal ลงทะเบียนให้ทุกตัว (ใครเลือดน้อยก็ถอยได้)
            _stateMachine.RegisterState(new Support.RetreatState(transform, _blackboard, RetreatSpeed, ObstacleMask, DangerRange * 2f));
            _stateMachine.RegisterState(new Support.SeekHealState(transform, _blackboard, ObstacleMask, RetreatSpeed * 0.8f));

            // HealAlly ลงทะเบียนเฉพาะ Healer — ตัวอื่นไม่มีทางเข้า state นี้อยู่แล้ว
            // (TacticalDecision จะเสนอก็ต่อเมื่อ blackboard.IsHealerRole เท่านั้น)
            if (Role == EnemyRole.Support)
                _stateMachine.RegisterState(new Support.HealAllyState(
                    transform, _blackboard, HealPerSecond, HealRange,
                    DangerRange * 1.5f, CoverMoveSpeed, ObstacleMask,
                    PlayerBulletMask, HealBeam));

            // ProtectHealer / PeelAlly ลงทะเบียนเฉพาะ Tank (§2/§8/§10)
            // — บทบาทอื่นไม่มีหน้าที่นี้ TacticalDecision จะไม่เสนอ
            if (Role == EnemyRole.Defensive)
            {
                _stateMachine.RegisterState(new Support.ProtectHealerState(
                    transform, _blackboard, CoverMoveSpeed, ProtectionDistance,
                    PositionSearchRadius, ObstacleMask, _shoot, MaxAttackRange));
                _stateMachine.RegisterState(new Support.PeelAllyState(
                    transform, _blackboard, CoverMoveSpeed, ObstacleMask, _shoot, MaxAttackRange));
            }

            // Flank ลงทะเบียนเฉพาะ Flanker (§3/§11/§25) — แทน Combat ปกติของมัน
            if (Role == EnemyRole.Flanker)
                _stateMachine.RegisterState(new Support.FlankState(
                    transform, _blackboard, ChaseSpeed, MaxAttackRange,
                    PreferredMinRange, ObstacleMask, _shoot, MaxAttackRange, _reload));

            _stateMachine.ChangeState(EnemyState.Patrol);
        }

        /// <summary>
        /// ลงทะเบียนกับ GroupAI ที่นี่ (Start) ไม่ใช่ Awake
        ///
        /// เหตุผล: GroupAI.Instance ถูกสร้างใน Awake ของ GroupAI เอง ซึ่งอาจรัน "หลัง"
        /// EnemyBrain.Awake ของศัตรูบางตัวตามลำดับการ Awake ของ Unity
        /// บั๊กที่เจอจริง: Tank กับ Sup ลงทะเบียนไม่สำเร็จ (ตอนนั้น Instance ยังเป็น null)
        /// เหลือแค่ Flanker ทำให้ระบบฮีล/ทีมมองไม่เห็นพวกเขาเลย
        /// Start รันหลังจาก Awake ของทุกวัตถุเสร็จแล้ว จึงการันตีว่า Instance พร้อมเสมอ
        /// </summary>
        private void Start()
        {
            if (Tactical.GroupAI.Instance != null)
                Tactical.GroupAI.Instance.Register(transform);

            // ตำแหน่งเกิดถือเป็นตำแหน่งปลอดภัยจุดแรก (ถ้าไม่ซ้อนกำแพงตั้งแต่ต้น)
            _lastSafePosition = transform.position;
            _hasSafePosition = !IsBodyOverlappingObstacle();
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

        /// <summary>
        /// เรียกอัตโนมัติทุกครั้งที่ Health ของตัวนี้โดนดาเมจ (ผูกไว้ผ่าน UnityEvent ตอน Awake)
        ///
        /// หลักการ "โดนยิง = รู้ตำแหน่งผู้เล่นทันที 100%" (§ ข้อมูลจากรอยแผล):
        ///   - ลูกกระสุนต้องมาจากผู้เล่นเสมอ (Bullet กัน friendly fire ไว้แล้ว)
        ///     จึงถือว่าผู้โจมตี = ผู้เล่น และรู้ตำแหน่งผู้เล่น "จริง" ณ ขณะนั้นทันที
        ///     แม้ไม่เคยเห็นผู้เล่นมาก่อนเลยก็ตาม (อยู่หลังกำแพง/ออกนอกสายตา)
        ///   - จดจำตำแหน่งนั้นด้วยความมั่นใจเต็มที่ (MemoryConfidence = 1.0)
        ///   - แล้วแชร์ตำแหน่งแบบแม่นยำให้เพื่อนทั้งทีมผ่าน BroadcastAlert
        ///
        /// การหมดอายุ: ความจำจะลดลงตามเวลา (MemoryDuration) และถ้าไปถึงตำแหน่งนั้น
        /// แล้วค้นหาจนครบโดยไม่เจอ → SearchState.IsSearchComplete → Forget → กลับไป Patrol
        /// เหมือนเดิมตามที่ออกแบบไว้
        /// </summary>
        private void OnDamagedTakeAlert(float currentHp)
        {
            if (Tactical.GroupAI.Instance == null) return;

            // ── 1) หาตำแหน่งผู้เล่นจริง ──
            // เกมนี้มีผู้เล่นตัวเดียว และกระสุนศัตรูไม่มีทางทำร้ายศัตรูด้วยกันเอง
            // (Bullet กัน friendly fire ไว้) ดังนั้น "ถูกดาเมจ" = "ผู้เล่นยิง"
            if (_playerTransform == null) ResolvePlayerTransform();

            Vector2 playerPos = _playerTransform != null
                ? (Vector2)_playerTransform.position
                : (Vector2)transform.position; // กันพังสุดท้าย: ไม่พบผู้เล่น ใช้จุดที่โดนยิงแทน

            Vector2 directionToPlayer = (_playerTransform != null
                ? (playerPos - (Vector2)transform.position).normalized
                : Vector2.zero);

            // ── 2) จดจำด้วยความมั่นใจเต็มที่ — รู้ทันที ไม่ต้องสงสัย (ไม่เข้า Suspicious ให้เสียเวลา) ──
            _memory.RecordSighting(playerPos, directionToPlayer);   // HasMemory=true + MemoryConfidence=1.0
            _memory.TargetMemoryData.SeedFromExternalIntel(playerPos, directionToPlayer);
            _blackboard.IsTargetConfirmed = true;                    // โดนยิงแล้วเห็นผู้เล่น → ปะทะได้เลย
            _blackboard.DeterminationScore += 20;                    // โดนแล้วยังไม่ถอย → ยิ่งตั้งใจ

            // ── 3) แชร์ตำแหน่งผู้เล่นแบบแม่นยำให้เพื่อนทั้งทีม ──
            // AlertWholeTeam = true → ทีมรู้หมดไม่ว่าอยู่ไกลแค่ไหน
            // false → ใช้ AlertShoutRadius ตามระยะเสียงปกติ (ปรับได้ใน Inspector)
            float shoutRadius = AlertWholeTeam ? float.MaxValue : AlertShoutRadius;
            Tactical.GroupAI.Instance.BroadcastAlert(transform.position, shoutRadius, playerPos, transform, directionToPlayer);

            _alertAudio?.PlayAlert();
        }

        /// <summary>หาผู้เล่นจาก Tag — ใช้ cache ถ้าหาแล้ว (เรียกซ้ำเมื่อ player ตายและเกิดใหม่)</summary>
        private void ResolvePlayerTransform()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) player = GameObject.Find("Player 1");
            if (player != null) _playerTransform = player.transform;
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
            // กันพัง: ถ้า Awake() ยังไม่รันจบ (เช่นมี Error เกิดขึ้นระหว่างทาง) หรือ object ถูกทำลายไปแล้ว
            // ให้ข้ามเฟรมนี้ไปเลย แทนที่จะโยน NullReferenceException สแปมรัวๆ ทุกเฟรม
            if (_blackboard == null || _stateMachine == null || _vision == null) return;

            float dt = Time.deltaTime;

            // 0) ซิงค์ HP ล่าสุดจาก Health component (ถ้ามี)
            // ต้องซิงค์ MaxHP ด้วยทุกเฟรม ไม่ใช่แค่ตอน Awake เพราะ Health.Awake()
            // อาจรันทีหลังแล้วคูณค่า MaxHP ตามความยาก ทำให้ HUD แสดงเพดานเลือดเดิม
            // ไม่ตรงกับเลือดจริงของศัตรู
            if (_health != null)
            {
                _blackboard.CurrentHP = _health.CurrentHP;
                _blackboard.MaxHP = _health.MaxHP;
            }

            // 0.5) ซิงค์สถานะ Mana ผู้เล่นล่าสุด ให้ระบบ Utility AI ใช้ตัดสินใจ (บทที่ 3.1.2)
            _blackboard.PlayerManaLow = _playerMana != null && _playerMana.IsLow;

            // 1) อัปเดตการมองเห็น (ยิงเรย์หลายเส้น — เป็นงานหนักที่สุดในบรรดาขั้นรับรู้)
            //    ตั้ง ScoringWeights.visionInterval > 0 เพื่อลดความถี่ได้ (0 = ทุกเฟรม)
            if (IsDue(ref _visionTimer, ScoringWeights.visionInterval, dt))
                _vision.Tick();

            // 1.5) ค่าที่ได้จากการรับรู้และนำไปใช้ให้คะแนนการตัดสินใจ
            //      ทุกค่าถูก normalize เป็น 0..1 เพื่อให้เทียบคะแนนกันได้อย่างเป็นธรรม
            //      (ค่าพวกนี้คำนวณถูกมาก จึงทำทุกเฟรมเสมอ)
            _blackboard.SelfHealthPercent = Utilities.MathUtility.HealthPercent(_blackboard.CurrentHP, _blackboard.MaxHP);
            _blackboard.DistanceScore = _blackboard.CanSeeTarget
                ? Utilities.MathUtility.DistanceScore(_blackboard.DistanceToTarget, Mathf.Max(0.01f, ViewRadius))
                : 0f;

            // ความสามารถในการเข้าถึงเป้าหมาย: ยังไม่มี Pathfinding เต็มรูปแบบในโปรเจกต์
            // จึงใช้การเห็นเป้าหมายเป็นตัวแทนก่อน — มองเห็นได้ = น่าจะเดินไปถึงได้
            _blackboard.AccessibilityScore = _blackboard.CanSeeTarget ? 1f : 0f;

            // จำนวนเพื่อนที่อยู่ใกล้ — ใช้ทั้งประเมินภัยคุกคามและประสานงานกับทีม
            // GroupAI เก็บรายชื่อแบบ List แล้ววนหาทั้งหมด จึงเป็นงานที่แพงขึ้นตามจำนวนศัตรู
            if (IsDue(ref _teamTimer, ScoringWeights.teamInterval, dt))
            {
                _blackboard.NearbyAllyCount = Tactical.GroupAI.Instance != null
                    ? Tactical.GroupAI.Instance.CountNearby(transform.position, AlertShoutRadius, transform)
                    : 0;

                // ── ทีมบัส (§20) — หา Healer ที่ใกล้ที่สุด + ตรวจว่ามีเพื่อนบาดเจ็บไหม ──
                // ใช้โดย RetreatState (ถอยไปหาใคร), SeekHealState (ยืนใกล้ใคร),
                // TacticalDecision (ถอยแล้วมีประโยชน์ไหม) และ HealAllyState (มีคนรอฮีลไหม)
                if (Tactical.GroupAI.Instance != null)
                {
                    Transform healer = Tactical.GroupAI.Instance.FindNearestHealer(transform.position, float.MaxValue);
                    _blackboard.HealerTransform = healer;
                    _blackboard.HealerAvailable = healer != null;
                    _blackboard.HealerDistance = healer != null
                        ? Vector2.Distance(transform.position, healer.position)
                        : float.MaxValue;

                    // ตรวจเพื่อนบาดเจ็บเฉพาะ healer — ตัวอื่นไม่จำเป็นต้องรู้ (ประหยัดงาน)
                    _blackboard.WoundedAllyExists = _blackboard.IsHealerRole
                        && Tactical.HealTargetScorer.HasWoundedAlly(transform, _blackboard.HealWorthThreshold);

                    // ── Tank Protect/Peel triggers (§8/§9/§10/§21) ──
                    // ตรวจเฉพาะ Tank — หน้าที่ของตัวอื่นไม่เกี่ยว (ประหยัดงาน)
                    if (_blackboard.IsTankRole)
                        EvaluateTankTriggers(healer);
                }
                else
                {
                    _blackboard.HealerAvailable = false;
                    _blackboard.HealerTransform = null;
                    _blackboard.HealerDistance = float.MaxValue;
                    _blackboard.WoundedAllyExists = false;
                    _blackboard.HealerThreatened = false;
                    _blackboard.RetreatingAllyUnderThreat = false;
                    _blackboard.RetreatingAllyTransform = null;
                }
            }

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

            // 1.8) ประเมินภัยคุกคาม หลังรับรู้และจดจำเสร็จแล้ว
            //      คำนวณที่นี่แทนที่จะรอให้ถึงขั้น Combat เพราะระหว่างช่วงสงสัย
            //      หรือช่วงที่เพิ่งเสียสายตาไปหมาดๆ AI ก็ต้องรู้ว่าตัวเองเสี่ยงแค่ไหน
            //      (ก่อนหน้านี้ค่านี้ถูกคำนวณเฉพาะตอนยืนยันเจอเป้าหมายแล้วเท่านั้น)
            if (IsDue(ref _threatTimer, ScoringWeights.threatInterval, dt))
            {
                _blackboard.ThreatLevel = Tactical.RiskEvaluation.EvaluateRisk(
                    _blackboard, _blackboard.DistanceToTarget, DangerRange, ScoringWeights);
            }

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

            // 2.5) Temporal Smoothing — ปรับค่าภัยคุกคามให้ราบรื่นก่อนใช้ตัดสินใจ
            // ถ้าใช้ค่าดิบจากเฟรมเดียว AI จะเปลี่ยนใจเพราะ Noise เพียงเฟรมเดียว
            // (เช่น เห็นเป้าหมายแวบเดียวแล้วค่าพุ่ง) ทำให้ดูไม่มีเหตุผล
            _blackboard.SmoothedThreat = Mathf.Lerp(
                _blackboard.SmoothedThreat,
                _blackboard.ThreatLevel,
                Mathf.Clamp01(dt * Mathf.Max(0.01f, ScoringWeights.threatSmoothingSpeed)));

            // 3) ตัดสินใจ State ถัดไปด้วย Tactical Layer (ยังเป็นตัวตัดสินเงื่อนไขความถูกต้อง)
            EnemyState proposedState = TacticalDecision.DecideNextState(
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

            // 3.5) ให้คะแนนและตัดสินใจขั้นสุดท้ายด้วย AIActionScorer
            //      - ตรวจ Override กรณีวิกฤต (ความอยู่รอดมาก่อนแผนปกติ)
            //      - ใช้ Hysteresis กันการสลับ State รัวๆ เมื่อคะแนนใกล้กัน
            //      - ยังคงกันไม่ให้ State ที่สำคัญน้อยกว่ามาขัดจังหวะ Dodge ที่กำลังทำงานอยู่
            bool isDodgingNow = _stateMachine.CurrentStateType == EnemyState.Dodge && _blackboard.IsDodging;

            EnemyState decided;
            float decidedScore;
            string scorerReason;

            if (isDodgingNow)
            {
                // กำลังหลบอยู่ ห้ามให้อะไรก็ตามมาแทรกจนกว่าจะหลบเสร็จ
                decided = _stateMachine.CurrentStateType;
                decidedScore = AIActionScorer.ScoreAction(decided, _blackboard, ScoringWeights);
                scorerReason = "กำลังหลบกระสุนอยู่ — ล็อกแผนไว้จนกว่าจะหลบเสร็จ";
            }
            else
            {
                decided = AIActionScorer.SelectAction(
                    _blackboard,
                    ScoringWeights,
                    _stateMachine.CurrentStateType,
                    proposedState,
                    out decidedScore,
                    out scorerReason);
            }

            _blackboard.CurrentActionScore = decidedScore;
            _blackboard.CurrentActionReason = scorerReason;

            // เก็บเหตุผลที่อ่านง่ายสำหรับ Debug HUD
            // ถ้า scorer มีเหตุผลของตัวเอง (เช่น ตัดสินใจไม่เปลี่ยนแผน) ให้ใช้เหตุผลนั้น
            _lastDecisionReason = string.IsNullOrEmpty(scorerReason) ? reason : scorerReason;

            if (decided != _stateMachine.CurrentStateType)
            {
                _stateMachine.ChangeState(decided);
            }

            // 4) รัน Tick ของ State ปัจจุบัน
            _positionBeforeStateTick = transform.position;
            _stateMachine.Tick(dt);

            // สะสมคะแนนความพยายามระหว่างที่กำลังไล่ล่า/ค้นหา/ต่อสู้อยู่ (แสดงว่า AI ยังไม่ยอมแพ้)
            if (_stateMachine.CurrentStateType == EnemyState.Chase
                || _stateMachine.CurrentStateType == EnemyState.Search
                || _stateMachine.CurrentStateType == EnemyState.Combat)
            {
                _blackboard.DeterminationScore += dt * 2f; // ~2 แต้มต่อวินาทีที่ยังพยายามอยู่
            }

            // กันมอนทะลุกำแพงแบบรวมศูนย์: ทุก State อาจขยับ Transform โดยตรง
            // จึงตรวจการเคลื่อนที่ด้วย Collider จริงหลัง State ทำงานทุกเฟรม
            ResolveObstacleCollision();

            // กันหลุดกรอบแมพ: ไม่ว่า State ไหนจะขยับตำแหน่งไปเท่าไหร่ ก็ดึงกลับเข้ากรอบเสมอ
            Vector2 clampedPos = MapBounds.Clamp(transform.position);
            transform.position = new Vector3(clampedPos.x, clampedPos.y, transform.position.z);

            // กันสุดท้าย: มอน "อยู่ในกำแพงไม่ได้แม้แต่เฟรมเดียว" — ถ้าซ้อน ดีดกลับตำแหน่งปลอดภัยล่าสุด
            EnsureNotInsideObstacle();

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
            if (_stateMachine == null) return;
            _positionBeforeFixedTick = transform.position;
            _stateMachine.FixedTick(Time.fixedDeltaTime);
            _positionBeforeStateTick = _positionBeforeFixedTick;
            ResolveObstacleCollision();
            EnsureNotInsideObstacle();
        }

        /// <summary>
        /// ตรวจ trigger ของ Tank: Healer ถูกคุกคามไหม (§9) / มีเพื่อนถูกไล่ไหม (§21)
        ///
        /// หลักการ (§50): ใช้ตำแหน่งผู้เล่นที่ "ตัวเองรับรู้ได้จริง" — เห็นด้วยตา หรือจำจากที่เพื่อนแจ้ง
        /// ไม่ใช้ตำแหน่งผู้เล่นจริงแบบโกง
        /// </summary>
        private void EvaluateTankTriggers(Transform healer)
        {
            // ตำแหน่งผู้เล่นที่รับรู้ได้: เห็นตอนนี้ > จำจากครั้งก่อน
            bool knowPlayer = false;
            Vector2 knownPlayerPos = Vector2.zero;

            if (_blackboard.CanSeeTarget && _blackboard.CurrentTarget != null)
            {
                knownPlayerPos = _blackboard.CurrentTarget.position;
                knowPlayer = true;
            }
            else if (_blackboard.HasMemory)
            {
                knownPlayerPos = _blackboard.LastSeen.Position;
                knowPlayer = true;
            }

            if (!knowPlayer || healer == null)
            {
                _blackboard.HealerThreatened = false;
                _blackboard.RetreatingAllyUnderThreat = false;
                _blackboard.RetreatingAllyTransform = null;
                return;
            }

            // ── §9: HealerThreat — ผู้เล่น "มองเห็น healer" + "ใกล้พอ" = คุกคาม ──
            float distPlayerToHealer = Vector2.Distance(knownPlayerPos, healer.position);
            bool playerSeesHealer = RaycastDetector.HasLineOfSight(
                knownPlayerPos, healer.position, ObstacleMask, distPlayerToHealer + 1f);

            _blackboard.HealerThreatened = playerSeesHealer && distPlayerToHealer <= HealerThreatRange;

            // ── §21: เพื่อนกำลังถอย (เลือดต่ำ) และผู้เล่นอยู่ใกล้มัน = กำลังถูกไล่ ──
            _blackboard.RetreatingAllyUnderThreat = false;
            _blackboard.RetreatingAllyTransform = null;

            if (Tactical.GroupAI.Instance == null) return;

            float bestAllyDistance = float.MaxValue;
            Transform bestAlly = null;

            foreach (var ally in Tactical.GroupAI.Instance.ActiveEnemies)
            {
                if (ally == null || ally == transform) continue;
                if (!ally.TryGetComponent(out EnemyBrain allyBrain)) continue;

                // ต้องกำลังหนีอยู่จริง ๆ (Retreat/SeekHeal) ไม่ใช่แค่เลือดน้อยแต่ยังยิงกันอยู่
                var allyState = allyBrain.GetCurrentState();
                if (allyState != EnemyState.Retreat && allyState != EnemyState.SeekHeal) continue;

                if (!ally.TryGetComponent(out Health allyHealth)) continue;
                if (allyHealth.IsDead) continue;

                float allyHpPercent = Utilities.MathUtility.HealthPercent(allyHealth.CurrentHP, allyHealth.MaxHP);
                if (allyHpPercent > PeelAllyHpThreshold) continue;

                // ผู้เล่นต้องอยู่ใกล้เพื่อนตัวนั้นพอที่จะถือว่า "กำลังไล่"
                float distPlayerToAlly = Vector2.Distance(knownPlayerPos, ally.position);
                if (distPlayerToAlly > HealerThreatRange) continue;

                if (distPlayerToAlly < bestAllyDistance)
                {
                    bestAllyDistance = distPlayerToAlly;
                    bestAlly = ally;
                }
            }

            if (bestAlly != null)
            {
                _blackboard.RetreatingAllyUnderThreat = true;
                _blackboard.RetreatingAllyTransform = bestAlly;
            }
        }

        /// <summary>
        /// กันสุดท้ายระดับสุดท้าย: มอน "อยู่ในกำแพงไม่ได้แม้แต่เฟรมเดียว"
        ///
        /// หลักการ:
        ///   1) ตรวจว่า collider ของตัวเองซ้อนกับกำแพง (ObstacleMask) หรือไม่
        ///   2) ถ้าซ้อน → ดีดกลับไปยัง "_lastSafePosition" ซึ่งเป็นตำแหน่งที่พิสูจน์แล้วว่าไม่ซ้อน
        ///   3) ถ้าไม่ซ้อน → บันทึกตำแหน่งนี้เป็นตำแหน่งปลอดภัยล่าสุด
        ///
        /// ต่างจาก ResolveObstacleCollision ตรงที่เมธอดนั้น "ดัน" แบบค่อยๆ ออก (มี cap ต่อเฟรม)
        /// ซึ่งอาจยังค้างอยู่ในกำแพงหลายเฟรมถ้ามุดลึก — เมธอดนี้จะ "จบในเฟรมเดียว" เสมอ
        /// ทำให้ไม่ว่าจะเกิดจากอะไร (วาร์ป/ดันแรง/หลบเข้ากำแพง) ก็ไม่มีทางอยู่ในกำแพงได้
        /// </summary>
        private void EnsureNotInsideObstacle()
        {
            if (_bodyCollider == null || ObstacleMask.value == 0) return;

            bool overlapped = IsBodyOverlappingObstacle();

            if (overlapped && _hasSafePosition)
            {
                // ดีดกลับตำแหน่งปลอดภัยล่าสุด — การันตีออกจากกำแพงทันที
                transform.position = new Vector3(
                    _lastSafePosition.x, _lastSafePosition.y, transform.position.z);
            }
            else if (overlapped && !_hasSafePosition)
            {
                // ไม่มีตำแหน่งปลอดภัยจดไว้ (เช่นเกิดมาทับกำแพง หรือถูกวาร์ปไกลมาก)
                // → ค้นหาจุดปลอดภัยใกล้ที่สุดด้วยวงกลมซ้อนรอบตัว (spiral search)
                // หาได้แน่นอนเพราะกำแพงไม่ได้กินพื้นที่ทั้งแมพ
                Vector2 safePoint;
                if (TryFindNearbySafePosition(out safePoint))
                {
                    transform.position = new Vector3(safePoint.x, safePoint.y, transform.position.z);
                    _lastSafePosition = safePoint;
                    _hasSafePosition = true;
                }
                // ถ้าหาไม่เจอเลย (ทุกทิศตันสุดๆ) ปล่อยให้ ResolveObstacleCollision ดันแบบนุ่มนวลต่อไป
            }
            else if (!overlapped)
            {
                // ตอนนี้ปลอดภัย → จดจำไว้เป็นจุดดีดกลับถัดไป
                _lastSafePosition = transform.position;
                _hasSafePosition = true;
            }
        }

        /// <summary>
        /// หาจุดปลอดกำแพงใกล้ที่สุดรอบตัว — ตรวจเป็นวงกลมซ้อน 12 ทิศ × 3 ระยะ
        /// ใช้เมื่อ "ไม่มีตำแหน่งปลอดภัยจดไว้เลย" (เกิดในกำแพง/ถูกวาร์ปลึก)
        /// คืน true พร้อมจุดที่ปลอดภัย หรือ false ถ้ารอบตัวตันทุกทิศ (แทบเป็นไปไม่ได้)
        /// </summary>
        private bool TryFindNearbySafePosition(out Vector2 safePoint)
        {
            safePoint = Vector2.zero;

            float coreRadius = GetBodyRadius() * 0.5f;
            Vector2 origin = transform.position;
            float[] searchDistances =
            {
                coreRadius * 2f,
                coreRadius * 4f,
                coreRadius * 8f
            };
            const int directions = 12;

            foreach (float searchDistance in searchDistances)
            {
                for (int a = 0; a < directions; a++)
                {
                    float angle = a * (360f / directions) * Mathf.Deg2Rad;
                    Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * searchDistance;

                    if (!PhysicsUtility.IsPositionBlocked(candidate, coreRadius, ObstacleMask))
                    {
                        safePoint = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>true ถ้า collider ของตัวเอง "ซ้อนลึก" อยู่ในกำแพงชิ้นใดชิ้นหนึ่ง
        /// (แค่ชิด/แตะผิวไม่นับ — ไม่งั้นจะดีดกลับรัวๆ ตอนเดินชิดกำแพง)</summary>
        private bool IsBodyOverlappingObstacle()
        {
            if (_bodyCollider == null || ObstacleMask.value == 0) return false;

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = ObstacleMask,
                useTriggers = false
            };
            var hits = new Collider2D[8];
            int count = _bodyCollider.Overlap(filter, hits);

            for (int i = 0; i < count; i++)
            {
                if (hits[i] == null || hits[i] == _bodyCollider) continue;

                ColliderDistance2D separation = Physics2D.Distance(_bodyCollider, hits[i]);
                // ซ้อนลึกเกินกว่า 0.1 หน่วย = "อยู่ในกำแพงจริง" (แค่แตะผิวไม่นับ)
                if (separation.isOverlapped && separation.distance < -0.05f) return true;
            }

            return false;
        }

        private void ResolveObstacleCollision()
        {
            if (_bodyCollider == null || ObstacleMask.value == 0) return;

            Vector2 prevPos = _positionBeforeStateTick;
            Vector2 current = transform.position;
            Vector2 movement = current - prevPos;
            float moveDistance = movement.magnitude;

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = ObstacleMask,
                useTriggers = false
            };

            // ── 1) Sweep จาก "ตำแหน่งก่อนขยับ" — กันข้ามกำแพงในสเต็ปเดียว ──
            // เดิมยิง Cast จากตำแหน่งปัจจุบัน ซึ่งอาจข้ามกำแพงไปแล้วในเฟรมนี้ (Cast เริ่มจาก
            // ตำแหน่ง collider ปัจจุบัน จึงมองไม่เห็นกำแพงที่ผ่านมาแล้ว) ต้องยิงจากตำแหน่งเดิม
            // พร้อมวงกลมขนาดเท่าตัวแทน จึงจะจับกำแพงที่ "ถูกข้าม" ได้ครบ
            if (moveDistance > 0.0001f)
            {
                var sweepHits = new RaycastHit2D[8];
                int hitCount = Physics2D.CircleCast(
                    prevPos,
                    GetBodyRadius(),
                    movement / moveDistance,
                    filter,
                    sweepHits,
                    moveDistance + ObstacleSkinWidth);

                RaycastHit2D nearest = default;
                bool found = false;
                for (int i = 0; i < hitCount; i++)
                {
                    if (sweepHits[i].collider == null || sweepHits[i].collider == _bodyCollider) continue;
                    if (!found || sweepHits[i].distance < nearest.distance)
                    {
                        nearest = sweepHits[i];
                        found = true;
                    }
                }

                if (found)
                {
                    // หยุดแค่ชิดผิวกำแพง — ห้ามเดินต่อแม้จะเหลืองานเดินอยู่
                    // (AI จะไล่ "อ้อม" ด้วยระบบ steering ของ state แทน ไม่ข้ามไปเลย)
                    float safeDistance = Mathf.Max(0f, nearest.distance - ObstacleSkinWidth);
                    Vector2 clamped = prevPos + movement.normalized * safeDistance;
                    if ((clamped - current).sqrMagnitude > 0.000001f)
                        transform.position = new Vector3(clamped.x, clamped.y, transform.position.z);
                    current = clamped;
                }
            }

            // ── 2) ดันออกจากกำแพงถ้ายังซ้อนอยู่ — จำกัดระยะต่อเฟรม ──
            // บั๊กเดิม: ดันตาม "ผิวที่ใกล้ที่สุด" ไม่จำกัดระยะ พอตัวซ้อนลึกเข้าไปในกำแพง
            // ผิวที่ใกล้ที่สุดกลายเป็น "ฟากตรงข้าม" → โดนดันข้ามกำแพงไปอีกฟาก = วาร์ป
            // วิธีแก้: เลือกดันไปทาง "ที่ที่เพิ่งมาจาก" (prevPos) เสมอ และจำกัดระยะต่อเฟรม
            var overlapHits = new Collider2D[8];
            int overlapCount = _bodyCollider.Overlap(filter, overlapHits);
            Vector2 totalPush = Vector2.zero;

            for (int i = 0; i < overlapCount; i++)
            {
                var obstacle = overlapHits[i];
                if (obstacle == null || obstacle == _bodyCollider) continue;

                ColliderDistance2D separation = Physics2D.Distance(_bodyCollider, obstacle);
                if (!separation.isOverlapped) continue;

                Vector2 pushOut = separation.normal * (-separation.distance + ObstacleSkinWidth);

                // ถ้าตำแหน่งเดิมอยู่นอกกำแพงนี้ ให้ดันกลับไปทางนั้น (ฟากเดิม) เสมอ
                Vector2 toPrev = prevPos - current;
                if (toPrev.sqrMagnitude > 0.0001f && Vector2.Dot(separation.normal, toPrev.normalized) < 0f)
                    pushOut = -pushOut;

                totalPush += pushOut;
            }

            // หักล้างกันของหลายกำแพงแล้วค่อยจำกัดระยะรวม — กันดันแรงหลายหน่วยในเฟรมเดียว
            if (totalPush.sqrMagnitude > 0.000001f)
            {
                if (totalPush.magnitude > MaxObstaclePushPerFrame)
                    totalPush = totalPush.normalized * MaxObstaclePushPerFrame;
                transform.position += (Vector3)totalPush;
            }
        }

        /// <summary>
        /// รัศมี "วงกลมครอบตัว" ของ collider — ใช้กับ CircleCast ตอนตรวจเส้นทางการขยับ
        /// คำนวณจาก bounds ทุกครั้ง เพราะขนาดจริงขึ้นกับ scale/rotation ของวัตถุ
        /// </summary>
        private float GetBodyRadius()
        {
            var extents = _bodyCollider.bounds.extents;
            return Mathf.Max(extents.x, extents.y);
        }

        public Blackboard GetBlackboard() => _blackboard;
        public EnemyState GetCurrentState() => _stateMachine != null ? _stateMachine.CurrentStateType : EnemyState.Patrol;

        private string _lastDecisionReason = "";

        /// <summary>ข้อมูลสรุปสถานะ AI ณ ปัจจุบัน สำหรับแสดงผล Debug/HUD</summary>
        public AIDebugInfo GetDebugInfo()
        {
            // กันพัง: ถ้า Awake() ยังไม่รันจบ หรือ object ถูกทำลายไปแล้ว คืนค่าเปล่าแทนการโยน Exception
            if (_stateMachine == null || _blackboard == null) return default;

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
                DeterminationScore = _blackboard.DeterminationScore,

                // ── ค่าความมั่นใจและคะแนนการตัดสินใจ (สำหรับตรวจสอบว่า AI คิดอย่างไร) ──
                SelfHealthPercent = _blackboard.SelfHealthPercent,
                ThreatLevel = _blackboard.ThreatLevel,
                SmoothedThreat = _blackboard.SmoothedThreat,
                VisionConfidence = _blackboard.VisionConfidence,
                MemoryConfidence = _blackboard.MemoryConfidence,
                PredictionConfidence = _blackboard.PredictionConfidence,
                DistanceScore = _blackboard.DistanceScore,
                AccessibilityScore = _blackboard.AccessibilityScore,
                NearbyAllyCount = _blackboard.NearbyAllyCount,
                CurrentActionScore = _blackboard.CurrentActionScore,
                CurrentActionReason = _blackboard.CurrentActionReason,
                PredictedTargetPosition = _blackboard.PredictedTargetPosition
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

        // ── ความมั่นใจในข้อมูลแต่ละแหล่ง (0..1) ──
        // AI ไม่ควรเชื่อข้อมูลที่ไม่แน่นอนเต็ม 100% ค่าเหล่านี้บอกว่า
        // "ข้อมูลที่ใช้ตัดสินใจอยู่ เชื่อถือได้แค่ไหน"
        public float SelfHealthPercent;
        public float ThreatLevel;         // ค่าดิบ
        public float SmoothedThreat;      // ค่าที่ปรับให้ราบรื่นแล้ว (ใช้ตัดสินใจจริง)
        public float VisionConfidence;
        public float MemoryConfidence;
        public float PredictionConfidence;

        // ── คะแนนประกอบการตัดสินใจ ──
        public float DistanceScore;
        public float AccessibilityScore;
        public int NearbyAllyCount;
        public float CurrentActionScore;
        public string CurrentActionReason;
        public Vector2 PredictedTargetPosition;
    }
}

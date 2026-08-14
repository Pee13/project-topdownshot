using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ╔══════════════════════════════════════════════════════════════╗
/// ║  INTELLIGENT AGENT — INFLUENCE MAP + UTILITY-BASED AI       ║
/// ║  v2.0 — เพิ่ม WeaponSystem + SquadAlert + NavMesh Support   ║
/// ╚══════════════════════════════════════════════════════════════╝
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    // ════════════════════════════════════════════════════════
    //  PERCEPTION
    // ════════════════════════════════════════════════════════

    [Header("── Perception ──")]
    [Tooltip("มุมมองด้านหน้า (องศา) — ต้องอยู่ในมุมนี้ถึงจะเห็นได้ (เหมือนสายตามองตรงไปข้างหน้า)")]
    public float fovAngle        = 120f;
    [Tooltip("ระยะที่มองเห็นได้ภายในมุมมองด้านหน้า (fovAngle)")]
    public float fovRange        = 500f;
    [Tooltip("ระยะที่ 'รู้สึกได้' รอบตัวแบบ 360 องศา ไม่เช็คมุมมองเลย (แค่เช็คว่ามีกำแพงบังไหม) — ถ้าตั้งไว้กว้างเกินไป จะดูเหมือน AI 'รู้' ตำแหน่งผู้เล่นทั้งที่ยังไม่เคยหันหน้าไปมองเลยด้วยซ้ำ แนะนำตั้งให้แคบ (ระยะประชิดตัวจริงๆ)")]
    public float peripheralRange = 60f;
    public int   fovRayCount     = 14;

    [HideInInspector] public bool    canSeePlayer = false;
    [HideInInspector] public bool    rawCanSeePlayer = false; // ค่า "เห็นจริงๆ" ไม่ผ่านช่วงผ่อนผัน Sight Persistence (ใช้จับจังหวะเสียสายตาจริงให้แม่นยำ)
    [HideInInspector] public float   distToPlayer = 0f;

    // ════════════════════════════════════════════════════════
    //  NAVMESH
    // ════════════════════════════════════════════════════════

    /// <summary>ถ้า true → EnemyNavMesh จะจัดการ movement แทน Steering</summary>
    [HideInInspector] public bool useNavMesh = false;

    // ════════════════════════════════════════════════════════
    //  INFLUENCE MAP
    // ════════════════════════════════════════════════════════

    [Header("── Influence Map ──")]
    public LayerMask wallLayer;
    public LayerMask bulletLayer;
    public float     cellSize         = 20f;
    public int       gridRadius       = 14;
    public float     dangerDecayRate  = 0.8f;
    public float     safetyBonus      = 100f;
    public float     attackBonus      = 60f;

    // ════════════════════════════════════════════════════════
    //  UTILITY
    // ════════════════════════════════════════════════════════

    [Header("── Utility (Decision Making) ──")]
    public float lowManaThreshold = 30f;
    [Tooltip("ระยะที่นับว่าเป็น 'เพื่อนอยู่ใกล้ๆ' สำหรับคำนวณความกล้าจากจำนวนที่ได้เปรียบ (Squad Courage)")]
    public float allyCourageRadius = 300f;
    [Tooltip("จำนวนเพื่อนขั้นต่ำที่ต้องอยู่ใกล้ๆ ถึงจะกล้าเข้าปะทะ (Aggressive) แม้ผู้เล่น Mana จะสูงอยู่ก็ตาม — ตั้งเป็นเลขมากๆ (เช่น 99) ถ้าไม่อยากให้มีผลนี้เลย")]
    public int minAlliesForCourage = 2;
    [Tooltip("ตัวคูณความเร็วตอนพุ่งเข้าปะทะ (Aggressive) คูณกับ Max Speed — ตั้งไว้ต่ำกว่า 1.0 โดยตั้งใจ (ช้ากว่า Max Speed ปกติ) ถ้ายังรู้สึกว่าเร็วเกินไปอยู่ ให้ลดค่า Max Speed หลักลงตรงๆ แทน เพราะทุก State ใช้ค่านี้เป็นฐานร่วมกันหมด")]
    public float aggressiveSpeedMultiplier = 0.75f;

    [Range(0f,2f)] public float weightDanger   = 1.0f;
    [Range(0f,2f)] public float weightSafety   = 1.0f;
    [Range(0f,2f)] public float weightAttack   = 0.8f;
    [Range(0f,2f)] public float weightDistance = 0.5f;

    // ════════════════════════════════════════════════════════
    //  BEHAVIOR
    // ════════════════════════════════════════════════════════

    [Header("── Behavior ──")]
    public float fleeRadius    = 300f;
    public float kiteMinDist   = 250f;
    public float kiteMaxDist   = 400f;
    public float aggressRadius = 150f;

    // ════════════════════════════════════════════════════════
    //  STEERING
    // ════════════════════════════════════════════════════════

    [Header("── Steering Behaviors ──")]
    public float maxSpeed       = 130f;
    public float rotationSpeed  = 10f;
    public float steeringForce  = 12f;

    // ════════════════════════════════════════════════════════
    //  PATROL
    // ════════════════════════════════════════════════════════

    [Header("── Patrol ──")]
    public Transform[] waypoints;
    public float waypointReachDistance = 14f;
    public float waypointWaitTime      = 0.6f;
    public float waypointTimeout       = 6f;

    // ════════════════════════════════════════════════════════
    //  ATTACK — ใช้ EnemyWeaponSystem แทน ShootAt()
    // ════════════════════════════════════════════════════════

    [Header("── Attack (Legacy — ถ้าไม่มี WeaponSystem) ──")]
    public GameObject bulletPrefab;
    public Transform  firePoint;
    public float      fireCooldown   = 1.1f;
    public float      bulletSpeed    = 400f;
    public int        attackDamage   = 10;
    public float      aimInaccuracy  = 12f;
    public float      predictionTime = 0.2f;

    // ════════════════════════════════════════════════════════
    //  HEALTH
    // ════════════════════════════════════════════════════════

    [Header("── Health ──")]
    public int   maxHealth      = 100;
    public float lowHealthRatio = 0.35f;
    private int  currentHealth;
    public bool  IsLowHealth => currentHealth <= maxHealth * lowHealthRatio;

    // ════════════════════════════════════════════════════════
    //  COVER
    // ════════════════════════════════════════════════════════

    [Header("── Cover ──")]
    public float coverSearchRadius  = 300f;
    public float coverReachDistance = 18f;
    public float peekInterval       = 2.2f;
    public float peekDuration       = 0.75f;

    // ════════════════════════════════════════════════════════
    //  SQUAD
    // ════════════════════════════════════════════════════════

    [Header("── Squad ──")]
    [Tooltip("ถ้ามี SquadManager จะ auto-find อัตโนมัติ")]
    public EnemySquadManager squad;
    [Tooltip("ส่ง Alert ไปให้ Squad ทั้งตอนเห็นผู้เล่นครั้งแรก และตอนเข้าสู่โหมดค้นหา (Search) หลังเสียสายตา — เรียกเพื่อนมาช่วยหาด้วย")]
    public bool broadcastAlertOnSight = true;
    private bool hasAlerted = false; // ส่งแล้วหรือยัง (reset ทุกครั้งที่เสีย sight)

    [Header("── Sight Persistence (กันลืมเร็วเกินไปตอนเห็นแวบเดียวหาย) ──")]
    [Tooltip("เวลาที่จะยัง 'ถือว่าเห็นอยู่' ต่อ แม้ Raycast จะไม่เจอผู้เล่นแล้วในเฟรมนั้นๆ (วินาที) — กันลืมเร็วเกินไปตอนผู้เล่นเดินผ่านมุมกำแพง/สิ่งกีดขวางแค่ชั่วครู่")]
    public float sightPersistenceDuration = 0.5f;

    [Header("── Path Around Walls (อ้อมมุมกำแพงตอนเดินไปเป้าหมายไกลๆ) ──")]
    [Tooltip("ระยะเว้นจากมุมกำแพงจริง ตอนคำนวณจุดอ้อม (กันเดินไปเบียดติดขอบกำแพงพอดี)")]
    public float pathCornerMargin = 15f;
    [Tooltip("ความถี่ในการคำนวณเส้นทางอ้อมใหม่ (วินาที) — ยิ่งถี่ยิ่งไวต่อการเปลี่ยนแปลงแต่เสี่ยงแกว่งง่ายขึ้น")]
    public float pathRecalcInterval = 0.4f;
    [Range(0f, 0.9f)]
    [Tooltip("ความ 'ดื้อ' ที่จะยึดมุมเดิมที่เคยเลือกไว้ (0 = ไม่ดื้อเลย เปลี่ยนใจง่าย, 0.3 = ต้องดีกว่าเดิมอย่างน้อย 30% ถึงจะยอมเปลี่ยน) ค่าสูงกันอาการเดินแกว่งซ้าย-ขวาสลับมุมไม่คืบหน้า")]
    public float pathStickBonus = 0.3f;

    [Header("── Fallback: ไม่มี Waypoint กำหนดไว้ ──")]
    [Tooltip("ถ้าไม่ได้ตั้งค่า Waypoints ไว้เลย จะเดินเล่นสุ่มวนรอบจุดเกิด (Spawn Position) แทน ไม่ใช่ยืนนิ่งสนิทค้างตลอดไป — รัศมีที่จะเดินวนรอบ")]
    public float noWaypointWanderRadius = 100f;
    [Tooltip("ถ้ามี Squad และตั้งค่า Map Center/Size ไว้แล้ว จะใช้ระบบกระจายตัวคุมพื้นที่ทั่วแมพแทนการวนอยู่แค่ใกล้จุดเกิดของตัวเอง (แนะนำเปิดไว้ถ้ามีศัตรูหลายตัว กันกระจุกกันโซนเดียว)")]
    public bool useSquadCoverage = true;

    // ════════════════════════════════════════════════════════
    //  SEARCH (ค้นหาต่อหลังเสียสายตาผู้เล่น)
    // ════════════════════════════════════════════════════════

    [Header("── Search (ค้นหาหลังเสียสายตาผู้เล่น) ──")]
    [Tooltip("[เลิกใช้แล้ว] เดิมเป็นระยะอ้อมคงที่ ตอนนี้แทนที่ด้วยระบบคำนวณอิงเวลาจริง (ดูหัวข้อ 'ความแม่นยำในการคาดเดา' ด้านล่าง) เก็บฟิลด์นี้ไว้เฉยๆ กันเสียค่าที่เคยตั้งไว้")]
    public float searchOvershootDistance = 150f;
    [Tooltip("เวลาสูงสุดที่จะค้นหาต่อเนื่องก่อนยอมแพ้แล้วกลับไปลาดตระเวน (วินาที)")]
    public float searchMaxDuration = 6f;
    [Tooltip("ความถี่ในการบันทึกตำแหน่งผู้เล่นเป็น 'รอยเท้า' ระหว่างที่ยังเห็นอยู่ (วินาที) ใช้ประมาณทิศทางที่วิ่งหนี")]
    public float trailRecordInterval = 0.3f;
    [Tooltip("จำนวนจุดรอยเท้าล่าสุดที่จะจำไว้")]
    public int trailMaxPoints = 6;
    [Tooltip("ระยะห่างจากขอบกำแพงตอนอ้อมไปเช็คแต่ละฝั่ง (ยิ่งน้อยยิ่งเดินชิดกำแพง)")]
    public float wallHugDistance = 40f;
    [Tooltip("จำนวนจุดที่จะเดินสำรวจต่อเนื่องกัน ก่อนจะยอมหยุดรอ (กันอาการเดินถึงจุดเดียวแล้วหยุดนิ่งค้าง)")]
    public int searchSweepPoints = 3;
    [Tooltip("ความเร็วตอนรีบไปยังจุดที่เห็นผู้เล่นล่าสุด (Alert) คูณกับ Max Speed — ถ้ายังรู้สึกว่าเร็วเกินไปอยู่ ให้ลดค่า Max Speed หลักลงตรงๆ แทน เพราะทุก State ใช้ค่านี้เป็นฐานร่วมกันหมด")]
    public float alertRushSpeedMultiplier = 1.1f;
    [Tooltip("ความเร็วตอนเดินสำรวจแบบระมัดระวังใน Search (คูณกับ Max Speed) — ยิ่งน้อยยิ่งเดินช้า/ระวังตัว เหมือนหน่วยรบเคลียร์พื้นที่")]
    public float searchWalkSpeedMultiplier = 0.5f;
    [Tooltip("เวลาที่จะหยุดหันมองกวาดจากซ้ายสุดไปขวาสุดทีเดียวแบบนุ่มนวล (วินาที) เหมือนเช็คมุมอับทีละจุด — ยิ่งเยอะยิ่งดูสุขุม/ไม่รีบร้อน")]
    public float searchLookDuration = 1.2f;
    [Tooltip("มุมสุดขอบซ้าย/ขวาที่จะกวาดไปถึงตอนเช็คแต่ละจุด (องศา) — กวาดจาก -มุมนี้ ไป +มุมนี้ ทีเดียวแบบนุ่มนวล ไม่ใช่หมุนวนหลายรอบ")]
    public float searchLookSweepAngle = 70f;

    [Header("── ความแม่นยำในการคาดเดา (Prediction Accuracy) ──")]
    [Tooltip("ความเร็วผู้เล่นโดยประมาณ ใช้แทนตอนไม่มีข้อมูล Velocity จริง (หน่วย/วินาที) สำหรับคำนวณระยะคาดการณ์")]
    public float assumedPlayerSpeed = 5f;
    [Tooltip("ระยะขั้นต่ำที่จะเดินอ้อมไปเช็ค แม้จะเพิ่งเสียสายตาไปไม่นาน")]
    public float minSearchDistance = 60f;
    [Tooltip("ระยะสูงสุดที่จะเดินอ้อมไปเช็ค กันคำนวณไกลเกินจริงถ้าเวลาผ่านไปนานมากแล้ว")]
    public float maxSearchDistance = 400f;
    [Range(0f, 1f)]
    [Tooltip("น้ำหนักของความเร็วสด (lastKnownVel) เทียบกับรอยเท้าที่ถ่วงน้ำหนักแล้ว ตอนผสมกันหาทิศทาง (1 = เชื่อความเร็วสดอย่างเดียว)")]
    public float velocityDirectionWeight = 0.7f;
    [Tooltip("มุมที่จะแยกทิศทางค้นหาต่อคน ถ้ามีศัตรูหลายตัวช่วยกันหาผู้เล่นคนเดียวกันพร้อมกัน (องศา) — มี 2 ตัว: ตัวหนึ่งไปซ้าย อีกตัวไปขวาทันที มี 3-4 ตัว: กว้างออกไปอีกชั้นในฝั่งเดียวกัน ต้องมี Squad ถึงจะทำงาน")]
    public float squadSearchSpreadAngle = 50f;

    private float timeSightLost = 0f; // Time.time ตอนเพิ่งเสียสายตา ใช้คำนวณว่าผู้เล่นน่าจะเดินไปได้ไกลแค่ไหนแล้ว

    private List<Vector2> playerTrail = new List<Vector2>();
    private float trailTimer = 0f;
    private float searchTimer = 0f;
    private Vector2 searchOvershootTarget;
    private bool hasSearchOvershootTarget = false;
    private Collider2D lastBlockingWall = null; // กำแพง/สิ่งกีดขวางตัวที่บังสายตาจริงๆ ตอนเสีย Sight
    private int searchPhase = 0; // จุดที่กำลังเช็คอยู่ (0 = จุดแรก, 1 = จุดที่ 2, ...)
    private bool isLookingAtPoint = false; // กำลังหยุดกวาดมองเช็คจุดปัจจุบันอยู่ (ไม่ใช่แค่หยุดนิ่งเฉยๆ)
    private float lookTimer = 0f;

    // ════════════════════════════════════════════════════════
    //  DEBUG
    // ════════════════════════════════════════════════════════

    [Header("── Debug Visualization ──")]
    public bool  showInfluenceGrid = false;

    // ════════════════════════════════════════════════════════
    //  STATE
    // ════════════════════════════════════════════════════════

    public enum BehaviorState
    {
        Patrol, Alert, Search, Flee, Kite, Aggressive,
        SeekCover, InCover, ReturnToWaypoint, Dodge
    }
    [HideInInspector] public BehaviorState currentBehaviorState = BehaviorState.Patrol;
    private BehaviorState state
    {
        get => currentBehaviorState;
        set => currentBehaviorState = value;
    }
    private BehaviorState stateBeforeDodge;

    // ════════════════════════════════════════════════════════
    //  INTERNAL
    // ════════════════════════════════════════════════════════

    private Rigidbody2D rb;
    private Animator    anim;
    private Transform   player;
    private Rigidbody2D playerRb;
    private PlayerStatus playerStatus;
    private EnemyWeaponSystem weapon;   // ← NEW: WeaponSystem

    private Vector2 currentVelocity = Vector2.zero;
    [HideInInspector] public Vector2 lastKnownPos = Vector2.zero;
    [HideInInspector] public Vector2 lastKnownVel = Vector2.zero;
    [HideInInspector] public bool    hasLastKnown = false;

    [HideInInspector] public Vector2 bestCell   = Vector2.zero;
    private float   remapTimer      = 0f;
    private float   remapInterval   = 0.3f;

    private int   waypointIndex = 0;
    private float waitTimer     = 0f;
    private bool  isWaiting     = false;
    private float waypointTimer = 0f;

    private float fireTimer  = 0f;   // ใช้เฉพาะ legacy (ไม่มี WeaponSystem)
    private float dodgeTimer = 0f;
    private bool  isDodging  = false;
    private Vector2 dodgeDir;

    private Transform currentCover  = null;
    private float peekTimer         = 0f;
    private bool  isPeeking         = false;

    private float alertTimer = 0f;
    private float sightLostGraceTimer = 0f;

    private Vector2 stuckCheckPos   = Vector2.zero;
    private Vector2 spawnPosition   = Vector2.zero;
    private Vector2 wanderTarget    = Vector2.zero;
    /// <summary>จุดหมายที่กำลังเดินเล่นสำรวจไปอยู่ตอนนี้ (Read-only) — ใช้โดย SquadDebugDisplay เพื่อวาดเส้นยืนยันว่าระบบกระจายพื้นที่ทำงานจริง</summary>
    public Vector2 WanderTargetDebug => wanderTarget;
    private Vector2 cachedWaypoint  = Vector2.zero;
    private Vector2 cachedPathTarget = Vector2.zero;
    private bool    hasCachedWaypoint = false;
    private float   pathRecalcTimer = 0f;
    private bool    hasWanderTarget = false;
    private float   stuckCheckTimer = 0f;
    private float   stuckRecovTimer = 0f;
    private Vector2 stuckRecovDir   = Vector2.zero;
    private bool    isStuckRecovering = false; // กำลังกู้ตัวเองออกจากจุดติดขัดอยู่ไหม (ใช้บอก Seek() ว่าควรหันหน้าตามทิศไหน)

    private struct GridDebug { public Vector2 pos; public float score; }
    private List<GridDebug> debugGrid = new List<GridDebug>();

    // ════════════════════════════════════════════════════════
    //  INIT
    // ════════════════════════════════════════════════════════

    // ════════════════════════════════════════════════════════
    //  SELF-REGISTRY (แทนการพึ่ง FindObjectsByType ที่บางโปรเจกต์หาไม่เจอ)
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// รายชื่อ EnemyAI ที่ Active อยู่ทั้งหมดในฉากตอนนี้ — แต่ละตัวลงทะเบียนตัวเองใน OnEnable() และถอนออกใน OnDisable()
    /// ใช้แทน FindObjectsByType&lt;EnemyAI&gt;() ที่บางเครื่อง/บางโปรเจกต์หา instance ไม่เจอ (สาเหตุที่พบบ่อยคือ
    /// มีไฟล์ EnemyAI.cs ซ้ำซ้อนกันในโปรเจกต์ ทำให้ Reflection สับสนว่าจะหา Type ไหน) — การลงทะเบียนตัวเองแบบนี้
    /// ไม่ต้องพึ่ง Reflection เลย เชื่อถือได้ 100% ไม่ว่าจะมีปัญหาอะไรกับโปรเจกต์ก็ตาม
    /// EnemySquadManager และ SquadDebugDisplay ใช้ตัวนี้แทนการเรียก FindObjectsByType โดยตรง
    /// </summary>
    public static readonly List<EnemyAI> AllActive = new List<EnemyAI>();

    void OnEnable()
    {
        if (!AllActive.Contains(this)) AllActive.Add(this);
        // Log ยืนยันตัว: ถ้ากด Play แล้วไม่เห็นข้อความนี้ใน Console เลยสักครั้ง ทั้งที่เห็น AI เคลื่อนไหวอยู่ในเกม
        // แปลว่า AI ตัวที่กำลังรันอยู่จริงๆ ไม่ได้ใช้โค้ดไฟล์นี้ (มีไฟล์ EnemyAI.cs ซ้ำอยู่ที่อื่นในโปรเจกต์แน่นอน)
        Debug.Log($"[EnemyAI] ลงทะเบียนตัวเองแล้ว: {gameObject.name} (ตอนนี้มีทั้งหมด {AllActive.Count} ตัว) จากไฟล์ {System.Reflection.Assembly.GetAssembly(typeof(EnemyAI)).GetName().Name}");
    }

    void OnDisable()
    {
        AllActive.Remove(this);
    }

    void Start()
    {
        rb   = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        weapon = GetComponent<EnemyWeaponSystem>(); // auto-find WeaponSystem

        currentHealth     = maxHealth;
        // สำคัญมาก: บังคับให้เป็น Kinematic เสมอ เพราะโค้ด AI ทั้งหมดในไฟล์นี้เขียนตำแหน่ง/ความเร็วผ่าน
        // rb.linearVelocity และ transform.position/rotation ตรงๆ เอง ถ้า Rigidbody2D เป็น Dynamic (ค่า Default
        // ของ Unity) พอตัวศัตรู 2 ตัวชนกัน/ชิดกัน Physics Engine จะเสริมแรงผลักกันเองอัตโนมัติ (Collision Response)
        // ซ้อนทับกับความเร็วที่สคริปต์สั่งไว้ กลายเป็นสองระบบแย่งกันคุมตำแหน่งเดียวกัน ทำให้เกิดอาการ "สไลด์" หรือ
        // เคลื่อนที่แปลกๆ ที่สคริปต์ไม่ได้สั่งเลย (บั๊กที่เจอตอนทดสอบ: ตัวสไลด์ไปเองระหว่างช่วงหยุดกวาดมอง)
        rb.bodyType       = RigidbodyType2D.Kinematic;
        rb.gravityScale   = 0f;
        rb.freezeRotation = true;
        // เปิด Continuous Collision Detection กันทะลุกำแพงบางๆ ตอนความเร็วสูง (Discrete ค่า Default ของ Unity
        // จะข้ามสิ่งกีดขวางบางๆ ไปได้ในเฟรมเดียวถ้าความเร็วมากพอ ทำให้ดูเหมือนเดินทะลุกำแพงได้แบบไม่มีปัญหาเลย)
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        stuckCheckPos     = transform.position;
        spawnPosition     = transform.position; // เก็บไว้ใช้เดินเล่นวนรอบ ถ้าไม่ได้ตั้งค่า Waypoints เอาไว้เลย

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player       = p.transform;
            playerRb     = p.GetComponent<Rigidbody2D>();
            playerStatus = p.GetComponent<PlayerStatus>();
        }
        else Debug.LogError("[EnemyAI] ไม่พบ Player!");

        // Auto-find Squad
        if (squad == null)
            squad = FindFirstObjectByType<EnemySquadManager>();

        if (waypoints != null && waypoints.Length > 0)
        { waypointIndex = Random.Range(0, waypoints.Length); waypointTimer = waypointTimeout; }
    }

    // ════════════════════════════════════════════════════════
    //  UPDATE — Sense-Think-Act Loop
    // ════════════════════════════════════════════════════════

    void Update()
    {
        if (player == null) return;

        bool wasRawSeeingPlayer = rawCanSeePlayer; // ค่า "เห็นจริงๆ" ก่อนหน้า ไม่ผ่านช่วงผ่อนผัน Sight Persistence

        fireTimer    -= Time.deltaTime;
        dodgeTimer   -= Time.deltaTime;
        remapTimer   -= Time.deltaTime;
        alertTimer   -= Time.deltaTime;
        searchTimer  -= Time.deltaTime;

        // ── 1. SENSE ──
        ScanFOV();
        distToPlayer = Vector2.Distance(transform.position, player.position);

        // ── เพิ่งเสีย Sight "จริงๆ" พอดี (ไม่ใช่ตอนช่วงผ่อนผันหมด): หาว่ากำแพงตัวไหนที่บังสายตาจริงๆ ──
        // ต้องใช้ rawCanSeePlayer (ไม่ผ่าน Sight Persistence) ไม่ใช่ canSeePlayer ที่ถูกหน่วงเวลาไว้
        // เพราะถ้าใช้ canSeePlayer จังหวะตรวจจับจะช้าไปกว่าตอนเสียสายตาจริง ทำให้ Raycast จากตำแหน่งปัจจุบัน
        // (ที่อาจขยับไปแล้วระหว่างช่วงผ่อนผัน) ไปยัง lastKnownPos (ที่ค้างอยู่จุดเดิม) พลาดกำแพงตัวที่บังจริงได้
        // ผลคือบางครั้งจับกำแพงไม่ได้เลย (lastBlockingWall เป็น null) ทั้งที่รู้แน่ๆ ว่าผู้เล่นอ้อมกำแพงตัวนี้ไปแน่นอน
        if (wasRawSeeingPlayer && !rawCanSeePlayer && hasLastKnown)
        {
            timeSightLost = Time.time; // เริ่มนับเวลา ใช้คำนวณว่าผู้เล่นน่าจะเดินไปได้ไกลแค่ไหนแล้ว

            Vector2 toLastKnown = lastKnownPos - (Vector2)transform.position;
            if (toLastKnown.sqrMagnitude > 0.01f)
            {
                RaycastHit2D wallHit = Physics2D.Raycast(transform.position, toLastKnown.normalized, toLastKnown.magnitude, wallLayer);
                lastBlockingWall = wallHit.collider; // อาจเป็น null ถ้าหาไม่เจอจริงๆ (DoSearch จะ fallback เอง)
            }
        }

        // ── บันทึก 'รอยเท้า' ผู้เล่นขณะยังเห็นอยู่ (สัญญาณอ่อนๆ ใช้เดาทิศตอนค้นหาทีหลัง) ──
        if (canSeePlayer)
        {
            trailTimer -= Time.deltaTime;
            if (trailTimer <= 0f)
            {
                playerTrail.Add(player.position);
                if (playerTrail.Count > trailMaxPoints) playerTrail.RemoveAt(0);
                trailTimer = trailRecordInterval;
            }
        }
        else if (!hasLastKnown)
        {
            playerTrail.Clear(); // ไม่มีความจำเหลือแล้ว เริ่มรอบใหม่ตอนเจอครั้งถัดไป
        }

        // ── Squad Alert ──────────────────────────────────────
        if (canSeePlayer && broadcastAlertOnSight && !hasAlerted && squad != null)
        {
            squad.BroadcastAlert(player.position, this);
            hasAlerted = true;
        }
        if (!canSeePlayer) hasAlerted = false; // reset เมื่อเสีย sight

        // ── Dodge ──
        if (!isDodging && state != BehaviorState.Dodge)
            CheckIncomingBullets();

        // ── 2. THINK ──
        if (remapTimer <= 0f)
        {
            remapTimer = remapInterval;
            float playerMana = playerStatus != null ? playerStatus.currentMana : 50f;
            bestCell = ComputeInfluenceMap(playerMana);
        }

        if (state != BehaviorState.Dodge)
            SelectBehavior();

        // ── 3. ACT ──
        ExecuteBehavior();

        if (anim)
        {
            anim.SetFloat("Speed", rb.linearVelocity.magnitude);
            anim.SetBool("IsAttacking",
                state == BehaviorState.Kite || state == BehaviorState.Aggressive || state == BehaviorState.InCover);
            anim.SetBool("InCover",   state == BehaviorState.InCover);
            anim.SetBool("IsDodging", state == BehaviorState.Dodge);
            // Reload animation
            if (weapon != null)
                anim.SetBool("IsReloading", weapon.IsReloading);
        }
    }

    // ════════════════════════════════════════════════════════
    //  PERCEPTION
    // ════════════════════════════════════════════════════════

    void ScanFOV()
    {
        bool rawCanSee = false;
        Vector2 facing = transform.up;
        float   half   = fovAngle * 0.5f;
        float   step   = fovAngle / (fovRayCount - 1);

        for (int i = 0; i < fovRayCount; i++)
        {
            Vector2 dir = Quaternion.Euler(0,0,-half+step*i) * facing;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, fovRange);
            if (hit.collider != null && hit.collider.CompareTag("Player"))
            { rawCanSee = true; break; }
        }

        if (!rawCanSee && distToPlayer <= peripheralRange)
        {
            Vector2 toP = (Vector2)player.position - (Vector2)transform.position;
            if (!Physics2D.Raycast(transform.position, toP.normalized, toP.magnitude, wallLayer))
                rawCanSee = true;
        }

        // Sight Persistence: กันลืมเร็วเกินไปตอนเห็นแวบเดียวหาย (เช่นผู้เล่นเดินผ่านมุมกำแพง/สิ่งกีดขวางแค่ชั่วครู่เดียว)
        // ของเดิม canSeePlayer เปลี่ยนเป็น false ทันทีที่ Raycast เฟรมนั้นไม่เจอผู้เล่น ทำให้ดูเหมือน "ลืมเร็วมาก"
        // ทั้งที่จริงๆ ผู้เล่นแค่บังแวบเดียวแล้วก็โผล่ต่อ — ตอนนี้จะยังถือว่า "เห็นอยู่" ต่อไปอีก sightPersistenceDuration
        // วินาที แม้ Raycast เฟรมนี้จะไม่เจอแล้วจริงๆ ก็ตาม กันการลืม/เข้าสู่โหมดค้นหาเร็วเกินไปโดยไม่จำเป็น
        rawCanSeePlayer = rawCanSee; // เก็บค่าดิบไว้ให้ Update() ใช้จับจังหวะเสียสายตาจริง (ไม่ผ่านช่วงผ่อนผัน)

        if (rawCanSee)
        {
            canSeePlayer = true;
            sightLostGraceTimer = sightPersistenceDuration;
        }
        else if (sightLostGraceTimer > 0f)
        {
            sightLostGraceTimer -= Time.deltaTime;
            canSeePlayer = true; // ยังอยู่ในช่วงผ่อนผัน ถือว่าเห็นต่อไปก่อน
        }
        else
        {
            canSeePlayer = false;
        }

        if (rawCanSee) // อัปเดตตำแหน่ง/ความเร็วล่าสุดเฉพาะตอนเห็นจริงๆ เท่านั้น ไม่ใช่ช่วงผ่อนผัน (กันตำแหน่งเพี้ยน)
        {
            lastKnownPos = player.position;
            lastKnownVel = playerRb != null ? playerRb.linearVelocity : Vector2.zero;
            hasLastKnown = true;
        }
    }

    // ════════════════════════════════════════════════════════
    //  INFLUENCE MAP
    // ════════════════════════════════════════════════════════

    Vector2 ComputeInfluenceMap(float playerMana)
    {
        float   bestScore  = -9999f;
        Vector2 bestTarget = transform.position;
        debugGrid.Clear();

        Vector2 pos = transform.position;
        Collider2D[] bullets = Physics2D.OverlapCircleAll(pos, cellSize * gridRadius * 1.5f, bulletLayer);

        Vector2[] bulletVels = new Vector2[bullets.Length];
        for (int b = 0; b < bullets.Length; b++)
        {
            var bRb = bullets[b].GetComponent<Rigidbody2D>();
            bulletVels[b] = bRb != null ? bRb.linearVelocity.normalized : Vector2.zero;
        }

        bool isPlayerLowMana = playerMana < lowManaThreshold;

        for (int gx = -gridRadius; gx <= gridRadius; gx++)
        {
            for (int gy = -gridRadius; gy <= gridRadius; gy++)
            {
                Vector2 cellPos = pos + new Vector2(gx * cellSize, gy * cellSize);
                if (Physics2D.OverlapCircle(cellPos, cellSize * 0.4f, wallLayer)) continue;

                float dangerScore = 0f, safetyScore = 0f, utilityBonus = 0f;

                for (int b = 0; b < bullets.Length; b++)
                {
                    if (!bullets[b].CompareTag("Bullet")) continue;
                    Vector2 bulletPos = bullets[b].transform.position;
                    Vector2 bulletDir = bulletVels[b];
                    float   dist      = Vector2.Distance(cellPos, bulletPos);
                    float   maxR      = cellSize * 4f;
                    if (dist > maxR) continue;
                    Vector2 toCell = (cellPos - bulletPos).normalized;
                    float   dot    = Vector2.Dot(bulletDir, toCell);
                    if (dot > 0.85f)       dangerScore += 100f;
                    else if (dot > 0.5f)   dangerScore += 50f;
                    else                   dangerScore += 30f * (1f - dist / maxR);
                }

                if (player != null)
                {
                    Vector2 toCell = cellPos - (Vector2)player.position;
                    RaycastHit2D coverHit = Physics2D.Raycast(
                        player.position, toCell.normalized, toCell.magnitude, wallLayer);
                    if (coverHit.collider != null) safetyScore = 50f;
                }

                if (isPlayerLowMana && CanSeePlayerFrom(cellPos)) utilityBonus = 30f;

                float finalScore = safetyScore - dangerScore + utilityBonus;

                Vector2 toTarget = cellPos - pos;
                float   distToCell = toTarget.magnitude;
                RaycastHit2D reach = Physics2D.Raycast(pos, toTarget.normalized, distToCell, wallLayer);
                if (reach.collider != null)
                    finalScore -= 80f * (1f - reach.distance / distToCell);

                if (finalScore > bestScore) { bestScore = finalScore; bestTarget = cellPos; }
                if (showInfluenceGrid)
                    debugGrid.Add(new GridDebug { pos = cellPos, score = finalScore });
            }
        }

        return bestTarget;
    }

    // ════════════════════════════════════════════════════════
    //  DECISION MAKING
    // ════════════════════════════════════════════════════════

    void SelectBehavior()
    {
        if (IsLowHealth && state != BehaviorState.InCover && state != BehaviorState.SeekCover)
        { TriggerSeekCover(); return; }

        float playerMana  = playerStatus != null ? playerStatus.currentMana : 50f;
        bool  isAggressive = playerMana < lowManaThreshold;

        // ── Squad Courage: ถ้ามีเพื่อนอยู่ใกล้ๆ หลายตัว (ได้เปรียบด้านจำนวน) ให้กล้าเข้าปะทะมากขึ้น ──
        // แม้ผู้เล่น Mana จะยังสูงอยู่ก็ตาม — ไม่งั้นทุกครั้งที่เริ่มเกมใหม่ (ผู้เล่น Mana เต็มเสมอ)
        // ศัตรูทุกตัวจะหนีพร้อมกันหมดทันทีที่เห็นผู้เล่น ดูไม่เป็นธรรมชาติ ทั้งที่จริงๆ มีจำนวนได้เปรียบกว่ามาก
        if (!isAggressive && squad != null)
        {
            int nearbyAllies = 0;
            foreach (var ally in AllActive)
            {
                if (ally == this || ally == null) continue;
                if (Vector2.Distance(transform.position, ally.transform.position) < allyCourageRadius)
                    nearbyAllies++;
            }
            if (nearbyAllies >= minAlliesForCourage) isAggressive = true;
        }

        switch (state)
        {
            case BehaviorState.Patrol:
                if (canSeePlayer || alertTimer > 0f)
                    state = isAggressive ? BehaviorState.Aggressive : BehaviorState.Flee;
                break;
            case BehaviorState.Alert:
                if (canSeePlayer)
                    state = isAggressive ? BehaviorState.Aggressive : BehaviorState.Flee;
                else if (Vector2.Distance(transform.position, lastKnownPos) < waypointReachDistance)
                {
                    // ถึงจุดสุดท้ายที่เห็นผู้เล่นแล้ว แต่ไม่เจอ -> เข้าสู่โหมดค้นหาต่อ (เดินอ้อมไปดูหลังกำแพง)
                    state = BehaviorState.Search;
                    searchTimer = searchMaxDuration;
                    hasSearchOvershootTarget = false;
                    isLookingAtPoint = false;

                    // เรียกเพื่อนในทีมมาช่วยหาด้วย (เดิมแจ้งแค่ตอนเห็นผู้เล่นครั้งแรก ไม่เคยแจ้งตอนเข้าโหมดค้นหาเลย
                    // ทำให้ตอนทดสอบหลายตัว เพื่อนที่ยัง Patrol อยู่ไม่มาช่วยหาด้วยเลยแม้จะหาไม่เจอนานแล้วก็ตาม)
                    if (squad != null && broadcastAlertOnSight)
                        squad.BroadcastAlert(lastKnownPos, this);
                }
                else if (alertTimer <= 0f && !hasLastKnown)
                { PickRandomWaypoint(); state = BehaviorState.ReturnToWaypoint; }
                break;
            case BehaviorState.Search:
                if (canSeePlayer)
                {
                    state = isAggressive ? BehaviorState.Aggressive : BehaviorState.Flee;
                }
                else if (searchTimer <= 0f)
                {
                    // หาไม่เจอภายในเวลาที่กำหนด -> ยอมแพ้ ลืมตำแหน่งผู้เล่น กลับไปลาดตระเวนตามปกติ
                    hasLastKnown = false;
                    playerTrail.Clear();
                    lastBlockingWall = null;
                    PickRandomWaypoint();
                    state = BehaviorState.ReturnToWaypoint;
                }
                break;
            case BehaviorState.Flee:
                if (canSeePlayer && distToPlayer >= kiteMinDist) state = BehaviorState.Kite;
                else if (isAggressive && canSeePlayer)           state = BehaviorState.Aggressive;
                else if (!canSeePlayer && !hasLastKnown)
                { PickRandomWaypoint(); state = BehaviorState.ReturnToWaypoint; }
                break;
            case BehaviorState.Kite:
                if (!canSeePlayer)               state = BehaviorState.Flee;
                else if (distToPlayer < kiteMinDist) state = BehaviorState.Flee;
                else if (isAggressive)           state = BehaviorState.Aggressive;
                break;
            case BehaviorState.Aggressive:
                if (!canSeePlayer && !hasLastKnown)
                { PickRandomWaypoint(); state = BehaviorState.ReturnToWaypoint; }
                else if (!isAggressive && distToPlayer > aggressRadius * 2f)
                    state = BehaviorState.Kite;
                break;
            case BehaviorState.ReturnToWaypoint:
                if (canSeePlayer || alertTimer > 0f)
                    state = isAggressive ? BehaviorState.Aggressive : BehaviorState.Flee;
                break;
        }
    }

    // ════════════════════════════════════════════════════════
    //  ACTION EXECUTION
    // ════════════════════════════════════════════════════════

    void ExecuteBehavior()
    {
        if (useNavMesh)
        {
            if (canSeePlayer && player != null) RotateToward(player.position);

            // ── ใช้ WeaponSystem ถ้ามี ────────────────────────
            if ((state == BehaviorState.Kite || state == BehaviorState.Aggressive)
                && canSeePlayer)
            {
                if (weapon != null)
                    weapon.TryShoot(player.position, lastKnownVel);
                else if (fireTimer <= 0f)
                { ShootAt(PredictPlayerPos(), aimInaccuracy); fireTimer = fireCooldown; }
            }
            return;
        }

        switch (state)
        {
            case BehaviorState.Patrol:            DoPatrol();            break;
            case BehaviorState.Alert:             DoAlert();             break;
            case BehaviorState.Search:             DoSearch();            break;
            case BehaviorState.Flee:              DoFlee();              break;
            case BehaviorState.Kite:              DoKite();              break;
            case BehaviorState.Aggressive:        DoAggressive();        break;
            case BehaviorState.SeekCover:         DoSeekCover();         break;
            case BehaviorState.InCover:           DoInCover();           break;
            case BehaviorState.ReturnToWaypoint:  DoReturnToWaypoint();  break;
            case BehaviorState.Dodge:                                    break;
        }
    }

    // ── PATROL ──────────────────────────────────────────────
    void DoPatrol()
    {
        if (waypoints == null || waypoints.Length == 0) { DoWanderNearSpawn(); return; }
        if (isWaiting)
        {
            Seek(transform.position);
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f) { isWaiting = false; PickRandomWaypoint(); }
            return;
        }
        waypointTimer -= Time.deltaTime;
        if (waypointTimer <= 0f) { PickRandomWaypoint(); return; }
        Seek(waypoints[waypointIndex].position, patrolMode: true);
        if (Vector2.Distance(transform.position, waypoints[waypointIndex].position) < waypointReachDistance)
        { isWaiting = true; waitTimer = waypointWaitTime; waypointTimer = waypointTimeout; }
    }

    /// <summary>
    /// Fallback ตอนไม่ได้ตั้งค่า Waypoints ไว้เลย — เดินเล่นสุ่มไปมาแทน ไม่งั้น DoPatrol()/DoReturnToWaypoint()
    /// แบบเดิมจะ "return เฉยๆ" ทันทีที่เจอ waypoints ว่าง ทำให้ตัว AI ยืนนิ่งสนิทค้างอยู่กับที่ตลอดไป
    /// ทุกครั้งที่กลับเข้าสู่ Patrol/ReturnToWaypoint (บั๊กที่เจอตอนทดสอบ)
    ///
    /// ถ้ามี Squad และตั้งค่า Map Center/Size ไว้แล้ว (useSquadCoverage) จะขอจุดกระจายตัวจาก Squad แทน
    /// ซึ่งจะเลือกพื้นที่ที่ยังไม่มีเพื่อนตัวอื่นคุมอยู่ ครอบคลุมทั่วทั้งแมพจริงๆ — แก้ปัญหาที่เจอว่า
    /// ถ้าใช้แค่ "วนใกล้จุดเกิดตัวเอง" (spawnPosition) แล้วจุดเกิดของทุกตัวกระจุกกันอยู่ฝั่งเดียวของแมพ
    /// จะไม่มี AI ตัวไหนเดินไปสำรวจฝั่งอื่นเลยตลอดไป แม้ผู้เล่นจะยืนอยู่แถวนั้นก็ตาม
    /// ถ้าไม่มี Squad หรือยังไม่ได้ตั้งค่า Map Size ไว้ จะ Fallback ไปใช้วิธีวนใกล้จุดเกิดตัวเองแบบเดิม
    /// </summary>
    void DoWanderNearSpawn()
    {
        if (!hasWanderTarget || Vector2.Distance(transform.position, wanderTarget) < waypointReachDistance)
        {
            if (useSquadCoverage && squad != null && squad.mapSize.sqrMagnitude > 1f)
                wanderTarget = squad.GetSpreadOutPosition(this);
            else
                wanderTarget = spawnPosition + Random.insideUnitCircle * noWaypointWanderRadius;

            hasWanderTarget = true;
            // ถ้ากำลังอยู่ใน ReturnToWaypoint (กลับมาจากการค้นหา) ให้กลับเข้าสู่ Patrol ตามปกติทันที
            // ไม่งั้นจะค้างอยู่ใน ReturnToWaypoint ตลอดไปเหมือนกัน เพราะไม่มี waypoints[] ให้เช็คระยะถึง
            if (state == BehaviorState.ReturnToWaypoint) state = BehaviorState.Patrol;
        }

        Seek(wanderTarget, patrolMode: true);
    }

    // ── ALERT: รีบเดินไปยังจุดที่เห็นผู้เล่นล่าสุดให้เร็วที่สุด (เฟส 1: Rush) ──
    void DoAlert()
    {
        if (hasLastKnown) Seek(lastKnownPos, maxSpeed * alertRushSpeedMultiplier, patrolMode: true);
        else rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.deltaTime * 5f);
    }

    // ── SEARCH: เดินอ้อมไปดูหลังกำแพง/สิ่งกีดขวาง โดยเดาทิศจากรอยเท้าผู้เล่นล่าสุด (เฟส 2: Tactical Sweep) ──
    // เดินช้าลง + หยุดกวาดมองเช็คทีละจุดก่อนไปจุดถัดไป (เหมือนหน่วยรบเคลียร์มุมอับทีละมุม)
    // เช็คครบทุกจุดแล้วไม่เจอ จะเลิกค้นหาทันที ไม่ต้องรอ searchTimer หมดเฉยๆ (ลดอาการ "ค้างนิ่ง" ที่ดูไม่ฉลาด)
    void DoSearch()
    {
        if (isLookingAtPoint)
        {
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.deltaTime * 8f);
            lookTimer -= Time.deltaTime;

            // หันกวาดจากซ้ายสุดไปขวาสุดทีเดียวแบบนุ่มนวล (SmoothStep) เหมือนกำลังเช็คมุมอับจริงๆ
            // (ของเดิมใช้ Sin เต็มรอบ ทำให้กวาดไปมา 2 รอบเต็มภายในเวลาสั้นๆ ดูเหมือนหมุนติ้วๆ ไม่ใช่เช็คมุมอับ)
            float t = 1f - Mathf.Clamp01(lookTimer / searchLookDuration);
            float angleOffset = Mathf.SmoothStep(-searchLookSweepAngle, searchLookSweepAngle, t);
            Vector2 baseDir = (searchOvershootTarget - (Vector2)transform.position);
            if (baseDir.sqrMagnitude < 0.01f) baseDir = transform.up;
            float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, baseAngle + angleOffset);

            if (lookTimer <= 0f)
            {
                isLookingAtPoint = false;
                searchPhase++;

                if (searchPhase < searchSweepPoints)
                {
                    // เช็คจุดนี้ครบแล้ว ไม่เจอ -> เดินสำรวจลึกเข้าไปอีกจุดในทิศทางเดียวกัน
                    Vector2 dir = GetSearchDirection();
                    searchOvershootTarget = searchOvershootTarget + dir * (ComputeAdaptiveSearchDistance() * 0.4f);
                    hasSearchOvershootTarget = true;
                }
                else
                {
                    // เช็คครบทุกจุดที่ตั้งใจแล้วไม่เจอจริงๆ -> เลิกค้นหาทันที กลับไปลาดตระเวน (ไม่รอ searchTimer หมดเฉยๆ)
                    hasLastKnown = false;
                    playerTrail.Clear();
                    lastBlockingWall = null;
                    PickRandomWaypoint();
                    state = BehaviorState.ReturnToWaypoint;
                }
            }
            return;
        }

        if (!hasSearchOvershootTarget)
        {
            searchOvershootTarget = ComputeFlankTarget(GetSearchDirection());
            hasSearchOvershootTarget = true;
            searchPhase = 0;
        }

        Seek(searchOvershootTarget, maxSpeed * searchWalkSpeedMultiplier, patrolMode: true);

        if (Vector2.Distance(transform.position, searchOvershootTarget) < waypointReachDistance)
        {
            // ถึงจุดนี้แล้ว -> หยุดกวาดมองเช็คก่อน ไม่ใช่เดินผ่านจุดต่อไปเลย (ให้ดูเหมือนกำลังตรวจสอบจริงๆ)
            isLookingAtPoint = true;
            lookTimer = searchLookDuration;
        }
    }

    /// <summary>
    /// ทิศทางที่จะใช้เดินสำรวจ — ผสมสัญญาณ 2 แหล่งเข้าด้วยกันแทนที่จะเลือกใช้แค่อย่างเดียว เพื่อความแม่นยำสูงสุด:
    /// 1) ความเร็วสดตอนเพิ่งเสียสายตา (lastKnownVel) — น้ำหนักหลัก เพราะเป็นข้อมูลล่าสุดจริง จับทิศตอน "หักมุม" ได้แม่น
    /// 2) รอยเท้าที่ถ่วงน้ำหนักตามความใหม่ (EstimateFleeDirection) — ช่วยกันสัญญาณรบกวน/ความเร็วกระตุกผิดปกติชั่วขณะ
    /// ปรับสัดส่วนน้ำหนักได้ที่ Velocity Direction Weight (ค่าเริ่มต้น 0.7 = เชื่อความเร็วสด 70%)
    ///
    /// ถ้ามีศัตรูหลายตัวกำลัง Search พร้อมกัน (ผ่าน Squad) จะแบ่งทิศทางออกคนละฝั่งด้วย (squadSearchSpreadAngle)
    /// แบ่งซ้าย-ขวาสมมาตรรอบทิศทางที่คาดเดาไว้ทันที: มี 2 ตัว -> ตัวหนึ่งไปซ้าย อีกตัวไปขวา พร้อมกันเลย
    /// มีมากกว่านั้น -> ตัวถัดๆ ไปในฝั่งเดียวกันจะกว้างออกไปอีกชั้น กันเดินไปจุดเดียวกันหมดทุกตัว
    /// </summary>
    Vector2 GetSearchDirection()
    {
        Vector2 velDir = lastKnownVel.sqrMagnitude > 0.05f ? lastKnownVel.normalized : Vector2.zero;
        Vector2 trailDir = EstimateFleeDirection();

        Vector2 baseDir;
        if (velDir != Vector2.zero && trailDir != Vector2.zero)
        {
            Vector2 blended = velDir * velocityDirectionWeight + trailDir * (1f - velocityDirectionWeight);
            baseDir = blended.sqrMagnitude > 0.0001f ? blended.normalized : velDir;
        }
        else if (velDir != Vector2.zero) baseDir = velDir;
        else if (trailDir != Vector2.zero) baseDir = trailDir;
        else baseDir = (Vector2)transform.up;

        // แบ่งทิศทางกับเพื่อนที่กำลังค้นหาพร้อมกันอยู่ (ถ้ามี Squad และมีมากกว่า 1 ตัวที่กำลัง Search)
        // แบ่งซ้าย-ขวาสมมาตรรอบทิศทางที่คาดเดาไว้ทันที: ตัวลำดับคู่ (0,2,4...) ไปทางซ้าย, ตัวลำดับคี่ (1,3,5...) ไปทางขวา
        // เช่น มี 2 ตัวช่วยกันหา -> ตัวแรกไปซ้าย ตัวที่สองไปขวา ทันที (ไม่ใช่ตัวแรกเดินตรงแล้วตัวรองค่อยแยก)
        // ตัวที่ 3, 4 ขึ้นไปในฝั่งเดียวกัน จะกว้างออกไปอีกชั้น (Sector) กันเดินทับเส้นทางตัวแรกของฝั่งเดียวกัน
        // จำกัด Sector สูงสุดไว้ที่ 3 ชั้น กันตัวหลังๆ ในกลุ่มใหญ่มากถูกส่งไปไกลเกินพื้นที่ค้นหาจริง
        if (squad != null)
        {
            int index = squad.GetSearchIndex(this, out int totalSearching);
            if (index >= 0 && totalSearching > 1)
            {
                int side = (index % 2 == 0) ? -1 : 1; // คู่ = ซ้าย, คี่ = ขวา
                int sector = Mathf.Min((index / 2) + 1, 3);
                float angle = side * squadSearchSpreadAngle * sector;
                baseDir = Quaternion.Euler(0, 0, angle) * baseDir;
            }
        }

        return baseDir;
    }

    /// <summary>
    /// คำนวณระยะที่ควรเดินอ้อมไปเช็ค โดยอิงจาก "เวลาที่ผ่านไปจริง" ตั้งแต่เสียสายตา คูณความเร็วผู้เล่นโดยประมาณ
    /// (ยิ่งเสียสายตานานเท่าไหร่ ผู้เล่นก็น่าจะเดินไปได้ไกลเท่านั้น) แทนการใช้ระยะคงที่ตายตัวแบบเดิม
    /// ซึ่งไม่สนใจเลยว่าเวลาผ่านไปนานแค่ไหนก่อนที่ AI จะเริ่มค้นหาจริงๆ (Rush ไปยัง Alert ก่อน กว่าจะถึง Search ก็ผ่านไปแล้วหลายวินาที)
    /// </summary>
    float ComputeAdaptiveSearchDistance()
    {
        float elapsed = Mathf.Max(0f, Time.time - timeSightLost);
        float speed = lastKnownVel.magnitude > 0.1f ? lastKnownVel.magnitude : assumedPlayerSpeed;
        return Mathf.Clamp(speed * elapsed, minSearchDistance, maxSearchDistance);
    }

    /// <summary>
    /// คำนวณจุดแรกที่ควรเดินไปเช็ค โดยยึด "กำแพงตัวที่บังสายตาจริง" (lastBlockingWall) เป็นหลัก
    /// ใช้ระยะ wallHugDistance (ค่าคงที่ ไม่ใช่ขนาดกำแพง) เกาะชิดขอบกำแพงตรงจุดที่ใกล้ผู้เล่นล่าสุดที่สุด
    /// แล้วอ้อมต่อไปในทิศที่ผู้เล่นวิ่งหนี (fleeDir) ด้วยระยะที่คำนวณแบบอิงเวลาจริง (ComputeAdaptiveSearchDistance)
    /// แทนระยะคงที่ตายตัว — แก้จากเวอร์ชันก่อนที่ใช้ขนาดกำแพงเองมาคำนวณระยะอ้อม ซึ่งถ้ากำแพงยาว/สูงมาก
    /// จะได้จุดที่ไกลเกินจริงมาก ไม่ได้เดินชิดกำแพงเลย
    /// ถ้าไม่รู้ว่ากำแพงตัวไหนบัง (lastBlockingWall เป็น null) จะ fallback ไปเดินอ้อมแบบเส้นตรงแทน
    /// </summary>
    Vector2 ComputeFlankTarget(Vector2 fleeDir)
    {
        float adaptiveDistance = ComputeAdaptiveSearchDistance();

        if (lastBlockingWall == null)
            return lastKnownPos + fleeDir * adaptiveDistance;

        Bounds wallBounds = lastBlockingWall.bounds;

        // จุดบนกำแพงที่ใกล้ตำแหน่งผู้เล่นล่าสุดที่สุด (มุม/ขอบที่ผู้เล่นเพิ่งเดินผ่านไปจริงๆ)
        Vector2 closestOnWall = wallBounds.ClosestPoint(lastKnownPos);

        Vector2 toWall = (Vector2)wallBounds.center - lastKnownPos;
        if (toWall.sqrMagnitude < 0.01f) toWall = (Vector2)wallBounds.center - (Vector2)transform.position;
        toWall.Normalize();
        Vector2 perpendicular = new Vector2(-toWall.y, toWall.x);

        // เกาะชิดขอบกำแพงด้วยระยะคงที่ (wallHugDistance) แล้วอ้อมต่อไปในทิศที่ผู้เล่นวิ่งหนีด้วยระยะที่คาดการณ์ไว้
        Vector2 sideA = closestOnWall + perpendicular * wallHugDistance + fleeDir * (adaptiveDistance * 0.5f);
        Vector2 sideB = closestOnWall - perpendicular * wallHugDistance + fleeDir * (adaptiveDistance * 0.5f);

        // เลือกฝั่งที่ตรงกับทิศทางที่ผู้เล่นวิ่งหนีมากกว่า (Dot Product สูงกว่า = ตรงทิศมากกว่า)
        float scoreA = Vector2.Dot((sideA - lastKnownPos).normalized, fleeDir);
        float scoreB = Vector2.Dot((sideB - lastKnownPos).normalized, fleeDir);

        return scoreA >= scoreB ? sideA : sideB;
    }

    /// <summary>
    /// ประมาณทิศทางที่ผู้เล่นวิ่งหนี จากรอยเท้า (playerTrail) ที่บันทึกไว้ตอนยังเห็นอยู่
    /// ถ่วงน้ำหนักให้ช่วง 'ล่าสุด' (ใกล้ตอนเสียสายตา) มีผลต่อทิศทางมากกว่าช่วงต้นๆ ของรอยเท้า
    /// เพราะทิศทางตอนกำลัง 'หักมุม' เข้ากำแพงสำคัญกว่าทิศทางตอนเดินมาก่อนหน้านั้นมาก
    /// (ของเดิมเฉลี่ยทุกช่วงเท่ากันหมด ทำให้ทิศทางเก่าที่เดินมานานกว่าถ่วงค่าเฉลี่ยให้เพี้ยนจากทิศจริงตอนหักมุม)
    /// </summary>
    Vector2 EstimateFleeDirection()
    {
        if (playerTrail.Count < 2) return Vector2.zero;

        Vector2 sum = Vector2.zero;
        float totalWeight = 0f;
        for (int i = 1; i < playerTrail.Count; i++)
        {
            Vector2 segment = playerTrail[i] - playerTrail[i - 1];
            if (segment.sqrMagnitude > 0.01f)
            {
                float weight = i; // index ยิ่งสูง (ยิ่งใหม่/ใกล้ตอนเสียสายตา) ยิ่งน้ำหนักเยอะ
                sum += segment.normalized * weight;
                totalWeight += weight;
            }
        }
        return totalWeight > 0f ? (sum / totalWeight).normalized : Vector2.zero;
    }

    void DoFlee() => Seek(bestCell, maxSpeed * 1.0f);

    void DoKite()
    {
        if (!canSeePlayer) { DoFlee(); return; }
        RotateToward(player.position);

        if (distToPlayer < kiteMinDist)
            ApplySteering(((Vector2)transform.position - (Vector2)player.position).normalized, maxSpeed);
        else if (distToPlayer > kiteMaxDist)
            ApplySteering(((Vector2)player.position - (Vector2)transform.position).normalized, maxSpeed * 0.6f);
        else
            ApplySteering(Vector2.Perpendicular(
                ((Vector2)player.position - (Vector2)transform.position).normalized), maxSpeed * 0.7f);

        // ── WeaponSystem / Legacy shoot ──────────────────────
        if (canSeePlayer)
        {
            if (weapon != null)
                weapon.TryShoot(player.position, lastKnownVel);
            else if (fireTimer <= 0f)
            { ShootAt(PredictPlayerPos(), aimInaccuracy); fireTimer = fireCooldown; }
        }
    }

    void DoAggressive()
    {
        Vector2 target = canSeePlayer ? (Vector2)player.position : lastKnownPos;
        Seek(target, maxSpeed * aggressiveSpeedMultiplier);
        RotateToward(target);

        if (canSeePlayer && distToPlayer <= aggressRadius * 1.5f)
        {
            if (weapon != null)
                weapon.TryShoot(player.position, lastKnownVel);
            else if (fireTimer <= 0f)
            { ShootAt(PredictPlayerPos(), aimInaccuracy * 0.6f); fireTimer = fireCooldown * 0.7f; }
        }
    }

    void DoReturnToWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) { DoWanderNearSpawn(); return; }
        waypointTimer -= Time.deltaTime;
        if (waypointTimer <= 0f) { PickRandomWaypoint(); return; }
        Seek(waypoints[waypointIndex].position, patrolMode: true);
        if (Vector2.Distance(transform.position, waypoints[waypointIndex].position) < waypointReachDistance)
        { state = BehaviorState.Patrol; waypointTimer = waypointTimeout; }
    }

    // ════════════════════════════════════════════════════════
    //  STEERING
    // ════════════════════════════════════════════════════════

    void Seek(Vector2 target, float speed = -1f, bool patrolMode = false)
    {
        if (speed < 0f) speed = maxSpeed;

        // หาจุดที่ควรมุ่งไปจริงๆ ก่อน (อาจเป็นเป้าหมายตรงๆ ถ้าไม่มีอะไรบัง หรือจุดอ้อมมุมกำแพงถ้ามีสิ่งกีดขวางบัง)
        // แก้ปัญหาเดิมที่ระบบ AvoidObstacles อย่างเดียวเป็นแค่การสะท้อนเฉพาะหน้า ไม่ได้คำนวณเส้นทางอ้อมจริงๆ
        // สำหรับเป้าหมายที่อยู่ไกล ทำให้บางครั้งเดินเข้าใกล้กำแพงแล้วเดินตรงต่อไปเรื่อยๆ (ดูเหมือนพยายามทะลุกำแพง)
        Vector2 waypoint = GetNextWaypointTowards(target);

        ApplySteering((waypoint - (Vector2)transform.position).normalized, speed);

        // ระหว่างกำลังกู้ตัวเองออกจากจุดติดขัด (isStuckRecovering) ให้หันหน้าตามทิศที่กำลังเดินจริง
        // (stuckRecovDir) แทนที่จะหันไปทาง target เดิมที่โดนกำแพงบังอยู่ — ของเดิมหันไปทาง target ตลอด
        // ไม่ว่าจะกำลังกู้ตัวเองอยู่หรือไม่ ทำให้ตัวเลื่อนไปทางหนึ่งแต่หน้าหมุนไล่อีกทางหนึ่งตลอดเวลา
        // ดูเหมือน "หมุนอยู่กับที่ไม่ไปไหน" ทั้งที่จริงๆ กำลังพยายามกู้ตัวเองออกจากมุมอับอยู่
        if (isStuckRecovering)
            RotateToward((Vector2)transform.position + stuckRecovDir);
        else
            RotateToward(waypoint);
    }

    /// <summary>
    /// หาจุดถัดไปที่ควรมุ่งไปเพื่อไปถึง target ในที่สุด — ถ้าเส้นตรงจาก origin ไป target ไม่มีอะไรบัง
    /// จะคืนค่า target ตรงๆ เลย แต่ถ้ามีกำแพงบังอยู่ จะหามุมของกำแพงตัวนั้น (2 มุมที่ใกล้เส้นทางที่สุด)
    /// แล้วเลือกมุมที่ทำให้ระยะทางรวม (ไปมุม + จากมุมไป target) สั้นที่สุด และเส้นทางทั้งสองช่วงต้องโล่งจริง
    /// ให้ Seek() มุ่งไปจุดนั้นก่อน — ได้เส้นทางที่ "หักเลี้ยว" อ้อมกำแพงจริงๆ แทนเดินตรงเข้าใกล้กำแพงเรื่อยๆ
    /// คำนวณซ้ำทุก Path Recalc Interval วินาที (ไม่คำนวณทุกเฟรม กันเปลือง Performance) และคำนวณใหม่ทันที
    /// ถ้า target เปลี่ยนตำแหน่งไปมากพอ (เช่นเปลี่ยนเป้าหมายไล่ล่า)
    /// </summary>
    Vector2 GetNextWaypointTowards(Vector2 target)
    {
        bool targetChanged = Vector2.Distance(target, cachedPathTarget) > 1f;
        pathRecalcTimer -= Time.deltaTime;

        if (!targetChanged && pathRecalcTimer > 0f && hasCachedWaypoint)
            return cachedWaypoint;

        pathRecalcTimer = pathRecalcInterval;
        cachedPathTarget = target;

        Vector2 origin = transform.position;
        Vector2 toTarget = target - origin;
        float dist = toTarget.magnitude;

        if (dist < 0.01f) { cachedWaypoint = target; hasCachedWaypoint = true; return target; }

        RaycastHit2D directHit = Physics2D.Raycast(origin, toTarget.normalized, dist, wallLayer);
        if (directHit.collider == null)
        {
            // ไม่มีอะไรบังเส้นตรง เดินตรงไปเป้าหมายได้เลย ไม่ต้องอ้อม
            cachedWaypoint = target;
            hasCachedWaypoint = true;
            return target;
        }

        // มีกำแพงบัง หามุมทั้ง 4 ของกำแพงตัวนั้น เลือกมุมที่อ้อมได้จริง (มองเห็นทั้งจากตัวเราและจากมุมไปเป้าหมาย)
        // แล้วระยะทางรวมสั้นที่สุด — มี Hysteresis กันเปลี่ยนใจสลับมุมไปมา (ดูคำอธิบายด้านล่าง)
        Bounds b = directHit.collider.bounds;
        Vector2[] corners =
        {
            new Vector2(b.min.x, b.min.y), new Vector2(b.min.x, b.max.y),
            new Vector2(b.max.x, b.min.y), new Vector2(b.max.x, b.max.y)
        };

        // จุดที่เคยเลือกไว้รอบก่อน ใช้เทียบว่ามุมไหน "เหมือนเดิม" กันเปลี่ยนใจง่ายเกินไป
        Vector2 previousWaypoint = hasCachedWaypoint ? cachedWaypoint : target;

        Vector2 best = target;
        float bestTotalDist = float.MaxValue;
        bool foundValidCorner = false;

        foreach (var corner in corners)
        {
            // ทิศจากจุดศูนย์กลางกำแพงออกไปยังมุมนี้ (ทิศ "ออกด้านนอก") ใช้ขยับจุดออกจากมุมจริงอีกนิด
            Vector2 outward = (corner - (Vector2)b.center).normalized;
            Vector2 waypointCandidate = corner + outward * pathCornerMargin;

            float distToCorner = Vector2.Distance(origin, waypointCandidate);
            float distCornerToTarget = Vector2.Distance(waypointCandidate, target);

            bool clearToCorner = distToCorner < 0.01f || !Physics2D.Raycast(origin, (waypointCandidate - origin).normalized, distToCorner, wallLayer);
            bool clearFromCorner = distCornerToTarget < 0.01f || !Physics2D.Raycast(waypointCandidate, (target - waypointCandidate).normalized, distCornerToTarget, wallLayer);
            if (!clearToCorner || !clearFromCorner) continue;

            float totalDist = distToCorner + distCornerToTarget;

            // Hysteresis: ถ้ามุมนี้คือมุมเดิมที่เคยเลือกไว้ (หรือใกล้เคียงมาก) ให้ได้เปรียบนิดหน่อยในการเปรียบเทียบ
            // กันอาการ "เปลี่ยนใจ" สลับไปมาระหว่าง 2 มุมที่คะแนนใกล้เคียงกันมากทุกครั้งที่คำนวณใหม่ (ทุก pathRecalcInterval)
            // ซึ่งทำให้เดินแกว่งซ้าย-ขวาไม่คืบหน้าไปทางไหนเลย (บั๊กที่เจอตอนทดสอบ) — ต้องดีกว่ามุมเดิมชัดเจนจริงๆ
            // (มากกว่า pathStickBonus) ถึงจะยอมเปลี่ยนไปใช้มุมใหม่แทน
            bool isSameAsPrevious = Vector2.Distance(waypointCandidate, previousWaypoint) < pathCornerMargin * 0.5f;
            float effectiveDist = isSameAsPrevious ? totalDist * (1f - pathStickBonus) : totalDist;

            if (effectiveDist < bestTotalDist)
            {
                bestTotalDist = effectiveDist;
                best = waypointCandidate;
                foundValidCorner = true;
            }
        }

        // หามุมที่อ้อมได้จริงไม่เจอเลย (เช่นโดนล้อมหลายด้าน) -> fallback ให้ AvoidObstacles() ตอน ApplySteering
        // จัดการเฉพาะหน้าต่อไปแทน ยังดีกว่าเดินตรงเข้าใส่กำแพงเฉยๆ
        cachedWaypoint = foundValidCorner ? best : target;
        hasCachedWaypoint = true;
        return cachedWaypoint;
    }

    void ApplySteering(Vector2 desiredDir, float speed)
    {
        desiredDir = AvoidObstacles(desiredDir);
        CheckStuck((Vector2)transform.position + desiredDir * cellSize);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredDir * speed, Time.deltaTime * steeringForce);
    }

    Vector2 AvoidObstacles(Vector2 desired)
    {
        float rayLen = cellSize * 1.5f;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, desired, rayLen, wallLayer);
        if (hit.collider == null) return desired;
        Vector2 reflected = Vector2.Reflect(desired, hit.normal);
        Vector2 left  = new Vector2(-desired.y,  desired.x);
        Vector2 right = new Vector2( desired.y, -desired.x);
        bool lClear = !Physics2D.Raycast(transform.position, left,  rayLen, wallLayer);
        bool rClear = !Physics2D.Raycast(transform.position, right, rayLen, wallLayer);
        if (lClear && rClear) return (reflected * 0.5f + (Random.value > 0.5f ? left : right) * 0.5f).normalized;
        else if (lClear) return left;
        else if (rClear) return right;
        return reflected.normalized;
    }

    void CheckStuck(Vector2 nextPos)
    {
        stuckCheckTimer -= Time.deltaTime;
        stuckRecovTimer -= Time.deltaTime;
        isStuckRecovering = stuckRecovTimer > 0f;

        if (isStuckRecovering) { rb.linearVelocity = stuckRecovDir * maxSpeed * 0.9f; return; }
        if (stuckCheckTimer <= 0f)
        {
            stuckCheckTimer = 0.5f;
            if (Vector2.Distance(transform.position, stuckCheckPos) < 5f)
            {
                stuckRecovDir = FindOpenDir();
                stuckRecovTimer = 0.7f;
                isStuckRecovering = true;
            }
            stuckCheckPos = transform.position;
        }
    }

    Vector2 FindOpenDir()
    {
        Vector2 best = transform.up; float bestDist = 0f;
        for (int i = 0; i < 16; i++)
        {
            Vector2 dir = Quaternion.Euler(0,0,i*22.5f) * Vector2.up;
            RaycastHit2D h = Physics2D.Raycast(transform.position, dir, cellSize*5f, wallLayer);
            float d = h.collider != null ? h.distance : cellSize*5f;
            if (d > bestDist) { bestDist = d; best = dir; }
        }
        return best;
    }

    // ════════════════════════════════════════════════════════
    //  COVER
    // ════════════════════════════════════════════════════════

    void TriggerSeekCover()
    {
        currentCover = FindBestCover();
        if (currentCover != null)
        { state = BehaviorState.SeekCover; peekTimer = peekInterval; }
    }

    void DoSeekCover()
    {
        if (currentCover == null) { state = BehaviorState.Flee; return; }
        Vector2 dest = GetCoverPos(currentCover);
        Seek(dest, maxSpeed * 1.1f);
        if (Vector2.Distance(transform.position, dest) < coverReachDistance)
        { rb.linearVelocity = Vector2.zero; state = BehaviorState.InCover; peekTimer = peekInterval; }
    }

    void DoInCover()
    {
        if (!IsLowHealth) { state = BehaviorState.Flee; return; }
        if (currentCover == null) { TriggerSeekCover(); return; }
        peekTimer -= Time.deltaTime;
        if (!isPeeking)
        {
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.deltaTime*8f);
            if (peekTimer <= 0f) { isPeeking = true; peekTimer = peekDuration; }
        }
        else
        {
            if (canSeePlayer)
            {
                RotateToward(player.position);
                Seek(GetPeekPos(currentCover), maxSpeed);
                if (weapon != null)
                    weapon.TryShoot(player.position, lastKnownVel);
                else if (fireTimer <= 0f)
                { ShootAt(PredictPlayerPos(), aimInaccuracy); fireTimer = fireCooldown; }
            }
            peekTimer -= Time.deltaTime;
            if (peekTimer <= 0f)
            { isPeeking = false; peekTimer = peekInterval; StartCoroutine(SlideBack(GetCoverPos(currentCover))); }
        }
    }

    IEnumerator SlideBack(Vector2 t)
    {
        float e = 0f; Vector2 s = transform.position;
        while (e < 0.3f) { e += Time.deltaTime; rb.MovePosition(Vector2.Lerp(s,t,e/0.3f)); yield return null; }
    }

    Transform FindBestCover()
    {
        GameObject[] covers = GameObject.FindGameObjectsWithTag("Cover");
        Transform best = null; float bestScore = float.MinValue;
        foreach (var c in covers)
        {
            float dMe = Vector2.Distance(transform.position, c.transform.position);
            if (dMe > coverSearchRadius) continue;
            float dPl = Vector2.Distance(player.position, c.transform.position);
            float score = Vector2.Dot(
                ((Vector2)player.position-(Vector2)transform.position).normalized,
                ((Vector2)c.transform.position-(Vector2)transform.position).normalized)*3f
                -dMe*0.5f+dPl*0.3f;
            if (score > bestScore) { bestScore = score; best = c.transform; }
        }
        return best;
    }

    Vector2 GetCoverPos(Transform cover)
    {
        Collider2D col = cover.GetComponent<Collider2D>();
        float size = col ? Mathf.Max(col.bounds.extents.x,col.bounds.extents.y)+15f : 30f;
        return (Vector2)cover.position+((Vector2)cover.position-(Vector2)player.position).normalized*size;
    }

    Vector2 GetPeekPos(Transform cover)
    {
        Collider2D col = cover.GetComponent<Collider2D>();
        float size = col ? Mathf.Max(col.bounds.extents.x,col.bounds.extents.y)+12f : 25f;
        return (Vector2)cover.position+Vector2.Perpendicular(
            ((Vector2)player.position-(Vector2)cover.position).normalized)*size;
    }

    // ════════════════════════════════════════════════════════
    //  DODGE
    // ════════════════════════════════════════════════════════

    void CheckIncomingBullets()
    {
        if (dodgeTimer > 0f) return;
        Collider2D[] bullets = Physics2D.OverlapCircleAll(transform.position, 100f, bulletLayer);
        foreach (var b in bullets)
        {
            if (!b.CompareTag("Bullet")) continue;
            Rigidbody2D bRb = b.GetComponent<Rigidbody2D>(); if (bRb == null) continue;
            Vector2 bv = bRb.linearVelocity.normalized;
            Vector2 te = ((Vector2)transform.position-(Vector2)b.transform.position).normalized;
            if (Vector2.Dot(bv,te) > 0.7f) { TriggerDodge(bv); return; }
        }
    }

    void TriggerDodge(Vector2 bd)
    {
        Vector2 pL = new Vector2(-bd.y,bd.x), pR = new Vector2(bd.y,-bd.x);
        bool lOk = !Physics2D.Raycast(transform.position,pL,60f,wallLayer);
        bool rOk = !Physics2D.Raycast(transform.position,pR,60f,wallLayer);
        if (!lOk && !rOk) return;
        dodgeDir = (lOk&&rOk)?(Random.value>0.5f?pL:pR):(lOk?pL:pR);
        stateBeforeDodge = state; state = BehaviorState.Dodge; isDodging = true;
        StartCoroutine(DodgeCoroutine());
    }

    IEnumerator DodgeCoroutine()
    {
        float e = 0f;
        while (e < 0.2f) { rb.linearVelocity = dodgeDir*maxSpeed*1.8f; e += Time.deltaTime; yield return null; }
        rb.linearVelocity = Vector2.zero;
        isDodging = false; dodgeTimer = 0.8f; state = stateBeforeDodge;
    }

    // ════════════════════════════════════════════════════════
    //  LEGACY SHOOT (ใช้เมื่อไม่มี WeaponSystem)
    // ════════════════════════════════════════════════════════

    Vector2 PredictPlayerPos()
    {
        float dist = Vector2.Distance(
            firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position,
            player.position);
        return (Vector2)player.position + lastKnownVel*(dist/Mathf.Max(bulletSpeed,1f))*predictionTime;
    }

    void ShootAt(Vector2 aim, float inaccuracy)
    {
        if (bulletPrefab == null || firePoint == null || !canSeePlayer) return;
        Vector2 dir = (aim-(Vector2)firePoint.position).normalized;
        dir = Quaternion.Euler(0,0,Random.Range(-inaccuracy,inaccuracy)) * dir;
        GameObject  b   = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        Rigidbody2D bRb = b.GetComponent<Rigidbody2D>();
        if (bRb) bRb.linearVelocity = dir*bulletSpeed;
        b.transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg-90f);
    }

    // ════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════

    bool CanSeePlayerFrom(Vector2 pos)
    {
        if (player == null) return false;
        Vector2 dir = (Vector2)player.position - pos;
        return !Physics2D.Raycast(pos, dir.normalized, dir.magnitude, wallLayer);
    }

    void RotateToward(Vector2 t)
    {
        Vector2 dir = (t-(Vector2)transform.position).normalized;
        transform.rotation = Quaternion.Lerp(transform.rotation,
            Quaternion.Euler(0,0,Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg-90f),
            rotationSpeed*Time.deltaTime);
    }

    void PickRandomWaypoint()
    {
        if (waypoints == null || waypoints.Length <= 1) return;
        int n; do { n = Random.Range(0, waypoints.Length); } while (n == waypointIndex);
        waypointIndex = n; waypointTimer = waypointTimeout;
    }

    public void ReceiveAlert(Vector2 pos, float duration = 3f)
    {
        if (state == BehaviorState.Patrol || state == BehaviorState.ReturnToWaypoint)
        {
            lastKnownPos = pos; hasLastKnown = true; alertTimer = duration; state = BehaviorState.Alert;
            playerTrail.Clear();
            hasSearchOvershootTarget = false;
            lastBlockingWall = null;
            isLookingAtPoint = false;
        }
    }

    public void TakeDamage(int dmg)
    {
        currentHealth -= dmg;
        if (currentHealth <= 0) { Destroy(gameObject); return; }
        if (IsLowHealth && state != BehaviorState.SeekCover && state != BehaviorState.InCover)
            TriggerSeekCover();
    }

    // ════════════════════════════════════════════════════════
    //  GIZMOS
    // ════════════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        Vector2 f = transform.up; float h = fovAngle*0.5f;
        Gizmos.color = canSeePlayer ? Color.red : new Color(0,1,0,0.2f);
        Vector2 prev = (Vector2)transform.position+(Vector2)(Quaternion.Euler(0,0,-h)*f)*fovRange;
        for (int i = 1; i <= 16; i++)
        {
            Vector2 cur = (Vector2)transform.position+(Vector2)(Quaternion.Euler(0,0,-h+(fovAngle/16f)*i)*f)*fovRange;
            Gizmos.DrawLine(prev, cur); prev = cur;
        }
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position,(Vector2)transform.position+(Vector2)(Quaternion.Euler(0,0,-h)*f)*fovRange);
        Gizmos.DrawLine(transform.position,(Vector2)transform.position+(Vector2)(Quaternion.Euler(0,0,h)*f)*fovRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(bestCell, cellSize * 0.5f);
        Gizmos.DrawLine(transform.position, bestCell);

        if (showInfluenceGrid && Application.isPlaying)
        {
            float minS = float.MaxValue, maxS = float.MinValue;
            foreach (var g in debugGrid) { minS = Mathf.Min(minS,g.score); maxS = Mathf.Max(maxS,g.score); }
            foreach (var g in debugGrid)
            {
                float t = maxS > minS ? (g.score - minS) / (maxS - minS) : 0.5f;
                Gizmos.color = new Color(1f - t, t, 0f, 0.4f);
                Gizmos.DrawCube(g.pos, Vector3.one * cellSize * 0.85f);
            }
        }

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 20f, 6f);
    }
}
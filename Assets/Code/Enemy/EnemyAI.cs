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
    public float fovAngle        = 120f;
    public float fovRange        = 500f;
    public float peripheralRange = 150f;
    public int   fovRayCount     = 14;

    [HideInInspector] public bool    canSeePlayer = false;
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
    [Tooltip("ส่ง Alert ไปให้ Squad เมื่อเห็น Player")]
    public bool broadcastAlertOnSight = true;
    private bool hasAlerted = false; // ส่งแล้วหรือยัง (reset ทุกครั้งที่เสีย sight)

    // ════════════════════════════════════════════════════════
    //  SEARCH (ค้นหาต่อหลังเสียสายตาผู้เล่น)
    // ════════════════════════════════════════════════════════

    [Header("── Search (ค้นหาหลังเสียสายตาผู้เล่น) ──")]
    [Tooltip("ระยะที่จะเดินอ้อมต่อไปตามทิศทางที่ผู้เล่นวิ่งหนี หลังถึงจุดสุดท้ายที่เห็นแล้ว (ใช้เดินอ้อมไปดูหลังกำแพง)")]
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
    [Tooltip("ความเร็วตอนรีบไปยังจุดที่เห็นผู้เล่นล่าสุด (Alert) คูณกับ Max Speed — ยิ่งเยอะยิ่งรีบไปไว")]
    public float alertRushSpeedMultiplier = 1.4f;
    [Tooltip("ความเร็วตอนเดินสำรวจแบบระมัดระวังใน Search (คูณกับ Max Speed) — ยิ่งน้อยยิ่งเดินช้า/ระวังตัว เหมือนหน่วยรบเคลียร์พื้นที่")]
    public float searchWalkSpeedMultiplier = 0.5f;
    [Tooltip("เวลาที่จะหยุดหันมองกวาดซ้าย-ขวาเช็คแต่ละจุด ก่อนไปจุดถัดไป (วินาที) เหมือนเช็คมุมอับทีละจุด")]
    public float searchLookDuration = 0.9f;
    [Tooltip("มุมที่จะหันกวาดซ้าย-ขวาตอนเช็คแต่ละจุด (องศา)")]
    public float searchLookSweepAngle = 70f;

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

    private Vector2 stuckCheckPos   = Vector2.zero;
    private float   stuckCheckTimer = 0f;
    private float   stuckRecovTimer = 0f;
    private Vector2 stuckRecovDir   = Vector2.zero;

    private struct GridDebug { public Vector2 pos; public float score; }
    private List<GridDebug> debugGrid = new List<GridDebug>();

    // ════════════════════════════════════════════════════════
    //  INIT
    // ════════════════════════════════════════════════════════

    void Start()
    {
        rb   = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        weapon = GetComponent<EnemyWeaponSystem>(); // auto-find WeaponSystem

        currentHealth     = maxHealth;
        rb.gravityScale   = 0f;
        rb.freezeRotation = true;
        stuckCheckPos     = transform.position;

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

        bool wasSeeingPlayer = canSeePlayer; // ค่าจากเฟรมก่อนหน้า ก่อนที่ ScanFOV() จะเขียนทับ

        fireTimer    -= Time.deltaTime;
        dodgeTimer   -= Time.deltaTime;
        remapTimer   -= Time.deltaTime;
        alertTimer   -= Time.deltaTime;
        searchTimer  -= Time.deltaTime;

        // ── 1. SENSE ──
        ScanFOV();
        distToPlayer = Vector2.Distance(transform.position, player.position);

        // ── เพิ่งเสีย Sight พอดี: หาว่ากำแพงตัวไหนที่บังสายตาจริงๆ (ใช้ระบุฝั่งที่ถูกต้องตอนค้นหาทีหลัง) ──
        if (wasSeeingPlayer && !canSeePlayer && hasLastKnown)
        {
            Vector2 toLastKnown = lastKnownPos - (Vector2)transform.position;
            if (toLastKnown.sqrMagnitude > 0.01f)
            {
                RaycastHit2D wallHit = Physics2D.Raycast(transform.position, toLastKnown.normalized, toLastKnown.magnitude, wallLayer);
                lastBlockingWall = wallHit.collider; // อาจเป็น null ถ้าหาไม่เจอ (DoSearch จะ fallback เอง)
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
        canSeePlayer = false;
        Vector2 facing = transform.up;
        float   half   = fovAngle * 0.5f;
        float   step   = fovAngle / (fovRayCount - 1);

        for (int i = 0; i < fovRayCount; i++)
        {
            Vector2 dir = Quaternion.Euler(0,0,-half+step*i) * facing;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, fovRange);
            if (hit.collider != null && hit.collider.CompareTag("Player"))
            { canSeePlayer = true; break; }
        }

        if (!canSeePlayer && distToPlayer <= peripheralRange)
        {
            Vector2 toP = (Vector2)player.position - (Vector2)transform.position;
            if (!Physics2D.Raycast(transform.position, toP.normalized, toP.magnitude, wallLayer))
                canSeePlayer = true;
        }

        if (canSeePlayer)
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
        if (waypoints == null || waypoints.Length == 0) return;
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

            // หันกวาดซ้าย-ขวาเหมือนกำลังเช็คมุมอับ (Pie/Jiggle Peek สไตล์หน่วยรบ)
            float t = 1f - Mathf.Clamp01(lookTimer / searchLookDuration);
            float angleOffset = Mathf.Sin(t * Mathf.PI * 2f) * searchLookSweepAngle;
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
                    searchOvershootTarget = searchOvershootTarget + dir * (searchOvershootDistance * 0.6f);
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
    /// ทิศทางที่จะใช้เดินสำรวจ — ใช้ 'ความเร็วล่าสุดตอนเพิ่งเสียสายตา' (lastKnownVel) เป็นอันดับแรก
    /// เพราะเป็นข้อมูลสดที่สุด จับทิศตอน 'หักมุม' เข้ากำแพงได้แม่นกว่ารอยเท้าที่เฉลี่ยกับช่วงเดินก่อนหน้าทั้งหมด
    /// ถ้าไม่มีข้อมูลความเร็ว (เช่น player ไม่มี Rigidbody2D) ค่อย fallback ไปใช้ค่าเฉลี่ยถ่วงน้ำหนักจากรอยเท้าแทน
    /// </summary>
    Vector2 GetSearchDirection()
    {
        if (lastKnownVel.sqrMagnitude > 0.05f) return lastKnownVel.normalized;

        Vector2 fleeDir = EstimateFleeDirection();
        if (fleeDir != Vector2.zero) return fleeDir;

        return (Vector2)transform.up;
    }

    /// <summary>
    /// คำนวณจุดแรกที่ควรเดินไปเช็ค โดยยึด "กำแพงตัวที่บังสายตาจริง" (lastBlockingWall) เป็นหลัก
    /// ใช้ระยะ wallHugDistance (ค่าคงที่ ไม่ใช่ขนาดกำแพง) เกาะชิดขอบกำแพงตรงจุดที่ใกล้ผู้เล่นล่าสุดที่สุด
    /// แล้วอ้อมต่อไปในทิศที่ผู้เล่นวิ่งหนี (fleeDir) อีกนิด — แก้จากเวอร์ชันก่อนที่ใช้ขนาดกำแพงเองมาคำนวณ
    /// ระยะอ้อม ซึ่งถ้ากำแพงยาว/สูงมากจะได้จุดที่ไกลเกินจริงมาก ไม่ได้เดินชิดกำแพงเลย
    /// ถ้าไม่รู้ว่ากำแพงตัวไหนบัง (lastBlockingWall เป็น null) จะ fallback ไปเดินอ้อมแบบเส้นตรงแทน
    /// </summary>
    Vector2 ComputeFlankTarget(Vector2 fleeDir)
    {
        if (lastBlockingWall == null)
            return lastKnownPos + fleeDir * searchOvershootDistance;

        Bounds wallBounds = lastBlockingWall.bounds;

        // จุดบนกำแพงที่ใกล้ตำแหน่งผู้เล่นล่าสุดที่สุด (มุม/ขอบที่ผู้เล่นเพิ่งเดินผ่านไปจริงๆ)
        Vector2 closestOnWall = wallBounds.ClosestPoint(lastKnownPos);

        Vector2 toWall = (Vector2)wallBounds.center - lastKnownPos;
        if (toWall.sqrMagnitude < 0.01f) toWall = (Vector2)wallBounds.center - (Vector2)transform.position;
        toWall.Normalize();
        Vector2 perpendicular = new Vector2(-toWall.y, toWall.x);

        // เกาะชิดขอบกำแพงด้วยระยะคงที่ (wallHugDistance) แล้วอ้อมต่อไปในทิศที่ผู้เล่นวิ่งหนีอีกนิด
        Vector2 sideA = closestOnWall + perpendicular * wallHugDistance + fleeDir * (searchOvershootDistance * 0.5f);
        Vector2 sideB = closestOnWall - perpendicular * wallHugDistance + fleeDir * (searchOvershootDistance * 0.5f);

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
        Seek(target, maxSpeed * 1.3f);
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
        if (waypoints == null || waypoints.Length == 0) return;
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
        ApplySteering((target - (Vector2)transform.position).normalized, speed);
        RotateToward(target);
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
        if (stuckRecovTimer > 0f) { rb.linearVelocity = stuckRecovDir * maxSpeed * 0.9f; return; }
        if (stuckCheckTimer <= 0f)
        {
            stuckCheckTimer = 0.5f;
            if (Vector2.Distance(transform.position, stuckCheckPos) < 5f)
            { stuckRecovDir = FindOpenDir(); stuckRecovTimer = 0.7f; }
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
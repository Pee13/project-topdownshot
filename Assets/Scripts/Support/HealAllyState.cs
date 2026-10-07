using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Player;
using TopDownTacticalAI.Tactical;
using TopDownTacticalAI.Utilities;
using TopDownTacticalAI.Vision;

namespace TopDownTacticalAI.Support
{
    /// <summary>
    /// Healer ฮีลเพื่อนที่บาดเจ็บ (§4, §14, §17, §23, §26)
    ///
    /// ลำดับการทำงานในหนึ่งเฟรม:
    ///   1) เลือกเป้าฮีลด้วย <see cref="HealTargetScorer"/> (§15) — มี hysteresis กันสลับเป้ารัวๆ (§28)
    ///   2) ถ้าไกลเกินระยะฮีล → เดินเข้าไปหา (beam ปิด)
    ///   3) ถ้าอยู่ในระยะแต่ "ไม่ปลอดภัย" (ผู้เล่นใกล้เกิน / มีกระสุนพุ่งมา) → หยุดฮีล ถอยออกห่าง (§17/§23)
    ///   4) ปลอดภัย → ฮีล + เปิด beam + หันหน้าไปทาง "เป้าฮีล" (§26 — ไม่ใช่หันหาผู้เล่น)
    /// </summary>
    public class HealAllyState : IAIState
    {
        public EnemyState StateType => EnemyState.HealAlly;

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly float _healPerSecond;
        private readonly float _healRange;
        private readonly float _playerDangerDistance;
        private readonly float _moveSpeed;
        private readonly LayerMask _obstacleMask;
        private readonly float _turnSpeed;

        private LineRenderer _healBeam;
        private Transform _currentTarget;
        private Health _currentTargetHealth;
        private readonly LayerMask _bulletMask;
        private readonly TopDownTacticalAI.Pathfinding.AgentPathMover _mover; // NavMesh เดินไปหาเพื่อน (fallback: wall-following เดิม)

        // ── Wall-following detour (เดินอ้อมกำแพงไปฮีล) ──
        // ผูกพันเดินตามขอบกำแพงทิศเดียว 1.5 วิ — กันสลับซ้ายขวาจนก้ำกึ่ง (§28)
        private Vector2 _detourDirection;
        private float _detourTimer;
        private const float DetourCommitTime = 1.5f;

        // เช็คกระสุนทุก 0.1 วิ (แทนทุกเฟรม — ของเดิมยิง OverlapCircle เปล่า ๆ ทุกเฟรม)
        // ยังคงรัศมี 2.5f เดิมของ healer (ต่างจาก DodgeDetectRadius ของ TacticalDecision ที่ Support ถูกครึ่ง)
        private const float BulletCheckInterval = 0.1f;
        private float _bulletCheckTimer;
        private bool _lastBulletIncoming;

        // เลือกจุดยืนใหม่ตามน้ำหนัก Healer (§16 ScoreHealer) ทุก 0.5 วิ ระหว่างที่ถูกขวางไม่ให้ฮีล
        private const float RepositionInterval = 1.0f;   // เลือกจุดยืนใหม่ห่างขึ้น (เดิม 0.5 — ถี่จนดูเหมือนเดินวน)
        private const float RepositionSearchRadius = 6f;
        private float _repositionCooldown;
        private Vector2 _repositionTarget;
        private bool _hasRepositionTarget;

        // ── การเลือก/เปลี่ยนเป้าฮีล (ส่วนที่ 1) ──
        private const float ReevalInterval = 0.3f;        // ประเมินเป้าใหม่ทุก 0.3 วิ (ไม่ล็อกยาวจนกว่าจะถึง)
        private const float SwitchHysteresisRatio = 1.18f; // เป้าใหม่ต้องดีกว่าเป้าปัจจุบัน ≥18% ถึงยอมสลับ (กันเดินสลับไปมา)
        private const float HealStopDistance = 1.5f;       // ระยะหยุดเดินของ NavMeshAgent — ต้องสั้นกว่า HealRange เสมอ
                                                             // (ป้องกันชนติดกันจนเข้าไม่ถึงระยะฮีล — ฮีลจากระยะไกลกว่านี้ได้อยู่แล้ว)
        private const float PatientHealthThreshold = 0.30f; // Cooperate Waiting: ชะลอรอฮีลเฉพาะเพื่อน HP ≤ 30%
                                                             // (ถ้าชะลอทุกเป้าที่ HP <80% จะไปหน่วงการต่อสู้ของเพื่อนเปล่า ๆ)
        private const float HealRangeHysteresis = 4f;        // กัน ping-pong ขอบระยะฮีล: เข้าฮีลที่ ≤ HealRange
                                                             // แต่ "หยุด" เฉพาะเมื่อหลุดเกิน HealRange+4
                                                             // (log: เป้าที่ยังไม่ถึง30% วิ่งเต็ม speed → หลุดที่27.3 บ่อย → ฮีลขาดช่วง — กว้างขึ้น = ฮีลต่อเนื่องยาวขึ้น)
        private const float SwitchCooldown = 1.0f;           // ห้ามสลับเป้าบ่อยกว่า 1 วิ — (log ล่าสุด สลับเป้า Tank↔Flanker 4 ครั้งในไม่กี่วิ
                                                             // เพราะคะแนน HP% ต่ำสุดแกว่งไปมาตอนฮีลเพิ่มเลือด)
        private float _switchCooldown;
        private float _reevalTimer;

        // ── อ้างอิงเป้าหมายปัจจุบัน (cache ตอนสลับเป้า — ใช้เรียก velocity/LOS โดยไม่ GetComponent ทุกเฟรม) ──
        private TopDownTacticalAI.Core.EnemyBrain _currentTargetBrain;
        private Collider2D _targetCollider;
        private Collider2D _selfCollider;

        // ── 2 ช่วงการทำงานของ Healer (ส่วนที่ 2 — แยกให้ชัด) ──
        // MoveToTarget = ช่วงเดินทางไปหาเป้า / ApplyHeal = ช่วงปล่อยฮีล (ในระยะ + LOS ผ่าน)
        // (อยู่ใน IAIState เดียวกัน — การแยกเป็น state จริงต้องต่อ TacticalDecision+Scorer+Priority เพิ่ม
        //  ซึ่งไม่คุ้มกับประโยชน์ เพราะสองช่วงนี้ไม่เคยขัดจังหวะกันเอง)
        private enum HealPhase { AcquireTarget, MoveToTarget, ApplyHeal, Interrupted }
        private HealPhase _phase = HealPhase.AcquireTarget;

        // ระยะเผื่อรอบตัวตอนเช็คกำแพง — ยิ่งมาก ยิ่งเว้นห่างกำแพงกว่าที่ collider จริงแตะ
        // (ชดเชยสไปรต์ปลายแหลมที่ยื่นเกินกรอบ collider)
        private const float WallPadding = 0.15f;

        public HealAllyState(
            Transform self,
            Blackboard blackboard,
            float healPerSecond,
            float healRange,
            float playerDangerDistance,
            float moveSpeed,
            LayerMask obstacleMask,
            LayerMask bulletMask,
            LineRenderer healBeam = null,
            float turnSpeed = 360f,
            TopDownTacticalAI.Pathfinding.AgentPathMover mover = null)
        {
            _self = self;
            _blackboard = blackboard;
            _healPerSecond = healPerSecond;
            _healRange = healRange;
            _playerDangerDistance = playerDangerDistance;
            _moveSpeed = moveSpeed;
            _obstacleMask = obstacleMask;
            _bulletMask = bulletMask;
            _healBeam = healBeam;
            _turnSpeed = turnSpeed;
            _mover = mover;
        }

        public void Enter()
        {
            // LineRenderer ของ Sup ใช้ตอนวาดลำแสงฮีล — หาเองถ้ายังไม่ได้ลากใน Inspector
            if (_healBeam == null) _healBeam = _self.GetComponent<LineRenderer>();

            _phase = HealPhase.AcquireTarget;
            _reevalTimer = 0f; // เข้า state → เลือกเป้าทันทีในเฟรมแรก (ไม่ต้องรอ ReevalInterval)
        }

        public void Tick(float deltaTime)
        {
            if (_switchCooldown > 0f) _switchCooldown -= deltaTime;

            // ── 1) ACQUIRE: เลือก/ประเมินเป้าฮีลใหม่ทุก ReevalInterval (0.3 วิ) ──
            // ของเดิม: หลัง cooldown หมดจะเข้าบล็อกเลือกเป้า "ทุกเฟรม" และล็อกเป้าจนกว่าจะเดินถึง
            // ของใหม่: เว้นจังหวะให้ชัดเจน + เทียบคะแนนแบบ Hysteresis 18% → เพื่อนที่เดินเข้ามาใกล้กว่า/
            // คะแนนดีกว่าอย่างมีนัยจะถูกเลือกแทนกลางทางได้ (ไม่ต้องรอเดินถึงเป้าเก่าก่อน)
            _reevalTimer -= deltaTime;
            bool targetInvalid = _currentTarget == null || _currentTargetHealth == null || _currentTargetHealth.IsDead;

            if (targetInvalid || _reevalTimer <= 0f)
            {
                _reevalTimer = ReevalInterval;

                bool found = HealTargetScorer.TrySelectHealTarget(
                    _self,
                    // ใช้เกณฑ์เดียวกับที่ EnemyBrain ใช้ตัดสินใจเข้า state (รวมโบนัสตอนปะทะ)
                    // ถ้าคนละค่า: เข้า HealAlly ด้วยเกณฑ์ 85% แต่เลือกเป้าด้วย 80% → ยืนนิ่งไม่ฮีลใคร (บั๊กจริงที่เจอ)
                    _blackboard.EffectiveHealWorthThreshold,
                    _blackboard.TankHealThreshold,
                    _blackboard.CriticalAllyHealthThreshold,
                    // ระยะรับรู้/คุ้มเดิน = ใหญ่สุดระหว่าง HealRange×2 กับ Circle Vision ใหญ่ (AllyAwarenessRadius)
                    Mathf.Max(_healRange * 2f, _blackboard.AllyAwarenessRadius),
                    _obstacleMask,
                    out Transform newTarget,
                    out Health newHealth,
                    out float newScore);

                if (!found)
                {
                    // ไม่มีใครต้องฮีลแล้ว — ให้ TacticalDecision ตัดสินใจเปลี่ยน state เอง
                    SetPhase(HealPhase.AcquireTarget, $"ไม่มีเป้าฮีลในระยะ (เกณฑ์ {_blackboard.EffectiveHealWorthThreshold:P0})");
                    DisableBeam();
                    ReleasePatient();
                    _currentTarget = null;
                    _currentTargetHealth = null;
                    _currentTargetBrain = null;
                    _targetCollider = null;
                    if (_blackboard.CaptureReasons)
                        _blackboard.CurrentActionReason =
                            $"Healer: ไม่มีเป้าฮีลในระยะ (เกณฑ์ {_blackboard.EffectiveHealWorthThreshold:P0})";
                    return;
                }

                if (targetInvalid)
                {
                    AssignTarget(newTarget, newHealth); // ยังไม่มีเป้า → รับทันที
                }
                else if (newTarget != _currentTarget && _switchCooldown <= 0f)
                {
                    // Hysteresis: ให้คะแนน "เป้าปัจจุบัน" ด้วยสูตรเดียวกับผู้สมัคร (ยุติธรรม)
                    // แล้วสลับเฉพาะเมื่อเป้าใหม่ดีกว่าอย่างน้อย 18% (กันเดินสลับไปมาซ้ำซ้อน)
                    float currentScore = HealTargetScorer.ScoreHealCandidate(
                        _self, _currentTarget, _self.position,
                        _blackboard.EffectiveHealWorthThreshold, _blackboard.TankHealThreshold,
                        _blackboard.CriticalAllyHealthThreshold,
                        Mathf.Max(_healRange * 2f, _blackboard.AllyAwarenessRadius), _obstacleMask);

                    if (newScore >= currentScore * SwitchHysteresisRatio)
                    {
                        AssignTarget(newTarget, newHealth);
                        _switchCooldown = SwitchCooldown; // เคาะคูลดาวน์กันสลับรัวตอนคะแนนแกว่งเพราะฮีลเพิ่มเลือด
                    }
                }
            }

            if (_currentTarget == null) { SetPhase(HealPhase.AcquireTarget, "ไม่มีเป้า"); DisableBeam(); return; }

            Vector2 selfPos = _self.position;
            Vector2 targetPos = _currentTarget.position;
            float distance = Vector2.Distance(selfPos, targetPos);

            // ── 2) LOS ไปเป้าฮีล — ห้ามทะลุกำแพง แต่ "ไม่นับ collider ตัวเราและเพื่อนเป้าหมาย" ──
            // (ส่วนที่ 2: ของเดิมใช้ ray ตรง ๆ ถ้าชน collider ตัวละครที่อยู่บน ObstacleMask
            //  จะคิดว่ามีกำแพงบัง → เดินชิดเพื่อนแล้วแต่ไม่ยอมฮีลเสียที)
            bool targetVisible = HasHealLineOfSight(selfPos, targetPos, distance);

            // ── 3) ตรวจความปลอดภัยก่อนฮีล/เดิน (§17/§23) — ใช้ได้ทั้งตอนเดินและตอนฮีล ──
            Vector2 playerPos = _blackboard.CurrentTarget != null
                ? (Vector2)_blackboard.CurrentTarget.position
                : Vector2.zero;
            float playerDistance = _blackboard.CurrentTarget != null
                ? Vector2.Distance(selfPos, playerPos)
                : float.MaxValue;

            // เงื่อนไขหยุดฮีล: ผู้เล่นต้อง "ใกล้จริง ๆ" (ครึ่งหนึ่งของ playerDangerDistance ≈ 4.5 หน่วย)
            // เดิมใช้เต็ม 9 หน่วย → แค่ผู้เล่นอยู่ห่าง ๆ ก็ยกเลิกฮีลตลอด → "ไม่ฮีลเลย" ทั้งที่ปลอดภัย
            bool playerTooClose = _blackboard.CanSeeTarget && playerDistance < _playerDangerDistance * 0.5f;

            // เช็คกระสุนแบบ throttle — ทุก 0.1 วิ (ค่ากลางระหว่างความไวหลบกับค่าใช้จ่ายต่อเฟรม)
            _bulletCheckTimer -= deltaTime;
            if (_bulletCheckTimer <= 0f)
            {
                _bulletCheckTimer = BulletCheckInterval;
                _lastBulletIncoming = Dodge.BulletDetector.IsBulletIncoming(selfPos, 2.5f, _bulletMask, out _, out _);
            }
            bool bulletIncoming = _lastBulletIncoming;

            if (playerTooClose || bulletIncoming)
            {
                SetPhase(HealPhase.Interrupted,
                    playerTooClose ? $"ผู้เล่นใกล้ {playerDistance:F1} (เกณฑ์ {_playerDangerDistance * 0.5f:F1})" : "มีกระสุนพุ่งมา");
                // หยุดฮีล → ถอยออกห่างจากผู้เล่น (ยังอยู่แถวเป้าฮีลเพื่อกลับมาฮีลต่อได้)
                DisableBeam();
                ReleasePatient(); // เลิกขอให้เพื่อนชะลอ — หนีให้พ้นก่อน ค่อยกลับมาฮีลใหม่

                // เหตุผลบน HUD — "ทำไมฮีลเลอร์ไม่ฮีลตอนนี้" ตอบตัวเองทันทีตอนเทสต์ (สร้างเฉพาะมี Debug HUD)
                if (_blackboard.CaptureReasons)
                    _blackboard.CurrentActionReason = playerTooClose
                        ? $"⏸ หยุดฮีล: ผู้เล่นใกล้ {playerDistance:F1} (เกณฑ์ {_playerDangerDistance * 0.5f:F1})"
                        : "⏸ หยุดฮีล: มีกระสุนพุ่งมา";

                // กำลังจะเขียนตำแหน่งเอง → สั่ง agent เงียบก่อน กันสองระบบแย่งกันเขียน transform ในเฟรมเดียว
                _mover?.StopPath();

                // ── Call-for-Help: ประกาศว่า "ฉันถูกคุกคาม" ──
                // Tank จะตอบสนองผ่าน GroupAI.TryGetRecentDistress ได้แม้ตัวเองไม่เห็นผู้เล่น
                // (ของเดิม: healer ไม่เคยขอความช่วยเหลือเลย Tank ต้องเดาจากสายตาตัวเอง — มักพลาด)
                Tactical.GroupAI.Instance?.BroadcastDistress(_self, selfPos);

                if (playerTooClose)
                {
                    // แรงหลบเฉพาะหน้า: ถอยตรงออกจากผู้เล่นทันที
                    Vector2 awayDir = (selfPos - playerPos).normalized;
                    Vector2 steered = SteeringMovement.GetSteeredDirection(selfPos, awayDir, _obstacleMask, self: _self, personalSpace: 1.2f);

                    if (steered.sqrMagnitude > 0.0001f)
                    {
                        _self.position = SteeringMovement.MoveWithCollisionCheck(
                            _self, steered * (_moveSpeed * deltaTime), _obstacleMask);
                        _blackboard.CurrentDestination = selfPos + steered * _moveSpeed;
                        FaceDirection(steered, deltaTime);
                    }
                }
                else
                {
                    // ── มีกระสุนแต่ผู้เล่นไม่ได้เข้าใกล้ → เลือกจุดยืนใหม่ตามน้ำหนัก Healer (§16) ──
                    // แทนการยืนนิ่งรอกระสุน — เลือกจุดที่ปลอดภัย+หลัง cover+เข้าถึงเพื่อนได้
                    // (เปิดใช้ ScoreHealer ที่เดิมเป็น dead code — อิง Recruits AI: position evaluation)
                    _repositionCooldown -= deltaTime;
                    if (_repositionCooldown <= 0f)
                    {
                        _repositionCooldown = RepositionInterval;

                        Vector2 anchor = _currentTarget != null ? (Vector2)_currentTarget.position : selfPos;
                        Vector2 playerKnown = _blackboard.CurrentTarget != null
                            ? (Vector2)_blackboard.CurrentTarget.position
                            : selfPos;

                        bool foundPoint = EnemyPositionScorer.TryFindBestPosition(
                            _self, EnemyRole.Support,
                            playerKnown, Vector2.zero, anchor,
                            0f, _obstacleMask, RepositionSearchRadius,
                            out Vector2 newPoint, out _);

                        // Anti-circle: เปลี่ยนจุดหมายเฉพาะเมื่อจุดใหม่ "ต่างจากเดิมชัดเจน" (>1.5 หน่วย)
                        // (ของเดิมสุ่มจุดใหม่ทุก 0.5 วิ → เดินวนไม่หยุด = "เดินวนไปมาบริเวณ" ที่เห็นในคลิป)
                        if (foundPoint && (!_hasRepositionTarget || Vector2.Distance(newPoint, _repositionTarget) > 1.5f))
                        {
                            _repositionTarget = newPoint;
                            _hasRepositionTarget = true;
                        }
                    }

                    if (_hasRepositionTarget
                        && Vector2.Distance(selfPos, _repositionTarget) > 0.6f)
                    {
                        Vector2 toTarget = (_repositionTarget - selfPos).normalized;
                        Vector2 steered = SteeringMovement.GetSteeredDirection(selfPos, toTarget, _obstacleMask, self: _self, personalSpace: 1.2f);

                        if (steered.sqrMagnitude > 0.0001f)
                        {
                            _self.position = SteeringMovement.MoveWithCollisionCheck(
                                _self, steered * (_moveSpeed * deltaTime), _obstacleMask);
                            _blackboard.CurrentDestination = _repositionTarget;
                            FaceDirection(steered, deltaTime);
                        }
                    }
                }
                return;
            }

            // ── 4) MOVE_TO_TARGET: ไกลเกินระยะฮีล "หรือ" มองไม่เห็นเป้า → เดินเข้าไปหา ──
            // ระยะมี hysteresis: กำลังฮีลอยู่ → อดทนต่อจนหลุดเกิน HealRange+2 จึงหยุด
            // (กันอาการใน log: 24.9 ↔ 25.6 สลับ ApplyHeal/MoveToTarget รัว ๆ → ฮีลได้แค่เสี้ยววินาที)
            float effectiveRange = _phase == HealPhase.ApplyHeal ? _healRange + HealRangeHysteresis : _healRange;

            if (distance > effectiveRange || !targetVisible)
            {
                SetPhase(HealPhase.MoveToTarget,
                    $"ระยะ {distance:F1}/{_healRange} | LOS {(targetVisible ? "ผ่าน" : "ติดกำแพง")}");
                DisableBeam();
                SetHealPatient(true); // บอกเพื่อนให้ชะลอตัวรอ (Cooperative Waiting) ระหว่างที่เรายังไปไม่ถึง
                if (_blackboard.CaptureReasons)
                    _blackboard.CurrentActionReason =
                        $"เดินเข้าหาเพื่อน {distance:F1}/{_healRange} | LOS {(targetVisible ? "ผ่าน" : "ติดกำแพง")}";
                WalkToTarget(selfPos, targetPos, deltaTime);
                return;
            }

            // ── 5) APPLY_HEAL: ในระยะ + LOS ผ่าน → ปล่อยสกิลฮีลต่อเนื่อง (HoT) ──
            SetPhase(HealPhase.ApplyHeal,
                $"ระยะ {distance:F1}/{_healRange} | เพื่อน {MathUtility.HealthPercent(_currentTargetHealth.CurrentHP, _currentTargetHealth.MaxHP):P0}");
            SetHealPatient(true); // ยังฮีลไม่จบ — ให้เพื่อนยืนรอตรงนี้ (ขยับได้ 35% และหลบกระสุนได้เต็มที่)

            // guard กันเพื่อนตาย/HP เต็มระหว่างทาง (Health.Heal ก็เช็ค IsDead อยู่แล้ว — เช็คซ้ำชั้นเดียว)
            if (_currentTargetHealth != null && !_currentTargetHealth.IsDead)
                _currentTargetHealth.Heal(_healPerSecond * deltaTime); // ← จุดสั่งเพิ่ม HP จริง (ต่อเนื่อง ไม่มี cooldown เพราะเป็น HoT)

            if (_blackboard.CaptureReasons)
                _blackboard.CurrentActionReason =
                    $"❤ ฮีลต่อเนื่อง → เพื่อน {MathUtility.HealthPercent(_currentTargetHealth.CurrentHP, _currentTargetHealth.MaxHP):P0} | ระยะ {distance:F1}/{_healRange}";

            if (_healBeam != null)
            {
                _healBeam.enabled = true;
                _healBeam.SetPosition(0, _self.position);
                _healBeam.SetPosition(1, targetPos);
            }

            _blackboard.CurrentDestination = targetPos;
            FaceDirection((targetPos - selfPos).normalized, deltaTime);
        }

        /// <summary>ตั้งเป้าหมายใหม่ + cache brain/collider ของเป้า (ใช้ตอน re-eval สลับเป้า)</summary>
        private void AssignTarget(Transform newTarget, Health newHealth)
        {
            string who = newTarget != null ? newTarget.name : "?";
            if (_currentTarget != null && _currentTarget != newTarget)
                Debug.Log($"[Healer] {gameObjectNameForLog}: สลับเป้า {_currentTarget.name} → {who}", _self);
            else if (_currentTarget == null)
                Debug.Log($"[Healer] {gameObjectNameForLog}: ล็อกเป้า {who}", _self);

            // ปลด "เพื่อนเก่า" ที่เคยเป็นเป้าให้ขยับได้ตามปกติก่อน
            ReleasePatient();

            _currentTarget = newTarget;
            _currentTargetHealth = newHealth;
            _currentTargetBrain = newTarget.TryGetComponent(out TopDownTacticalAI.Core.EnemyBrain b) ? b : null;
            _targetCollider = newTarget.TryGetComponent(out Collider2D c) ? c : null;
            SetHealPatient(true);
        }

        /// <summary>
        /// บอกเพื่อนเป้าฮีลให้ชะลอ (true) หรือปล่อยให้ขยับตามปกติ (false)
        /// เงื่อนไข (ส่วนที่ 1.3): ชะลอเฉพาะเพื่อนที่ HP ≤ PatientHealthThreshold (30%) เท่านั้น —
        /// เพื่อนเลือดยังดีพอไม่ต้องรอ ให้ต่อสู้ได้เต็มที่
        /// </summary>
        private void SetHealPatient(bool on)
        {
            if (_currentTargetBrain == null) return;

            bool hpLowEnough = _currentTargetHealth != null && !_currentTargetHealth.IsDead
                && MathUtility.HealthPercent(_currentTargetHealth.CurrentHP, _currentTargetHealth.MaxHP) <= PatientHealthThreshold;

            _currentTargetBrain.IsHealPatient = on && hpLowEnough;
        }

        private void ReleasePatient() => SetHealPatient(false);

        /// <summary>
        /// LOS ไปเป้าฮีล — ห้ามทะลุกำแพง แต่ข้าม collider ของ "ตัวเราเอง" และ "เพื่อนเป้าหมาย"
        /// (แก้ปัญหาเดินชิดเพื่อนแล้วไม่ยอมฮีล เพราะ ray ชน collider ตัวละครจนคิดว่ามีกำแพงบัง)
        /// </summary>
        private bool HasHealLineOfSight(Vector2 from, Vector2 to, float distance)
        {
            if (_selfCollider == null) _selfCollider = _self.GetComponent<Collider2D>();
            return RaycastDetector.HasLineOfSightIgnoring(
                from, to, _obstacleMask, distance + 1f, _selfCollider, _targetCollider);
        }

        /// <summary>
        /// เดินเข้าไปหาเป้าฮีล — ถ้ามี NavMesh ให้เดินตามเส้นทางที่อ้อมกำแพงได้จริง
        /// (แก้ปัญหา "ฮีลเลอร์พยายามเดินไปหาเพื่อนบาดเจ็บแต่ติดกำแพง" — เดินไกลข้ามแมพ steering เดิมหลงทางง่าย)
        /// ไม่มี NavMesh → fallback wall-following เดิม (ผูกพันทิศ 1.5 วิ กันสลับซ้ายขวา)
        ///
        /// Target Interception (ส่วนที่ 3): เป้าหมายเดิน = ตำแหน่ง "คาดการณ์" ของเพื่อนตอนเราไปถึง
        /// (ตำแหน่งปัจจุบัน + ความเร็ว × lead) — เดินตัดหน้าแทนการไล่ตามตูด ไล่ทันและเสียเวลาน้อยกว่า
        /// </summary>
        private void WalkToTarget(Vector2 selfPos, Vector2 targetPos, float deltaTime)
        {
            float distance = Vector2.Distance(selfPos, targetPos);
            Vector2 dir = (targetPos - selfPos).normalized;

            // ── คำนวณจุดที่เพื่อน "น่าจะอยู่ตอนเราไปถึง" (interception) ──
            Vector2 destination = targetPos;
            if (_currentTargetBrain != null && distance > 4f)
            {
                Vector2 allyVel = _currentTargetBrain.VelocityEstimate;
                if (allyVel.sqrMagnitude > 1f)
                {
                    // lead = เวลาที่เราต้องใช้เดินไปถึง (จำกัด ≤1.2 วิ กันทำนายไกลเกินจริง)
                    float lead = Mathf.Clamp(distance / Mathf.Max(_moveSpeed, 0.1f), 0f, 1.2f);
                    Vector2 predicted = targetPos + allyVel * lead;

                    // กันเพี้ยน: จุดทำนายห่างจากตัวจริงไม่เกิน 6 หน่วย
                    Vector2 offset = predicted - targetPos;
                    if (offset.magnitude > 6f)
                        predicted = targetPos + offset.normalized * 6f;

                    destination = predicted;
                }
            }

            Vector2 destDir = (destination - selfPos).normalized;

            // ── โหมด NavMesh: ให้ pathfinder พาอ้อมกำแพงไปหาเป้าโดยตรง ──
            if (_mover != null && _mover.PathAvailable)
            {
                Vector2 walkDest = destination;

                // ยังมองไม่เห็นเพื่อน (มีอะไรกั้นข้างหน้า) → เลือก "จุดยืนรอบเพื่อนที่สายตาโล่งถึงเพื่อน"
                // แทนการเดินชนเข้าไปติดกำแพงตรงหน้าเพื่อน — ฮีลได้ต้องเห็นชัดแบบไม่มีอะไรมากั้นระหว่างกัน
                if (!HasHealLineOfSight(selfPos, targetPos, distance)
                    && TryFindLosClearSpot(selfPos, targetPos, out Vector2 clearSpot))
                {
                    walkDest = clearSpot;
                }

                // arriveThreshold = HealStopDistance — สั้นกว่า HealRange เสมอ:
                // ฮีลได้จากระยะ HealRange อยู่แล้ว ไม่ต้องเดินชนตัวเพื่อน (ป้องกันเข้าไม่ถึงระยะเพราะชนกัน)
                _mover.MoveTo(walkDest, deltaTime, _moveSpeed, arriveThreshold: HealStopDistance);
                _blackboard.CurrentDestination = walkDest;
                // §26 — ยังคงหน้าหาเป้าฮีลขณะเดิน (updateRotation ของ agent ปิดอยู่ เราคุมคนเดียว)
                FaceDirection(dir, deltaTime);
                return;
            }

            // ── Fallback: wall-following เดิม (ไม่มี mesh ในฉาก) ──
            Vector2 moveDirection;

            bool wallAhead = IsWallAhead(selfPos, destDir);
            bool detouring = _detourTimer > 0f && _detourDirection.sqrMagnitude > 0.0001f;

            if (!wallAhead && !detouring)
            {
                // ทางโล่ง → เดินตรงเข้าหาเป้า
                moveDirection = destDir;
            }
            else
            {
                // กำแพงขวาง → wall follow
                if (!detouring)
                {
                    // เลือกซ้าย/ขวาที่ "โล่งลึกกว่า" — นับทางหนีที่จุดหนึ่งเดินจากตรงนั้น
                    Vector2 perp = Vector2.Perpendicular(destDir);
                    Vector2 leftProbe = selfPos + perp * 3f;
                    Vector2 rightProbe = selfPos - perp * 3f;
                    int leftOpen = OpenDirectionsAt(leftProbe);
                    int rightOpen = OpenDirectionsAt(rightProbe);
                    _detourDirection = leftOpen >= rightOpen ? perp : -perp;
                    _detourTimer = DetourCommitTime;
                }

                _detourTimer -= deltaTime;
                if (_detourTimer <= 0f)
                    _detourDirection = Vector2.zero; // หมดเวลาผูกพัน → ประเมินใหม่เฟรมหน้า

                // เดินตามขอบกำแพง + โค้งเข้าหาเป้าเบา ๆ (ไม่หนีเป้าไกลเกิน)
                moveDirection = _detourDirection.sqrMagnitude > 0.0001f
                    ? (_detourDirection + destDir * 0.6f).normalized
                    : destDir;
            }

            if (moveDirection.sqrMagnitude < 0.0001f) return;

            var steered = SteeringMovement.GetSteeredDirection(
                selfPos, moveDirection, _obstacleMask, self: _self, personalSpace: 1.2f);
            if (steered.sqrMagnitude < 0.0001f) return;

            _self.position = SteeringMovement.MoveWithCollisionCheck(
                _self, steered * (_moveSpeed * deltaTime), _obstacleMask);
            _blackboard.CurrentDestination = destination;

            // §26 — healer หันหน้าไปทาง "เป้าฮีล" ขณะเดินไปฮีล (ไม่ใช่หันตามทิศเดิน)
            FaceDirection(dir, deltaTime);
        }

        // buffer กลาง — ไม่ allocate array ใหม่ทุกเฟรม/ทุกครั้งที่เช็คกำแพง (single-threaded ใช้แล้วจบในรอบเรียก)
        private static readonly RaycastHit2D[] s_wallHits = new RaycastHit2D[2];
        private static readonly RaycastHit2D[] s_stepHits = new RaycastHit2D[4];
        private static readonly Vector2[] s_cardinalDirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        private static readonly Vector2[] s_eightDirs =
        {
            Vector2.up, new Vector2(0.7071f, 0.7071f), Vector2.right, new Vector2(0.7071f, -0.7071f),
            Vector2.down, new Vector2(-0.7071f, -0.7071f), Vector2.left, new Vector2(-0.7071f, 0.7071f)
        };

        /// <summary>
        /// หา "จุดยืนรอบเพื่อน" ที่สายตาโล่งถึงเพื่อนจริง (ไม่มีอะไรมากั้นระหว่างกัน) — 8 ทิศ × 2 ระยะ
        /// ใช้ตอนมีกำแพงขวางระหว่าง healer กับเพื่อน: ให้เดินไปยืนมุมที่เห็นชัด แล้วค่อยฮีล
        /// คืน false ถ้ารอบเพื่อนถูกบังหมดทุกทิศ (ผู้เรียกใช้ destination เดิมต่อ)
        /// </summary>
        private bool TryFindLosClearSpot(Vector2 selfPos, Vector2 allyPos, out Vector2 spot)
        {
            if (_selfCollider == null) _selfCollider = _self.GetComponent<Collider2D>();

            float[] rings = { HealStopDistance + 2.5f, Mathf.Min(_healRange * 0.5f, 12f) };
            float bestSqr = float.MaxValue;
            spot = allyPos;
            bool found = false;

            foreach (float ring in rings)
            {
                foreach (var dir in s_eightDirs)
                {
                    Vector2 cand = allyPos + dir * ring;

                    // เห็นจากตัวเรา → จุดยืน และจากจุดยืน → เพื่อน (ข้าม collider ของเพื่อนเป้าหมาย)
                    float dSelf = Vector2.Distance(selfPos, cand);
                    if (!RaycastDetector.HasLineOfSightIgnoring(selfPos, cand, _obstacleMask, dSelf + 1f, _selfCollider, _targetCollider))
                        continue;
                    float dAlly = Vector2.Distance(cand, allyPos);
                    if (!RaycastDetector.HasLineOfSightIgnoring(cand, allyPos, _obstacleMask, dAlly + 1f, null, _targetCollider))
                        continue;

                    float sqr = (cand - selfPos).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        spot = cand;
                        found = true;
                    }
                }
            }
            return found;
        }

        /// <summary>กำแพงขวางหน้าเดินไหม — CircleCast ขนาดตัว+padding ยาว 3 หน่วย</summary>
        private bool IsWallAhead(Vector2 from, Vector2 dir)
        {
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = _obstacleMask,
                useTriggers = false
            };
            var hits = s_wallHits;
            float radius = GetBodyRadius() + WallPadding;

            int count = Physics2D.CircleCast(from, radius, dir, filter, hits, 3f);
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null) return true;

            return false;
        }

        /// <summary>นับทางหนีรอบจุด (4 ทิศหลัก) — ใช้เลือก detour ที่โล่งกว่า</summary>
        private int OpenDirectionsAt(Vector2 point)
        {
            int open = 0;
            foreach (var d in s_cardinalDirs)
                if (!IsWallAhead(point, d)) open++;
            return open;
        }

        private void DisableBeam()
        {
            if (_healBeam != null && _healBeam.enabled) _healBeam.enabled = false;
        }

        /// <summary>
        /// เช็คว่า "ก้าวนี้" จะพาตัวไปชน/มุดกำแพงหรือไม่ — ตรวจด้วย CircleCast จากตำแหน่งปัจจุบัน
        /// ใช้รัศมีจาก collider จริง + ระยะเผื่อ เพื่อให้ "ปากแหลม" ของสไปรต์ไม่มุดเข้ากำแพงก่อน collider แตะ
        /// </summary>
        private bool IsStepBlocked(Vector2 from, Vector2 step)
        {
            float distance = step.magnitude;
            if (distance < 0.0001f) return false;

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = _obstacleMask,
                useTriggers = false
            };
            var hits = s_stepHits;

            float radius = GetBodyRadius() + WallPadding;
            int count = Physics2D.CircleCast(from, radius, step.normalized, filter, hits, distance + 0.05f);

            for (int i = 0; i < count; i++)
                if (hits[i].collider != null) return true;

            // จุดปลายทางต้องไม่อยู่ในสิ่งกีดขวางด้วย
            return PhysicsUtility.IsPositionBlocked(from + step, radius, _obstacleMask);
        }

        /// <summary>รัศมีครอบตัวของ collider เอง (อ่านจาก bounds จริง กันสไปรต์แหลมพ้นกรอบ)</summary>
        private float GetBodyRadius()
        {
            var collider = _self.GetComponent<Collider2D>();
            if (collider == null) return 0.5f;
            var e = collider.bounds.extents;
            return Mathf.Max(e.x, e.y);
        }

        /// <summary>หันหน้าแบบเฟรมเวิร์ก (สไปรต์หัน "ขึ้น" จึงต้องลบ 90° เหมือน AimController)</summary>
        private void FaceDirection(Vector2 direction, float deltaTime)
        {
            if (direction.sqrMagnitude < 0.0001f) return;

            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            float currentAngle = _self.eulerAngles.z;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, _turnSpeed * deltaTime);
            _self.rotation = Quaternion.Euler(0f, 0f, newAngle);
        }

        /// <summary>เปลี่ยน phase พร้อม log เมื่อเปลี่ยนจริงเท่านั้น (ใช้วิเคราะห์จาก Editor.log — ปริมาณน้อย)</summary>
        private void SetPhase(HealPhase next, string why)
        {
            if (_phase == next) return;
            Debug.Log($"[Healer] {gameObjectNameForLog}: {_phase} → {next} | {why}", _self);
            _phase = next;
        }

        private string gameObjectNameForLog => _self != null ? _self.name : "?";

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit()
        {
            DisableBeam();
            ReleasePatient(); // ออกจาก state → เพื่อนที่เคยเป็นเป้ากลับขยับได้ตามปกติ
            _currentTarget = null;
            _currentTargetHealth = null;
            _currentTargetBrain = null;
            _targetCollider = null;
            _phase = HealPhase.AcquireTarget;
        }
    }
}

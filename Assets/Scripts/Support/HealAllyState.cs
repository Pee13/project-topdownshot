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
        private float _targetSwitchCooldown;
        private readonly LayerMask _bulletMask;

        // ── Wall-following detour (เดินอ้อมกำแพงไปฮีล) ──
        // ผูกพันเดินตามขอบกำแพงทิศเดียว 1.5 วิ — กันสลับซ้ายขวาจนก้ำกึ่ง (§28)
        private Vector2 _detourDirection;
        private float _detourTimer;
        private const float DetourCommitTime = 1.5f;

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
            float turnSpeed = 360f)
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
        }

        public void Enter()
        {
            // LineRenderer ของ Sup ใช้ตอนวาดลำแสงฮีล — หาเองถ้ายังไม่ได้ลากใน Inspector
            if (_healBeam == null) _healBeam = _self.GetComponent<LineRenderer>();
        }

        public void Tick(float deltaTime)
        {
            if (_targetSwitchCooldown > 0f) _targetSwitchCooldown -= deltaTime;

            // ── 1) เลือกเป้าฮีล ──
            // มี cooldown กันเปลี่ยนเป้าทุกเฟรมเมื่อคะแนนสองตัวใกล้กัน (§28 anti-jitter)
            if (_currentTarget == null || _currentTargetHealth == null
                || _currentTargetHealth.IsDead || _targetSwitchCooldown <= 0f)
            {
                bool found = HealTargetScorer.TrySelectHealTarget(
                    _self,
                    _blackboard.HealWorthThreshold,
                    _blackboard.TankHealThreshold,
                    _blackboard.CriticalAllyHealthThreshold,
                    _healRange * 2f,
                    _obstacleMask,
                    out Transform newTarget,
                    out Health newHealth,
                    out float newScore);

                if (found)
                {
                    // เปลี่ยนเป้าเฉพาะเมื่อเป้าใหม่ดีกว่าเป้าเดิม "ชัดเจน" — กันสลับไปมา
                    if (_currentTarget == null || newTarget != _currentTarget)
                    {
                        float currentScore = 0f;
                        if (_currentTargetHealth != null && !_currentTargetHealth.IsDead)
                        {
                            float hp = MathUtility.HealthPercent(_currentTargetHealth.CurrentHP, _currentTargetHealth.MaxHP);
                            currentScore = 100f - hp * 100f * 0.5f; // ประมาณคะแนนเป้าเดิมจากเลือดที่หายไป
                        }

                        if (_currentTarget == null || newScore >= currentScore + 10f)
                        {
                            _currentTarget = newTarget;
                            _currentTargetHealth = newHealth;
                            _targetSwitchCooldown = 0.5f;
                        }
                    }
                }
                else
                {
                    // ไม่มีใครต้องฮีลแล้ว — ให้ TacticalDecision ตัดสินใจเปลี่ยน state เอง
                    DisableBeam();
                    _currentTarget = null;
                    _currentTargetHealth = null;
                    return;
                }
            }

            if (_currentTarget == null) { DisableBeam(); return; }

            Vector2 selfPos = _self.position;
            Vector2 targetPos = _currentTarget.position;
            float distance = Vector2.Distance(selfPos, targetPos);

            // ── 2) LoS ไปเป้าฮีล — ลำแสงฮีลห้ามทะลุกำแพง (§17) ──
            bool targetVisible = RaycastDetector.HasLineOfSight(selfPos, targetPos, _obstacleMask, distance + 1f);

            // ── 3) ตรวจความปลอดภัยก่อนฮีล/เดิน (§17/§23) — ใช้ได้ทั้งตอนเดินและตอนฮีล ──
            Vector2 playerPos = _blackboard.CurrentTarget != null
                ? (Vector2)_blackboard.CurrentTarget.position
                : Vector2.zero;
            float playerDistance = _blackboard.CurrentTarget != null
                ? Vector2.Distance(selfPos, playerPos)
                : float.MaxValue;

            bool playerTooClose = _blackboard.CanSeeTarget && playerDistance < _playerDangerDistance;
            bool bulletIncoming = Dodge.BulletDetector.IsBulletIncoming(selfPos, 2.5f, _bulletMask, out _, out _);

            if (playerTooClose || bulletIncoming)
            {
                // หยุดฮีล → ถอยออกห่างจากผู้เล่น (ยังอยู่แถวเป้าฮีลเพื่อกลับมาฮีลต่อได้)
                DisableBeam();

                if (playerTooClose)
                {
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
                return;
            }

            // ── 4) ไกลเกินระยะฮีล "หรือ" มองไม่เห็นเป้า (มีกำแพงขวาง) → เดินเข้าไปหา ──
            // เดิมเดินตรง ๆ แล้วเลียนกำแพงไปมา (แต่ละเฟรมลองเลี้ยวใหม่ ไม่มีการผูกพันทิศ)
            // ตอนนี้ใช้ wall-following: ผูกพันเดินตามขอบกำแพงจนมองเห็นเป้า แล้วค่อยเดินตรง
            if (distance > _healRange || !targetVisible)
            {
                DisableBeam();
                WalkToTarget(selfPos, targetPos, deltaTime);
                return;
            }

            // ── 5) ในระยะ + เห็นเป้า → ฮีล + หันหน้าไปทางเป้าฮีล (§26) ──
            _currentTargetHealth.Heal(_healPerSecond * deltaTime);

            if (_healBeam != null)
            {
                _healBeam.enabled = true;
                _healBeam.SetPosition(0, _self.position);
                _healBeam.SetPosition(1, targetPos);
            }

            _blackboard.CurrentDestination = targetPos;
            FaceDirection((targetPos - selfPos).normalized, deltaTime);
        }

        /// <summary>
        /// เดินเข้าไปหาเป้าฮีลแบบ "อ้อมกำแพงได้" (§ — ไม่มี pathfinding ในโปรเจกต์
        /// จึงใช้ wall-following แบบผูกพันทิศแทน)
        ///
        /// หลักการ: ถ้ากำแพงขวางหน้าเดิน → เลือกซ้าย/ขวาที่โล่งกว่า แล้ว "ผูกพัน" เดิน
        /// ตามขอบกำแพงในทิศนั้น 1.5 วินาที (กันสลับซ้ายขวาจนก้ำกึ่ง — §28)
        /// พอมองเห็นเป้าแล้ว (อ้อมมาถึง) → ปิด detour เดินตรงเข้าหา
        /// </summary>
        private void WalkToTarget(Vector2 selfPos, Vector2 targetPos, float deltaTime)
        {
            Vector2 dir = (targetPos - selfPos).normalized;
            Vector2 moveDirection;

            bool wallAhead = IsWallAhead(selfPos, dir);
            bool detouring = _detourTimer > 0f && _detourDirection.sqrMagnitude > 0.0001f;

            if (!wallAhead && !detouring)
            {
                // ทางโล่ง → เดินตรงเข้าหาเป้า
                moveDirection = dir;
            }
            else
            {
                // กำแพงขวาง → wall follow
                if (!detouring)
                {
                    // เลือกซ้าย/ขวาที่ "โล่งลึกกว่า" — นับทางหนีที่จุดหนึ่งเดินจากตรงนั้น
                    Vector2 perp = Vector2.Perpendicular(dir);
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
                    ? (_detourDirection + dir * 0.6f).normalized
                    : dir;
            }

            if (moveDirection.sqrMagnitude < 0.0001f) return;

            var steered = SteeringMovement.GetSteeredDirection(
                selfPos, moveDirection, _obstacleMask, self: _self, personalSpace: 1.2f);
            if (steered.sqrMagnitude < 0.0001f) return;

            _self.position = SteeringMovement.MoveWithCollisionCheck(
                _self, steered * (_moveSpeed * deltaTime), _obstacleMask);
            _blackboard.CurrentDestination = targetPos;

            // §26 — healer หันหน้าไปทาง "เป้าฮีล" ขณะเดินไปฮีล (ไม่ใช่หันตามทิศเดิน)
            FaceDirection(dir, deltaTime);
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
            var hits = new RaycastHit2D[2];
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
            foreach (var d in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
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
            var hits = new RaycastHit2D[4];

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

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit()
        {
            DisableBeam();
        }
    }
}

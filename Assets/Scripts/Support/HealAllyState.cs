using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Player;
using TopDownTacticalAI.Tactical;
using TopDownTacticalAI.Utilities;

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

            // ── 2) ไกลเกินระยะฮีล → เดินเข้าไปหา ──
            if (distance > _healRange)
            {
                DisableBeam();
                Vector2 dir = (targetPos - selfPos).normalized;
                Vector2 steered = SteeringMovement.GetSteeredDirection(selfPos, dir, _obstacleMask, self: _self, personalSpace: 1.2f);

                if (steered.sqrMagnitude > 0.0001f)
                {
                    Vector2 step = steered * (_moveSpeed * deltaTime);
                    Vector2 nextPos = selfPos + step;

                    // กันมุดกำแพง: เช็ค "จุดที่จะไปถึง" ก่อนย้ายจริงทุกเฟรม
                    // ไม่ใช่ย้ายก่อนแล้วค่อยให้ระบบชนดันออก (ปากตัวแหลมจะมุดเข้าไปก่อน collider แตะ)
                    if (IsStepBlocked(selfPos, step))
                    {
                        // ทางตรงตัน → ลองเลื่อนทแยง 45° สองข้าง (เดินอ้อมต่อ ไม่หยุดค้าง)
                        Vector2 perp = new Vector2(-steered.y, steered.x);
                        Vector2 left = (steered + perp * 0.9f).normalized;
                        Vector2 right = (steered - perp * 0.9f).normalized;

                        Vector2 chosen = Vector2.zero;
                        if (!IsStepBlocked(selfPos, left * _moveSpeed * deltaTime))
                            chosen = left;
                        else if (!IsStepBlocked(selfPos, right * _moveSpeed * deltaTime))
                            chosen = right;

                        if (chosen.sqrMagnitude > 0.0001f)
                        {
                            Vector2 altStep = chosen * (_moveSpeed * deltaTime);
                            _self.position = selfPos + altStep;
                            _blackboard.CurrentDestination = targetPos;
                            FaceDirection(steered, deltaTime);
                        }
                        // สองข้างตัน → ยืนรอเฟรมหน้า (อย่าฝืนเดินเข้ากำแพง)
                        return;
                    }

                    _self.position = selfPos + step;
                    _blackboard.CurrentDestination = targetPos;
                    FaceDirection(steered, deltaTime);
                }
                return;
            }

            // ── 3) ตรวจความปลอดภัยก่อนฮีล (§17/§23) ──
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
                        _self.position = selfPos + steered * _moveSpeed * deltaTime;
                        _blackboard.CurrentDestination = selfPos + steered * _moveSpeed;
                        FaceDirection(steered, deltaTime);
                    }
                }
                return;
            }

            // ── 4) ปลอดภัย → ฮีล + หันหน้าไปทางเป้าฮีล (§26) ──
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

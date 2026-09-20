using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Combat;
using TopDownTacticalAI.Tactical;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Support
{
    /// <summary>
    /// Flanker ปะทะแบบอ้อม (§3, §7, §11, §25)
    ///
    /// แยกพฤติกรรมตาม "วงระยะ" กับผู้เล่น (§7):
    ///   ไกลเกิน MaxAttackRange × 1.5 → เข้าหา (Approach)
    ///   ใกล้เกิน PreferredMinRange   → ถอยหลัง (Backstep — ไม่บุกชนตัวต่อตัว)
    ///   อยู่ในวงกลาง                → หาตำแหน่งข้าง/หลังผู้เล่นด้วย Position Scorer (§11)
    ///                                 แล้ว Strafe วนรอบ + ยิงระหว่างวน
    ///
    /// §25 (สำคัญ): Movement กับ Facing แยกกัน — ไม่ว่าจะวิ่งเข้า/ถอย/วน
    /// ตัว "หันหน้า" ไปทางผู้เล่นเสมอ ห้ามหันตามทิศที่เดิน
    ///
    /// §11 น้ำหนักจุดยืน: FlankAngle 25 + AttackPotential 20 + Safety 20
    ///                     + CombatDistance 15 + EscapeRoute 10 + Cover 10
    /// §28 anti-jitter: เปลี่ยนจุดยืนเมื่อจุดใหม่ดีกว่า ≥ 10 คะแนน
    /// </summary>
    public class FlankState : IAIState
    {
        public EnemyState StateType => EnemyState.Flank;

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly float _moveSpeed;
        private readonly float _maxAttackRange;
        private readonly float _minRange;
        private readonly LayerMask _obstacleMask;
        private readonly ShootController _shoot;
        private readonly float _maxFireRange;

        private Vector2 _currentStation;
        private float _currentStationScore = -1f;
        private float _scoreCooldown;
        private readonly float _scoreInterval = 0.4f; // §27 tactical update
        private readonly ReloadController _reload;    // ไม่งั้นยิง 12 นัดแล้วรีโหลดไม่ได้อีกเลย

        public FlankState(
            Transform self,
            Blackboard blackboard,
            float moveSpeed,
            float maxAttackRange,
            float minRange,
            LayerMask obstacleMask,
            ShootController shoot = null,
            float maxFireRange = 6f,
            ReloadController reload = null)
        {
            _self = self;
            _blackboard = blackboard;
            _moveSpeed = moveSpeed;
            _maxAttackRange = maxAttackRange;
            _minRange = minRange;
            _obstacleMask = obstacleMask;
            _shoot = shoot;
            _maxFireRange = maxFireRange;
            _reload = reload;
            _currentStation = self != null ? (Vector2)self.position : Vector2.zero;
        }

        public void Enter()
        {
            // เข้า state ใหม่ → คำนวณจุดยืนใหม่ทันที
            _currentStationScore = -1f;
            _scoreCooldown = 0f;
        }

        public void Tick(float deltaTime)
        {
            // ── รีโหลด: กระสุนหมด → รีโหลดทันที (ไม่งั้นยิง 12 นัดแล้วเงียบตลอดชีพ) ──
            // หมายเหตุ: _reload.Tick รันอยู่แล้วใน EnemyBrain.Update ที่เดียว จึงไม่เรียกซ้ำที่นี่
            if (_reload != null && _reload.NeedsReload && !_blackboard.IsReloading)
                _reload.StartReload();

            if (_blackboard.CurrentTarget == null) return;

            Vector2 selfPos = _self.position;
            Vector2 playerPos = _blackboard.CurrentTarget.position;
            float distToPlayer = Vector2.Distance(selfPos, playerPos);

            Vector2 moveDirection = Vector2.zero;

            // ── §7 วงระยะ ──
            if (distToPlayer > _maxAttackRange * 1.5f)
            {
                // ไกลเกิน → เข้าหา (Approach)
                moveDirection = (playerPos - selfPos).normalized;
            }
            else if (distToPlayer < _minRange)
            {
                // ใกล้เกิน → ถอยหลัง (Backstep — §3 ไม่บุกชนตัวต่อตัว)
                moveDirection = (selfPos - playerPos).normalized;
            }
            else
            {
                // อยู่ในวงกลาง → Strafe: หาตำแหน่งข้าง/หลังด้วย Position Scorer (§11)
                _scoreCooldown -= deltaTime;
                if (_scoreCooldown <= 0f)
                {
                    _scoreCooldown = _scoreInterval;

                    // ทิศที่ผู้เล่นหันหน้า — ใช้คำนวณ FlankAngle (ข้าง/หลังดีกว่าหน้า)
                    Vector2 playerFacing = Vector2.zero;
                    var playerBody = _blackboard.CurrentTarget.GetComponent< Rigidbody2D>();
                    if (playerBody != null)
                        playerFacing = playerBody.transform.right;

                    bool found = EnemyPositionScorer.TryFindBestPosition(
                        _self, EnemyRole.Flanker,
                        playerPos, playerFacing, Vector2.zero,
                        0f, _obstacleMask, _maxAttackRange * 0.8f,
                        out Vector2 bestPosition, out float bestScore);

                    // §28 anti-jitter: เปลี่ยนจุดยืนเมื่อจุดใหม่ดีกว่าชัดเจน (≥ 10 คะแนน)
                    if (found && bestScore >= _currentStationScore + 10f)
                    {
                        _currentStation = bestPosition;
                        _currentStationScore = bestScore;
                    }
                }

                float stationDistance = Vector2.Distance(selfPos, _currentStation);
                if (stationDistance > 0.6f)
                    moveDirection = (_currentStation - selfPos).normalized;
                // อยู่จุดยืนแล้ว → เดินช้า ๆ วนรอบ (Strafe) — ไม่หยุดนิ่งให้ผู้เล่นเล็งง่าย
                else
                    moveDirection = Vector2.Perpendicular((playerPos - selfPos).normalized) * 0.4f;
            }

            // ── เดิน (แยกจากการหันหน้า — §25) ──
            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                Vector2 steered = SteeringMovement.GetSteeredDirection(
                    selfPos, moveDirection.normalized, _obstacleMask, self: _self, personalSpace: 1.2f);

                if (steered.sqrMagnitude > 0.0001f)
                {
                    _self.position = SteeringMovement.MoveWithCollisionCheck(
                        _self, steered * (_moveSpeed * deltaTime), _obstacleMask);
                }
            }
            _blackboard.CurrentDestination = moveDirection.sqrMagnitude > 0.0001f
                ? selfPos + moveDirection * _moveSpeed
                : selfPos;

            // ── §25 หันหน้า/เล็งผู้เล่นเสมอ (ไม่ว่าจะเดินไปทางไหน) ──
            Vector2 toPlayer = playerPos - selfPos;
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                float targetAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg - 90f;
                float currentAngle = _self.eulerAngles.z;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, 360f * deltaTime);
                _self.rotation = Quaternion.Euler(0, 0, newAngle);
            }

            // ── ยิงเมื่อเห็น + หันตรง + ระยะพอ ──
            TryShoot(playerPos, deltaTime);
        }

        private void TryShoot(Vector2 playerPos, float deltaTime)
        {
            if (_shoot == null) return;
            if (!_blackboard.CanSeeTarget) return;
            if (_blackboard.IsReloading || _blackboard.CurrentAmmo <= 0) return;

            Vector2 selfPos = _self.position;
            float distance = Vector2.Distance(selfPos, playerPos);
            if (distance > _maxFireRange) return;

            Vector2 toTarget = (playerPos - selfPos).normalized;
            float angleDiff = Vector2.Angle(_self.up, toTarget);
            if (angleDiff > 12f) return;

            if (!AttackDecision.ShouldFire(_blackboard, selfPos, playerPos, _obstacleMask, _maxFireRange, true))
                return;

            _shoot.Tick(deltaTime);
            if (!_shoot.CanShoot) return;

            _shoot.Shoot(toTarget);
            _blackboard.CurrentAmmo--;
        }

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit()
        {
            _currentStationScore = -1f;
        }
    }
}

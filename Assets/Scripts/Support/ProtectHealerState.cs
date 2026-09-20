using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Combat;
using TopDownTacticalAI.Tactical;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Support
{
    /// <summary>
    /// Tank ยืนบังระหว่างผู้เล่นกับ Healer (§2, §8, §9)
    ///
    /// หลักการ (§4): ProtectionDirection = normalize(Player - Healer)
    ///               DesiredPosition = Healer + ProtectionDirection * ProtectionDistance
    /// แต่ "ไม่ยึดตาย" กับจุดนั้น — ใช้ EnemyPositionScorer ให้คะแนน 16 จุดรอบตัว
    /// ตามน้ำหนัก Tank (§8) แล้วเลือกจุดที่บังได้ดีที่สุด
    ///
    /// §9: ถ้าผู้เล่นขยับไปอีกด้าน จุดบังที่ดีที่สุดจะเลื่อนตามอัตโนมัติ (dynamic reposition)
    /// พร้อม anti-jitter (§28) — เปลี่ยนจุดยืนเฉพาะเมื่อจุดใหม่ดีกว่าจุดเดิม ≥ 10 คะแนน
    ///
    /// ขณะบัง ยังยิงผู้เล่นได้ถ้าเห็น + ระยะพอ (§10 — Tank can attack player)
    /// </summary>
    public class ProtectHealerState : IAIState
    {
        public EnemyState StateType => EnemyState.ProtectHealer;

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly float _moveSpeed;
        private readonly float _protectionDistance;
        private readonly float _searchRadius;
        private readonly LayerMask _obstacleMask;
        private readonly ShootController _shoot;
        private readonly float _maxFireRange;

        private Vector2 _currentStation;
        private float _currentStationScore = -1f;
        private float _scoreCooldown;
        private readonly float _scoreInterval = 0.3f; // §27 — tactical update 3-5 ครั้ง/วินาที

        public ProtectHealerState(
            Transform self,
            Blackboard blackboard,
            float moveSpeed,
            float protectionDistance,
            float searchRadius,
            LayerMask obstacleMask,
            ShootController shoot = null,
            float maxFireRange = 6f)
        {
            _self = self;
            _blackboard = blackboard;
            _moveSpeed = moveSpeed;
            _protectionDistance = protectionDistance;
            _searchRadius = searchRadius;
            _obstacleMask = obstacleMask;
            _shoot = shoot;
            _maxFireRange = maxFireRange;
            _currentStation = self != null ? (Vector2)self.position : Vector2.zero;
        }

        public void Enter()
        {
            // เข้า state ใหม่ → บังคับคำนวณจุดยืนใหม่ทันที (ไม่ใช้ค่าค้างจากรอบก่อน)
            _currentStationScore = -1f;
            _scoreCooldown = 0f;
        }

        public void Tick(float deltaTime)
        {
            Transform healer = _blackboard.HealerTransform;
            if (healer == null) return; // healer ตาย/หายไป — ให้ TacticalDecision ตัดสินใจใหม่

            Vector2 selfPos = _self.position;
            Vector2 healerPos = healer.position;

            // ผู้เล่น ณ ตำแหน่งที่ตัวเองรับรู้ได้จริง (§50 — ห้ามรู้ข้อมูลนอก perception)
            Vector2 playerPos;
            if (_blackboard.CanSeeTarget && _blackboard.CurrentTarget != null)
                playerPos = _blackboard.CurrentTarget.position;
            else if (_blackboard.HasMemory)
                playerPos = _blackboard.LastSeen.Position;
            else return; // ไม่รู้ว่าผู้เล่นอยู่ไหน — ยืนเฝ้า healer ก่อน

            // ── 1) คำนวณจุดยืนที่บังได้ดีที่สุด (tactical update §27) ──
            _scoreCooldown -= deltaTime;
            if (_scoreCooldown <= 0f)
            {
                _scoreCooldown = _scoreInterval;

                bool found = EnemyPositionScorer.TryFindBestPosition(
                    _self, EnemyRole.Defensive,
                    playerPos, playerFacing: Vector2.zero,
                    healerPos, _protectionDistance, _obstacleMask, _searchRadius,
                    out Vector2 bestPosition, out float bestScore);

                // §28 anti-jitter: เปลี่ยนจุดยืนเฉพาะเมื่อจุดใหม่ดีกว่าจุดเดิมชัดเจน (≥ 10 คะแนน)
                if (found && bestScore >= _currentStationScore + 10f)
                {
                    _currentStation = bestPosition;
                    _currentStationScore = bestScore;
                }
            }

            _blackboard.CurrentDestination = _currentStation;

            // ── 2) เดินไปยืนจุดบัง ──
            float stationDistance = Vector2.Distance(selfPos, _currentStation);
            if (stationDistance > 0.6f)
            {
                Vector2 dir = (_currentStation - selfPos).normalized;
                Vector2 steered = SteeringMovement.GetSteeredDirection(
                    selfPos, dir, _obstacleMask, self: _self, personalSpace: 1.2f);

                if (steered.sqrMagnitude > 0.0001f)
                {
                    // เดินแบบ "เช็คก่อนก้าว" (กันมุดกำแพง — ระบบกลางเดียวกับ state อื่น)
                    _self.position = SteeringMovement.MoveWithCollisionCheck(
                        _self, steered * (_moveSpeed * deltaTime), _obstacleMask);
                }
            }

            // ── 3) หันหน้าเข้าหาผู้เล่นเสมอ (§25 — Tank เดินบังแต่หันไปทางผู้เล่น) ──
            Vector2 toPlayer = playerPos - selfPos;
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                float targetAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg - 90f;
                float currentAngle = _self.eulerAngles.z;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, 360f * deltaTime);
                _self.rotation = Quaternion.Euler(0, 0, newAngle);
            }

            // ── 4) ยิงผู้เล่นได้ถ้าเห็น + ระยะพอ (§10) ──
            TryShootWhileBlocking(playerPos, deltaTime);
        }

        /// <summary>ยิงได้เมื่อเห็นผู้เล่นจริง (LoS) + ระยะพอ + หันหน้าตรงพอ — แบบเดียวกับ DodgeState</summary>
        private void TryShootWhileBlocking(Vector2 playerPos, float deltaTime)
        {
            if (_shoot == null) return;
            if (!_blackboard.CanSeeTarget) return;
            if (_blackboard.IsReloading || _blackboard.CurrentAmmo <= 0) return;

            Vector2 selfPos = _self.position;
            float distance = Vector2.Distance(selfPos, playerPos);
            if (distance > _maxFireRange) return;

            Vector2 toTarget = (playerPos - selfPos).normalized;
            float angleDiff = Vector2.Angle(_self.up, toTarget);
            if (angleDiff > 15f) return; // ยังหันไม่ตรงพอ

            if (!Combat.AttackDecision.ShouldFire(_blackboard, selfPos, playerPos, _obstacleMask, _maxFireRange, true))
                return;

            _shoot.Tick(deltaTime);
            if (!_shoot.CanShoot) return;

            _shoot.Shoot(toTarget);
            _blackboard.CurrentAmmo--;
        }

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit()
        {
            // รีเซ็ตคะแนนจุดยืน — เข้ารอบหน้าให้คำนวณใหม่สด ๆ
            _currentStationScore = -1f;
        }
    }
}

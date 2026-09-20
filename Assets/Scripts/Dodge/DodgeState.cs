using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Utilities;
using TopDownTacticalAI.Tactical;
using TopDownTacticalAI.Combat;

namespace TopDownTacticalAI.Dodge
{
    /// <summary>
    /// State หลบกระสุน: ตรวจจับกระสุนที่พุ่งเข้ามา -> ใช้ Influence Map หาตำแหน่งที่ดีที่สุดจริงๆ
    /// (คำนึงถึงอันตรายจากกระสุนรอบตัวทั้งหมด + ที่กำบัง + โอกาสโจมตีสวนตามสถานะ Mana ผู้เล่น)
    /// แล้วเดินหลบไปตรงนั้น (ไม่ใช่ Dash แกว่งซ้ายขวา หรือแค่เบี่ยงตั้งฉากทิศเดียวแบบเดิม)
    /// เป็น State ที่มีความสำคัญสูงสุด (Priority สูงกว่า Combat/Cover) เมื่อมีกระสุนใกล้ตัว
    ///
    /// ระหว่างหลบจะ "หันหน้าเข้าหาผู้เล่นตลอด" (Strafe) ไม่ใช่หันตามทิศทางที่เดิน และถ้าหันตรงพอ +
    /// มองเห็นผู้เล่นจริง + มีกระสุน จะยิงสวนระหว่างหลบได้ด้วย เหมือนคนยิงปืนจริงๆ ที่หลบไปด้วยยิงไปด้วย
    ///
    /// อ้างอิงตามเอกสารบทที่ 3.1.3 และ 3.2: ใช้ FindBestPosition() ของ InfluenceMap
    /// แทนการเบี่ยงทิศทางตั้งฉากแบบง่ายๆ เพื่อให้ AI เลือกจุดหลบที่ "ฉลาด" กว่าเดิมจริงๆ
    /// </summary>
    public class DodgeState : IAIState
    {
        public EnemyState StateType => EnemyState.Dodge;

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly DodgeMovement _dodgeMovement;
        private readonly ShootController _shoot;
        private readonly LayerMask _bulletMask;
        private readonly LayerMask _obstacleMask;
        private readonly float _detectRadius;
        private readonly float _dodgeDistance;
        private readonly float _safetySearchRadius;
        private readonly float _influenceCellSize;
        private readonly float _attackUtilityBonus;
        private readonly float _maxFireRange;
        private readonly float _fireAngleTolerance;

        public DodgeState(
            Transform self,
            Blackboard blackboard,
            LayerMask bulletMask,
            LayerMask obstacleMask,
            ShootController shoot = null,
            float detectRadius = 2.5f,
            float dodgeSpeed = 4f,
            float dodgeDistance = 2f,
            float safetySearchRadius = 6f,
            float influenceCellSize = 0.75f,
            float attackUtilityBonus = 30f,
            float maxFireRange = 6f,
            float fireAngleTolerance = 12f)
        {
            _self = self;
            _blackboard = blackboard;
            _shoot = shoot;
            _bulletMask = bulletMask;
            _obstacleMask = obstacleMask;
            _detectRadius = detectRadius;
            _dodgeDistance = dodgeDistance;
            _safetySearchRadius = safetySearchRadius;
            _influenceCellSize = influenceCellSize;
            _attackUtilityBonus = attackUtilityBonus;
            _maxFireRange = maxFireRange;
            _fireAngleTolerance = fireAngleTolerance;
            _dodgeMovement = new DodgeMovement(self, obstacleMask, dodgeSpeed);
        }

        public void Enter()
        {
            if (BulletDetector.IsBulletIncoming(_self.position, _detectRadius, _bulletMask, out Vector2 vel, out Vector2 bulletPos))
            {
                // ── ตัวหลัก: DodgeDirectionScorer (§8/§18) ──
                // ตรวจครบ: ทำนายแนวกระสุน 3 จุดเวลา + ทางเดินไปจุดหลบติดกำแพงไหม
                //          + กำแพงที่จุดหลบ + เพื่อน + ระยะห่างผู้เล่น + ที่กำบัง
                Vector2 playerPos0 = _blackboard.CurrentTarget != null
                    ? (Vector2)_blackboard.CurrentTarget.position
                    : (Vector2)_self.position;

                bool scored = DodgeDirectionScorer.TryFindBestDodgeDirection(
                    _self.position, bulletPos, vel, _dodgeDistance,
                    _obstacleMask, playerPos0,
                    out Vector2 scoredDir, out float scoredScore);

                Vector2 candidate;
                if (scored)
                {
                    candidate = (Vector2)_self.position + scoredDir * _dodgeDistance;
                }
                else
                {
                    // Fallback: InfluenceMap (ค้นหาเป็นกริด) — ใช้เมื่อ scorer หาทิศไม่ได้เลย
                    candidate = FindDodgeDestinationViaInfluenceMap(vel, bulletPos);
                }

                Vector2 dodgeDir = (candidate - (Vector2)_self.position).normalized;

                _blackboard.DodgeDirection = dodgeDir;
                _blackboard.CurrentDestination = candidate;
                _dodgeMovement.StartDodgeTo(candidate);
            }
            _blackboard.IsDodging = true;
        }

        /// <summary>
        /// รวบรวมกระสุนทุกลูกที่อยู่ในระยะรอบตัวเป็น "แหล่งอันตราย" (Danger Source)
        /// แล้วให้ InfluenceMap หาช่องตารางที่คะแนนรวมดีที่สุด (อันตรายน้อย + ปลอดภัยสูง + โอกาสโจมตีถ้ามี)
        /// ถ้าหาไม่เจอเลย (เช่นโดนล้อมมุมอับ) จะ Fallback กลับไปใช้วิธีเบี่ยงตั้งฉากแบบเดิมแทน
        /// </summary>
        private Vector2 FindDodgeDestinationViaInfluenceMap(Vector2 incomingBulletVelocity, Vector2 bulletPosition)
        {
            Vector2 selfPos = _self.position;

            var dangerSources = new System.Collections.Generic.List<InfluenceMap.DangerSource>();
            Collider2D[] bullets = Physics2D.OverlapCircleAll(selfPos, _safetySearchRadius, _bulletMask);
            foreach (var bullet in bullets)
            {
                dangerSources.Add(new InfluenceMap.DangerSource(bullet.transform.position, _dodgeDistance * 1.5f, 100f));
            }

            Vector2 playerPosition = _blackboard.CurrentTarget != null ? (Vector2)_blackboard.CurrentTarget.position : selfPos;

            bool foundBest = InfluenceMap.FindBestPosition(
                selfPos,
                _safetySearchRadius,
                _influenceCellSize,
                dangerSources,
                _obstacleMask,
                playerPosition,
                _blackboard.PlayerManaLow,
                _attackUtilityBonus,
                out InfluenceMap.CellResult best);

            if (foundBest && Vector2.Distance(selfPos, best.WorldPosition) > 0.1f)
                return best.WorldPosition;

            // Fallback: ให้คะแนนทิศหลบเอง (§8/§18) เผื่อ Influence Map หาจุดไม่ได้เลย (เช่นโดนล้อมมุมอับ)
            // ไม่สุ่มซ้าย/ขวาแบบเดิมแล้ว — เลือกจากการทำนายแนวกระสุน + กำแพง + เพื่อน + ผู้เล่น + ที่กำบัง
            Vector2 playerPos = _blackboard.CurrentTarget != null
                ? (Vector2)_blackboard.CurrentTarget.position
                : selfPos;

            Vector2 scoredDir = SafePositionFinder.GetDodgeDirection(
                selfPos, bulletPosition, incomingBulletVelocity,
                _dodgeDistance, _obstacleMask, playerPos);

            if (scoredDir.sqrMagnitude > 0.0001f)
            {
                Vector2 scoredCandidate = selfPos + scoredDir * _dodgeDistance;
                if (!PhysicsUtility.IsPositionBlocked(scoredCandidate, 1.2f, _obstacleMask))
                    return scoredCandidate;
            }

            // ทุกทิศที่ให้คะแนนไปไม่ได้เลย → อยู่กับที่ (ดีกว่าหลบเข้ากำแพง/เข้ากระสุน)
            return selfPos;
        }

        public void Tick(float deltaTime)
        {
            // ส่งตำแหน่งผู้เล่นล่าสุดเข้าไปให้ DodgeMovement หันหน้าเข้าหาตลอดเวลาที่หลบ (Strafe)
            // ใช้ CurrentTarget ถ้ายังเห็นอยู่ ไม่งั้นใช้ตำแหน่งล่าสุดที่จำได้ (LastSeen) แทน
            Vector2? facePosition = null;
            if (_blackboard.CurrentTarget != null)
                facePosition = _blackboard.CurrentTarget.position;
            else if (_blackboard.LastSeen.IsValid)
                facePosition = _blackboard.LastSeen.Position;

            _dodgeMovement.Tick(deltaTime, facePosition);

            TryShootWhileDodging(facePosition);

            if (!_dodgeMovement.IsDodging)
            {
                _blackboard.IsDodging = false;
            }
        }

        /// <summary>
        /// ยิงสวนระหว่างหลบได้ ถ้าหันหน้าตรงพอ + เห็นผู้เล่นจริง (LOS) + มีกระสุน + ไม่ได้กำลังรีโหลด
        /// (ใช้เงื่อนไขเดียวกับ AttackDecision.ShouldFire แต่เช็คมุมหันหน้าเองแทน AimController
        /// เพราะการหมุนตัวตอนหลบคุมโดย DodgeMovement ไปแล้ว ไม่ต้องการให้สองระบบแย่งกันหมุน)
        /// </summary>
        private void TryShootWhileDodging(Vector2? facePosition)
        {
            if (_shoot == null || !facePosition.HasValue) return;
            if (!_blackboard.CanSeeTarget) return; // ต้องเห็นผู้เล่นจริงเท่านั้นถึงยิงได้ (เหมือนระบบอื่น)
            if (_blackboard.IsReloading || _blackboard.CurrentAmmo <= 0) return;

            Vector2 selfPos = _self.position;
            float distance = Vector2.Distance(selfPos, facePosition.Value);
            if (distance > _maxFireRange) return;

            Vector2 toTarget = (facePosition.Value - selfPos).normalized;
            float angleDiff = Vector2.Angle(_self.up, toTarget);
            if (angleDiff > _fireAngleTolerance) return; // ยังหันไม่ตรงพอ รอหันให้ตรงก่อนค่อยยิง

            if (!AttackDecision.ShouldFire(_blackboard, selfPos, facePosition.Value, _obstacleMask, _maxFireRange, true))
                return;

            _shoot.Tick(0f); // อัปเดตคูลดาวน์ (Tick หลักของ ShootController ทำใน CombatState ปกติ แต่ตอนหลบต้องเช็คเองด้วย)
            if (!_shoot.CanShoot) return;

            _shoot.Shoot(toTarget);
            _blackboard.CurrentAmmo--;
        }

        /// <summary>ใช้ให้ EnemyBrain เช็คภายนอกว่าควรเข้า State นี้หรือไม่ โดยไม่ต้อง Enter ก่อน</summary>
        public static bool DetectIncomingThreat(Vector2 origin, LayerMask bulletMask, float detectRadius)
        {
            bool incoming = BulletDetector.IsBulletIncoming(origin, detectRadius, bulletMask, out Vector2 bulletVel, out Vector2 bulletPos);
            return DodgeDecision.ShouldDodge(incoming, origin, bulletPos, bulletVel);
        }

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit()
        {
            _blackboard.IsDodging = false;
        }
    }
}

using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Combat;
using TopDownTacticalAI.Tactical;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Support
{
    /// <summary>
    /// Tank สกัด/ดึงดูดผู้เล่นที่กำลังไล่ตามเพื่อนที่หนีไปฮีล (§10, §21)
    ///
    /// สถานการณ์: Low HP Flanker → Retreat → Healer
    ///            Player → ไล่ตาม Flanker
    ///            Tank → เข้าไปขวางระหว่างผู้เล่นกับ Flanker
    ///
    /// เป้าหมายไม่ใช่ฆ่าผู้เล่นทันที แต่คือ:
    ///   1) ยืนขวาง (Body Block) — ตัดเส้นทางผู้เล่นไปหาเพื่อน
    ///   2) โจมตีผู้เล่น — บังคับให้ผู้เล่นเปลี่ยนเป้า (BAIT/PEEL)
    ///   3) เดินเข้าหาผู้เล่น — ดึงความสนใจ
    ///
    /// จุดขวางคำนวณแบบ dynamic: จุดกึ่งกลางระหว่างผู้เล่นกับเพื่อนที่หนี
    /// (ผู้เล่นขยับ → จุดขวางเลื่อนตาม ไม่ใช่ fixed position)
    /// </summary>
    public class PeelAllyState : IAIState
    {
        public EnemyState StateType => EnemyState.PeelAlly;

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly float _moveSpeed;
        private readonly LayerMask _obstacleMask;
        private readonly ShootController _shoot;
        private readonly float _maxFireRange;

        public PeelAllyState(
            Transform self,
            Blackboard blackboard,
            float moveSpeed,
            LayerMask obstacleMask,
            ShootController shoot = null,
            float maxFireRange = 6f)
        {
            _self = self;
            _blackboard = blackboard;
            _moveSpeed = moveSpeed;
            _obstacleMask = obstacleMask;
            _shoot = shoot;
            _maxFireRange = maxFireRange;
        }

        public void Enter() { }

        public void Tick(float deltaTime)
        {
            // เป้าที่กำลังถูกไล่ (ตั้งโดยทีมบัสใน EnemyBrain)
            Transform retreatingAlly = _blackboard.RetreatingAllyTransform;
            if (retreatingAlly == null) return; // เพื่อนตาย/หายไป — ให้ TacticalDecision ตัดสินใจใหม่

            Vector2 selfPos = _self.position;
            Vector2 allyPos = retreatingAlly.position;

            // ผู้เล่น ณ ตำแหน่งที่รับรู้ได้ (§50)
            Vector2 playerPos;
            if (_blackboard.CanSeeTarget && _blackboard.CurrentTarget != null)
                playerPos = _blackboard.CurrentTarget.position;
            else if (_blackboard.HasMemory)
                playerPos = _blackboard.LastSeen.Position;
            else return;

            // ── จุดขวาง: กึ่งกลางระหว่างผู้เล่นกับเพื่อนที่หนี (§21 — dynamic, ไม่ fixed) ──
            Vector2 blockPoint = Vector2.Lerp(allyPos, playerPos, 0.55f);
            _blackboard.CurrentDestination = blockPoint;

            // เดินไปจุดขวาง — ถ้าอยู่ใกล้จุดขวางพอแล้ว ให้ยืนกันผู้เล่นผ่าน (และยิงระหว่างยืน)
            float blockDistance = Vector2.Distance(selfPos, blockPoint);
            if (blockDistance > 0.8f)
            {
                Vector2 dir = (blockPoint - selfPos).normalized;
                Vector2 steered = SteeringMovement.GetSteeredDirection(
                    selfPos, dir, _obstacleMask, self: _self, personalSpace: 1.2f);

                if (steered.sqrMagnitude > 0.0001f)
                {
                    _self.position = SteeringMovement.MoveWithCollisionCheck(
                        _self, steered * (_moveSpeed * deltaTime), _obstacleMask);
                }
            }

            // §25 — Tank เดินบังแต่หันหน้าเข้าหาผู้เล่น
            Vector2 toPlayer = playerPos - selfPos;
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                float targetAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg - 90f;
                float currentAngle = _self.eulerAngles.z;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, 360f * deltaTime);
                _self.rotation = Quaternion.Euler(0, 0, newAngle);
            }

            // ยิงผู้เล่นได้ระหว่างสกัด (§10 — Tank can attack player)
            TryShootPlayer(playerPos, deltaTime);
        }

        /// <summary>ยิงได้เมื่อเห็นผู้เล่นจริง (LoS) + ระยะพอ + หันหน้าตรงพอ</summary>
        private void TryShootPlayer(Vector2 playerPos, float deltaTime)
        {
            if (_shoot == null) return;
            if (!_blackboard.CanSeeTarget) return;
            if (_blackboard.IsReloading || _blackboard.CurrentAmmo <= 0) return;

            Vector2 selfPos = _self.position;
            float distance = Vector2.Distance(selfPos, playerPos);
            if (distance > _maxFireRange) return;

            Vector2 toTarget = (playerPos - selfPos).normalized;
            float angleDiff = Vector2.Angle(_self.up, toTarget);
            if (angleDiff > 15f) return;

            if (!Combat.AttackDecision.ShouldFire(_blackboard, selfPos, playerPos, _obstacleMask, _maxFireRange, true))
                return;

            _shoot.Tick(deltaTime);
            if (!_shoot.CanShoot) return;

            _shoot.Shoot(toTarget);
            _blackboard.CurrentAmmo--;
        }

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit() { }
    }
}

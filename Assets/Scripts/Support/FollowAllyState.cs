using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Tactical;
using TopDownTacticalAI.Utilities;
using TopDownTacticalAI.Pathfinding;
using TopDownTacticalAI.Player; // Health

namespace TopDownTacticalAI.Support
{
    /// <summary>
    /// ฮีลเลอร์ "เดินตามเพื่อน" ตลอดเวลา (§4 — Support อยู่กับทีม เตรียมพร้อมฮีลทันที)
    ///
    /// ก่อนหน้านี้: ไม่มีคนบาดเจ็บ → ไป Patrol เดินมั่วห่างทีม เวลาเพื่อนโดนยิงต้องวิ่งข้ามแมพมาฮีล
    /// ตอนนี้: ไม่มีคนบาดเจ็บ → เดินตามเพื่อน (Tank เป็นหลัก ไม่มี tank = ตามตัวที่ใกล้ที่สุด)
    /// รักษาระยะติด ๆ ให้พร้อมฮีลทันทีที่มีคนบาดเจ็บ
    ///
    /// ขับเคลื่อนผ่าน AgentPathMover (NavMesh อ้อมกำแพง / steering fallback) จึงไม่ติดกำแพงเหมือนเดิม
    /// </summary>
    public class FollowAllyState : IAIState
    {
        public EnemyState StateType => EnemyState.FollowAlly;

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly AgentPathMover _mover;
        private readonly float _moveSpeed;
        private readonly LayerMask _obstacleMask;
        private readonly float _personalSpace;
        private readonly float _followDistance; // หยุดเมื่ออยู่ห่างเพื่อนเท่านี้ (หน่วยโลก)

        private Transform _buddy;
        private float _buddyRefreshTimer;
        private const float BuddyRefreshInterval = 0.5f; // เลือกเพื่อนที่จะตามใหม่ทุก 0.5 วิ

        public FollowAllyState(
            Transform self,
            Blackboard blackboard,
            AgentPathMover mover,
            float moveSpeed,
            LayerMask obstacleMask,
            float personalSpace = 1.3f,
            float followDistance = 6f)
        {
            _self = self;
            _blackboard = blackboard;
            _mover = mover;
            _moveSpeed = moveSpeed;
            _obstacleMask = obstacleMask;
            _personalSpace = personalSpace;
            _followDistance = followDistance;
        }

        public void Enter()
        {
            _buddy = null;
            _buddyRefreshTimer = 0f;
        }

        public void Tick(float deltaTime)
        {
            // เลือกเพื่อนที่จะตามใหม่เป็นระยะ — Tank (Defensive) ก่อน ไม่มี → ตัวที่ใกล้ที่สุด
            _buddyRefreshTimer -= deltaTime;
            if (_buddyRefreshTimer <= 0f || !IsBuddyValid(_buddy))
            {
                _buddyRefreshTimer = BuddyRefreshInterval;
                _buddy = PickBuddy();
            }

            // ไม่มีเพื่อนเหลือเลย (ทีมตายหมด) → ยืนนิ่งรอ (TacticalDecision จะจัดการเองเฟรมหน้า)
            if (_buddy == null)
            {
                _blackboard.CurrentDestination = _self.position;
                return;
            }

            Vector2 selfPos = _self.position;
            Vector2 buddyPos = _buddy.position;
            _blackboard.CurrentDestination = buddyPos;

            float distance = Vector2.Distance(selfPos, buddyPos);

            // อยู่ในระยะตามแล้ว → หยุดนิ่ง (ไม่ต้องเดินวนให้เปลือง)
            if (distance <= _followDistance)
            {
                FaceThreatOrIdle(deltaTime);
                return;
            }

            // เดินตามเพื่อนผ่านระบบนำทาง Hybrid (NavMesh อ้อมกำแพง / steering fallback)
            if (_mover != null)
            {
                // arriveThreshold = ระยะตาม — agent จะหยุดเองเมื่อถึงระยะจากเพื่อน
                _mover.MoveTo(buddyPos, deltaTime, _moveSpeed, arriveThreshold: _followDistance);
            }
            else
            {
                Vector2 dir = (buddyPos - selfPos).normalized;
                Vector2 steered = SteeringMovement.GetSteeredDirection(
                    selfPos, dir, _obstacleMask, self: _self, personalSpace: _personalSpace);
                if (steered.sqrMagnitude > 0.0001f)
                    _self.position = SteeringMovement.MoveWithCollisionCheck(
                        _self, steered * (_moveSpeed * deltaTime), _obstacleMask);
            }

            FaceThreatOrIdle(deltaTime);
        }

        /// <summary>
        /// เลือกเพื่อนที่จะตาม: เน้น Tank (Defensive) ก่อน เพราะเป็นแกนของทีม
        /// ไม่มี Tank ที่ยังมีชีวิต → เพื่อนที่ใกล้ที่สุด (ไม่รวมตัวเองและคนตาย)
        /// </summary>
        private Transform PickBuddy()
        {
            if (GroupAI.Instance == null) return null;

            Transform nearestTank = null;
            float nearestTankDist = float.MaxValue;
            Transform nearestAlly = null;
            float nearestAllyDist = float.MaxValue;

            foreach (var ally in GroupAI.Instance.ActiveEnemies)
            {
                if (ally == null || ally == _self) continue;
                if (!ally.TryGetComponent(out Health health)) continue;
                if (health.IsDead || health.CurrentHP <= 0f) continue;

                float dist = Vector2.Distance(_self.position, ally.position);

                if (nearestAlly == null || dist < nearestAllyDist)
                {
                    nearestAlly = ally;
                    nearestAllyDist = dist;
                }

                if (ally.TryGetComponent(out EnemyBrain brain) && brain.Role == EnemyRole.Defensive)
                {
                    if (nearestTank == null || dist < nearestTankDist)
                    {
                        nearestTank = ally;
                        nearestTankDist = dist;
                    }
                }
            }

            return nearestTank != null ? nearestTank : nearestAlly;
        }

        private static bool IsBuddyValid(Transform buddy)
        {
            if (buddy == null) return false; // Unity null = ถูกทำลาย
            if (buddy.TryGetComponent(out Health h) && (h.IsDead || h.CurrentHP <= 0f)) return false;
            return true;
        }

        /// <summary>
        /// หันหน้า: ถ้าเห็นผู้เล่น → หันเตรียมพร้อมรับมือ (เหมือน §25 ของ Flank)
        /// ไม่เห็น → คงทิศเดิมไว้ (ไม่ต้องหันตามทิศเดิน ให้ดูเป็นธรรมชาติแบบยามระวังตัว)
        /// </summary>
        private void FaceThreatOrIdle(float deltaTime)
        {
            if (!_blackboard.CanSeeTarget || _blackboard.CurrentTarget == null) return;

            Vector2 toPlayer = (Vector2)_blackboard.CurrentTarget.position - (Vector2)_self.position;
            if (toPlayer.sqrMagnitude < 0.0001f) return;

            float targetAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg - 90f;
            float newAngle = Mathf.MoveTowardsAngle(_self.eulerAngles.z, targetAngle, 360f * deltaTime);
            _self.rotation = Quaternion.Euler(0, 0, newAngle);
        }

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit() { }
    }
}

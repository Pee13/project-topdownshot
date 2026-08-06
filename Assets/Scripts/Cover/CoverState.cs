using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Combat;

namespace TopDownTacticalAI.Cover
{
    /// <summary>
    /// State เข้าที่กำบัง: วิ่งเข้าไป -> หลบอยู่ -> โผล่ออกมายิงเป็นจังหวะ (Peek) -> กลับเข้าที่กำบัง
    /// หมายเหตุ: ไม่ได้ระบุใน IAIState enum เดิม (EnemyState.Cover มีอยู่แล้ว) จึงลงทะเบียนใน StateMachine ได้ปกติ
    /// </summary>
    public class CoverState : IAIState
    {
        public EnemyState StateType => EnemyState.Cover;

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly RetreatSystem _retreat;
        private readonly PeekSystem _peek;
        private readonly AimController _aim;
        private readonly ShootController _shoot;
        private readonly LayerMask _obstacleMask;
        private readonly float _maxRange;

        private bool _arrivedAtCover;

        public CoverState(Transform self, Blackboard blackboard, float moveSpeed, AimController aim, ShootController shoot, LayerMask obstacleMask, float maxRange = 6f)
        {
            _self = self;
            _blackboard = blackboard;
            _retreat = new RetreatSystem(self, moveSpeed, obstacleMask);
            _peek = new PeekSystem();
            _aim = aim;
            _shoot = shoot;
            _obstacleMask = obstacleMask;
            _maxRange = maxRange;
        }

        public void Enter()
        {
            _arrivedAtCover = false;
            _blackboard.InCover = false;
        }

        public void Tick(float deltaTime)
        {
            if (_blackboard.CurrentCover == null || _blackboard.CurrentTarget == null)
                return;

            Vector2 coverPos = _blackboard.CurrentCover.position;
            _blackboard.CurrentDestination = coverPos;

            if (!_arrivedAtCover)
            {
                _arrivedAtCover = _retreat.MoveToCover(coverPos, deltaTime);
                if (_arrivedAtCover)
                    _blackboard.InCover = true;
                return;
            }

            _peek.Tick(deltaTime);
            _blackboard.IsPeeking = _peek.IsPeeking;

            if (_peek.IsPeeking)
            {
                Vector2 targetPos = _blackboard.CurrentTarget.position;
                _aim.AimAt(targetPos, deltaTime);
                _shoot.Tick(deltaTime);

                if (_aim.IsAimedAt(targetPos) && AttackDecision.ShouldFire(_blackboard, _self.position, targetPos, _obstacleMask, _maxRange, true))
                {
                    Vector2 fireDir = (targetPos - (Vector2)_self.position).normalized;
                    _shoot.Shoot(fireDir);
                    _blackboard.CurrentAmmo--;
                }
            }
            else
            {
                _peek.TryStartPeek();
            }
        }

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit()
        {
            _blackboard.InCover = false;
            _blackboard.IsPeeking = false;

            // ปล่อยจุดกำบังคืนทุกครั้งที่ออกจาก Cover State (ไม่ใช่แค่ตอน Enemy ถูกทำลาย)
            // ถ้าไม่ปล่อยตรงนี้ จุดกำบังนี้จะค้างสถานะ "มีคนใช้" ตลอดไป ทำให้ตัวอื่นใช้จุดนี้ไม่ได้อีกเลย
            if (_blackboard.CurrentCover != null && _blackboard.CurrentCover.TryGetComponent(out CoverPoint cp))
            {
                cp.Release();
            }
            _blackboard.CurrentCover = null;
        }
    }
}

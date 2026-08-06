using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Tactical;

namespace TopDownTacticalAI.Combat
{
    /// <summary>
    /// State การต่อสู้: เล็ง, ยิง, รีโหลด
    /// ตัดสินใจเว้นระยะ/ถอยเมื่อเป้าหมายใกล้เกินไป (ส่งสัญญาณให้ EnemyBrain ตัดสินใจ Cover/Dodge ต่อ)
    /// ยิงได้ก็ต่อเมื่อมองเห็นผู้เล่นจริง (Line of Sight) เท่านั้น ไม่ใช่แค่ดูระยะ/เลเยอร์
    /// มีแรงผลักกันพันธมิตรตัวอื่นไว้ด้วย กันศัตรูหลายตัวเข้ามารุมยิงชิดกันเกินไป
    /// </summary>
    public class CombatState : IAIState
    {
        public EnemyState StateType => EnemyState.Combat;

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly AimController _aim;
        private readonly ShootController _shoot;
        private readonly ReloadController _reload;
        private readonly LayerMask _obstacleMask;
        private readonly float _maxRange;
        private readonly float _preferredMinRange;
        private readonly float _retreatSpeed;
        private readonly float _personalSpace;

        public CombatState(Transform self, Blackboard blackboard, AimController aim, ShootController shoot, ReloadController reload, LayerMask obstacleMask, float maxRange = 6f, float preferredMinRange = 2.5f, float retreatSpeed = 1.5f, float personalSpace = 1.3f)
        {
            _self = self;
            _blackboard = blackboard;
            _aim = aim;
            _shoot = shoot;
            _reload = reload;
            _obstacleMask = obstacleMask;
            _maxRange = maxRange;
            _preferredMinRange = preferredMinRange;
            _retreatSpeed = retreatSpeed;
            _personalSpace = personalSpace;
        }

        public void Enter() { }

        public void Tick(float deltaTime)
        {
            if (_blackboard.CurrentTarget == null) return;

            Vector2 targetPos = _blackboard.CurrentTarget.position;
            _blackboard.CurrentDestination = targetPos;

            _shoot.Tick(deltaTime);
            _reload.Tick(deltaTime);

            if (_reload.NeedsReload && !_blackboard.IsReloading)
            {
                _reload.StartReload();
                return;
            }

            // ยังเล็งได้แม้มองไม่เห็นชั่วขณะ (เดาทิศจากตำแหน่งล่าสุดที่รู้) แต่จะ "ยิง" ไม่ได้ถ้าไม่เห็นจริง
            _aim.AimAt(targetPos, deltaTime);
            bool aimed = _aim.IsAimedAt(targetPos);

            // เว้นระยะ: ถ้าเป้าหมายใกล้เกินไป ให้ถอยออกขณะยังเล็งอยู่ (ผสมแรงผลักจากพันธมิตรตัวอื่นด้วย)
            float distance = Vector2.Distance(_self.position, targetPos);
            if (distance < _preferredMinRange)
            {
                Vector2 retreatDir = ((Vector2)_self.position - targetPos).normalized;

                if (GroupAI.Instance != null)
                {
                    Vector2 separationPush = GroupAI.Instance.ComputeSeparationPush(_self.position, _self, _personalSpace);
                    Vector2 blended = (retreatDir + separationPush).normalized;
                    if (blended.sqrMagnitude > 0.0001f)
                        retreatDir = blended;
                }

                _self.position = (Vector2)_self.position + retreatDir * _retreatSpeed * deltaTime;
            }

            if (AttackDecision.ShouldFire(_blackboard, _self.position, targetPos, _obstacleMask, _maxRange, aimed))
            {
                Vector2 fireDir = (targetPos - (Vector2)_self.position).normalized;
                _shoot.Shoot(fireDir);
                _blackboard.CurrentAmmo--;
            }
        }

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit() { }
    }
}

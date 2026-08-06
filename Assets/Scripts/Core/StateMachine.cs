using System.Collections.Generic;
using UnityEngine;

namespace TopDownTacticalAI.Core
{
    /// <summary>
    /// ตัวควบคุมการสลับ State ของ AI แต่ละตัว
    /// เก็บ State ทั้งหมดไว้ใน Dictionary แล้วสลับตาม EnemyState ที่ร้องขอ
    /// </summary>
    public class StateMachine
    {
        private readonly Dictionary<EnemyState, IAIState> _states = new Dictionary<EnemyState, IAIState>();
        public IAIState CurrentState { get; private set; }
        public EnemyState CurrentStateType { get; private set; }

        public void RegisterState(IAIState state)
        {
            _states[state.StateType] = state;
        }

        public void ChangeState(EnemyState newState)
        {
            if (!_states.ContainsKey(newState))
            {
                Debug.LogWarning($"[StateMachine] ไม่พบ State: {newState}");
                return;
            }

            if (CurrentState != null && CurrentStateType == newState)
                return; // อยู่ State เดิมอยู่แล้ว ไม่ต้องสลับซ้ำ

            CurrentState?.Exit();
            CurrentState = _states[newState];
            CurrentStateType = newState;
            CurrentState.Enter();
        }

        public void Tick(float deltaTime)
        {
            CurrentState?.Tick(deltaTime);
        }

        public void FixedTick(float fixedDeltaTime)
        {
            CurrentState?.FixedTick(fixedDeltaTime);
        }
    }
}

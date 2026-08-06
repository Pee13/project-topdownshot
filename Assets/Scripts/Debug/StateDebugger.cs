using UnityEngine;
using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.DebugTools
{
    /// <summary>
    /// แสดง State ปัจจุบันของศัตรูเป็นข้อความลอยเหนือหัว (World Space) เพื่อ Debug
    /// </summary>
    public class StateDebugger : MonoBehaviour
    {
        public bool ShowInGameView = true;
        private EnemyState _currentState;

        public void SetState(EnemyState state)
        {
            _currentState = state;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!ShowInGameView) return;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, _currentState.ToString());
        }
#endif
    }
}

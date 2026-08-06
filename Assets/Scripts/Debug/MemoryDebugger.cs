using UnityEngine;
using TopDownTacticalAI.Memory;

namespace TopDownTacticalAI.DebugTools
{
    /// <summary>
    /// วาดตำแหน่ง Last Seen ของ AI ใน Scene View
    /// </summary>
    public class MemoryDebugger : MonoBehaviour
    {
        public EnemyMemory TargetMemoryComponent;

        private void OnDrawGizmos()
        {
            if (TargetMemoryComponent == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.4f);
        }
    }
}

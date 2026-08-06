using UnityEngine;
using TopDownTacticalAI.Cover;

namespace TopDownTacticalAI.DebugTools
{
    /// <summary>
    /// วาดจุด Cover ทั้งหมดในฉากพร้อมสีบอกสถานะ (เขียว = ว่าง, แดง = มีคนใช้)
    /// แปะ Component นี้ไว้ที่ CoverPoint object เพื่อ Debug รายจุด
    /// </summary>
    [RequireComponent(typeof(CoverPoint))]
    public class CoverDebugger : MonoBehaviour
    {
        private CoverPoint _cover;

        private void Awake()
        {
            _cover = GetComponent<CoverPoint>();
        }

        private void OnDrawGizmos()
        {
            if (_cover == null) _cover = GetComponent<CoverPoint>();
            Gizmos.color = _cover.IsOccupied ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)_cover.FacingDirection);
        }
    }
}

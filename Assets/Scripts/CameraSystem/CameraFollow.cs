using UnityEngine;

namespace TopDownTacticalAI.CameraSystem
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("ลาก Player 1 มาใส่ช่องนี้")]
        public Transform target;

        [Header("Smooth Settings")]
        [Tooltip("ยิ่งค่าน้อยยิ่งตามเร็ว แนะนำ 0.15 - 0.25")]
        [Range(0.05f, 0.5f)]
        public float smoothTime = 0.2f;

        [Header("Offset")]
        [Tooltip("ระยะห่างของกล้อง (แกน Z ต้องเป็น -10 เสมอ)")]
        public Vector3 offset = new Vector3(0f, 0f, -10f);

        // ตัวแปรความเร็วภายใน ห้ามสร้างใหม่ใน LateUpdate
        private Vector3 _currentVelocity = Vector3.zero;

        private void Start()
        {
            // Fail-safe ช่วยหาผู้เล่นให้อัตโนมัติเผื่อลืมลากใส่
            if (target == null)
            {
                GameObject player = GameObject.Find("Player 1");
                if (player == null) player = GameObject.FindWithTag("Player");
                if (player != null) target = player.transform;
            }

            // จัดตำแหน่งเริ่มต้นให้ตรงกับเป้าหมายทันทีตอนเปิดเกม ไม่ต้องให้ลอยวิ่งมาจากจุดไกล
            if (target != null)
            {
                transform.position = new Vector3(target.position.x + offset.x, target.position.y + offset.y, offset.z);
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 1. คำนวณพิกัดเป้าหมาย (ล็อก Z ไว้ที่ -10 เสมอสำหรับเกม 2D)
            Vector3 targetPosition = new Vector3(
                target.position.x + offset.x, 
                target.position.y + offset.y, 
                offset.z
            );

            // 2. เคลื่อนที่ด้วยความหน่วงนุ่มนวลระดับเฟรม
            transform.position = Vector3.SmoothDamp(
                transform.position, 
                targetPosition, 
                ref _currentVelocity, 
                smoothTime
            );
        }
    }
}
using UnityEngine;

namespace TopDownTacticalAI.CameraSystem
{
    /// <summary>
    /// กล้องตามผู้เล่นแบบนุ่มนวล (Smooth Follow) สำหรับเกม Top-down 2D
    /// วิธีใช้: แปะสคริปต์นี้ไว้ที่ Main Camera แล้วลาก Transform ผู้เล่นมาใส่ช่อง Target
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform ที่ต้องการให้กล้องตาม (ปกติคือผู้เล่น)")]
        public Transform Target;

        [Header("Follow Settings")]
        [Tooltip("ยิ่งน้อยยิ่งตามติดเร็ว ยิ่งมากยิ่งตามนุ่มนวล/หน่วงกว่า")]
        public float SmoothTime = 0.15f;

        [Tooltip("ระยะออฟเซ็ตจากตัวผู้เล่น (ปกติ Z ควรเป็นค่าลบเพื่อให้กล้องอยู่หลังฉาก)")]
        public Vector3 Offset = new Vector3(0f, 0f, -10f);

        [Header("Boundary (ทางเลือก)")]
        [Tooltip("ถ้าต้องการจำกัดขอบเขตกล้องไม่ให้ออกนอกฉาก ให้ติ๊กเปิดแล้วตั้งค่าขอบเขตด้านล่าง")]
        public bool UseBounds = false;
        public Vector2 MinBounds;
        public Vector2 MaxBounds;

        private Vector3 _velocity = Vector3.zero;

        private void LateUpdate()
        {
            if (Target == null) return;

            Vector3 desiredPosition = Target.position + Offset;

            if (UseBounds)
            {
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, MinBounds.x, MaxBounds.x);
                desiredPosition.y = Mathf.Clamp(desiredPosition.y, MinBounds.y, MaxBounds.y);
            }

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, SmoothTime);
        }
    }
}

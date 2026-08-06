using UnityEngine;

namespace TopDownTacticalAI.Cover
{
    /// <summary>
    /// Component ติดไว้กับ GameObject ที่ทำหน้าที่เป็นจุดกำบังในฉาก
    /// วาง Object เปล่าๆ ที่ต้องการให้เป็นที่กำบัง แล้วแปะ Component นี้ + Collider (isTrigger)
    /// </summary>
    public class CoverPoint : MonoBehaviour
    {
        public bool IsOccupied { get; private set; }
        [Tooltip("ทิศทางที่หันเข้าหาศัตรู/แนวยิง เพื่อใช้ตอน Peek")]
        public Vector2 FacingDirection = Vector2.up;

        public void Occupy() => IsOccupied = true;
        public void Release() => IsOccupied = false;
    }
}

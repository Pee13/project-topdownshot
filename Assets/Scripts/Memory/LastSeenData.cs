using UnityEngine;

namespace TopDownTacticalAI.Memory
{
    /// <summary>
    /// ข้อมูลตำแหน่ง/เวลา/ทิศทางล่าสุดของผู้เล่นที่ AI เคยเห็น
    /// </summary>
    [System.Serializable]
    public class LastSeenData
    {
        public Vector2 Position;
        public Vector2 Direction;
        public float TimeSeen;
        public bool IsValid;

        public void Record(Vector2 position, Vector2 direction, float time)
        {
            Position = position;
            Direction = direction;
            TimeSeen = time;
            IsValid = true;
        }

        public void Clear()
        {
            IsValid = false;
        }
    }
}

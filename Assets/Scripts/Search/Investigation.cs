using UnityEngine;

namespace TopDownTacticalAI.Search
{
    /// <summary>
    /// เดินตรงไปยังตำแหน่งล่าสุดที่จำได้ (Last Seen Position)
    /// </summary>
    public class Investigation
    {
        public bool HasArrived(Vector2 currentPosition, Vector2 lastSeenPosition, float threshold = 0.3f)
        {
            return Vector2.Distance(currentPosition, lastSeenPosition) <= threshold;
        }

        public Vector2 GetDirection(Vector2 currentPosition, Vector2 lastSeenPosition)
        {
            return (lastSeenPosition - currentPosition).normalized;
        }
    }
}

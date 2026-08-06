using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.Tactical
{
    /// <summary>
    /// จัดลำดับความสำคัญของ State ที่ควรอยู่ในตอนนี้
    /// ลำดับ: Dodge (สูงสุด) > Cover (เมื่อเสี่ยง) > Combat > Chase > Suspicious > Search > Patrol (ต่ำสุด)
    /// </summary>
    public static class PrioritySystem
    {
        public static int GetPriority(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Dodge: return 6;
                case EnemyState.Cover: return 5;
                case EnemyState.Combat: return 4;
                case EnemyState.Chase: return 3;
                case EnemyState.Suspicious: return 2;
                case EnemyState.Search: return 1;
                case EnemyState.Patrol: return 0;
                default: return 0;
            }
        }

        /// <summary>คืนค่า true ถ้า candidate มีความสำคัญสูงกว่า current และควรสลับ</summary>
        public static bool ShouldOverride(EnemyState current, EnemyState candidate)
        {
            return GetPriority(candidate) > GetPriority(current);
        }
    }
}

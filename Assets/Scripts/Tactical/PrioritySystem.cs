using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.Tactical
{
    /// <summary>
    /// จัดลำดับความสำคัญของ State ที่ควรอยู่ในตอนนี้
    ///
    /// ลำดับ: Dodge (สูงสุด) > Retreat / SeekHeal / HealAlly / Cover > Combat > Chase > Suspicious > Search > Patrol
    ///
    /// เหตุผล:
    ///   - Dodge สูงสุด เพราะเป็นการเอาตัวรอดจากกระสุนที่กำลังจะโดน "ตอนนี้"
    ///   - Retreat / SeekHeal สูงเท่ากับ Cover เพราะเป็นการเอาตัวรอดเช่นกัน (§18/§19)
    ///     แต่ไม่สูงกว่า Dodge เพราะถ้ากำลังจะโดนกระสุน ต้องหลบก่อนแล้วค่อยเดินต่อ
    ///   - HealAlly สูงเท่ากับ Cover เพราะเป็นหน้าที่หลักของ Healer (§4)
    ///     แต่ความปลอดภัยของตัว Healer เองถูกจัดการโดย Critical Override ใน AIActionScorer แทน
    /// </summary>
    public static class PrioritySystem
    {
        public static int GetPriority(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Dodge: return 6;
                case EnemyState.Retreat: return 5;
                case EnemyState.SeekHeal: return 5;
                case EnemyState.HealAlly: return 5;
                case EnemyState.Cover: return 5;
                case EnemyState.ProtectHealer: return 5; // Tank — หน้าที่หลัก (§2 priority 1)
                case EnemyState.PeelAlly: return 5;      // Tank — ช่วยเพื่อนที่ถูกไล่ (§2 priority 2)
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

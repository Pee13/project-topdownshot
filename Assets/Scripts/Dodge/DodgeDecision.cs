namespace TopDownTacticalAI.Dodge
{
    /// <summary>
    /// ตัดสินใจว่าควรหลบหรือไม่ (มีโอกาสพลาดบ้างตาม Skill/Difficulty เพื่อไม่ให้ AI เก่งเกินจริง)
    /// </summary>
    public static class DodgeDecision
    {
        public static bool ShouldDodge(bool bulletIncoming, float dodgeChance = 0.7f)
        {
            if (!bulletIncoming) return false;
            return UnityEngine.Random.value <= dodgeChance;
        }
    }
}

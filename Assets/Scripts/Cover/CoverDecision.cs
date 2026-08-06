using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.Cover
{
    /// <summary>
    /// ตัดสินใจว่าควรเข้าที่กำบังหรือไม่ โดยพิจารณา HP, กระสุน, และระยะเป้าหมาย
    /// </summary>
    public static class CoverDecision
    {
        public static bool ShouldSeekCover(Blackboard blackboard, float distanceToTarget, float dangerRange)
        {
            if (blackboard.InCover) return false;

            bool lowHp = blackboard.IsLowHP;
            bool lowAmmo = blackboard.CurrentAmmo <= 0 || blackboard.IsReloading;
            bool tooClose = distanceToTarget <= dangerRange;

            return lowHp || lowAmmo || tooClose;
        }
    }
}

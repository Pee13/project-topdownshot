namespace TopDownTacticalAI.Core
{
    /// <summary>
    /// สถานะหลักทั้งหมดที่ AI สามารถอยู่ได้
    /// </summary>
    public enum EnemyState
    {
        Patrol,
        Suspicious,
        Chase,
        Search,
        Combat,
        Cover,
        Dodge,
        FollowTank
    }
}

namespace TopDownTacticalAI.Search
{
    /// <summary>
    /// สัญญาณบอกว่าการค้นหาจบแล้วและถึงเวลากลับไป Patrol
    /// (ไม่มี Logic ซับซ้อน เป็น Marker/Helper ให้ SearchState ใช้ตัดสินใจ)
    /// </summary>
    public static class ReturnPatrol
    {
        public static bool ShouldReturnToPatrol(bool searchPatternFinished, bool targetFound)
        {
            return searchPatternFinished && !targetFound;
        }
    }
}

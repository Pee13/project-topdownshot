namespace TopDownTacticalAI.Core
{
    /// <summary>
    /// Interface กลางที่ทุก State ต้อง implement
    /// ใช้ร่วมกับ StateMachine เพื่อควบคุมพฤติกรรม AI
    /// </summary>
    public interface IAIState
    {
        EnemyState StateType { get; }

        /// <summary>เรียกครั้งเดียวตอนเข้า State</summary>
        void Enter();

        /// <summary>เรียกทุกเฟรมขณะอยู่ใน State</summary>
        void Tick(float deltaTime);

        /// <summary>เรียกทุก FixedUpdate ถ้า State ต้องใช้ Physics</summary>
        void FixedTick(float fixedDeltaTime);

        /// <summary>เรียกครั้งเดียวตอนออกจาก State</summary>
        void Exit();
    }
}

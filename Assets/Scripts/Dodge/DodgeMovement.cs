using UnityEngine;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Dodge
{
    /// <summary>
    /// เดินหลบไปยัง "ตำแหน่งปลอดภัย" จริงๆ ตำแหน่งเดียว (คำนวณครั้งเดียวตอนเริ่มหลบ)
    /// แทนที่จะเป็นการ Dash แกว่งซ้าย-ขวาแบบเดิม ซึ่งดูเหมือนเต้นส่าย และมีโอกาสพุ่งทะลุกำแพงได้ง่าย
    /// ใช้ SteeringMovement (Whisker Raycast) เดินไปหาจุดหมายเหมือนระบบเคลื่อนที่อื่นๆ ในเกม
    /// จึงเลี่ยงกำแพงได้เป็นธรรมชาติ และหยุดเองถ้าติดทางตันแทนที่จะฝืนพุ่งทะลุ
    ///
    /// สำคัญ: การหมุนตัวระหว่างหลบจะ "หันเข้าหาผู้เล่นตลอด" (ถ้าระบุ facePosition มา) แบบ Strafe
    /// ไม่ใช่หันตามทิศทางที่กำลังเดิน — ทำให้ AI มองหน้า+เล็งผู้เล่นได้ตลอดเวลาที่หลบ เหมือนคนยิงปืนจริงๆ
    /// ที่เดินหลบข้างๆ แต่ตายังจ้องเป้าหมายอยู่ ไม่ใช่หันหลังให้ระหว่างหลบแล้วค่อยหันกลับทีหลัง
    /// </summary>
    public class DodgeMovement
    {
        private readonly Transform _self;
        private readonly LayerMask _obstacleMask;
        private readonly float _moveSpeed;
        private readonly float _maxDuration;
        private readonly float _turnSpeed;
        private float _elapsed;
        private Vector2 _destination;

        public bool IsDodging { get; private set; }

        public DodgeMovement(Transform self, LayerMask obstacleMask, float moveSpeed = 4f, float maxDuration = 0.6f, float turnSpeed = 720f)
        {
            _self = self;
            _obstacleMask = obstacleMask;
            _moveSpeed = moveSpeed;
            _maxDuration = maxDuration;
            _turnSpeed = turnSpeed;
        }

        /// <summary>เริ่มหลบไปยังตำแหน่งเป้าหมายที่คำนวณไว้แล้ว (ดูจาก SafePositionFinder)</summary>
        public void StartDodgeTo(Vector2 destination)
        {
            _destination = destination;
            _elapsed = 0f;
            IsDodging = true;
        }

        /// <param name="facePosition">
        /// ตำแหน่งที่อยากให้หันหน้าไปหาตลอดเวลาที่หลบ (ปกติคือตำแหน่งผู้เล่น) เพื่อให้เล็ง/ยิงระหว่างหลบได้
        /// ถ้าไม่ระบุ (null) จะหันตามทิศทางที่กำลังเดินแทนแบบเดิม
        /// </param>
        public void Tick(float deltaTime, Vector2? facePosition = null)
        {
            if (!IsDodging) return;

            Vector2 current = _self.position;
            float distanceToTarget = Vector2.Distance(current, _destination);

            // ถึงจุดหมายแล้ว หรือใช้เวลาเกินกำหนด (กันเคสจุดหมายไปไม่ถึงจริงๆ) ให้จบการหลบ
            if (distanceToTarget <= 0.15f || _elapsed >= _maxDuration)
            {
                IsDodging = false;
                return;
            }

            Vector2 desiredDir = (_destination - current).normalized;
            Vector2 steeredDir = SteeringMovement.GetSteeredDirection(current, desiredDir, _obstacleMask, self: _self);

            _self.position = current + steeredDir * _moveSpeed * deltaTime;

            if (facePosition.HasValue)
            {
                // หันหน้าเข้าหาผู้เล่นตลอด (Strafe) แบบนุ่มนวลด้วย MoveTowardsAngle ไม่ใช่สแนปทันที
                Vector2 toFaceTarget = facePosition.Value - current;
                if (toFaceTarget.sqrMagnitude > 0.0001f)
                {
                    float targetAngle = Mathf.Atan2(toFaceTarget.y, toFaceTarget.x) * Mathf.Rad2Deg - 90f;
                    float currentAngle = _self.eulerAngles.z;
                    float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, _turnSpeed * deltaTime);
                    _self.rotation = Quaternion.Euler(0, 0, newAngle);
                }
            }
            else if (steeredDir.sqrMagnitude > 0.001f)
            {
                // ไม่ได้ระบุตำแหน่งที่จะหันมองไว้ -> หันตามทิศทางที่เดินแบบเดิม
                float angle = Mathf.Atan2(steeredDir.y, steeredDir.x) * Mathf.Rad2Deg - 90f;
                _self.rotation = Quaternion.Euler(0, 0, angle);
            }

            _elapsed += deltaTime;
        }
    }
}

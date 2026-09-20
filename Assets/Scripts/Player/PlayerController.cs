using UnityEngine;
using TopDownTacticalAI.Map;
using TopDownTacticalAI.Tactical;

namespace TopDownTacticalAI.Player
{
    /// <summary>
    /// ควบคุมการเคลื่อนที่ของผู้เล่นด้วย WASD/Arrow Keys แบบ Top-down
    /// กด Shift ค้างเพื่อวิ่ง (เร็วขึ้น แต่เสียงดังกว่า ศัตรูได้ยินจากระยะไกลขึ้น)
    /// กด Space เพื่อ Dash พุ่งไปทางทิศที่กำลังเดินอยู่ (หรือทิศที่หันหน้าถ้ายืนนิ่ง)
    /// ต้องมี Rigidbody2D (Body Type: Dynamic, Gravity Scale: 0)
    /// ถ้ามี MapBounds อยู่ในฉาก จะไม่สามารถเดินหลุดออกนอกกรอบแมพได้
    ///
    /// Dash แบบ ULTRAKILL: จำกัดด้วย "ระยะทาง" (Dash Distance) ไม่ใช่เวลา
    /// พุ่งจากจุด A ไปจุด B ด้วยความเร็วคงที่สูง พอถึงระยะที่กำหนดแล้ว (หรือชนกำแพงก่อน)
    /// จะกลับสู่ความเร็วเดินปกติทันที ไม่ใช่ค่อยๆ ชะลอ
    ///
    /// หมายเหตุสำคัญ: ตัวจับเวลา/ระยะทางและการเคลื่อนที่ของ Dash ทั้งหมดอยู่ใน FixedUpdate() ที่เดียวเท่านั้น
    /// (ไม่แยกไปทำใน Update()) เพื่อกันปัญหาจังหวะ Update/FixedUpdate ไม่ตรงกันจนตัวจับเวลาไม่ทำงาน
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("ความเร็ว")]
        [Tooltip("ความเร็วพื้นฐาน (ระบบ Equipment/Item สามารถปรับค่านี้ตรงๆ ได้ เช่น รองเท้าเพิ่มความเร็ว)")]
        public float MoveSpeed = 4f;
        [Tooltip("ตัวคูณความเร็วตอนกด Shift วิ่ง (ความเร็วจริง = MoveSpeed x ตัวนี้)")]
        public float RunSpeedMultiplier = 1.75f;

        [Header("Dash (แบบ ULTRAKILL — จำกัดด้วยระยะทาง ไม่ใช่เวลา)")]
        public KeyCode DashKey = KeyCode.Space;
        [Tooltip("ความเร็วขณะ Dash (World Units/วินาที) — ยิ่งสูงยิ่งถึงจุดหมายไว")]
        public float DashSpeed = 20f;
        [Tooltip("ระยะทางที่ Dash จะพุ่งไปจากจุดเริ่ม (World Units) พอถึงระยะนี้จะกลับสู่ความเร็วปกติทันที")]
        public float DashDistance = 1.5f;
        [Tooltip("เวลาคูลดาวน์ก่อน Dash ได้อีกครั้ง (วินาที)")]
        public float DashCooldown = 0.6f;
        [Tooltip("Layer ของกำแพง/สิ่งกีดขวาง — ต้องตั้งให้ตรงกับ Obstacle Mask ที่ใช้ใน EnemyBrain ไม่งั้น Dash จะไม่หยุดตอนชนกำแพง")]
        public LayerMask ObstacleMask;
        [Tooltip("ระยะเว้นจากผิวกำแพงตอน Dash ชน (กันติดคาไปในกำแพงพอดี)")]
        public float DashSkinWidth = 0.1f;

        [Header("เสียงฝีเท้า (Sound Investigation)")]
        [Tooltip("ระยะที่เสียงเดินไปถึง (ศัตรูในระยะนี้จะได้ยินและมาตรวจสอบ)")]
        public float WalkNoiseRadius = 3f;
        [Tooltip("ระยะที่เสียงวิ่งไปถึง (ดังกว่าเดินมาก)")]
        public float RunNoiseRadius = 7f;
        [Tooltip("ระยะที่เสียง Dash ไปถึง (ดังสุด เหมือนเสียงกระแทก/บูสต์)")]
        public float DashNoiseRadius = 10f;
        [Tooltip("ความถี่ที่จะส่งเสียงฝีเท้าออกไป (วินาทีต่อครั้ง)")]
        public float FootstepNoiseInterval = 0.4f;

        /// <summary>กำลัง Dash อยู่ตอนนี้หรือไม่ — ระบบอื่น (เช่น Equipment/Animation) เช็คค่านี้ได้เพื่อล็อคการกระทำอื่นระหว่าง Dash</summary>
        public bool IsDashing { get; private set; }

        private Rigidbody2D _rb;
        private Collider2D _playerCollider;
        private Vector2 _input;
        private Vector2 _lastFacingDirection = Vector2.up;
        private bool _isRunning;
        private bool _dashRequested;
        private float _footstepTimer;
        private float _dashCooldownRemaining;
        private Vector2 _dashDirection;
        private Vector2 _dashStartPosition;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _playerCollider = GetComponent<Collider2D>();

            // ล็อคการหมุนของ Physics ไว้ (เราหมุนผู้เล่นเองผ่าน PlayerShoot ตามเมาส์อยู่แล้ว
            // ถ้าไม่ล็อค พอชนกำแพง/สิ่งกีดขวางเอียงๆ แรงกระแทกจาก Physics จะดันหมุนแข่งกับโค้ด ทำให้ดูเหมือนควงสวิง)
            _rb.freezeRotation = true;

            // Interpolate ช่วยให้การเคลื่อนที่ดูนุ่มนวลขึ้นเมื่อ Frame Rate สูงกว่า Physics Rate
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Continuous กันการทะลุกำแพงตอนเคลื่อนที่เร็วมากๆ (เช่นตอน Dash)
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 0f;
            _rb.linearDamping = 0f;
            _rb.freezeRotation = true;

            // กันเผลอลืมตั้งค่า Distance เป็น 0 หรือติดลบ ซึ่งจะทำให้ Dash จบทันทีที่เริ่ม (ระยะ 0)
            if (DashDistance <= 0f) DashDistance = 1.5f;
        }

        private void Update()
        {
            _input.x = Input.GetAxisRaw("Horizontal");
            _input.y = Input.GetAxisRaw("Vertical");
            _input = _input.normalized;

            if (_input.sqrMagnitude > 0.01f)
                _lastFacingDirection = _input;

            _isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // แค่ "จำ" ว่ามีการกดปุ่ม Dash ไว้เฉยๆ ส่วนตัดสินใจ/นับระยะทางจริงทำใน FixedUpdate ทั้งหมด
            if (Input.GetKeyDown(DashKey))
                _dashRequested = true;

            HandleFootstepNoise();
        }

        private void FixedUpdate()
        {
            if (_dashCooldownRemaining > 0f)
                _dashCooldownRemaining -= Time.fixedDeltaTime;

            if (IsDashing)
            {
                TickDashMovement();
            }
            else
            {
                if (_dashRequested && _dashCooldownRemaining <= 0f)
                {
                    StartDash();
                }
                _dashRequested = false;

                if (!IsDashing) // เผื่อเพิ่งเริ่ม Dash ไปหมาดๆ ในเฟรมนี้ ไม่ต้องเซ็ต Velocity ปกติทับ
                {
                    float speed = _isRunning ? MoveSpeed * RunSpeedMultiplier : MoveSpeed;
                    _rb.linearVelocity = _input * speed;
                }
            }

            // กันหลุดกรอบแมพ: ถ้าฟิสิกส์ดันตำแหน่งเกินขอบไปแล้ว (เช่นโดนชนกระแทก) ให้ดึงกลับเข้ากรอบทันที
            Vector2 clamped = MapBounds.Clamp(_rb.position);
            if (clamped != _rb.position)
                _rb.position = clamped;
        }

        /// <summary>
        /// ขยับตัวช่วง Dash โดยจำกัดด้วย "ระยะทางสะสมจากจุดเริ่ม Dash" ไม่ใช่เวลา
        /// พร้อมเช็คกำแพงล่วงหน้าทุกสเต็ปฟิสิกส์ (เหมือนระบบ Dodge ของศัตรู) — ถ้าเจอกำแพงจะหยุดทันที
        /// ถ้าไม่เจอกำแพง จะพุ่งต่อจนกว่าระยะทางสะสมจะถึง DashDistance พอดี แล้วกลับสู่ความเร็วปกติทันที
        /// </summary>
        private void TickDashMovement()
        {
            float distanceTraveled = Vector2.Distance(_dashStartPosition, _rb.position);
            float remainingDistance = DashDistance - distanceTraveled;

            if (remainingDistance <= 0f)
            {
                EndDash();
                return;
            }

            // ก้าวในเฟรมนี้ต้องไม่เกินระยะที่เหลือ กันพุ่งเลย DashDistance ไปในสเต็ปสุดท้าย
            float stepDistance = Mathf.Min(DashSpeed * Time.fixedDeltaTime, remainingDistance);

            // Cast the player's actual collider, not just a ray from its centre.
            // This prevents the body from clipping through thin/rotated walls.
            RaycastHit2D hit = default;
            if (_playerCollider != null)
            {
                var filter = new ContactFilter2D
                {
                    useLayerMask = true,
                    layerMask = ObstacleMask,
                    useTriggers = false
                };
                var castHits = new RaycastHit2D[1];
                int hitCount = _playerCollider.Cast(
                    _dashDirection,
                    filter,
                    castHits,
                    stepDistance + DashSkinWidth);
                if (hitCount > 0)
                    hit = castHits[0];
            }
            else
            {
                hit = Physics2D.Raycast(
                    _rb.position,
                    _dashDirection,
                    stepDistance + DashSkinWidth,
                    ObstacleMask);
            }

            if (hit.collider != null)
            {
                float safeDistance = Mathf.Max(0f, hit.distance - DashSkinWidth);
                _rb.MovePosition(_rb.position + _dashDirection * safeDistance);
                EndDash();
                return;
            }

            _rb.MovePosition(_rb.position + _dashDirection * stepDistance);
            _rb.linearVelocity = _dashDirection * DashSpeed; // ไว้ให้ Physics/Animation อื่นอ่านความเร็วปัจจุบันได้ต่อ
        }

        private void StartDash()
        {
            _dashDirection = _lastFacingDirection.sqrMagnitude > 0.01f ? _lastFacingDirection.normalized : Vector2.up;
            _dashStartPosition = _rb.position;
            IsDashing = true;

            // เสียง Dash ดังพอสมควร (เหมือนเสียงบูสต์/กระแทก) ศัตรูในระยะได้ยินแล้วมาตรวจสอบได้
            GroupAI.Instance?.EmitNoise(transform.position, DashNoiseRadius);
        }

        /// <summary>จบ Dash ไม่ว่าจะเป็นเพราะถึงระยะที่กำหนด หรือชนกำแพง — รวมไว้จุดเดียวกันลืมเคลียร์ค่าไม่ครบ
        /// ความเร็วจะกลับสู่ปกติทันที (ไม่ใช่ค่อยๆ ชะลอ) ตรงตามอาการ Dash แบบ ULTRAKILL ที่ต้องการ</summary>
        private void EndDash()
        {
            IsDashing = false;
            _rb.linearVelocity = Vector2.zero;
            _dashCooldownRemaining = DashCooldown;
        }

        /// <summary>ให้ระบบภายนอก (เช่น Equipment/Thruster) เรียก Dash แบบกำหนดความเร็ว/ระยะทางเองได้ ไม่ต้องรอปุ่มกด</summary>
        public void TriggerDash(float dashSpeed, float distance)
        {
            if (IsDashing) return;
            if (_dashCooldownRemaining > 0f) return;

            DashSpeed = dashSpeed;
            DashDistance = distance > 0f ? distance : 1.5f;
            StartDash();
        }

        /// <summary>ส่งเสียงฝีเท้าเป็นจังหวะขณะเดิน/วิ่ง ให้ศัตรูในระยะได้ยินผ่าน GroupAI.EmitNoise</summary>
        private void HandleFootstepNoise()
        {
            bool isMoving = _input.sqrMagnitude > 0.01f && !IsDashing;
            if (!isMoving || GroupAI.Instance == null)
            {
                _footstepTimer = 0f;
                return;
            }

            _footstepTimer += Time.deltaTime;
            if (_footstepTimer < FootstepNoiseInterval) return;

            _footstepTimer = 0f;
            float radius = _isRunning ? RunNoiseRadius : WalkNoiseRadius;
            GroupAI.Instance.EmitNoise(transform.position, radius);
        }
    }
}

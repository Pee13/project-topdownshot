using UnityEngine;
using TopDownTacticalAI.Player;

namespace TopDownTacticalAI.Enemy
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class SupportAI : MonoBehaviour
    {
        [Header("Distance & Movement")]
        [Tooltip("ปิดไว้เมื่อให้ EnemyBrain (เฟรมเวิร์ก) เป็นผู้คุมการเคลื่อนที่ของตัวนี้\n" +
                 "สคริปต์จะเหลือหน้าที่ 'ฮีล' เท่านั้น ไม่เขียน velocity/rotation อีกต่อไป\n" +
                 "ปล่อยไว้จะเกิดสองระบบแย่งกันคุม Transform.rotation และตำแหน่ง (§ มี authority เดียว)")]
        public bool ControlMovement = true;
        public float FollowDistance = 16f;     // ระยะห่างจากหลัง Tank
        public float FleeDistance = 18f;       // ระยะปลอดภัยจากผู้เล่น
        public float MoveSpeed = 8f;
        public float RotationSpeed = 360f;     // ความเร็วการหันหน้า (องศา/วินาที)

        [Header("Vision")]
        public float ViewRadius = 45f;         // ระยะมองเห็นผู้เล่น
        public LayerMask ObstacleMask;         // เลเยอร์ Wall

        [Header("Heal Settings")]
        public float HealRange = 25f;
        public float HealPerSecond = 20f;
        public LineRenderer HealBeam;

        private Rigidbody2D _rb;
        private Transform _player;
        private Transform _tank;
        private Health _tankHealth;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;

            if (ObstacleMask == 0) ObstacleMask = LayerMask.GetMask("Wall");
            if (HealBeam == null) HealBeam = GetComponent<LineRenderer>();
        }

        private void Start()
        {
            FindPlayer();
            FindTank();
        }

        private void Update()
        {
            FindPlayer();
            FindTank();

            // เมื่อ EnemyBrain เป็นผู้คุมการเคลื่อนที่แล้ว สคริปต์นี้เหลือหน้าที่ฮีลเท่านั้น
            // การเคลื่อนที่/การหันหน้าให้ฝั่งเฟรมเวิร์กจัดการทั้งหมด เพื่อไม่ให้สองระบบแย่งกัน
            if (ControlMovement)
            {
                bool canSeePlayer = CheckLineOfSightToPlayer();
                HandleTactics(canSeePlayer);
            }
            HandleHealing();
        }

        private void FindPlayer()
        {
            if (_player != null) return;
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) p = GameObject.Find("Player 1");
            if (p != null) _player = p.transform;
        }

        private void FindTank()
        {
            if (_tank != null) return;
            GameObject t = GameObject.Find("Tank");
            if (t == null) t = GameObject.FindGameObjectWithTag("Tank");
            if (t != null)
            {
                _tank = t.transform;
                _tankHealth = t.GetComponent<Health>();
            }
        }

        private bool CheckLineOfSightToPlayer()
        {
            if (_player == null) return false;
            float dist = Vector2.Distance(transform.position, _player.position);
            if (dist > ViewRadius) return false;

            // ยิง Raycast ตรวจว่ามีกำแพงบังสายตาหรือไม่
            RaycastHit2D hit = Physics2D.Raycast(transform.position, (_player.position - transform.position).normalized, dist, ObstacleMask);
            return hit.collider == null;
        }

        private void HandleTactics(bool canSeePlayer)
        {
            // 1. ถ้าผู้เล่นเข้าใกล้เกินระยะปลอดภัย -> ถอยหนี
            if (_player != null && Vector2.Distance(transform.position, _player.position) < FleeDistance)
            {
                Vector2 fleeDir = ((Vector2)transform.position - (Vector2)_player.position).normalized;
                _rb.linearVelocity = fleeDir * MoveSpeed;
                SmoothRotateTowards(transform.position + (Vector3)fleeDir);
                return;
            }

            // 2. ถ้าเห็นผู้เล่นยืนอยู่ (แต่ยังไม่ถึงระยะอันตราย) -> หยุดเดินแล้ว "หันหน้ามองผู้เล่น"
            if (canSeePlayer && _player != null)
            {
                _rb.linearVelocity = Vector2.zero;
                SmoothRotateTowards(_player.position);
                return;
            }

            // 3. ปกติ -> เดินตามหลัง Tank และหันหน้าไปทิศทางที่ Tank กำลังเดินไป
            if (_tank != null && _tankHealth != null && _tankHealth.CurrentHP > 0f)
            {
                Vector2 targetPos = (Vector2)_tank.position - ((Vector2)_tank.right * FollowDistance);
                float dist = Vector2.Distance(transform.position, targetPos);

                if (dist > 2.5f)
                {
                    Vector2 moveDir = (targetPos - (Vector2)transform.position).normalized;
                    _rb.linearVelocity = moveDir * MoveSpeed;
                    SmoothRotateTowards(transform.position + (Vector3)moveDir);
                }
                else
                {
                    _rb.linearVelocity = Vector2.zero;
                    SmoothRotateTowards(transform.position + _tank.right);
                }
            }
            else
            {
                _rb.linearVelocity = Vector2.zero;
            }
        }

        private void HandleHealing()
        {
            if (HealBeam == null) return;

            bool isTankInjured = _tank != null && _tankHealth != null && 
                                 _tankHealth.CurrentHP > 0f && _tankHealth.CurrentHP < _tankHealth.MaxHP;
            float dist = _tank != null ? Vector2.Distance(transform.position, _tank.position) : float.MaxValue;

            if (isTankInjured && dist <= HealRange)
            {
                _tankHealth.Heal(HealPerSecond * Time.deltaTime);
                HealBeam.enabled = true;
                HealBeam.SetPosition(0, transform.position);
                HealBeam.SetPosition(1, _tank.position);
            }
            else
            {
                HealBeam.enabled = false;
            }
        }

        private void SmoothRotateTowards(Vector3 targetPos)
        {
            Vector2 dir = (targetPos - transform.position).normalized;
            if (dir == Vector2.zero) return;

            float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float currentAngle = transform.eulerAngles.z;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, RotationSpeed * Time.deltaTime);

            transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
        }
    }
}
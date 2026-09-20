using UnityEngine;

namespace TopDownTacticalAI.Player
{
    /// <summary>
    /// พฤติกรรมกระสุน: พุ่งไปข้างหน้า ทำดาเมจ แล้ว "ปักค้าง" อยู่ตรงจุดที่ชน
    /// รองรับทั้งกระสุนของผู้เล่นและกระสุนของ AI
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class Bullet : MonoBehaviour
    {
        [Header("Shooter Type")]
        [Tooltip("ติ๊กถูกถ้ากระสุนนี้เป็นของ Enemy (เพื่อยิงโดน Player และไม่โดนพวกเดียวกัน)")]
        public bool IsEnemyBullet = false;

        [Header("Bullet Movement & Damage")]
        public float Speed = 18f; // ความเร็วพุ่งที่เหมาะสมในหน่วยฟิสิกส์ 2D
        public float Damage = 20f;
        
        [Header("Duration Settings")]
        [Tooltip("อายุกระสุนถ้ายังไม่ชนอะไรเลย")]
        public float LifeTime = 3f;
        [Tooltip("ระยะเวลาที่กระสุนจะ 'ปักค้าง' อยู่หลังชน")]
        public float StickDuration = 3f;

        private bool _hasHit;
        private Rigidbody2D _rb;
        private Collider2D _collider;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();

            if (_rb != null)
            {
                _rb.gravityScale = 0f;
            }
        }

        private void Start()
        {
            if (_rb != null)
            {
                _rb.linearVelocity = transform.up * Speed;
            }

            Invoke(nameof(DestroySelf), LifeTime);
        }

        private void Update()
        {
            if (!_hasHit && (_rb == null || _rb.bodyType == RigidbodyType2D.Kinematic))
            {
                transform.position += transform.up * (Speed * Time.deltaTime);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasHit || other.isTrigger) return;

            // 1. กระสุนของฝั่งผู้เล่น: ไม่ชนผู้เล่นเอง
            if (!IsEnemyBullet)
            {
                if (other.CompareTag("Player") || other.gameObject.layer == LayerMask.NameToLayer("Player"))
                    return;
            }
            // 2. กระสุนของฝั่งศัตรู (AI): ไม่ชนศัตรูตัวอื่นหรือตัวเอง
            else
            {
                if (other.CompareTag("Enemy") || other.gameObject.layer == LayerMask.NameToLayer("EnemyBullet"))
                    return;
            }

            _hasHit = true;

            // สั่งหักเลือดเป้าหมาย
            if (other.TryGetComponent(out Health health))
            {
                health.TakeDamage(Damage);
            }

            EmbedInto(other);
        }

        private void EmbedInto(Collider2D other)
        {
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.bodyType = RigidbodyType2D.Kinematic;
            }

            if (_collider != null)
                _collider.enabled = false;

            transform.SetParent(other.transform);

            CancelInvoke(nameof(DestroySelf));
            Invoke(nameof(DestroySelf), StickDuration);
        }

        private void DestroySelf()
        {
            Destroy(gameObject);
        }
    }
}
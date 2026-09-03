using UnityEngine;

namespace TopDownTacticalAI.Player
{
    /// <summary>
    /// พฤติกรรมกระสุน: พุ่งไปข้างหน้า ทำดาเมจ แล้ว "ปักค้าง" อยู่ตรงจุดที่ชน
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class Bullet : MonoBehaviour
    {
        [Header("Bullet Movement & Damage")]
        public float Speed = 180f; // ความเร็วพุ่ง
        public float Damage = 25f;
        
        [Header("Duration Settings")]
        [Tooltip("อายุกระสุนถ้ายังไม่ชนอะไรเลย (บินไปเรื่อยๆ)")]
        public float LifeTime = 3f;
        [Tooltip("ระยะเวลาที่กระสุนจะ 'ปักค้าง' อยู่หลังชนอะไรสักอย่าง ก่อนจะหายไปจริงๆ")]
        public float StickDuration = 4f;

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
            // พุ่งไปตามแกน Up ของหัวกระสุน
            if (_rb != null)
            {
                _rb.linearVelocity = transform.up * Speed;
            }

            Invoke(nameof(DestroySelf), LifeTime);
        }

        private void Update()
        {
            // อัปเดตตำแหน่งกรณี Rigidbody เป็น Kinematic
            if (!_hasHit && (_rb == null || _rb.bodyType == RigidbodyType2D.Kinematic))
            {
                transform.position += transform.up * (Speed * Time.deltaTime);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasHit) return;

            // บล็อกไม่ให้ชนตัวผู้เล่นเอง หรือชิ้นส่วนของผู้เล่น
            if (other.CompareTag("Player") || other.gameObject.layer == LayerMask.NameToLayer("Player") || other.isTrigger) 
                return;

            _hasHit = true;

            // ทำดาเมจใส่เป้าหมายที่มี Health
            if (other.TryGetComponent(out Health health))
            {
                health.TakeDamage(Damage);
            }

            EmbedInto(other);
        }

        /// <summary>หยุดกระสุนและ "ปัก" ค้างอยู่ตรงจุดชน แทนที่จะลบทิ้งทันที</summary>
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
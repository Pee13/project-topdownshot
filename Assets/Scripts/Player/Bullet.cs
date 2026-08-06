using UnityEngine;

namespace TopDownTacticalAI.Player
{
    /// <summary>
    /// พฤติกรรมกระสุน: ทำดาเมจเมื่อชนเป้าหมายที่มี Health แล้ว "ปักค้าง" อยู่ตรงจุดที่ชน
    /// (แทนที่จะหายไปทันที) เหมือนกระสุนปักเข้าไปในกำแพง/ตัวละครจริงๆ
    /// แล้วค่อยลบตัวเองออกหลังจากปักอยู่สักพัก (กันกระสุนสะสมเกลื่อนฉากตลอดไป)
    /// </summary>
    public class Bullet : MonoBehaviour
    {
        public float Damage = 10f;
        [Tooltip("อายุกระสุนถ้ายังไม่ชนอะไรเลย (บินไปเรื่อยๆ)")]
        public float LifeTime = 3f;
        [Tooltip("ระยะเวลาที่กระสุนจะ 'ปักค้าง' อยู่หลังชนอะไรสักอย่าง ก่อนจะหายไปจริงๆ")]
        public float StickDuration = 5f;

        private bool _hasHit;
        private Rigidbody2D _rb;
        private Collider2D _collider;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
        }

        private void Start()
        {
            // ใช้ Invoke (ยกเลิกได้จริงด้วย CancelInvoke) แทน Destroy(gameObject, delay) ตรงๆ
            // เพื่อให้ตอนปักค้างแล้ว เปลี่ยนกำหนดเวลาลบใหม่ได้ถูกต้อง ไม่ชนกับตัวจับเวลาเดิม
            Invoke(nameof(DestroySelf), LifeTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasHit) return; // กันชนซ้ำหลายครั้งในเฟรมเดียวกัน
            _hasHit = true;

            if (other.TryGetComponent(out Health health))
            {
                health.TakeDamage(Damage);
            }

            EmbedInto(other);
        }

        /// <summary>หยุดกระสุนและ "ปัก" ค้างอยู่ตรงจุดชน แทนที่จะลบทิ้งทันที</summary>
        private void EmbedInto(Collider2D other)
        {
            // หยุดการเคลื่อนที่ทันที
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.bodyType = RigidbodyType2D.Kinematic;
            }

            // ปิด Collider ตัวเองกันไปโดนซ้ำ/ชนอย่างอื่นต่อ
            if (_collider != null)
                _collider.enabled = false;

            // ฝากตัวเองไว้กับสิ่งที่โดนชน เผื่อสิ่งนั้นเคลื่อนที่ (เช่นปักติดตัวศัตรูที่ยังเดินต่อ)
            transform.SetParent(other.transform);

            // ยกเลิกตัวจับเวลาเดิม (ตอนยังบินอยู่) แล้วตั้งเวลาลบใหม่นับจากตอนปักค้าง
            CancelInvoke(nameof(DestroySelf));
            Invoke(nameof(DestroySelf), StickDuration);
        }

        private void DestroySelf()
        {
            Destroy(gameObject);
        }
    }
}

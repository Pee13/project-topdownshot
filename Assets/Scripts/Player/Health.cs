using UnityEngine;
using UnityEngine.Events;
using TopDownTacticalAI.UI;

namespace TopDownTacticalAI.Player
{
    /// <summary>
    /// ระบบพลังชีวิตแบบใช้ร่วมกันได้ทั้งผู้เล่นและศัตรู รองรับการปรับสเกลความยากและการฮีล
    /// </summary>
    public class Health : MonoBehaviour
    {
        [Header("HP Settings")]
        public float MaxHP = 100f;
        public float CurrentHP { get; private set; }

        [Tooltip("สั่งทำลาย GameObject ทันทีเมื่อเลือดหมด")]
        public bool DestroyOnDeath = true;

        [Header("Difficulty Scaling (Enemy)")]
        [Tooltip("คูณ MaxHP ตามระดับความยากใน GameDifficulty อัตโนมัติ")]
        public bool applyDifficultyScaling = true;
        [Tooltip("Tag ของยูนิตที่จะให้สเกลตามความยาก")]
        public string objectTag = "Enemy";

        [Header("Events")]
        public UnityEvent OnDeath;
        public UnityEvent<float> OnDamaged; // ส่งค่า HP ปัจจุบันหลังโดนยิง
        public UnityEvent<float> OnHealed;  // ส่งค่า HP ปัจจุบันหลังได้รับการฮีล

        // Helper สำหรับ AI Support และ UI นำไปตรวจสอบเงื่อนไข
        public bool IsDead => CurrentHP <= 0f;
        public bool IsFullHP => CurrentHP >= MaxHP;
        public float HPPercent => CurrentHP / MaxHP;

        private void Awake()
        {
            // ปรับสเกลเลือดตามระดับความยากของเกม
            if (applyDifficultyScaling && CompareTag("Enemy"))
            {
                var mult = GameDifficulty.GetMultipliers();
                MaxHP *= mult.maxHPMultiplier;
            }

            CurrentHP = MaxHP;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            CurrentHP = Mathf.Max(0f, CurrentHP - amount);
            Debug.Log($"[{gameObject.name}] โดนดาเมจ {amount} | HP เหลือ: {CurrentHP}/{MaxHP}");
            
            OnDamaged?.Invoke(CurrentHP);

            if (CurrentHP <= 0f)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;

            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
            OnHealed?.Invoke(CurrentHP);
        }

        private void Die()
        {
            Debug.Log($"[{gameObject.name}] เสียชีวิตแล้ว!");
            OnDeath?.Invoke();

            if (DestroyOnDeath)
            {
                Destroy(gameObject);
            }
        }

        public void SetMaxHP(float newMaxHP)
        {
            MaxHP = newMaxHP;
            CurrentHP = Mathf.Min(CurrentHP, MaxHP);
        }
    }
}
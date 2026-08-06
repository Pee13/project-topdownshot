using UnityEngine;
using UnityEngine.Events;

namespace TopDownTacticalAI.Player
{
    /// <summary>
    /// ระบบพลังชีวิตแบบใช้ร่วมกันได้ทั้งผู้เล่นและศัตรู
    /// สำหรับศัตรู แนะนำให้ผูก OnDeath เข้ากับการปิด EnemyBrain + Object Pool/Destroy
    /// </summary>
    public class Health : MonoBehaviour
    {
        public float MaxHP = 100f;
        public float CurrentHP { get; private set; }

        public UnityEvent OnDeath;
        public UnityEvent<float> OnDamaged; // ส่งค่า HP ปัจจุบันหลังโดนตี

        private void Awake()
        {
            CurrentHP = MaxHP;
        }

        public void TakeDamage(float amount)
        {
            if (CurrentHP <= 0f) return;

            CurrentHP = Mathf.Max(0f, CurrentHP - amount);
            OnDamaged?.Invoke(CurrentHP);

            if (CurrentHP <= 0f)
                OnDeath?.Invoke();
        }

        public void Heal(float amount)
        {
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
        }
    }
}

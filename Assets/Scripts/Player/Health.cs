using UnityEngine;
using UnityEngine.Events;
using TopDownTacticalAI.UI;

namespace TopDownTacticalAI.Player
{
    /// <summary>
<<<<<<< HEAD
    /// ระบบพลังชีวิตแบบใช้ร่วมกันได้ทั้งผู้เล่นและศัตรู
    /// </summary>
    public class Health : MonoBehaviour
    {
        [Header("HP Settings")]
        public float MaxHP = 100f;
        public float CurrentHP { get; private set; }

        [Tooltip("สั่งทำลาย GameObject ทันทีเมื่อเลือดหมด")]
        public bool DestroyOnDeath = true;

        [Header("Events")]
        public UnityEvent OnDeath;
        public UnityEvent<float> OnDamaged; // ส่งค่า HP ปัจจุบันหลังโดนยิง
        public UnityEvent<float> OnHealed;  // ส่งค่า HP ปัจจุบันหลังได้รับการฮีล

        // Helper สำหรับให้ AI Support / UI ดึงไปเช็กเงื่อนไข
        public bool IsDead => CurrentHP <= 0f;
        public bool IsFullHP => CurrentHP >= MaxHP;
        public float HPPercent => CurrentHP / MaxHP;
=======
    /// Health system that can be shared between the player and enemies.
    /// For enemies, hook OnDeath to disable EnemyBrain + play death anim.
    /// Supports difficulty multipliers from GameDifficulty.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [Header("Base Stats")]
        public float MaxHP = 100f;
        public float CurrentHP { get; private set; }

        [Header("Difficulty Scaling (Enemy)")]
        [Tooltip("Multiplies MaxHP by GameDifficulty.Current on Awake.")]
        public bool applyDifficultyScaling = true;
        [Tooltip("Tag of this object (Player/Enemy) - decides whether to scale by difficulty.")]
        public string objectTag = "Enemy";

        public UnityEvent OnDeath;
        public UnityEvent<float> OnDamaged; // Passes current HP after the hit
>>>>>>> 060403a36486f248d352388e14efe17bf1a7a602

        private void Awake()
        {
            // Apply difficulty scaling for enemies
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
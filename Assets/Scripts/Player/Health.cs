using UnityEngine;
using UnityEngine.Events;
using TopDownTacticalAI.UI;

namespace TopDownTacticalAI.Player
{
    /// <summary>
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

        public void SetMaxHP(float newMaxHP)
        {
            MaxHP = newMaxHP;
            CurrentHP = Mathf.Min(CurrentHP, MaxHP);
        }
    }
}

using UnityEngine;
using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.Animation
{
    /// <summary>
    /// ส่งพารามิเตอร์ไปยัง Animator ตาม State ปัจจุบันของ AI
    /// ต้องมี Animator Controller ที่มี Parameter: "State" (int), "Speed" (float), "IsAiming" (bool)
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AnimationController : MonoBehaviour
    {
        private Animator _animator;
        private static readonly int StateParam = UnityEngine.Animator.StringToHash("State");
        private static readonly int SpeedParam = UnityEngine.Animator.StringToHash("Speed");
        private static readonly int AimingParam = UnityEngine.Animator.StringToHash("IsAiming");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void SetState(EnemyState state)
        {
            if (_animator == null) return;
            _animator.SetInteger(StateParam, (int)state);
        }

        public void SetSpeed(float speed)
        {
            if (_animator == null) return;
            _animator.SetFloat(SpeedParam, speed);
        }

        public void SetAiming(bool isAiming)
        {
            if (_animator == null) return;
            _animator.SetBool(AimingParam, isAiming);
        }
    }
}

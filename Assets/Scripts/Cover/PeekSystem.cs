using UnityEngine;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Cover
{
    /// <summary>
    /// จัดการจังหวะโผล่ออกมายิงแล้วหลบกลับเข้าที่กำบัง (Peek-and-Shoot)
    /// </summary>
    public class PeekSystem
    {
        private readonly CountdownTimer _peekTimer = new CountdownTimer();
        private readonly float _peekDuration;
        private readonly float _peekCooldown;
        private readonly CountdownTimer _cooldownTimer = new CountdownTimer();

        public bool IsPeeking { get; private set; }

        public PeekSystem(float peekDuration = 1.0f, float peekCooldown = 1.5f)
        {
            _peekDuration = peekDuration;
            _peekCooldown = peekCooldown;
        }

        public void TryStartPeek()
        {
            if (IsPeeking) return;
            if (!_cooldownTimer.IsFinished) return;

            IsPeeking = true;
            _peekTimer.Start(_peekDuration);
        }

        public void Tick(float deltaTime)
        {
            if (IsPeeking)
            {
                _peekTimer.Tick(deltaTime);
                if (_peekTimer.IsFinished)
                {
                    IsPeeking = false;
                    _cooldownTimer.Start(_peekCooldown);
                }
            }
            else
            {
                _cooldownTimer.Tick(deltaTime);
            }
        }
    }
}

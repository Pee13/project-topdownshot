using TopDownTacticalAI.Core;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Combat
{
    /// <summary>
    /// ควบคุมการรีโหลดกระสุนของศัตรู
    /// </summary>
    public class ReloadController
    {
        private readonly Blackboard _blackboard;
        private readonly float _reloadDuration;
        private readonly CountdownTimer _timer = new CountdownTimer();

        public ReloadController(Blackboard blackboard, float reloadDuration = 1.8f)
        {
            _blackboard = blackboard;
            _reloadDuration = reloadDuration;
        }

        public bool NeedsReload => _blackboard.CurrentAmmo <= 0;

        public void StartReload()
        {
            _blackboard.IsReloading = true;
            _timer.Start(_reloadDuration);
        }

        public void Tick(float deltaTime)
        {
            if (!_blackboard.IsReloading) return;

            _timer.Tick(deltaTime);
            if (_timer.IsFinished)
            {
                _blackboard.CurrentAmmo = _blackboard.MaxAmmo;
                _blackboard.IsReloading = false;
            }
        }
    }
}

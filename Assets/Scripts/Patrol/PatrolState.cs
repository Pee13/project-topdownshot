using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Patrol
{
    /// <summary>
    /// State การเดินตรวจตรา: ใช้ WaypointPatrol ถ้ามีจุดกำหนดไว้ มิฉะนั้นใช้ CoveragePatrol
    /// (ลาดตระเวนแบบจำพื้นที่ทั้งหมดได้ วนไปทีละจุดอย่างเป็นระบบ ไม่ใช่สุ่มมั่วๆ)
    /// เมื่อ Blackboard.CanSeeTarget เป็น true ระบบภายนอก (EnemyBrain) จะสั่งเปลี่ยนไป Chase
    /// </summary>
    public class PatrolState : IAIState
    {
        public EnemyState StateType => EnemyState.Patrol;

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly float _moveSpeed;
        private readonly float _waitTime;

        private WaypointPatrol _waypointPatrol;
        private CoveragePatrol _coveragePatrol;
        private readonly IdleLookAround _idleLook = new IdleLookAround();
        private readonly CountdownTimer _waitTimer = new CountdownTimer();
        private readonly LayerMask _obstacleMask;
        private readonly float _personalSpace;
        private readonly StuckDetector _stuckDetector = new StuckDetector();

        private bool _isWaiting;

        public PatrolState(Transform self, Blackboard blackboard, Transform[] waypoints, LayerMask obstacleMask, float moveSpeed = 2f, float waitTime = 2f, float patrolRadius = 8f, int coveragePointCount = 8, float personalSpace = 1.3f)
        {
            _self = self;
            _blackboard = blackboard;
            _moveSpeed = moveSpeed;
            _waitTime = waitTime;
            _obstacleMask = obstacleMask;
            _personalSpace = personalSpace;

            if (waypoints != null && waypoints.Length > 0)
                _waypointPatrol = new WaypointPatrol(waypoints, obstacleMask);
            else
                _coveragePatrol = new CoveragePatrol(self.position, patrolRadius, obstacleMask, coveragePointCount);
        }

        public void Enter()
        {
            _isWaiting = false;
            _stuckDetector.Reset(_self.position);
        }

        public void Tick(float deltaTime)
        {
            Vector2 currentPos = _self.position;
            bool useWaypoints = _waypointPatrol != null && _waypointPatrol.HasWaypoints;

            Vector2 destination = useWaypoints ? _waypointPatrol.GetCurrentWaypoint() : _coveragePatrol.GetCurrentPoint();
            _blackboard.CurrentDestination = destination;
            bool reached = useWaypoints ? _waypointPatrol.HasReached(currentPos) : _coveragePatrol.HasReached(currentPos);

            if (_isWaiting)
            {
                float sway = _idleLook.Tick(deltaTime);
                _self.rotation = Quaternion.Euler(0, 0, sway);

                _waitTimer.Tick(deltaTime);
                if (_waitTimer.IsFinished)
                {
                    _isWaiting = false;
                    if (useWaypoints)
                        _waypointPatrol.AdvanceToNext();
                    else
                        _coveragePatrol.AdvanceToNext();
                }
                return;
            }

            if (reached)
            {
                _isWaiting = true;
                _waitTimer.Start(_waitTime);
                _idleLook.StartLooking();
                return;
            }

            Vector2 desiredDirection = (destination - currentPos).normalized;
            Vector2 direction = SteeringMovement.GetSteeredDirection(currentPos, desiredDirection, _obstacleMask, self: _self, personalSpace: _personalSpace);

            _stuckDetector.Tick(currentPos, deltaTime);
            if (_stuckDetector.IsStuck)
            {
                // จุดหมายเดินไปไม่ถึง (ทางตัน/อยู่หลังกำแพง) ข้ามไปจุดถัดไปในลำดับลาดตระเวนเลย
                if (useWaypoints)
                    _waypointPatrol.AdvanceToNext();
                else
                    _coveragePatrol.AdvanceToNext();

                _stuckDetector.Reset(currentPos);
                return;
            }

            // เดินแบบ "เช็คก่อนก้าว" — กันมุดกำแพงเหมือน state อื่น (ระบบกลาง SteeringMovement)
            _self.position = SteeringMovement.MoveWithCollisionCheck(
                _self, direction * (_moveSpeed * deltaTime), _obstacleMask);

            if (direction.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                _self.rotation = Quaternion.Euler(0, 0, angle);
            }
        }

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit() { }
    }
}

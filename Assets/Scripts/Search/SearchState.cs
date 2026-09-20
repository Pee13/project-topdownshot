using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Utilities;
using TopDownTacticalAI.Memory;

namespace TopDownTacticalAI.Search
{
    /// <summary>
    /// State ค้นหาผู้เล่นหลังจากไล่ตามแล้วเสียเป้าหมายไป
    /// ขั้นตอน:
    /// 1) เดินไปตำแหน่งล่าสุดที่เห็น (Investigation)
    /// 2) ถ้ารู้ทิศทางที่ผู้เล่นกำลังวิ่งอยู่ตอนเสียสายตา (เช่นเพิ่งเลี้ยวมุมกำแพงไป) จะ "วิ่งไล่ต่อ" ไปอีกนิด
    ///    ตามทิศนั้นก่อน (เหมือนคนไล่ตามจริงๆ ที่อ้อมมุมแล้ววิ่งต่ออีกหน่อยก่อนจะเริ่มมองหาแบบละเอียด)
    ///    แทนที่จะหยุดค้นหาตรงจุดที่เสียสายตาทันที
    /// 3) มองซ้ายขวา + เดินสำรวจแบบมีทิศทาง (SearchPattern)
    /// 4) ถ้าไม่พบ กลับ Patrol / ถ้าพบ กลับ Chase (ควบคุมโดย EnemyBrain)
    /// ใช้ SteeringMovement เลี่ยงกำแพง + StuckDetector กันติดมุมอับ
    /// </summary>
    public class SearchState : IAIState
    {
        public EnemyState StateType => EnemyState.Search;

        private enum SubPhase { MovingToLastSeen, PursuingPastCorner, SearchPatternActive }

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly float _moveSpeed;
        private readonly float _searchWanderRadius;
        private readonly LayerMask _obstacleMask;
        private readonly TargetMemory _targetMemory;
        private readonly float _pursuitOvershootDistance;
        private readonly float _maxAlertDuration;
        private readonly float _personalSpace;
        private readonly StuckDetector _stuckDetector = new StuckDetector();

        private readonly Investigation _investigation = new Investigation();
        private SearchPattern _searchPattern;
        private SubPhase _subPhase;
        private Vector2 _pursuitTarget;
        private Vector2 _fleeDirection;
        private float _alertTimer;

        public bool IsSearchComplete { get; private set; }

        /// <param name="maxAlertDuration">
        /// เวลารวมสูงสุด (วินาที) ที่ยอมให้อยู่ในโหมดตื่นตัว/ค้นหาต่อเนื่อง ก่อนจะยอมแพ้แล้วกลับไปเฝ้าจุดเดิม
        /// (เหมือนโหมด Alert ใน Metal Gear Solid ที่มีเวลาจำกัดก่อนลดระดับเตือนภัยกลับเป็นปกติ)
        /// </param>
        public SearchState(Transform self, Blackboard blackboard, float moveSpeed, float searchWanderRadius, LayerMask obstacleMask, TargetMemory targetMemory, float pursuitOvershootDistance = 3f, float maxAlertDuration = 99f, float personalSpace = 1.3f)
        {
            _self = self;
            _blackboard = blackboard;
            _moveSpeed = moveSpeed;
            _searchWanderRadius = searchWanderRadius;
            _obstacleMask = obstacleMask;
            _targetMemory = targetMemory;
            _pursuitOvershootDistance = pursuitOvershootDistance;
            _maxAlertDuration = maxAlertDuration;
            _personalSpace = personalSpace;
        }

        public void Enter()
        {
            _subPhase = SubPhase.MovingToLastSeen;
            IsSearchComplete = false;
            _searchPattern = null;
            _alertTimer = 0f;
            _stuckDetector.Reset(_self.position);
        }

        public void Tick(float deltaTime)
        {
            Vector2 currentPos = _self.position;
            _stuckDetector.Tick(currentPos, deltaTime);

            // นับเวลารวมที่อยู่ในโหมดตื่นตัวต่อเนื่อง ไม่ว่าจะอยู่เฟสไหนก็ตาม
            _alertTimer += deltaTime;
            if (_alertTimer >= _maxAlertDuration)
            {
                // หาไม่เจอภายในเวลาที่กำหนด -> ยอมแพ้ กลับไปเฝ้าจุดเดิม (Patrol) เหมือนลดระดับเตือนภัยกลับเป็นปกติ
                IsSearchComplete = true;
                return;
            }

            switch (_subPhase)
            {
                case SubPhase.MovingToLastSeen:
                    TickMovingToLastSeen(currentPos, deltaTime);
                    break;

                case SubPhase.PursuingPastCorner:
                    TickPursuingPastCorner(currentPos, deltaTime);
                    break;

                case SubPhase.SearchPatternActive:
                    TickSearchPattern(currentPos, deltaTime);
                    break;
            }
        }

        private void TickMovingToLastSeen(Vector2 currentPos, float deltaTime)
        {
            Vector2 lastSeenPos = _blackboard.LastSeen.Position;
            _blackboard.CurrentDestination = lastSeenPos;

            if (_investigation.HasArrived(currentPos, lastSeenPos) || _stuckDetector.IsStuck)
            {
                // มาถึงจุดล่าสุดที่เห็นแล้ว: เช็คว่ารู้ทิศทางที่ผู้เล่นกำลังวิ่งอยู่ตอนนั้นไหม
                // ใช้ทิศทางเฉลี่ยจากประวัติเส้นทาง (Breadcrumb Trail) แทนความเร็วเฟรมเดียว เพราะแม่นยำ/นิ่งกว่า
                // โดยเฉพาะถ้าผู้เล่นเดินอ้อมโค้งกำแพงมา จะเดาทิศตามแนวโค้งได้ดีกว่าพุ่งตรงเป็นเส้นตรง
                _fleeDirection = _targetMemory != null ? _targetMemory.GetSmoothedDirection() : Vector2.zero;

                if (_fleeDirection.sqrMagnitude > 0.1f)
                {
                    // รู้ทิศทาง -> วิ่งไล่ต่อไปอีกนิดตามทิศนั้นก่อน (เหมือนอ้อมมุมกำแพงแล้ววิ่งต่อ)
                    // แทนที่จะหยุดมองหาตรงจุดเสียสายตาทันที ซึ่งมักจะติดอยู่แค่ริมกำแพงพอดี
                    _pursuitTarget = lastSeenPos + _fleeDirection * _pursuitOvershootDistance;
                    _subPhase = SubPhase.PursuingPastCorner;
                }
                else
                {
                    // ไม่รู้ทิศทาง (เช่นผู้เล่นหยุดนิ่งตอนเสียสายตาพอดี) -> เริ่มค้นหาตรงจุดนี้เลย
                    StartSearchPattern(currentPos);
                }

                _stuckDetector.Reset(currentPos);
                return;
            }

            Vector2 desiredDir = _investigation.GetDirection(currentPos, lastSeenPos);
            Vector2 dir = SteeringMovement.GetSteeredDirection(currentPos, desiredDir, _obstacleMask, self: _self, personalSpace: _personalSpace);
            _self.position = SteeringMovement.MoveWithCollisionCheck(_self, dir * (_moveSpeed * deltaTime), _obstacleMask);
            RotateTowards(dir);
        }

        private void TickPursuingPastCorner(Vector2 currentPos, float deltaTime)
        {
            _blackboard.CurrentDestination = _pursuitTarget;
            if (Vector2.Distance(currentPos, _pursuitTarget) <= 0.3f || _stuckDetector.IsStuck)
            {
                // วิ่งไล่ต่อสุดระยะแล้ว (หรือติดทางตันไปต่อไม่ได้) ค่อยเริ่มค้นหาแบบละเอียดจากจุดนี้
                StartSearchPattern(currentPos);
                _stuckDetector.Reset(currentPos);
                return;
            }

            Vector2 desiredDir = (_pursuitTarget - currentPos).normalized;
            Vector2 dir = SteeringMovement.GetSteeredDirection(currentPos, desiredDir, _obstacleMask, self: _self, personalSpace: _personalSpace);
            _self.position = SteeringMovement.MoveWithCollisionCheck(_self, dir * (_moveSpeed * deltaTime), _obstacleMask);
            RotateTowards(dir);
        }

        private void StartSearchPattern(Vector2 center)
        {
            _searchPattern = new SearchPattern(center, _searchWanderRadius, _obstacleMask, _fleeDirection);
            _subPhase = SubPhase.SearchPatternActive;
        }

        private void TickSearchPattern(Vector2 currentPos, float deltaTime)
        {
            _searchPattern.Tick(deltaTime);

            if (_searchPattern.IsWandering)
            {
                Vector2 target = _searchPattern.WanderTarget;
                _blackboard.CurrentDestination = target;
                if (Vector2.Distance(currentPos, target) <= 0.3f || _stuckDetector.IsStuck)
                {
                    _searchPattern.NotifyReachedWanderPoint();
                    _stuckDetector.Reset(currentPos);
                }
                else
                {
                    Vector2 desiredDir = (target - currentPos).normalized;
                    Vector2 dir = SteeringMovement.GetSteeredDirection(currentPos, desiredDir, _obstacleMask, self: _self, personalSpace: _personalSpace);
                    _self.position = SteeringMovement.MoveWithCollisionCheck(_self, dir * (_moveSpeed * deltaTime), _obstacleMask);
                    RotateTowards(dir);
                }
            }
            else if (_searchPattern.IsReturningToCenter)
            {
                Vector2 center = _searchPattern.CenterPosition;
                _blackboard.CurrentDestination = center;
                if (Vector2.Distance(currentPos, center) <= 0.3f || _stuckDetector.IsStuck)
                {
                    _searchPattern.NotifyReturnedToCenter();
                    _stuckDetector.Reset(currentPos);
                }
                else
                {
                    Vector2 desiredDir = (center - currentPos).normalized;
                    Vector2 dir = SteeringMovement.GetSteeredDirection(currentPos, desiredDir, _obstacleMask, self: _self, personalSpace: _personalSpace);
                    _self.position = SteeringMovement.MoveWithCollisionCheck(_self, dir * (_moveSpeed * deltaTime), _obstacleMask);
                    RotateTowards(dir);
                }
            }
            else if (!_searchPattern.IsFinished)
            {
                _self.rotation = Quaternion.Euler(0, 0, _searchPattern.CurrentLookAngleOffset);
            }

            if (_searchPattern.IsFinished)
            {
                IsSearchComplete = true;
            }
        }

        private void RotateTowards(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            _self.rotation = Quaternion.Euler(0, 0, angle);
        }

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit() { }
    }
}

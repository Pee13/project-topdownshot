using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Utilities;
using TopDownTacticalAI.Memory;
using TopDownTacticalAI.Pathfinding;
using TopDownTacticalAI.Chase; // TargetPrediction.PredictAlongPath
using TopDownTacticalAI.Tactical; // GroupAI.GetSearchSectorIndex

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
        private readonly AgentPathMover _mover;
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
        public SearchState(Transform self, Blackboard blackboard, float moveSpeed, float searchWanderRadius, LayerMask obstacleMask, TargetMemory targetMemory, float pursuitOvershootDistance = 3f, float maxAlertDuration = 99f, float personalSpace = 1.3f, AgentPathMover mover = null)
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
            _mover = mover;
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
            // ไป "จุดที่ผู้เล่นน่าจะอยู่ตอนนี้" ไม่ใช่จุดที่เห็นค้างเก่า
            // (ฉายจากประวัติการเคลื่อนที่/ทิศทาง × อายุความจำ × ระดับความมั่นใจ + ปรับตามเส้นทางจริง)
            Vector2 searchOrigin = ComputeSearchOrigin();
            _blackboard.CurrentDestination = searchOrigin;

            if (_investigation.HasArrived(currentPos, searchOrigin) || _stuckDetector.IsStuck)
            {
                // มาถึงจุดที่คาดว่าผู้เล่นอยู่แล้ว: เช็คว่ารู้ทิศทางที่ผู้เล่นกำลังวิ่งอยู่ตอนนั้นไหม
                // ใช้ทิศทางเฉลี่ยจากประวัติเส้นทาง (Breadcrumb Trail) แทนความเร็วเฟรมเดียว เพราะแม่นยำ/นิ่งกว่า
                // โดยเฉพาะถ้าผู้เล่นเดินอ้อมโค้งกำแพงมา จะเดาทิศตามแนวโค้งได้ดีกว่าพุ่งตรงเป็นเส้นตรง
                _fleeDirection = _targetMemory != null ? _targetMemory.GetSmoothedDirection() : Vector2.zero;

                if (_fleeDirection.sqrMagnitude > 0.1f)
                {
                    // รู้ทิศทาง -> วิ่งไล่ต่อไปอีกนิดตามทิศนั้นก่อน (เหมือนอ้อมมุมกำแพงแล้ววิ่งต่อ)
                    // แทนที่จะหยุดมองหาตรงจุดเสียสายตาทันที ซึ่งมักจะติดอยู่แค่ริมกำแพงพอดี
                    Vector2 pursuitTarget = searchOrigin + _fleeDirection * _pursuitOvershootDistance;

                    // path-aware: ถ้าจุดที่ได้ทะลุกำแพง ให้เลื่อนตามเส้นทาง NavMesh จริงแทน
                    if (TargetPrediction.PredictAlongPath(searchOrigin, pursuitTarget, _obstacleMask, out Vector2 pathAware))
                        pursuitTarget = pathAware;

                    _pursuitTarget = pursuitTarget;
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

            MoveTowards(searchOrigin, currentPos, deltaTime);
        }

        /// <summary>
        /// ทำนาย "ผู้เล่นตอนนี้น่าจะอยู่ไหน" จากข้อมูลที่รับรู้ได้จริง — แทนการไปจุด seen เดิมซ้ำ ๆ
        ///
        /// สูตร: lastSeen + LastVelocity × lead
        ///   - lead = อายุความจำ (จำกัด ≤ 2.5 วิ กันฉายไกลเกินจริง) × ระดับความมั่นใจ
        ///   - ความมั่นใจต่ำ (เสียงไกล/ความจำเก่า) → lead สั้น → แทบไม่ต่างจากจุด seen
        ///     (เสียง/ข้อมูลเก่า = "ข้อมูลประกอบ" ไม่ใช่ตำแหน่งแน่นอน ตามหลักการ)
        ///   - ทิศทางมาจาก TargetMemory.LastVelocity (อัปเดตตอนเห็นจริง ไม่ใช่สุ่ม)
        ///   - จุดทำนายชนกำแพง → ปรับตามเส้นทาง NavMesh จริง (ผู้เล่นวิ่งอ้อม ไม่ใช่ทะลุ)
        /// </summary>
        private Vector2 ComputeSearchOrigin()
        {
            Vector2 lastSeenPos = _blackboard.LastSeen.Position;
            if (_targetMemory == null || !_blackboard.LastSeen.IsValid) return lastSeenPos;
            if (_targetMemory.LastVelocity.sqrMagnitude < 0.01f) return lastSeenPos;

            float age = Mathf.Max(0f, Time.time - _blackboard.LastSeen.TimeSeen);
            float confidence = Mathf.Clamp01(_blackboard.MemoryConfidence);
            float lead = Mathf.Min(age, 2.5f) * confidence;
            if (lead < 0.05f) return lastSeenPos; // แทบไม่แน่ใจ/เพิ่งเกิด → ใช้จุด seen เดิม

            Vector2 predicted = lastSeenPos + _targetMemory.LastVelocity * lead;

            // กันฉายไกลผิดจริง: ห่างจากจุด seen เดิมไม่เกิน 12 หน่วย (สเกลแมพนี้พอเหมาะ)
            Vector2 offset = predicted - lastSeenPos;
            if (offset.magnitude > 12f)
                predicted = lastSeenPos + offset.normalized * 12f;

            // path-aware: จุดทำนายหลังกำแพง → ใช้จุดบนเส้นทางวิ่งจริง
            if (TargetPrediction.PredictAlongPath(lastSeenPos, predicted, _obstacleMask, out Vector2 pathAware))
                predicted = pathAware;

            // ── NavMesh Validation: snap จุดหมายลงบน NavMesh เสมอ ──
            // (กัน HasArrived เทียบกับพิกัดนอก mesh ที่ agent ไปไม่ถึง — ไปไม่ถึง → StuckDetector พาเข้าเฟสถัดไปช้ากว่าที่ควร)
            if (NavMesh2DUtility.TrySamplePosition(predicted, 3f, out Vector3 snapped))
                predicted = snapped;

            return predicted;
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

            MoveTowards(_pursuitTarget, currentPos, deltaTime);
        }

        private void StartSearchPattern(Vector2 center)
        {
            // ความจำยิ่งเก่า/คลาดเคลื่อน (confidence ต่ำ) → ค้นพื้นที่กว้างขึ้นเป็นพิเศษ
            // ("อาจจะอยู่แถวนั้น" = ต้องกระจายตัวค้นมากกว่าตอนที่เพิ่งเห็นสด ๆ)
            float uncertainty = 1f - Mathf.Clamp01(_blackboard.MemoryConfidence);
            float wanderRadius = _searchWanderRadius * Mathf.Lerp(1f, 1.7f, uncertainty);

            // ── Area Memory: จดจำ "พื้นที่ที่ผู้เล่นเพิ่งวิ่งผ่าน" (breadcrumb trail) แล้วค้นครอบคลุมทั้งเส้นทาง ──
            // ไม่ใช่แค่จุดสุดท้ายที่เห็น — กลางพื้นที่ = จุดกึ่งกลางระหว่างจุดที่มาถึงกับเส้นทางที่ผู้เล่นผ่านมา
            Vector2 areaCenter = center;
            float areaRadius = wanderRadius;
            if (_targetMemory != null && _targetMemory.TryGetArea(out Vector2 trailCenter, out float trailRadius))
            {
                areaCenter = Vector2.Lerp(center, trailCenter, 0.4f);
                areaRadius = Mathf.Min(
                    Mathf.Max(wanderRadius, trailRadius + 2f),
                    wanderRadius * 2.5f); // เพดานกันพื้นที่ค้นใหญ่ผิดปกติ
            }

            // แบ่งโซนค้นหา (หลายตัว Search พร้อมกัน → ไม่ค้นทับกันที่ทิศเดิมซ้ำ ๆ) ──
            Vector2 bias = _fleeDirection;
            if (GroupAI.Instance != null)
            {
                int sector = GroupAI.Instance.GetSearchSectorIndex(_self, out int totalSearchers);
                if (sector > 0 && totalSearchers > 1)
                {
                    float sectorAngle = 360f * sector / totalSearchers;
                    Vector2 baseDir = bias.sqrMagnitude > 0.01f ? bias : Vector2.up;
                    bias = RotateVector(baseDir, sectorAngle);
                }
            }

            _searchPattern = new SearchPattern(
                areaCenter, areaRadius, _obstacleMask, bias,
                lookDuration: 1.75f); // scan นิ่งหมุนสายตาตอนเริ่ม = 1.75×2 = 3.5 วินาที (อยู่ในช่วง 3–5 วิ ตามเกณฑ์)
            _subPhase = SubPhase.SearchPatternActive;
        }

        /// <summary>หมุนเวกเตอร์ในระนาบ XY ตามมุม (องศา) — ใช้แบ่งโซนค้นหา</summary>
        private static Vector2 RotateVector(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
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
                    MoveTowards(target, currentPos, deltaTime);
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
                    MoveTowards(center, currentPos, deltaTime);
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

        /// <summary>เดินไปจุดหมายผ่านระบบนำทาง Hybrid (NavMesh/steering) — state ไม่เขียน position เอง</summary>
        private void MoveTowards(Vector2 destination, Vector2 currentPos, float deltaTime)
        {
            // ── ความเร็วตาม "ความชัดของความจำ" (memory confidence) ──
            // จำได้ชัด (เพิ่งเห็น) → วิ่งเร็ว รีบไปจุดล่าสุด / ความจำเก่าคลาดเคลื่อน → ช้าลง ค้นอย่างระมัดระวัง
            // (หลักการเดียวกับเกม top-down: pursuit ที่ดีไม่ได้วิ่งเร็วเท่ากันตลอด แต่เร่งเมื่อมั่นใจ)
            float confidence = Mathf.Clamp01(_blackboard.MemoryConfidence);
            float speedScale = Mathf.Lerp(0.7f, 1.25f, confidence);
            float speed = _moveSpeed * speedScale;

            if (_mover != null)
            {
                _mover.MoveTo(destination, deltaTime, speed, arriveThreshold: 0.3f);
                return;
            }

            // fallback ปลอดภัย (ไม่ผ่าน EnemyBrain): steering เดิม
            Vector2 desiredDir = (destination - currentPos).normalized;
            Vector2 dir = SteeringMovement.GetSteeredDirection(currentPos, desiredDir, _obstacleMask, self: _self, personalSpace: _personalSpace);
            _self.position = SteeringMovement.MoveWithCollisionCheck(_self, dir * (speed * deltaTime), _obstacleMask);
            RotateTowards(dir);
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

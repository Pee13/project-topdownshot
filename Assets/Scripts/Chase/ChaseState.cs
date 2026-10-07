using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Memory;
using TopDownTacticalAI.Tactical;
using TopDownTacticalAI.Pathfinding;

namespace TopDownTacticalAI.Chase
{
    /// <summary>
    /// State ไล่ตามผู้เล่นขณะที่ยังมองเห็นอยู่
    /// ถ้ามองไม่เห็นแล้ว ระบบภายนอก (EnemyBrain) จะเปลี่ยนไป Search โดยใช้ตำแหน่งใน Memory
    /// ถ้าระยะใกล้พอและมีกระสุน จะเปลี่ยนไป Combat
    ///
    /// ถ้ามีศัตรูหลายตัวไล่ผู้เล่นพร้อมกัน (เช็คผ่าน GroupAI):
    /// - ตัวแรก (index 0) จะไล่ตรงเข้าหาผู้เล่นตามปกติ
    /// - ตัวอื่นๆ จะ "อ้อม" ไปดักคนละมุมรอบผู้เล่นแทน (ดู Flanking.cs) เพื่อปิดทางหนี ไม่ใช่วิ่งไล่ทับเส้นทางเดียวกันหมด
    ///
    /// การเคลื่อนที่ส่งผ่าน AgentPathMover (NavMesh อ้อมกำแพง หรือ steering fallback)
    /// </summary>
    public class ChaseState : IAIState
    {
        public EnemyState StateType => EnemyState.Chase;

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly AgentPathMover _mover;
        private readonly TargetMemory _targetMemory;
        private readonly LayerMask _obstacleMask;
        private readonly float _moveSpeed;
        private readonly float _predictionLeadTime;
        private readonly float _flankRadius;
        private readonly AIScoringWeights _weights;

        public ChaseState(Transform self, Blackboard blackboard, TargetMemory targetMemory, float moveSpeed, LayerMask obstacleMask, float predictionLeadTime = 0.3f, float flankRadius = 3f, float personalSpace = 1.3f, AIScoringWeights weights = null, AgentPathMover mover = null)
        {
            _self = self;
            _blackboard = blackboard;
            _targetMemory = targetMemory;
            _moveSpeed = moveSpeed;
            _obstacleMask = obstacleMask;
            _predictionLeadTime = predictionLeadTime;
            _flankRadius = flankRadius;
            _weights = weights;
            _mover = mover;
        }

        public void Enter() { }

        public void Tick(float deltaTime)
        {
            if (_blackboard.CurrentTarget == null)
            {
                // ── Intercept mode: ยังไม่เห็นผู้เล่นเอง แต่ "เพื่อนกำลังเห็นอยู่" (Active Contact) ──
                // วิ่งไปตำแหน่งที่เพื่อนเจอ + จัดวงล้อมตามทิศหนี — ปิดทางหนีแทนการไปค้นจุดเก่า
                if (GroupAI.Instance != null
                    && GroupAI.Instance.TryGetContact(out Vector2 contactPos)
                    && _blackboard.HasMemory)
                {
                    Vector2 interceptDest = GetDestinationWithFlanking(contactPos);
                    _blackboard.CurrentDestination = interceptDest;
                    _mover?.MoveTo(interceptDest, deltaTime, _moveSpeed, arriveThreshold: 0.5f);
                }
                return;
            }

            Vector2 targetPos = _blackboard.CurrentTarget.position;
            _targetMemory.Update(targetPos, deltaTime);

            // ทำนายตำแหน่งล่วงหน้าพร้อมความมั่นใจ
            // ถ้า AI ไม่มั่นใจในการทำนาย (เป้าหมายเปลี่ยนทิศบ่อย) ให้ถ่วงน้ำหนักกลับมาที่
            // ตำแหน่งปัจจุบันมากขึ้น แทนที่จะวิ่งไปจุดที่เดาผิด
            Vector2 predicted;
            float predictionConfidence;

            if (_weights != null)
            {
                predicted = TargetPrediction.PredictWithConfidence(
                    targetPos,
                    _targetMemory.LastVelocity,
                    _blackboard.DistanceToTarget,
                    _weights.estimatedTravelSpeed,
                    _weights.minPredictionTime,
                    _weights.maxPredictionTime,
                    _targetMemory.GetSmoothedDirection(),
                    out predictionConfidence);

                predicted = Vector2.Lerp(targetPos, predicted, predictionConfidence);
            }
            else
            {
                predicted = TargetPrediction.Predict(targetPos, _targetMemory.LastVelocity, _predictionLeadTime);
                predictionConfidence = 1f;
            }

            _blackboard.PredictionConfidence = predictionConfidence;
            _blackboard.PredictedTargetPosition = predicted;

            // ── Path-aware refinement ──
            // ถ้าจุดทำนายเส้นตรงชนกำแพง (ผู้เล่นวิ่งอ้อมมุม) ให้ประมาณตามเส้นทาง NavMesh จริง
            // แทนที่จะไล่ไปชนกำแพงตามเป้าปลอม — คืน false (ไม่มี mesh/ทางตรงโล่ง) ก็ใช้ผลเดิมต่อ
            if (TargetPrediction.PredictAlongPath(targetPos, predicted, _obstacleMask, out Vector2 pathAwarePredicted))
                predicted = pathAwarePredicted;

            Vector2 destination = GetDestinationWithFlanking(predicted);
            _blackboard.CurrentDestination = destination;

            // สั่งเดินผ่านระบบนำทาง Hybrid — ถ้าไม่มี mover (ไม่ผ่าน EnemyBrain) จะไม่ขยับ ไม่ log ทุกเฟรม
            _mover?.MoveTo(destination, deltaTime, _moveSpeed, arriveThreshold: 0.5f);

            // ── หันหน้าหาผู้เล่นตลอดตอนไล่/อ้อม (เฉพาะตอนที่เห็นผู้เล่นอยู่) ──
            // ตามคำสั่ง: ตอนอ้อม/โจมตี ให้ AI หันมาหาผู้เล่นเสมอ — ทิศ "เดิน" ใช้แค่ตอนไม่เห็นเป้า
            if (_blackboard.CanSeeTarget)
                FaceTowards(targetPos, deltaTime);
        }

        /// <summary>หันหน้าไปหาจุด world แบบหมุนเนียน (720°/วินาที)</summary>
        private void FaceTowards(Vector2 worldPos, float deltaTime)
        {
            Vector2 to = worldPos - (Vector2)_self.position;
            if (to.sqrMagnitude < 0.0001f) return;
            float targetAngle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - 90f;
            float smoothed = Mathf.MoveTowardsAngle(_self.eulerAngles.z, targetAngle, 720f * deltaTime);
            _self.rotation = Quaternion.Euler(0, 0, smoothed);
        }

        /// <summary>
        /// ถ้ามีเพื่อนไล่ผู้เล่นอยู่ด้วยหลายตัว ให้แบ่งบทบาท: ตัวหลักไล่ตรง ตัวอื่นอ้อมไปดักรอบๆ แทน
        /// </summary>
        private Vector2 GetDestinationWithFlanking(Vector2 predictedTargetPos)
        {
            if (GroupAI.Instance == null) return predictedTargetPos;

            // ── Tank (Defensive) "ไล่ตรงเสมอ" ไม่เข้าวงล้อมอ้อม ──
            // (แก้อาการในคลิป: มี Flanker ไล่ด้วย → Tank ได้ index>0 → วิ่งวนรอบผู้เล่นเป็นวง
            //  ดูเหมือนเดินวนอยู่ตรงบริเวณนั้น — บทบาท tank คือกดแนวหน้าตรงไป การอ้อมเป็นหน้าที่ Flanker)
            if (_blackboard.IsTankRole) return predictedTargetPos;

            int myIndex = GroupAI.Instance.GetActiveChaseIndex(_self, out int totalChasers);

            // index 0 (ตัวหลัก) หรือมีตัวเดียว -> ไล่ตรงตามปกติ ไม่ต้องอ้อม
            if (myIndex <= 0 || totalChasers <= 1)
                return predictedTargetPos;

            // ส่งทิศหนีของผู้เล่นไปด้วย — วงล้อมจะเอียงไปรอขวาง "ด้านหน้าทางหนี"
            Vector2 escapeDir = _targetMemory != null ? _targetMemory.GetSmoothedDirection() : Vector2.zero;
            return Flanking.GetFlankPosition(predictedTargetPos, myIndex, totalChasers, _flankRadius, escapeDir);
        }

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit() { }
    }
}

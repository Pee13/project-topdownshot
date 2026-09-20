using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Tactical;
using TopDownTacticalAI.Utilities;
using TopDownTacticalAI.Vision;

namespace TopDownTacticalAI.Support
{
    /// <summary>
    /// ถอยกลับไปหา Healer เมื่อเลือดน้อย (§18, §19)
    ///
    /// จะ "ไม่" เดินตรงไปหา Healer เสมอ เพราะผู้เล่นอาจยืนขวางอยู่
    /// จึงประเมิน 4 เส้นทาง (ตรง / ซ้าย / ขวา / ผ่านที่กำบัง) แล้วเลือกเส้นทางที่ปลอดภัยที่สุด (§20)
    ///
    /// RetreatScore =
    ///     Safety           × 35%   ← ไกลจากกระสุน + ไกลจากผู้เล่น
    ///   + Cover            × 20%   ← ผู้เล่นมองไม่เห็นจุดนั้น
    ///   + Distance         × 20%   ← พาไปใกล้ Healer มากกว่าเดิม
    ///   + PlayerAvoidance  × 15%   ← หนีออกห่างจากผู้เล่น
    ///   + PathLength       × 10%   ← ไม่อ้อมยาวเกินจนไปไม่ทัน
    ///
    /// พอถึงระยะฮีล TacticalDecision จะเปลี่ยนไป SeekHeal ให้เอง
    /// </summary>
    public class RetreatState : IAIState
    {
        public EnemyState StateType => EnemyState.Retreat;

        private readonly Transform _self;
        private readonly Blackboard _blackboard;
        private readonly float _moveSpeed;
        private readonly LayerMask _obstacleMask;
        private readonly float _playerAvoidDistance;
        private readonly float _stepDistance;

        public RetreatState(
            Transform self,
            Blackboard blackboard,
            float moveSpeed,
            LayerMask obstacleMask,
            float playerAvoidDistance,
            float stepDistance = 3f)
        {
            _self = self;
            _blackboard = blackboard;
            _moveSpeed = moveSpeed;
            _obstacleMask = obstacleMask;
            _playerAvoidDistance = playerAvoidDistance;
            _stepDistance = stepDistance;
        }

        public void Enter() { }

        public void Tick(float deltaTime)
        {
            Transform healer = _blackboard.HealerTransform;
            if (healer == null) return; // ไม่มี healer — รอ TacticalDecision ตัดสินใจใหม่เฟรมหน้า

            Vector2 selfPos = _self.position;
            Vector2 healerPos = healer.position;
            Vector2 playerPos = _blackboard.CurrentTarget != null
                ? (Vector2)_blackboard.CurrentTarget.position
                : selfPos;

            Vector2 bestStep = ScoreRetreatRoutes(selfPos, healerPos, playerPos);

            if (bestStep.sqrMagnitude < 0.0001f)
                bestStep = (healerPos - selfPos).normalized; // ไม่มีเส้นทางดีเลย → เดินตรงไปหา healer ก็ยังดีกว่ายืนตาย

            Vector2 steered = SteeringMovement.GetSteeredDirection(
                selfPos, bestStep, _obstacleMask, self: _self, personalSpace: 1.2f);

            if (steered.sqrMagnitude < 0.0001f) return; // ติดมุมอับ — อย่าฝืนเดินเข้ากำแพง

            // เดินแบบ "เช็คก่อนก้าว" — ระบบกลางเดียวกับ state อื่น กันมุดกำแพง
            _self.position = SteeringMovement.MoveWithCollisionCheck(
                _self, steered * (_moveSpeed * deltaTime), _obstacleMask);
            _blackboard.CurrentDestination = selfPos + steered * _moveSpeed;

            // หันหน้าตามทิศที่วิ่ง (ตอนถอยไม่ใช่ combat จึงไม่ต้องเล็งผู้เล่น)
            if (steered.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(steered.y, steered.x) * Mathf.Rad2Deg - 90f;
                _self.rotation = Quaternion.Euler(0, 0, angle);
            }
        }

        /// <summary>
        /// ประเมิน 4 เส้นทางหนี (§20) แล้วคืน "ทิศทาง" ของเส้นทางที่ปลอดภัยที่สุด
        /// </summary>
        private Vector2 ScoreRetreatRoutes(Vector2 selfPos, Vector2 healerPos, Vector2 playerPos)
        {
            Vector2 directDir = (healerPos - selfPos).normalized;

            // 4 เส้นทาง: ตรง / ซ้าย / ขวา / ทแยง (ผ่านที่กำบัง)
            Vector2 left = (directDir + Vector2.Perpendicular(directDir) * 0.8f).normalized;
            Vector2 right = (directDir - Vector2.Perpendicular(directDir) * 0.8f).normalized;
            Vector2 escapeDir = playerPos != selfPos
                ? ((Vector2)selfPos - playerPos).normalized
                : directDir;
            Vector2 coverRoute = ((directDir + escapeDir) * 0.5f).normalized;

            Vector2[] candidates = { directDir, left, right, coverRoute };

            float bestScore = float.MinValue;
            Vector2 bestDirection = Vector2.zero;
            bool found = false;

            foreach (Vector2 dir in candidates)
            {
                if (dir.sqrMagnitude < 0.0001f) continue;

                Vector2 point = selfPos + dir * _stepDistance;

                // ── Safety (35%) — ไกลจากผู้เล่น + ไม่มีกระสุนพุ่งมาแถวนั้น ──
                float playerDistance = Vector2.Distance(point, playerPos);
                float safety = Mathf.Clamp01(playerDistance / Mathf.Max(0.01f, _playerAvoidDistance * 2f));

                // ── Cover (20%) — ผู้เล่นมองไม่เห็นจุดนี้ ──
                float sight = Vector2.Distance(playerPos, point) + 1f;
                bool seen = RaycastDetector.HasLineOfSight(playerPos, point, _obstacleMask, sight);
                float cover = seen ? 0f : 1f;

                // ── Distance (20%) — พาไปใกล้ healer มากกว่าตำแหน่งเดิม ──
                float oldHealerDistance = Vector2.Distance(selfPos, healerPos);
                float newHealerDistance = Vector2.Distance(point, healerPos);
                float progress = Mathf.Clamp01((oldHealerDistance - newHealerDistance) / Mathf.Max(0.01f, _stepDistance) * 0.5f + 0.5f);

                // ── PlayerAvoidance (15%) — ทิศนั้นพาหนีออกจากผู้เล่น ──
                float playerAvoidance = playerPos != point
                    ? Mathf.Clamp01(Vector2.Dot(dir, ((Vector2)selfPos - playerPos).normalized) * 0.5f + 0.5f)
                    : 0.5f;

                // ── PathLength (10%) — ยิ่งสั้นยิ่งไปทัน ──
                float pathLength = 1f;

                float score = safety * 35f
                            + cover * 20f
                            + progress * 20f
                            + playerAvoidance * 15f
                            + pathLength * 10f;

                // จุดที่ไปไม่ได้ (กำแพง/อยู่ในสิ่งกีดขวาง) ตัดทิ้ง
                if (PhysicsUtility.IsPositionBlocked(point, 0.7f, _obstacleMask)) continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDirection = dir;
                    found = true;
                }
            }

            return found ? bestDirection : Vector2.zero;
        }

        public void FixedTick(float fixedDeltaTime) { }

        public void Exit() { }
    }
}

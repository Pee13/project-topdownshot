using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Player;
using TopDownTacticalAI.Utilities;
using TopDownTacticalAI.Vision;
using TopDownTacticalAI.Pathfinding; // NavMesh2DUtility.PathLength

namespace TopDownTacticalAI.Tactical
{
    /// <summary>
    /// เลือก "เป้าหมายที่ควรฮีล" ด้วยคะแนน 0–100 (§11, §14, §15, §16)
    ///
    /// HealScore =
    ///     RolePriority   × 35%   ← ตัวไหนสำคัญต่อทีมมากกว่า (Support สูงสุด, Tank รอง)
    ///   + MissingHP      × 25%   ← เลือดที่หายไปเท่าไหร่
    ///   + CriticalHP     × 20%   ← เลือดใกล้ตายหรือเปล่า (≤25% ถือว่าวิกฤต)
    ///   + Distance       × 10%   ← ยิ่งใกล้ยิ่งฮีลทัน
    ///   + Accessibility  × 10%   ← มองเห็น/เดินไปถึงได้จริงไหม
    ///
    /// กฎพิเศษตาม §16:
    ///   - Tank (บทบาท Defensive) เลือดต่ำกว่า 80% → ได้โบนัสความสำคัญ (Tank-first)
    ///   - Tank เลือด ≥ 80% → ไม่เข้าเป็นเป้าหมาย ให้พิจารณาเพื่อนตัวอื่น
    ///   - เพื่อนที่วิกฤต (≤25%) สามารถชนะ Tank ที่เลือดยังไม่วิกฤตได้
    ///
    /// ทุกตัวเลขเป็น 0–100 (ไม่ normalize ด้วยจำนวนเป้าหมาย) เพื่อให้ตั้ง threshold
    /// "คะแนนเท่าไหร่ถึงคุ้มที่จะฮีล" ได้ตรงๆ
    /// </summary>
    public static class HealTargetScorer
    {
        // น้ำหนัก (รวมกันได้ 100) — ปรับตามนโยบายใหม่: "เน้นตัวที่ HP% น้อยสุดก่อน เป็นพิเศษ"
        // (เดิมตาม §15: Role35/Missing25/Critical20/Dist10/Access10 — บทบาทเด่นกว่า HP จนบางทีไปฮีล
        //  ตัวเลือดดีกว่าตัวใกล้ตาย)
        private const float WeightRolePriority = 10f;   // บทบาท = ตัวตัดสินใจเมื่อ HP ใกล้เคียงกันเท่านั้น
        private const float WeightMissingHP = 55f;      // HP% ที่หายไป = ตัวชี้ขาดอันดับหนึ่ง
        private const float WeightCriticalHP = 15f;     // โบนัสเร่งเมื่อใกล้ตาย (ทวีคูณ "ตัวน้อยสุด")
        private const float WeightDistance = 10f;
        private const float WeightAccessibility = 10f;

        // ── โบนัส/โทษ (ตามแนวทางปรับปรุง) ──
        private const float ApproachBonus = 12f;     // เพื่อน "กำลังเดินเข้าหา healer" → เจอกันเร็ว = คุ้มกว่า
        private const float DetourPenalty = 25f;     // (พับเข้า hard filter ระยะแล้ว — เก็บไว้ถ้าอยากผ่อนปรนภายหลัง)

        /// <summary>
        /// มีเพื่อนที่ "ควรได้รับการฮีล" อยู่ไหม — ใช้เป็น precondition ก่อนเข้า state ฮีล
        /// </summary>
        /// <param name="self">ตัว healer เอง (ไม่ฮีลตัวเอง)</param>
        /// <param name="healWorthThreshold">ฮีลเฉพาะเพื่อนที่เลือดต่ำกว่านี้ (0..1)</param>
        /// <param name="maxDistance">ระยะสูงสุดที่ยอมถือว่า "คุ้มเดินไปฮีล" (เส้นตรง — คัดเข้มกว่าตอน Score ที่ใช้เส้นทาง NavMesh)</param>
        public static bool HasWoundedAlly(Transform self, float healWorthThreshold, float maxDistance = float.MaxValue)
        {
            if (GroupAI.Instance == null) return false;

            foreach (var ally in GroupAI.Instance.ActiveEnemies)
            {
                if (ally == null || ally == self) continue;
                if (!ally.TryGetComponent(out Health health)) continue;
                if (health.IsDead || health.CurrentHP <= 0f) continue;

                float hpPercent = MathUtility.HealthPercent(health.CurrentHP, health.MaxHP);
                if (hpPercent >= healWorthThreshold) continue;

                // ไกลเกินระยะทำการ → ไม่นับ (กันเข้า HealAlly แล้วหาเป้าไม่เจอ → state flapping ทุกเฟรม)
                if (maxDistance < float.MaxValue
                    && Vector2.Distance(self.position, ally.position) > maxDistance)
                    continue;

                return true;
            }
            return false;
        }

        /// <summary>
        /// เลือกเป้าหมายฮีลที่คะแนนดีที่สุด
        /// </summary>
        /// <param name="self">ตัว healer เอง</param>
        /// <param name="healWorthThreshold">ฮีลเฉพาะเพื่อนที่เลือดต่ำกว่านี้ (0..1)</param>
        /// <param name="tankHealThreshold">ถ้า Tank เลือดต่ำกว่านี้ให้โบนัส Tank-first (0..1)</param>
        /// <param name="criticalHealthThreshold">เกณฑ์ "เลือดวิกฤต" (0..1)</param>
        /// <param name="maxHealDistance">ระยะมาตรฐานที่ใช้คิดคะแนนระยะ</param>
        /// <param name="obstacleMask">Layer กำแพง (ใช้ตรวจ LoS / Accessibility)</param>
        /// <param name="target">ผลลัพธ์: เป้าหมายที่ควรฮีล</param>
        /// <param name="targetHealth">ผลลัพธ์: Health ของเป้าหมาย</param>
        /// <param name="score">ผลลัพธ์: คะแนน 0–100</param>
        /// <returns>false ถ้าไม่มีเป้าหมายที่ควรฮีลเลย</returns>
        public static bool TrySelectHealTarget(
            Transform self,
            float healWorthThreshold,
            float tankHealThreshold,
            float criticalHealthThreshold,
            float maxHealDistance,
            LayerMask obstacleMask,
            out Transform target,
            out Health targetHealth,
            out float score)
        {
            target = null;
            targetHealth = null;
            score = 0f;

            if (GroupAI.Instance == null) return false;

            Vector2 selfPos = self != null ? (Vector2)self.position : Vector2.zero;
            float bestScore = 0f;
            bool found = false;

            foreach (var ally in GroupAI.Instance.ActiveEnemies)
            {
                if (ally == null || ally == self) continue;

                float candidate = ScoreHealCandidate(
                    self, ally, selfPos, healWorthThreshold, tankHealThreshold,
                    criticalHealthThreshold, maxHealDistance, obstacleMask);

                if (candidate > bestScore)
                {
                    bestScore = candidate;
                    target = ally;
                    targetHealth = ally.TryGetComponent(out Health h) ? h : null;
                    if (targetHealth != null) found = true;
                }
            }

            if (found) score = Mathf.Clamp(bestScore, 0f, 100f);
            return found;
        }

        /// <summary>
        /// ให้คะแนน "ผู้สมัครเดี่ยว" ว่าควรฮีลตัวนี้แค่ไหน (0–100) — คืนต่ำกว่า 0 = ไม่ผ่านเกณฑ์ (ข้าม)
        /// แยกออกมาเพื่อให้ HealAllyState เอามาคำนวณคะแนน "เป้าหมายปัจจุบัน" ตอนเทียบ Hysteresis ได้
        /// (สองฝั่งใช้สูตรเดียวกัน → การเปรียบเทียบยุติธรรม ไม่ใช่ประมาณเอา)
        ///
        /// ปรับปรุงตามแนวทางใหม่:
        /// - ระยะใช้ "ความยาวเส้นทาง NavMesh จริง" (หลบสิ่งกีดขวางแล้ว) ไม่ใช่ระยะเส้นตรง
        /// - โทษ DetourPenalty ถ้าเส้นทางอ้อมยาวเกินขอบเขต (ไปยาก = หักคะแนนทันที)
        /// - โบนัส ApproachBonus ถ้าเพื่อนกำลังเดินเข้าหา healer (เจอกันเร็ว = คุ้มกว่า)
        /// </summary>
        public static float ScoreHealCandidate(
            Transform self,
            Transform ally,
            Vector2 selfPos,
            float healWorthThreshold,
            float tankHealThreshold,
            float criticalHealthThreshold,
            float maxHealDistance,
            LayerMask obstacleMask)
        {
            if (self == null || ally == null || ally == self) return -1f;
            if (!ally.TryGetComponent(out Health health)) return -1f;
            if (health.IsDead || health.CurrentHP <= 0f) return -1f;

            float hpPercent = MathUtility.HealthPercent(health.CurrentHP, health.MaxHP);
            if (hpPercent >= healWorthThreshold) return -1f; // เลือดยังดีพอ ไม่ใช่เป้าหมาย

            // ── RolePriority (35) + Tank-first (§16) ──
            float rolePriority = 0f;
            EnemyBrain brain = null;
            if (ally.TryGetComponent(out brain))
                rolePriority = AIActionScorer.GetRoleImportance(brain.Role) * WeightRolePriority;

            if (brain != null && brain.Role == EnemyRole.Defensive && hpPercent < tankHealThreshold)
                rolePriority += TankGuardBonus;

            float missingHp = MathUtility.MissingHealth(health.CurrentHP, health.MaxHP) * WeightMissingHP;
            float criticalHp = MathUtility.CriticalBonus(hpPercent, criticalHealthThreshold) * WeightCriticalHP;

            // ── Distance (10) — ด้วย "ความยาวเส้นทางเดินจริง" (NavMesh Path Length) ──
            Vector2 allyPos = ally.position;
            float euclidean = Vector2.Distance(selfPos, allyPos);
            float pathLength = NavMesh2DUtility.PathLength(selfPos, allyPos, fallback: euclidean);

            // เกินขอบเขตทำการ → ตัดทิ้ง (ใช้ "ระยะเส้นตรง" ให้ตรงกับ HasWoundedAlly ที่ใช้เข้า state)
            // เดิมใช้ pathLength → ประตูสองจุดไม่ตรงกัน (เข้าด้วยตรง แต่คัดออกด้วย path ที่อ้อม/ไม่เสถียร)
            // → ฮีลเดินไป96 หน่วยแล้ว "ไม่มีเป้า" กลางทาง วนเข้า-ออก state ไม่หยุด (บั๊กในคลิป)
            if (euclidean > maxHealDistance) return -1f;

            // คะแนนระยะยังคงใช้เส้นทางจริง (NavMesh Path Length) — ยิ่งอ้อมยิ่งคะแนนต่ำ = ไม่คุ้มเดิน
            float distanceScore = MathUtility.DistanceScore(pathLength, Mathf.Max(0.01f, maxHealDistance)) * WeightDistance;

            // ── Accessibility (10) — LOS ไม่นับ collider ตัวเรา/เพื่อนเป้าหมาย ──
            Collider2D selfCol = self.GetComponent<Collider2D>();
            Collider2D allyCol = ally.GetComponent<Collider2D>();
            bool hasAccess = RaycastDetector.HasLineOfSightIgnoring(
                selfPos, allyPos, obstacleMask, euclidean + 1f, selfCol, allyCol);
            float accessibility = hasAccess ? WeightAccessibility : 0f;

            float total = rolePriority + missingHp + criticalHp + distanceScore + accessibility;

            // ── โบนัสเพื่อน "กำลังเดินเข้าหา" healer — ใช้เวลาไปพบกันน้อยที่สุด ──
            if (brain != null)
            {
                Vector2 allyVel = brain.VelocityEstimate;
                Vector2 toHealer = selfPos - allyPos;
                if (allyVel.sqrMagnitude > 1f && toHealer.sqrMagnitude > 0.01f
                    && Vector2.Dot(allyVel.normalized, toHealer.normalized) > 0.3f)
                {
                    total += ApproachBonus;
                }
            }

            // ── โทษเส้นทางอ้อมยาวเกินขอบเขต — ถูกแทนด้วย hard filter ด้านบนแล้ว (pathLength > max → ตัดทิ้ง)
            // (เก็บไว้เป็นกรณีอยากผ่อนปรน: if (pathLength > maxHealDistance * 1.5f) total -= DetourPenalty;)

            return total;
        }

        /// <summary>
        /// โบนัส Tank-first (§16) — ปรับลดเหลือแค่ "tiebreak" (หน่วยคะแนนเต็ม 100)
        /// ตามนโยบายใหม่: HP% น้อยสุดต้องชนะเสมอ — bonus ต้องเล็กพอที่ไม่พลิกสถานการณ์
        /// (เช่น Tank 40% ยังแพ้ Flanker 35% ได้จริง) — ใช้ตัดสินเฉพาะเมื่อ HP แทบเท่ากัน
        /// </summary>
        private const float TankGuardBonus = 3f;
    }
}

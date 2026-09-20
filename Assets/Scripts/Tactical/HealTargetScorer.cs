using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Player;
using TopDownTacticalAI.Utilities;
using TopDownTacticalAI.Vision;

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
        // น้ำหนักตาม §15 (รวมกันได้ 100)
        private const float WeightRolePriority = 35f;
        private const float WeightMissingHP = 25f;
        private const float WeightCriticalHP = 20f;
        private const float WeightDistance = 10f;
        private const float WeightAccessibility = 10f;

        /// <summary>
        /// มีเพื่อนที่ "ควรได้รับการฮีล" อยู่ไหม — ใช้เป็น precondition ก่อนเข้า state ฮีล
        /// </summary>
        /// <param name="self">ตัว healer เอง (ไม่ฮีลตัวเอง)</param>
        /// <param name="healWorthThreshold">ฮีลเฉพาะเพื่อนที่เลือดต่ำกว่านี้ (0..1)</param>
        public static bool HasWoundedAlly(Transform self, float healWorthThreshold)
        {
            if (GroupAI.Instance == null) return false;

            foreach (var ally in GroupAI.Instance.ActiveEnemies)
            {
                if (ally == null || ally == self) continue;
                if (!ally.TryGetComponent(out Health health)) continue;
                if (health.IsDead || health.CurrentHP <= 0f) continue;

                float hpPercent = MathUtility.HealthPercent(health.CurrentHP, health.MaxHP);
                if (hpPercent < healWorthThreshold) return true;
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
                if (!ally.TryGetComponent(out Health health)) continue;
                if (health.IsDead || health.CurrentHP <= 0f) continue;

                float hpPercent = MathUtility.HealthPercent(health.CurrentHP, health.MaxHP);

                // ฮีลเฉพาะเพื่อนที่เลือดต่ำกว่าเกณฑ์ (กัน healer ไล่ตามฮีลคนที่เลือดเกือบเต็ม)
                if (hpPercent >= healWorthThreshold) continue;

                // ── RolePriority (35) ──
                float rolePriority = 0f;
                if (ally.TryGetComponent(out EnemyBrain brain))
                    rolePriority = AIActionScorer.GetRoleImportance(brain.Role) * WeightRolePriority;

                // Tank-first (§16): Tank เลือดต่ำกว่าเกณฑ์ → เพิ่มความสำคัญพิเศษ
                bool isTankGuard = brain != null
                    && brain.Role == EnemyRole.Defensive
                    && hpPercent < tankHealThreshold;
                if (isTankGuard)
                    rolePriority += TankGuardBonus;

                // ── MissingHP (25) ──
                float missingHp = MathUtility.MissingHealth(health.CurrentHP, health.MaxHP) * WeightMissingHP;

                // ── CriticalHP (20) ──
                float criticalHp = MathUtility.CriticalBonus(hpPercent, criticalHealthThreshold) * WeightCriticalHP;

                // ── Distance (10) ──
                float distance = Vector2.Distance(selfPos, ally.position);
                float distanceScore = MathUtility.DistanceScore(distance, Mathf.Max(0.01f, maxHealDistance)) * WeightDistance;

                // ── Accessibility (10) ──
                // มองเห็น/เดินไปถึงได้จริง — ถ้ากำแพงบัง ฮีลไม่ได้ (ยิงลำแสงไม่ผ่าน)
                bool hasAccess = RaycastDetector.HasLineOfSight(
                    selfPos, ally.position, obstacleMask, distance + 1f);
                float accessibility = hasAccess ? WeightAccessibility : 0f;

                float total = rolePriority + missingHp + criticalHp + distanceScore + accessibility;

                if (total > bestScore)
                {
                    bestScore = total;
                    target = ally;
                    targetHealth = health;
                    found = true;
                }
            }

            if (found) score = Mathf.Clamp(bestScore, 0f, 100f);
            return found;
        }

        /// <summary>
        /// โบนัส Tank-first (§16) — หน่วยคะแนนเต็ม 100
        /// ตั้งให้พอเหมาะที่ Tank เลือดประมาณ 55% จะยังชนะ Flanker ที่เลือด 20%
        /// แต่ Flanker ที่ใกล้ตายมาก (≤10%) จะยังชนะ Tank ได้ตามกฎ Critical ที่มีความเสี่ยงเสียชีวิตทันที
        /// </summary>
        private const float TankGuardBonus = 15f;
    }
}

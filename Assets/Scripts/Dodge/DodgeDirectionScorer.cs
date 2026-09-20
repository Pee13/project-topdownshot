using UnityEngine;
using TopDownTacticalAI.Tactical;
using TopDownTacticalAI.Utilities;
using TopDownTacticalAI.Vision;

namespace TopDownTacticalAI.Dodge
{
    /// <summary>
    /// เลือก "ทิศทางการหลบ" ด้วยการให้คะแนน แทนการสุ่มซ้าย/ขวา (§8, §18)
    ///
    /// สร้างทิศผู้สมัคร 5 ทิศรอบแนวกระสุน (ซ้ายตั้งฉาก / ขวาตั้งฉาก / ถอยหลังจากกระสุน /
    /// ทแยง 45° ซ้าย / ทแยง 45° ขวา) แล้วให้คะแนนแต่ละทิศจากสถานการณ์จริง:
    ///
    ///   ProjectileSafety 40% — ทำนายตำแหน่งกระสุนที่ t = 0.15/0.30/0.45s แล้วดูว่า
    ///                        จุดหลบหลุดจากแนววิถีกระสุนแค่ไหน
    ///   WallClearance   25% — จุดหลบต้องไม่โดนกำแพง/อยู่ในกำแพง
    ///   AllyClearance   15% — ไม่ควรหลบไปชนเพื่อน (หลบไปรุมกันที่เดียวกันจะโดนรวดเดียว)
    ///   AwayFromPlayer  10% — ยิ่งหนีออกห่างจากผู้เล่นยิ่งดี
    ///   CoverBonus      10% — จุดหลบที่ผู้เล่นมองไม่เห็นได้โบนัส
    ///
    /// หลักการ (§41): สุ่มได้เฉพาะตอน "คะแนนใกล้เคียงกันมาก" เท่านั้น เพื่อไม่ให้
    /// หลบทางเดียวกันทุกครั้งจนผู้เล่นจำทางได้ — ไม่ใช่สุ่มเพื่อตัดสินใจ
    /// </summary>
    public static class DodgeDirectionScorer
    {
        // น้ำหนัก (รวมกันได้ 1.0)
        private const float WeightProjectileSafety = 0.40f;
        private const float WeightWallClearance = 0.25f;
        private const float WeightAllyClearance = 0.15f;
        private const float WeightAwayFromPlayer = 0.10f;
        private const float WeightCoverBonus = 0.10f;

        // คะแนนต่างกันน้อยกว่านี้ถือว่า "เสมอ" — ใช้สุ่มตัดสินได้ (§41)
        private const float TieBreakThreshold = 0.02f;

        // ช่วงเวลาที่ใช้ทำนายตำแหน่งกระสุน (วินาที)
        private static readonly float[] PredictionTimes = { 0.15f, 0.30f, 0.45f };

        /// <summary>
        /// หาทิศหลบที่ปลอดภัยที่สุด
        /// </summary>
        /// <param name="origin">ตำแหน่งตัวเอง</param>
        /// <param name="bulletPosition">ตำแหน่งกระสุน</param>
        /// <param name="bulletVelocity">ความเร็วกระสุน</param>
        /// <param name="dodgeDistance">ระยะที่จะหลบ (หน่วยโลก)</param>
        /// <param name="obstacleMask">Layer กำแพง/สิ่งกีดขวาง</param>
        /// <param name="playerPosition">ตำแหน่งผู้เล่น</param>
        /// <param name="bestDirection">ผลลัพธ์: ทิศที่ควรหลบ (normalized)</param>
        /// <param name="bestScore">ผลลัพธ์: คะแนนของทิศนั้น 0..1</param>
        /// <returns>false ถ้าทุกทิศไปไม่ได้เลย (โดนล้อม) — ผู้เรียกควรไม่หลบ</returns>
        public static bool TryFindBestDodgeDirection(
            Vector2 origin,
            Vector2 bulletPosition,
            Vector2 bulletVelocity,
            float dodgeDistance,
            LayerMask obstacleMask,
            Vector2 playerPosition,
            out Vector2 bestDirection,
            out float bestScore)
        {
            bestDirection = Vector2.zero;
            bestScore = 0f;

            if (bulletVelocity.sqrMagnitude < 0.0001f || dodgeDistance <= 0f) return false;

            Vector2 bulletDir = bulletVelocity.normalized;

            // ตั้งฉากกับแนวกระสุน = ทิศหลบมาตรฐาน (ออกจากแนววิถีเร็วที่สุด)
            Vector2 perpendicular = new Vector2(-bulletDir.y, bulletDir.x);

            // ทิศผู้สมัคร: ซ้าย / ขวา / ถอยหลังจากกระสุน / ทแยง 45° ซ้าย-ขวา
            Vector2[] candidates =
            {
                perpendicular,
                -perpendicular,
                -bulletDir,
                (perpendicular - bulletDir * 0.6f).normalized,
                (-perpendicular - bulletDir * 0.6f).normalized,
            };

            float topScore = float.MinValue;
            float runnerUp = float.MinValue;
            Vector2 topDirection = Vector2.zero;
            bool anyValid = false;

            foreach (Vector2 dir in candidates)
            {
                if (dir.sqrMagnitude < 0.0001f) continue;
                dir.Normalize();

                // §18 — ทางไปจุดหลบต้องเดินถึงจริง: ถ้ามีกำแพงขวางระหว่างตัวกับจุดหลบ
                // ทิศนั้นใช้ไม่ได้ (เดินชนกำแพงแทนหลบ) ตัดทิ้งก่อนคิดคะแนนอื่น
                if (IsPathToDodgeBlocked(origin, dir, dodgeDistance, obstacleMask)) continue;

                float score = ScoreDirection(
                    origin, dir, bulletPosition, bulletVelocity,
                    dodgeDistance, obstacleMask, playerPosition);

                // ทิศที่ "ยังโดนกระสุนอยู่" ถือว่าใช้ไม่ได้เลย ตัดทิ้งตั้งแต่ตรงนี้
                if (score <= 0f) continue;

                anyValid = true;

                if (score > topScore)
                {
                    runnerUp = topScore;
                    topScore = score;
                    topDirection = dir;
                }
                else if (score > runnerUp)
                {
                    runnerUp = score;
                }
            }

            if (!anyValid) return false;

            bestScore = topScore;
            bestDirection = topDirection;

            // §41 — สุ่มตัดสินได้เฉพาะเมื่อสองทิศดีพอกันจริงๆ (tie-breaker ไม่ใช่ผู้ตัดสินใจ)
            if (runnerUp > 0f && (topScore - runnerUp) < TieBreakThreshold && Random.value > 0.5f)
            {
                // สุ่มใหม่ระหว่างสองทิศที่ใกล้เคียงกัน เพื่อไม่ให้หลบทางเดิมทุกครั้งจนจำทางได้
                foreach (Vector2 dir in candidates)
                {
                    if (dir.sqrMagnitude < 0.0001f) continue;
                    Vector2 n = dir.normalized;
                    float score = ScoreDirection(origin, n, bulletPosition, bulletVelocity, dodgeDistance, obstacleMask, playerPosition);
                    if (score > 0f && Mathf.Abs(score - topScore) < TieBreakThreshold)
                    {
                        bestDirection = n;
                        break;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// ตรวจว่า "ทางเดินไปจุดหลบ" ติดกำแพงหรือไม่ — ยิง CircleCast จากตัวไปตามทิศหลบ
        /// (ไม่ใช่แค่จุดหลบโล่ง แต่ "ทางเดินไปถึง" ต้องโล่งด้วย ไม่งั้นเดินชนกำแพง)
        /// </summary>
        private static bool IsPathToDodgeBlocked(Vector2 origin, Vector2 direction, float dodgeDistance, LayerMask obstacleMask)
        {
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = obstacleMask,
                useTriggers = false
            };
            var hits = new RaycastHit2D[2];

            // ใช้วงกลมครึ่งหนึ่งของขนาดตัว — กันตันเกินจริงจนหลบไม่ได้สักทิศ
            int count = Physics2D.CircleCast(origin, 0.6f, direction, filter, hits, dodgeDistance);
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null) return true;

            return false;
        }

        /// <summary>
        /// ให้คะแนนทิศหลบหนึ่งทิศ 0..1
        /// คืน 0 ถ้าทิศนั้น "ยังโดนกระสุนอยู่" — หลบไปแล้วโดนอีกไม่มีประโยชน์
        /// </summary>
        private static float ScoreDirection(
            Vector2 origin,
            Vector2 direction,
            Vector2 bulletPosition,
            Vector2 bulletVelocity,
            float dodgeDistance,
            LayerMask obstacleMask,
            Vector2 playerPosition)
        {
            Vector2 dodgePoint = origin + direction * dodgeDistance;

            // ── 1) ProjectileSafety (40%) ──
            // ทำนายว่ากระสุนจะอยู่ตรงไหนในอีก 0.15/0.30/0.45 วินาที
            // แล้วดูว่าจุดหลบของเราห่างจากแนววิถีกระสุนพอหรือไม่
            float bodyRadius = 0.5f;
            float safetySum = 0f;

            foreach (float t in PredictionTimes)
            {
                Vector2 predictedBullet = BulletDetector.PredictBulletPosition(bulletPosition, bulletVelocity, t);
                float missDistance = BulletDetector.ClosestApproachDistance(dodgePoint, predictedBullet, bulletVelocity);

                // 1.0 = หลุดไกลกว่า 2 เท่าของขนาดตัว, 0 = ยังอยู่ในรัศมีที่โดน
                safetySum += Mathf.Clamp01((missDistance - bodyRadius) / (bodyRadius * 2f));
            }
            float projectileSafety = safetySum / PredictionTimes.Length;

            // ถ้าหลบไปแล้วยังโดนชัดๆ → ทิศนี้ใช้ไม่ได้
            if (projectileSafety <= 0.05f) return 0f;

            // ── 2) WallClearance (25%) ──
            // จุดหลบต้องเป็นที่ที่ยืนได้จริง (ไม่อยู่ในกำแพง และมีที่ว่างรอบๆ พอ)
            float wallClearance = PhysicsUtility.IsPositionBlocked(dodgePoint, 0.8f, obstacleMask) ? 0f : 1f;

            // ── 3) AllyClearance (15%) ──
            // หลบไปทับเพื่อน = โดนรวดเดียวพร้อมกัน
            float allyCount = GroupAI.Instance != null
                ? GroupAI.Instance.CountNearby(dodgePoint, 1.6f, null)
                : 0f;
            float allyClearance = Mathf.Clamp01(1f - allyCount * 0.5f);

            // ── 4) AwayFromPlayer (10%) ──
            // ยิ่งจุดหลบห่างจากผู้เล่นมากกว่าตำแหน่งเดิม ยิ่งดี (ผู้เล่นเล็งใหม่ยากขึ้น)
            float oldPlayerDistance = Vector2.Distance(origin, playerPosition);
            float newPlayerDistance = Vector2.Distance(dodgePoint, playerPosition);
            float awayFromPlayer = Mathf.Clamp01((newPlayerDistance - oldPlayerDistance) / (dodgeDistance * 2f) + 0.5f);

            // ── 5) CoverBonus (10%) ──
            // จุดหลบที่ผู้เล่นมองไม่เห็น = โบนัส (ยิงตามไม่ทัน)
            float sightDistance = Vector2.Distance(playerPosition, dodgePoint) + 1f;
            bool seenByPlayer = RaycastDetector.HasLineOfSight(playerPosition, dodgePoint, obstacleMask, sightDistance);
            float coverBonus = seenByPlayer ? 0f : 1f;

            float score = projectileSafety * WeightProjectileSafety
                        + wallClearance * WeightWallClearance
                        + allyClearance * WeightAllyClearance
                        + awayFromPlayer * WeightAwayFromPlayer
                        + coverBonus * WeightCoverBonus;

            return Mathf.Clamp01(score);
        }
    }
}

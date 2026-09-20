using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Utilities;
using TopDownTacticalAI.Vision;

namespace TopDownTacticalAI.Tactical
{
    /// <summary>
    /// ระบบให้คะแนน "ตำแหน่งยืน" แบบไม่สุ่ม (§5, §6, §15, §16)
    ///
    /// สร้าง Candidate Position เป็นวงรอบตัว (8 ทิศ × 2 รัศมี) แล้วให้คะแนนทุกจุด
    /// ตามน้ำหนักที่แตกต่างกันตาม Role (§16) จากนั้นหัก Danger (§7)
    ///
    /// Tank (§8)   : ProtectionAngle 30% + BlockPotential 25% + DistanceToHealer 15%
    ///               + Cover 10% + PlayerThreat 10% + AttackPotential 5% + MovementSafety 5%
    /// Flanker(§11): FlankAngle 25% + AttackPotential 20% + Safety 20% + CombatDistance 15%
    ///               + EscapeRoute 10% + Cover 10%
    /// Healer(§16) : Safety 30% + TankProtection 20% + Cover 20% + HealingAccess 15%
    ///               + PlayerDistance 10% + EscapeRoute 5%
    ///
    /// คะแนนรวม 0–100 (§5/§15) และจุดที่คะแนน < 25 ถือว่าอันตรายเกินไป ตัดทิ้ง (§7)
    ///
    /// หลักการ (§50): ทุกจุดคะแนนมาจากข้อมูลที่ตัวเอง "รับรู้ได้จริง" —
    /// ตำแหน่งผู้เล่นใช้จากสายตา/ความจำ ไม่ใช่รู้ทุกที่
    /// </summary>
    public static class EnemyPositionScorer
    {
        private const int CandidateDirections = 8;
        private static readonly float[] CandidateRadiusFactors = { 0.55f, 1.0f };

        // คะแนนขั้นต่ำ — จุดที่ต่ำกว่านี้ถือว่าอันตรายเกิน ห้ามเลือก (§7)
        private const float MinimumViableScore = 25f;

        // ความกว้าง "ทางเดินบัง" ระหว่าง Tank กับเป้าหมายที่ถือว่ายังบังอยู่ (หน่วยโลก)
        private const float BlockCorridorWidth = 4f;

        /// <summary>
        /// หาตำแหน่งยืนที่ดีที่สุดรอบตัว ตามบทบาท (§5/§16)
        /// </summary>
        /// <param name="self">ตัวผู้เดินเอง</param>
        /// <param name="role">บทบาท — กำหนดน้ำหนักการให้คะแนน</param>
        /// <param name="playerPos">ตำแหน่งผู้เล่นที่ตัวเองรับรู้ได้ (สายตา/ความจำ)</param>
        /// <param name="healerPos">ตำแหน่ง Healer (Tank ใช้คำนวณจุดบัง)</param>
        /// <param name="protectDistance">ระยะที่ Tank ควรยืนห่างจาก Healer ตอนบัง</param>
        /// <param name="obstacleMask">Layer กำแพง</param>
        /// <param name="searchRadius">รัศมีค้นหาจุดรอบตัว</param>
        /// <param name="bestPosition">ผลลัพธ์: ตำแหน่งที่คะแนนดีที่สุด</param>
        /// <param name="bestScore">ผลลัพธ์: คะแนน 0–100</param>
        /// <returns>false ถ้าไม่มีจุดไหนปลอดภัยพอ (โดนล้อมสนิท)</returns>
        public static bool TryFindBestPosition(
            Transform self,
            EnemyRole role,
            Vector2 playerPos,
            Vector2 playerFacing,
            Vector2 healerPos,
            float protectDistance,
            LayerMask obstacleMask,
            float searchRadius,
            out Vector2 bestPosition,
            out float bestScore)
        {
            bestPosition = Vector2.zero;
            bestScore = 0f;

            Vector2 selfPos = self != null ? (Vector2)self.position : Vector2.zero;
            float bodyRadius = SteeringMovement.GetBodyRadius(self);

            float bestTotal = float.MinValue;
            bool found = false;

            // สร้าง candidates: 8 ทิศ × 2 รัศมี = 16 จุด (§5)
            for (int d = 0; d < CandidateDirections; d++)
            {
                float angle = d * (360f / CandidateDirections) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                foreach (float radiusFactor in CandidateRadiusFactors)
                {
                    Vector2 candidate = selfPos + dir * (searchRadius * radiusFactor);

                    // จุดที่เดินไปไม่ถึง (ในกำแพง/นอกขอบ) ตัดทิ้งก่อนเป็นอันดับแรก
                    if (PhysicsUtility.IsPositionBlocked(candidate, bodyRadius, obstacleMask)) continue;

                    float score = ScoreCandidate(
                        role, candidate, selfPos, playerPos, playerFacing, healerPos,
                        protectDistance, obstacleMask, searchRadius, bodyRadius);

                    if (score > bestTotal)
                    {
                        bestTotal = score;
                        bestPosition = candidate;
                        found = true;
                    }
                }
            }

            if (!found) return false;

            // จุดที่อันตรายเกินไป (§7 — TotalScore < 25 ไม่ควรเลือก)
            if (bestTotal < MinimumViableScore) return false;

            bestScore = Mathf.Clamp(bestTotal, 0f, 100f);
            return true;
        }

        /// <summary>
        /// ให้คะแนนจุดหนึ่ง ๆ ตามบทบาท (§8/§11/§16) + หัก Danger (§7) — คืน 0–100
        /// </summary>
        private static float ScoreCandidate(
            EnemyRole role,
            Vector2 candidate,
            Vector2 selfPos,
            Vector2 playerPos,
            Vector2 playerFacing,
            Vector2 healerPos,
            float protectDistance,
            LayerMask obstacleMask,
            float searchRadius,
            float bodyRadius)
        {
            float total;
            switch (role)
            {
                case EnemyRole.Defensive: // Tank ในฉากนี้
                    total = ScoreTank(candidate, playerPos, healerPos, protectDistance, obstacleMask, searchRadius, bodyRadius);
                    break;
                case EnemyRole.Flanker:
                    total = ScoreFlanker(candidate, playerPos, playerFacing, obstacleMask, searchRadius, bodyRadius);
                    break;
                case EnemyRole.Support:
                    total = ScoreHealer(candidate, playerPos, healerPos, obstacleMask, searchRadius, bodyRadius);
                    break;
                default:
                    total = ScoreNeutral(candidate, playerPos, obstacleMask, searchRadius, bodyRadius);
                    break;
            }

            // ── Danger (§7) — หักคะแนนจุดเสี่ยง ──
            // 1) ผู้เล่นมองเห็นจุดนี้ (ยิงถึง) -10  (Tank ยกเว้น — หน้าที่คือยืนให้ยิง)
            if (role != EnemyRole.Defensive)
            {
                float sight = Vector2.Distance(playerPos, candidate) + 1f;
                bool seen = RaycastDetector.HasLineOfSight(playerPos, candidate, obstacleMask, sight);
                if (seen) total -= 10f;
            }

            // 2) ใกล้ผู้เล่นเกินไป -10 (Tank ยกเว้น — มันควรอยู่ใกล้)
            float distToPlayer = Vector2.Distance(candidate, playerPos);
            if (distToPlayer < bodyRadius * 2f && role != EnemyRole.Defensive)
                total -= 10f;

            // 3) ทางตัน (Dead End) -10 — นับทิศที่หนีต่อได้
            int openDirections = CountOpenDirections(candidate, obstacleMask, bodyRadius);
            if (openDirections <= 1) total -= 10f;
            else if (openDirections == 2) total -= 3f;

            return Mathf.Clamp(total, 0f, 100f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Tank (§8) — ยืนบังระหว่าง PLAYER → TANK → HEALER
        // ─────────────────────────────────────────────────────────────────

        private static float ScoreTank(
            Vector2 candidate, Vector2 playerPos, Vector2 healerPos,
            float protectDistance, LayerMask obstacleMask, float searchRadius, float bodyRadius)
        {
            Vector2 healerToPlayer = (playerPos - healerPos).normalized;
            Vector2 healerToCandidate = (candidate - healerPos).normalized;

            // ProtectionAngle (30%) — อยู่บนเส้น healer→player ฝั่ง player มากที่สุด
            // (dot = 1 คืออยู่ "ตรงแนว" ที่ผู้เล่นอยู่, dot = -1 คืออยู่หลัง healer ผิดฝั่ง)
            float protectionAngle = Mathf.Clamp01(Vector2.Dot(healerToPlayer, healerToCandidate)) * 30f;

            // BlockPotential (25%) — อยู่ใกล้ "ทางเดิน" ระหว่างผู้เล่นกับ healer
            // (บังได้จริง = อยู่ในรัศมีบังจากแนวเส้น player→healer)
            float distToLine = DistancePointToSegment(candidate, healerPos, playerPos);
            float blockPotential = Mathf.Clamp01(1f - (distToLine / BlockCorridorWidth)) * 25f;

            // DistanceToHealer (15%) — อยู่ห่างจาก healer ประมาณระยะป้องกัน (ไม่ชิดจนทับ ไม่ห่างจนบังไม่ทัน)
            float distToHealer = Vector2.Distance(candidate, healerPos);
            float distanceToHealer = (1f - Mathf.Clamp01(Mathf.Abs(distToHealer - protectDistance) / Mathf.Max(0.01f, searchRadius))) * 15f;

            // PlayerThreat (10%) — ใกล้ผู้เล่นพอที่จะ block ได้ทัน
            float distToPlayer = Vector2.Distance(candidate, playerPos);
            float playerThreat = (1f - Mathf.Clamp01(distToPlayer / Mathf.Max(0.01f, searchRadius * 1.5f))) * 10f;

            // AttackPotential (5%) — ยิงผู้เล่นได้จากจุดนี้
            float sight = distToPlayer + 1f;
            bool canShoot = RaycastDetector.HasLineOfSight(candidate, playerPos, obstacleMask, sight);
            float attackPotential = (canShoot ? 1f : 0f) * 5f;

            // MovementSafety (5%) — จุดนี้เดินได้ ไม่ติดมุมอับ
            float movementSafety = (CountOpenDirections(candidate, obstacleMask, bodyRadius) >= 3 ? 1f : 0f) * 5f;

            return protectionAngle + blockPotential + distanceToHealer
                 + playerThreat + attackPotential + movementSafety;
        }

        // ─────────────────────────────────────────────────────────────────
        // Flanker (§11) — มุมข้าง/หลังผู้เล่น + ยิงได้ + ปลอดภัย
        // ─────────────────────────────────────────────────────────────────

        private static float ScoreFlanker(
            Vector2 candidate, Vector2 playerPos, Vector2 playerFacing,
            LayerMask obstacleMask, float searchRadius, float bodyRadius)
        {
            // FlankAngle (25%) — อยู่ด้านข้าง/หลังผู้เล่น (มุม 90–180° จากทิศที่ผู้เล่นหันหน้า)
            // ผู้เล่นไม่มีทิศชัดเจน (ยืนนิ่ง) → ให้คะแนนกลาง
            Vector2 toCandidate = (candidate - playerPos).normalized;
            float angleToCandidate = playerFacing.sqrMagnitude > 0.01f
                ? Vector2.Angle(playerFacing, toCandidate)
                : 90f;
            float flankAngle = Mathf.Clamp01(angleToCandidate / 180f) * 25f;

            // AttackPotential (20%) — ยิงผู้เล่นได้
            float distToPlayer = Vector2.Distance(candidate, playerPos);
            bool canShoot = RaycastDetector.HasLineOfSight(candidate, playerPos, obstacleMask, distToPlayer + 1f);
            float attackPotential = (canShoot ? 1f : 0f) * 20f;

            // Safety (20%) — ห่างผู้เล่นพอควร (ไม่ชิดจนโดนตบ)
            float safety = Mathf.Clamp01(distToPlayer / Mathf.Max(0.01f, searchRadius)) * 20f;

            // CombatDistance (15%) — อยู่ในระยะยิงที่เหมาะสม
            float combatDistance = (1f - Mathf.Clamp01(Mathf.Abs(distToPlayer - searchRadius * 0.6f) / Mathf.Max(0.01f, searchRadius))) * 15f;

            // EscapeRoute (10%) — มีทางหนีรอบจุดนี้
            float escapeRoute = (CountOpenDirections(candidate, obstacleMask, bodyRadius) / 4f) * 10f;

            // Cover (10%) — ผู้เล่นมองไม่เห็นจุดนี้
            bool seen = RaycastDetector.HasLineOfSight(playerPos, candidate, obstacleMask, distToPlayer + 1f);
            float cover = (seen ? 0f : 1f) * 10f;

            return flankAngle + attackPotential + safety + combatDistance + escapeRoute + cover;
        }

        // ─────────────────────────────────────────────────────────────────
        // Healer (§16) — ปลอดภัย + อยู่หลัง Tank + ฮีลถึงเพื่อน
        // ─────────────────────────────────────────────────────────────────

        private static float ScoreHealer(
            Vector2 candidate, Vector2 playerPos, Vector2 healerPos,
            LayerMask obstacleMask, float searchRadius, float bodyRadius)
        {
            // Safety (30%) — ไกลจากผู้เล่น
            float distToPlayer = Vector2.Distance(candidate, playerPos);
            float safety = Mathf.Clamp01(distToPlayer / Mathf.Max(0.01f, searchRadius * 1.5f)) * 30f;

            // TankProtection (20%) — ใกล้ Tank ที่กำลังคุ้มกัน (ใช้ healerPos แทนตำแหน่ง tank
            // เพราะ healer ยืนหลัง tank อยู่แล้ว — จุดใกล้ healer = โซนปลอดภัยของทีม)
            float distToHealer = Vector2.Distance(candidate, healerPos);
            float tankProtection = (1f - Mathf.Clamp01(distToHealer / Mathf.Max(0.01f, searchRadius))) * 20f;

            // Cover (20%) — ผู้เล่นมองไม่เห็นจุดนี้
            bool seen = RaycastDetector.HasLineOfSight(playerPos, candidate, obstacleMask, distToPlayer + 1f);
            float cover = (seen ? 0f : 1f) * 20f;

            // HealingAccess (15%) — เข้าถึงเพื่อนที่ต้องฮีลได้ (ยิงลำแสงฮีลผ่าน)
            float healingAccess = 7.5f; // ค่ากลาง — ปรับโดยผู้เรียกได้ในอนาคต

            // PlayerDistance (10%) — ยิ่งห่างยิ่งดี (คุ้มกับ safety ซ้ำ แต่น้ำหนักตาม §16)
            float playerDistance = Mathf.Clamp01(distToPlayer / Mathf.Max(0.01f, searchRadius * 2f)) * 10f;

            // EscapeRoute (5%) — มีทางหนี
            float escapeRoute = (CountOpenDirections(candidate, obstacleMask, bodyRadius) / 4f) * 5f;

            return safety + tankProtection + cover + healingAccess + playerDistance + escapeRoute;
        }

        /// <summary>คะแนนกลางสำหรับ Role ที่ไม่มีสูตรเฉพาะ</summary>
        private static float ScoreNeutral(
            Vector2 candidate, Vector2 playerPos, LayerMask obstacleMask, float searchRadius, float bodyRadius)
        {
            float distToPlayer = Vector2.Distance(candidate, playerPos);
            float safety = Mathf.Clamp01(distToPlayer / Mathf.Max(0.01f, searchRadius * 1.5f)) * 50f;
            bool seen = RaycastDetector.HasLineOfSight(playerPos, candidate, obstacleMask, distToPlayer + 1f);
            float cover = (seen ? 0f : 1f) * 25f;
            float escape = (CountOpenDirections(candidate, obstacleMask, bodyRadius) / 4f) * 25f;
            return safety + cover + escape;
        }

        // ─────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────

        /// <summary>ระยะจากจุด p ถึง "เส้นตรงช่วง" a→b (ใช้วัดว่ายืนบังอยู่ในทางหรือไม่)</summary>
        private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>
        /// นับจำนวนทิศ (จาก 4 ทิศหลัก) ที่เดินหนีต่อได้โดยไม่ติดกำแพง
        /// ใช้ประเมิน Escape Route (§11/§16) และ Dead End (§7)
        /// </summary>
        private static int CountOpenDirections(Vector2 point, LayerMask obstacleMask, float bodyRadius)
        {
            int open = 0;
            foreach (var dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                if (!PhysicsUtility.IsPositionBlocked(point + dir * (bodyRadius + 0.5f), bodyRadius, obstacleMask))
                    open++;
            }
            return open;
        }
    }
}

using UnityEngine;
using TopDownTacticalAI.Vision;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Cover
{
    /// <summary>
    /// สแกนหาจุดกำบัง (CoverPoint) ที่ "ดีที่สุด" ในรัศมีที่กำหนด ไม่ใช่แค่ใกล้ที่สุด
    /// ให้คะแนนจาก 3 ปัจจัย:
    /// 1) บังสายตาจากผู้เล่นได้จริงไหม (สำคัญที่สุด — ถ้าไปยืนแล้วยังโดนยิงเห็นๆ ก็ไม่ใช่ที่กำบังที่ดี)
    /// 2) ระยะห่างจากตัวเอง (ใกล้กว่าดีกว่า แต่ไม่ใช่ปัจจัยหลัก)
    /// 3) มีทางหนีไหม (ไม่ใช่มุมอับที่โดนล้อมได้ง่าย)
    /// </summary>
    public static class CoverScanner
    {
        private static readonly float[] EscapeCheckAngles = { 0f, 45f, 90f, 135f, 180f, -45f, -90f, -135f };

        public static CoverPoint FindBestAvailableCover(Vector2 origin, Vector2 targetPosition, float searchRadius, LayerMask coverMask, LayerMask obstacleMask)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, searchRadius, coverMask);
            CoverPoint best = null;
            float bestScore = float.MinValue;

            foreach (var hit in hits)
            {
                if (!hit.TryGetComponent(out CoverPoint cover)) continue;
                if (cover.IsOccupied) continue;

                float score = ScoreCoverPoint(origin, hit.transform.position, targetPosition, searchRadius, obstacleMask);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = cover;
                }
            }

            return best;
        }

        private static float ScoreCoverPoint(Vector2 origin, Vector2 coverPos, Vector2 targetPosition, float searchRadius, LayerMask obstacleMask)
        {
            // 1) บังสายตาได้จริงไหม (น้ำหนักมากสุด): ถ้า Raycast จากจุดกำบังไปยังผู้เล่นโดนกำแพงบัง = กำบังได้จริง
            bool blocksLineOfSight = !RaycastDetector.HasLineOfSight(coverPos, targetPosition, obstacleMask, Vector2.Distance(coverPos, targetPosition) + 1f);
            float exposureScore = blocksLineOfSight ? 1f : 0f;

            // 2) ระยะห่างจากตัวเอง (ยิ่งใกล้ยิ่งดี แต่น้ำหนักน้อยกว่าการบังสายตา)
            float distance = Vector2.Distance(origin, coverPos);
            float distanceScore = 1f - Mathf.Clamp01(distance / searchRadius);

            // 3) มีทางหนีไหม: เช็คว่ารอบๆ จุดนี้ไม่ได้ถูกกำแพงล้อมจนอับ (สุ่มยิงเรย์สั้นๆ รอบตัว)
            int openDirections = 0;
            foreach (float angle in EscapeCheckAngles)
            {
                Vector2 dir = Vector2.up.Rotate(angle);
                if (!Physics2D.Raycast(coverPos, dir, 1f, obstacleMask))
                    openDirections++;
            }
            float escapeScore = (float)openDirections / EscapeCheckAngles.Length;

            return exposureScore * 0.5f + distanceScore * 0.3f + escapeScore * 0.2f;
        }
    }
}

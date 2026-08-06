using System.Collections.Generic;
using UnityEngine;
using TopDownTacticalAI.Vision;

namespace TopDownTacticalAI.Tactical
{
    /// <summary>
    /// ระบบผังอิทธิพล (Influence Maps) ตามทฤษฎีในบทที่ 2.1.1 และอัลกอริทึมในบทที่ 3.2
    /// แบ่งพื้นที่รอบตัวออกเป็นตาราง (Grid) แล้วคำนวณคะแนน 3 ส่วนในแต่ละช่อง:
    /// 1) Danger Score (ลบคะแนน) — จากระยะห่างของแหล่งอันตราย (กระสุน/ผู้เล่น) แบบ Linear Decay
    /// 2) Safety/Cover Score (บวกคะแนน) — ถ้าช่องนั้นมีสิ่งกีดขวางบังสายตาจากผู้เล่นได้
    /// 3) Utility/Attack Bonus (บวกคะแนน) — ถ้าผู้เล่น Mana ต่ำ และช่องนั้นเห็นผู้เล่นได้ (โอกาสโจมตี)
    /// แล้ววนลูปหาช่องที่คะแนนรวมสูงสุด (Best Grid Cell) คืนเป็นตำแหน่งโลกจริงให้ระบบเคลื่อนที่นำไปใช้
    ///
    /// อ้างอิงเทียบเท่า Pseudocode ในบทที่ 3.2.3: FindBestPosition(GridMap, Player, Bullets)
    /// </summary>
    public static class InfluenceMap
    {
        public struct DangerSource
        {
            public Vector2 Position;
            public float Radius;      // รัศมีที่ค่าอันตรายแผ่ขยายไปถึง (R ในสมการ I(d))
            public float Intensity;   // ค่าอิทธิพลเริ่มต้นที่จุดกำเนิด (I0)

            public DangerSource(Vector2 position, float radius, float intensity = 100f)
            {
                Position = position;
                Radius = radius;
                Intensity = intensity;
            }
        }

        public struct CellResult
        {
            public Vector2 WorldPosition;
            public float DangerScore;
            public float SafetyScore;
            public float UtilityScore;
            public float TotalScore;
        }

        /// <summary>
        /// ค้นหาช่องตารางที่ดีที่สุด (คะแนนรวมสูงสุด) รอบตำแหน่ง origin ภายในรัศมี searchRadius
        /// ตรงกับ FindBestPosition() ใน Pseudocode ของเอกสารบทที่ 3.2.3
        /// </summary>
        /// <param name="origin">ศูนย์กลางพื้นที่ที่จะสแกน (ปกติคือตำแหน่งปัจจุบันของ Agent)</param>
        /// <param name="searchRadius">รัศมีพื้นที่ที่จะสแกนหาตำแหน่ง</param>
        /// <param name="cellSize">ขนาดของแต่ละช่องตาราง (ยิ่งเล็กยิ่งละเอียดแต่ยิ่งคำนวณหนัก)</param>
        /// <param name="dangerSources">แหล่งอันตรายทั้งหมด (เช่น ตำแหน่งกระสุนที่ตรวจพบ)</param>
        /// <param name="obstacleMask">Layer ของสิ่งกีดขวาง ใช้เช็คว่าช่องไหนบังสายตาจากผู้เล่นได้ (Safety)</param>
        /// <param name="playerPosition">ตำแหน่งผู้เล่น ใช้คำนวณ Safety และ Utility</param>
        /// <param name="playerManaLow">true ถ้าผู้เล่น Mana ต่ำกว่าเกณฑ์ (กระตุ้นให้ AI พิจารณาโจมตีมากขึ้น)</param>
        /// <param name="attackBonus">คะแนนพิเศษที่จะบวกให้ช่องที่เห็นผู้เล่นได้ตอน Mana ผู้เล่นต่ำ</param>
        public static bool FindBestPosition(
            Vector2 origin,
            float searchRadius,
            float cellSize,
            List<DangerSource> dangerSources,
            LayerMask obstacleMask,
            Vector2 playerPosition,
            bool playerManaLow,
            float attackBonus,
            out CellResult best)
        {
            best = default;
            float bestScore = float.MinValue;
            bool found = false;

            int steps = Mathf.Max(1, Mathf.RoundToInt(searchRadius / cellSize));

            for (int gx = -steps; gx <= steps; gx++)
            {
                for (int gy = -steps; gy <= steps; gy++)
                {
                    Vector2 cell = origin + new Vector2(gx * cellSize, gy * cellSize);

                    if (Vector2.Distance(origin, cell) > searchRadius)
                        continue; // อยู่นอกวงกลมค้นหา ข้ามไป (ตารางที่สแกนเป็นวงกลม ไม่ใช่สี่เหลี่ยมเต็มพื้นที่)

                    // ช่องนี้อยู่ในสิ่งกีดขวางพอดี เดินไปไม่ได้จริง ข้ามไปเลย
                    if (Physics2D.OverlapCircle(cell, cellSize * 0.3f, obstacleMask) != null)
                        continue;

                    CellResult result = EvaluateCell(cell, dangerSources, obstacleMask, playerPosition, playerManaLow, attackBonus);

                    if (result.TotalScore > bestScore)
                    {
                        bestScore = result.TotalScore;
                        best = result;
                        found = true;
                    }
                }
            }

            return found;
        }

        /// <summary>คำนวณคะแนนทั้ง 3 ส่วนของช่องตารางเดียว ตรงกับขั้นตอน 3.2.2.2 ในเอกสาร</summary>
        private static CellResult EvaluateCell(
            Vector2 cell,
            List<DangerSource> dangerSources,
            LayerMask obstacleMask,
            Vector2 playerPosition,
            bool playerManaLow,
            float attackBonus)
        {
            CellResult result = new CellResult { WorldPosition = cell };

            // 1) Danger Calculation — ลบคะแนนตาม Linear Decay: I(d) = I0 * (1 - d/R)
            float dangerScore = 0f;
            if (dangerSources != null)
            {
                foreach (var source in dangerSources)
                {
                    float distance = Vector2.Distance(cell, source.Position);
                    if (distance < source.Radius)
                    {
                        float falloff = 1f - (distance / source.Radius);
                        dangerScore += source.Intensity * falloff;
                    }
                }
            }
            result.DangerScore = dangerScore;

            // 2) Safety/Cover Calculation — บวกคะแนนถ้าช่องนี้มีสิ่งกีดขวางบังสายตาจากผู้เล่นได้จริง
            bool blocksLineOfSight = !RaycastDetector.HasLineOfSight(cell, playerPosition, obstacleMask, Vector2.Distance(cell, playerPosition) + 1f);
            result.SafetyScore = blocksLineOfSight ? 50f : 0f;

            // 3) Utility Calculation — ถ้าผู้เล่น Mana ต่ำ และช่องนี้เห็นผู้เล่นได้ (โอกาสโจมตีสวน) ให้คะแนนพิเศษ
            bool hasSightToPlayer = !blocksLineOfSight; // มองเห็นได้ = ไม่ได้ถูกบัง
            result.UtilityScore = (playerManaLow && hasSightToPlayer) ? attackBonus : 0f;

            result.TotalScore = result.SafetyScore + result.UtilityScore - dangerScore;
            return result;
        }
    }
}

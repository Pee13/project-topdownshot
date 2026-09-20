using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Utilities;

namespace TopDownTacticalAI.Tactical
{
    /// <summary>
    /// ชั้นให้คะแนนการตัดสินใจของ AI (Action Scoring + Stability)
    ///
    /// หน้าที่ของชั้นนี้คือ "ห่อ" TacticalDecision ไม่ใช่แทนที่:
    ///   1) TacticalDecision ยังเป็นตัวตัดสินว่า Action ไหน "ทำได้จริง" (Precondition)
    ///      เช่น "Heal ได้ไหม" "Cover ไปถึงไหม" — ตรรกะนี้มีอยู่แล้วและถูกต้อง
    ///   2) ชั้นนี้ให้ "คะแนน 0..1" กับ Action ที่เสนอมา และกับ Action ที่ทำอยู่ตอนนี้
    ///   3) ถ้าคะแนนต่างกันไม่พอ ก็ไม่เปลี่ยนใจ (Hysteresis) — กัน AI กระตุกสลับไปมา
    ///   4) ถ้าภัยคุกคามวิกฤต ให้ Override ยกเลิก Action ปกติทันที (ความอยู่รอดมาก่อน)
    ///
    /// ผลลัพธ์: AI ตัดสินใจจากคะแนนที่มีความหมาย ไม่ใช่ threshold เดี่ยว
    /// แต่ยังรักษาสถาปัตยกรรมและเงื่อนไขเดิมไว้ทั้งหมด
    /// </summary>
    public static class AIActionScorer
    {
        // ─────────────────────────────────────────────────────────────────
        // Role Importance
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// ความสำคัญของแต่ละบทบาทต่อทีม (0..1)
        ///
        /// ใช้ตอบคำถามว่า "ตัวไหนควรได้รับการคุ้มครองมากกว่า" ซึ่งมีผลสองที่:
        ///   1) การเลือก Action — ตัวสำคัญจะยอมหลบเข้าที่กำบังมากกว่า และไม่เข้าปะทะซึ่งๆ หน้า
        ///   2) การเลือกเป้าหมายฮีลในอนาคต (Support ควรรักษาตัวที่ทดแทนยากก่อน)
        ///
        /// Support สูงสุดเพราะถ้าตาย ทีมจะขาดการฮีลทั้งทีม
        /// ตัวบุกมีค่าต่ำกว่าเพราะสามารถทดแทนกันได้
        /// </summary>
        public static float GetRoleImportance(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Support:    return 1.00f;  // ทดแทนยากที่สุด — ขาดแล้วทีมไม่มีฮีล
                case EnemyRole.Defensive:  return 0.80f;  // แนวหน้า รับความเสียหายแทนเพื่อน
                case EnemyRole.Sniper:     return 0.75f;  // ยิงไกล มีคุณค่าแต่เปราะ
                case EnemyRole.Aggressive: return 0.70f;  // ตัวบุก ทดแทนได้
                case EnemyRole.Flanker:    return 0.60f;  // อ้อมโจมตี เสี่ยงสูงแต่ทดแทนง่าย
                case EnemyRole.Scout:      return 0.55f;  // ตายแล้วเสียการรับรู้ แต่ไม่ถึงกับพัง
                default:                   return 0.60f;  // Custom — ค่ากลางๆ
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Scoring
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// ให้คะแนน 0..1 กับ Action หนึ่งๆ ตามสถานการณ์ปัจจุบัน
        /// คะแนนสูง = Action นี้เหมาะกับสถานการณ์ตอนนี้มาก
        ///
        /// ทุกองค์ประกอบถูก normalize เป็น 0..1 ก่อนถ่วงน้ำหนักเสมอ
        /// </summary>
        public static float ScoreAction(EnemyState state, Blackboard bb, AIScoringWeights w)
        {
            if (bb == null) return 0f;
            if (w == null) w = new AIScoringWeights();

            // ── ปัจจัยพื้นฐาน (normalize เป็น 0..1 ทั้งหมด) ──
            float threat = bb.SmoothedThreat;
            float safety = 1f - threat;                                  // ยิ่ง threat ต่ำ ยิ่งปลอดภัย
            float healthNeed = w.EvaluateHealthNeed(MathUtility.HealthPercent(bb.CurrentHP, bb.MaxHP));
            float critical = MathUtility.CriticalBonus(
                MathUtility.HealthPercent(bb.CurrentHP, bb.MaxHP), w.criticalThreshold);
            float ammoReady = bb.IsReloading
                ? 0f
                : MathUtility.Normalize01(bb.CurrentAmmo, Mathf.Max(1, bb.MaxAmmo));
            float vision = bb.VisionConfidence;
            float memory = bb.MemoryConfidence;
            float suspicion = Mathf.Clamp01(bb.SuspicionLevel);
            float reachability = bb.AccessibilityScore;

            // ── ถ่วงน้ำหนักที่ปรับได้จาก Inspector ──
            // ทุกตัวคูณน้ำหนักของตัวเอง เพื่อให้การเลื่อนสไลเดอร์ใน Inspector มีผลจริง
            float wThreat = threat * w.threatImportance;
            float wHealth = healthNeed * w.healthNeed;
            float wCritical = critical * w.criticalBonus;
            float wVision = vision * w.visibility;
            float wReach = reachability * w.accessibility;
            float wSelfWorth = Mathf.Clamp01(bb.SelfRoleImportance) * w.roleImportance;

            switch (state)
            {
                // หลบกระสุน: เอาตัวรอดล้วนๆ — คุ้มเมื่อภัยสูงและเลือดน้อย
                case EnemyState.Dodge:
                    return Normalize(wThreat + wHealth + wCritical,
                                     w.threatImportance + w.healthNeed + w.criticalBonus);

                // เข้าที่กำบัง: เอาตัวรอด + กระสุนไม่พร้อม
                // ตัวที่สำคัญต่อทีม (เช่น Support) จะได้คะแนนสูงกว่า จึงยอมหลบมากกว่า
                case EnemyState.Cover:
                    return Normalize(
                        wThreat + wHealth + wCritical + (1f - ammoReady) * w.accessibility + wSelfWorth,
                        w.threatImportance + w.healthNeed + w.criticalBonus + w.accessibility + w.roleImportance);

                // ปะทะ: ต้องเห็นชัด ไปถึงได้ กระสุนพร้อม และไม่เสี่ยงเกิน
                // ตัวที่สำคัญต่อทีมจะได้คะแนนลดลง เพราะไม่ควรเอาตัวไปเสี่ยงปะทะซึ่งๆ หน้า
                case EnemyState.Combat:
                    return Normalize(
                        wVision + wReach + ammoReady * 0.25f + safety * 0.25f,
                        w.visibility + w.accessibility + 0.5f) * (1f - wSelfWorth * 0.5f);

                // ไล่ตาม: เห็นเป้าหมายหรือยังจำได้ ไปถึงได้ และปลอดภัยพอ
                case EnemyState.Chase:
                    return Normalize(
                        wVision + wReach + memory * 0.3f + ammoReady * 0.2f + safety * 0.2f,
                        w.visibility + w.accessibility + 0.7f);

                // ค้นหา: คุ้มเมื่อยังจำได้และภัยไม่สูง
                case EnemyState.Search:
                    return Normalize(memory * 0.6f + safety * 0.4f, 1f);

                // สงสัย: คุ้มเมื่อความสงสัยสะสมสูง
                case EnemyState.Suspicious:
                    return Normalize(suspicion * 0.7f + wVision, 0.7f + w.visibility);

                // ลาดตระเวน: คุ้มเมื่อไม่มีภัย ไม่มีข้อมูลค้าง
                case EnemyState.Patrol:
                    return Normalize(
                        safety * 0.5f + (1f - vision) * 0.3f + (bb.HasMemory ? 0f : 0.2f),
                        1f);

                // ── เอาชีวิตรอด + สนับสนุน (§18/§19/§14) ──

                // ถอยไปหา Healer: คุ้มเมื่อเลือดน้อย "และ" มี Healer ให้ถอยไปหา
                // ถ้าไม่มี healer แล้ว การถอยก็ไม่ช่วยอะไร — คะแนนจะต่ำลงเอง
                case EnemyState.Retreat:
                    return Normalize(
                        wHealth + wCritical
                        + (bb.HealerAvailable ? 0.30f : 0f)
                        + wSelfWorth * 0.3f,
                        w.healthNeed + w.criticalBonus + 0.30f + w.roleImportance * 0.3f);

                // อยู่ในระยะฮีลแล้ว: คุ้มเมื่อเลือดน้อย + มี healer + ยังปลอดภัยพอ
                // (ถ้าไม่ปลอดภัยควรหลบ ซึ่ง Dodge มี priority สูงกว่าอยู่แล้ว)
                case EnemyState.SeekHeal:
                    return Normalize(
                        wHealth + wCritical
                        + (bb.HealerAvailable ? 0.30f : 0f)
                        + safety * 0.20f,
                        w.healthNeed + w.criticalBonus + 0.30f + 0.20f);

                // ฮีลเพื่อน: คุ้มเมื่อมีเพื่อนรอฮีล "และ" ตัวเองยังปลอดภัย
                // ความปลอดภัยของ healer เองจัดการโดย Critical Override แล้ว (§4 — stay alive ก่อน)
                case EnemyState.HealAlly:
                    return Normalize(
                        (bb.WoundedAllyExists ? 0.45f : 0f)
                        + safety * 0.35f
                        + wSelfWorth * 0.20f,
                        0.45f + 0.35f + w.roleImportance * 0.20f);

                // ── Tank (§2/§8/§9/§10/§21) ──

                // บัง healer: หน้าที่หลักของ Tank — คุ้มเมื่อ healer ถูกคุกคาม (precondition แล้ว)
                // ตั้งใจให้คะแนน "สูงเสมอ" เมื่อ trigger แล้ว เพราะเป็นอัตลักษณ์ของบทบาท
                // (Tank ยอมตัวเสียเพื่อ healer อยู่ดี — ตายไปแล้วก็บังไม่ได้อีก)
                case EnemyState.ProtectHealer:
                    return Normalize(
                        (bb.HealerThreatened ? 0.55f : 0f) + wSelfWorth * 0.45f,
                        0.55f + w.roleImportance * 0.45f);

                // สกัด/ดึงดูดผู้เล่นที่ไล่เพื่อน: คุ้มเมื่อมีเพื่อนถูกไล่ (precondition แล้ว)
                case EnemyState.PeelAlly:
                    return Normalize(
                        (bb.RetreatingAllyUnderThreat ? 0.55f : 0f) + wSelfWorth * 0.45f,
                        0.55f + w.roleImportance * 0.45f);

                default:
                    return 0f;
            }
        }

        /// <summary>
        /// หารด้วยน้ำหนักรวมเพื่อให้คะแนนกลับมาอยู่ในช่วง 0..1 เสมอ
        /// จำเป็นเพราะแต่ละ Action ใช้น้ำหนักคนละชุด ถ้าไม่หาร คะแนนจะเทียบกันไม่ได้
        /// และค่า switchThreshold ที่ตั้งไว้จะไม่มีความหมายคงที่
        /// </summary>
        private static float Normalize(float weightedSum, float totalWeight)
        {
            if (totalWeight <= 0.0001f) return 0f;
            return Mathf.Clamp01(weightedSum / totalWeight);
        }

        // ─────────────────────────────────────────────────────────────────
        // Selection (Override + Hysteresis)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// ตัดสินใจขั้นสุดท้ายว่าจะเปลี่ยน Action หรือไม่
        /// </summary>
        /// <param name="bb">ข้อมูลกลางของ AI</param>
        /// <param name="w">น้ำหนักที่ปรับได้</param>
        /// <param name="currentState">Action ที่ทำอยู่ตอนนี้</param>
        /// <param name="proposedState">Action ที่ TacticalDecision เสนอ (ผ่านเงื่อนไขความถูกต้องแล้ว)</param>
        /// <param name="chosenScore">ผลลัพธ์: คะแนนของ Action ที่เลือก</param>
        /// <param name="reasonWhy">ผลลัพธ์: เหตุผลที่เลือก (ใช้แสดง Debug)</param>
        /// <returns>Action ที่ควรทำในเฟรมนี้</returns>
        public static EnemyState SelectAction(
            Blackboard bb,
            AIScoringWeights w,
            EnemyState currentState,
            EnemyState proposedState,
            out float chosenScore,
            out string reasonWhy)
        {
            if (w == null) w = new AIScoringWeights();

            float proposedScore = ScoreAction(proposedState, bb, w);
            float currentScore = ScoreAction(currentState, bb, w);

            // ─────────────────────────────────────────────────────────
            // 1) Critical Override — ความอยู่รอดเหนือกว่าแผนปกติเสมอ
            //
            //    สำคัญ: ที่นี่ "ระงับ Hysteresis" ไม่ใช่ "บังคับ State เอง"
            //    เราไม่บังคับ Cover ตรงๆ เพราะถ้าฉากนั้นไม่มี CoverPoint ให้ใช้
            //    CoverState จะไม่ทำอะไรเลยแล้ว AI จะยืนนิ่ง
            //    การปล่อยให้ TacticalDecision เป็นคนเลือกแทน ทำให้ได้ State ที่
            //    "ทำได้จริง" ในสภาพฉากนั้นเสมอ (มันรู้ว่ามีรังให้เข้าไหม)
            //    เราแค่ไม่ยอมให้ความหนืดของการเปลี่ยนแผนมาขวางในจังหวะวิกฤต
            // ─────────────────────────────────────────────────────────
            if (IsCriticalSituation(bb, w) && proposedState != currentState)
            {
                chosenScore = proposedScore;
                reasonWhy = $"⚠ สถานการณ์วิกฤต → ระงับความหนืดของการเปลี่ยนแผน เปลี่ยนเป็น {proposedState} ทันที";
                return proposedState;
            }

            // ─────────────────────────────────────────────────────────
            // 2) Action เดิมถูกต้องอยู่แล้ว — ไม่ต้องคิดมาก
            // ─────────────────────────────────────────────────────────
            if (proposedState == currentState)
            {
                chosenScore = currentScore;
                reasonWhy = null; // ปล่อยให้ TacticalDecision เป็นคนอธิบายเหตุผล
                return currentState;
            }

            // ─────────────────────────────────────────────────────────
            // 3) Action ใหม่มีลำดับความสำคัญสูงกว่า (Priority) เช่น กำลังจะโดนยิง
            //    กรณีนี้ให้เปลี่ยนได้ทันที เพราะเป็นเรื่องความปลอดภัย
            // ─────────────────────────────────────────────────────────
            bool higherPriority = PrioritySystem.ShouldOverride(currentState, proposedState);

            // ─────────────────────────────────────────────────────────
            // 4) Hysteresis — เปลี่ยนใจเฉพาะเมื่อคะแนนใหม่ชนะอย่างมีนัยสำคัญ
            //    ถ้าคะแนนต่างกันแค่เสี้ยว การเปลี่ยนไปมาจะทำให้ตัวกระตุก
            // ─────────────────────────────────────────────────────────
            float margin = proposedScore - currentScore;
            bool scoreDecisive = margin >= w.switchThreshold;

            // แผนเดิมหมดความหมายแล้ว (คะแนนต่ำมาก) — ไม่ควรดื้อทำต่อ
            // ไม่งั้น Hysteresis จะกลายเป็นตัวล็อกให้ AI ค้างอยู่ใน Action ที่ไม่เหมาะ
            bool currentNoLongerViable = currentScore < w.minViableScore;

            if (higherPriority || scoreDecisive || currentNoLongerViable)
            {
                chosenScore = proposedScore;
                reasonWhy = null; // ใช้เหตุผลจาก TacticalDecision
                return proposedState;
            }

            // ไม่เปลี่ยน — อยู่ในแผนเดิมต่อ
            chosenScore = currentScore;
            reasonWhy = $"คะแนนต่างไม่พอ ({proposedScore:F2} vs {currentScore:F2} " +
                        $"— ต้องต่างอย่างน้อย {w.switchThreshold:F2}) → ทำแผนเดิมต่อ";
            return currentState;
        }

        /// <summary>
        /// ตรวจว่าสถานการณ์อยู่ในขั้นวิกฤตจนไม่ควรปล่อยให้ความหนืดของการเปลี่ยนแผน
        /// (Hysteresis) มาขวางการตัดสินใจหรือยัง
        ///
        /// กติกา:
        ///   - เลือดใกล้หมด: ต้องยอมให้เปลี่ยนแผนทันที
        ///   - ภัยคุกคามสูงมาก ร่วมกับเลือดไม่ดีหรือกระสุนไม่พร้อม: ต้องเปลี่ยนทันที
        ///
        /// ฟังก์ชันนี้ตอบแค่ "ควรระงับ Hysteresis ไหม" ไม่ได้เลือก State เอง
        /// การเลือก State ยังเป็นหน้าที่ของ TacticalDecision ซึ่งรู้ว่าอะไรทำได้จริงในฉากนั้น
        /// </summary>
        private static bool IsCriticalSituation(Blackboard bb, AIScoringWeights w)
        {
            float healthPercent = MathUtility.HealthPercent(bb.CurrentHP, bb.MaxHP);

            // เลือดวิกฤต
            if (bb.CurrentHP > 0f && healthPercent <= w.criticalHealthOverride)
                return true;

            // ภัยคุกคามสูงมาก + อยู่ในสภาพที่สู้ไม่ไหว
            if (bb.SmoothedThreat >= w.criticalThreatOverride)
            {
                float ammoReady = bb.IsReloading
                    ? 0f
                    : MathUtility.Normalize01(bb.CurrentAmmo, Mathf.Max(1, bb.MaxAmmo));

                if (healthPercent <= 0.5f || ammoReady < 0.3f)
                    return true;
            }

            return false;
        }
    }
}

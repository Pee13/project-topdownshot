using UnityEngine;
using TopDownTacticalAI.Core;
using TopDownTacticalAI.Cover;

namespace TopDownTacticalAI.Tactical
{
    /// <summary>
    /// สมองส่วนตัดสินใจขั้นสูงสุด: รวมข้อมูล HP, ระยะ, จำนวนศัตรู, กระสุน, ความปลอดภัย, ที่กำบัง
    /// แล้วเลือก EnemyState ที่เหมาะสมที่สุดในเฟรมนี้ พร้อม "เหตุผล" (reason) เพื่อใช้แสดงผล Debug
    ///
    /// สำคัญ: มี Hysteresis (กันการสลับ State รัวๆ) สำหรับ Cover และ Chase/Combat
    /// เพราะถ้าไม่มี การเช็คแบบ Threshold ตรงๆ ทุกเฟรมจะทำให้ AI "สลับ State ไปมาทุกเฟรม"
    /// เมื่อค่าที่วัด (ระยะ/HP/กระสุน) อยู่ใกล้เส้นแบ่งพอดี ซึ่งจะทำให้ตัวละครหมุน/ขยับกระตุกเหมือนเป็นบั๊ก
    /// </summary>
    public static class TacticalDecision
    {
        public static EnemyState DecideNextState(
            Blackboard blackboard,
            Vector2 selfPosition,
            LayerMask bulletMask,
            LayerMask coverMask,
            LayerMask obstacleMask,
            float dangerRange,
            float coverSearchRadius,
            float dodgeDetectRadius,
            EnemyState currentState,
            out string reason)
        {
            // 1) Priority สูงสุด: เห็นผู้เล่นอยู่ + กระสุนจะโดนจริง -> Dodge
            //    (ไม่ใช้การทอยสุ่มแล้ว — หลบเมื่อการทำนายบอกว่ากระสุนจะพุ่งโดนตัวเราจริง §17/§18)
            //    และบาง Role เช่น Tank ไม่ dodge แบบปกติ (§29) — ใช้ตัวมันบังกระสุนแทน
            if (blackboard.CanSeeTarget && blackboard.CanDodge)
            {
                bool bulletIncoming = Dodge.BulletDetector.IsBulletIncoming(selfPosition, dodgeDetectRadius, bulletMask, out Vector2 bulletVel, out Vector2 bulletPos);
                if (bulletIncoming && Dodge.DodgeDecision.ShouldDodge(true, selfPosition, bulletPos, bulletVel))
                {
                    reason = "เห็นผู้เล่น + ทำนายแล้วว่ากระสุนจะโดนจริง → หลบด่วน!";
                    return EnemyState.Dodge;
                }
            }

            // 1.2) TANK: Healer ถูกคุกคาม → ออกไปบัง (§2 priority 1, §8/§9)
            //      วางไว้ก่อนกิ่งถอย เพราะหน้าที่อันดับแรกของ Tank คือคุ้มกัน
            //      (และ RetreatOnlyWhenCritical ทำให้ tank ถอยเฉพาะเลือดวิกฤตอยู่ดี)
            if (blackboard.IsTankRole && blackboard.HealerThreatened)
            {
                reason = "Tank: Healer ถูกคุกคาม! → ออกไปบังระหว่างผู้เล่นกับ Healer (PROTECT_HEALER)";
                return EnemyState.ProtectHealer;
            }

            // 1.3) TANK: เพื่อนกำลังหนีและถูกผู้เล่นไล่ → เข้าไปสกัด/ดึงดูด (§2 priority 2-4, §10/§21)
            if (blackboard.IsTankRole && blackboard.RetreatingAllyUnderThreat)
            {
                reason = "Tank: เพื่อนกำลังหนีและถูกไล่ → เข้าไปขวาง/สกัดผู้เล่น (PEEL_ALLY)";
                return EnemyState.PeelAlly;
            }

            // 2) เลือดน้อย → ถอยกลับไปหา Healer (§18/§19)
            //    วางไว้หลัง Dodge เพราะถ้ากำลังจะโดนกระสุน ต้องหลบก่อนแล้วค่อยเดินถอย
            //    และทำงานได้ทั้งตอนเห็นและไม่เห็นผู้เล่น — การถอยไม่จำเป็นต้องรู้ว่าผู้เล่นอยู่ไหน
            //    precondition: ต้องมี Healer ที่ยังมีชีวิตอยู่ ไม่งั้นการถอยไม่ช่วยอะไร
            //    (กันไม่ให้ AI เดินถอยหนีจนหมดแมพทั้งที่ฮีลไม่ได้แล้ว)
            //    Tank ใช้เกณฑ์ที่เข้มขึ้น (วิกฤตเท่านั้น) ตาม RetreatOnlyWhenCritical
            float effectiveRetreatThreshold = blackboard.RetreatOnlyWhenCritical
                ? blackboard.CriticalRetreatHealthThreshold
                : blackboard.RetreatHealthThreshold;
            if (blackboard.SelfHealthPercent <= effectiveRetreatThreshold && blackboard.HealerAvailable)
            {
                bool inHealRange = blackboard.HealerDistance <= blackboard.HealRange;

                if (inHealRange)
                {
                    reason = $"เลือดเหลือ {blackboard.SelfHealthPercent * 100f:F0}% และอยู่ในระยะฮีลแล้ว → อยู่กับที่รอฮีล (SEEK_HEAL)";
                    return EnemyState.SeekHeal;
                }

                bool isCritical = blackboard.SelfHealthPercent <= blackboard.CriticalRetreatHealthThreshold;
                reason = isCritical
                    ? $"⚠ เลือดวิกฤต {blackboard.SelfHealthPercent * 100f:F0}%! เอาชีวิตรอดมาก่อน → ถอยหนีไปหา Healer"
                    : $"เลือดเหลือ {blackboard.SelfHealthPercent * 100f:F0}% → ถอยกลับไปหา Healer (RETREAT)";
                return EnemyState.Retreat;
            }

            // 2.4) Healer: ฮีลเพื่อนที่บาดเจ็บ (§4/§14)
            //      Healer ทำงานได้ทั้งตอนเห็นและไม่เห็นผู้เล่น — การฮีลไม่ต้องรู้ว่าผู้เล่นอยู่ไหน
            //      วางไว้ก่อนกิ่ง Patrol/Search เพื่อให้ฮีลได้แม้ไม่เห็นผู้เล่นเลย
            if (blackboard.IsHealerRole && blackboard.WoundedAllyExists)
            {
                reason = "Healer: มีเพื่อนบาดเจ็บ → เข้าไปฮีล (HEAL_ALLY)";
                return EnemyState.HealAlly;
            }

            // 2.5) ไม่เห็นผู้เล่นเลย
            if (!blackboard.CanSeeTarget && !blackboard.HasMemory)
            {
                reason = "ไม่เห็นผู้เล่น และไม่มีความจำเก่า → เดินตรวจตราตามปกติ";
                return EnemyState.Patrol;
            }

            if (!blackboard.CanSeeTarget && blackboard.HasMemory)
            {
                // Healer ไม่ออกค้นหาเอง — หน้าที่คืออยู่ให้ปลอดภัยและรอฮีลเพื่อน (§4)
                if (blackboard.IsHealerRole)
                {
                    reason = "Healer: เสียสายตาผู้เล่น → ไม่ออกค้นหาเอง อยู่ดูแลเพื่อน";
                    return blackboard.WoundedAllyExists ? EnemyState.HealAlly : EnemyState.Patrol;
                }

                reason = $"เพิ่งเห็นผู้เล่นหายไป (จำได้อีก {blackboard.LastSeen.TimeSeen:F0}s ที่แล้ว) → ไปดูจุดล่าสุด/ค้นหา";
                return EnemyState.Search;
            }

            // 2.6) เห็นผู้เล่นอยู่ แต่ยังไม่ "ยืนยัน" แน่ชัด (Alert Phase / Suspicious)
            // ต้องเห็นต่อเนื่องสะสมความสงสัยจนครบก่อน ถึงจะเข้าสู่ Combat/Chase/Cover เต็มตัว
            // (กันไม่ให้ AI "รู้ทันที" ว่าเจอผู้เล่นแบบไม่สมจริง เหมือนโหมด Caution ก่อน Alert เต็มตัวใน MGS)
            if (blackboard.CanSeeTarget && !blackboard.IsTargetConfirmed)
            {
                reason = $"เห็นเงาๆ กำลังตรวจสอบให้แน่ใจ... (สงสัย {blackboard.SuspicionLevel * 100f:F0}%)";
                return EnemyState.Suspicious;
            }

            // Utility Bias ตามสถานะ Mana ผู้เล่น (เอกสารบทที่ 3.1.2):
            // - Mana ผู้เล่นสูง (ไม่ Low) → โหมด "ระมัดระวัง" (Cautious): ให้ความสำคัญกับ Safety ก่อน เข้าที่กำบังไวขึ้น
            // - Mana ผู้เล่นต่ำ (Low) → โหมด "รุกราน" (Aggressive): กล้าเสี่ยงมากขึ้น ไม่รีบถอย เน้นโอกาสโจมตี
            float manaAdjustedDangerRange = blackboard.PlayerManaLow ? dangerRange * 0.6f : dangerRange * 1.3f;

            // 2.5) กันสลับ State รัวๆ: ถ้ากำลังอยู่ในที่กำบังอยู่แล้ว ให้ "อยู่ต่อ" จนกว่าจะปลอดภัยจริงๆ ค่อยออก
            // (ถ้าไม่มีเงื่อนไขนี้ พอ InCover เป็น true เฟรมถัดไปจะหลุดไป Combat ทันที แล้วเดี๋ยวก็กลับเข้า Cover วนไปมา)
            if (currentState == EnemyState.Cover && blackboard.InCover)
            {
                bool stillDangerous = blackboard.IsLowHP
                    || blackboard.CurrentAmmo <= 0
                    || blackboard.IsReloading
                    || blackboard.DistanceToTarget <= manaAdjustedDangerRange + 1f; // +1f กันเผลอออกตอนระยะแกว่งอยู่ขอบพอดี

                if (stillDangerous)
                {
                    reason = "ยังอยู่ในสถานการณ์อันตราย (HP/กระสุน/ระยะ) → หลบอยู่ในที่กำบังต่อ";
                    return EnemyState.Cover;
                }
                // ปลอดภัยพอแล้ว: ปล่อยให้ไหลลงไปประเมินใหม่ด้านล่างตามปกติ (ออกจากที่กำบัง)
            }

            // 3) เห็นผู้เล่นอยู่: ประเมินความเสี่ยง
            // หมายเหตุ: ค่า ThreatLevel ถูกคำนวณไว้แล้วใน EnemyBrain หลังขั้นรับรู้ของทุกเฟรม
            // ไม่คำนวณซ้ำที่นี่ เพื่อให้มีแหล่งความจริงเดียว (Single Source of Truth)
            bool shouldSeekCover = CoverDecision.ShouldSeekCover(blackboard, blackboard.DistanceToTarget, manaAdjustedDangerRange);
            if (shouldSeekCover)
            {
                CoverPoint cover = CoverScanner.FindBestAvailableCover(selfPosition, blackboard.CurrentTarget != null ? (Vector2)blackboard.CurrentTarget.position : selfPosition, coverSearchRadius, coverMask, obstacleMask);
                if (cover != null)
                {
                    blackboard.CurrentCover = cover.transform;
                    cover.Occupy();

                    string coverCause = blackboard.IsLowHP ? "HP ต่ำ" : (blackboard.IsReloading || blackboard.CurrentAmmo <= 0) ? "กระสุนหมด/กำลังรีโหลด" : "ผู้เล่นเข้าใกล้เกินไป";
                    string manaNote = blackboard.PlayerManaLow ? " (แต่ผู้เล่น Mana ต่ำ ยังกล้าเสี่ยงอยู่บ้าง)" : " (ผู้เล่น Mana เต็ม ระวังตัวไว้ก่อน)";
                    reason = $"เห็นผู้เล่น แต่ {coverCause} → หลบเข้าที่กำบัง{manaNote}";
                    return EnemyState.Cover;
                }
            }

            // 4) ระยะไกลเกินไปที่จะยิง -> ไล่ตามก่อน (มี Hysteresis กันแกว่งกับ Combat)
            float chaseCombatThreshold = dangerRange * 2f;
            float chaseHysteresis = 0.75f;
            bool effectiveTooFar = currentState == EnemyState.Combat
                ? blackboard.DistanceToTarget > chaseCombatThreshold + chaseHysteresis  // อยู่ Combat อยู่แล้ว ต้องไกลกว่านี้ถึงจะออกไปไล่
                : blackboard.DistanceToTarget > chaseCombatThreshold - chaseHysteresis; // ยังไม่ได้ Combat ต้องใกล้กว่านี้ถึงจะเข้า

            if (effectiveTooFar)
            {
                // Healer ไม่ไล่ตามผู้เล่น (§4 — attack only when safe, ไม่ออกบุกเดี่ยว)
                // กลับไปทำหน้าที่ฮีล หรือเฝ้าตำแหน่งแทน
                if (blackboard.IsHealerRole)
                {
                    reason = "Healer: ผู้เล่นอยู่ไกล → ไม่ไล่ตาม กลับไปดูแลเพื่อน";
                    return blackboard.WoundedAllyExists ? EnemyState.HealAlly : EnemyState.Patrol;
                }

                reason = $"เห็นผู้เล่นแต่ระยะไกลเกินไป ({blackboard.DistanceToTarget:F1}m) → วิ่งเข้าไปไล่ตาม";
                return EnemyState.Chase;
            }

            // 5) กรณีปกติ: เข้าสู่ Combat
            //    Healer เข้า state Combat ได้ — มันจะ "รักษาระยะ" ด้วยการถอยเมื่อผู้เล่นใกล้เกิน
            //    PreferredMinRange ตามที่ preset ตั้งไว้ (และยิงไม่ออกเพราะไม่มีกระสุนให้)
            //    ซึ่งตรงกับ "attack only when safe / keep distance" ของบทบาท healer (§4/§29)
            reason = $"เห็นผู้เล่นในระยะยิง ({blackboard.DistanceToTarget:F1}m) HP/กระสุนพร้อม → เข้าปะทะ";
            return EnemyState.Combat;
        }
    }
}

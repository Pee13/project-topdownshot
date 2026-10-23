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
    ///
    /// หมายเหตุประสิทธิภาพ:
    /// - กระสุน: อ่านจาก blackboard.BulletIncoming (EnemyBrain คำนวณครั้งเดียวตาม bulletDetectInterval)
    /// - ที่กำบัง: cache ไว้ใน blackboard.CurrentCover — สแกนใหม่เมื่อ cover หาย/หมดคูลดาวน์ 0.5 วิ
    ///   (ของเดิมยิง CoverScanner 9 เส้นต่อ candidate ทุกเฟรมระหว่างเดินเข้า cover + ทำให้ cover "กระโดด")
    /// - เหตุผล: buildReason=false ตอนไม่มี Debug HUD → ไม่ allocate string ทุกเฟรม
    /// </summary>
    public static class TacticalDecision
    {
        /// <summary>เวลารอสแกนหาที่กำบังใหม่เมื่อ cover เดิมยังใช้ได้ (วินาที)</summary>
        private const float CoverRescanInterval = 0.5f;

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
            out string reason,
            bool buildReason = true)
        {
            // 1) Priority สูงสุด: เห็นผู้เล่นอยู่ + กระสุนจะโดนจริง -> Dodge
            //    (ไม่ใช้การทอยสุ่มแล้ว — หลบเมื่อการทำนายบอกว่ากระสุนจะพุ่งโดนตัวเราจริง §17/§18)
            //    และบาง Role เช่น Tank ไม่ dodge แบบปกติ (§29) — ใช้ตัวมันบังกระสุนแทน
            //    ผลตรวจกระสุนมาจาก EnemyBrain (blackboard.BulletIncoming) — ไม่ยิง OverlapCircle เองทุกเฟรม
            if (blackboard.CanSeeTarget && blackboard.CanDodge && blackboard.BulletIncoming)
            {
                if (Dodge.DodgeDecision.ShouldDodge(true, selfPosition, blackboard.BulletPosition, blackboard.BulletVelocity, blackboard.BodyRadius))
                {
                    reason = buildReason ? "เห็นผู้เล่น + ทำนายแล้วว่ากระสุนจะโดนจริง → หลบด่วน!" : null;
                    return EnemyState.Dodge;
                }
            }

            // 1.2/1.3) TANK duty (คุ้มกัน Healer / สกัดเพื่อน) — มีเงื่อนไข 2 ข้อก่อน:
            //
            // a) Self-bailout: เลือดวิกฤต (≤ critical) + มี healer → "การอยู่รอด" มาก่อนหน้าที่
            //    (ของเดิม: duty branch อยู่ก่อน retreat + priority เท่ากัน → tank 5% HP ยังยืนบังจนตาย)
            // b) Leash: อยู่ห่าง healer เกิน TankLeashRange → ไม่ไล่ทำ duty ข้ามแมพ
            //    (ของเดิม: ไม่มีระยะจำกัด เดินข้ามแมพไปกลางทางผู้เล่น-เพื่อน)
            bool tankMustSurvive = blackboard.IsTankRole
                && blackboard.HealerAvailable
                && blackboard.SelfHealthPercent <= blackboard.CriticalRetreatHealthThreshold;
            bool tankWithinLeash = !blackboard.IsTankRole
                || blackboard.HealerDistance <= blackboard.TankLeashRange;

            // TANK: Healer ถูกคุกคาม → ออกไปบัง (§2 priority 1, §8/§9)
            //      วางไว้ก่อนกิ่งถอย เพราะหน้าที่อันดับแรกของ Tank คือคุ้มกัน
            //      (และ RetreatOnlyWhenCritical ทำให้ tank ถอยเฉพาะเลือดวิกฤตอยู่ดี)
            if (blackboard.IsTankRole && blackboard.HealerThreatened && !tankMustSurvive && tankWithinLeash)
            {
                reason = buildReason ? "Tank: Healer ถูกคุกคาม! → ออกไปบังระหว่างผู้เล่นกับ Healer (PROTECT_HEALER)" : null;
                return EnemyState.ProtectHealer;
            }

            // TANK: เพื่อนกำลังหนีและถูกผู้เล่นไล่ → เข้าไปสกัด/ดึงดูด (§2 priority 2-4, §10/§21)
            if (blackboard.IsTankRole && blackboard.RetreatingAllyUnderThreat && !tankMustSurvive && tankWithinLeash)
            {
                reason = buildReason ? "Tank: เพื่อนกำลังหนีและถูกไล่ → เข้าไปขวาง/สกัดผู้เล่น (PEEL_ALLY)" : null;
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
                    reason = buildReason ? $"เลือดเหลือ {blackboard.SelfHealthPercent * 100f:F0}% และอยู่ในระยะฮีลแล้ว → อยู่กับที่รอฮีล (SEEK_HEAL)" : null;
                    return EnemyState.SeekHeal;
                }

                bool isCritical = blackboard.SelfHealthPercent <= blackboard.CriticalRetreatHealthThreshold;
                reason = buildReason
                    ? (isCritical
                        ? $"⚠ เลือดวิกฤต {blackboard.SelfHealthPercent * 100f:F0}%! เอาชีวิตรอดมาก่อน → ถอยหนีไปหา Healer"
                        : $"เลือดเหลือ {blackboard.SelfHealthPercent * 100f:F0}% → ถอยกลับไปหา Healer (RETREAT)")
                    : null;
                return EnemyState.Retreat;
            }

            // 2.4) Healer: ฮีลเพื่อนที่บาดเจ็บ (§4/§14)
            //      Healer ทำงานได้ทั้งตอนเห็นและไม่เห็นผู้เล่น — การฮีลไม่ต้องรู้ว่าผู้เล่นอยู่ไหน
            //      วางไว้ก่อนกิ่ง Patrol/Search เพื่อให้ฮีลได้แม้ไม่เห็นผู้เล่นเลย
            if (blackboard.IsHealerRole && blackboard.WoundedAllyExists)
            {
                reason = buildReason ? "Healer: มีเพื่อนบาดเจ็บ → เข้าไปฮีล (HEAL_ALLY)" : null;
                return EnemyState.HealAlly;
            }

            // 2.5) ไม่เห็นผู้เล่นเลย
            if (!blackboard.CanSeeTarget && !blackboard.HasMemory)
            {
                // Healer: ไม่ไป Patrol เดินมั่ว — เดินตามเพื่อนเตรียมพร้อมฮีลแทน (§4)
                if (blackboard.IsHealerRole)
                {
                    reason = buildReason ? "Healer: ไม่มีอะไรเร่งด่วน → เดินตามเพื่อนเตรียมพร้อมฮีล (FOLLOW_ALLY)" : null;
                    return EnemyState.FollowAlly;
                }

                reason = buildReason ? "ไม่เห็นผู้เล่น และไม่มีความจำเก่า → เดินตรวจตราตามปกติ" : null;
                return EnemyState.Patrol;
            }

            if (!blackboard.CanSeeTarget && blackboard.HasMemory)
            {
                // Healer ไม่ออกค้นหาเอง — หน้าที่คืออยู่ให้ปลอดภัยและอยู่กับเพื่อน (§4)
                if (blackboard.IsHealerRole)
                {
                    if (blackboard.WoundedAllyExists)
                    {
                        reason = buildReason ? "Healer: เสียสายตาผู้เล่น แต่มีเพื่อนรอฮีล → เข้าไปฮีล" : null;
                        return EnemyState.HealAlly;
                    }
                    reason = buildReason ? "Healer: เสียสายตาผู้เล่น → ไม่ออกค้นหาเอง กลับไปเดินตามเพื่อน" : null;
                    return EnemyState.FollowAlly;
                }

                // ── Pursuit Grace: เพิ่งคลาดสายตา (หรือเพิ่งโดนยิงจากด้านหลัง) → ไล่ต่ออีกครู่ ──
                if (blackboard.InPursuitGrace)
                {
                    float left = blackboard.PursuitGraceTime - (Time.time - blackboard.LostSightTime);
                    reason = buildReason ? $"เพิ่งคลาดสายตา → ไล่ต่ออีก {Mathf.Max(0f, left):F1}s ก่อนค้นหา" : null;
                    return EnemyState.Chase;
                }

                // ── Intercept (ระบบอ้อมดักแบบใหม่): เพื่อนยัง "เห็นผู้เล่นอยู่จริง" ตอนนี้ ──
                // → ไม่ต้องไปค้นจุดเก่า รีบไปตำแหน่งที่เพื่อนเจอ + จัดวงล้อมปิดทางหนีทันที
                // (คนแรกเห็น → ทุกตัวที่มีความจำเข้าโหมดอ้อมดัก = ผู้เล่นหนีไม่หลุด)
                if (GroupAI.Instance != null && GroupAI.Instance.TryGetContact(out _))
                {
                    reason = buildReason ? "เพื่อนยังเห็นผู้เล่นอยู่ (Active Contact) → รีบไปอ้อมดัก (INTERCEPT)" : null;
                    return EnemyState.Chase;
                }

                // Peripheral: รู้สึกได้รอบตัวแบบอ่อนๆ จากทรงกลม (มีคนข้างหลังแต่ยังเห็นไม่ชัด)
                // → หยุดหันไปจ้องทิศนั้นก่อน (Suspicious) ไม่รีบวิ่งไปแบบสุ่มสี่สุ่มห้า
                if (blackboard.PeripheralHit)
                {
                    reason = buildReason ? "รู้สึกได้รอบตัว (Peripheral Vision) → หยุดหันไปจ้องทิศที่รู้สึก" : null;
                    return EnemyState.Suspicious;
                }

                reason = buildReason ? $"เพิ่งเห็นผู้เล่นหายไป (จำได้อีก {blackboard.LastSeen.TimeSeen:F0}s ที่แล้ว) → ไปดูจุดล่าสุด/ค้นหา" : null;
                return EnemyState.Search;
            }

            // 2.6) เห็นผู้เล่นอยู่ แต่ยังไม่ "ยืนยัน" แน่ชัด (Alert Phase / Suspicious)
            // ต้องเห็นต่อเนื่องสะสมความสงสัยจนครบก่อน ถึงจะเข้าสู่ Combat/Chase/Cover เต็มตัว
            // (กันไม่ให้ AI "รู้ทันที" ว่าเจอผู้เล่นแบบไม่สมจริง เหมือนโหมด Caution ก่อน Alert เต็มตัวใน MGS)
            if (blackboard.CanSeeTarget && !blackboard.IsTargetConfirmed)
            {
                // Healer: "ไม่หยุดยืนสงสัย" — เดินตามเพื่อน/ฮีลต่อไป ความสงสัยยังสะสมใน EnemyBrain เหมือนเดิม
                // พอครบ 100% → IsTargetConfirmed → เข้า Combat เองได้ตามปกติ
                // (แก้อาการ log: FollowAlly ↔ Suspicious สลับรัว = ฮีลเลอร์หยุดยืน-เดิน-หยุด ดูกระตุก)
                if (blackboard.IsHealerRole)
                {
                    reason = buildReason ? "Healer: เห็นเงาๆ ยังไม่ยืนยัน → ไม่หยุดสงสัย เดินตามเพื่อน/ฮีลต่อ" : null;
                    return EnemyState.FollowAlly;
                }

                reason = buildReason ? $"เห็นเงาๆ กำลังตรวจสอบให้แน่ใจ... (สงสัย {blackboard.SuspicionLevel * 100f:F0}%)" : null;
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
                    reason = buildReason ? "ยังอยู่ในสถานการณ์อันตราย (HP/กระสุน/ระยะ) → หลบอยู่ในที่กำบังต่อ" : null;
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
                // ── ใช้ cover ที่ cache ไว้ก่อน — สแกนใหม่เฉพาะเมื่อ cover หาย/ถูกทำลาย/หมดคูลดาวน์ ──
                // (ของเดิมเรียก CoverScanner ทุกเฟรม: OverlapCircleAll + 9 raycasts ต่อ candidate
                //  และเพราะ FindBestAvailableCover "ข้าม cover ที่ occupied" รวมของตัวเอง
                //  ทำให้เลือก cover ใหม่สลับไปมา = cover hopping + leak ของ flag Occupy)
                bool coverCacheValid = blackboard.CurrentCover != null; // Unity null = ถูกทำลาย
                if (!coverCacheValid || blackboard.CoverScanCooldown <= 0f)
                {
                    CoverPoint cover = CoverScanner.FindBestAvailableCover(
                        selfPosition,
                        blackboard.CurrentTarget != null ? (Vector2)blackboard.CurrentTarget.position : selfPosition,
                        coverSearchRadius, coverMask, obstacleMask);

                    if (cover != null)
                    {
                        // ปลด cover เก่า (ของตัวเอง) ก่อนสลับ — กัน flag Occupy รั่ว
                        if (blackboard.CurrentCover != null
                            && blackboard.CurrentCover.TryGetComponent(out CoverPoint oldCover))
                        {
                            oldCover.Release();
                        }

                        blackboard.CurrentCover = cover.transform;
                        cover.Occupy();
                        blackboard.CoverScanCooldown = CoverRescanInterval;
                        coverCacheValid = true;
                    }
                    else if (!coverCacheValid)
                    {
                        // ไม่มี cover ในรอบนี้ — รอคูลดาวน์แล้วค่อยสแกนใหม่ ไม่ยิงซ้ำทุกเฟรม
                        blackboard.CoverScanCooldown = CoverRescanInterval;
                    }
                }

                if (coverCacheValid)
                {
                    string coverCause = blackboard.IsLowHP ? "HP ต่ำ" : (blackboard.IsReloading || blackboard.CurrentAmmo <= 0) ? "กระสุนหมด/กำลังรีโหลด" : "ผู้เล่นเข้าใกล้เกินไป";
                    string manaNote = blackboard.PlayerManaLow ? " (แต่ผู้เล่น Mana ต่ำ ยังกล้าเสี่ยงอยู่บ้าง)" : " (ผู้เล่น Mana เต็ม ระวังตัวไว้ก่อน)";
                    reason = buildReason ? $"เห็นผู้เล่น แต่ {coverCause} → หลบเข้าที่กำบัง{manaNote}" : null;
                    return EnemyState.Cover;
                }
            }

            // 4) ระยะไกลเกินไปที่จะยิง -> ไล่ตามก่อน (มี Hysteresis กันแกว่งกับ Combat)
            float chaseCombatThreshold = dangerRange * 2f;
            float chaseHysteresis = 1.5f; // กว้างขึ้นจาก 0.75 — (log จริง: Tank เข้า-ออกระยะ 5.3↔7.3↔9.4 สลับถี่ = เดินเข้า-ออกรอบเดิม)
            bool effectiveTooFar = currentState == EnemyState.Combat || currentState == EnemyState.Flank
                ? blackboard.DistanceToTarget > chaseCombatThreshold + chaseHysteresis  // อยู่ Combat อยู่แล้ว ต้องไกลกว่านี้ถึงจะออกไปไล่
                : blackboard.DistanceToTarget > chaseCombatThreshold - chaseHysteresis; // ยังไม่ได้ Combat ต้องใกล้กว่านี้ถึงจะเข้า

            if (effectiveTooFar)
            {
                // Healer ไม่ไล่ตามผู้เล่น (§4 — attack only when safe, ไม่ออกบุกเดี่ยว)
                // กลับไปทำหน้าที่ฮีล หรือเดินตามเพื่อนเตรียมพร้อมแทน
                if (blackboard.IsHealerRole)
                {
                    if (blackboard.WoundedAllyExists)
                    {
                        reason = buildReason ? "Healer: ผู้เล่นอยู่ไกล แต่มีเพื่อนรอฮีล → กลับไปฮีล" : null;
                        return EnemyState.HealAlly;
                    }
                    reason = buildReason ? "Healer: ผู้เล่นอยู่ไกล → ไม่ไล่ตาม กลับไปเดินตามเพื่อน (FOLLOW_ALLY)" : null;
                    return EnemyState.FollowAlly;
                }

                reason = buildReason ? $"เห็นผู้เล่นแต่ระยะไกลเกินไป ({blackboard.DistanceToTarget:F1}m) → วิ่งเข้าไปไล่ตาม" : null;
                return EnemyState.Chase;
            }

            // 4.5) FLANKER: เข้า Flank แทน Combat ปกติ (§3/§11/§25)
            //      อ้อมหาตำแหน่งข้าง/หลัง + รักษาระยะ (§7) + หันหน้าหาผู้เล่นเสมอ (§25)
            //      + ยิงระหว่างวน (§3 priority 5)
            if (blackboard.IsFlankerRole)
            {
                reason = buildReason ? "Flanker: เข้าปะทะ → อ้อมหาตำแหน่งข้าง/หลังแล้ววนยิง (FLANK)" : null;
                return EnemyState.Flank;
            }

            // 5) กรณีปกติ: เข้าสู่ Combat
            //    Healer เข้า state Combat ได้ — มันจะ "รักษาระยะ" ด้วยการถอยเมื่อผู้เล่นใกล้เกิน
            //    PreferredMinRange ตามที่ preset ตั้งไว้ (และยิงไม่ออกเพราะไม่มีกระสุนให้)
            //    ซึ่งตรงกับ "attack only when safe / keep distance" ของบทบาท healer (§4/§29)
            reason = buildReason ? $"เห็นผู้เล่นในระยะยิง ({blackboard.DistanceToTarget:F1}m) HP/กระสุนพร้อม → เข้าปะทะ" : null;
            return EnemyState.Combat;
        }
    }
}
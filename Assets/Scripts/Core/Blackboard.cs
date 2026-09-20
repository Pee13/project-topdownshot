using UnityEngine;
using TopDownTacticalAI.Memory;

namespace TopDownTacticalAI.Core
{
    /// <summary>
    /// พื้นที่เก็บข้อมูลกลางที่ทุกระบบ (Vision, Memory, Combat, Cover, Tactical ฯลฯ)
    /// อ่าน/เขียนร่วมกันได้ เพื่อลด Dependency ระหว่างระบบโดยตรง
    /// </summary>
    public class Blackboard
    {
        // ---- Target Info ----
        public Transform CurrentTarget;
        public bool CanSeeTarget;
        public float DistanceToTarget;

        // ---- Memory ----
        public LastSeenData LastSeen = new LastSeenData();
        public bool HasMemory;

        // ---- Combat ----
        public int CurrentAmmo;
        public int MaxAmmo = 12;
        public bool IsReloading;
        public bool TargetIsReloading;

        // ---- Health / Status ----
        public float CurrentHP = 100f;
        public float MaxHP = 100f;
        public bool IsLowHP => CurrentHP <= MaxHP * 0.3f;

        // ---- Cover ----
        public Transform CurrentCover;
        public bool InCover;
        public bool IsPeeking;

        // ---- Dodge ----
        public bool IsDodging;
        public Vector2 DodgeDirection;

        // บางบทบาทไม่ควรหลบแบบเบี่ยงข้าง (§29) เช่น Tank ที่ต้องยืนบังกระสุนแทนเพื่อน
        // ตั้งค่าจาก Role ใน EnemyBrain.ApplyRolePreset() แล้ว TacticalDecision จะข้ามกิ่ง Dodge ให้เอง
        public bool CanDodge = true;

        // ---- Group ----
        public int NearbyAllyCount;

        // ---- Player Mana Awareness (ตามเอกสารบทที่ 3.1.2) ----
        // ใช้ตัดสินใจว่าควร "ระมัดระวัง" (ผู้เล่น Mana สูง เน้น Safety) หรือ "รุกราน" (ผู้เล่น Mana ต่ำ เน้นโอกาสโจมตี)
        public bool PlayerManaLow;

        // ---- Alert Phase (สถานะสงสัยก่อนยืนยันว่าเจอผู้เล่นจริง) ----
        // สะสมขึ้นเรื่อยๆ ขณะเห็นผู้เล่น (CanSeeTarget) ลดลงเรื่อยๆ ขณะไม่เห็น
        // 0 = ยังไม่สงสัยเลย, 1 = ยืนยันแน่นอนแล้วว่าเจอผู้เล่นจริง (ถึงจะเข้า Chase/Combat/Cover เต็มตัวได้)
        public float SuspicionLevel;
        public bool IsTargetConfirmed;

        // ---- Path Visualization ----
        // ตำแหน่งที่ State ปัจจุบันกำลัง "ตั้งใจ" จะเดินไป อัปเดตทุกเฟรมโดย State ที่กำลังทำงานอยู่
        // ใช้โดย AIPathVisualizer วาดเป็นเส้นบอกทิศทาง ไม่ใช่ค่าที่ระบบตัดสินใจอื่นใช้งาน
        public Vector2 CurrentDestination;

        // ---- Confidence (0..1) ----
        // ทุกข้อมูลที่ AI "ไม่แน่ใจ" ต้องมีค่าความมั่นใจกำกับ ไม่ใช่ true/false เปล่าๆ
        // คะแนนสุดท้ายจะถูกคูณด้วย confidence เสมอ เพื่อไม่ให้ AI เชื่อข้อมูลที่ไม่แน่นอนเต็ม 100%
        public float VisionConfidence;      // เขียนโดย VisionSensor — ระยะ/FOV/Line of Sight
        public float MemoryConfidence;      // เขียนโดย EnemyMemory — ลดลงตามเวลาที่ผ่านไป
        public float PredictionConfidence;  // เขียนโดย ChaseState — ความนิ่งของทิศทางเป้าหมาย

        // ---- Threat (0..1) ----
        public float ThreatLevel;      // ค่าดิบจาก RiskEvaluation (เขียนใน TacticalDecision)
        public float SmoothedThreat;   // ค่าที่ผ่าน temporal smoothing แล้ว (เขียนใน EnemyBrain)

        // ---- ค่าที่คำนวณไว้เพื่อใช้ตัดสินใจและแสดงผล Debug ----
        public float SelfHealthPercent = 1f;
        public float DistanceScore;         // 0..1 — ยิ่งใกล้ยิ่งสูง
        public float AccessibilityScore;    // 0..1 — 0 = ไปไม่ได้

        // ความสำคัญของตัวเองต่อทีม (0..1) — ตั้งค่าจาก Role ใน EnemyBrain.Awake()
        // ตัวสนับสนุนมีค่าสูงสุด เพราะถ้าตายทีมจะขาดการฮีล ส่วนตัวบุกมีค่าต่ำกว่าเพราะทดแทนได้ง่าย
        // ใช้ถ่วงการตัดสินใจ: ตัวสำคัญจะยอมหลบเข้าที่กำบังมากกว่า และไม่เข้าปะทะซึ่งๆ หน้า
        public float SelfRoleImportance = 0.6f;

        // ---- ข้อมูลทีม (§20) — เขียนโดย EnemyBrain ทุกเฟรม (หรือตาม teamInterval) ----
        // Healer ที่ใกล้ที่สุด: ใช้โดยตัวที่เลือดน้อยตอนถอยกลับไปหา (§18)
        // และใช้ยืนยันว่า "การถอยมีประโยชน์" — ถ้า healer ตายหมด การถอยก็ไม่ช่วยแล้ว
        public bool HealerAvailable;
        public Transform HealerTransform;
        public float HealerDistance = float.MaxValue;

        // มีเพื่อนที่ควรได้รับการฮีลอยู่ไหม — precondition ของ state HealAlly (§14)
        public bool WoundedAllyExists;

        // ---- การตั้งค่าระบบฮีล/ถอย (ตั้งจาก EnemyBrain ให้ TacticalDecision/State อ่าน) ----
        public bool IsHealerRole;                       // ตัวนี้เป็น healer หรือเปล่า
        public bool IsTankRole;                         // ตัวนี้เป็น Tank (บัง/คุ้มกันเพื่อน) หรือเปล่า
        public bool RetreatOnlyWhenCritical;            // Tank — ถอยเฉพาะเมื่อเลือดวิกฤต (§2 priority 7)

        // ---- Tank Protect/Peel (§8/§9/§10/§21) — เขียนโดยทีมบัสใน EnemyBrain ----
        public bool HealerThreatened;                   // ผู้เล่นมองเห็น/เข้าใกล้ Healer จนถึงขั้นอันตราย
        public Transform RetreatingAllyTransform;       // เพื่อนที่กำลังถอยและถูกผู้เล่นไล่
        public bool RetreatingAllyUnderThreat;          // มีเพื่อนกำลังถอยและถูกผู้เล่นไล่ตาม
        public float RetreatHealthThreshold = 0.30f;    // เลือดต่ำกว่านี้ → Retreat (§18)
        public float CriticalRetreatHealthThreshold = 0.15f; // เลือดต่ำกว่านี้ → Critical Retreat (§19)
        public float HealRange = 25f;                   // ระยะที่ฮีลได้
        public float HealWorthThreshold = 0.80f;        // ฮีลเฉพาะเพื่อนที่เลือดต่ำกว่านี้ (§16)
        public float TankHealThreshold = 0.80f;         // Tank ต่ำกว่านี้ได้โบนัส Tank-first (§16)
        public float CriticalAllyHealthThreshold = 0.25f; // เกณฑ์ "เพื่อนวิกฤต" (§16)
        public Vector2 PredictedTargetPosition;
        public float CurrentActionScore;    // คะแนนของ Action ที่เลือกในเฟรมนี้
        public string CurrentActionReason;

        // ---- Determination Score (คะแนนความพยายาม) ----
        // ตัวเลขสะสมที่เพิ่มขึ้นเมื่อ AI ทำสิ่งที่แสดงถึงความ "ตั้งใจ/พยายาม" ไล่ล่าผู้เล่น
        // เช่น เจอผู้เล่นครั้งแรก, เรียกพวกมาช่วย, ค้นหาไปทีละจุดไม่ยอมแพ้ ฯลฯ
        // ไม่มีผลต่อ Gameplay โดยตรง แต่ใช้แสดงผลใน Debug HUD ให้เห็นว่า AI กำลัง "พยายาม" อยู่มากแค่ไหน
        public float DeterminationScore;

        public void ResetCombatFlags()
        {
            IsReloading = false;
            IsDodging = false;
            IsPeeking = false;
        }
    }
}

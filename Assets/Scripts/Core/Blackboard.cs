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

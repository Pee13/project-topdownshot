namespace TopDownTacticalAI.Core
{
    /// <summary>
    /// สถานะหลักทั้งหมดที่ AI สามารถอยู่ได้
    ///
    /// กลุ่มพื้นฐาน: Patrol / Suspicious / Chase / Search / Combat / Cover / Dodge
    /// กลุ่มเอาชีวิตรอด (§18/§19): Retreat / SeekHeal — เลือดน้อย ถอยกลับไปหา Healer
    /// กลุ่มสนับสนุน (§4/§14): HealAlly — Healer ฮีลเพื่อนที่บาดเจ็บ
    /// </summary>
    public enum EnemyState
    {
        Patrol,
        Suspicious,
        Chase,
        Search,
        Combat,
        Cover,
        Dodge,

        // ── เอาชีวิตรอด (§18, §19) ──
        Retreat,   // เลือดต่ำกว่า RetreatHealthThreshold → ถอยกลับไปหา Healer
        SeekHeal,  // ถึงระยะฮีลแล้ว → อยู่กับที่ให้ Healer ฮีลจนหาย

        // ── สนับสนุน (§4, §14) ──
        HealAlly,  // Healer: เดินเข้าไปหา/ฮีลเพื่อนที่บาดเจ็บ

        // ── Tank (§2, §8, §9, §10, §21) ──
        ProtectHealer, // ยืนบังระหว่างผู้เล่นกับ Healer — คำนวณตำแหน่งแบบ dynamic
        PeelAlly,      // สกัดผู้เล่นที่ไล่ตามเพื่อนที่หนี (รวม BAIT — บังคับให้ผู้เล่นเปลี่ยนเป้า)

        // ── Flanker (§3, §11, §25) ──
        Flank,         // เข้าปะทะโดยอ้อมหาตำแหน่งข้าง/หลัง + รักษาระยะ + หันหน้าหาผู้เล่นเสมอ

        FollowTank
    }
}

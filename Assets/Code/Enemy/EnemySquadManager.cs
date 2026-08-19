using UnityEngine;

/// <summary>
/// ตัวกลางกระจายสัญญาณเตือนภัยให้ศัตรูทุกตัวในฉาก
/// EnemyAI.cs จะ auto-find component นี้เองใน Start() ผ่าน FindObjectOfType&lt;EnemySquadManager&gt;()
/// แล้วเรียก squad.BroadcastAlert(player.position, this) ทันทีที่เห็นผู้เล่นครั้งแรก
///
/// วิธีติดตั้ง: สร้าง Empty GameObject ชื่อ "SquadManager" วางไว้ในฉาก แปะสคริปต์นี้ (มีตัวเดียวพอทั้งฉาก)
/// </summary>
public class EnemySquadManager : MonoBehaviour
{
    [Header("── Alert Settings ──")]
    [Tooltip("รัศมีที่สัญญาณเตือนไปถึง (หน่วยเดียวกับ Unity Units) ตั้งเป็น 0 หรือติดลบ = แจ้งทุกตัวในฉากไม่จำกัดระยะ")]
    public float alertRadius = 800f;

    [Tooltip("ระยะเวลาที่ศัตรูตัวอื่นจะจำตำแหน่งที่ได้รับแจ้งไว้ (ส่งต่อให้ EnemyAI.ReceiveAlert)")]
    public float alertDuration = 3f;

    /// <summary>บันทึกประวัติการแจ้งเตือนล่าสุด — ใช้โดย SquadDebugDisplay เพื่อยืนยันบนจอว่า BroadcastAlert ถูกเรียกจริง</summary>
    public struct AlertLogEntry
    {
        public float time;
        public string sourceName;
        public int notifiedCount;
    }
    [HideInInspector] public System.Collections.Generic.List<AlertLogEntry> recentAlerts = new System.Collections.Generic.List<AlertLogEntry>();

    /// <summary>แจ้งเตือนศัตรูทุกตัวในฉาก (ยกเว้นตัวที่ส่งสัญญาณเอง) ว่าเจอผู้เล่นที่ตำแหน่งนี้</summary>
    public void BroadcastAlert(Vector3 position, EnemyAI source)
    {
        // ใช้ EnemyAI.AllActive (ลงทะเบียนตัวเอง) แทน FindObjectsByType ที่บางโปรเจกต์หา instance ไม่เจอ
        var allEnemies = EnemyAI.AllActive;
        int notifiedCount = 0;

        foreach (var enemy in allEnemies)
        {
            if (enemy == source) continue;

            if (alertRadius > 0f)
            {
                float dist = Vector2.Distance(enemy.transform.position, position);
                if (dist > alertRadius) continue; // ไกลเกินไป ไม่ได้ยิน/ไม่รู้เรื่อง
            }

            enemy.ReceiveAlert(position, alertDuration);
            notifiedCount++;
        }

        recentAlerts.Add(new AlertLogEntry { time = Time.time, sourceName = source != null ? source.name : "?", notifiedCount = notifiedCount });
        if (recentAlerts.Count > 8) recentAlerts.RemoveAt(0);
    }

    /// <summary>
    /// หาลำดับ (index) ของตัวนี้ ในกลุ่มศัตรูทุกตัวที่กำลัง "ค้นหา" (Search) อยู่จริงๆ ตอนนี้
    /// ใช้ให้ EnemyAI แบ่งทิศทางค้นหากันคนละมุม แทนที่จะเดินไปจุดเดียวกันหมดทุกตัว (ดู squadSearchSpreadAngle)
    /// index 0 = ตัวหลัก (ค้นหาตามทิศที่คาดเดาไว้ตรงๆ), index อื่นๆ = แยกมุมออกไปสำรวจเพิ่ม
    /// </summary>
    public int GetSearchIndex(EnemyAI self, out int totalSearching)
    {
        var allEnemies = EnemyAI.AllActive;

        int index = -1;
        int count = 0;
        foreach (var enemy in allEnemies)
        {
            if (enemy == null) continue;
            if (enemy.currentBehaviorState != EnemyAI.BehaviorState.Search) continue;

            if (enemy == self) index = count;
            count++;
        }

        totalSearching = count;
        return index;
    }

    [Header("── Map Coverage (กระจาย AI ให้คุมพื้นที่ทั่วแมพตอนไม่มีอะไรกระตุ้น) ──")]
    [Tooltip("จุดศูนย์กลางแมพ ใช้กำหนดขอบเขตที่ AI จะกระจายตัวออกไปสำรวจตอน Patrol/Wander (ตั้งให้ครอบคลุมพื้นที่เล่นจริงทั้งหมด)")]
    public Vector2 mapCenter = Vector2.zero;
    [Tooltip("ขนาดแมพ (กว้าง, สูง) คู่กับ Map Center — ถ้าปล่อยเป็น (0,0) จะถือว่ายังไม่ได้ตั้งค่า ระบบกระจายพื้นที่จะไม่ทำงาน (EnemyAI จะ Fallback ไปใช้วิธีวนใกล้จุดเกิดตัวเองแบบเดิม)")]
    public Vector2 mapSize = Vector2.zero;
    [Tooltip("จำนวนจุดที่จะสุ่มลองต่อครั้ง ตอนหาตำแหน่งกระจายตัวใหม่ ยิ่งเยอะยิ่งกระจายตัวได้ดีขึ้นแต่กิน Performance มากขึ้น")]
    public int coverageSampleCount = 12;

    /// <summary>
    /// หาจุดกระจายตัวใหม่ทั่วแมพ (ภายในขอบเขต mapCenter/mapSize) โดยเลี่ยงพื้นที่ที่มีเพื่อนตัวอื่นคุมอยู่แล้ว
    /// สุ่มลองหลายจุด (coverageSampleCount) แล้วเลือกจุดที่ "ห่างจากเพื่อนตัวที่ใกล้ที่สุด" มากที่สุด
    /// — ถ้าทุกจุดมีเพื่อนอยู่ใกล้หมด (แมพเล็ก/AI เยอะ) จะคืนจุดที่ดีที่สุดเท่าที่หาได้ ไม่บังคับเลี่ยงจนสุดโต่ง
    /// (ยอมให้ AI อยู่ด้วยกันในพื้นที่เดียวกันได้ถ้าจำเป็นจริงๆ ตามที่อธิบายไว้)
    /// แก้ปัญหาที่เจอ: AI ทุกตัวเดินวนอยู่แค่ใกล้จุดเกิดตัวเอง ถ้าจุดเกิดกระจุกกันอยู่ฝั่งเดียว
    /// จะไม่มีตัวไหนเดินไปสำรวจฝั่งอื่นของแมพเลยตลอดไป แม้ผู้เล่นจะยืนอยู่แถวนั้นก็ตาม
    /// </summary>
    public Vector2 GetSpreadOutPosition(EnemyAI requester)
    {
        var allEnemies = EnemyAI.AllActive;

        Vector2 best = mapCenter;
        float bestMinDist = -1f;

        for (int i = 0; i < coverageSampleCount; i++)
        {
            Vector2 candidate = mapCenter + new Vector2(
                Random.Range(-mapSize.x * 0.5f, mapSize.x * 0.5f),
                Random.Range(-mapSize.y * 0.5f, mapSize.y * 0.5f));

            float minDistToOther = float.MaxValue;
            foreach (var enemy in allEnemies)
            {
                if (enemy == null || enemy == requester) continue;
                float d = Vector2.Distance(candidate, enemy.transform.position);
                if (d < minDistToOther) minDistToOther = d;
            }

            if (minDistToOther > bestMinDist)
            {
                bestMinDist = minDistToOther;
                best = candidate;
            }
        }

        return best;
    }
}

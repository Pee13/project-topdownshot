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

    /// <summary>แจ้งเตือนศัตรูทุกตัวในฉาก (ยกเว้นตัวที่ส่งสัญญาณเอง) ว่าเจอผู้เล่นที่ตำแหน่งนี้</summary>
    public void BroadcastAlert(Vector3 position, EnemyAI source)
    {
        EnemyAI[] allEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);

        foreach (var enemy in allEnemies)
        {
            if (enemy == source) continue;

            if (alertRadius > 0f)
            {
                float dist = Vector2.Distance(enemy.transform.position, position);
                if (dist > alertRadius) continue; // ไกลเกินไป ไม่ได้ยิน/ไม่รู้เรื่อง
            }

            enemy.ReceiveAlert(position, alertDuration);
        }
    }
}

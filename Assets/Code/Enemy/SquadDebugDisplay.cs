using UnityEngine;

/// <summary>
/// แสดงผลยืนยันว่าระบบ Squad (กระจายพื้นที่ทั่วแมพ + แจ้งเตือนเพื่อนตอนเจอผู้เล่น) กำลังทำงานอยู่จริงหรือไม่
/// แบบเห็นชัดๆ บนจอ ไม่ต้องเดา/เชื่อเฉยๆ ว่ามันทำงาน
///
/// วิธีใช้: สร้าง Empty GameObject ใหม่ (หรือแปะที่ตัวเดียวกับ EnemySquadManager ก็ได้) แล้วแปะสคริปต์นี้ กด Play
/// จะเห็น:
/// 1) แผงข้อความมุมซ้ายบน — บอกว่า Squad Manager เจอไหม, Map Coverage เปิดอยู่ไหม, แต่ละตัวเชื่อมกับ Squad หรือยัง
/// 2) เส้นสีเหลืองจากแต่ละ AI ไปยังจุดที่กำลังเดินเล่นสำรวจอยู่ (เห็น Scene View) — ถ้าเห็นเป็นจุดคนละที่กัน
///    กระจายทั่วแมพ แปลว่าระบบกระจายพื้นที่ทำงานจริง ถ้าทุกเส้นชี้ไปจุดใกล้ๆ กันหมด แปลว่ายังไม่ทำงาน
/// 3) กรอบสีฟ้า (Scene View) แสดงขอบเขตแมพที่ตั้งไว้ใน EnemySquadManager
/// 4) รายการแจ้งเตือนล่าสุดในแผงข้อความ — เห็นว่า BroadcastAlert ถูกเรียกจริงเมื่อไหร่ แจ้งไปกี่ตัว
/// </summary>
public class SquadDebugDisplay : MonoBehaviour
{
    [Header("── เปิด/ปิดการแสดงผล ──")]
    public bool showOnScreenPanel = true;
    public bool showWanderTargetLines = true;
    public bool showMapBounds = true;

    public EnemySquadManager squad;

    private void Awake()
    {
        if (squad == null) squad = FindFirstObjectByType<EnemySquadManager>();
    }

    private void OnGUI()
    {
        if (!showOnScreenPanel) return;
        if (squad == null) squad = FindFirstObjectByType<EnemySquadManager>();

        var enemies = EnemyAI.AllActive; // ใช้ระบบลงทะเบียนตัวเอง แทน FindObjectsByType ที่บางโปรเจกต์หา instance ไม่เจอ

        // ปรับขนาดตัวอักษรตามความกว้างจอ กันเล็กเกินไปตอนจอความละเอียดสูง
        float scale = Mathf.Clamp(Screen.width / 1000f, 1f, 2.2f);

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            normal = { background = MakeSolidTexture(new Color(0f, 0f, 0f, 0.85f)) }
        };
        GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(24 * scale),
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            normal = { textColor = Color.white }
        };
        GUIStyle lineStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(19 * scale),
            wordWrap = true,
            normal = { textColor = Color.white }
        };
        GUIStyle okStyle = new GUIStyle(lineStyle) { normal = { textColor = new Color(0.4f, 1f, 0.4f) } };
        GUIStyle badStyle = new GUIStyle(lineStyle) { normal = { textColor = new Color(1f, 0.4f, 0.4f) } };

        float w = 620f * scale;
        float h = (170f + enemies.Count * 30f + 140f) * scale;
        GUILayout.BeginArea(new Rect(15, 15, w, h), boxStyle);
        GUILayout.Space(6);

        bool squadOk = squad != null;
        GUILayout.Label(squadOk ? "✓ EnemySquadManager: เชื่อมต่อแล้ว" : "✗ EnemySquadManager: ไม่พบในฉาก!", squadOk ? okStyle : badStyle);

        if (squad != null)
        {
            bool mapSet = squad.mapSize.sqrMagnitude > 1f;
            GUILayout.Label(mapSet ? "✓ Map Coverage: เปิดใช้งาน" : "✗ Map Coverage: ยังไม่ตั้งค่า Map Size", mapSet ? okStyle : badStyle);
            if (mapSet)
                GUILayout.Label($"   Center={squad.mapCenter}  Size={squad.mapSize}", lineStyle);
            GUILayout.Label($"จำนวน AI ในฉากตอนนี้: {enemies.Count} ตัว", lineStyle);
        }

        GUILayout.Space(10);
        GUILayout.Label("── สถานะแต่ละตัว ──", headerStyle);
        foreach (var e in enemies)
        {
            if (e == null) continue;
            bool linked = e.squad != null;
            GUILayout.Label($"{e.name}: {e.currentBehaviorState} | Squad {(linked ? "✓" : "✗")}", linked ? lineStyle : badStyle);
        }

        if (squad != null)
        {
            GUILayout.Space(10);
            GUILayout.Label("── แจ้งเตือนล่าสุด ──", headerStyle);
            if (squad.recentAlerts.Count == 0)
            {
                GUILayout.Label("ยังไม่มีการแจ้งเตือนเลย (ยังไม่มี AI ตัวไหนเจอผู้เล่น)", lineStyle);
            }
            else
            {
                int start = Mathf.Max(0, squad.recentAlerts.Count - 4);
                for (int i = squad.recentAlerts.Count - 1; i >= start; i--)
                {
                    var log = squad.recentAlerts[i];
                    float ago = Time.time - log.time;
                    GUILayout.Label($"{log.sourceName} แจ้งเพื่อน {log.notifiedCount} ตัว ({ago:F1} วิที่แล้ว)", okStyle);
                }
            }
        }

        GUILayout.EndArea();
    }

    private Texture2D _bgTexCache;
    private Texture2D MakeSolidTexture(Color color)
    {
        if (_bgTexCache != null) return _bgTexCache;
        _bgTexCache = new Texture2D(1, 1);
        _bgTexCache.SetPixel(0, 0, color);
        _bgTexCache.Apply();
        return _bgTexCache;
    }

    private void OnDrawGizmos()
    {
        if (squad == null) squad = FindFirstObjectByType<EnemySquadManager>();
        if (squad == null) return;

        if (showMapBounds && squad.mapSize.sqrMagnitude > 1f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(squad.mapCenter, squad.mapSize);
        }

        if (showWanderTargetLines)
        {
            var enemies = EnemyAI.AllActive; // ใช้ระบบลงทะเบียนตัวเอง แทน FindObjectsByType ที่บางโปรเจกต์หา instance ไม่เจอ
            Gizmos.color = Color.yellow;
            foreach (var e in enemies)
            {
                if (e == null) continue;
                Vector3 target = e.WanderTargetDebug;
                Gizmos.DrawLine(e.transform.position, target);
                Gizmos.DrawWireSphere(target, 8f);
            }
        }
    }
}

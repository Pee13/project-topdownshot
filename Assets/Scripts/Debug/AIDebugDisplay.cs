using UnityEngine;
using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.DebugTools
{
    /// <summary>
    /// แสดง "ความคิด" ของ AI แบบเรียลไทม์ตอนกด Play เป็นกล่องข้อความลอยเหนือหัวศัตรู
    /// บอกว่าตอนนี้อยู่ State ไหน, ตัดสินใจเพราะอะไร, เห็นผู้เล่นไหม, HP/กระสุนเท่าไหร่
    /// วิธีใช้: แปะสคริปต์นี้ไว้ที่ตัวศัตรู (ต้องมี EnemyBrain อยู่ในตัวเดียวกัน) แล้วกด Play ดูใน Game View ได้เลย
    /// ไม่ต้องพึ่ง UI Canvas หรือ TextMeshPro เพราะวาดผ่าน OnGUI ตรงๆ
    /// </summary>
    [RequireComponent(typeof(EnemyBrain))]
    public class AIDebugDisplay : MonoBehaviour
    {
        [Header("Toggle")]
        [Tooltip("เปิด/ปิดการแสดงผล HUD นี้ (ปิดตอนทำ Build จริงได้)")]
        public bool ShowDebugHUD = true;

        [Tooltip("แสดงเฉพาะชื่อ State สั้นๆ พอ ไม่ต้องมีคำอธิบายเหตุผลยาวๆ")]
        public bool CompactMode = false;

        [Tooltip("แสดงคะแนนและความมั่นใจของระบบตัดสินใจ (Threat / Confidence / Action Score)\n" +
                 "ใช้ตอบคำถามว่า \"AI เลือกสิ่งนี้เพราะอะไร\" — ปิดได้ถ้า HUD รกเกินไป")]
        public bool ShowScoringMetrics = true;

        [Header("Style")]
        public Vector2 ScreenOffset = new Vector2(0f, -60f);
        public int FontSize = 12;

        private EnemyBrain _brain;
        private TopDownTacticalAI.Player.Health _health;
        private static readonly System.Collections.Generic.Dictionary<EnemyState, Color> StateColors = new System.Collections.Generic.Dictionary<EnemyState, Color>
        {
            { EnemyState.Patrol, new Color(0.6f, 0.6f, 0.6f) },
            { EnemyState.Suspicious, new Color(1f, 0.75f, 0.1f) },
            { EnemyState.Chase, new Color(1f, 0.65f, 0f) },
            { EnemyState.Search, new Color(1f, 0.9f, 0.2f) },
            { EnemyState.Combat, new Color(1f, 0.25f, 0.25f) },
            { EnemyState.Cover, new Color(0.3f, 0.6f, 1f) },
            { EnemyState.Dodge, new Color(1f, 0.2f, 0.8f) },
            { EnemyState.Retreat, new Color(1f, 0.5f, 0.1f) },
            { EnemyState.SeekHeal, new Color(0.4f, 1f, 0.5f) },
            { EnemyState.HealAlly, new Color(0.3f, 1f, 0.4f) },
            { EnemyState.ProtectHealer, new Color(1f, 0.85f, 0.3f) },
            { EnemyState.PeelAlly, new Color(0.9f, 0.6f, 0.1f) },
            { EnemyState.Flank, new Color(1f, 0.4f, 0.6f) },
        };

        private static readonly System.Collections.Generic.Dictionary<EnemyState, string> StateThaiNames = new System.Collections.Generic.Dictionary<EnemyState, string>
        {
            { EnemyState.Patrol, "ลาดตระเวน" },
            { EnemyState.Suspicious, "สงสัย" },
            { EnemyState.Chase, "ไล่ตาม" },
            { EnemyState.Search, "ค้นหา" },
            { EnemyState.Combat, "ปะทะ" },
            { EnemyState.Cover, "หลบกำบัง" },
            { EnemyState.Dodge, "หลบกระสุน" },
            { EnemyState.Retreat, "ถอยหา Healer" },
            { EnemyState.SeekHeal, "รอฮีล" },
            { EnemyState.HealAlly, "ฮีลเพื่อน" },
            { EnemyState.ProtectHealer, "บัง Healer" },
            { EnemyState.PeelAlly, "สกัดผู้เล่น" },
            { EnemyState.Flank, "อ้อมโจมตี" },
        };

        private void Awake()
        {
            _brain = GetComponent<EnemyBrain>();
            _health = GetComponent<TopDownTacticalAI.Player.Health>();
        }

        private void OnGUI()
        {
            if (!ShowDebugHUD || _brain == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(transform.position);
            if (screenPos.z < 0) return; // อยู่หลังกล้อง ไม่ต้องวาด

            // GUI ใช้พิกัด Y กลับด้านกับ Screen ปกติ ต้องแปลง
            float guiX = screenPos.x + ScreenOffset.x;
            float guiY = Screen.height - screenPos.y + ScreenOffset.y;

            AIDebugInfo info = _brain.GetDebugInfo();
            Color stateColor = StateColors.TryGetValue(info.State, out Color c) ? c : Color.white;
            string stateName = StateThaiNames.TryGetValue(info.State, out string n) ? n : info.State.ToString();

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box) { fontSize = FontSize, alignment = TextAnchor.UpperLeft, wordWrap = true };
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { fontSize = FontSize + 2, fontStyle = FontStyle.Bold, normal = { textColor = stateColor } };

            float boxWidth = CompactMode ? 90f : (ShowScoringMetrics ? 280f : 220f);
            float boxHeight = CompactMode ? 24f : (ShowScoringMetrics ? 250f : 124f);

            Rect rect = new Rect(guiX - boxWidth * 0.5f, guiY - boxHeight, boxWidth, boxHeight);

            GUI.Box(rect, GUIContent.none, boxStyle);

            GUILayout.BeginArea(rect);
            GUILayout.Label($"[{stateName}]", headerStyle);

            if (!CompactMode)
            {
                string seeStatus = info.CanSeeTarget ? "เห็นผู้เล่น" : (info.HasMemory ? "จำได้ (มองไม่เห็นตอนนี้)" : "ไม่เห็น/ไม่จำ");
                GUILayout.Label($"มองเห็น: {seeStatus}");

                if (info.CanSeeTarget)
                    GUILayout.Label($"ระยะ: {info.DistanceToTarget:F1} m");

                // อ่าน HP จาก Health component โดยตรง (แหล่งความจริงเดียว)
                // ถ้าอ่านจาก EnemyBrain อย่างเดียว ค่าจะค้างเมื่อ EnemyBrain ถูกปิด
                // หรือเมื่อเลือดถูกหักผ่าน Health โดยที่ blackboard ยังไม่อัปเดต
                float shownHP = _health != null ? _health.CurrentHP : info.CurrentHP;
                float shownMaxHP = _health != null ? _health.MaxHP : info.MaxHP;
                GUILayout.Label($"HP: {shownHP:F0}/{shownMaxHP:F0}   กระสุน: {info.CurrentAmmo}/{info.MaxAmmo}{(info.IsReloading ? " (รีโหลด)" : "")}");
                GUILayout.Label($"คิดว่า: {info.Reason}", new GUIStyle(GUI.skin.label) { fontSize = FontSize - 1, wordWrap = true, fontStyle = FontStyle.Italic });
                GUILayout.Label($"คะแนนความพยายาม: {info.DeterminationScore:F0}", new GUIStyle(GUI.skin.label) { fontSize = FontSize - 1 });

                // ── คะแนนและความมั่นใจของระบบตัดสินใจ (§52) ──
                // ส่วนนี้ตอบคำถามว่า "AI เลือกสิ่งนี้เพราะอะไร" และ
                // "ข้อมูลที่ใช้ตัดสินใจเชื่อถือได้แค่ไหน"
                if (ShowScoringMetrics)
                {
                    var smallLabel = new GUIStyle(GUI.skin.label) { fontSize = FontSize - 1 };
                    var warnLabel = new GUIStyle(smallLabel) { normal = { textColor = new Color(1f, 0.5f, 0.4f) } };

                    GUILayout.Label("── คะแนนการตัดสินใจ ──", smallLabel);

                    float threat = info.SmoothedThreat;
                    GUILayout.Label(
                        $"ภัยคุกคาม: {threat:F2} ({TopDownTacticalAI.Tactical.RiskEvaluation.DescribeThreat(threat)})",
                        threat >= 0.75f ? warnLabel : smallLabel);

                    GUILayout.Label($"เลือด: {info.SelfHealthPercent * 100f:F0}%   เพื่อนใกล้: {info.NearbyAllyCount}");

                    GUILayout.Label($"Action Score: {info.CurrentActionScore:F2}");

                    GUILayout.Label("── ความมั่นใจในข้อมูล ──", smallLabel);
                    GUILayout.Label($"มองเห็น: {info.VisionConfidence:F2}   ความจำ: {info.MemoryConfidence:F2}");
                    GUILayout.Label($"ทำนาย: {info.PredictionConfidence:F2}   ระยะ: {info.DistanceScore:F2}");

                    if (!string.IsNullOrEmpty(info.CurrentActionReason))
                        GUILayout.Label($"หมายเหตุ: {info.CurrentActionReason}", smallLabel);
                }
            }

            GUILayout.EndArea();
        }

        // วาดเส้นเชื่อมไปยังตำแหน่งล่าสุดที่เห็นผู้เล่น (Scene View เท่านั้น) ช่วยดูว่า AI "จำ" ตำแหน่งไหนอยู่
        private void OnDrawGizmosSelected()
        {
            if (_brain == null) _brain = GetComponent<EnemyBrain>();
            if (_brain == null || !Application.isPlaying) return;

            AIDebugInfo info = _brain.GetDebugInfo();
            if (info.LastSeenValid)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, info.LastSeenPosition);
                Gizmos.DrawWireSphere(info.LastSeenPosition, 0.3f);
            }
        }
    }
}

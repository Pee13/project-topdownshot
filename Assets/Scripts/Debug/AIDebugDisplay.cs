using System.Collections.Generic;
using UnityEngine;
using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.DebugTools
{
    /// <summary>
    /// แสดง "ความคิด" ของ AI แบบเรียลไทม์ตอนกด Play เป็นการ์ดลอยเหนือหัวศัตรู
    /// บรรทัดบน: บทบาท + สถานะ (สีตามสถานะ) / ถัดมา: เหตุผลที่ตัดสินใจ / การมองเห็น / แถบเลือด
    /// ขนาดการ์ดปรับตามข้อความอัตโนมัติ และเลื่อนหนีกันเองเมื่อศัตรูยืนใกล้กัน
    /// วิธีใช้: แปะสคริปต์นี้ไว้ที่ตัวศัตรู (ต้องมี EnemyBrain อยู่ในตัวเดียวกัน) แล้วกด Play ดูใน Game View
    /// วาดผ่าน OnGUI ตรงๆ ไม่ต้องใช้ Canvas หรือ TextMeshPro
    /// </summary>
    [RequireComponent(typeof(EnemyBrain))]
    public class AIDebugDisplay : MonoBehaviour
    {
        [Header("Toggle")]
        [Tooltip("เปิด/ปิดการแสดงผล HUD นี้")]
        public bool ShowDebugHUD = true;

        [Tooltip("แสดงแค่บทบาท + สถานะ บรรทัดเดียว")]
        public bool CompactMode = false;

        [Tooltip("แสดงคะแนนการตัดสินใจเพิ่ม (ภัยคุกคาม / Action Score / ความมั่นใจ) — เปิดตอนอธิบายกรรมการ ปิดตอนแคปรูป")]
        public bool ShowScoringMetrics = false;

        [Header("Style")]
        public Vector2 ScreenOffset = new Vector2(0f, -40f);
        [Tooltip("ขนาดตัวอักษรหลัก (แนะนำ 16–18 ที่ Game view 1920x1080)")]
        public int FontSize = 16;
        [Tooltip("ความกว้างการ์ด (px) เมื่อ FontSize = 16 จะขยายตาม FontSize อัตโนมัติ")]
        public float BoxWidth = 250f;
        [Range(0.3f, 1f)] public float BackgroundAlpha = 0.85f;
        [Tooltip("เลื่อนการ์ดขึ้นเมื่อทับกับการ์ดของศัตรูตัวอื่น")]
        public bool AvoidOverlap = true;

        private EnemyBrain _brain;
        private TopDownTacticalAI.Player.Health _health;

        // ใช้ร่วมกันทุกตัว: จดตำแหน่งการ์ดที่วาดไปแล้วในเฟรมนี้ เพื่อไม่ให้ทับกัน
        private static readonly List<Rect> _placedThisFrame = new List<Rect>();
        private static int _placedFrame = -1;
        private static Texture2D _white;

        private static readonly Dictionary<EnemyState, Color> StateColors = new Dictionary<EnemyState, Color>
        {
            { EnemyState.Patrol, new Color(0.65f, 0.7f, 0.75f) },
            { EnemyState.Suspicious, new Color(1f, 0.75f, 0.1f) },
            { EnemyState.Chase, new Color(1f, 0.6f, 0.1f) },
            { EnemyState.Search, new Color(1f, 0.9f, 0.25f) },
            { EnemyState.Combat, new Color(1f, 0.3f, 0.3f) },
            { EnemyState.Cover, new Color(0.35f, 0.65f, 1f) },
            { EnemyState.Dodge, new Color(1f, 0.3f, 0.85f) },
            { EnemyState.Retreat, new Color(1f, 0.55f, 0.2f) },
            { EnemyState.SeekHeal, new Color(0.45f, 1f, 0.55f) },
            { EnemyState.HealAlly, new Color(0.3f, 1f, 0.45f) },
            { EnemyState.ProtectHealer, new Color(1f, 0.85f, 0.35f) },
            { EnemyState.PeelAlly, new Color(0.95f, 0.6f, 0.15f) },
            { EnemyState.Flank, new Color(1f, 0.45f, 0.65f) },
            { EnemyState.FollowAlly, new Color(0.25f, 0.9f, 0.9f) },
        };

        private static readonly Dictionary<EnemyState, string> StateThaiNames = new Dictionary<EnemyState, string>
        {
            { EnemyState.Patrol, "ลาดตระเวน" },
            { EnemyState.Suspicious, "สงสัย" },
            { EnemyState.Chase, "ไล่ตาม" },
            { EnemyState.Search, "ค้นหา" },
            { EnemyState.Combat, "ปะทะ" },
            { EnemyState.Cover, "หลบหลังที่กำบัง" },
            { EnemyState.Dodge, "หลบกระสุน" },
            { EnemyState.Retreat, "ถอยหา Healer" },
            { EnemyState.SeekHeal, "รอฮีล" },
            { EnemyState.HealAlly, "ฮีลเพื่อน" },
            { EnemyState.ProtectHealer, "บัง Healer" },
            { EnemyState.PeelAlly, "สกัดผู้เล่น" },
            { EnemyState.Flank, "อ้อมโจมตี" },
            { EnemyState.FollowAlly, "เดินตามเพื่อน" },
        };

        private void Awake()
        {
            _brain = GetComponent<EnemyBrain>();
            _health = GetComponent<TopDownTacticalAI.Player.Health>();
        }

        private static Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = new Texture2D(1, 1);
                    _white.SetPixel(0, 0, Color.white);
                    _white.Apply();
                }
                return _white;
            }
        }

        private static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = old;
        }

        private string RoleLabel()
        {
            switch (_brain.Role)
            {
                case EnemyRole.Aggressive: return "TANK";
                case EnemyRole.Flanker: return "FLANKER";
                case EnemyRole.Support: return "SUPPORT";
                default: return gameObject.name.ToUpper();
            }
        }

        private void OnGUI()
        {
            if (!ShowDebugHUD || _brain == null) return;
            if (Event.current.type != EventType.Repaint) return; // วาดอย่างเดียว ไม่รับคลิก

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(transform.position);
            if (screenPos.z < 0) return;

            if (_placedFrame != Time.frameCount)
            {
                _placedThisFrame.Clear();
                _placedFrame = Time.frameCount;
            }

            AIDebugInfo info = _brain.GetDebugInfo();
            Color stateColor = StateColors.TryGetValue(info.State, out Color c) ? c : Color.white;
            string stateName = StateThaiNames.TryGetValue(info.State, out string n) ? n : info.State.ToString();

            float k = Mathf.Max(0.5f, FontSize / 16f);
            float width = CompactMode ? 170f * k : BoxWidth * k;
            float pad = 8f * k;
            float gap = 3f * k;
            float strip = 5f * k;
            float innerW = width - pad * 2f - strip;

            var headStyle = new GUIStyle(GUI.skin.label) { fontSize = FontSize, fontStyle = FontStyle.Bold, richText = true, wordWrap = false };
            headStyle.normal.textColor = Color.white;
            var bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(FontSize * 0.85f), wordWrap = true };
            bodyStyle.normal.textColor = new Color(0.92f, 0.94f, 0.97f);
            var smallStyle = new GUIStyle(bodyStyle) { fontSize = Mathf.RoundToInt(FontSize * 0.75f) };
            smallStyle.normal.textColor = new Color(0.7f, 0.76f, 0.82f);

            string stateHex = ColorUtility.ToHtmlStringRGB(stateColor);
            var header = new GUIContent($"<size={Mathf.RoundToInt(FontSize * 0.75f)}>{RoleLabel()}</size>  <color=#{stateHex}>{stateName}</color>");

            string reason = string.IsNullOrEmpty(info.Reason) ? "-" : info.Reason;
            var reasonContent = new GUIContent("คิด: " + reason);

            string sight = info.CanSeeTarget
                ? $"เห็นผู้เล่น · ระยะ {info.DistanceToTarget:F0}"
                : (info.HasMemory ? "มองไม่เห็น · จำตำแหน่งล่าสุดได้" : "ไม่พบผู้เล่น");
            var sightContent = new GUIContent(sight);

            GUIContent scoreContent = null;
            if (ShowScoringMetrics && !CompactMode)
            {
                string note = string.IsNullOrEmpty(info.CurrentActionReason) ? "" : $"\n{info.CurrentActionReason}";
                scoreContent = new GUIContent(
                    $"ภัยคุกคาม {info.SmoothedThreat:F2} · Action {info.CurrentActionScore:F2} · เพื่อนใกล้ {info.NearbyAllyCount}\n" +
                    $"มั่นใจ: เห็น {info.VisionConfidence:F2} · จำ {info.MemoryConfidence:F2} · ทำนาย {info.PredictionConfidence:F2}{note}");
            }

            // ── คำนวณความสูงตามข้อความจริง ไม่ให้ตัวหนังสือล้นกล่อง ──
            float hHead = headStyle.CalcHeight(header, innerW);
            float hReason = 0f, hSight = 0f, hBar = 0f, hScore = 0f;
            if (!CompactMode)
            {
                hReason = bodyStyle.CalcHeight(reasonContent, innerW);
                hSight = smallStyle.CalcHeight(sightContent, innerW);
                hBar = 7f * k;
                if (scoreContent != null) hScore = smallStyle.CalcHeight(scoreContent, innerW);
            }
            float height = pad + hHead
                         + (CompactMode ? 0f : gap + hReason + gap + hSight + gap + hBar)
                         + (hScore > 0f ? gap + hScore : 0f)
                         + pad;

            float x = screenPos.x + ScreenOffset.x - width * 0.5f;
            float y = Screen.height - screenPos.y + ScreenOffset.y - height;
            Rect rect = new Rect(x, y, width, height);

            // ── เลื่อนขึ้นถ้าทับการ์ดของตัวอื่น ──
            if (AvoidOverlap)
            {
                for (int tries = 0; tries < 8; tries++)
                {
                    bool moved = false;
                    foreach (Rect other in _placedThisFrame)
                    {
                        if (rect.Overlaps(other))
                        {
                            rect.y = other.yMin - rect.height - 4f * k;
                            moved = true;
                        }
                    }
                    if (!moved) break;
                }
            }

            // กันหลุดขอบจอ
            rect.x = Mathf.Clamp(rect.x, 4f, Mathf.Max(4f, Screen.width - rect.width - 4f));
            rect.y = Mathf.Clamp(rect.y, 4f, Mathf.Max(4f, Screen.height - rect.height - 4f));
            _placedThisFrame.Add(rect);

            // ── วาดการ์ด ──
            Fill(rect, new Color(0.05f, 0.07f, 0.11f, BackgroundAlpha));
            Fill(new Rect(rect.x, rect.y, strip, rect.height), stateColor);

            float cx = rect.x + strip + pad;
            float cy = rect.y + pad;
            GUI.Label(new Rect(cx, cy, innerW, hHead), header, headStyle);
            cy += hHead;

            if (!CompactMode)
            {
                cy += gap;
                GUI.Label(new Rect(cx, cy, innerW, hReason), reasonContent, bodyStyle);
                cy += hReason + gap;
                GUI.Label(new Rect(cx, cy, innerW, hSight), sightContent, smallStyle);
                cy += hSight + gap;

                float hp = _health != null ? _health.CurrentHP : info.CurrentHP;
                float maxHp = _health != null ? _health.MaxHP : info.MaxHP;
                float pct = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
                Rect bar = new Rect(cx, cy, innerW, hBar);
                Fill(bar, new Color(1f, 1f, 1f, 0.15f));
                Fill(new Rect(bar.x, bar.y, bar.width * pct, bar.height), Color.Lerp(new Color(1f, 0.3f, 0.3f), new Color(0.35f, 0.95f, 0.45f), pct));
                cy += hBar;

                if (scoreContent != null)
                {
                    cy += gap;
                    GUI.Label(new Rect(cx, cy, innerW, hScore), scoreContent, smallStyle);
                }
            }
        }

        // วาดเส้นไปยังตำแหน่งล่าสุดที่เห็นผู้เล่น (Scene View เท่านั้น)
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

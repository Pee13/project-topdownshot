using UnityEngine;

namespace TopDownTacticalAI.DebugTools
{
    /// <summary>
    /// สโลว์โมชัน: กดปุ่มค้างไว้ (ค่าเริ่มต้น T) เกมจะช้าลง ปล่อยแล้วกลับความเร็วปกติ
    /// ใช้ดูการตัดสินใจของ AI ทีละจังหวะ / แคปภาพลงเล่ม / สาธิตให้กรรมการดูตอนนำเสนอ
    /// ไม่ใช้ Space เพราะ Space เป็นปุ่ม Dash ของผู้เล่นอยู่แล้ว (เปลี่ยนได้ใน Inspector)
    ///
    /// วิธีใช้: แปะสคริปต์นี้ที่ object ใดก็ได้ในฉาก (เช่น HUDCanvas) แล้วกด Play
    /// - ไม่ยุ่งกับเกมตอนหยุด (Pause Menu ตั้ง timeScale = 0 อยู่)
    /// - ปรับ fixedDeltaTime ตาม เพื่อให้ฟิสิกส์ (กระสุน/การชน) ลื่นตอนช้า
    /// </summary>
    public class SlowMotionDebug : MonoBehaviour
    {
        [Tooltip("ปุ่มสำหรับสโลว์ — อย่าใช้ Space เพราะชนกับ Dash")]
        public KeyCode SlowKey = KeyCode.T;

        [Tooltip("เปิด = กดค้างเพื่อช้า / ปิด = กดหนึ่งครั้งสลับช้า-ปกติ")]
        public bool HoldToSlow = true;

        [Tooltip("ความเร็วตอนสโลว์ (0.25 = ช้าลง 4 เท่า)")]
        [Range(0.05f, 1f)] public float SlowScale = 0.25f;

        [Tooltip("ความไวในการเปลี่ยนความเร็ว (ยิ่งมากยิ่งเปลี่ยนทันที)")]
        public float BlendSpeed = 8f;

        [Tooltip("แสดงป้ายบอกว่ากำลังสโลว์อยู่ที่ด้านบนจอ")]
        public bool ShowLabel = true;

        private static float s_baseFixedDelta = -1f; // ค่าฟิสิกส์ตั้งต้นของโปรเจกต์ (เก็บครั้งเดียว กันค่าเพี้ยนตอนโหลดฉากใหม่)
        private bool _slowOn;
        private float _current = 1f;
        private bool _weChangedTime;
        private GUIStyle _labelStyle;

        private void Awake()
        {
            if (s_baseFixedDelta < 0f) s_baseFixedDelta = Time.fixedDeltaTime;
        }

        private void Update()
        {
            if (HoldToSlow) _slowOn = Input.GetKey(SlowKey);
            else if (Input.GetKeyDown(SlowKey)) _slowOn = !_slowOn;

            // เกมหยุดอยู่ (Pause Menu) → ไม่แตะ timeScale เด็ดขาด
            if (Time.timeScale == 0f) return;

            float target = _slowOn ? SlowScale : 1f;
            _current = Mathf.MoveTowards(_current, target, BlendSpeed * Time.unscaledDeltaTime);

            // เขียนค่าเฉพาะตอนเรากำลังสโลว์ หรือกำลังคืนค่าจากการสโลว์ — ไม่ไปทับระบบอื่นตอนปกติ
            if (_slowOn || _weChangedTime)
            {
                if (_current >= 0.999f)
                {
                    _current = 1f;
                    RestoreNormal();
                }
                else
                {
                    Time.timeScale = _current;
                    Time.fixedDeltaTime = s_baseFixedDelta * _current;
                    _weChangedTime = true;
                }
            }
        }

        private void RestoreNormal()
        {
            if (!_weChangedTime) return;
            if (Time.timeScale != 0f) Time.timeScale = 1f;
            Time.fixedDeltaTime = s_baseFixedDelta;
            _weChangedTime = false;
        }

        // ออกจากฉาก / ปิดสคริปต์ระหว่างสโลว์ → คืนความเร็วปกติเสมอ
        private void OnDisable() => RestoreNormal();

        private void OnGUI()
        {
            if (!ShowLabel || !_weChangedTime || Event.current.type != EventType.Repaint) return;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = Mathf.Max(12, Screen.height / 45),
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                _labelStyle.normal.textColor = new Color(0.55f, 0.85f, 1f);
            }

            string text = $"สโลว์ ×{_current:0.00}";
            Vector2 size = _labelStyle.CalcSize(new GUIContent(text));
            size.x += 24f; size.y += 8f;
            GUI.Box(new Rect((Screen.width - size.x) * 0.5f, 12f, size.x, size.y), text, _labelStyle);
        }
    }
}

using UnityEngine;
using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.Alert
{
    /// <summary>
    /// แสดงไอคอนบอกสถานะเตือนภัยเหนือหัวศัตรู ให้ "ผู้เล่นเห็นตลอดเวลาเล่นจริง" (ไม่ใช่เครื่องมือ Debug)
    /// "?" (สีเหลือง) = กำลังสงสัย (Suspicious) — เห็นเงาๆ อยู่ กำลังสะสมความมั่นใจ
    /// "!" (สีแดง) = เพิ่งเข้าโหมดต่อสู้จากโหมดสงบ (ลาดตระเวน/ค้นหา/สงสัย/เดินตามเพื่อน → ปะทะ/ไล่ตาม/กำบัง/อ้อม)
    ///
    /// แก้จากเวอร์ชันเดิม:
    /// 1) TextMesh ที่สร้างด้วยโค้ดไม่มีฟอนต์ → ไม่วาดอะไรเลย  ⇒ ใส่ฟอนต์ในตัวของ Unity ให้
    /// 2) ขนาดตายตัวเล็กมากเมื่อแมพขยาย 12 เท่า ⇒ ปรับขนาดอัตโนมัติให้สูงตาม TargetHeight (หน่วยโลก)
    /// 3) อาจโดนพื้น/ฉากบัง ⇒ วาดใน Sorting Layer เดียวกับตัวศัตรู แต่ลำดับสูงกว่า
    /// 4) ขึ้นเหนือหัวพอดีตามขนาดตัว (AutoOffset) + มีเงาดำให้อ่านง่ายบนพื้นเทา
    /// </summary>
    [RequireComponent(typeof(EnemyBrain))]
    public class AlertIconDisplay : MonoBehaviour
    {
        [Header("ตำแหน่ง/ขนาด")]
        [Tooltip("วางไอคอนเหนือขอบบนของตัวศัตรูอัตโนมัติ (ถ้าปิด จะใช้ Offset ตรงๆ)")]
        public bool AutoOffset = true;
        public Vector3 Offset = new Vector3(0f, 14f, 0f);
        [Tooltip("ความสูงของตัวอักษรในโลกเกม (แมพนี้ใหญ่ ~12 เท่า แนะนำ 15–30)")]
        public float TargetHeight = 22f;
        public int FontSize = 64;
        [Tooltip("ใช้เมื่อปิดการปรับขนาดอัตโนมัติ")]
        public float WorldScale = 0.4f;
        public bool AutoSize = true;

        [Header("ระยะเวลาที่ไอคอน ! ค้างแสดงหลังยืนยันเจอ")]
        public float ConfirmedIconDuration = 1.2f;

        private EnemyBrain _brain;
        private SpriteRenderer _bodySprite;
        private TextMesh _text, _shadow;
        private Transform _iconTransform;
        private EnemyState _lastState;
        private float _confirmedIconTimer;
        private bool _sized;

        private static Font s_font;

        private static Font GetBuiltinFont()
        {
            if (s_font != null) return s_font;
            // Unity 2022.2+ ใช้ชื่อ LegacyRuntime.ttf ส่วนเวอร์ชันเก่าใช้ Arial.ttf
            try { s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { s_font = null; }
            if (s_font == null)
            {
                try { s_font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { s_font = null; }
            }
            if (s_font == null) Debug.LogWarning("[AlertIcon] หาฟอนต์ในตัวของ Unity ไม่เจอ — ไอคอน ? ! จะไม่แสดง");
            return s_font;
        }

        private void Awake()
        {
            _brain = GetComponent<EnemyBrain>();
            _bodySprite = GetComponent<SpriteRenderer>();
            if (_bodySprite == null) _bodySprite = GetComponentInChildren<SpriteRenderer>();

            GameObject iconObj = new GameObject("AlertIcon");
            iconObj.transform.SetParent(transform, false);
            iconObj.transform.localScale = Vector3.one * WorldScale;
            _iconTransform = iconObj.transform;

            // เงาดำขยับลงขวาเล็กน้อย วาดก่อนตัวจริง
            GameObject shadowObj = new GameObject("Shadow");
            shadowObj.transform.SetParent(_iconTransform, false);
            shadowObj.transform.localPosition = new Vector3(0.6f, -0.6f, 0.01f);
            _shadow = CreateText(shadowObj, 99);
            _shadow.color = new Color(0f, 0f, 0f, 0.75f);

            _text = CreateText(iconObj, 100);
            UpdatePosition();
        }

        private TextMesh CreateText(GameObject go, int order)
        {
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.fontSize = FontSize;
            tm.characterSize = 0.1f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.text = "";

            Font f = GetBuiltinFont();
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (f != null)
            {
                tm.font = f;
                mr.sharedMaterial = f.material;
            }
            // วาดใน Sorting Layer เดียวกับตัวศัตรู แต่ลำดับสูงกว่า → ไม่โดนพื้น/ตัวละครบัง
            if (_bodySprite != null) mr.sortingLayerID = _bodySprite.sortingLayerID;
            mr.sortingOrder = (_bodySprite != null ? _bodySprite.sortingOrder : 0) + order;
            return tm;
        }

        private void UpdatePosition()
        {
            Vector3 offset = Offset;
            if (AutoOffset && _bodySprite != null)
            {
                // ขอบบนของตัวศัตรู (คิดเป็นหน่วยโลก แล้วแปลงเป็นพิกัดภายในตัว)
                float topWorld = _bodySprite.bounds.max.y + TargetHeight * 0.15f;
                Vector3 worldPos = new Vector3(transform.position.x, topWorld, transform.position.z);
                _iconTransform.position = worldPos;
                return;
            }
            _iconTransform.localPosition = offset;
        }

        private void FitSize()
        {
            if (!AutoSize || _sized || _text == null) return;
            var mr = _text.GetComponent<MeshRenderer>();
            float h = mr.bounds.size.y;
            if (h <= 0.0001f) return; // ข้อความยังไม่ถูกสร้าง mesh รอเฟรมถัดไป
            float k = TargetHeight / h;
            _iconTransform.localScale *= k;
            _sized = true;
        }

        private static bool IsCalm(EnemyState s) =>
            s == EnemyState.Patrol || s == EnemyState.Search || s == EnemyState.Suspicious || s == EnemyState.FollowAlly;

        private static bool IsAlert(EnemyState s) =>
            s == EnemyState.Combat || s == EnemyState.Chase || s == EnemyState.Cover || s == EnemyState.Flank;

        private void LateUpdate()
        {
            if (_brain == null || _text == null) return;

            AIDebugInfo info = _brain.GetDebugInfo();

            // เพิ่งเปลี่ยนจากโหมดสงบ → โหมดต่อสู้ : โชว์ "!" ค้างไว้สักพัก
            if (IsCalm(_lastState) && IsAlert(info.State))
                _confirmedIconTimer = ConfirmedIconDuration;

            string s;
            Color c;
            if (_confirmedIconTimer > 0f)
            {
                _confirmedIconTimer -= Time.deltaTime;
                s = "!"; c = new Color(1f, 0.2f, 0.2f);
            }
            else if (info.State == EnemyState.Suspicious)
            {
                s = "?"; c = new Color(1f, 0.85f, 0.1f);
            }
            else
            {
                s = ""; c = Color.white;
            }

            _text.text = s; _text.color = c;
            _shadow.text = s;
            _lastState = info.State;

            if (s.Length > 0) FitSize();
            UpdatePosition();

            // หันตามกล้องเสมอ (ตัวศัตรูหมุนตามทิศเดิน ไอคอนต้องตั้งตรง)
            if (Camera.main != null)
                _iconTransform.rotation = Camera.main.transform.rotation;
        }
    }
}
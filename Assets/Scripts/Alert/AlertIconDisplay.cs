using UnityEngine;
using TopDownTacticalAI.Core;

namespace TopDownTacticalAI.Alert
{
    /// <summary>
    /// แสดงไอคอนบอกสถานะเตือนภัยเหนือหัวศัตรู ให้ "ผู้เล่นเห็นตลอดเวลาเล่นจริง" (ไม่ใช่เครื่องมือ Debug)
    /// "?" (สีเหลือง) = กำลังสงสัย (Suspicious) — เห็นเงาๆ อยู่ กำลังสะสมความมั่นใจ
    /// "!" (สีแดง) = เพิ่งยืนยันเจอผู้เล่นจริง (แสดงแวบเดียวตอนเปลี่ยนจาก Suspicious → Combat/Chase/Cover)
    /// ไม่แสดงอะไรเลยตอน Patrol/Search ปกติ (ยังไม่มีอะไรน่าตกใจ)
    ///
    /// ใช้ TextMesh ธรรมดาแทน Sprite เพื่อไม่ต้องพึ่ง Asset ภายนอกใดๆ ทำงานได้ทันทีไม่ต้องตั้งค่าเพิ่ม
    /// วิธีใช้: แปะสคริปต์นี้ที่ตัวศัตรู (ต้องมี EnemyBrain อยู่ด้วย) แล้วกด Play
    /// </summary>
    [RequireComponent(typeof(EnemyBrain))]
    public class AlertIconDisplay : MonoBehaviour
    {
        [Header("ตำแหน่ง/ขนาด")]
        public Vector3 Offset = new Vector3(0f, 1f, 0f);
        public int FontSize = 32;
        [Tooltip("ขนาดจริงในโลกเกมของตัวอักษร (ตัวคูณจาก FontSize)")]
        public float WorldScale = 0.03f;

        [Header("ระยะเวลาที่ไอคอน ! ค้างแสดงหลังยืนยันเจอ")]
        public float ConfirmedIconDuration = 1f;

        private EnemyBrain _brain;
        private TextMesh _textMesh;
        private Transform _iconTransform;
        private EnemyState _lastState;
        private float _confirmedIconTimer;

        private void Awake()
        {
            _brain = GetComponent<EnemyBrain>();

            GameObject iconObj = new GameObject("AlertIcon");
            iconObj.transform.SetParent(transform, false);
            iconObj.transform.localPosition = Offset;
            iconObj.transform.localScale = Vector3.one * WorldScale;
            _iconTransform = iconObj.transform;

            _textMesh = iconObj.AddComponent<TextMesh>();
            _textMesh.fontSize = FontSize;
            _textMesh.anchor = TextAnchor.LowerCenter;
            _textMesh.alignment = TextAlignment.Center;
            _textMesh.text = "";

            MeshRenderer renderer = iconObj.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sortingOrder = 20;
        }

        private void LateUpdate()
        {
            if (_brain == null) return;

            AIDebugInfo info = _brain.GetDebugInfo();

            // เพิ่งเปลี่ยนจาก Suspicious ไปเป็น State ที่ยืนยันแล้ว (Combat/Chase/Cover) -> โชว์ "!" ค้างไว้สักพัก
            bool justConfirmed = _lastState == EnemyState.Suspicious &&
                (info.State == EnemyState.Combat || info.State == EnemyState.Chase || info.State == EnemyState.Cover);

            if (justConfirmed)
                _confirmedIconTimer = ConfirmedIconDuration;

            if (_confirmedIconTimer > 0f)
            {
                _confirmedIconTimer -= Time.deltaTime;
                _textMesh.text = "!";
                _textMesh.color = new Color(1f, 0.2f, 0.2f);
            }
            else if (info.State == EnemyState.Suspicious)
            {
                _textMesh.text = "?";
                _textMesh.color = new Color(1f, 0.85f, 0.1f);
            }
            else
            {
                _textMesh.text = "";
            }

            _lastState = info.State;

            // ให้ไอคอนหันหน้าเข้าหากล้องเสมอ (Billboard) กันข้อความบิดเบี้ยวตอนกล้องหมุน/เอียง
            if (Camera.main != null)
                _iconTransform.rotation = Camera.main.transform.rotation;
        }
    }
}

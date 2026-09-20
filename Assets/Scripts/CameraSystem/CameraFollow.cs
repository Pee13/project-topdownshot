using UnityEngine;

namespace TopDownTacticalAI.CameraSystem
{
    /// <summary>
    /// กล้องตามผู้เล่นแบบนุ่มนวล (Smooth Follow) สำหรับเกม Top-down 2D
    ///
    /// วิธีใช้: แปะสคริปต์นี้ไว้ที่ "Main Camera" (ต้องเป็น GameObject ที่มี Camera component จริง)
    /// แล้วลาก Transform ผู้เล่นมาใส่ช่อง Target — ถ้าเว้นว่างไว้ ระบบจะหาจาก Tag "Player" ให้เอง
    ///
    /// ข้อควรระวัง: ถ้าแปะไว้ที่ GameObject อื่นที่ไม่ใช่กล้อง สคริปต์จะขยับตัวมันเอง
    /// แต่ภาพจากกล้องจะไม่เปลี่ยน — สคริปต์จะเตือนให้ทราบเมื่อตรวจไม่พบ Camera
    ///
    /// หมายเหตุ: จงใจไม่ใช้ [RequireComponent(typeof(Camera))] เพราะ Unity จะ "เพิ่ม
    /// Camera component ให้เอง" กับทุกวัตถุที่แปะสคริปต์นี้ ซึ่งไปเพิ่มกล้องซ้อนในฉากของผู้ใช้
    /// โดยไม่ตั้งใจ จึงเลือกเตือนตอนรันแทนการแก้ฉากให้
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform ที่ต้องการให้กล้องตาม (ปกติคือผู้เล่น) — เว้นว่างไว้ให้หาจาก Tag 'Player' อัตโนมัติ")]
        public Transform Target;

        [Tooltip("ค้นหาผู้เล่นจาก Tag อัตโนมัติถ้าไม่ได้ระบุ Target")]
        public bool AutoFindPlayer = true;

        [Header("Follow Settings")]
        [Tooltip("ยิ่งน้อยยิ่งตามติดเร็ว ยิ่งมากยิ่งตามนุ่มนวล/หน่วงกว่า")]
        public float SmoothTime = 0.15f;

        [Tooltip("ระยะออฟเซ็ตจากตัวผู้เล่น (ปกติ Z ควรเป็นค่าลบเพื่อให้กล้องอยู่หลังฉาก)")]
        public Vector3 Offset = new Vector3(0f, 0f, -10f);

        [Tooltip("ตอนเริ่มฉาก ให้กระโดดไปที่ตัวผู้เล่นทันทีไม่ต้องค่อยๆ เลื่อนตาม\n" +
                 "ปิดไว้จะเกิดอาการกล้องเริ่มที่จุดเดิมในฉากแล้วค่อยๆ ลากไปหาผู้เล่น")]
        public bool SnapOnStart = true;

        [Tooltip("ถ้าผู้เล่นย้ายตำแหน่งเกินค่านี้ภายในเฟรมเดียว (เกิดใหม่/วาร์ป) ให้กล้องกระโดดตามทันที\n" +
                 "แทนที่จะค่อยๆ ไถกล้องผ่านทั้งแมพ")]
        public float TeleportSnapDistance = 20f;

        [Header("Look Ahead (มองล่วงหน้าตามทิศที่เดิน)")]
        [Tooltip("เลื่อนกล้องไปข้างหน้าผู้เล่นตามทิศที่กำลังเดิน เพื่อให้เห็นสิ่งที่อยู่ข้างหน้าได้ไกลขึ้น (0 = ปิด)")]
        public float LookAheadDistance = 0f;

        [Tooltip("ความเร็วในการปรับระยะมองล่วงหน้าให้ราบรื่น (ยิ่งสูงยิ่งไว)")]
        public float LookAheadSmoothing = 2f;

        [Header("Boundary (ทางเลือก)")]
        [Tooltip("ถ้าต้องการจำกัดขอบเขตกล้องไม่ให้ออกนอกฉาก ให้ติ๊กเปิดแล้วตั้งค่าขอบเขตด้านล่าง")]
        public bool UseBounds = false;
        public Vector2 MinBounds;
        public Vector2 MaxBounds;

        private Vector3 _velocity = Vector3.zero;
        private Vector3 _currentLookAhead = Vector3.zero;
        private Vector3 _lastTargetPosition;
        private bool _hasSnapped;

        private void Awake()
        {
            WarnIfNotOnACamera();
            ResolveTargetIfNeeded();
        }

        /// <summary>
        /// เตือนถ้าสคริปต์ถูกแปะไว้บนวัตถุที่ไม่มีกล้อง
        /// กรณีนี้สคริปต์จะขยับตัวมันเองอยู่เฉยๆ แต่ภาพจากกล้องไม่เปลี่ยน —
        /// เป็นสาเหตุที่พบบ่อยที่สุดของอาการ "กล้องไม่ตามผู้เล่น"
        /// </summary>
        private void WarnIfNotOnACamera()
        {
            if (GetComponent<Camera>() != null) return;

            Debug.LogWarning(
                $"[CameraFollow] '{name}' ไม่มี Camera component — สคริปต์นี้กำลังขยับวัตถุนี้อยู่ " +
                "แต่ภาพจากกล้องจะไม่เปลี่ยน กรุณาย้ายสคริปต์นี้ไปไว้ที่ Main Camera", this);
        }

        private void LateUpdate()
        {
            // ถ้าผู้เล่นเกิดใหม่/ถูกสลับกลางเกม ให้ตามหาตัวใหม่เอง
            ResolveTargetIfNeeded();
            if (Target == null) return;

            UpdateLookAhead(Time.unscaledDeltaTime);

            Vector3 desiredPosition = Target.position + Offset + _currentLookAhead;

            if (UseBounds && HasValidBounds())
            {
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, MinBounds.x, MaxBounds.x);
                desiredPosition.y = Mathf.Clamp(desiredPosition.y, MinBounds.y, MaxBounds.y);
            }

            // เฟรมแรก: ตัดสินใจว่าจะกระโดดไปหาผู้เล่นเลย หรือค่อยๆ เลื่อนตามที่ตั้งไว้
            // การกระโดดเป็นค่าเริ่มต้น เพราะถ้าปล่อยให้เลื่อน กล้องจะเริ่มที่จุดที่ตั้งไว้ในฉาก
            // แล้วลากผ่านทั้งฉากมาหาผู้เล่น ซึ่งดูเหมือนกล้อง "ย้ายไปอยู่จุดที่ไม่ควรอยู่" แล้วค่อยกลับมา
            if (!_hasSnapped)
            {
                _hasSnapped = true;
                _lastTargetPosition = Target.position;

                if (SnapOnStart)
                {
                    transform.position = desiredPosition;
                    _velocity = Vector3.zero;
                    return;
                }
            }
            else if (Vector3.Distance(Target.position, _lastTargetPosition) > TeleportSnapDistance)
            {
                // ผู้เล่นย้ายระยะไกลในเฟรมเดียว (เกิดใหม่/วาร์ป/ย้ายจุดเกิด)
                // ถ้าปล่อยให้นุ่มนวล กล้องจะไถผ่านทั้งแมพให้ดู ซึ่งไม่ใช่สิ่งที่ต้องการ
                transform.position = desiredPosition;
                _velocity = Vector3.zero;
                _lastTargetPosition = Target.position;
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, SmoothTime);
            _lastTargetPosition = Target.position;
        }

        /// <summary>สั่งให้กล้องกระโดดไปหาผู้เล่นทันที (เรียกได้จากสคริปต์เกิดใหม่/ย้ายฉาก)</summary>
        public void SnapToTarget()
        {
            _hasSnapped = false;
        }

        /// <summary>
        /// คำนวณระยะมองล่วงหน้าจากทิศที่ผู้เล่นกำลังเดิน
        /// ช่วยให้เห็นสิ่งที่อยู่ข้างหน้าได้ไกลขึ้น แทนที่จะเห็นแค่รอบตัวเท่าๆ กันทุกทิศ
        /// </summary>
        private void UpdateLookAhead(float deltaTime)
        {
            Vector3 desiredLookAhead = Vector3.zero;

            if (LookAheadDistance > 0.001f && Target != null)
            {
                Vector3 movement = Target.position - _lastTargetPosition;
                movement.z = 0f;

                // ใช้ทิศที่เดินจริงเฉพาะเมื่อมีการขยับพอสมควร กันกล้องสั่นตอนผู้เล่นยืนนิ่ง
                if (movement.sqrMagnitude > 0.0001f)
                    desiredLookAhead = movement.normalized * LookAheadDistance;
            }

            _currentLookAhead = Vector3.Lerp(
                _currentLookAhead,
                desiredLookAhead,
                Mathf.Clamp01(deltaTime * Mathf.Max(0.01f, LookAheadSmoothing)));
        }

        /// <summary>
        /// ขอบเขตถือว่าใช้ได้เฉพาะเมื่อตั้งค่าจริงแล้ว (max ต้องมากกว่า min)
        /// ถ้าเปิด UseBounds ไว้แต่ค่าเป็น 0 ทั้งคู่ กล้องจะถูกบีบไปที่จุดกำเนิดและดูเหมือนไม่ตามผู้เล่น
        /// กรณีแบบนี้ให้ข้ามการ clamp ไปเลย ดีกว่าปล่อยให้กล้องค้างอยู่ที่เดิม
        /// </summary>
        private bool HasValidBounds()
        {
            return MaxBounds.x > MinBounds.x && MaxBounds.y > MinBounds.y;
        }

        private void ResolveTargetIfNeeded()
        {
            if (Target != null || !AutoFindPlayer) return;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            // ใช้ transform.root ไม่ใช่ตัวที่ติด Tag ตรงๆ
            // เพราะในฉากนี้ Tag "Player" ถูกติดไว้ที่ลูกอย่าง "FirePoint" ด้วย
            // ถ้าตามตัวลูก กล้องจะเพี้ยนไปตามตำแหน่งปลายกระบอกปืนแทนตัวผู้เล่น
            Target = player.transform.root;
        }

        private void OnValidate()
        {
            if (UseBounds && !HasValidBounds())
            {
                Debug.LogWarning(
                    "[CameraFollow] เปิด UseBounds ไว้แต่ Min/Max ยังเป็น 0 ทั้งคู่ — " +
                    "กล้องจะถูกบีบไปที่จุดกำเนิด กรุณาตั้งขอบเขตจริง หรือปิด UseBounds", this);
            }
        }
    }
}

using UnityEngine;
using UnityEngine.AI;

namespace TopDownTacticalAI.Chase
{
    /// <summary>
    /// ทำนายตำแหน่งในอนาคตของเป้าหมายจากความเร็วปัจจุบัน
    /// เพื่อให้การไล่ตามตัดหน้าได้แม่นยำขึ้นแทนการวิ่งตามตำแหน่งปัจจุบันตรงๆ
    /// </summary>
    public static class TargetPrediction
    {
        // buffer กลางสำหรับ path-aware prediction (ไม่ allocate NavMeshPath ทุกครั้ง — single-threaded)
        private static readonly NavMeshPath s_path = new NavMeshPath();

        public static Vector2 Predict(Vector2 targetPosition, Vector2 targetVelocity, float leadTime)
        {
            return targetPosition + targetVelocity * leadTime;
        }

        /// <summary>
        /// ทำนายตำแหน่งเป้าหมายพร้อม "ความมั่นใจ" ในการทำนาย
        ///
        /// ต่างจาก Predict() ตรงที่:
        ///   1) เวลานำ (lead time) คำนวณจากระยะทางจริง หารด้วยความเร็วที่คาดว่า AI จะไปถึง
        ///      ไม่ใช่ค่าคงที่ — เป้าหมายอยู่ไกลก็ต้องนำนานกว่าเพื่อให้ทันตัดหน้า
        ///   2) ถูก clamp ด้วย min/max กันไม่ให้เป้าหมายที่วิ่งเร็วมากทำให้ AI ทำนายไกลเกินจริง
        ///   3) คืนค่า confidence บอกว่าควรเชื่อการทำนายนี้แค่ไหน
        ///
        /// confidence ต่ำลงเมื่อเป้าหมายเปลี่ยนทิศทางบ่อย (คาดเดายาก)
        /// ผู้เรียกควรคูณน้ำหนักการทำนายด้วยค่านี้ ไม่ควรเชื่อ 100%
        /// </summary>
        /// <param name="targetPosition">ตำแหน่งปัจจุบันของเป้าหมาย</param>
        /// <param name="targetVelocity">ความเร็วปัจจุบันของเป้าหมาย</param>
        /// <param name="distance">ระยะห่างจาก AI ถึงเป้าหมาย</param>
        /// <param name="estimatedTravelSpeed">ความเร็วที่ใช้ประมาณเวลาที่ AI จะไปถึง</param>
        /// <param name="minLeadTime">เวลานำขั้นต่ำ (วินาที)</param>
        /// <param name="maxLeadTime">เวลานำสูงสุด (วินาที)</param>
        /// <param name="previousDirection">ทิศทางการเคลื่อนที่ที่เคยเห็นครั้งก่อน (ใช้เทียบความนิ่ง)</param>
        /// <param name="confidence">ผลลัพธ์: ความมั่นใจในการทำนาย 0..1</param>
        public static Vector2 PredictWithConfidence(
            Vector2 targetPosition,
            Vector2 targetVelocity,
            float distance,
            float estimatedTravelSpeed,
            float minLeadTime,
            float maxLeadTime,
            Vector2 previousDirection,
            out float confidence)
        {
            float speed = Mathf.Max(0.1f, estimatedTravelSpeed);

            // เวลานำตามระยะทางจริง แล้ว clamp ให้อยู่ในกรอบที่สมเหตุสมผล
            float leadTime = Mathf.Clamp(distance / speed, minLeadTime, maxLeadTime);

            Vector2 predicted = targetPosition + targetVelocity * leadTime;

            // ── ความมั่นใจในการทำนาย ──
            // เป้าหมายที่วิ่งเป็นเส้นตรง = ทำนายง่าย (มั่นใจสูง)
            // เป้าหมายที่เปลี่ยนทิศไปมา = ทำนายยาก (มั่นใจต่ำ)
            float confidenceNow = ComputeDirectionStability(targetVelocity, previousDirection);

            // เป้าหมายที่แทบไม่ขยับ ก็ไม่ต้องเชื่อการทำนายมากนัก (ทำนายได้แต่มันไม่มีนัยสำคัญ)
            float speedFactor = Mathf.Clamp01(targetVelocity.magnitude / Mathf.Max(0.1f, speed));
            confidenceNow *= Mathf.Lerp(0.5f, 1f, speedFactor);

            confidence = Mathf.Clamp01(confidenceNow);
            return predicted;
        }

        /// <summary>
        /// วัดความนิ่งของทิศทาง 0..1
        /// ทิศทางตรงกับครั้งก่อน = 1 (คาดเดาได้), สวนทางกัน = 0 (คาดเดาไม่ได้)
        /// </summary>
        public static float ComputeDirectionStability(Vector2 currentVelocity, Vector2 previousDirection)
        {
            if (currentVelocity.sqrMagnitude < 0.0001f) return 0f;
            if (previousDirection.sqrMagnitude < 0.0001f) return 0.5f; // ยังไม่เคยเห็นทิศทาง = กลางๆ

            Vector2 currentDir = currentVelocity.normalized;
            Vector2 previousDir = previousDirection.normalized;

            // Dot เป็น 1 = ทิศเดิมเป๊ะ, 0 = ตั้งฉาก, -1 = สวนทาง
            float dot = Vector2.Dot(currentDir, previousDir);

            // แปลงจาก -1..1 ให้เป็น 0..1
            return Mathf.Clamp01((dot + 1f) * 0.5f);
        }

        /// <summary>
        /// ทำนายแบบ "รู้เส้นทาง" (Path-aware) — ใช้ NavMesh ช่วยเมื่อจุดทำนายเส้นตรง "ชนกำแพง"
        ///
        /// หลักการ: ผู้เล่นวิ่งได้เฉพาะทางเดินจริง (อ้อมกำาว) ไม่ใช่ทะลุ —
        /// ถ้า linear prediction ชี้ไปหลังกำแพง การไล่ตามจะวิ่งชนกำแพงตามเป้าปลอม
        /// วิธีแก้: คำนวณเส้นทาง NavMesh จากตำแหน่งปัจจุบันไปจุดทำนาย แล้วเลื่อนไปตามเส้นทาง
        /// ระยะทางเท่ากับระยะที่จะวิ่ง (distance ของ linear prediction) — ได้จุดที่ผู้เล่น "น่าจะอยู่จริง"
        ///
        /// คืน false เมื่อ: ไม่มี NavMesh ในฉาก / path ไม่สมบูรณ์ / เส้นทางตรงโล่งอยู่แล้ว
        /// (ผู้เรียกใช้ผล linear prediction เดิมต่อ — fallback ปลอดภัย)
        /// </summary>
        /// <param name="from">ตำแหน่งปัจจุบันของเป้าหมาย (จุดเริ่มเส้นทาง)</param>
        /// <param name="linearPrediction">ผลทำนายแบบเส้นตรง (targetPos + velocity * lead)</param>
        /// <param name="obstacleMask">Layer สิ่งกีดขวาง ใช้เช็คว่าเส้นตรงชนกำแพงไหม</param>
        /// <param name="result">ผลลัพธ์: ตำแหน่งที่ทำนายบนเส้นทางจริง (คืนค่าเดิมถ้าทางตรงโล่ง)</param>
        public static bool PredictAlongPath(Vector2 from, Vector2 linearPrediction, LayerMask obstacleMask, out Vector2 result)
        {
            result = linearPrediction;

            // ทางตรงโล่งอยู่แล้ว → ไม่ต้องพึ่ง NavMesh (เส้นทางตรง = เร็วที่สุด)
            Vector2 direct = linearPrediction - from;
            float directDist = direct.magnitude;
            if (directDist < 0.01f) return false;
            if (!Physics2D.Raycast(from, direct / directDist, directDist, obstacleMask))
                return true; // ไม่มีกำแพงขวาง → ใช้ผลเส้นตรง

            // มีกำแพงขวาง → หาเส้นทาง NavMesh ที่อ้อมได้จริง
            // หมายเหตุ: s_path.corners คืน array ใหม่ (API ของ Unity) — แต่จะเข้าสู่บล็อกนี้เฉพาะตอน
            // ที่เส้นทางตรงชนกำแพงเท่านั้น (มี raycast gate ข้างบน) จึงไม่ใช่ทุกเฟรมปกติ
            if (!NavMesh.CalculatePath(from, linearPrediction, NavMesh.AllAreas, s_path))
                return false;
            if (s_path.status == NavMeshPathStatus.PathInvalid)
                return false;

            Vector3[] corners = s_path.corners;
            if (corners.Length < 2) return false;

            // เดินตามเส้นทางไป "ระยะทางเท่ากับ" ระยะที่ linear prediction กะไว้
            // (ถ้าเส้นทางสั้นกว่า = ถึงปลายทางก่อน ใช้ปลายทาง)
            float remaining = directDist;
            Vector2 prev = from;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 corner = corners[i];
                float segLen = Vector2.Distance(prev, corner);

                if (segLen >= remaining && segLen > 0.0001f)
                {
                    result = prev + (corner - prev) * (remaining / segLen);
                    return true;
                }

                remaining -= segLen;
                prev = corner;
            }

            // เส้นทางสั้นกว่าระยะทำนาย → ค้างที่ปลายเส้นทาง (ดีกว่าทะลุกำแพง)
            result = prev;
            return true;
        }
    }
}

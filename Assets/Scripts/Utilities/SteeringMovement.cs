using UnityEngine;
using TopDownTacticalAI.Tactical;

namespace TopDownTacticalAI.Utilities
{
    /// <summary>
    /// ระบบเลี่ยงสิ่งกีดขวาง + กันชนพันธมิตร รวม 3 ชั้นทำงานร่วมกัน:
    /// 1) "แรงผลัก" ต่อเนื่องจากกำแพงที่อยู่ใกล้ (Wall Avoidance Push)
    /// 2) "แรงผลัก" ต่อเนื่องจากพันธมิตรตัวอื่นที่อยู่ใกล้เกินไป (Separation Push)
    ///    — ใช้ GroupAI กลาง ทำให้ทุก State (Patrol/Chase/Search/ฯลฯ) กันชนกันอัตโนมัติเหมือนกันหมด
    ///    ไม่ต้องเขียนซ้ำแยกในแต่ละ State เอง (ของเดิมทำแยกใน ChaseMovement/CombatState เท่านั้น
    ///    ทำให้ตอน Patrol/Search ที่ไม่มีระบบนี้ ศัตรูเดินไปสืบจุดเดียวกันแล้วซ้อนทับกันได้)
    /// 3) Whisker Raycast (สำรอง) — ถ้าทิศทางที่ผสมแรงผลักทั้งหมดแล้วยังโดนบังตรงๆ ให้หามุมข้างเคียงที่โล่งแทน
    /// </summary>
    public static class SteeringMovement
    {
        private static readonly float[] WhiskerAngles = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 120f, -120f };
        private static readonly float[] ProbeAngles = { 0f, 45f, 90f, 135f, 180f, -45f, -90f, -135f };

        /// <summary>
        /// คืนทิศทางที่ควรเดินจริง โดยหลบกำแพง + เว้นระยะจากพันธมิตร แล้วค่อยตรวจสอบทางตันซ้ำอีกที
        /// </summary>
        /// <param name="self">Transform ของตัวเอง (ใส่ไว้เพื่อกันชนกับพันธมิตรตัวอื่นผ่าน GroupAI ถ้าไม่ใส่จะข้ามการกันชนพันธมิตร)</param>
        /// <param name="personalSpace">ระยะห่างขั้นต่ำที่อยากมีจากพันธมิตรตัวอื่น</param>
        /// <param name="probeDistance">ระยะที่ Whisker Raycast เช็คว่าทางข้างหน้าตันไหม</param>
        /// <param name="wallMargin">ระยะห่างขั้นต่ำที่อยากให้มีจากกำแพง (ยิ่งเยอะยิ่งไม่ชิดกำแพง)</param>
        public static Vector2 GetSteeredDirection(Vector2 origin, Vector2 desiredDirection, LayerMask obstacleMask, float probeDistance = 1.6f, float wallMargin = 1.1f, Transform self = null, float personalSpace = 1.3f)
        {
            if (desiredDirection.sqrMagnitude < 0.0001f)
                return Vector2.zero;

            desiredDirection = desiredDirection.normalized;

            // 0) กู้ตัวเองก่อนเลย ถ้าเผลอไปอยู่ "ข้างใน" กำแพงพอดี (เช่นโดนหมุนเร็วๆ แล้วหลุดทะลุเข้าไป)
            // Raycast ปกติตรวจจับกำแพงที่ตัวเองอยู่ข้างในไม่ได้ (ข้อจำกัดของ Physics2D) จึงต้องเช็คแยกด้วย OverlapCircle
            // แล้วใช้ทิศจากจุดศูนย์กลางกำแพงมาที่ตัวเราเป็นทิศ "หนีออก" แทนการคำนวณแบบปกติ
            Collider2D stuckInside = Physics2D.OverlapCircle(origin, 0.1f, obstacleMask);
            if (stuckInside != null)
            {
                Vector2 escapeDir = origin - (Vector2)stuckInside.bounds.center;
                if (escapeDir.sqrMagnitude < 0.0001f)
                    escapeDir = desiredDirection;
                return escapeDir.normalized;
            }

            // 1) แรงผลักจากกำแพงที่อยู่ใกล้ๆ
            Vector2 avoidPush = ComputeAvoidancePush(origin, obstacleMask, wallMargin);

            // 2) แรงผลักจากพันธมิตรตัวอื่นที่อยู่ใกล้เกินไป (ถ้ามี GroupAI และระบุ self มา)
            Vector2 separationPush = Vector2.zero;
            if (self != null && GroupAI.Instance != null)
                separationPush = GroupAI.Instance.ComputeSeparationPush(origin, self, personalSpace);

            Vector2 blendedDirection = (desiredDirection + avoidPush + separationPush).normalized;
            if (blendedDirection.sqrMagnitude < 0.0001f)
                blendedDirection = desiredDirection;

            // 3) ใช้ Whisker Raycast/Collider Cast ตรวจสอบทิศทางที่ผสมแล้ว
            // Collider Cast สำคัญกว่าการยิงจากจุดกลาง เพราะศัตรูมีขนาดจริง
            Collider2D selfCollider = self != null ? self.GetComponent<Collider2D>() : null;
            var castHits = new RaycastHit2D[1];
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = obstacleMask,
                useTriggers = false
            };

            foreach (float angle in WhiskerAngles)
            {
                Vector2 testDir = angle == 0f ? blendedDirection : blendedDirection.Rotate(angle);
                bool blocked = selfCollider != null
                    ? selfCollider.Cast(testDir, filter, castHits, probeDistance) > 0
                    : Physics2D.Raycast(origin, testDir, probeDistance, obstacleMask).collider != null;

                if (!blocked)
                {
                    // ทิศนี้โล่ง ใช้ได้เลย (ลำดับ WhiskerAngles เรียงจากใกล้ทิศเดิมไปไกลสุด)
                    return testDir;
                }
            }

            // ทุกทิศติดหมด (โดนล้อมมุมอับ) ให้หยุดนิ่งแทนที่จะฝืนเดินเข้าไปในกำแพง
            return Vector2.zero;
        }

        /// <summary>
        /// ยิงเรย์สั้นๆ รอบตัว 8 ทิศ หากำแพงที่อยู่ใกล้กว่าระยะ margin แล้วคืนแรงผลักออกจากกำแพงนั้น
        /// ยิ่งกำแพงใกล้เท่าไหร่ แรงผลักยิ่งแรง ทำให้เดินโค้งอ้อมออกห่างก่อนจะถึงตัวกำแพงจริงๆ
        /// </summary>
        private static Vector2 ComputeAvoidancePush(Vector2 origin, LayerMask obstacleMask, float margin)
        {
            Vector2 push = Vector2.zero;

            foreach (float angle in ProbeAngles)
            {
                Vector2 dir = Vector2.up.Rotate(angle);
                RaycastHit2D hit = Physics2D.Raycast(origin, dir, margin, obstacleMask);

                if (hit.collider != null)
                {
                    float closeness = 1f - (hit.distance / margin); // 0 = เพิ่งเข้าเขต margin, 1 = ชิดกำแพงพอดี
                    push -= dir * closeness;
                }
            }

            return push;
        }

        // ─────────────────────────────────────────────────────────────────
        // Move With Collision Check (กันมุดกำแพงแบบ "หน้าแหลม")
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// ย้ายตำแหน่งแบบ "เช็คก่อนก้าว" — ตรวจว่าก้าวนี้จะพาตัวไปชนกำแพงหรือไม่ ก่อนย้ายจริง
        ///
        /// ปัญหาที่แก้: สไปรต์หน้าแหลม (ปากยื่นเกินกรอบ collider) มุดเข้ากำแพงได้ก่อนที่
        /// ระบบชนจะหยุด เพราะระบบชนหยุดเมื่อ "collider" แตะ ไม่ใช่เมื่อ "ภาพ" แตะ
        /// วิธีนี้จึงเว้นระยะ (padding) จากกำแพงมากกว่าขนาด collider จริงเสมอ
        ///
        /// ทุก state ที่เดินด้วย transform.position ควรเรียกเมธอดนี้แทนการ + position ตรงๆ
        /// เพื่อให้ทุกตัวมีพฤติกรรมเดียวกัน (Sup / Flanker / Tank ไม่ต่างกัน)
        /// </summary>
        /// <param name="self">Transform ของผู้เดิน</param>
        /// <param name="step">เวกเตอร์การก้าว (ทิศ × ระยะในเฟรมนี้)</param>
        /// <param name="obstacleMask">Layer กำแพง</param>
        /// <param name="wallPadding">ระยะเผื่อรอบตัวนอกเหนือจาก collider (กันปลายแหลมพ้นกรอบ)</param>
        /// <returns>ตำแหน่งใหม่ที่ปลอดภัย — ถ้าก้าวตรงตัน จะลองเลี้ยวทแยงซ้าย/ขวาให้เอง
        /// ถ้าทุกทิศตัน คืนตำแหน่งเดิม (ยืนรอ ไม่ฝืนมุดกำแพง)</returns>
        public static Vector2 MoveWithCollisionCheck(
            Transform self, Vector2 step, LayerMask obstacleMask, float wallPadding = 0.15f)
        {
            Vector2 from = self.position;
            float distance = step.magnitude;
            if (distance < 0.0001f) return from;

            float radius = GetBodyRadius(self) + wallPadding;
            Vector2 dir = step / distance;

            // ทางตรงโล่ง? → เดินได้เลย
            if (!IsPathBlocked(from, dir, distance, radius, obstacleMask))
                return from + step;

            // ทางตรงตัน → ลองเลี้ยวทแยงซ้าย/ขวา (เดินอ้อมต่อ ไม่หยุดค้าง)
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2[] alternatives =
            {
                (dir + perp * 0.9f).normalized,
                (dir - perp * 0.9f).normalized,
                perp,
                -perp
            };

            foreach (var alt in alternatives)
            {
                if (!IsPathBlocked(from, alt, distance, radius, obstacleMask))
                    return from + alt * distance;
            }

            // ทุกทิศตัน → อยู่กับที่ (ดีกว่ามุดกำแพง)
            return from;
        }

        /// <summary>แนวทางจาก → ปลายทาง ตัดผ่านกำแพงหรือเปล่า (ใช้ CircleCast ขนาดเท่าตัว)</summary>
        private static bool IsPathBlocked(Vector2 from, Vector2 dir, float distance, float radius, LayerMask obstacleMask)
        {
            if (distance < 0.0001f) return false;

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = obstacleMask,
                useTriggers = false
            };
            var hits = new RaycastHit2D[4];

            int count = Physics2D.CircleCast(from, radius, dir, filter, hits, distance + 0.05f);
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null) return true;

            // จุดปลายทางต้องไม่อยู่ในสิ่งกีดขวางด้วย
            return PhysicsUtility.IsPositionBlocked(from + dir * distance, radius, obstacleMask);
        }

        /// <summary>รัศมีครอบตัวของ collider (อ่านจาก bounds จริง — คลุมทั้งตัวไม่ว่าสไปรต์จะแหลม)</summary>
        public static float GetBodyRadius(Transform self)
        {
            var collider = self.GetComponent<Collider2D>();
            if (collider == null) return 0.5f;
            var e = collider.bounds.extents;
            return Mathf.Max(e.x, e.y);
        }
    }
}

using UnityEngine;

namespace TopDownTacticalAI.Utilities
{
    public static class PhysicsUtility
    {
        // ต้องมีค่าอย่างน้อยเท่ากับ wallMargin ที่ใช้ใน SteeringMovement (0.7f)
        // ไม่งั้นจุดหมายที่เลือกได้อาจอยู่ในระยะที่ระบบเดินจะคอยผลักออกตลอด ทำให้เดินไปไม่ถึงจุดจริงๆ สักที
        // หรือถึงจุดแล้วแต่จุดนั้นชิดกำแพงเกินไป ดูเหมือนติดกำแพงอยู่
        private const float DefaultWallClearance = 1.2f;

        /// <summary>ตรวจสอบว่าตำแหน่งที่ระบุมีสิ่งกีดขวางอยู่หรือไม่ (ใช้เช็คก่อนวางจุด Cover/Search)</summary>
        public static bool IsPositionBlocked(Vector2 position, float checkRadius, LayerMask obstacleMask)
        {
            return Physics2D.OverlapCircle(position, checkRadius, obstacleMask) != null;
        }

        public static bool TryFindNearestValidPoint(Vector2 origin, float searchRadius, LayerMask obstacleMask, out Vector2 result, int attempts = 12)
        {
            for (int i = 0; i < attempts; i++)
            {
                Vector2 candidate = origin + Random.insideUnitCircle * searchRadius;
                if (!IsPositionBlocked(candidate, DefaultWallClearance, obstacleMask))
                {
                    result = candidate;
                    return true;
                }
            }
            result = origin;
            return false;
        }

        /// <summary>
        /// หาจุดที่ "เอนเอียง" ไปทางทิศทางที่กำหนด (เช่น ทิศที่ผู้เล่นเคยวิ่งหนีไปล่าสุด)
        /// แทนที่จะสุ่มรอบทิศทาง 360 องศาเท่าๆ กัน ใช้ตอน Search เพื่อเดาว่าผู้เล่นน่าจะหนีไปทางไหนต่อ
        /// </summary>
        /// <param name="biasDirection">ทิศทางที่อยากให้เอนเอียงไป (ถ้าเป็น Vector2.zero จะสุ่มรอบทิศทางปกติแทน)</param>
        /// <param name="coneAngle">มุมกรวยรอบ biasDirection ที่ยอมให้สุ่ม (องศา) ยิ่งน้อยยิ่งตรงทิศ</param>
        public static bool TryFindDirectionalPoint(Vector2 origin, float searchRadius, Vector2 biasDirection, LayerMask obstacleMask, out Vector2 result, float coneAngle = 100f, int attempts = 12)
        {
            if (biasDirection.sqrMagnitude < 0.01f)
                return TryFindNearestValidPoint(origin, searchRadius, obstacleMask, out result, attempts);

            biasDirection.Normalize();

            for (int i = 0; i < attempts; i++)
            {
                float angleOffset = Random.Range(-coneAngle * 0.5f, coneAngle * 0.5f);
                Vector2 dir = biasDirection.Rotate(angleOffset);
                float distance = Random.Range(searchRadius * 0.4f, searchRadius);
                Vector2 candidate = origin + dir * distance;

                if (!IsPositionBlocked(candidate, DefaultWallClearance, obstacleMask))
                {
                    result = candidate;
                    return true;
                }
            }

            // หาตามทิศทางไม่เจอเลย ลองสุ่มรอบทิศทางปกติแทนเป็นทางเลือกสำรอง
            return TryFindNearestValidPoint(origin, searchRadius, obstacleMask, out result, attempts);
        }
    }
}

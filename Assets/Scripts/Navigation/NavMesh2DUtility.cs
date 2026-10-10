using UnityEngine;
using UnityEngine.AI;

namespace TopDownTacticalAI.Pathfinding
{
    /// <summary>
    /// ฟังก์ชันช่วยตรวจหา/ใช้งาน NavMesh ระนาบ 2D (XY) ร่วมกันระหว่างฝั่ง Editor (ตอน Bake)
    /// และฝั่ง Runtime (ตอน EnemyMover ตรวจว่าฉากมี NavMesh ให้ใช้)
    ///
    /// สำคัญ: ต้องเลือก agentTypeID ตัวเดียวกับตอน Bake ไม่งั้น Agent ยืนไม่ติด NavMesh
    /// (isOnNavMesh = false เสมอ) — เดิมมีสูตรนี้ซ้ำกันใน NavMesh2DSetup.PickAgentTypeID
    /// จึงยกมาไว้ที่เดียวเพื่อไม่ให้สองฝั่งขัดกัน
    /// </summary>
    public static class NavMesh2DUtility
    {
        /// <summary>
        /// เลือก agent type ที่เหมาะกับสเกลแมพ (radius ≤ 2)
        /// ไม่ใช้ "Humanoid" ที่ตั้ง radius=50 เพราะ navmesh จะถูกกัดร่นจากกำแพง 50 หน่วย
        /// ทำให้แมพใหญ่เหลือพื้นเดินแคบ/ขาดตอน
        /// </summary>
        public static int GetCompatibleAgentTypeId()
        {
            int fallback = 0;
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                NavMeshBuildSettings s = NavMesh.GetSettingsByIndex(i);
                if (i == 0) fallback = s.agentTypeID;
                if (s.agentRadius <= 2f)
                    return s.agentTypeID;
            }
            return fallback;
        }

        /// <summary>ตรวจว่ามี NavMesh ให้ยืนอยู่ใกล้ตำแหน่งที่กำหนดไหม (ใช้ตอน Start ของแต่ละตัว)</summary>
        public static bool IsNavWorldAvailable(Vector2 near, float radius)
        {
            return NavMesh.SamplePosition(near, out _, radius, NavMesh.AllAreas);
        }

        /// <summary>หาจุดบน NavMesh ที่ใกล้กับตำแหน่งที่กำหนด — คืน false ถ้าไม่เจอในรัศมีที่กำหนด</summary>
        public static bool TrySamplePosition(Vector2 near, float radius, out Vector3 navPosition)
        {
            if (NavMesh.SamplePosition(near, out NavMeshHit hit, radius, NavMesh.AllAreas))
            {
                navPosition = hit.position;
                return true;
            }

            navPosition = near;
            return false;
        }

        // buffer path กลางสำหรับคำนวณความยาวเส้นทาง (single-threaded)
        private static readonly NavMeshPath s_measurePath = new NavMeshPath();

        /// <summary>
        /// ความยาว "เส้นทางเดินจริง" ระหว่างสองจุด (หน่วยโลก) — หลบสิ่งกีดขวางแล้ว
        /// ไม่ใช่ระยะเส้นตรง (Euclidean) — ใช้ตอนให้คะแนนเป้าหมายว่า "เดินไปถึงยากแค่ไหน"
        ///
        /// คืน fallback เมื่อ: ไม่มี NavMesh ในฉาก / เส้นทางไม่สมบูรณ์ (ไม่มีทางถึงกัน)
        /// หมายเหตุ: s_measurePath.corners คืน array ใหม่ (API ของ Unity)
        /// — เรียกเฉพาะตอน re-eval เป้าฮีล (~0.3 วิ) ไม่ใช่ทุกเฟรม จึงรับได้
        /// </summary>
        public static float PathLength(Vector2 from, Vector2 to, float fallback)
        {
            if ((from - to).sqrMagnitude < 0.0001f) return 0f;

            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, s_measurePath))
                return fallback;
            if (s_measurePath.status == NavMeshPathStatus.PathInvalid)
                return fallback;

            Vector3[] corners = s_measurePath.corners;
            if (corners.Length < 2) return fallback;

            float length = 0f;
            for (int i = 1; i < corners.Length; i++)
                length += Vector3.Distance(corners[i - 1], corners[i]);

            return length > 0.01f ? length : fallback;
        }
    }
}

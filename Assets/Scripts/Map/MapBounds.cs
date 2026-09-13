 using UnityEngine;

namespace TopDownTacticalAI.Map
{
    /// <summary>
    /// กำหนดขอบเขตของแมพแบบ Global เพียงจุดเดียว ใช้ควบคุมไม่ให้ผู้เล่น/ศัตรูเดินหลุดออกนอกแมพ
    /// วิธีใช้: สร้าง Empty GameObject ชื่อ "MapBounds" วางไว้ในฉาก แปะสคริปต์นี้ แล้วปรับ Size/Center
    /// ให้ครอบคลุมพื้นที่เล่นทั้งหมด (ดูกรอบสีเขียวใน Scene View เพื่อเช็คเทียบกับแมพจริง)
    ///
    /// ระบบทำงาน 2 ชั้นร่วมกัน:
    /// 1) สร้าง "กำแพงล่องหน" จริง (BoxCollider2D 4 ด้าน) รอบขอบเขตอัตโนมัติตอนเริ่มเกม — กันได้จริงด้วยฟิสิกส์
    ///    ผู้เล่น (มี Rigidbody2D) จะชนแล้วหยุดเองตามธรรมชาติ ส่วนศัตรู (ใช้ Raycast เลี่ยงสิ่งกีดขวาง) ก็จะเห็นกำแพงนี้เป็น Obstacle ปกติ
    /// 2) PlayerController และ EnemyBrain ยังเรียก MapBounds.Clamp() เป็น Fail-safe สำรอง เผื่อกรณีหลุดผ่านกำแพงไปได้ (เช่น Dash เร็วมากๆ)
    /// </summary>
    public class MapBounds : MonoBehaviour
    {
        public static MapBounds Instance { get; private set; }

        [Header("ขนาดขอบเขตแมพ (World Units)")]
        [Tooltip("จุดศูนย์กลางของพื้นที่เล่น")]
        public Vector2 Center = Vector2.zero;
        [Tooltip("ความกว้าง x ความสูง ของพื้นที่เล่นทั้งหมด")]
        public Vector2 Size = new Vector2(40f, 24f);

        [Tooltip("เว้นระยะขอบเล็กน้อยกันตัวละครติดขอบพอดี (ใช้กับระบบ Clamp สำรองเท่านั้น)")]
        public float Padding = 0.3f;

        [Header("กำแพงล่องหน (Physical Walls)")]
        [Tooltip("ถ้าเปิดไว้ จะสร้าง BoxCollider2D ล้อมรอบขอบเขตอัตโนมัติตอนเริ่มเกม (แนะนำให้เปิดไว้)")]
        public bool GeneratePhysicalWalls = true;

        [Tooltip("ต้องตรงกับ Layer ที่ตั้งไว้ใน ObstacleMask ของ EnemyBrain ไม่งั้นศัตรูจะไม่เห็นกำแพงนี้")]
        public string ObstacleLayerName = "Obstacle";

        [Tooltip("ความหนาของกำแพงล่องหน (ยิ่งหนายิ่งกันการดันทะลุได้ดีขึ้น)")]
        public float WallThickness = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (GeneratePhysicalWalls)
                GenerateWalls();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public Vector2 MinBounds => Center - Size * 0.5f + Vector2.one * Padding;
        public Vector2 MaxBounds => Center + Size * 0.5f - Vector2.one * Padding;

        /// <summary>บีบตำแหน่งให้อยู่ในกรอบแมพ ถ้าไม่มี MapBounds ในฉากจะคืนค่าตำแหน่งเดิมโดยไม่ทำอะไร</summary>
        public static Vector2 Clamp(Vector2 position)
        {
            if (Instance == null) return position;

            Vector2 min = Instance.MinBounds;
            Vector2 max = Instance.MaxBounds;

            return new Vector2(
                Mathf.Clamp(position.x, min.x, max.x),
                Mathf.Clamp(position.y, min.y, max.y)
            );
        }

        private void GenerateWalls()
        {
            int layer = LayerMask.NameToLayer(ObstacleLayerName);
            if (layer == -1)
            {
                Debug.LogWarning($"[MapBounds] ไม่พบ Layer ชื่อ '{ObstacleLayerName}' — ไปสร้าง Layer นี้ที่ Edit > Project Settings > Tags and Layers ก่อน ไม่งั้นกำแพงล่องหนจะไม่ทำงาน (ศัตรู/Vision/กระสุน จะมองไม่เห็นกำแพงนี้)");
                return;
            }

            // ใช้ขอบเขตจริงตาม Center/Size (ไม่หัก Padding) เพื่อให้กำแพงอยู่ตรงขอบพอดี
            Vector2 min = Center - Size * 0.5f;
            Vector2 max = Center + Size * 0.5f;
            float width = max.x - min.x;
            float height = max.y - min.y;

            CreateWall("Wall_Top", new Vector2(Center.x, max.y + WallThickness * 0.5f), new Vector2(width + WallThickness * 2f, WallThickness), layer);
            CreateWall("Wall_Bottom", new Vector2(Center.x, min.y - WallThickness * 0.5f), new Vector2(width + WallThickness * 2f, WallThickness), layer);
            CreateWall("Wall_Left", new Vector2(min.x - WallThickness * 0.5f, Center.y), new Vector2(WallThickness, height + WallThickness * 2f), layer);
            CreateWall("Wall_Right", new Vector2(max.x + WallThickness * 0.5f, Center.y), new Vector2(WallThickness, height + WallThickness * 2f), layer);
        }

        private void CreateWall(string name, Vector2 position, Vector2 size, int layer)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(transform);
            wall.transform.position = position;
            wall.layer = layer;

            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Center, Size);
        }
    }
}

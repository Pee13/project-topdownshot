using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using TopDownTacticalAI.Map;
using TopDownTacticalAI.Pathfinding;

/// <summary>
/// เครื่องมือตั้งค่า NavMesh 2D ให้ฉาก TEST ทั้งระบบ — เมนู: Tools > NavMesh 2D > Setup + Bake (TEST Scene)
/// รันซ้ำได้ (จะลบของเดิมแล้วสร้างใหม่ทั้งหมด)
///
/// ทำไมต้องสร้าง proxy เอง (ไม่ใช้ NavMeshPlus หรือ package เพิ่ม):
/// ตัว NavMeshSurface ของ Unity เก็บเฉพาะ geometry 3D (BoxCollider/Mesh) เท่านั้น —
/// "มองไม่เห็น" Collider2D / SpriteRenderer เลย จึงต้องสร้าง BoxCollider ครอบพื้นกับกำแพง 2D แทน
///
/// ขั้นตอน:
/// 1) สร้างพื้น walkable (BoxCollider ขนาดเท่า MapBounds หน้าบนชิด z=0 ตรงตำแหน่งตัวละคร)
/// 2) สร้าง BoxCollider ครอบกำแพงทุกชิ้น (ชั้น Wall/Obstacle) + NavMeshModifier = Not Walkable
///    (กันหน้าบนของกำแพงกลายเป็นพื้นเดินได้ลอยฟ้า)
/// 3) สร้าง NavMeshSurface หมุน 90° บนแกน X → ระนาบ navmesh ตรงกับระนาบ XY ของเกม2D (local up = world +Z)
///    (ใน Unity 6 บั๊ก agent แกน Y ตอนหมุนแบบนี้แก้แล้ว)
/// 4) Bake แล้วตรวจว่ามี walkable area จริง
/// 5) สร้างตัวทดสอบ NavMeshTestAgent (เดินไล่ผู้เล่น + วาดเส้นทาง)
/// 6) บันทึกฉาก
/// </summary>
public static class NavMesh2DSetup
{
    private const string ScenePath = "Assets/Scenes/Test Game/TEST.unity";
    private const string SurfaceName = "NavMesh2D";
    private const string GeometryName = "NavMesh2D_Geometry";
    private const string AgentName = "NavMeshTestAgent";

    [MenuItem("Tools/NavMesh 2D/Setup + Bake (TEST Scene)")]
    public static void SetupAndBake()
    {
        SetupAndBakeScene(ScenePath, spawnTestAgent: true);
    }

    /// <summary>
    /// Bake ให้ "ฉากที่เปิดอยู่ตอนนี้" — ใช้ตอนขยาย NavMesh ไปฉากอื่น (เช่น Level1)
    /// ต่างจากเมนู TEST ตรงที่ไม่สร้างตัวทดสอบ NavMeshTestAgent (ใช้ศัตรูจริงในฉากแทน)
    /// </summary>
    [MenuItem("Tools/NavMesh 2D/Setup + Bake (Active Scene)")]
    public static void SetupAndBakeActive()
    {
        SetupAndBakeScene(null, spawnTestAgent: false);
    }

    /// <summary>
    /// เครื่องมือตั้ค่า NavMesh 2D ให้ฉากทั้งระบบ — รันซ้ำได้ (จะลบของเดิมแล้วสร้างใหม่ทั้งหมด)
    ///
    /// ทำไมต้องสร้าง proxy เอง (ไม่ใช้ NavMeshPlus หรือ package เพิ่ม):
    /// ตัว NavMeshSurface ของ Unity เก็บเฉพาะ geometry 3D (BoxCollider/Mesh) เท่านั้น —
    /// "มองไม่เห็น" Collider2D / SpriteRenderer เลย จึงต้องสร้าง BoxCollider ครอบพื้นกับกำแพง 2D แทน
    ///
    /// ขั้นตอน:
    /// 1) สร้างพื้น walkable (BoxCollider ขนาดเท่า MapBounds หน้าบนชิด z=0 ตรงตำแหน่งตัวละคร)
    /// 2) สร้าง BoxCollider ครอบกำแพงทุกชิ้น (ชั้น Wall/Obstacle) + NavMeshModifier = Not Walkable
    ///    (กันหน้าบนของกำแพงกลายเป็นพื้นเดินได้ลอยฟ้า)
    /// 3) สร้าง NavMeshSurface หมุน 90° บนแกน X → ระนาบ navmesh ตรงกับระนาบ XY ของเกม2D (local up = world +Z)
    ///    (ใน Unity 6 บั๊ก agent แกน Y ตอนหมุนแบบนี้แก้แล้ว)
    /// 4) Bake แล้วตรวจว่ามี walkable area จริง
    /// 5) สร้างตัวทดสอบ NavMeshTestAgent (เฉพาะเมนู TEST — เดินไล่ผู้เล่น + วาดเส้นทาง)
    /// 6) บันทึกฉาก
    /// </summary>
    /// <param name="scenePath">path ของฉากที่จะเปิด/ทำ — null = ใช้ฉากที่เปิดอยู่ (ห้ามมีฉากแก้ค้างโดยไม่ save)</param>
    /// <param name="spawnTestAgent">สร้างตัวทดสอบเดินไล่ผู้เล่นด้วยไหม (เมนู TEST เท่านั้น)</param>
    private static void SetupAndBakeScene(string scenePath, bool spawnTestAgent)
    {
        // ── 1) เปิดฉากเป้าหมาย (ถ้าระบุ path ไว้และยังไม่ได้เปิด) ──
        if (scenePath != null && !EditorSceneManager.GetActiveScene().path.EndsWith(System.IO.Path.GetFileName(scenePath)))
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        else if (scenePath == null && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return; // เมนู Active Scene: ต้อง save/ยอม discard ก่อน ไม่งั้นของเดิมที่ค้างจะหาย
        }

        // ── 2) ล้างของเดิม (รันซ้ำได้ ไม่สร้างซ้ำซ้อน) ──
        foreach (string n in new[] { SurfaceName, GeometryName, AgentName })
        {
            GameObject old = GameObject.Find(n);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
        }

        // ── 3) พื้น walkable ขนาดเท่าพื้นที่แมพ (อ่านจาก MapBounds ถ้ามีในฉาก) ──
        MapBounds mb = UnityEngine.Object.FindFirstObjectByType<MapBounds>();
        Vector2 mapCenter = mb != null ? mb.Center : Vector2.zero;
        Vector2 mapSize = mb != null ? mb.Size : new Vector2(500f, 425f);
        const float groundTopZ = 0f; // ระดับหน้าบนของพื้น = z 0 ตรงกับตำแหน่งตัวละคร (ตั้งค่า EnemyBrain ฯลฯ อยู่ที่ z=0)

        var geomRoot = new GameObject(GeometryName);

        var floor = new GameObject("Floor");
        floor.transform.SetParent(geomRoot.transform);
        var floorCol = floor.AddComponent<BoxCollider>();
        floorCol.size = new Vector3(mapSize.x, mapSize.y, 1f);
        floorCol.center = new Vector3(mapCenter.x, mapCenter.y, groundTopZ - 0.5f); // หน้าบนชิด z = 0

        // ── 4) สร้าง BoxCollider ครอบกำแพง 2D ทุกชิ้น (ชั้น Wall / Obstacle) ──
        int wallLayerMask = 0;
        int wallLayer = LayerMask.NameToLayer("Wall");
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (wallLayer >= 0) wallLayerMask |= 1 << wallLayer;
        if (obstacleLayer >= 0) wallLayerMask |= 1 << obstacleLayer;

        int proxyCount = 0;
        foreach (BoxCollider2D c2 in UnityEngine.Object.FindObjectsByType<BoxCollider2D>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (c2.isTrigger) continue; // trigger = จุดเช็คอย่างอื่น ไม่ใช่กำแพงจริง
            if ((wallLayerMask & (1 << c2.gameObject.layer)) == 0) continue;

            Bounds b = c2.bounds; // world AABB (หมุนในระนาบ XY ไม่มีผลกับ AABB แนวนอน)
            var w = new GameObject($"WallProxy_{proxyCount++}");
            w.transform.SetParent(geomRoot.transform);
            var c3 = w.AddComponent<BoxCollider>();
            c3.size = new Vector3(Mathf.Max(b.size.x, 0.1f), Mathf.Max(b.size.y, 0.1f), 1f);
            c3.center = new Vector3(b.center.x, b.center.y, groundTopZ + 0.5f); // ยืนจากพื้น z0 สูงขึ้น 1 หน่วย

            // หน้าบนของกำแพงห้ามเป็นพื้นเดินได้ (กัน navmesh ลอยฟ้าบนยอดกำแพง)
            var mod = w.AddComponent<NavMeshModifier>();
            mod.overrideArea = true;
            mod.area = 1; // 1 = Not Walkable (ดู NavMeshAreas.asset)
        }

        // ── 5) NavMeshSurface หมุนระนาบให้ตรงกับเกม2D ──
        // Euler(90,0,0): local up (0,1,0) → world (0,0,1)=+Z → ระนาบ navmesh = ระนาบ XY ของเกม
        var surfaceGo = new GameObject(SurfaceName);
        surfaceGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
        var surface = surfaceGo.AddComponent<NavMeshSurface>();

        int agentTypeID = PickAgentTypeID();
        surface.agentTypeID = agentTypeID;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; // เก็บจาก BoxCollider ที่สร้างไว้ข้อ 3-4
        surface.collectObjects = CollectObjects.All;
        surface.layerMask = ~0;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.25f; // ละเอียดพอสำหรับ radius ของ agent ที่เลือก (ทุกอย่างเป็นสเกลหน่วยใหญ่)
        surface.defaultArea = 0;   // Walkable
        surface.BuildNavMesh();

        if (surface.navMeshData == null)
        {
            Debug.LogError("[NavMesh2D] Bake ล้มเหลว (navMeshData = null) — ตรวจว่า BoxCollider ถูกสร้างครบ (Floor + WallProxy)");
            return;
        }

        int vertCount = 0;
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        if (tri.vertices != null) vertCount = tri.vertices.Length;
        if (vertCount == 0)
        {
            Debug.LogError("[NavMesh2D] Bake ผ่านแต่ไม่มีพื้นที่ walkable เลย (vertices = 0) — เช็ค agent radius กับขนาดพื้น/ช่องว่างระหว่างกำแพง");
            return;
        }

        // ── 6) สร้างตัวทดสอบเดินไล่ผู้เล่น (เฉพาะเมนู TEST — ฉากจริงใช้ศัตรูจริง) ──
        if (spawnTestAgent)
        {
            Vector3 spawn = new Vector3(mapCenter.x, mapCenter.y, 0f);
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                spawn = player.transform.position + new Vector3(25f, 15f, 0f);
            if (NavMesh.SamplePosition(spawn, out NavMeshHit hitSpawn, 100f, NavMesh.AllAreas))
                spawn = hitSpawn.position;
            spawn.z = 0f;

            var agentGo = new GameObject(AgentName);
            agentGo.transform.position = spawn;
            agentGo.transform.localScale = Vector3.one * 4f;

            var sr = agentGo.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSquareSprite();
            sr.color = new Color(0.95f, 0.35f, 0.9f, 1f);
            sr.sortingOrder = 100; // เหนือ sprite อื่น (ทุก object ในฉากใช้ sorting layer Default)

            var agent = agentGo.AddComponent<NavMeshAgent>();
            agent.agentTypeID = agentTypeID; // ต้องตรงกับที่ bake — ไม่งั้น agent ยืนไม่ติด navmesh (isOnNavMesh = false)
            agent.speed = 40f;          // สเกลแมพใหญ่ (ViewRadius 200, ChaseSpeed 60)
            agent.angularSpeed = 720f;
            agent.acceleration = 512f;
            agent.stoppingDistance = 6f;
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agentGo.AddComponent<NavMeshTestAgent>();
        }

        // ── 7) บันทึกฉาก ──
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log($"[NavMesh2D] Bake สำเร็จ: vertices={vertCount}, wallProxies={proxyCount}, agentTypeID={agentTypeID} | " +
                  $"Scene view = เห็นพื้นสีฟ้า (NavMesh) — กลับ Game view แล้วกด Play ดู {AgentName} เดินไล่ผู้เล่น");
    }

    /// <summary>
    /// เลือก agent type ที่เหมาะกับสเกลแมพ — ใช้สูตรกลางจาก NavMesh2DUtility
    /// เพื่อให้ฝั่ง Bake (Editor) กับฝั่ง Runtime (AgentPathMover) เลือกตัวเดียวกันเสมอ
    /// (ไม่งั้น agent ยืนไม่ติด navmesh)
    /// </summary>
    private static int PickAgentTypeID() => NavMesh2DUtility.GetCompatibleAgentTypeId();

    private static Sprite LoadSquareSprite()
    {
        // sprite สี่เหลี่ยมมาตรฐานที่มีในโปรเจกต์ (ใช้อยู่ในฉาก TEST แล้ว)
        string path = AssetDatabase.GUIDToAssetPath("311925a002f4447b3a28927169b83ea6");
        if (!string.IsNullOrEmpty(path))
        {
            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) return sp;
        }

        // fallback: สร้าง sprite ขาว 1x1 จาก white texture (กัน sprite = null แล้วมองไม่เห็นตัวทดสอบ)
        return Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}

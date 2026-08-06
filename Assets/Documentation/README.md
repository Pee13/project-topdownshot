# TopDown Tactical AI Framework — Unity 6 (2D)

Framework AI สำหรับเกม Top-down Shooter ตามที่ออกแบบไว้ในเอกสาร: State Machine
พร้อมระบบ Vision, Memory, Patrol, Chase, Search, Combat, Cover, Dodge, Tactical Decision,
Animation, Audio และ Debug ครบทุกระบบ

## 1. วิธีติดตั้ง

1. เปิดโปรเจกต์ Unity 6 (2D Core Template)
2. คัดลอกโฟลเดอร์ `Assets/Scripts` ทั้งหมดไปวางใน `Assets/` ของโปรเจกต์คุณ
3. รอให้ Unity Compile Script เสร็จ (เช็คว่าไม่มี Error ใน Console)

## 2. โครงสร้างสคริปต์

```
Assets/Scripts
├── Core        -> EnemyBrain, StateMachine, Blackboard, IAIState, EnemyState
├── Vision      -> VisionSensor, FieldOfView, RaycastDetector, TargetDetector, VisionDebugger
├── Memory      -> EnemyMemory, LastSeenData, TargetMemory, MemoryTimer
├── Patrol      -> PatrolState, WaypointPatrol, RandomPatrol, IdleLookAround
├── Chase       -> ChaseState, ChaseMovement, TargetPrediction, PathFollowing
├── Search      -> SearchState, SearchPattern, Investigation, ReturnPatrol
├── Combat      -> CombatState, AimController, ShootController, ReloadController, AttackDecision
├── Cover       -> CoverState, CoverScanner, CoverPoint, CoverDecision, PeekSystem, RetreatSystem
├── Dodge       -> DodgeState, BulletDetector, DodgeDecision, DodgeMovement, SafePositionFinder
├── Tactical    -> TacticalDecision, PrioritySystem, RiskEvaluation, Flanking, GroupAI
├── Animation   -> AnimationController, AimAnimation, MovementAnimation
├── Audio       -> FootstepAudio, AlertAudio, CombatAudio
├── Debug       -> StateDebugger, MemoryDebugger, CoverDebugger, GizmosDrawer
├── Utilities   -> MathUtility, PhysicsUtility, TimerUtility, ExtensionMethods
└── Player      -> PlayerController, PlayerShoot, Health, Bullet
```

`EnemyBrain.cs` (ใน Core) คือจุดศูนย์กลางที่ประกอบทุกระบบเข้าด้วยกัน
และรัน Logic ตามลำดับที่ระบุในเอกสาร (Patrol → Vision → Chase → Memory → Search → Combat/Cover/Dodge)

## 3. ตั้งค่า Layer ก่อนใช้งาน (สำคัญมาก)

ไปที่ **Edit > Project Settings > Tags and Layers** แล้วสร้าง Layer ต่อไปนี้:

| Layer            | ใช้กับ                                  |
|------------------|------------------------------------------|
| `Player`         | ตัวผู้เล่น (สำหรับ TargetMask)            |
| `Obstacle`       | กำแพง/สิ่งกีดขวาง (สำหรับ ObstacleMask)   |
| `Cover`          | จุดกำบัง (สำหรับ CoverMask)               |
| `PlayerBullet`   | กระสุนผู้เล่น (สำหรับ PlayerBulletMask)   |
| `EnemyBullet`    | กระสุนศัตรู (ป้องกันศัตรูยิงโดนกันเอง)     |

## 4. ตั้งค่า Player

1. สร้าง GameObject ชื่อ `Player`
2. เพิ่ม Component: `Rigidbody2D` (Body Type = Dynamic, Gravity Scale = 0), `Collider2D`
3. ตั้ง Layer = `Player`
4. แปะสคริปต์ `PlayerController.cs` และ `PlayerShoot.cs` และ `Health.cs`
5. สร้าง Child Object ชื่อ `MuzzlePoint` ไว้ปลายปืน ลาก Reference ไปใส่ใน `PlayerShoot`
6. สร้าง Bullet Prefab (วงกลมเล็กๆ + Rigidbody2D + Collider2D isTrigger + `Bullet.cs`) ตั้ง Layer = `PlayerBullet`
7. อย่าลืมแปะ `MapBoundary/BoundsClamper.cs` ด้วย (ดูข้อ 4.6) ถ้าไม่อยากให้ผู้เล่นเดินหลุดออกนอกแมพ

## 4.5 ตั้งค่ากล้องตามผู้เล่น (Camera Follow)

1. เลือก `Main Camera` ใน Hierarchy
2. แปะสคริปต์ `CameraSystem/CameraFollow.cs`
3. ลาก Transform ผู้เล่นมาใส่ช่อง `Target`
4. ปรับ `Smooth Time` ตามชอบ (ค่าเริ่มต้น 0.15 วิ)

## 4.6 ตั้งค่ากรอบเขตแมพ (Map Bounds) — กันผู้เล่น/ศัตรูเดินหลุดแมพ

1. สร้าง Empty GameObject ชื่อ `MapBounds` วางไว้ที่ไหนก็ได้ในฉาก
2. แปะสคริปต์ `Map/MapBounds.cs`
3. ปรับ `Center` และ `Size` ใน Inspector ให้กรอบสีเขียวใน Scene View ครอบคลุมพื้นที่เล่นทั้งหมดพอดี
4. เช็คว่า `Obstacle Layer Name` ตรงกับ Layer ที่ตั้งไว้ใน `Obstacle Mask` ของ `EnemyBrain` (ค่าเริ่มต้นคือ `Obstacle`)
5. กด Play — ระบบจะสร้าง **กำแพงล่องหน 4 ด้าน** (BoxCollider2D) ล้อมรอบขอบเขตให้อัตโนมัติทันที ไม่ต้องสร้างเอง

ผู้เล่นจะชนกำแพงนี้แล้วหยุดเองตามธรรมชาติ (เพราะมี Rigidbody2D + Collider ชนกันจริง) ส่วนศัตรูก็จะเลี่ยงกำแพงนี้เหมือนสิ่งกีดขวางทั่วไป (เพราะอยู่ Layer `Obstacle` เดียวกัน) และยังมี `MapBounds.Clamp()` เป็น Fail-safe สำรองอีกชั้น เผื่อกรณีหลุดผ่านไปได้ (เช่น Dash เร็วมากๆ)

**หมายเหตุ:** กำแพงที่สร้างอัตโนมัตินี้เป็น Runtime-only (สร้างตอนกด Play เท่านั้น ไม่ถูกบันทึกลง Scene ถาวร) ถ้าไม่อยากให้สร้างอัตโนมัติ ปิด `Generate Physical Walls` ใน Inspector แล้วสร้างกำแพงเองรอบขอบแมพแทนได้

## 5. ตั้งค่า Enemy

1. สร้าง GameObject ชื่อ `Enemy`
2. เพิ่ม Component: `Rigidbody2D`, `Collider2D`, `Health.cs`
3. แปะสคริปต์หลัก **`EnemyBrain.cs`** (จะเพิ่ม VisionSensor / EnemyMemory ให้อัตโนมัติ)
4. ตั้งค่าใน Inspector ของ `EnemyBrain`:
   - `Patrol Waypoints` — ลาก Transform จุดต่างๆ ที่ต้องการให้เดินวน (หรือเว้นว่างไว้ให้เดินสุ่ม)
   - `Eye Point` / `Muzzle Point` — สร้าง Child Object แล้วลากมาใส่
   - `Bullet Prefab` — ใช้ Prefab เดียวกับของผู้เล่นได้ หรือสร้างแยกสำหรับศัตรู (ตั้ง Layer = `EnemyBullet`)
   - `Target Mask` = Player, `Obstacle Mask` = Obstacle, `Cover Mask` = Cover, `Player Bullet Mask` = PlayerBullet
5. (ทางเลือก) แปะ `AnimationController.cs` ถ้ามี Animator + Parameters: `State` (int), `Speed` (float), `IsAiming` (bool)
6. (ทางเลือก) แปะ `AlertAudio.cs`, `CombatAudio.cs`, `FootstepAudio.cs` พร้อมใส่ AudioClip
7. อย่าลืมแปะ `MapBoundary/BoundsClamper.cs` ด้วย (ดูข้อ 4.6) ถ้าไม่อยากให้มอนเดินหลุดออกนอกแมพ

## 6. ตั้งค่า Cover Points (ทางเลือก)

1. สร้าง GameObject เปล่าตามจุดที่ต้องการให้เป็นที่กำบัง (ข้างกำแพง/ลัง)
2. เพิ่ม `Collider2D` (ติ๊ก Is Trigger) + ตั้ง Layer = `Cover`
3. แปะสคริปต์ `CoverPoint.cs` (และ `CoverDebugger.cs` ถ้าต้องการเห็นสถานะใน Scene View)

## 7. ตั้งค่า Group AI (ทางเลือก สำหรับศัตรูหลายตัวล้อมผู้เล่น)

1. สร้าง Empty GameObject ชื่อ `GameManager` แปะสคริปต์ `GroupAI.cs`
2. ต้องมีอยู่ในฉากก่อนที่ Enemy จะ `Awake()` (วางไว้บนสุดของ Hierarchy หรือกำหนด Script Execution Order)

## 8. ทดสอบเกม

1. กด Play
2. เดินด้วย WASD, เล็ง/ยิงด้วยเมาส์
3. เดินเข้าใกล้ระยะมองเห็นของศัตรู (วงสีเหลืองใน Scene View ถ้าแปะ `VisionDebugger`) จะเห็น AI เปลี่ยนจาก Patrol → Chase → Combat/Cover/Dodge ตามสถานการณ์

## 9. ดูว่า AI กำลังคิดอะไรอยู่ (AI Debug HUD)

แปะสคริปต์ `DebugTools/AIDebugDisplay.cs` เพิ่มที่ตัวศัตรู (ต้องมี `EnemyBrain` อยู่ในตัวเดียวกันอยู่แล้ว) แล้วกด Play จะเห็นกล่องข้อความลอยเหนือหัวศัตรูใน **Game View** บอกว่า:

- อยู่ State ไหน (ลาดตระเวน / ไล่ตาม / ค้นหา / ปะทะ / หลบกำบัง / หลบกระสุน) พร้อมสีประจำ State
- เห็นผู้เล่นอยู่ไหม / จำตำแหน่งล่าสุดได้ไหม
- ระยะห่างจากผู้เล่น, HP, กระสุน
- **"คิดว่า:" เหตุผลที่ตัดสินใจแบบนี้** เช่น "เห็นผู้เล่นแต่ระยะไกลเกินไป → วิ่งเข้าไปไล่ตาม"

ปรับได้ในช่อง Inspector: `Compact Mode` (โชว์แค่ชื่อ State สั้นๆ), `Screen Offset`, `Font Size`
ถ้าเลือกตัวศัตรูไว้ใน Scene View จะเห็นเส้นสีม่วงชี้ไปยังตำแหน่งล่าสุดที่จำไว้ด้วย

**ข้อควรระวังเรื่อง Font:** สคริปต์นี้วาดผ่าน `OnGUI` (IMGUI) ซึ่งใช้ฟอนต์ Built-in ของ Unity ที่**อาจไม่รองรับภาษาไทย**ในบางเวอร์ชัน/แพลตฟอร์ม ถ้าเปิดมาแล้วข้อความไทยเป็นกล่องสี่เหลี่ยม (☐☐☐) ให้สร้าง `GUISkin` ที่กำหนด Font ไทยแล้วโหลดเข้ามาใช้ในสคริปต์นี้แทน (บอกได้ถ้าต้องการให้ช่วยปรับส่วนนี้)

## 11. แก้ปัญหาที่พบบ่อย (Troubleshooting)

**กำแพง/สิ่งกีดขวางเห็นใน Scene View แต่หายไปตอน Play (Game View)**
เช็ค **Main Camera > Culling Mask** ต้องติ๊ก Layer ที่กำแพงใช้อยู่ด้วย (เช่น `Obstacle`) — Scene View จะโชว์ทุกอย่างเสมอไม่สนใจ Culling Mask แต่ Game View จริงจะโชว์เฉพาะ Layer ที่กล้องอนุญาต ถ้ากำแพงเป็น GameObject ที่วาดเอง อย่าลืมเช็คด้วยว่ามี `Sprite Renderer` ติดอยู่จริง (กำแพงล่องหนที่ `MapBounds.cs` สร้างอัตโนมัติมีแค่ Collider ไม่มี Sprite ตั้งใจให้มองไม่เห็นอยู่แล้ว อันนี้ปกติ)

**ตัวละคร (ผู้เล่น/ศัตรู) ไหลซ้ายขวาหรือหมุนควงสวิงตอนยืนนิ่งๆ**
สาเหตุคือ `Rigidbody2D` เป็น Dynamic แล้วแรงชนจาก Physics แย่งคุมตำแหน่ง/การหมุนกับโค้ด AI/Player ที่เขียนทับตรงๆ ทุกเฟรม ตอนนี้แก้ให้อัตโนมัติแล้ว:
- `EnemyBrain.Awake()` จะบังคับ Rigidbody2D ของศัตรูเป็น **Kinematic** เสมอ (ถ้ามี) และล็อคการหมุน
- `PlayerController.Awake()` จะล็อคการหมุนของผู้เล่น (`Freeze Rotation`) และเปิด `Interpolate` ให้อัตโนมัติ

ถ้าอัปเดตโค้ดแล้วยังมีอาการอยู่ ลองเช็คว่า Rigidbody2D ตัวอื่นๆ ในฉาก (เช่นกระสุน, ที่กำบัง) ไม่ได้ตั้งเป็น Dynamic โดยไม่จำเป็นด้วย

## 13. ระบบใหม่ที่เพิ่มล่าสุด

**ค้นหาแบบสำรวจทีละเส้นทาง (Search)** — เจอผู้เล่นแล้วหาย จะเดินไปตำแหน่งล่าสุด มองซ้ายขวา แล้วเดินสำรวจไปทีละจุดตามทิศที่คาดว่าผู้เล่นวิ่งหนี ถ้าจุดนั้นไม่เจอ (หรือติดทางตัน) จะ**เดินย้อนกลับจุดศูนย์กลางก่อน** แล้วค่อยไปสำรวจเส้นทางถัดไป ไม่ใช่กระโดดไปมาแบบสุ่ม

**ลาดตระเวนแบบครอบคลุมพื้นที่ (`Patrol/CoveragePatrol.cs`)** — แทน RandomPatrol เดิม สร้างจุดลาดตระเวนกระจายทั่วพื้นที่ล่วงหน้า (แบบเมล็ดทานตะวัน) แล้ววนไปทีละจุดตามลำดับอย่างเป็นระบบ จุดไหนไปไม่ถึง (ทางตัน) ก็ข้ามไปจุดถัดไป ปรับจำนวนจุด/รัศมีได้ที่ `Patrol Radius` และ `Patrol Point Count` ใน `EnemyBrain`

**เรียกพวกเมื่อโดนโจมตี หรือเจอผู้เล่นครั้งแรก (Group Alert)** — เมื่อศัตรูโดนดาเมจ **หรือ** เพิ่งเห็นผู้เล่นครั้งแรก จะ "ตะโกน" แจ้งเพื่อนที่อยู่ในระยะ `Alert Shout Radius` (ปรับได้ใน `EnemyBrain`) ให้รู้ตำแหน่งภัยคุกคามทันที แม้เพื่อนจะยังไม่เห็นผู้เล่นด้วยตาตัวเอง (จะเข้า Search ไปตรวจสอบจุดนั้น) เพื่อนที่อยู่ไกลเกินรัศมีจะไม่รู้เรื่อง — ต้องมี `GroupAI` อยู่ในฉาก (ดูข้อ 7)

**เดินหลบแทน Dash แกว่งซ้ายขวา (Dodge)** — เปลี่ยนจากการ Dash กระตุกเป็นการเดินไปยังตำแหน่งปลอดภัยจริง (คำนวณครั้งเดียวตอนเริ่มหลบ ตรวจสอบไม่ให้ชนกำแพง) ใช้ระบบเลี่ยงกำแพงเดียวกับการเดินปกติ ลดโอกาสทะลุกำแพงและดูเป็นธรรมชาติกว่าเดิม

**กระสุนปักค้างแทนหายทันที (`Player/Bullet.cs`)** — กระสุนที่ชนอะไรก็ตาม (กำแพง/ศัตรู/ผู้เล่น) จะหยุดนิ่งและ "ปักค้าง" อยู่ตรงจุดนั้น (ฝากตัวไว้กับสิ่งที่โดนชน) แล้วค่อยหายไปเองหลังผ่านไป `Stick Duration` วินาที (ปรับได้ที่ตัว Bullet Prefab)

**เดินอ้อมกำแพงแบบมีระยะห่าง ไม่ชิด/ไม่สะบัดหมุน (`Utilities/SteeringMovement.cs`)** — ของเดิมใช้ Whisker Raycast เลือกมุมโล่งอย่างเดียว พอเดินเข้าใกล้กำแพงมากๆ การเลือกมุมจะสลับข้างไปมาได้ง่ายทุกเฟรม (ดูเหมือนติดสะบัดหมุน) ตอนนี้เพิ่ม "แรงผลักต่อเนื่อง" จากกำแพงที่อยู่ใกล้ (ปรับที่พารามิเตอร์ `wallMargin` ในโค้ด ค่าเริ่มต้น 0.7) ทำให้เดินโค้งอ้อมกำแพงออกห่างตามธรรมชาติ แทนที่จะเดินชิดจนแทบติดผิวกำแพง

**วิ่งไล่ต่อก่อนเริ่มค้นหา (`SearchState.cs`)** — พอเสียสายตาผู้เล่น (เช่นเลี้ยวมุมกำแพงไป) ถ้ารู้ทิศทางที่ผู้เล่นกำลังวิ่งอยู่ตอนนั้น จะ**วิ่งไล่ต่อไปอีก `Search Pursuit Overshoot` หน่วย** ตามทิศนั้นก่อน (เหมือนคนไล่ตามจริงๆ ที่อ้อมมุมแล้ววิ่งต่ออีกหน่อย) แทนที่จะหยุดมองหาตรงจุดที่เสียสายตาทันที ปรับได้ใน `EnemyBrain` พร้อมเพิ่มความจำเริ่มต้นเป็น `20 วินาที` (จาก 12) และเพิ่มจำนวนจุดค้นหาต่อรอบเป็น 6 จุด (จาก 4) ให้ค้นหาอึดขึ้น ไม่ยอมแพ้ง่ายๆ

**เข้าหาผู้เล่นแบบแบ่งบทบาท ไม่ซ้อนทับกัน (Flanking + Separation)** — เมื่อศัตรูหลายตัวไล่ผู้เล่นพร้อมกัน (ต้องมี `GroupAI` อยู่ในฉาก): ตัวแรกจะไล่ตรงเข้าหาผู้เล่นตามปกติ ส่วนตัวที่เหลือจะ**อ้อมไปดักคนละมุมรอบผู้เล่นแทน** (ปรับระยะที่จะอ้อมได้ที่ `Flank Radius`) เพื่อปิดทางหนี ไม่ใช่วิ่งไล่ทับเส้นทางเดียวกันหมด และทุกตัวจะมีแรงผลักกันไม่ให้เดินชิด/ซ้อนทับกันเกินไป (ปรับระยะห่างที่ `Personal Space`) ทั้งตอนไล่ตาม (Chase) และตอนต่อสู้ (Combat)

**ใช้ประวัติเส้นทางแทนความจำจุดเดียว (`Memory/TargetMemory.cs`)** — เดิมใช้ความเร็วเฟรมเดียวก่อนเสียสายตา (อาจสั่น/ไม่แม่น) ตอนนี้เก็บ "ประวัติเส้นทาง" (Breadcrumb Trail) ล่าสุดไว้ 8 จุด แล้วคำนวณทิศทางเฉลี่ยจากเส้นทางจริง ทำให้เดาทิศที่ผู้เล่นวิ่งหนีได้แม่นยำขึ้นมาก โดยเฉพาะถ้าผู้เล่นเดินอ้อมโค้งกำแพงมา (จะเดาทิศตามแนวโค้งได้ ไม่ใช่พุ่งตรงเป็นเส้นตรงเฉยๆ)

**จำกัดเวลาโหมดตื่นตัวแบบ MGS (`Search/SearchState.cs`)** — ค้นหาต่อเนื่องได้สูงสุด `Max Alert Duration` (ค่าเริ่มต้น **99 วินาที** ตามที่ขอ) ถ้าหาไม่เจอภายในเวลานี้ จะยอมแพ้และบังคับลืมความจำทันที กลับไปเฝ้าจุดเดิม (Patrol) เหมือนโหมด Alert ใน Metal Gear Solid ที่ลดระดับเตือนภัยกลับเป็นปกติเมื่อหาไม่เจอ (ก่อนหน้านี้ค่านี้ยังไม่เชื่อมกับระบบตัดสินใจจริงๆ ตอนนี้แก้ให้ทำงานแล้ว)

**คะแนนความพยายาม (Determination Score)** — เพิ่มตัวเลขสะสมใน Blackboard แสดงผลใน AI Debug HUD บอกว่า AI "พยายาม" ไล่ล่าผู้เล่นมากแค่ไหน: +15 ตอนเจอผู้เล่นครั้งแรก, +20 ตอนโดนโจมตีแล้วเรียกพวกมาช่วย, และสะสมต่อเนื่องขณะไล่ตาม/ค้นหา/ต่อสู้ ~2 แต้มต่อวินาที เป็นตัวเลขแสดงผลเชิงบรรยากาศ (ไม่กระทบ Gameplay โดยตรง) ถ้าต้องการนำไปใช้ปรับความยากหรือทำ Scoreboard ในอนาคตก็ต่อยอดจากตรงนี้ได้

## 14. จุดที่ควรต่อยอดเอง

- **Pathfinding**: `PathFollowing.cs` เป็น Steering แบบง่าย ถ้าฉากซับซ้อนแนะนำเปลี่ยนไปใช้ NavMesh2D หรือ A* Pathfinding Project
- **Object Pooling**: ตอนนี้ `ShootController`/`Bullet` ใช้ `Instantiate`/`Destroy` ตรงๆ ถ้ามีการยิงถี่มากควรทำ Pool
- **Animator Controller**: ต้องสร้างเอง (ไฟล์นี้ให้แค่ตัวส่งพารามิเตอร์)
- **Death Handling**: ปัจจุบัน `Health.OnDeath` เป็น UnityEvent เปล่า ให้ผูกกับการปิด `EnemyBrain`/เล่น Animation ตาย/Destroy ผ่าน Inspector หรือโค้ดเพิ่มเติม

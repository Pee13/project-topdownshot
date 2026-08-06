หมายเหตุสำคัญ: โฟลเดอร์ Assets/Code/ นี้เป็นคนละระบบกับ Assets/Scripts/
================================================================================

Assets/Scripts/  -> TopDownTacticalAI Framework (Namespace TopDownTacticalAI.*)
                    ระบบ AI หลักที่พัฒนาในบทสนทนานี้ (State Machine, Vision, Memory,
                    Patrol, Chase, Search, Combat, Cover, Dodge, Influence Map ฯลฯ)

Assets/Code/     -> ระบบ EnemyAI แยกต่างหาก (ไม่มี Namespace, ไฟล์ EnemyAI.cs ที่คุณมีอยู่แล้ว
                    ในโปรเจกต์จริงที่ path Assets\Code\Enemy\EnemyAI.cs)
                    ในนี้มีแค่ 3 ไฟล์ที่ EnemyAI.cs ต้องการแต่ยังไม่มี:

                    Assets/Code/Enemy/EnemyWeaponSystem.cs
                    Assets/Code/Enemy/EnemySquadManager.cs
                    Assets/Code/Player/PlayerStatus.cs

** ทั้งสองระบบนี้ไม่ได้เชื่อมต่อกันหรือทำงานร่วมกันแต่อย่างใด **
เป็นแค่การรวมไฟล์ไว้ในซิปเดียวกันเพื่อความสะดวกในการดาวน์โหลดเท่านั้น

วิธีติดตั้ง 3 ไฟล์ใน Assets/Code/:
1) EnemyWeaponSystem.cs และ EnemySquadManager.cs -> เอาไปวางที่ไหนก็ได้ใน Assets ของโปรเจกต์จริงคุณ
   (แนะนำโฟลเดอร์เดียวกับ EnemyAI.cs ที่คุณมีอยู่แล้ว)
2) PlayerStatus.cs -> เอาไปวางที่ไหนก็ได้ใน Assets เช่นกัน (แนะนำโฟลเดอร์ Player ของคุณ)
3) แปะ EnemyWeaponSystem.cs เพิ่มที่ "ตัวศัตรูทุกตัวที่มี EnemyAI ติดอยู่"
4) แปะ PlayerStatus.cs เพิ่มที่ "ตัวผู้เล่น" (Tag Player)
5) สร้าง Empty GameObject ใหม่ แปะ EnemySquadManager.cs (ตัวเดียวพอทั้งฉาก)

คู่มือติดตั้งระบบเมนูเกม (Main Menu / Level Select / Settings / Pause)
================================================================================

สคริปต์ในโฟลเดอร์นี้เป็น "ตรรกะ" (Logic) เท่านั้น — ไม่ได้สร้างหน้าตา UI จริงให้อัตโนมัติ
เพราะไฟล์ Scene/UI ของ Unity เป็นไฟล์ภาพซับซ้อนที่สร้างผ่านเครื่องมือ Text ไม่ได้
ต้องประกอบ Canvas/Panel/Button เองใน Editor ตามขั้นตอนด้านล่าง (ทำครั้งเดียว ไม่ยาก)

ไฟล์ทั้งหมดในโฟลเดอร์นี้:
  AudioManager.cs         - จัดการเสียง Music/SFX กลาง (Singleton ข้ามฉาก)
  SettingsManager.cs      - เก็บ "ค่า" การตั้งค่าทั้งหมด + Apply/Save/Load (Singleton ข้ามฉาก ไม่ถือ UI เอง)
  SettingsPanelBinder.cs  - แปะที่ Settings Panel แต่ละชุด เชื่อม UI ของ Panel นั้นกับ SettingsManager อัตโนมัติ
  FpsCounterDisplay.cs    - แสดง FPS มุมจอ (สร้างอัตโนมัติเมื่อเปิด Option "Show FPS")
  GameDifficulty.cs       - เก็บระดับความยากที่เลือกไว้ (Static Class ไม่ต้องแปะกับ GameObject)
  MainMenuController.cs   - ตัวควบคุมหน้าเมนูหลัก
  LevelSelectController.cs- ตัวควบคุมหน้าเลือกด่าน
  PauseMenu.cs             - เมนู Pause ระหว่างเล่น (กด Esc)


ขั้นตอนที่ 1: สร้าง Scene ใหม่ 2 ฉาก
--------------------------------------------------------------------------------
1) File > New Scene > ตั้งชื่อ "MainMenu" บันทึกไว้ใน Assets/Scenes/
2) File > New Scene > ตั้งชื่อ "LevelSelect" บันทึกไว้ใน Assets/Scenes/
3) เปิด File > Build Settings > ลากทั้ง 2 ฉากนี้ + ฉากเกมจริง (ด่านที่มีอยู่แล้ว) เข้าไปใน
   "Scenes In Build" — เรียงลำดับ: MainMenu ต้องอยู่บนสุด (Index 0) เพราะเกมจะเริ่มที่ฉากแรกในลิสต์นี้เสมอ
   ** สำคัญมาก: ชื่อ Scene ต้องตรงกับที่พิมพ์ไว้ในช่อง Inspector ของสคริปต์ทุกตัวอักษร (ตัวพิมพ์เล็ก/ใหญ่ด้วย) **


ขั้นตอนที่ 2: สร้าง Canvas หลักของฉาก MainMenu
--------------------------------------------------------------------------------
1) คลิกขวาใน Hierarchy > UI > Canvas (จะได้ Canvas + EventSystem มาด้วยอัตโนมัติ)
2) เลือก Canvas > Canvas Scaler (Component) > UI Scale Mode = "Scale With Screen Size"
   > Reference Resolution = 1920 x 1080 (กันขนาดเพี้ยนตอนเปลี่ยนความละเอียดจอ)
3) สร้าง Empty GameObject ชื่อ "MainMenuController" แปะสคริปต์ MainMenuController.cs
4) สร้าง Empty GameObject ชื่อ "SettingsManager" แปะสคริปต์ SettingsManager.cs
5) สร้าง Empty GameObject ชื่อ "AudioManager" แปะสคริปต์ AudioManager.cs
   แล้วเพิ่ม Component AudioSource 2 อัน (หรือสร้างเป็นลูก 2 ตัวก็ได้) ตั้ง Loop=true ให้ตัว Music
   ลากทั้งคู่มาใส่ช่อง Music Source / SFX Source ใน Inspector ของ AudioManager


ขั้นตอนที่ 3: สร้าง Panel "เมนูหลัก" (ลูกของ Canvas)
--------------------------------------------------------------------------------
1) คลิกขวาที่ Canvas > UI > Panel ตั้งชื่อ "MainMenuPanel"
2) ใส่ชื่อเกม (Text/TextMeshPro) ไว้ด้านบน
3) สร้างปุ่ม (UI > Button - TextMeshPro) 4 ปุ่ม ใต้ MainMenuPanel:
      - "เล่น"      -> OnClick() -> ลาก MainMenuController -> เลือก OnPlayButton()
      - "ตั้งค่า"    -> OnClick() -> ลาก MainMenuController -> เลือก OnSettingsButton()
      - "วิธีเล่น"   -> OnClick() -> ลาก MainMenuController -> เลือก OnControlsButton() (ไม่บังคับ)
      - "ออกจากเกม" -> OnClick() -> ลาก MainMenuController -> เลือก OnQuitButton()
4) ลาก MainMenuPanel มาใส่ช่อง "Main Menu Panel" ใน Inspector ของ MainMenuController


ขั้นตอนที่ 4: สร้าง Panel "ตั้งค่า" (Settings) — ลูกของ Canvas เช่นกัน (ซ่อนไว้ก่อน)
--------------------------------------------------------------------------------
** อัปเดตสำคัญ: ตอนนี้ Settings Panel แต่ละชุด (มีได้หลายชุด เช่น ชุดในเมนูหลัก + ชุดใน Pause Menu)
   ต้องแปะสคริปต์ SettingsPanelBinder.cs ไว้ที่ตัว Panel เอง แล้วลาก UI ของ Panel ชุดนั้นเข้าไปเฉพาะของมัน
   ไม่ใช่ลากไปใส่ที่ SettingsManager แบบเดิมอีกต่อไป (SettingsManager เก็บแค่ค่า ไม่ถือ Reference ของ UI แล้ว)
   ทำให้สร้าง Settings Panel กี่ชุดก็ได้ ทุกชุดจะซิงค์ค่ากันเองอัตโนมัติ ไม่มีปัญหา Reference หลุดหายอีก **

สร้าง Panel ใหม่ชื่อ "SettingsPanel" แปะสคริปต์ SettingsPanelBinder.cs ที่ตัว Panel นี้เอง
ใส่ Element เหล่านี้ (ทุกตัวเป็น UI มาตรฐานของ Unity):

  เสียง:
    - Slider "MasterVolumeSlider"   (Min Value=0, Max Value=1)
    - Slider "MusicVolumeSlider"    (Min Value=0, Max Value=1)
    - Slider "SFXVolumeSlider"      (Min Value=0, Max Value=1)

  หน้าจอ:
    - Dropdown (TMP) "ResolutionDropdown"
    - Toggle "FullscreenToggle"
    - Dropdown (TMP) "QualityDropdown"

  เสริม (Option ที่แนะนำเพิ่ม):
    - Slider "MouseSensitivitySlider" (Min Value=0.3, Max Value=3)
    - Toggle "ScreenShakeToggle"
    - Toggle "ShowFpsToggle"

  ปุ่มย้อนกลับ:
    - Button "กลับ" -> OnClick() -> ลาก MainMenuController -> เลือก OnSettingsBackButton()

จากนั้นลาก Slider/Dropdown/Toggle ทั้งหมดข้างต้น ไปใส่ในช่องที่ตรงชื่อกันใน Inspector
ของ GameObject "SettingsPanel" เอง (ช่อง SettingsPanelBinder ที่เพิ่งแปะไป ไม่ใช่ SettingsManager)
** ไม่ต้องผูก OnValueChanged() เองใน Inspector ก็ได้ สคริปต์ผูกให้อัตโนมัติตอน Panel เปิดขึ้นครั้งแรก **

ลาก SettingsPanel มาใส่ช่อง "Settings Panel" ใน Inspector ของ MainMenuController ด้วย
(ทำ Settings Panel ชุดที่ 2 สำหรับ Pause Menu แบบเดียวกันนี้ซ้ำอีกรอบ — ดูขั้นตอนที่ 7)


ขั้นตอนที่ 5: สร้าง Panel "ยืนยันออกจากเกม" (ไม่บังคับ แต่แนะนำ)
--------------------------------------------------------------------------------
Panel เล็กๆ ถามว่า "แน่ใจนะว่าจะออกจากเกม?" มี 2 ปุ่ม:
    - "ใช่" -> OnClick() -> MainMenuController -> OnQuitConfirmYes()
    - "ไม่"  -> OnClick() -> MainMenuController -> OnQuitConfirmNo()
ลากมาใส่ช่อง "Quit Confirm Panel" ใน Inspector ของ MainMenuController


ขั้นตอนที่ 6: ฉาก LevelSelect
--------------------------------------------------------------------------------
1) สร้าง Canvas เหมือนขั้นตอนที่ 2
2) สร้าง Empty GameObject "LevelSelectController" แปะสคริปต์ LevelSelectController.cs
3) สร้างปุ่มด่านตามจำนวนที่มี (เริ่มจากด่านเดียวก็ได้ เพิ่มทีหลังได้เรื่อยๆ)
   แต่ละปุ่ม -> OnClick() -> ลาก LevelSelectController -> เลือกฟังก์ชัน LoadLevel (string)
   -> พิมพ์ชื่อ Scene ของด่านนั้นในช่องที่โผล่มา (เช่น "Level1")
4) ปุ่ม "ย้อนกลับ" -> OnClick() -> LevelSelectController -> BackToMainMenu()
5) (ไม่บังคับ) สร้างปุ่ม/Toggle 3 อัน สำหรับเลือกความยาก -> ผูกกับ
   SetDifficultyEasy() / SetDifficultyNormal() / SetDifficultyHard()
   แล้วสร้าง Text แสดงระดับที่เลือกไว้ ลากมาใส่ช่อง "Difficulty Label"


ขั้นตอนที่ 7: เมนู Pause ในฉากเกมจริง (Option เสริม แนะนำให้ทำ)
--------------------------------------------------------------------------------
1) เปิดฉากเกมที่มี Player/Enemy อยู่แล้ว
2) สร้าง Canvas ใหม่ (หรือใช้ Canvas ของ HUD เดิมที่มีอยู่แล้วก็ได้) ชื่อ "PauseCanvas"
3) สร้าง Empty GameObject "PauseMenu" แปะสคริปต์ PauseMenu.cs
4) สร้าง Panel "PausePanel" (ซ่อนไว้ก่อน) มีปุ่ม:
     - "เล่นต่อ"        -> PauseMenu -> Resume()
     - "ตั้งค่า"          -> PauseMenu -> OpenSettingsFromPause()
     - "เล่นด่านนี้ใหม่"  -> PauseMenu -> RestartLevel()
     - "กลับเมนูหลัก"    -> PauseMenu -> ReturnToMainMenu()
     - "ออกจากเกม"       -> PauseMenu -> OnQuitButton()
5) ทำ Panel Settings ซ้ำเหมือนขั้นตอนที่ 4 อีกชุด (Copy Panel เดิมจากฉาก MainMenu มาวางก็ได้ เร็วกว่า
   แต่ SettingsPanelBinder.cs ที่ติดมากับ Panel ก็จะติดมาด้วยอัตโนมัติ ไม่ต้องแปะใหม่ ใช้ตัวเดิมที่ Copy มาได้เลย)
   ลากมาใส่ช่อง "Settings Panel" ของ PauseMenu แทน
6) ลาก PausePanel มาใส่ช่อง "Pause Panel" ใน Inspector ของ PauseMenu


หมายเหตุสำคัญ
--------------------------------------------------------------------------------
- ถ้ายังไม่เคยใช้ TextMeshPro ในโปรเจกต์มาก่อน Unity จะถามให้ Import "TMP Essentials"
  ตอนสร้างปุ่ม/Text แบบ TextMeshPro ครั้งแรก กด Import ได้เลย (ทำครั้งเดียว)

- GameDifficulty.cs ยังไม่ได้ผูกเข้ากับ EnemyBrain.cs/EnemyAI.cs จริง (แค่เก็บค่าไว้เฉยๆ)
  ถ้าอยากให้ระดับความยากมีผลกับ AI จริง (ความเร็ว/ความดุ) บอกได้เลย จะเพิ่มโค้ดเชื่อมให้

- ระบบ Settings ทั้งหมดใช้ PlayerPrefs บันทึกอัตโนมัติ ไม่ต้องกดปุ่ม "บันทึก" เอง
  เปลี่ยนค่าปุ๊บ บันทึกปั๊บ เปิดเกมใหม่ค่าที่ตั้งไว้จะกลับมาเองอัตโนมัติ
